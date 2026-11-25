"""Export the verified merged rune candidate for Unity Sentis.

Only the merged model, normalized embeddings, canonical icon art, and
provenance are written into Assets. No user drawing coordinates, sample
labels, split manifests, or raw training records are exported.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort
import timm
import torch
import torch.nn as nn
import torch.nn.functional as F
from PIL import Image
from safetensors.torch import load_file

from recognizer import MEAN, STD, MODEL_REPO, MODEL_REVISION, MODEL_SHA256, MODEL_BYTES, sha256_of
from runes import RUNES, rasterize, reference_bank, variant


class ExportEncoder(nn.Module):
    def __init__(self, backbone):
        super().__init__()
        self.backbone = backbone

    def forward(self, image):
        # The runtime normalizes this 192-D CLS vector for cosine matching.
        return self.backbone.forward_features(image)[:, 0, :]


def input_tensor(images):
    array = np.stack([np.asarray(image.convert("RGB"), dtype=np.float32) for image in images])
    return torch.from_numpy(np.ascontiguousarray((array.transpose(0, 3, 1, 2) / 255.0 - MEAN) / STD))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("experiment", type=Path)
    parser.add_argument("--assets", type=Path, required=True)
    args = parser.parse_args()
    root = args.assets.resolve()
    target = args.experiment.resolve()
    metadata = json.loads((target / "candidate-metadata.json").read_text(encoding="utf-8"))
    if metadata.get("base_sha256") != MODEL_SHA256 or metadata.get("merged_parameters") != 5524416:
        raise ValueError("Unexpected merged ViT-Tiny candidate")
    weights = target / "candidate-merged.safetensors"
    if weights.stat().st_size != metadata.get("merged_bytes"):
        raise ValueError("Merged candidate size does not match metadata")
    with np.load(target / "candidate-template-embeddings.npz", allow_pickle=False) as data:
        embeddings = np.asarray(data["embeddings"], dtype="<f4", order="C")
        labels = list(data["labels"])
    if labels != [r.id for r in RUNES] or embeddings.shape != (12, 10, 192):
        raise ValueError("Expected exact trained bank: 12 glyphs x 10 x 192")
    if not np.isfinite(embeddings).all() or not np.allclose(np.linalg.norm(embeddings, axis=-1), 1, atol=1e-3):
        raise ValueError("Invalid reference vectors")

    torch.set_num_threads(4)
    torch.set_float32_matmul_precision("highest")
    timm.layers.set_fused_attn(False)
    backbone = timm.create_model("vit_tiny_patch16_224", pretrained=False, num_classes=0)
    backbone.load_state_dict(load_file(str(weights), device="cpu"), strict=True)
    backbone.eval().requires_grad_(False)
    model = ExportEncoder(backbone).eval()
    root.mkdir(parents=True, exist_ok=True)
    onnx_path = root / "ViTTinyRune.onnx"
    with torch.inference_mode():
        torch.onnx.export(
            model, torch.zeros(1, 3, 224, 224), str(onnx_path), opset_version=17,
            input_names=["ink"], output_names=["embedding"], do_constant_folding=True,
            export_params=True, dynamo=False, dynamic_axes=None,
        )
    graph = onnx.load(str(onnx_path))
    onnx.checker.check_model(graph)
    ops = sorted({node.op_type for node in graph.graph.node})
    options = ort.SessionOptions()
    options.intra_op_num_threads = 4
    session = ort.InferenceSession(str(onnx_path), sess_options=options, providers=["CPUExecutionProvider"])
    # Verify against the actual post-training backbone and the canonical half
    # of the exported embeddings, with additional synthetic shape variations.
    paths = [path for _, bank in reference_bank() for path in bank]
    rng = np.random.default_rng(7117)
    paths.extend(variant(rune.points, rng, 1.25) for rune in RUNES)
    min_parity = 1.0
    min_reference = 1.0
    max_abs = 0.0
    for index, path in enumerate(paths):
        x = input_tensor([rasterize(path)])
        with torch.inference_mode():
            expected = F.normalize(model(x), dim=-1).numpy()[0]
        actual = session.run(["embedding"], {"ink": x.numpy()})[0][0]
        actual /= np.linalg.norm(actual)
        min_parity = min(min_parity, float(expected @ actual))
        max_abs = max(max_abs, float(np.abs(expected - actual).max()))
        if index < 60:
            min_reference = min(min_reference, float(actual @ embeddings[index // 5, index % 5]))
    if min_parity < .9999 or min_reference < .9999:
        raise RuntimeError(f"ONNX parity failed: {min_parity}, bank={min_reference}")
    reference_path = root / "RuneReferences.bytes"
    reference_path.write_bytes(struct.pack("<4sIIII", b"LSRU", 1, 12, 10, 192) + embeddings.tobytes())
    glyph_dir = root / "Glyphs"
    glyph_dir.mkdir(exist_ok=True)
    for rune in RUNES:
        grayscale = rasterize(rune.points)
        alpha = Image.fromarray(255 - np.asarray(grayscale)).resize((128, 128), Image.Resampling.LANCZOS)
        image = Image.new("RGBA", (128, 128), (255, 255, 255, 0))
        image.putalpha(alpha)
        image.save(glyph_dir / (rune.id + ".png"), optimize=True)
    result = {
        "formatVersion": 1,
        "sourceRepo": MODEL_REPO,
        "sourceRevision": MODEL_REVISION,
        "baseSha256": MODEL_SHA256,
        "mergedSha256": sha256_of(weights),
        "onnxSha256": sha256_of(onnx_path),
        "referenceSha256": sha256_of(reference_path),
        "modelName": "ViT-Tiny + LoRA rank 4",
        "license": "Apache-2.0",
        "inputSize": 224,
        "embeddingSize": 192,
        "referenceCount": 120,
        "labels": labels,
        "calibratedThreshold": metadata["threshold"],
        "calibratedMargin": metadata["margin"],
        "gameplayThreshold": 0.90,
        "gameplayMargin": 0.05,
        "onnxOpset": 17,
        "onnxOps": ops,
        "onnxMinCosineVsTorch": min_parity,
        "onnxMinCosineVsReference": min_reference,
        "onnxMaxAbsDifference": max_abs,
        "modelBytes": onnx_path.stat().st_size,
    }
    (root / "RuneModelInfo.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)


if __name__ == "__main__":
    main()
