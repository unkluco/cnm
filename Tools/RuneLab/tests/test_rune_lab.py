from __future__ import annotations

import json
import tempfile
import threading
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

import numpy as np

from recognizer import FEATURE_DIM, LOCAL, MODEL_SHA256, RuneRecognizer, decide
from runes import RUNES, TEMPLATE_COUNT, is_too_short, rasterize, reference_bank, stroke_metrics, validate_points
from run_lab import LabHandler, LabServer, LabState


class RasterizationTests(unittest.TestCase):
    def test_reference_bank_is_deterministic_and_has_five_per_rune(self):
        bank_a, bank_b = reference_bank(), reference_bank()
        self.assertEqual(len(bank_a), 12)
        for (rune_a, paths_a), (rune_b, paths_b) in zip(bank_a, bank_b):
            self.assertEqual(rune_a.id, rune_b.id)
            self.assertEqual(len(paths_a), TEMPLATE_COUNT)
            for a, b in zip(paths_a, paths_b):
                np.testing.assert_array_equal(a, b)
                self.assertFalse(is_too_short(stroke_metrics(a)))

    def test_translation_and_uniform_scale_do_not_change_model_image(self):
        original = RUNES[4].points
        transformed = (original - 0.5) * 0.43 + np.array([0.27, 0.68])
        a = np.asarray(rasterize(original), dtype=np.float32)
        b = np.asarray(rasterize(transformed), dtype=np.float32)
        self.assertEqual(a.shape, (224, 224))
        self.assertLess(np.abs(a - b).mean(), 0.2)

    def test_orientation_is_preserved_and_reverse_is_visually_equal(self):
        line_x, line_y = RUNES[0].points, RUNES[1].points
        a = np.asarray(rasterize(line_x), dtype=np.float32)
        b = np.asarray(rasterize(line_y), dtype=np.float32)
        self.assertGreater(np.abs(a - b).mean(), 10)
        reversed_a = np.asarray(rasterize(line_x[::-1]), dtype=np.float32)
        self.assertLess(np.abs(a - reversed_a).mean(), 0.5)

    def test_short_or_empty_strokes_are_rejected(self):
        for raw in ([], [[0.4, 0.4]], [[0.5, 0.5], [0.52, 0.51]]):
            self.assertTrue(is_too_short(stroke_metrics(validate_points(raw))))

    def test_malformed_coordinates_are_rejected(self):
        for value in (None, "bad", [[0, float("nan")]], [[True, 0]], [[-0.1, 0.5]], [[1, 2, 3]]):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    validate_points(value)

    def test_threshold_and_margin_are_independent(self):
        ranked = [{"id": "slash", "score": 0.92}, {"id": "diagonal", "score": 0.84}]
        self.assertTrue(decide(ranked, 0.85, 0.07)["accepted"])
        self.assertEqual(decide(ranked, 0.94, 0.05)["reason"], "below_threshold")
        self.assertEqual(decide(ranked, 0.85, 0.1)["reason"], "ambiguous")
        self.assertEqual(decide([], 0.85, 0.08)["reason"], "too_short")


class FrozenModelTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.recognizer = RuneRecognizer("cpu", 4)

    def test_model_is_frozen_and_reference_embeddings_are_normalized(self):
        r = self.recognizer
        self.assertEqual(r.info()["sha256"], MODEL_SHA256)
        self.assertEqual(r.parameter_count, 5524416)
        self.assertFalse(r.model.training)
        self.assertFalse(any(parameter.requires_grad for parameter in r.model.parameters()))
        self.assertEqual(r.templates.shape, (12, TEMPLATE_COUNT, FEATURE_DIM))
        np.testing.assert_allclose(np.linalg.norm(r.templates, axis=2), 1, atol=1e-5)

    def test_every_canonical_glyph_is_top_ranked(self):
        for rune in RUNES:
            result = self.recognizer.recognize(rune.points)
            self.assertEqual(result["scores"][0]["id"], rune.id)
            self.assertGreater(result["scores"][0]["score"], 0.999)
            self.assertEqual(len(result["scores"]), 12)

    def test_invalid_tap_skips_inference(self):
        result = self.recognizer.recognize(validate_points([[0.5, 0.5]]))
        self.assertEqual(result["model_ms"], 0.0)
        self.assertEqual(result["decision"]["reason"], "too_short")
        self.assertIsNone(result["image"])

    def test_many_request_threads_reuse_one_inference_worker(self):
        class ObservedRecognizer:
            def __init__(self, delegate):
                self.delegate = delegate
                self.thread_ids = set()

            def recognize(self, *args):
                self.thread_ids.add(threading.get_ident())
                return self.delegate.recognize(*args)

        observer = ObservedRecognizer(self.recognizer)
        LOCAL.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=LOCAL) as directory:
            state = LabState(observer, Path(directory))
            try:
                payload = {"points": RUNES[4].points.tolist()}
                with ThreadPoolExecutor(max_workers=8) as clients:
                    results = list(clients.map(lambda _: state.analyze(payload), range(24)))
                self.assertEqual(len(observer.thread_ids), 1)
                self.assertTrue(all(result["scores"][0]["id"] == "lightning" for result in results))
                self.assertEqual(state.sample_count(), 0)
            finally:
                state.close()

    def test_http_routes_analyze_save_export_and_validate(self):
        LOCAL.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=LOCAL) as directory:
            state = LabState(self.recognizer, Path(directory))
            server = LabServer(("127.0.0.1", 0), LabHandler)
            server.state = state
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            base = f"http://127.0.0.1:{server.server_port}"

            def post(path, data):
                req = Request(
                    base + path, data=json.dumps(data).encode("utf-8"),
                    headers={"Content-Type": "application/json"}, method="POST",
                )
                with urlopen(req, timeout=15) as response:
                    return json.load(response)

            try:
                with urlopen(base + "/api/catalog", timeout=10) as response:
                    catalog = json.load(response)
                self.assertEqual(len(catalog["runes"]), 12)
                self.assertEqual(catalog["samples_saved"], 0)
                self.assertEqual(catalog["model"]["feature_dim"], 192)
                with urlopen(base, timeout=10) as response:
                    self.assertIn("Rune Lab", response.read().decode("utf-8"))
                result = post("/api/recognize", {"points": RUNES[0].points.tolist(), "input_method": "mouse"})
                self.assertEqual(result["scores"][0]["id"], "slash")
                self.assertTrue(result["preview"].startswith("data:image/png;base64,"))
                self.assertGreater(result["model_ms"], 0)
                self.assertEqual(state.sample_count(), 0)  # No implicit capture.
                sample = dict(id=result["id"], participant="QA-only", expected="slash", threshold=0.85, margin=0.08)
                self.assertEqual(post("/api/samples", sample)["count"], 1)
                with self.assertRaises(HTTPError) as duplicate:
                    post("/api/samples", sample)
                self.assertEqual(duplicate.exception.code, 400)
                with self.assertRaises(HTTPError) as malformed:
                    post("/api/recognize", {"points": [[2, 2]]})
                self.assertEqual(malformed.exception.code, 400)
                with urlopen(base + "/api/samples.jsonl", timeout=10) as response:
                    saved = json.loads(response.read())
                self.assertEqual(saved["expected"], "slash")
                self.assertEqual(saved["participant"], "QA-only")
                self.assertEqual(saved["model"]["sha256"], MODEL_SHA256)
                self.assertEqual(len(saved["points"]), len(RUNES[0].points))
            finally:
                server.shutdown()
                server.server_close()
                thread.join(timeout=5)


if __name__ == "__main__":
    unittest.main()
