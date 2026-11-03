#!/usr/bin/env python3
"""
Makes the MSIX visual assets (windows/packaging/msix/Assets/*.png) from the InstaWords icon,
site/icon.svg, redrawn here with Pillow so the output is the same on every machine (no SVG
renderer needed). The PNGs are committed; run this again only when the icon changes:

    pip install Pillow          # any recent version
    python3 windows/packaging/msix/make-assets.py

Sizes follow the MSIX/Partner Center asset rules: every logo at scale-100/125/150/200/400, and the
app-list/taskbar icon (Square44x44Logo) also at targetsize-16/24/32/48/256, plain, altform-unplated
and altform-lightunplated (the icon draws its own rounded plate, so the three look the same).
"""
import math
import os
import sys

from PIL import Image, ImageChops, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Assets")

# site/icon.svg, viewBox 0 0 64 64
GRADIENT = ((0x4C, 0x9A, 0x1E), (0x0F, 0x76, 0x6E))  # top-left -> bottom-right
LIME = (0xD9, 0xF9, 0x9D)
WHITE = (255, 255, 255)

SCALES = (100, 125, 150, 200, 400)
TARGET_SIZES = (16, 24, 32, 48, 256)


def cubic(p0, p1, p2, p3, steps=64):
    pts = []
    for i in range(steps + 1):
        t = i / steps
        u = 1 - t
        pts.append((
            u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0],
            u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1],
        ))
    return pts


def rounded_rect_outline(x, y, w, h, r, steps_per_arc=48):
    """The outline of an SVG <rect rx> as points, starting at (x + r, y), clockwise like SVG."""
    pts = []

    def arc(cx, cy, a0):
        for i in range(steps_per_arc + 1):
            a = math.radians(a0 + 90 * i / steps_per_arc)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))

    pts.append((x + r, y))
    pts.append((x + w - r, y))
    arc(x + w - r, y + r, -90)
    pts.append((x + w, y + h - r))
    arc(x + w - r, y + h - r, 0)
    pts.append((x + r, y + h))
    arc(x + r, y + h - r, 90)
    pts.append((x, y + r))
    arc(x + r, y + r, 180)
    return pts


def dashes(points, on, off):
    """Splits a closed polyline into dash polylines (SVG stroke-dasharray="on off")."""
    segs = list(zip(points, points[1:] + points[:1]))
    out, current, pos, period = [], [], 0.0, on + off
    for (ax, ay), (bx, by) in segs:
        length = math.hypot(bx - ax, by - ay)
        if length == 0:
            continue
        done = 0.0
        while done < length:
            phase = pos % period
            drawing = phase < on
            step = min(length - done, (on - phase) if drawing else (period - phase))
            t0, t1 = done / length, (done + step) / length
            p0 = (ax + (bx - ax) * t0, ay + (by - ay) * t0)
            p1 = (ax + (bx - ax) * t1, ay + (by - ay) * t1)
            if drawing:
                if not current:
                    current.append(p0)
                current.append(p1)
            elif current:
                out.append(current)
                current = []
            done += step
            pos += step
    if current:
        out.append(current)
    return out


def stroke(draw, pts, width, fill, k, round_caps=True):
    """A stroked polyline. Round caps: a round brush stamped along the path (smooth at any size;
    Pillow's thick-line joins leave notches). Butt caps (the short dashes): a plain line."""
    scaled = [(x * k, y * k) for x, y in pts]
    if not round_caps:
        draw.line(scaled, fill=fill, width=max(1, round(width * k)))
        return
    r = width * k / 2
    step = max(0.5, r / 6)
    for (ax, ay), (bx, by) in zip(scaled, scaled[1:]):
        n = max(1, math.ceil(math.hypot(bx - ax, by - ay) / step))
        for i in range(n + 1):
            x, y = ax + (bx - ax) * i / n, ay + (by - ay) * i / n
            draw.ellipse((x - r, y - r, x + r, y + r), fill=fill)


def icon(size, detail=True):
    """The InstaWords icon as a size x size RGBA image (transparent outside the rounded square)."""
    ss = max(4, min(16, 2048 // size))  # supersampling
    n = size * ss
    k = n / 64.0

    # Diagonal gradient (x1=0 y1=0 x2=1 y2=1): t = (x + y) / 2.
    ramp = Image.linear_gradient("L").resize((n, n))
    t = ImageChops.add(ramp, ramp.transpose(Image.Transpose.ROTATE_90).transpose(Image.Transpose.FLIP_TOP_BOTTOM), scale=2)
    a = Image.new("RGB", (n, n), GRADIENT[0])
    b = Image.new("RGB", (n, n), GRADIENT[1])
    bg = Image.composite(b, a, t)

    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, n - 1, n - 1), radius=15 * k, fill=255)
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    img.paste(bg, (0, 0), mask)

    if detail:
        # Dashed inner frame, white at 38% opacity (too fine to read below 32 px, so left out there).
        layer = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        for dash in dashes(rounded_rect_outline(7, 7, 50, 50, 10), 2, 3):
            stroke(d, dash, 1.5, WHITE + (255,), k, round_caps=False)
        alpha = layer.getchannel("A").point(lambda v: round(v * 0.38))
        layer.putalpha(alpha)
        img = Image.alpha_composite(img, layer)

    d = ImageDraw.Draw(img)
    # Smile: M15 27 c0 9.5 6.6 14 17 14 s17-4.5 17-14
    smile = cubic((15, 27), (15, 36.5), (21.6, 41), (32, 41)) + cubic((32, 41), (42.4, 41), (49, 36.5), (49, 27))[1:]
    stroke(d, smile, 5.5, WHITE, k)
    for cx in (27.5, 37.5):
        r = 3.6 * k
        d.ellipse((cx * k - r, 19.5 * k - r, cx * k + r, 19.5 * k + r), fill=WHITE)
    stroke(d, [(17, 50), (47, 50)], 3, LIME, k)

    return img.resize((size, size), Image.Resampling.LANCZOS)


def canvas(w, h, icon_px, dy=0):
    """A transparent w x h image with the icon centered (moved up by dy for the tile name)."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ic = icon(icon_px)
    img.alpha_composite(ic, ((w - icon_px) // 2, (h - icon_px) // 2 - dy))
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
        # App list / taskbar icon: the icon fills the box (it has its own plate).
        made.append(save(icon(round(44 * f)), f"Square44x44Logo.scale-{s}.png"))
        # Start tile: icon at half size, a little above center so the name fits underneath.
        t = round(150 * f)
        made.append(save(canvas(t, t, round(t * 0.5), dy=round(t * 0.05)), f"Square150x150Logo.scale-{s}.png"))
        w, h = round(310 * f), round(150 * f)
        made.append(save(canvas(w, h, round(h * 0.5), dy=round(h * 0.05)), f"Wide310x150Logo.scale-{s}.png"))
        made.append(save(icon(round(50 * f)), f"StoreLogo.scale-{s}.png"))
    for ts in TARGET_SIZES:
        img = icon(ts, detail=ts >= 32)
        for suffix in ("", "_altform-unplated", "_altform-lightunplated"):
            made.append(save(img, f"Square44x44Logo.targetsize-{ts}{suffix}.png"))
    print(f"{len(made)} files in {os.path.relpath(OUT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
