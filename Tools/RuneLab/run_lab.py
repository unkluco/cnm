"""Local, single-user Rune Lab. Run: python run_lab.py"""

from __future__ import annotations

import argparse
import ipaddress
import json
import re
import threading
import uuid
import webbrowser
from collections import OrderedDict
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

from recognizer import DEFAULT_MARGIN, DEFAULT_THRESHOLD, LOCAL, ROOT, RuneRecognizer, checked_limits, decide
from runes import RUNE_BY_ID, catalog_payload, png_data_url, validate_points

WEB = ROOT / "web"
MAX_BODY = 512 * 1024
STATIC = {
    "/": ("index.html", "text/html; charset=utf-8"),
    "/index.html": ("index.html", "text/html; charset=utf-8"),
    "/styles.css": ("styles.css", "text/css; charset=utf-8"),
    "/app.js": ("app.js", "application/javascript; charset=utf-8"),
    "/favicon.svg": ("favicon.svg", "image/svg+xml"),
}


class LabState:
    def __init__(self, recognizer: RuneRecognizer, data_dir: Path):
        self.recognizer = recognizer
        self.data_dir = data_dir
        self.samples_path = data_dir / "samples.jsonl"
        self.catalog = catalog_payload()
        self.model_info = recognizer.info()
        self.threshold = getattr(recognizer, "default_threshold", DEFAULT_THRESHOLD)
        self.margin = getattr(recognizer, "default_margin", DEFAULT_MARGIN)
        for rune in self.catalog:
            rune["template_count"] = self.model_info["references_per_rune"]
        self.is_candidate = self.model_info["variant"] == "lora"
        self.attempts = OrderedDict()
        self.lock = threading.Lock()
        # Reuse one inference thread across HTTP connections. On Windows the
        # OpenMP runtime can otherwise exhaust resources after many distinct
        # request threads each initialize their own CPU worker team.
        self.inference = ThreadPoolExecutor(max_workers=1, thread_name_prefix="rune-inference")
        data_dir.mkdir(parents=True, exist_ok=True)

    def close(self):
        self.inference.shutdown(wait=True, cancel_futures=True)

    @staticmethod
    def count_file(path: Path) -> int:
        if not path.exists():
            return 0
        with path.open("r", encoding="utf-8") as reader:
            return sum(1 for line in reader if line.strip())

    def sample_count(self) -> int:
        return self.count_file(self.samples_path)

    def analyze(self, payload: dict) -> dict:
        points = validate_points(payload.get("points"))
        threshold, margin = checked_limits(
            payload.get("threshold", self.threshold), payload.get("margin", self.margin)
        )
        input_method = payload.get("input_method", "unknown")
        if input_method not in ("mouse", "touch", "pen"):
            input_method = "unknown"
        result = self.inference.submit(self.recognizer.recognize, points, threshold, margin).result()
        image = result.pop("image")
        attempt_id = uuid.uuid4().hex
        with self.lock:
            self.attempts[attempt_id] = {
                "points": points.tolist(),
                "input_method": input_method,
                "result": result,
                "recorded": False,
            }
            while len(self.attempts) > 128:
                self.attempts.popitem(last=False)
        return {
            "id": attempt_id,
            **result,
            "preview": png_data_url(image) if image is not None else None,
        }

    def record(self, payload: dict) -> dict:
        attempt_id = payload.get("id")
        expected = payload.get("expected")
        participant = payload.get("participant")
        if not isinstance(participant, str) or not re.fullmatch(r"[A-Za-z0-9_.-]{1,40}", participant):
            raise ValueError("Mã người vẽ cần 1–40 ký tự chữ, số, dấu gạch hoặc dấu chấm.")
        if not isinstance(expected, str) or (expected not in RUNE_BY_ID and expected != "invalid"):
            raise ValueError("Hãy chọn ký hiệu định vẽ hoặc 'Nét không hợp lệ'.")
        threshold, margin = checked_limits(payload.get("threshold"), payload.get("margin"))
        with self.lock:
            attempt = self.attempts.get(attempt_id) if isinstance(attempt_id, str) else None
            if attempt is None:
                raise ValueError("Lượt vẽ đã hết hạn. Hãy vẽ lại trước khi lưu.")
            if attempt["recorded"]:
                raise ValueError("Lượt vẽ này đã được lưu.")
            result = attempt["result"]
            record = {
                "schema_version": 1,
                "id": attempt_id,
                "recorded_at": datetime.now(timezone.utc).isoformat(),
                "source": "interactive",
                "participant": participant,
                "expected": expected,
                "input_method": attempt["input_method"],
                "points": attempt["points"],
                "stroke": result["stroke"],
                "scores": result["scores"],
                "decision": decide(result["scores"], threshold, margin),
                "threshold": threshold,
                "margin": margin,
                "model_ms": result["model_ms"],
                "total_ms": result["total_ms"],
                "model": self.recognizer.info(),
            }
            with self.samples_path.open("a", encoding="utf-8", newline="\n") as writer:
                writer.write(json.dumps(record, ensure_ascii=False, allow_nan=False) + "\n")
            attempt["recorded"] = True
            return {"saved": True, "count": self.sample_count()}


