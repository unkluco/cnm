"""Frozen ViT-Tiny inference and calibrated-template matching.

No optimization, gradient updates, classifier training, or remote inference.
The checkpoint is pinned and verified; only safetensors weights are loaded.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import tempfile
import threading
import time
import urllib.request
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F
import timm
from safetensors.torch import load_file

from runes import RUNES, TEMPLATE_COUNT, rasterize, reference_bank, stroke_metrics, is_too_short

ROOT = Path(__file__).resolve().parent
LOCAL = ROOT / ".local"
MODEL_REPO = "hasanfaesal/vit-tiny-patch16-224-dinov2-distill"
MODEL_REVISION = "82c08b3370e0f10c3726352c0b75ae39da034d69"
MODEL_SHA256 = "0bcaedfa2fefe1bd04625e2a361ba42c82509fcbf2796063f8f2f42d7e23b1b6"
MODEL_BYTES = 22111184
MODEL_URL = f"https://huggingface.co/{MODEL_REPO}/resolve/{MODEL_REVISION}/model.safetensors"
FEATURE_DIM = 192
# Synthetic pilot operating point; intentionally editable in the laboratory.
# Human validation (and actual device timing) is a separate next phase.
DEFAULT_THRESHOLD = 0.92
DEFAULT_MARGIN = 0.02
MEAN = np.array([0.485, 0.456, 0.406], dtype=np.float32)[None, :, None, None]
STD = np.array([0.229, 0.224, 0.225], dtype=np.float32)[None, :, None, None]


def sha256_of(path: Path) -> str:
    with path.open("rb") as handle:
        return hashlib.file_digest(handle, "sha256").hexdigest()


def ensure_weights(cache: Path = LOCAL / "model") -> Path:
    cache.mkdir(parents=True, exist_ok=True)
    path = cache / "vit-tiny-dinov2-distilled.safetensors"
    if path.is_file() and path.stat().st_size == MODEL_BYTES and sha256_of(path) == MODEL_SHA256:
        return path
    print("Downloading pinned ViT-Tiny safetensors (22.1 MB)…", flush=True)
    temp_name = None
    try:
        request = urllib.request.Request(MODEL_URL, headers={"User-Agent": "LightSouls-RuneLab/1.0"})
        with urllib.request.urlopen(request, timeout=60) as response:
            with tempfile.NamedTemporaryFile(prefix="download-", suffix=".part", dir=cache, delete=False) as out:
                temp_name = Path(out.name)
                digest = hashlib.sha256()
                written = 0
                while chunk := response.read(1024 * 1024):
                    written += len(chunk)
                    if written > MODEL_BYTES:
                        raise RuntimeError("Model download is unexpectedly large.")
                    digest.update(chunk)
                    out.write(chunk)
        if written != MODEL_BYTES or digest.hexdigest() != MODEL_SHA256:
            raise RuntimeError("Model checksum mismatch. The unverified download will not be loaded.")
        os.replace(temp_name, path)
        temp_name = None
        return path
    finally:
        if temp_name is not None:
            temp_name.unlink(missing_ok=True)


def checked_limits(threshold, margin) -> tuple[float, float]:
    if isinstance(threshold, bool) or isinstance(margin, bool):
        raise ValueError("Ngưỡng nhận dạng không hợp lệ.")
    try:
        threshold, margin = float(threshold), float(margin)
    except (TypeError, ValueError) as exc:
        raise ValueError("Ngưỡng nhận dạng không hợp lệ.") from exc
    if not (math.isfinite(threshold) and 0.0 <= threshold <= 1.0):
        raise ValueError("Ngưỡng similarity phải trong khoảng 0–1.")
    if not (math.isfinite(margin) and 0.0 <= margin <= 1.0):
        raise ValueError("Khoảng chênh phải trong khoảng 0–1.")
    return threshold, margin


def decide(scores: list[dict], threshold: float, margin: float, too_short=False) -> dict:
    """Reject weak or ambiguous matches. Runner-up is a different rune."""
    threshold, margin = checked_limits(threshold, margin)
    if too_short or not scores:
        return {"accepted": False, "rune_id": None, "reason": "too_short", "gap": None}
    first = scores[0]
    gap = first["score"] - scores[1]["score"] if len(scores) > 1 else 2.0
    if first["score"] < threshold:
        reason = "below_threshold"
    elif gap < margin:
        reason = "ambiguous"
    else:
        reason = "accepted"
    return {
        "accepted": reason == "accepted",
        "rune_id": first["id"] if reason == "accepted" else None,
        "reason": reason,
        "gap": float(gap),
    }


class RuneRecognizer:
    """Inference-only backbone; optionally load a separately trained candidate."""

    def __init__(self, device="cpu", threads=4, candidate_dir: Path | None = None):
        if device == "auto":
            device = "cuda" if torch.cuda.is_available() else "cpu"
        if device == "cuda" and not torch.cuda.is_available():
            raise RuntimeError("CUDA is not available in the installed PyTorch build.")
        self.device = torch.device(device)
        self.threads = max(1, min(threads, os.cpu_count() or 1))
        torch.set_num_threads(self.threads)
        self.lock = threading.Lock()
        self.default_threshold, self.default_margin = DEFAULT_THRESHOLD, DEFAULT_MARGIN
        self.candidate_dir = Path(candidate_dir).resolve() if candidate_dir is not None else None
        self.candidate_config = None
        self.reference_sha256 = None
        candidate_templates = None
        if self.candidate_dir is None:
            weights = ensure_weights()
            self.model_name = "ViT-Tiny / DINOv2 distilled"
            self.model_sha256 = MODEL_SHA256
        else:
            directory = self.candidate_dir
            metadata = json.loads((directory / "candidate-metadata.json").read_text(encoding="utf-8"))
            if (
                metadata.get("base_repo") != MODEL_REPO
                or metadata.get("base_revision") != MODEL_REVISION
                or metadata.get("base_sha256") != MODEL_SHA256
                or metadata.get("merged_parameters") != 5524416
            ):
                raise ValueError("Candidate must originate from the pinned Rune Lab ViT-Tiny backbone.")
            weights = directory / "candidate-merged.safetensors"
            bank_path = directory / "candidate-template-embeddings.npz"
            if weights.stat().st_size != metadata.get("merged_bytes"):
                raise ValueError("Candidate weight file size differs from the training metadata.")
            self.model_sha256 = sha256_of(weights)
            if self.model_sha256 == MODEL_SHA256:
                raise ValueError("Candidate weights are identical to the original frozen model.")
            self.reference_sha256 = sha256_of(bank_path)
            self.candidate_config = metadata["config"]
            self.model_name = f"ViT-Tiny + LoRA rank {self.candidate_config['rank']}"
            self.default_threshold, self.default_margin = checked_limits(metadata["threshold"], metadata["margin"])
            with np.load(bank_path, allow_pickle=False) as bank:
                labels = bank["labels"].tolist()
                candidate_templates = np.ascontiguousarray(bank["embeddings"], dtype=np.float32)
            if labels != [rune.id for rune in RUNES]:
                raise ValueError("Candidate template labels or order differ from the Rune Lab vocabulary.")
            if (
                candidate_templates.ndim != 3
                or candidate_templates.shape[0] != len(RUNES)
                or candidate_templates.shape[1] < TEMPLATE_COUNT
                or candidate_templates.shape[2] != FEATURE_DIM
                or candidate_templates.shape[0] * candidate_templates.shape[1] != metadata.get("reference_count")
                or not np.isfinite(candidate_templates).all()
                or not np.allclose(np.linalg.norm(candidate_templates, axis=-1), 1.0, atol=1e-3)
            ):
                raise ValueError("Candidate reference embeddings have invalid dimensions or normalization.")
        self.weights_bytes = weights.stat().st_size
        self.model = timm.create_model("vit_tiny_patch16_224", pretrained=False, num_classes=0)
        self.model.load_state_dict(load_file(str(weights), device="cpu"), strict=True)
        self.model.eval().requires_grad_(False).to(self.device)
        self.parameter_count = sum(parameter.numel() for parameter in self.model.parameters())
        bank = reference_bank()
        embeddings = self.encode_images([rasterize(path) for _, paths in bank for path in paths])
        expected_shape = (len(RUNES) * TEMPLATE_COUNT, FEATURE_DIM)
        if embeddings.shape != expected_shape:
            raise RuntimeError(f"Unexpected feature shape {embeddings.shape}; expected {expected_shape}.")
        canonical_embeddings = embeddings.reshape(len(RUNES), TEMPLATE_COUNT, FEATURE_DIM)
        if candidate_templates is None:
            self.templates = canonical_embeddings
        else:
            # The first five vectors per rune were computed from this exact
            # canonical bank during training. Verify the merged model and
            # supplied reference bank are a matching pair before activation.
            parity = float((canonical_embeddings * candidate_templates[:, :TEMPLATE_COUNT]).sum(axis=-1).min())
            if parity < 0.9999:
                raise ValueError(f"Candidate model and reference bank do not match (cosine={parity:.6f}).")
            self.templates = candidate_templates
            print(f"Candidate reference parity: {parity:.8f}", flush=True)
        self.reference_count = int(self.templates.shape[0] * self.templates.shape[1])
        print(f"{self.model_name} ready: {self.parameter_count:,} parameters, {self.reference_count} reference embeddings, {self.device}.", flush=True)

    def info(self) -> dict:
        return {
            "model": self.model_name,
            "variant": "lora" if self.candidate_dir is not None else "frozen",
            "repo": MODEL_REPO,
            "revision": MODEL_REVISION,
            "base_sha256": MODEL_SHA256,
            "sha256": self.model_sha256,
            "reference_sha256": self.reference_sha256,
            "weights_bytes": self.weights_bytes,
            "parameters": self.parameter_count,
            "feature_dim": FEATURE_DIM,
            "image_size": 224,
            "device": str(self.device),
            "threads": self.threads,
            "reference_count": self.reference_count,
            "references_per_rune": int(self.templates.shape[1]),
            "candidate_config": self.candidate_config,
            "trainable": False,
        }

    def encode_images(self, images, batch_size=8) -> np.ndarray:
        if not images:
            return np.empty((0, FEATURE_DIM), dtype=np.float32)
        result = []
        with self.lock, torch.inference_mode():
            for offset in range(0, len(images), batch_size):
                batch = images[offset : offset + batch_size]
                array = np.stack([np.asarray(img.convert("RGB"), dtype=np.float32) for img in batch])
                array = array.transpose(0, 3, 1, 2) / 255.0
                array = np.ascontiguousarray((array - MEAN) / STD)
                tensor = torch.from_numpy(array).to(self.device)
                tokens = self.model.forward_features(tensor)
                features = F.normalize(tokens[:, 0], p=2, dim=-1)
                result.append(features.cpu().numpy())
        return np.concatenate(result, axis=0)

    def rank(self, embed: np.ndarray) -> list[dict]:
        # All embeddings are L2-normalized. Dot product = cosine similarity.
        values = np.clip(self.templates @ embed, -1.0, 1.0)
        class_scores = values.max(axis=1)
        ids = np.argsort(-class_scores, kind="stable")
        return [
            {
                "id": RUNES[index].id,
                "name": RUNES[index].name,
                "intent": RUNES[index].intent,
                "score": float(class_scores[index]),
                "reference": int(np.argmax(values[index])) + 1,
            }
            for index in ids
        ]

    def recognize(self, points: np.ndarray, threshold=None, margin=None) -> dict:
        threshold = self.default_threshold if threshold is None else threshold
        margin = self.default_margin if margin is None else margin
        threshold, margin = checked_limits(threshold, margin)
        start = time.perf_counter()
        metrics = stroke_metrics(points)
        if is_too_short(metrics):
            scores, model_ms, image = [], 0.0, None
        else:
            image = rasterize(points)
            infer_start = time.perf_counter()
            embed = self.encode_images([image], batch_size=1)[0]
            model_ms = (time.perf_counter() - infer_start) * 1000
            scores = self.rank(embed)
        return {
            "scores": scores,
            "decision": decide(scores, threshold, margin, too_short=not scores),
            "stroke": metrics,
            "model_ms": model_ms,
            "total_ms": (time.perf_counter() - start) * 1000,
            "threshold": threshold,
            "margin": margin,
            "image": image,
        }
