"""Optional real Chromium UI QA with the live, frozen model.

pip install playwright
python tests/browser_smoke.py --browser-path <Chrome or Edge executable>
Uses an isolated temporary sample database; never writes player samples.
"""

from __future__ import annotations

import argparse
import sys
import tempfile
import threading
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from playwright.sync_api import sync_playwright, expect

from recognizer import LOCAL, RuneRecognizer
from run_lab import LabHandler, LabServer, LabState
from runes import RUNE_BY_ID


def canvas_coords(page, rune_id):
    canvas = page.locator("#ink")
    canvas.scroll_into_view_if_needed()
    rect = canvas.bounding_box()
    points = RUNE_BY_ID[rune_id].points[::3].tolist()
    final = RUNE_BY_ID[rune_id].points[-1].tolist()
    if points[-1] != final:
        points.append(final)
    return [(rect["x"] + x * rect["width"], rect["y"] + y * rect["height"]) for x, y in points]


def draw_mouse(page, rune_id):
    coords = canvas_coords(page, rune_id)
    page.mouse.move(*coords[0])
    page.mouse.down()
    for point in coords[1:]:
        page.mouse.move(*point)
    page.mouse.up()
    expect(page.locator("#score-list .score-row")).to_have_count(12)


def draw_touch(page, context, rune_id):
    coords = canvas_coords(page, rune_id)
    cdp = context.new_cdp_session(page)
    cdp.send("Input.dispatchTouchEvent", {
        "type": "touchStart", "touchPoints": [{"x": coords[0][0], "y": coords[0][1]}],
    })
    for x, y in coords[1:]:
        cdp.send("Input.dispatchTouchEvent", {
            "type": "touchMove", "touchPoints": [{"x": x, "y": y}],
        })
    cdp.send("Input.dispatchTouchEvent", {"type": "touchEnd", "touchPoints": []})
    expect(page.locator("#score-list .score-row")).to_have_count(12)


def assert_no_horizontal_overflow(page):
    assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth"), "Horizontal overflow"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--browser-path", type=Path, required=True)
    parser.add_argument("--candidate", type=Path, default=None)
    args = parser.parse_args()
    screenshots = LOCAL / "qa" / "lora-candidate" if args.candidate else LOCAL / "qa"
    screenshots.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=LOCAL) as temp:
        state = LabState(RuneRecognizer("cpu", 4, args.candidate), Path(temp))
        server = LabServer(("127.0.0.1", 0), LabHandler)
        server.state = state
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        url = f"http://127.0.0.1:{server.server_port}"
        try:
            with sync_playwright() as playwright:
                browser = playwright.chromium.launch(executable_path=str(args.browser_path), headless=True)
                errors = []
                page = browser.new_page(viewport={"width": 1440, "height": 980}, device_scale_factor=1)
                page.on("pageerror", lambda error: errors.append(str(error)))
                page.goto(url)
                expect(page.locator(".rune-card")).to_have_count(12)
                if args.candidate:
                    expect(page.locator("#trial-banner")).to_be_visible()
                    expect(page.locator("#model-name")).to_contain_text("LoRA")
                    expect(page.locator("#preset-buttons button")).to_have_count(2)
                    page.locator('[data-preset="strict"]').click()
                    expect(page.locator("#threshold")).to_have_value("0.9")
                    page.locator('[data-preset="calibrated"]').click()
                    expect(page.locator("#threshold")).to_have_value(str(state.threshold))
                else:
                    expect(page.locator("#trial-banner")).to_be_hidden()
                assert_no_horizontal_overflow(page)
                page.locator('[data-rune="lightning"]').click()
                expect(page.locator("#expected")).to_have_value("lightning")
                page.screenshot(path=screenshots / "desktop-idle.png", full_page=True)
                draw_mouse(page, "lightning")
                expect(page.locator("#verdict-title")).to_have_text("Tia chớp")
                expect(page.locator("#verdict")).to_have_class("verdict accepted")
                expect(page.locator("#preview-image img")).to_have_count(1)
                page.screenshot(path=screenshots / "desktop-recognized.png", full_page=True)
                page.locator("#participant").fill("QA-browser")
                page.locator("#save").click()
                expect(page.locator("#saved-count")).to_have_text("1")
                expect(page.locator("#save")).to_be_disabled()
                assert state.sample_count() == 1
                if args.candidate:
                    import json
                    sample = json.loads(state.samples_path.read_text(encoding="utf-8"))
                    assert sample["model"]["variant"] == "lora"
                    assert sample["model"]["sha256"] == state.recognizer.model_sha256

                page.locator("#threshold").evaluate("(slider) => {slider.value='1';slider.dispatchEvent(new Event('input',{bubbles:true}));}")
                expect(page.locator("#verdict")).to_have_class("verdict rejected")
                page.locator("#reset-limits").click()
                expect(page.locator("#verdict")).to_have_class("verdict accepted")
                page.locator("#clear").click()
                page.locator("#ink").click(position={"x": 100, "y": 100})
                expect(page.locator("#verdict-title")).to_have_text("Nét vẽ quá ngắn")
                expect(page.locator("#score-list .score-row")).to_have_count(0)
                page.locator("#clear").click()
                expect(page.locator("#verdict-title")).to_have_text("Chưa có kết quả")
                print("Desktop: real drawing, recognition, threshold re-judgment, save, tap rejection and clear passed.")

                touch = browser.new_context(
                    viewport={"width": 390, "height": 844}, device_scale_factor=2, is_mobile=True, has_touch=True
                )
                mobile = touch.new_page()
                mobile.on("pageerror", lambda error: errors.append(str(error)))
                mobile.goto(url)
                expect(mobile.locator(".rune-card")).to_have_count(12)
                assert_no_horizontal_overflow(mobile)
                mobile.locator("#expected").select_option("lightning")
                draw_touch(mobile, touch, "lightning")
                expect(mobile.locator("#verdict-title")).to_have_text("Tia chớp")
                expect(mobile.locator("#verdict")).to_have_class("verdict accepted")
                mobile.screenshot(path=screenshots / "mobile-recognized.png", full_page=True)
                print("Mobile viewport: touch drawing, recognition, responsive layout passed.")
                assert not errors, errors
                browser.close()
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)
    print(f"Browser screenshots: {screenshots}")


if __name__ == "__main__":
    main()