class LabHandler(BaseHTTPRequestHandler):
    server_version = "RuneLab/1.0"

    def send_bytes(self, data: bytes, content_type: str, status=200, extra_headers=()):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header(
            "Content-Security-Policy",
            "default-src 'self'; img-src 'self' data:; script-src 'self'; "
            "style-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'",
        )
        for name, value in extra_headers:
            self.send_header(name, value)
        self.end_headers()
        try:
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            # A newer drawing may cancel an outstanding browser request.
            pass

    def send_json(self, obj, status=200):
        self.send_bytes(
            json.dumps(obj, ensure_ascii=False, allow_nan=False).encode("utf-8"),
            "application/json; charset=utf-8",
            status,
        )

    @property
    def state(self) -> LabState:
        return self.server.state

    def valid_host(self):
        try:
            hostname = urlsplit("//" + self.headers.get("Host", "")).hostname
        except ValueError:
            return False
        if hostname == "localhost":
            return True
        try:
            address = ipaddress.ip_address(hostname or "")
        except ValueError:
            return False
        return address.is_loopback or (getattr(self.server, "lan", False) and address.is_private)

    def do_GET(self):
        if not self.valid_host():
            self.send_json({"error": "Địa chỉ truy cập Rune Lab không hợp lệ."}, 403)
            return
        path = urlsplit(self.path).path
        if path in STATIC:
            filename, content_type = STATIC[path]
            try:
                self.send_bytes((WEB / filename).read_bytes(), content_type)
            except OSError:
                self.send_json({"error": "Thiếu tài nguyên giao diện."}, 500)
        elif path == "/api/catalog":
            with self.state.lock:
                self.send_json(
                    {
                        "runes": self.state.catalog,
                        "model": self.state.model_info,
                        "threshold": self.state.threshold,
                        "margin": self.state.margin,
                        "samples_saved": self.state.sample_count(),
                        "baseline_samples_saved": self.state.count_file(LOCAL / "data" / "samples.jsonl") if self.state.is_candidate else None,
                        "presets": [
                            {"id": "calibrated", "name": "Đã hiệu chỉnh", "threshold": self.state.threshold, "margin": self.state.margin},
                            {"id": "strict", "name": "Chặt hơn", "threshold": 0.90, "margin": 0.05},
                        ] if self.state.is_candidate else [],
                    }
                )
        elif path == "/api/samples.jsonl":
            with self.state.lock:
                data = self.state.samples_path.read_bytes() if self.state.samples_path.exists() else b""
            self.send_bytes(
                data,
                "application/x-ndjson; charset=utf-8",
                extra_headers=(("Content-Disposition", 'attachment; filename="rune-lab-samples.jsonl"'),),
            )
        else:
            self.send_json({"error": "Không tìm thấy."}, 404)

    def read_payload(self):
        origin = self.headers.get("Origin")
        host = self.headers.get("Host", "")
        if origin and origin != f"http://{host}":
            raise ValueError("Yêu cầu phải đến từ giao diện Rune Lab.")
        if self.headers.get_content_type() != "application/json":
            raise ValueError("Yêu cầu phải là JSON.")
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError as exc:
            raise ValueError("Kích thước yêu cầu không hợp lệ.") from exc
        if not 0 < length <= MAX_BODY:
            raise ValueError("Kích thước yêu cầu vượt giới hạn.")
        try:
            payload = json.loads(self.rfile.read(length))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise ValueError("JSON không hợp lệ.") from exc
        if not isinstance(payload, dict):
            raise ValueError("JSON phải là một đối tượng.")
        return payload

    def do_POST(self):
        if not self.valid_host():
            self.send_json({"error": "Địa chỉ truy cập Rune Lab không hợp lệ."}, 403)
            return
        path = urlsplit(self.path).path
        if path not in ("/api/recognize", "/api/samples"):
            self.send_json({"error": "Không tìm thấy."}, 404)
            return
        try:
            payload = self.read_payload()
            if path == "/api/recognize":
                self.send_json(self.state.analyze(payload))
            else:
                self.send_json(self.state.record(payload), 201)
        except ValueError as exc:
            self.send_json({"error": str(exc)}, 400)
        except Exception as exc:
            print(f"Rune Lab error: {type(exc).__name__}: {exc}", flush=True)
            self.send_json({"error": "Không thể xử lý lượt vẽ. Kiểm tra cửa sổ chạy Rune Lab."}, 500)

    def log_message(self, format, *args):
        pass


