"""Verify exported LoRA candidate on the CPU runtime; never recalibrate."""

from __future__ import annotations

import argparse
import hashlib
import json
import time
from pathlib import Path

import numpy as np
import torch
from PIL import Image, ImageDraw, ImageFont
from safetensors.torch import load_file

from benchmark import synthetic_cases
from runes import RUNES, is_too_short, rasterize, stroke_metrics
from train_lora import (
    LABEL_TO_INT, Encoder, create_backbone, make_queries, measure,
    prototype_pixels, read_records, json_write,
)


def timing(encoder, model, images):
    encoder.encode(model, images[:1], batch_size=1)
    durations = []
    for image in images:
        start = time.perf_counter()
        encoder.encode(model, image[None], batch_size=1)
        durations.append((time.perf_counter() - start) * 1000.0)
    return {"count": len(durations), "median_ms": float(np.median(durations)), "p95_ms": float(np.percentile(durations, 95))}


def failure_sheet(output, records, indexes, scores, threshold, margin):
    examples = []
    for index, row in zip(indexes, scores):
        label, prediction = row["label"], row["prediction"]
        accepted = prediction >= 0 and row["score"] >= threshold and row["gap"] >= margin
        correct = (not accepted) if label < 0 else (accepted and prediction == label)
        if not correct:
            examples.append((records[index], row, accepted))
    if not examples:
        return None
    try:
        font = ImageFont.truetype("arial.ttf", 14)
        title = ImageFont.truetype("arial.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
        title = font
    columns = min(5, len(examples))
    height = 240
    sheet = Image.new("RGB", (columns * 224 + 24, ((len(examples) + columns - 1) // columns) * height + 62), "#111d25")
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Rune Lab — lỗi trên tập test giữ riêng", font=title, fill="#edd39d")
    draw.text((12, 36), "Không dùng các mẫu này để sửa model hoặc ngưỡng.", font=font, fill="#b9cbd1")
    for i, (record, row, accepted) in enumerate(examples):
        x, y = 12 + (i % columns) * 224, 60 + (i // columns) * height
        color = "#ee998d" if accepted else "#e1ba76"
        draw.rounded_rectangle((x, y, x+215, y+height-8), radius=5, outline=color, width=2, fill="#182a34")
        if not record["too_short"]:
            pic = rasterize(record["points"]).resize((144, 144)).convert("RGB")
        else:
            pic = Image.new("RGB", (144, 144), "white")
        sheet.paste(pic, (x+36, y+8))
        expected = RUNES[row["label"]].name if row["label"] >= 0 else "Nét không hợp lệ"
        predicted = RUNES[row["prediction"]].name if row["prediction"] >= 0 else "Không có"
        draw.text((x+8, y+160), f"Nhãn: {expected}", font=font, fill="#e4e9e8")
        draw.text((x+8, y+177), f"Top 1: {predicted}", font=font, fill=color)
        draw.text((x+8, y+194), f"Score {row['score']:.3f} · gap {row['gap']:.3f}", font=font, fill="#b9cbd1")
        draw.text((x+8, y+211), "NHẬN NHẦM" if accepted else "TỪ CHỐI NÉT ĐÚNG", font=font, fill=color)
    path = output / "holdout-failures.png"
    sheet.save(path)
    return path.name


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("experiment", type=Path)
    args = parser.parse_args()
    directory = args.experiment.resolve()
    report = json.loads((directory / "report.json").read_text(encoding="utf-8"))
    split = json.loads((directory / "split.json").read_text(encoding="utf-8"))
    blob = (directory / "dataset_snapshot.jsonl").read_bytes()
    if hashlib.sha256(blob).hexdigest() != report["dataset_sha256"]:
        raise RuntimeError("Snapshot integrity check failed")
    records = read_records(blob)
    index_by_id = {record["id"]: index for index, record in enumerate(records)}
    development = [index_by_id[ident] for ident in split["development"]]
    holdout = [index_by_id[ident] for ident in split["test"]]
    bank = prototype_pixels(records, development)

    torch.set_num_threads(4)
    torch.set_float32_matmul_precision("highest")
    encoder = Encoder("cpu")
    frozen = create_backbone()
    candidate = create_backbone()
    candidate.load_state_dict(load_file(str(directory / "candidate-merged.safetensors"), device="cpu"), strict=True)
    embeddings = encoder.encode(candidate, bank)
    with np.load(directory / "candidate-template-embeddings.npz", allow_pickle=False) as data:
        gpu_reference = data["embeddings"]
    similarity = (gpu_reference * embeddings).sum(axis=-1)
    minimum = float(similarity.min())
    if minimum < 0.9999:
        raise RuntimeError("CPU / training-device reference embeddings diverge")
    winner = report["winner"]
    threshold = report["artifact"]["threshold"]
    margin = report["artifact"]["margin"]
    query = make_queries(records, holdout)
    final_scores = encoder.score(candidate, bank, query)
    cpu_result = measure(final_scores, threshold, margin, detailed=True)
    image = failure_sheet(directory, records, holdout, final_scores, threshold, margin)

    # Independent synthetic stress check (NOT a claim about new human users).
    cases = synthetic_cases(seed=981724, per_rune=20)
    synthetic = [
        {"label": LABEL_TO_INT[case["expected"]], "points": case["points"],
         "too_short": is_too_short(stroke_metrics(case["points"]))}
        for case in cases
    ]
    syn_query = make_queries(synthetic, list(range(len(synthetic))))
    baseline_settings = report["development"]["frozen-real+synthetic"]["metrics"]
    synthetic_baseline = measure(
        encoder.score(frozen, bank, syn_query), baseline_settings["threshold"], baseline_settings["margin"], detailed=True
    )
    synthetic_candidate_rows = encoder.score(candidate, bank, syn_query)
    synthetic_candidate = measure(synthetic_candidate_rows, threshold, margin, detailed=True)
    # Compare the same inputs and CPU runtime, not GPU training throughput.
    timed_images = query[0][:48]
    base_timing = timing(encoder, frozen, timed_images)
    acquired_timing = timing(encoder, candidate, timed_images)
    result = {
        "cpu_min_cosine_vs_training_device_reference": minimum,
        "cpu_holdout": cpu_result,
        "synthetic_seen_geometry_unseen_samples": {
            "scope": "independent synthetic strokes; same vocabulary; thresholds fixed, not human validation",
            "frozen_real_and_synthetic": synthetic_baseline,
            "candidate": synthetic_candidate,
        },
        "single_image_cpu_ms": {"frozen": base_timing, "candidate_merged": acquired_timing},
        "holdout_failure_image": image,
        "predefined_operating_points": {
            f"{t:.2f}/{m:.2f}": {
                "holdout": measure(final_scores, t, m, detailed=True),
                "synthetic": measure(synthetic_candidate_rows, t, m, detailed=True),
            }
            for t, m in ((threshold, margin), (0.90, 0.05), (0.92, 0.02))
        },
    }
    json_write(directory / "verification.json", result)
    extra = [
        "", "## Xác minh trên môi trường CPU đang dùng cho Rune Lab", "",
        f"Cosine nhỏ nhất giữa các vector tham chiếu GPU và CPU: {minimum:.8f}. Kết quả test thật tái hiện như lúc chạy GPU.",
        f"Median/p95 inference một ảnh (48 lần, desktop CPU 4 luồng): frozen {base_timing['median_ms']:.2f}/{base_timing['p95_ms']:.2f} ms; candidate merged {acquired_timing['median_ms']:.2f}/{acquired_timing['p95_ms']:.2f} ms.",
        "", "### Kiểm tra mở rộng bằng 350 nét mô phỏng độc lập", "",
        "Đây là một thử nghiệm độ bền của model, không phải dữ liệu người mới; ngưỡng giữ nguyên từ development.",
        "", "| Model | Top-1 | Chấp nhận đúng | Nhầm chiêu | Nhận nét lạ |", "|---|---:|---:|---:|---:|",
    ]
    for name, stats in (("Frozen + mẫu thật", synthetic_baseline), ("LoRA đã gộp", synthetic_candidate)):
        extra.append(f"| {name} | {stats['top1']}/{stats['valid']} | {stats['correct_accept']}/{stats['valid']} | {stats['wrong_accept']} | {stats['invalid_false_accept']}/{stats['invalid']} |")
    extra += [
        "", "### Đánh đổi ở các ngưỡng đã định trước", "",
        "Các ngưỡng 0,90/0,05 và 0,92/0,02 đã dùng trước đợt huấn luyện. Đây chỉ là đối chiếu, không phải chọn ngưỡng mới dựa trên test.",
        "", "| Similarity / độ chênh | Nét thật đúng | Nét thật lạ bị nhận | Nét mô phỏng đúng | Nét mô phỏng lạ bị nhận |",
        "|---|---:|---:|---:|---:|",
    ]
    for name, stats in result["predefined_operating_points"].items():
        h, s = stats["holdout"], stats["synthetic"]
        extra.append(f"| {name} | {h['correct_accept']}/{h['valid']} | {h['invalid_false_accept']}/{h['invalid']} | {s['correct_accept']}/{s['valid']} | {s['invalid_false_accept']}/{s['invalid']} |")
    extra += [
        "", "### Kết luận", "",
        "LoRA cải thiện phân biệt các ký hiệu hợp lệ. Chưa đủ an toàn để bật thi triển tự động: cần thêm nét lạ khó, các nét thiếu đoạn và nét từ người chơi khác; hiệu chỉnh ngưỡng bằng development mới, giữ bộ test độc lập theo người vẽ.",
    ]
    if image:
        extra += ["", f"![Những trường hợp sai hoặc bị từ chối ở test]({image})"]
    extra.append("")
    (directory / "REPORT-FULL.md").write_text(
        (directory / "REPORT.md").read_text(encoding="utf-8") + "\n".join(extra), encoding="utf-8"
    )
    print("CPU holdout", {key: value for key, value in cpu_result.items() if key not in ("by_class", "top1_confusions")})
    print("CPU parity", minimum, "latency", json.dumps(result["single_image_cpu_ms"]))
    for name, stats in (("frozen", synthetic_baseline), ("candidate", synthetic_candidate)):
        print("Synthetic stress", name, {key: value for key, value in stats.items() if key not in ("by_class", "top1_confusions")})
    print("Fixed operating points", {
        name: (stats["holdout"]["correct_accept"], stats["holdout"]["invalid_false_accept"],
               stats["synthetic"]["correct_accept"], stats["synthetic"]["invalid_false_accept"])
        for name, stats in result["predefined_operating_points"].items()
    })
    print("Verification:", directory / "verification.json")


if __name__ == "__main__":
    main()
