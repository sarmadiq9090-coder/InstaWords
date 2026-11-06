#!/usr/bin/env python3
"""
Makes the MSIX visual assets (windows/packaging/msix/Assets/*.png) from the InstaWords icon,
FastKiwi brand v2: the SVG masters in windows/branding/, rasterized with Pillow alone by
windows/branding/brandicon.py, so the output is the same on every machine (no SVG renderer
needed). The PNGs are committed; run this again only when the icon changes:

    pip install Pillow          # any recent version
    python3 windows/packaging/msix/make-assets.py

Sizes follow the MSIX/Partner Center asset rules: every logo at scale-100/125/150/200/400, and the
app-list/taskbar icon (Square44x44Logo) also at targetsize-16/24/32/48/256. The file names match
AppxManifest.xml and brand/icons/instawords/msix/ in the site repo (which has more targetsizes).

  - Plated, with the forest tile: Square44x44Logo.scale-*, Square44x44Logo.targetsize-N (Start,
    app list), Square150x150Logo, Wide310x150Logo, StoreLogo.
  - targetsize-N_altform-unplated (dark taskbar): the cheque alone in coral, Windows 11 style, its
    forest details cut out; targetsize-N_altform-lightunplated (light taskbar): the same in ink
    #D03400, because coral is too faint on a light taskbar (owner's decision, brand/README.md).
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Assets")
sys.path.insert(0, os.path.join(HERE, "..", "..", "branding"))
sys.dont_write_bytecode = True  # no __pycache__ in the repo
import brandicon  # noqa: E402

SCALES = (100, 125, 150, 200, 400)
TARGET_SIZES = (16, 24, 32, 48, 256)


def canvas(w, h, icon_px, dy=0):
    """A transparent w x h image with the icon centered (moved up by dy for the tile name)."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    img.alpha_composite(brandicon.icon(icon_px), ((w - icon_px) // 2, (h - icon_px) // 2 - dy))
    return img


def save(img, name):
    img.save(os.path.join(OUT, name), optimize=True)
    return name


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".png"):
            os.remove(os.path.join(OUT, f))
    made = []
    for s in SCALES:
        f = s / 100
        # App list icon: the tile fills the box.
        made.append(save(brandicon.icon(round(44 * f)), f"Square44x44Logo.scale-{s}.png"))
        # Start tile: icon at half size, a little above center so the name fits underneath.
        t = round(150 * f)
        made.append(save(canvas(t, t, round(t * 0.5), dy=round(t * 0.05)), f"Square150x150Logo.scale-{s}.png"))
        w, h = round(310 * f), round(150 * f)
        made.append(save(canvas(w, h, round(h * 0.5), dy=round(h * 0.05)), f"Wide310x150Logo.scale-{s}.png"))
        made.append(save(brandicon.icon(round(50 * f)), f"StoreLogo.scale-{s}.png"))
    for ts in TARGET_SIZES:
        made.append(save(brandicon.icon(ts), f"Square44x44Logo.targetsize-{ts}.png"))
        made.append(save(brandicon.glyph(ts), f"Square44x44Logo.targetsize-{ts}_altform-unplated.png"))
        made.append(save(brandicon.glyph(ts, light=True), f"Square44x44Logo.targetsize-{ts}_altform-lightunplated.png"))
    print(f"{len(made)} files in {os.path.relpath(OUT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