class LabServer(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = True

    def server_close(self):
        super().server_close()
        state = getattr(self, "state", None)
        if state is not None:
            state.close()


def main():
    parser = argparse.ArgumentParser(description="Light Souls — frozen DINOv2-distilled ViT-Tiny Rune Lab")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--device", choices=("cpu", "cuda", "auto"), default="cpu")
    parser.add_argument("--threads", type=int, default=4)
    parser.add_argument("--data-dir", type=Path, default=None)
    parser.add_argument("--candidate", type=Path, default=None, help="Load merged LoRA weights and their matching reference embeddings as an isolated trial.")
    parser.add_argument("--open-browser", action="store_true")
    parser.add_argument(
        "--lan", action="store_true",
        help="Explicit opt-in to trusted local Wi-Fi testing. Anyone on that network can access the lab and its saved samples.",
    )
    args = parser.parse_args()
    host = "0.0.0.0" if args.lan else "127.0.0.1"
    if not 1024 <= args.port <= 65535:
        parser.error("Choose a port between 1024 and 65535.")
    # Bind first so a port conflict is detected before loading the model.
    with LabServer((host, args.port), LabHandler) as server:
        server.lan = args.lan
        recognizer = RuneRecognizer(args.device, args.threads, args.candidate)
        data_dir = args.data_dir
        if data_dir is None:
            data_dir = (
                LOCAL / "trials" / ("lora-" + recognizer.model_sha256[:12])
                if args.candidate is not None else LOCAL / "data"
            )
        if args.candidate is not None and data_dir.resolve() == (LOCAL / "data").resolve():
            parser.error("Candidate trials must use a separate data directory to preserve the original sample set.")
        server.state = LabState(recognizer, data_dir.resolve())
        url = f"http://127.0.0.1:{args.port}"
        print(f"Rune Lab listening at {url}", flush=True)
        if args.lan:
            print("LAN enabled. Use only on a trusted network, then stop the server.", flush=True)
        if args.open_browser:
            webbrowser.open(url)
        try:
            server.serve_forever(poll_interval=0.25)
        except KeyboardInterrupt:
            print("Rune Lab stopped.", flush=True)


if __name__ == "__main__":
    main()
