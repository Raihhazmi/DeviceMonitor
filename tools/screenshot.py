#!/usr/bin/env python3
"""Ambil screenshot seluruh halaman dashboard (dark & light) untuk verifikasi visual.
Tahan-crash: setiap halaman memakai context baru, dengan retry bila renderer crash."""
import os
import sys
from playwright.sync_api import sync_playwright

BASE = os.environ.get("DM_BASE", "http://127.0.0.1:5080")
OUT = os.environ.get("DM_SHOTS", "/tmp/shots")
os.makedirs(OUT, exist_ok=True)

PAGES = [
    ("01-overview", "/Overview"),
    ("02-hardware", "/Hardware"),
    ("03-storage", "/Storage"),
    ("04-agent", "/Agent"),
    ("05-software", "/Software"),
    ("06-data", "/Data"),
    ("07-device-detail", "/Device/Details/TEST-SMOKE-01"),
]

THEMES = sys.argv[1:] or ["dark", "light"]
ARGS = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu", "--js-flags=--max-old-space-size=1024"]
errors = []


def shoot(browser, theme, name, path, full=True):
    ctx = browser.new_context(viewport={"width": 1560, "height": 980}, device_scale_factor=1)
    page = ctx.new_page()
    page.on("console", lambda m: errors.append(f"[{theme}/{name}] console: {m.text}") if m.type == "error" else None)
    page.on("pageerror", lambda e: errors.append(f"[{theme}/{name}] pageerror: {e}"))
    page.goto(BASE + "/Overview", wait_until="domcontentloaded")
    page.evaluate(f"localStorage.setItem('dm-theme','{theme}')")
    page.goto(BASE + path, wait_until="networkidle")
    page.evaluate("document.querySelectorAll('input[data-auto-refresh]').forEach(e=>e.checked=false)")
    page.wait_for_timeout(1100)
    page.evaluate("window.DM && window.DM.charts.render()")
    page.wait_for_timeout(600)
    canvases = page.evaluate("Array.from(document.querySelectorAll('canvas')).filter(c=>c.width>0).length")
    total = page.evaluate("document.querySelectorAll('canvas').length")
    out = f"{OUT}/{name}-{theme}.png"
    try:
        page.screenshot(path=out, full_page=full)
    except Exception as e:
        errors.append(f"[{theme}/{name}] screenshot full gagal ({str(e)[:60]}), pakai viewport")
        page.screenshot(path=out, full_page=False)
    ctx.close()
    print(f"{theme:5} {name:18} canvas {canvases}/{total}  {os.path.getsize(out)//1024:>5} KB")


with sync_playwright() as p:
    browser = p.chromium.launch(channel="chromium", args=ARGS)
    for theme in THEMES:
        for name, path in PAGES:
            for attempt in range(3):
                try:
                    shoot(browser, theme, name, path)
                    break
                except Exception as e:
                    errors.append(f"[{theme}/{name}] attempt {attempt + 1}: {str(e)[:100]}")
                    try:
                        browser.close()
                    except Exception:
                        pass
                    browser = p.chromium.launch(channel="chromium", args=ARGS)
    browser.close()

print("\nErrors:", len(errors))
for e in errors[:25]:
    print(" -", e[:200])
