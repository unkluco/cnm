"""Rune vocabulary and a shared, aspect-preserving stroke rasterizer.

The laboratory uses a small set of single-stroke marks. These are synthetic
initial references, not a training dataset, and are never used to train ViT.
"""

from __future__ import annotations

import base64
import io
import math
from dataclasses import dataclass

import numpy as np
from PIL import Image, ImageDraw

IMAGE_SIZE = 224
INK_EXTENT = 176.0
INK_WIDTH = 9.0
MIN_EXTENT = 0.075
MIN_LENGTH = 0.09
MAX_POINTS = 4096
TEMPLATE_COUNT = 5


def polyline(vertices, count=81):
    """Resample by arc length. The rasterizer does not depend on draw speed."""
    coords = np.asarray(vertices, dtype=np.float64)
    lens = np.linalg.norm(np.diff(coords, axis=0), axis=1)
    keep = np.r_[True, lens > 1e-10]
    coords = coords[keep]
    if len(coords) < 2:
        return coords
    distances = np.r_[0.0, np.cumsum(np.linalg.norm(np.diff(coords, axis=0), axis=1))]
    targets = np.linspace(0.0, distances[-1], count)
    return np.column_stack([np.interp(targets, distances, coords[:, axis]) for axis in range(2)])


def arc(start, end, radius=0.32, count=81):
    angles = np.linspace(start, end, count)
    return np.column_stack((0.5 + radius * np.cos(angles), 0.5 + radius * np.sin(angles)))


def spiral():
    t = np.linspace(0.0, 1.0, 121)
    theta = -math.pi / 2.0 + t * (2.7 * math.pi)
    radius = 0.34 * (1 - t) + 0.045
    return np.column_stack((0.5 + radius * np.cos(theta), 0.5 + radius * np.sin(theta)))


T = np.linspace(0.0, 1.0, 81)


@dataclass(frozen=True)
class Rune:
    id: str
    name: str
    intent: str
    points: np.ndarray


RUNES = (
    Rune("slash", "Nét ngang", "Chém nhanh", polyline([(0.14, 0.5), (0.86, 0.5)])),
    Rune("thrust", "Nét dọc", "Đâm thẳng", polyline([(0.5, 0.16), (0.5, 0.84)])),
    Rune("diagonal", "Nét chéo", "Chém chéo", polyline([(0.22, 0.78), (0.78, 0.22)])),
    Rune("cleave", "Góc V", "Trọng kích", polyline([(0.15, 0.19), (0.50, 0.82), (0.85, 0.19)])),
    Rune("lightning", "Tia chớp", "Lôi kích", polyline([(0.19, 0.18), (0.82, 0.18), (0.18, 0.82), (0.82, 0.82)])),
    Rune("ring", "Vòng tròn", "Bộc phát", arc(-math.pi / 2.0, 3.0 * math.pi / 2.0)),
    Rune("crescent", "Trăng khuyết", "Lưỡi trăng", arc(math.pi / 4.0, 7.0 * math.pi / 4.0)),
    Rune("wave", "Làn sóng", "Sóng năng lượng", np.column_stack((0.14 + 0.72 * T, 0.5 + 0.15 * np.sin(2 * math.pi * T)))),
    Rune("flame", "Nét S", "Hỏa thuật", np.column_stack((0.5 + 0.18 * np.sin(2 * math.pi * T), 0.14 + 0.72 * T))),
    Rune("vortex", "Xoáy ốc", "Hút trọng lực", spiral()),
    Rune("ward", "Tam giác", "Hộ ấn", polyline([(0.5, 0.13), (0.87, 0.82), (0.13, 0.82), (0.5, 0.13)])),
    Rune("wall", "Hình vuông", "Dựng tường", polyline([(0.18, 0.18), (0.82, 0.18), (0.82, 0.82), (0.18, 0.82), (0.18, 0.18)])),
)
RUNE_BY_ID = {rune.id: rune for rune in RUNES}


def validate_points(raw) -> np.ndarray:
    """Validate normalized browser coordinates, including empty/short attempts."""
    if not isinstance(raw, (list, tuple)) or len(raw) > MAX_POINTS:
        raise ValueError(f"Nét vẽ phải chứa tối đa {MAX_POINTS} điểm.")
    result = []
    for pair in raw:
        if not isinstance(pair, (list, tuple)) or len(pair) != 2:
            raise ValueError("Mỗi điểm phải có hai tọa độ.")
        if any(isinstance(value, bool) or not isinstance(value, (int, float)) for value in pair):
            raise ValueError("Tọa độ phải là số.")
        x, y = map(float, pair)
        if not (math.isfinite(x) and math.isfinite(y) and 0.0 <= x <= 1.0 and 0.0 <= y <= 1.0):
            raise ValueError("Tọa độ phải nằm trong vùng vẽ (0–1).")
        if not result or (x, y) != result[-1]:
            result.append((x, y))
    return np.asarray(result, dtype=np.float64).reshape(-1, 2)


