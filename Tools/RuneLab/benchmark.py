"""Offline frozen-model smoke test using *synthetic* strokes.

This is not human/mobile validation. No weights are trained or modified.
Calibration and validation use independent synthetic seeds.
"""

from __future__ import annotations

import argparse
import json
import math
import time
from collections import Counter, defaultdict

import numpy as np

from recognizer import DEFAULT_MARGIN, DEFAULT_THRESHOLD, LOCAL, RuneRecognizer, decide
from runes import RUNES, is_too_short, polyline, rasterize, stroke_metrics, variant


def unknown_shapes():
    t = np.linspace(0.0, 2.0 * math.pi, 101)
    return (
        ("crossed_loop", polyline([(0.15, 0.17), (0.85, 0.82), (0.15, 0.82), (0.85, 0.17), (0.15, 0.17)])),
        ("figure_eight", np.column_stack((0.5 + 0.30 * np.sin(t), 0.5 + 0.30 * np.sin(2 * t)))),
        ("ladder", polyline([(0.12, 0.12), (0.12, 0.83), (0.34, 0.83), (0.34, 0.24), (0.56, 0.24), (0.56, 0.78), (0.84, 0.78)])),
        ("double_v", polyline([(0.12, 0.18), (0.30, 0.80), (0.50, 0.22), (0.70, 0.80), (0.88, 0.18)])),
        ("loop_tail", polyline([(0.12, 0.85), (0.72, 0.14), (0.86, 0.42), (0.57, 0.60), (0.37, 0.35), (0.72, 0.14), (0.86, 0.84)])),
        ("irregular", polyline([(0.18, 0.14), (0.83, 0.65), (0.29, 0.62), (0.5, 0.88), (0.71, 0.26), (0.16, 0.35)])),
        ("hook", polyline([(0.34, 0.14), (0.34, 0.83), (0.79, 0.83)])),
        ("open_box", polyline([(0.18, 0.18), (0.82, 0.18), (0.82, 0.82), (0.18, 0.82)])),
    )


