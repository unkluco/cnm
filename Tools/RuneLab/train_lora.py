"""Reproducible, local LoRA + cosine triplet experiment for Rune Lab.

The original backbone and live recognition service are never overwritten.
The player dataset, split manifest, adapters, weights and reports live under
.local/training (ignored by Git). No training or calibration uses final holdout.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import random
import statistics
import time
from collections import Counter, defaultdict
from dataclasses import asdict, dataclass
from pathlib import Path

import numpy as np
import timm
import torch
import torch.nn as nn
import torch.nn.functional as F
from safetensors.torch import load_file, save_file

from recognizer import LOCAL, MODEL_REPO, MODEL_REVISION, MODEL_SHA256, ensure_weights, MEAN, STD
from runes import RUNES, is_too_short, rasterize, reference_bank, stroke_metrics, validate_points, variant

CLASSES = [rune.id for rune in RUNES]
LABEL_TO_INT = {label: index for index, label in enumerate(CLASSES)}
LABEL_TO_INT["invalid"] = -1
N_CLASSES = len(CLASSES)
N_REAL_PROTOTYPES = 5
DEFAULT_SEED = 25112026
DEFAULT_EPOCHS = 20
DEFAULT_STEPS = 6


@dataclass(frozen=True)
class Config:
    name: str
    rank: int
    alpha: int
    last_blocks: int
    targets: tuple[str, ...]
    lr: float
    triplet_margin: float
    dropout: float
    mining: str = "hard"


CONFIGS = (
    Config("r2-last2-qkv", 2, 4, 2, ("qkv",), 7e-4, 0.15, 0.03),
    Config("r4-last4-hard", 4, 8, 4, ("qkv", "proj"), 5e-4, 0.20, 0.05),
    Config("r8-last4-hard", 8, 16, 4, ("qkv", "proj"), 3e-4, 0.25, 0.05),
    Config("r4-last4-semihard", 4, 8, 4, ("qkv", "proj"), 5e-4, 0.20, 0.05, "semi-hard"),
)


def set_seed(seed):
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(seed)


class LoRALinear(nn.Module):
    """W x + (alpha/r) B A x. Zero B initialization, frozen base linear."""

    def __init__(self, base: nn.Linear, rank: int, alpha: int, dropout: float):
        super().__init__()
        if not 0 < rank <= min(base.in_features, base.out_features):
            raise ValueError("Invalid LoRA rank")
        self.base = base.requires_grad_(False)
        self.rank = rank
        self.scale = alpha / rank
        self.lora_a = nn.Parameter(torch.empty(rank, base.in_features))
        self.lora_b = nn.Parameter(torch.zeros(base.out_features, rank))
        self.dropout = nn.Dropout(dropout)
        nn.init.kaiming_uniform_(self.lora_a, a=math.sqrt(5))

    def forward(self, x):
        update = F.linear(F.linear(self.dropout(x), self.lora_a), self.lora_b)
        return self.base(x) + update * self.scale

    def merged_weight(self):
        return self.base.weight.detach().float() + self.scale * (self.lora_b.detach().float() @ self.lora_a.detach().float())


def create_backbone(config: Config | None = None):
    model = timm.create_model("vit_tiny_patch16_224", pretrained=False, num_classes=0)
    model.load_state_dict(load_file(str(ensure_weights()), device="cpu"), strict=True)
    model.requires_grad_(False).eval()
    if config:
        for block in model.blocks[-config.last_blocks:]:
            for target in config.targets:
                base = getattr(block.attn, target)
                if not isinstance(base, nn.Linear):
                    raise TypeError(f"Unexpected attention layer {target}")
                setattr(block.attn, target, LoRALinear(base, config.rank, config.alpha, config.dropout))
        if not all(".lora_" in name for name, param in model.named_parameters() if param.requires_grad):
            raise RuntimeError("A non-LoRA parameter was accidentally made trainable")
    return model


def adapter_state(model):
    return {
        name: param.detach().cpu().contiguous().clone()
        for name, param in model.named_parameters() if param.requires_grad
    }


def load_adapter_state(model, state):
    current = {name: p for name, p in model.named_parameters() if p.requires_grad}
    if set(state) != set(current):
        raise ValueError("Adapter keys differ from configured LoRA parameters")
    with torch.no_grad():
        for name, param in current.items():
            param.copy_(state[name].to(param.device, dtype=param.dtype))


def frozen_digest(model):
    digest = hashlib.sha256()
    for name, param in model.named_parameters():
        if not param.requires_grad:
            digest.update(name.encode("utf-8"))
            digest.update(param.detach().cpu().contiguous().numpy().tobytes())
    return digest.hexdigest()


def merge_state(model):
    state = {
        name: tensor.detach().cpu().contiguous().clone()
        for name, tensor in model.state_dict().items()
    }
    for name, module in model.named_modules():
        if not isinstance(module, LoRALinear):
            continue
        state[name + ".weight"] = module.merged_weight().cpu().contiguous()
        state.pop(name + ".base.weight")
        if module.base.bias is not None:
            state[name + ".bias"] = state.pop(name + ".base.bias")
        state.pop(name + ".lora_a")
        state.pop(name + ".lora_b")
    return state


def triplet_loss(z, labels, margin, mining="hard"):
    """Cosine-distance triplets; unknown strokes can be negatives, not anchors.

    A batch has at least two different labeled source drawings per rune class.
    For batch-hard, choose the farthest positive and nearest negative.
    Semi-hard chooses the nearest negative farther than the positive, falling
    back to the nearest negative if none exists. No classification loss/head.
    """
    z = F.normalize(z.float(), dim=-1)
    distance = (1.0 - z @ z.T).clamp(0.0, 2.0)
    valid = labels >= 0
    same = labels[:, None] == labels[None, :]
    identity = torch.eye(len(labels), device=labels.device, dtype=torch.bool)
    positive_mask = same & ~identity & valid[:, None] & valid[None, :]
    negative_mask = ~same & valid[:, None]
    anchors = valid & positive_mask.any(dim=1) & negative_mask.any(dim=1)
    if not bool(anchors.any()):
        raise ValueError("Each batch needs labeled positives and negatives")
    positive = distance.masked_fill(~positive_mask, -1.0).max(dim=1).values
    hard_negative = distance.masked_fill(~negative_mask, 3.0).min(dim=1).values
    if mining == "semi-hard":
        candidates = negative_mask & (distance > positive[:, None])
        semi_negative = distance.masked_fill(~candidates, 3.0).min(dim=1).values
        negative = torch.where(candidates.any(dim=1), semi_negative, hard_negative)
    elif mining == "hard":
        negative = hard_negative
    else:
        raise ValueError(f"Unknown mining {mining}")
    values = F.relu(positive[anchors] - negative[anchors] + margin)
    return values.mean(), float((values.detach() > 0).float().mean().cpu())


def read_records(blob):
    records = []
    seen = set()
    for line in blob.decode("utf-8").splitlines():
        if not line.strip():
            continue
        raw = json.loads(line)
        ident, label = raw["id"], raw["expected"]
        if ident in seen or label not in LABEL_TO_INT:
            raise ValueError(f"Invalid or duplicate record {ident}")
        seen.add(ident)
        points = validate_points(raw["points"])
        too_short = is_too_short(stroke_metrics(points))
        records.append({"id": ident, "label": LABEL_TO_INT[label], "points": points, "too_short": too_short})
    return records


def split_records(records, seed, folds=3):
    """Fixed untouched stratified 25% holdout; three development folds.

    Per class: 5 final test, 15 development for 20 examples. Each development
    fold trains on 10 and validates on 5. Split records before augmentation.
    We cannot infer distinct writers from participant codes in this pilot.
    """
    groups = defaultdict(list)
    for index, row in enumerate(records):
        groups[row["label"]].append(index)
    chunks = [[] for _ in range(folds)]
    test = []
    for label in list(range(N_CLASSES)) + [-1]:
        ids = groups[label]
        if len(ids) < 12:
            raise ValueError(f"Need at least 12 samples for label {label}")
        rng = np.random.default_rng(seed + (label + 2) * 1009)
        ids = rng.permutation(ids).tolist()
        n_test = max(1, round(len(ids) * 0.25))
        test.extend(ids[:n_test])
        for fold, part in enumerate(np.array_split(ids[n_test:], folds)):
            chunks[fold].extend(int(i) for i in part)
    dev = [i for chunk in chunks for i in chunk]
    if set(dev) & set(test) or len(set(dev + test)) != len(records):
        raise RuntimeError("Invalid dataset split")
    parts = [
        {"train": [i for j, chunk in enumerate(chunks) if j != fold for i in chunk], "val": list(chunks[fold])}
        for fold in range(folds)
    ]
    # Prevent pixel-identical real strokes from straddling splits.
    fingerprints = [None if row["too_short"] else hashlib.sha256(rasterize(row["points"]).tobytes()).hexdigest() for row in records]
    for part in parts:
        a = {fingerprints[i] for i in part["train"] if fingerprints[i]}
        b = {fingerprints[i] for i in part["val"] + test if fingerprints[i]}
        if a & b:
            raise ValueError("Pixel-identical real strokes straddle train/validation. Group duplicates before training.")
    return {"development": dev, "test": test, "folds": parts}


def pixels(points):
    return np.asarray(rasterize(points), dtype=np.uint8)


def make_training_pool(records, train_indices, seed):
    """References are allowed to be training samples; validation/test never are."""
    rng = np.random.default_rng(seed)
    real, synthetic = defaultdict(list), defaultdict(list)

    def variants(path, label, strength):
        result = [pixels(path)]
        for index in range(3):
            altered = variant(path, rng, strength=float(rng.uniform(0.45, strength)))
            if label == LABEL_TO_INT["diagonal"] and index == 1:
                altered = altered.copy()
                altered[:, 0] = 1.0 - altered[:, 0]  # / and backslash are one rune.
            result.append(pixels(altered))
        return result

    for index in train_indices:
        row = records[index]
        if row["too_short"]:
            continue
        real[row["label"]].append(variants(row["points"], row["label"], 1.05 if row["label"] >= 0 else 0.70))
    for label, (_, paths) in enumerate(reference_bank()):
        for path in paths:
            synthetic[label].append(variants(path, label, 1.4))
    for label in range(N_CLASSES):
        if len(real[label]) < 5:
            raise ValueError(f"Insufficient real training samples for {CLASSES[label]}")
    return real, synthetic


def sample_batch(pool, rng, per_class=3, unknown_count=6):
    real, synthetic = pool
    batch, labels = [], []

    def add(source, label):
        batch.append(source[int(rng.integers(len(source)))])
        labels.append(label)

    for label in range(N_CLASSES):
        # Prioritize real strokes; one synthetic positive in some batches to
        # retain recognition of the original intended rune geometry.
        n_synthetic = int(rng.random() < 0.45)
        n_real = per_class - n_synthetic
        for index in rng.choice(len(real[label]), n_real, replace=False):
            add(real[label][int(index)], label)
        if n_synthetic:
            add(synthetic[label][int(rng.integers(len(synthetic[label])))], label)
    if real[-1]:
        for index in rng.choice(len(real[-1]), min(unknown_count, len(real[-1])), replace=False):
            add(real[-1][int(index)], -1)
    return np.stack(batch), np.asarray(labels, dtype=np.int64)


def prototype_pixels(records, train_indices, include_real=True):
    groups = defaultdict(list)
    for index in train_indices:
        row = records[index]
        if row["label"] >= 0 and not row["too_short"]:
            groups[row["label"]].append(index)
    bank = []
    for label, (_, paths) in enumerate(reference_bank()):
        images = [pixels(path) for path in paths]
        if include_real:
            ids = groups[label][:N_REAL_PROTOTYPES]
            if len(ids) != N_REAL_PROTOTYPES:
                raise ValueError("Not enough training-only real references")
            images.extend(pixels(records[index]["points"]) for index in ids)
        bank.append(np.stack(images))
    return np.stack(bank)


def make_queries(records, indices):
    images, labels, positions = [], [], []
    for position, index in enumerate(indices):
        row = records[index]
        labels.append(row["label"])
        if not row["too_short"]:
            images.append(pixels(row["points"]))
            positions.append(position)
    return (np.stack(images) if images else np.empty((0, 224, 224), dtype=np.uint8), labels, positions)


class Encoder:
    def __init__(self, device):
        self.device = torch.device(device)
        self.mean = torch.tensor(MEAN, device=self.device)
        self.std = torch.tensor(STD, device=self.device)

    def tensor(self, images):
        x = torch.from_numpy(np.ascontiguousarray(images)).to(self.device)
        # Same RGB/ImageNet normalization as the frozen live recognizer.
        x = x[:, None].expand(-1, 3, -1, -1).float().div(255.0)
        return (x - self.mean) / self.std

    @torch.inference_mode()
    def encode(self, model, images, batch_size=32):
        model.eval()
        shape = images.shape
        images = images.reshape(-1, 224, 224)
        result = []
        for offset in range(0, len(images), batch_size):
            tokens = model.forward_features(self.tensor(images[offset:offset + batch_size]))
            result.append(F.normalize(tokens[:, 0].float(), dim=-1).cpu().numpy())
        if not result:
            return np.empty((*shape[:-2], 192), dtype=np.float32)
        return np.concatenate(result).reshape(*shape[:-2], 192)

    def score(self, model, bank, query):
        bank_emb = self.encode(model, bank)
        images, labels, positions = query
        query_emb = self.encode(model, images)
        scores = np.clip(np.einsum("qf,crf->qcr", query_emb, bank_emb).max(axis=2), -1, 1)
        output = [
            {"label": int(label), "prediction": -1, "score": -2.0, "gap": -2.0, "true_margin": -2.0}
            for label in labels
        ]
        for position, row in zip(positions, scores):
            order = np.argsort(-row, kind="stable")
            label = int(labels[position])
            true_margin = float(row[label] - max(row[i] for i in range(N_CLASSES) if i != label)) if label >= 0 else 0.0
            output[position] = {
                "label": label,
                "prediction": int(order[0]),
                "score": float(row[order[0]]),
                "gap": float(row[order[0]] - row[order[1]]),
                "true_margin": true_margin,
            }
        return output


def measure(rows, threshold, margin, detailed=False):
    counts = Counter()
    per_class = defaultdict(Counter)
    confusions = Counter()
    for row in rows:
        label, prediction = row["label"], row["prediction"]
        accepted = prediction >= 0 and row["score"] >= threshold and row["gap"] >= margin
        if label < 0:
            counts["invalid"] += 1
            counts["invalid_false_accept"] += int(accepted)
            counts["invalid_rejected"] += int(not accepted)
            continue
        name = CLASSES[label]
        counts["valid"] += 1
        per_class[name]["total"] += 1
        correct = prediction == label
        counts["top1"] += int(correct)
        per_class[name]["top1"] += int(correct)
        if not correct:
            confusions[f"{name}->{CLASSES[prediction] if prediction >= 0 else 'none'}"] += 1
        outcome = "correct_accept" if accepted and correct else "wrong_accept" if accepted else "rejected"
        counts[outcome] += 1
        per_class[name][outcome] += 1
    keys = ("valid", "top1", "correct_accept", "wrong_accept", "rejected", "invalid", "invalid_false_accept", "invalid_rejected")
    result = {key: counts[key] for key in keys}
    result["threshold"], result["margin"] = float(threshold), float(margin)
    if detailed:
        result["by_class"] = {key: dict(value) for key, value in per_class.items()}
        result["top1_confusions"] = dict(confusions.most_common())
    return result


def calibrate(rows):
    """Fit an operating point on DEVELOPMENT rows only.

    First minimize false casts (wrong rune + unknown accepted), then maximize
    accepted-correct runes. Use the middle of tied grid cells for stability.
    A positive ambiguity margin is always required.
    """
    thresholds = np.arange(0, 101, dtype=np.float64) / 100
    margins = np.arange(1, 51, dtype=np.float64) / 100
    scores = np.array([row["score"] for row in rows])
    gaps = np.array([row["gap"] for row in rows])
    good = np.array([row["label"] >= 0 and row["prediction"] == row["label"] for row in rows])
    accepted = (scores[None, None, :] >= thresholds[:, None, None]) & (gaps[None, None, :] >= margins[None, :, None])
    bad = (accepted & ~good[None, None, :]).sum(axis=-1)
    right = (accepted & good[None, None, :]).sum(axis=-1)
    safest = bad == bad.min()
    best = safest & (right == right[safest].max())
    cells = np.argwhere(best)
    middle = np.median(cells, axis=0)
    cell = cells[np.argmin(((cells - middle) ** 2).sum(axis=1))]
    return measure(rows, float(thresholds[cell[0]]), float(margins[cell[1]]), detailed=True)


def selection_key(rows):
    result = calibrate(rows)
    valid_margins = [row["true_margin"] for row in rows if row["label"] >= 0]
    unknown_scores = [row["score"] for row in rows if row["label"] < 0]
    return (
        -(result["wrong_accept"] + result["invalid_false_accept"]),
        result["correct_accept"],
        result["top1"],
        float(np.mean(valid_margins)),
        -float(np.mean(unknown_scores)) if unknown_scores else 0.0,
    ), result


def fit(config, encoder, pool, bank, query, epochs, steps, seed, run_name, per_class=3):
    set_seed(seed)
    model = create_backbone(config).to(encoder.device)
    initial_hash = frozen_digest(model)
    trainable = [p for p in model.parameters() if p.requires_grad]
    n_trainable = sum(p.numel() for p in trainable)
    optimizer = torch.optim.AdamW(trainable, lr=config.lr, weight_decay=0.01)
    total_steps = epochs * steps
    warmup = max(1, total_steps // 10)

    def lr_schedule(step):
        if step < warmup:
            return 0.2 + 0.8 * step / warmup
        progress = (step - warmup) / max(1, total_steps - warmup)
        return 0.15 + 0.85 * (1.0 + math.cos(math.pi * min(progress, 1.0))) / 2.0

    scheduler = torch.optim.lr_scheduler.LambdaLR(optimizer, lr_schedule)
    rng = np.random.default_rng(seed + 77)
    use_amp = encoder.device.type == "cuda" and torch.cuda.is_bf16_supported()
    if encoder.device.type == "cuda":
        torch.cuda.reset_peak_memory_stats()
    history, best_state, best_key, best_rows, best_epoch = [], None, None, None, 0
    started = time.perf_counter()
    print(f"START {run_name} | {n_trainable:,} trainable parameters | max {epochs} epochs", flush=True)
    stale = 0
    for epoch in range(1, epochs + 1):
        model.train()
        losses, active_triplets = [], []
        for _ in range(steps):
            images, labels = sample_batch(pool, rng, per_class=per_class)
            optimizer.zero_grad(set_to_none=True)
            y = torch.from_numpy(labels).to(encoder.device)
            with torch.autocast(device_type=encoder.device.type, dtype=torch.bfloat16, enabled=use_amp):
                features = model.forward_features(encoder.tensor(images))[:, 0]
            loss, active = triplet_loss(features, y, config.triplet_margin, config.mining)
            if not bool(torch.isfinite(loss)):
                raise RuntimeError("Triplet loss became non-finite")
            loss.backward()
            torch.nn.utils.clip_grad_norm_(trainable, max_norm=1.0)
            optimizer.step()
            scheduler.step()
            losses.append(float(loss.detach().cpu()))
            active_triplets.append(active)
        row = {"epoch": epoch, "loss": float(np.mean(losses)), "active_triplets": float(np.mean(active_triplets))}
        evaluate_now = query is not None and (epoch == 1 or epoch % 2 == 0 or epoch == epochs)
        if evaluate_now:
            scores = encoder.score(model, bank, query)
            key, metrics = selection_key(scores)
            row["validation"] = {name: value for name, value in metrics.items() if name not in ("by_class", "top1_confusions")}
            if best_key is None or key > best_key:
                best_key, best_state, best_rows, best_epoch = key, adapter_state(model), scores, epoch
                stale = 0
            else:
                stale += 1
            print(
                f"{run_name} ep {epoch:02d}/{epochs:02d} loss={row['loss']:.4f} "
                f"val top1={metrics['top1']}/{metrics['valid']} "
                f"safe-accept={metrics['correct_accept']}/{metrics['valid']} "
                f"bad={metrics['wrong_accept'] + metrics['invalid_false_accept']} best={best_epoch}",
                flush=True,
            )
        elif query is None and (epoch == 1 or epoch % 4 == 0 or epoch == epochs):
            print(f"{run_name} ep {epoch:02d}/{epochs:02d} loss={row['loss']:.4f}", flush=True)
        history.append(row)
        if query is not None and stale >= 6 and epoch >= 12:
            break
    if query is None:
        best_epoch, best_state = history[-1]["epoch"], adapter_state(model)
    else:
        load_adapter_state(model, best_state)
    if frozen_digest(model) != initial_hash:
        raise RuntimeError("Frozen backbone changed during LoRA training")
    if not any(float(state.abs().max()) > 0 for name, state in best_state.items() if name.endswith("lora_b")):
        raise RuntimeError("LoRA B matrices did not learn an update")
    seconds = time.perf_counter() - started
    peak_mb = torch.cuda.max_memory_allocated() / (1024**2) if encoder.device.type == "cuda" else None
    print(f"DONE {run_name} | best epoch {best_epoch} | {seconds:.1f}s | peak alloc {peak_mb} MiB", flush=True)
    return model.eval(), {
        "config": asdict(config), "n_trainable": n_trainable, "best_epoch": best_epoch,
        "history": history, "val_scores": best_rows, "seconds": seconds, "peak_gpu_mib": peak_mb,
        "backbone_unchanged": True,
    }


def json_write(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(json.dumps(obj, ensure_ascii=False, indent=2, allow_nan=False), encoding="utf-8")
    os.replace(temp, path)


def print_metrics(name, m):
    print(
        f"{name}: top1 {m['top1']}/{m['valid']}, accepted correct {m['correct_accept']}/{m['valid']}, "
        f"wrong {m['wrong_accept']}, unknown accepted {m['invalid_false_accept']}/{m['invalid']}, "
        f"threshold={m['threshold']:.2f} gap={m['margin']:.2f}",
        flush=True,
    )


def report_markdown(report):
    def ratio(n, d):
        return f"{n}/{d} ({100*n/d:.1f}%)" if d else "—"

    lines = [
        "# Light Souls — thử nghiệm LoRA + Triplet Loss",
        "",
        "Dữ liệu hiện tại được Kiệt vẽ trong Rune Lab; đây là thử nghiệm trong phạm vi người vẽ hiện có, chưa phải đánh giá nhiều người.",
        f"Snapshot SHA-256: `{report['dataset_sha256']}`",
        f"Thiết bị: {report['device']}; PyTorch {report['torch_version']}.",
        "",
        "## So sánh cấu hình trên 3 fold phát triển",
        "",
        "Ngưỡng của từng cấu hình được chọn trên dữ liệu development, ưu tiên không thi triển nhầm rồi tối đa hóa số nét nhận đúng. Các chỉ số này được dùng để chọn model, **không phải kết quả test độc lập**.",
        "",
        "| Cấu hình | Tham số train | Top-1 | Chấp nhận đúng | Nhầm chiêu | Nhận nét lạ | Ngưỡng / chênh |",
        "|---|---:|---:|---:|---:|---:|---|",
    ]
    for name, info in report["development"].items():
        m = info["metrics"]
        lines.append(
            f"| {name} | {info.get('n_trainable',0):,} | {ratio(m['top1'],m['valid'])} | "
            f"{ratio(m['correct_accept'],m['valid'])} | {m['wrong_accept']} | "
            f"{m['invalid_false_accept']}/{m['invalid']} | {m['threshold']:.2f} / {m['margin']:.2f} |"
        )
    lines += ["", f"Cấu hình LoRA được chọn trước khi mở tập test: **{report['winner']}**.", "",
              "## Tập kiểm tra cuối (chưa dùng để train, chọn mẫu hay ngưỡng)", "",
              "| Model | Top-1 | Chấp nhận đúng | Nhầm chiêu | Nhận nét lạ |",
              "|---|---:|---:|---:|---:|"]
    for name, m in report["holdout"].items():
        lines.append(f"| {name} | {ratio(m['top1'],m['valid'])} | {ratio(m['correct_accept'],m['valid'])} | {m['wrong_accept']} | {m['invalid_false_accept']}/{m['invalid']} |")
    lines += [
        "", "## Kích thước và triển khai", "",
        f"Adapter: {report['artifact']['adapter_bytes']:,} bytes; merged ViT-Tiny: {report['artifact']['merged_bytes']:,} bytes.",
        f"Cosine nhỏ nhất giữa embedding trước/sau merge: {report['artifact']['merge_min_cosine']:.8f}.",
        "Model merge giữ nguyên kiến trúc 5.524.416 tham số, đầu vào 224×224 và embedding 192 chiều. Chưa tích hợp model vào Rune Lab đang chạy, chưa xuất ONNX, chưa đo mobile.",
        "", "## Giới hạn", "",
        "- 260 nét vẽ hiện có không đủ đại diện cho người chơi mới; mã người vẽ khác nhau không chứng minh độc lập người vẽ.",
        "- Tập test chỉ có 5 nét không hợp lệ; không thể dùng kết quả 0/5 để khẳng định tỷ lệ kích hoạt nhầm dưới 2%.",
        "- Cần bổ sung dữ liệu người vẽ khác, nét không hợp lệ đa dạng và kiểm tra trực tiếp trên mobile trước khi sử dụng gameplay.",
        "", "Tham khảo: [LoRA](https://arxiv.org/abs/2106.09685), [Batch-Hard Triplet Loss](https://arxiv.org/abs/1703.07737).",
    ]
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path, default=LOCAL / "data" / "samples.jsonl")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--device", choices=("cpu", "cuda", "auto"), default="auto")
    parser.add_argument("--epochs", type=int, default=DEFAULT_EPOCHS)
    parser.add_argument("--steps", type=int, default=DEFAULT_STEPS)
    parser.add_argument("--per-class", type=int, default=3)
    parser.add_argument("--threads", type=int, default=4)
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--configs", nargs="+", choices=[c.name for c in CONFIGS])
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--smoke", action="store_true", help="One dev fold, one epoch, no access to holdout.")
    args = parser.parse_args()
    if args.epochs < 1 or args.steps < 1 or not 2 <= args.per_class <= 5 or args.threads < 1:
        parser.error("epochs/steps/threads must be positive and per-class must be 2–5")
    device = "cuda" if args.device == "auto" and torch.cuda.is_available() else ("cpu" if args.device == "auto" else args.device)
    if device == "cuda" and not torch.cuda.is_available():
        raise RuntimeError("CUDA requested but unavailable")
    torch.set_num_threads(args.threads)
    torch.set_float32_matmul_precision("high")
    output = args.output.resolve()
    configs = [c for c in CONFIGS if not args.configs or c.name in args.configs]
    if args.smoke:
        configs, args.epochs, args.steps = configs[:1], 1, 1
    arguments = {
        "seed": args.seed, "epochs": args.epochs, "steps": args.steps, "per_class": args.per_class,
        "device": device, "configs": [asdict(c) for c in configs], "smoke": args.smoke,
        "base_sha256": MODEL_SHA256,
    }
    # Canonicalize tuple/list fields for resume comparisons.
    arguments = json.loads(json.dumps(arguments))
    snapshot, manifest = output / "dataset_snapshot.jsonl", output / "manifest.json"
    if output.exists():
        if not args.resume or not snapshot.is_file() or not manifest.is_file():
            raise FileExistsError(f"Output exists. Choose a new directory or resume a matching experiment: {output}")
        meta = json.loads(manifest.read_text(encoding="utf-8"))
        blob = snapshot.read_bytes()
        if meta["arguments"] != arguments or meta["sha256"] != hashlib.sha256(blob).hexdigest():
            raise ValueError("Experiment resume/config/snapshot mismatch")
        if (output / "report.json").is_file() and not args.smoke:
            print(f"This completed run is already available at {output / 'REPORT.md'}. Choose a new output to repeat.")
            return
    else:
        output.mkdir(parents=True)
        blob = args.dataset.read_bytes()
        snapshot.write_bytes(blob)
        json_write(manifest, {"sha256": hashlib.sha256(blob).hexdigest(), "arguments": arguments})
    dataset_hash = hashlib.sha256(blob).hexdigest()
    records = read_records(blob)
    split = split_records(records, args.seed)
    json_write(output / "split.json", {
        "development": [records[i]["id"] for i in split["development"]],
        "test": [records[i]["id"] for i in split["test"]],
        "folds": [{key: [records[i]["id"] for i in ids] for key, ids in part.items()} for part in split["folds"]],
    })
    encoder = Encoder(device)
    print(f"Data hash {dataset_hash}; {len(records)} strokes; dev={len(split['development'])}; strict holdout={len(split['test'])}", flush=True)
    print(f"Training on {device} / {torch.__version__}; frozen backbone {MODEL_SHA256[:12]}", flush=True)
    development = defaultdict(list)
    results = defaultdict(list)
    folds = split["folds"][:1] if args.smoke else split["folds"]
    for fold_index, part in enumerate(folds):
        fold_path = output / f"fold-{fold_index + 1}"
        fold_path.mkdir(exist_ok=True)
        train_indices, val_indices = part["train"], part["val"]
        bank = prototype_pixels(records, train_indices)
        synthetic_bank = prototype_pixels(records, train_indices, include_real=False)
        query = make_queries(records, val_indices)
        baseline_file = fold_path / "baseline.json"
        if baseline_file.is_file() and args.resume:
            baseline = json.loads(baseline_file.read_text(encoding="utf-8"))
        else:
            frozen = create_backbone().to(encoder.device)
            baseline = {
                "frozen-synthetic": encoder.score(frozen, synthetic_bank, query),
                "frozen-real+synthetic": encoder.score(frozen, bank, query),
            }
            json_write(baseline_file, baseline)
            del frozen
        for name, rows in baseline.items():
            development[name].extend(rows)
            print_metrics(f"fold {fold_index+1} {name}", calibrate(rows))
        pool = make_training_pool(records, train_indices, args.seed + fold_index * 541)
        for config in configs:
            folder = fold_path / config.name
            result_file = folder / "result.json"
            if result_file.is_file() and args.resume:
                result = json.loads(result_file.read_text(encoding="utf-8"))
                print(f"RESUME completed {config.name} fold {fold_index+1}", flush=True)
            else:
                folder.mkdir(exist_ok=True)
                model, result = fit(config, encoder, pool, bank, query, args.epochs, args.steps,
                                    args.seed + fold_index * 1001, f"fold{fold_index+1}/{config.name}",
                                    per_class=args.per_class)
                save_file(adapter_state(model), str(folder / "adapter.safetensors"))
                json_write(result_file, result)
                del model
                if device == "cuda":
                    torch.cuda.empty_cache()
            development[config.name].extend(result["val_scores"])
            results[config.name].append(result)
    summary = {}
    for name, rows in development.items():
        summary[name] = {
            "metrics": calibrate(rows),
            "n_trainable": results[name][0]["n_trainable"] if results[name] else 0,
            "fixed_090_005": measure(rows, 0.90, 0.05, detailed=True),
            "fixed_092_002": measure(rows, 0.92, 0.02, detailed=True),
            "best_epochs": [result["best_epoch"] for result in results[name]],
            "seconds": sum(result["seconds"] for result in results[name]),
        }
        print_metrics("DEVELOPMENT " + name, summary[name]["metrics"])
    json_write(output / "development-summary.json", summary)
    if args.smoke:
        print("Smoke test complete. Final holdout was not encoded or evaluated.", flush=True)
        return
    # Model-selection policy is fixed and never sees the strict holdout.
    def winner_key(config):
        m = summary[config.name]["metrics"]
        return (-(m["wrong_accept"] + m["invalid_false_accept"]), m["correct_accept"], m["top1"], -summary[config.name]["n_trainable"])

    winner = max(configs, key=winner_key)
    winner_epochs = max(1, round(statistics.median(summary[winner.name]["best_epochs"])))
    selected = {
        "config": asdict(winner), "epochs": winner_epochs,
        "threshold": summary[winner.name]["metrics"]["threshold"],
        "margin": summary[winner.name]["metrics"]["margin"],
    }
    json_write(output / "selected-before-holdout.json", selected)
    print(f"Selected before holdout: {winner.name} / {winner_epochs} epochs / {selected['threshold']:.2f},{selected['margin']:.2f}", flush=True)
    dev = split["development"]
    final_bank = prototype_pixels(records, dev)
    final_pool = make_training_pool(records, dev, args.seed + 315)
    final_model, final_result = fit(winner, encoder, final_pool, final_bank, None, winner_epochs, args.steps,
                                    args.seed + 9151, f"final/{winner.name}", per_class=args.per_class)
    json_write(output / "final-training.json", final_result)
    adapter_file = output / "candidate-adapter.safetensors"
    merged_file = output / "candidate-merged.safetensors"
    save_file(adapter_state(final_model), str(adapter_file))
    save_file(merge_state(final_model), str(merged_file))
    # Verify merged, ordinary backbone produces the same embedding without
    # adapter modules; deployment can therefore preserve architecture/size.
    plain = create_backbone().to(encoder.device)
    plain.load_state_dict(load_file(str(merged_file), device="cpu"), strict=True)
    probe = final_bank[:, 0]
    a, b = encoder.encode(final_model, probe), encoder.encode(plain, probe)
    min_cosine = float((a * b).sum(axis=-1).min())
    max_abs = float(np.abs(a - b).max())
    if min_cosine < 0.9999 or sum(p.numel() for p in plain.parameters()) != 5524416:
        raise RuntimeError("Merged adapter failed parity or changed the backbone size")
    bank_emb = encoder.encode(plain, final_bank)
    np.savez_compressed(output / "candidate-template-embeddings.npz", embeddings=bank_emb, labels=np.array(CLASSES))

    # Only now may the strict holdout be scored. No later threshold change.
    test_query = make_queries(records, split["test"])
    frozen = create_backbone().to(encoder.device)
    base_synthetic = encoder.score(frozen, prototype_pixels(records, dev, include_real=False), test_query)
    base_real = encoder.score(frozen, final_bank, test_query)
    final_scores = encoder.score(plain, final_bank, test_query)
    holdout_rows = {"frozen-synthetic": base_synthetic, "frozen-real+synthetic": base_real, winner.name: final_scores}
    holdout = {
        name: measure(rows, summary[name]["metrics"]["threshold"], summary[name]["metrics"]["margin"], detailed=True)
        for name, rows in holdout_rows.items()
    }
    for name, m in holdout.items():
        print_metrics("STRICT HOLDOUT " + name, m)
    artifact = {
        "base_repo": MODEL_REPO, "base_revision": MODEL_REVISION, "base_sha256": MODEL_SHA256,
        "adapter_bytes": adapter_file.stat().st_size, "merged_bytes": merged_file.stat().st_size,
        "merged_parameters": sum(p.numel() for p in plain.parameters()), "merge_min_cosine": min_cosine,
        "merge_max_abs": max_abs, "config": asdict(winner),
        "threshold": selected["threshold"], "margin": selected["margin"],
        "reference_count": int(bank_emb.shape[0] * bank_emb.shape[1]),
    }
    json_write(output / "candidate-metadata.json", artifact)
    report = {
        "dataset_sha256": dataset_hash, "sample_count": len(records),
        "device": str(encoder.device), "device_name": torch.cuda.get_device_name(0) if device == "cuda" else "CPU",
        "torch_version": torch.__version__, "seed": args.seed, "steps_per_epoch": args.steps,
        "development": summary, "winner": winner.name, "final_epochs": winner_epochs,
        "holdout": holdout, "holdout_fixed_090_005": {name: measure(rows, 0.90, 0.05, detailed=True) for name, rows in holdout_rows.items()},
        "holdout_fixed_092_002": {name: measure(rows, 0.92, 0.02, detailed=True) for name, rows in holdout_rows.items()},
        "artifact": artifact,
    }
    json_write(output / "report.json", report)
    (output / "REPORT.md").write_text(report_markdown(report), encoding="utf-8")
    print(f"FINISHED. Report: {output / 'REPORT.md'}", flush=True)


if __name__ == "__main__":
    main()