def stroke_metrics(points: np.ndarray) -> dict:
    if len(points) < 2:
        return {"point_count": len(points), "extent": 0.0, "length": 0.0}
    return {
        "point_count": len(points),
        "extent": float(np.max(np.ptp(points, axis=0))),
        "length": float(np.linalg.norm(np.diff(points, axis=0), axis=1).sum()),
    }


def is_too_short(metrics: dict) -> bool:
    return metrics["point_count"] < 2 or metrics["extent"] < MIN_EXTENT or metrics["length"] < MIN_LENGTH


def rasterize(points: np.ndarray) -> Image.Image:
    """Render a black, fixed-width stroke on white. Keep orientation/aspect.

    Canvas colour, UI effects, position, size and pointer speed are not features.
    """
    if len(points) < 2:
        raise ValueError("Nét vẽ chưa đủ dài.")
    span = np.ptp(points, axis=0)
    extent = float(span.max())
    if extent < 1e-8:
        raise ValueError("Nét vẽ chưa đủ dài.")
    center = (points.min(axis=0) + points.max(axis=0)) * 0.5
    # Quantize sub-pixel coordinates before selecting raster pixels. Tiny
    # floating-point differences caused by a prior translation/scale must not
    # make equivalent strokes fall on opposite sides of a half-pixel tie.
    fitted = np.round((points - center) * (INK_EXTENT / extent) + (IMAGE_SIZE - 1) * 0.5, 8)
    scale = 3
    pixels = [(math.floor(x * scale + 0.5), math.floor(y * scale + 0.5)) for x, y in fitted]
    image = Image.new("L", (IMAGE_SIZE * scale, IMAGE_SIZE * scale), 255)
    draw = ImageDraw.Draw(image)
    width = round(INK_WIDTH * scale)
    draw.line(pixels, fill=0, width=width, joint="curve")
    radius = width / 2.0
    for x, y in (pixels[0], pixels[-1]):
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=0)
    return image.resize((IMAGE_SIZE, IMAGE_SIZE), Image.Resampling.LANCZOS)


def png_data_url(image: Image.Image) -> str:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")


def variant(points: np.ndarray, rng: np.random.Generator, strength: float = 1.0) -> np.ndarray:
    """Controlled shape perturbations for references and synthetic smoke tests.

    Do not rotate by arbitrary angles: orientations are distinct test classes.
    Smooth perturbations avoid unrealistically independent per-point noise.
    """
    coords = np.asarray(points, dtype=np.float64)
    center = (coords.min(axis=0) + coords.max(axis=0)) * 0.5
    coords = (coords - center) * rng.uniform(1.0 - 0.12 * strength, 1.0 + 0.12 * strength, 2)
    angle = math.radians(float(rng.uniform(-9.0, 9.0) * strength))
    rotation = np.array(((math.cos(angle), -math.sin(angle)), (math.sin(angle), math.cos(angle))))
    coords = coords @ rotation.T
    t = np.linspace(0, 1, len(coords))
    for axis in range(2):
        phase = rng.uniform(0, 2 * math.pi)
        amp = rng.uniform(0.004, 0.019) * strength
        coords[:, axis] += amp * np.sin(2 * math.pi * t + phase)
        coords[:, axis] += 0.003 * strength * np.sin(5 * math.pi * t + phase / 2.0)
    # References/variants all safely fit the canvas; rasterization removes size
    # and position. This normalizes the generator, not the user's drawing.
    coords = coords * min(1.0, 0.80 / max(1e-6, np.max(np.ptp(coords, axis=0))))
    center = (coords.min(axis=0) + coords.max(axis=0)) * 0.5
    return np.clip(coords - center + 0.5 + rng.uniform(-0.035, 0.035, 2), 0.0, 1.0)


def reference_bank():
    result = []
    for index, rune in enumerate(RUNES):
        rng = np.random.default_rng(74019 + index * 97)
        paths = [rune.points]
        paths.extend(variant(rune.points, rng, 0.75) for _ in range(TEMPLATE_COUNT - 1))
        result.append((rune, tuple(paths)))
    return tuple(result)


def catalog_payload():
    return [
        {
            "id": rune.id,
            "name": rune.name,
            "intent": rune.intent,
            "preview": png_data_url(rasterize(rune.points)),
            "template_count": TEMPLATE_COUNT,
        }
        for rune in RUNES
    ]