def synthetic_cases(seed, per_rune):
    rng = np.random.default_rng(seed)
    cases = []
    for rune in RUNES:
        for index in range(per_rune):
            strength = float(rng.uniform(1.0, 2.0))
            points = variant(rune.points, rng, strength)
            # A slight gap is natural when drawing a closed symbol.
            if rune.id in ("ring", "wall", "ward") and index % 3 == 0:
                points = points[:-int(rng.integers(1, 4))]
            cases.append({"expected": rune.id, "family": rune.id, "points": points})
    repetitions = max(4, per_rune // 2)
    for family, path in unknown_shapes():
        for _ in range(repetitions):
            cases.append({"expected": "invalid", "family": family, "points": variant(path, rng, 1.3)})
    for _ in range(repetitions * 2):
        vertices = rng.uniform(0.13, 0.87, (int(rng.integers(4, 9)), 2))
        cases.append({"expected": "invalid", "family": "scribble", "points": polyline(vertices)})
    for _ in range(repetitions):
        origin = rng.uniform(0.15, 0.80, 2)
        points = np.vstack((origin, origin + rng.uniform(0.00, 0.035, 2)))
        cases.append({"expected": "invalid", "family": "tiny_or_tap", "points": points})
    return cases


def infer_cases(recognizer, cases):
    images = []
    valid_indexes = []
    for index, case in enumerate(cases):
        if not is_too_short(stroke_metrics(case["points"])):
            images.append(rasterize(case["points"]))
            valid_indexes.append(index)
        case["scores"] = []
    started = time.perf_counter()
    embeddings = recognizer.encode_images(images)
    elapsed = (time.perf_counter() - started) * 1000.0
    for index, embedding in zip(valid_indexes, embeddings):
        cases[index]["scores"] = recognizer.rank(embedding)
    return elapsed


def evaluate(cases, threshold, margin, detailed=False):
    counts = Counter()
    per_rune = defaultdict(Counter)
    false_accepts = Counter()
    confusion = Counter()
    for case in cases:
        expected = case["expected"]
        scores = case["scores"]
        result = decide(scores, threshold, margin)
        if expected == "invalid":
            counts["invalid"] += 1
            if result["accepted"]:
                counts["invalid_false_accept"] += 1
                false_accepts[f'{case["family"]} → {result["rune_id"]}'] += 1
            else:
                counts["invalid_rejected"] += 1
        else:
            counts["valid"] += 1
            per_rune[expected]["total"] += 1
            top = scores[0]["id"] if scores else None
            if top == expected:
                counts["valid_top1"] += 1
                per_rune[expected]["top1"] += 1
            else:
                confusion[f"{expected} → {top}"] += 1
            if result["rune_id"] == expected:
                counts["valid_accepted_correct"] += 1
                per_rune[expected]["accepted_correct"] += 1
            elif result["accepted"]:
                counts["valid_wrong_accept"] += 1
                per_rune[expected]["wrong_accept"] += 1
            else:
                counts["valid_rejected"] += 1
                per_rune[expected]["rejected"] += 1
    valid, invalid = counts["valid"], counts["invalid"]

    def ratio(part, total):
        return float(part / total) if total else 0.0

    report = {
        "threshold": threshold,
        "margin": margin,
        "valid": valid,
        "invalid": invalid,
        "valid_top1_accuracy": ratio(counts["valid_top1"], valid),
        "valid_accepted_correct": ratio(counts["valid_accepted_correct"], valid),
        "valid_wrong_accept": ratio(counts["valid_wrong_accept"], valid),
        "valid_rejected": ratio(counts["valid_rejected"], valid),
        "invalid_false_accept": ratio(counts["invalid_false_accept"], invalid),
        "invalid_rejected": ratio(counts["invalid_rejected"], invalid),
    }
    if detailed:
        report["by_rune"] = {key: dict(value) for key, value in per_rune.items()}
        report["top1_confusions"] = dict(confusion.most_common())
        report["invalid_false_accept_families"] = dict(false_accepts.most_common())
    return report


def calibrate(cases):
    """Pick a synthetic-only candidate operating point. Reject-first trade-off."""
    candidates = []
    for threshold_int in range(70, 100):
        # Keep a real ambiguity guard in the candidate; never optimize it away.
        for margin_int in range(1, 21):
            result = evaluate(cases, threshold_int / 100.0, margin_int / 100.0)
            candidates.append(result)
    safe = [r for r in candidates if r["valid_wrong_accept"] <= 0.02 and r["invalid_false_accept"] <= 0.02]
    if safe:
        return max(safe, key=lambda r: (r["valid_accepted_correct"], -r["invalid_false_accept"], -r["valid_wrong_accept"], r["margin"]))
    return min(candidates, key=lambda r: (r["invalid_false_accept"] + r["valid_wrong_accept"], -r["valid_accepted_correct"]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--per-rune", type=int, default=16)
    parser.add_argument("--threads", type=int, default=4)
    parser.add_argument("--device", choices=("cpu", "cuda", "auto"), default="cpu")
    parser.add_argument("--seed", type=int, default=41127)
    args = parser.parse_args()
    if not 4 <= args.per_rune <= 100:
        parser.error("--per-rune must be between 4 and 100")
    recognizer = RuneRecognizer(args.device, args.threads)
    calibration = synthetic_cases(args.seed, args.per_rune)
    validation = synthetic_cases(args.seed + 1743, args.per_rune)
    print(f"Encoding {len(calibration)} synthetic calibration and {len(validation)} independent validation strokes…", flush=True)
    calib_batch_ms = infer_cases(recognizer, calibration)
    validation_batch_ms = infer_cases(recognizer, validation)
    candidate = calibrate(calibration)
    initial = evaluate(validation, 0.85, 0.08, detailed=True)
    current_defaults = evaluate(validation, DEFAULT_THRESHOLD, DEFAULT_MARGIN, detailed=True)
    tuned = evaluate(validation, candidate["threshold"], candidate["margin"], detailed=True)

    # Desktop single-query latency, separately from batched feature extraction.
    recognizer.recognize(RUNES[0].points)
    rng = np.random.default_rng(args.seed + 611)
    latency_cases = [variant(RUNES[i % len(RUNES)].points, rng, 1.35) for i in range(48)]
    latencies = [recognizer.recognize(path) for path in latency_cases]
    model_times = [r["model_ms"] for r in latencies]
    total_times = [r["total_ms"] for r in latencies]
    report = {
        "scope": "synthetic smoke test only; no human draws, no mobile timing, no training",
        "model": recognizer.info(),
        "per_rune": args.per_rune,
        "calibration_seed": args.seed,
        "validation_seed": args.seed + 1743,
        "calibration_batch_ms": calib_batch_ms,
        "validation_batch_ms": validation_batch_ms,
        "initial_validation": initial,
        "current_defaults_validation": current_defaults,
        "calibrated_candidate": candidate,
        "candidate_validation": tuned,
        "desktop_single_query": {
            "count": len(latencies),
            "model_median_ms": float(np.median(model_times)),
            "model_p95_ms": float(np.percentile(model_times, 95)),
            "total_median_ms": float(np.median(total_times)),
            "total_p95_ms": float(np.percentile(total_times, 95)),
        },
    }
    output = LOCAL / "reports" / "synthetic-benchmark.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    for label, result in (("Initial proposal", initial), ("Current lab defaults", current_defaults), ("Calibrated candidate (validation)", tuned)):
        print(f"{label} threshold={result['threshold']:.2f}, margin={result['margin']:.2f}:")
        print(
            f"  top1 {result['valid_top1_accuracy']:.1%}, "
            f"correct accepted {result['valid_accepted_correct']:.1%}, "
            f"wrong accepted {result['valid_wrong_accept']:.1%}, "
            f"invalid false accept {result['invalid_false_accept']:.1%}"
        )
    print("Desktop latency:", json.dumps(report["desktop_single_query"]))
    print(f"Full synthetic report: {output}", flush=True)


if __name__ == "__main__":
    main()
