"""
The InstaWords app icon (FastKiwi brand v2), rasterized from the SVG masters in this folder with
Pillow alone, so the output is the same on every machine (no SVG renderer or native library).
Used by make-icons.py (the .ico and the in-app logo) and ../packaging/msix/make-assets.py.

The SVGs are copied unchanged from the FastKiwi site repo (sarmadiq9090-coder.github.io, main,
brand/icons/instawords/, brand v2, site PR #19); brand/README.md there says which drawing to use
at which size. Replace them with newer copies from there and run both scripts again; never edit
them here.

    instawords.svg          the full tile (256 grid) with the lime seed, used from 41 px up
    instawords-NN.svg       hand-tuned 16/20/24/32 px drawings without the seed, used up to 40 px

What this module adds, following brand/source/export.py:
  - the tile's lighter edge is redrawn 1 px wide at every output size;
  - the unplated taskbar glyph (the symbol alone, Windows 11 style): the cheque without the tile,
    its forest details cut out as holes, in coral (dark taskbar) or ink #D03400 (light taskbar).

Only the SVG features these files use are supported: <path fill fill-rule d> with the absolute
commands M L H V A Q C Z, filled with the nonzero or evenodd rule.
"""
import io
import math
import os
import re
import struct

from PIL import Image, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))

FOREST = "#123B2A"
EDGE = "#2F6249"
LIME = "#B9F34A"
CORAL = "#FFAA8E"
INK = "#D03400"     # coral's ink tone for the light taskbar (4.54:1 on #F3F3F3)
TILE = 256.0
CROP = 200.0        # unplated full glyph: the middle 200 of the 256 grid fills the canvas
SMALL_GRIDS = (16, 20, 24, 32)


# ---------------------------------------------------------------- SVG
def _num(v):
    return float(v)


def load(name):
    """[(fill, evenodd, d)] and the grid size of windows/branding/<name>."""
    with open(os.path.join(HERE, name), encoding="utf-8") as fh:
        text = fh.read()
    vb = re.search(r'viewBox="0 0 ([\d.]+) ([\d.]+)"', text)
    paths = []
    for m in re.finditer(r"<path\b([^>]*)/>", text):
        attrs = dict(re.findall(r'([\w-]+)="([^"]*)"', m.group(1)))
        paths.append((attrs["fill"].upper(), attrs.get("fill-rule") == "evenodd", attrs["d"]))
    return paths, float(vb.group(1))


def _tokens(d):
    for m in re.finditer(r"([A-Za-z])|(-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)", d):
        yield m.group(1) or float(m.group(2))


def _arc(x1, y1, rx, ry, phi, large, sweep, x2, y2):
    """Points along an SVG elliptical arc (endpoint parameterization, SVG 1.1 F.6.5)."""
    if rx == 0 or ry == 0 or (x1 == x2 and y1 == y2):
        return [(x2, y2)]
    rx, ry = abs(rx), abs(ry)
    c, s = math.cos(math.radians(phi)), math.sin(math.radians(phi))
    dx, dy = (x1 - x2) / 2, (y1 - y2) / 2
    xp, yp = c * dx + s * dy, -s * dx + c * dy
    lam = (xp * xp) / (rx * rx) + (yp * yp) / (ry * ry)
    if lam > 1:
        rx, ry = rx * math.sqrt(lam), ry * math.sqrt(lam)
    num = rx * rx * ry * ry - rx * rx * yp * yp - ry * ry * xp * xp
    den = rx * rx * yp * yp + ry * ry * xp * xp
    k = math.sqrt(max(0.0, num / den))
    if large == sweep:
        k = -k
    cxp, cyp = k * rx * yp / ry, -k * ry * xp / rx
    cx, cy = c * cxp - s * cyp + (x1 + x2) / 2, s * cxp + c * cyp + (y1 + y2) / 2

    def ang(ux, uy, vx, vy):
        a = math.atan2(ux * vy - uy * vx, ux * vx + uy * vy)
        return a

    t1 = ang(1, 0, (xp - cxp) / rx, (yp - cyp) / ry)
    dt = ang((xp - cxp) / rx, (yp - cyp) / ry, (-xp - cxp) / rx, (-yp - cyp) / ry)
    if not sweep and dt > 0:
        dt -= 2 * math.pi
    elif sweep and dt < 0:
        dt += 2 * math.pi
    n = max(4, math.ceil(abs(dt) / (math.pi / 64)))
    pts = []
    for i in range(1, n + 1):
        t = t1 + dt * i / n
        ex, ey = rx * math.cos(t), ry * math.sin(t)
        pts.append((c * ex - s * ey + cx, s * ex + c * ey + cy))
    pts[-1] = (x2, y2)
    return pts


def flatten(d):
    """An SVG path's subpaths as closed polygons, in path units."""
    toks = list(_tokens(d))
    i, cmd = 0, None
    polys, cur = [], []
    x = y = sx = sy = 0.0

    def take(n):
        nonlocal i
        v = toks[i:i + n]
        i += n
        return v

    while i < len(toks):
        if isinstance(toks[i], str):
            cmd = toks[i]
            i += 1
        if cmd == "M":
            if len(cur) > 2:
                polys.append(cur)
            x, y = take(2)
            sx, sy = x, y
            cur = [(x, y)]
            cmd = "L"  # further pairs are line-tos
        elif cmd == "L":
            x, y = take(2)
            cur.append((x, y))
        elif cmd == "H":
            (x,) = take(1)
            cur.append((x, y))
        elif cmd == "V":
            (y,) = take(1)
            cur.append((x, y))
        elif cmd == "Q":
            qx, qy, ex, ey = take(4)
            for k in range(1, 17):
                t = k / 16
                u = 1 - t
                cur.append((u * u * x + 2 * u * t * qx + t * t * ex, u * u * y + 2 * u * t * qy + t * t * ey))
            x, y = ex, ey
        elif cmd == "C":
            ax, ay, bx, by, ex, ey = take(6)
            for k in range(1, 33):
                t = k / 32
                u = 1 - t
                cur.append((u ** 3 * x + 3 * u * u * t * ax + 3 * u * t * t * bx + t ** 3 * ex,
                            u ** 3 * y + 3 * u * u * t * ay + 3 * u * t * t * by + t ** 3 * ey))
            x, y = ex, ey
        elif cmd == "A":
            rx, ry, phi, large, sweep, ex, ey = take(7)
            cur += _arc(x, y, rx, ry, phi, bool(large), bool(sweep), ex, ey)
            x, y = ex, ey
        elif cmd == "Z":
            if len(cur) > 2:
                polys.append(cur)
            cur = []
            x, y = sx, sy
            cmd = None
        else:
            raise ValueError(f"unsupported SVG path command {cmd!r}")
    if len(cur) > 2:
        polys.append(cur)
    return polys


# ---------------------------------------------------------------- rasterizer
def coverage(d, evenodd, size, vb_origin, vb_size, ss):
    """The path's coverage as a size x size "L" mask: ss x ss samples per pixel (box filter)."""
    n = size * ss
    k = n / vb_size
    edges = []  # (y0, y1, x at y0, dx/dy, winding)
    for poly in flatten(d):
        pts = [((px - vb_origin) * k, (py - vb_origin) * k) for px, py in poly]
        for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]):
            if ay == by:
                continue
            w = 1 if by > ay else -1
            if ay > by:
                ax, ay, bx, by = bx, by, ax, ay
            edges.append((ay, by, ax, (bx - ax) / (by - ay), w))
    buf = bytearray(n * n)
    if edges:
        y_lo = max(0, math.floor(min(e[0] for e in edges)))
        y_hi = min(n, math.ceil(max(e[1] for e in edges)) + 1)
        edges.sort()
        for row in range(y_lo, y_hi):
            yc = row + 0.5
            xs = [(e[2] + (yc - e[0]) * e[3], e[4]) for e in edges if e[0] <= yc < e[1]]
            if not xs:
                continue
            xs.sort()
            wind, start, base = 0, 0.0, row * n
            for xv, w in xs:
                inside = (wind % 2 == 1) if evenodd else (wind != 0)
                wind += w
                now = (wind % 2 == 1) if evenodd else (wind != 0)
                if not inside and now:
                    start = xv
                elif inside and not now:
                    x0 = max(0, math.ceil(start - 0.5))
                    x1 = min(n, math.ceil(xv - 0.5))
                    if x1 > x0:
                        buf[base + x0:base + x1] = b"\xff" * (x1 - x0)
    return Image.frombytes("L", (n, n), bytes(buf)).reduce(ss)


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def paint(paths, size, vb_origin, vb_size):
    """Composites the paths in order (each by its own coverage, like an SVG renderer)."""
    ss = max(4, min(16, 2048 // size))
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    for fill, evenodd, d in paths:
        layer = Image.new("RGBA", (size, size), rgb(fill) + (0,))
        layer.putalpha(coverage(d, evenodd, size, vb_origin, vb_size, ss))
        img = Image.alpha_composite(img, layer)
    return img


# ---------------------------------------------------------------- the icon
def f(v):
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def rrect(x0, y0, x1, y1, r):
    """A rounded rectangle as path data, drawn exactly like brand/source/gen.py."""
    return (f"M{f(x0 + r)} {f(y0)}H{f(x1 - r)}A{f(r)} {f(r)} 0 0 1 {f(x1)} {f(y0 + r)}"
            f"V{f(y1 - r)}A{f(r)} {f(r)} 0 0 1 {f(x1 - r)} {f(y1)}H{f(x0 + r)}"
            f"A{f(r)} {f(r)} 0 0 1 {f(x0)} {f(y1 - r)}V{f(y0 + r)}A{f(r)} {f(r)} 0 0 1 {f(x0 + r)} {f(y0)}Z")


def tile(n, e):
    """The forest tile filling an n x n grid, with the lighter edge e grid units wide."""
    r = 0.22 * n
    return [(EDGE, False, rrect(0, 0, n, n, r)), (FOREST, False, rrect(e, e, n - e, n - e, r - e))]


def grid_for(size):
    """brand/README.md: 16-17 px grid 16, 18-21 grid 20, 22-27 grid 24, 28-40 grid 32, else 256."""
    if size > 40:
        return 256
    return 16 if size <= 17 else 20 if size <= 21 else 24 if size <= 27 else 32


def drawing(grid):
    """(glyph paths, seed paths) of one master: the tile is redrawn per size, so it is split off."""
    paths, n = load("instawords.svg" if grid == 256 else f"instawords-{grid}.svg")
    assert n == grid, (grid, n)
    # The masters' own tile is the 1-unit-edge tile; check it so a changed brand file is noticed.
    assert [p[:2] for p in paths[:2]] == [(EDGE, False), (FOREST, False)], "tile layers moved"
    assert paths[:2] == tile(grid, 1.0), f"instawords-{grid}: tile differs from the brand formula"
    body = paths[2:]
    return [p for p in body if p[0] != LIME], [p for p in body if p[0] == LIME]


def icon(size):
    """The plated icon (the tile), size x size RGBA, with a 1 px lighter edge."""
    g = grid_for(size)
    glyph, seed = drawing(g)
    return paint(tile(g, g / size) + glyph + seed, size, 0.0, float(g))


def glyph(size, light=False):
    """The unplated taskbar icon: the cheque alone, its forest details cut out as real holes."""
    g = grid_for(size)
    body, _ = drawing(g)
    o, vb = (0.0, float(g)) if g < 256 else ((TILE - CROP) / 2, CROP)
    if light:
        body = [(INK if p[0] == CORAL else p[0],) + p[1:] for p in body]
    color = paint(body, size, o, vb)
    mask = paint([("#000000" if p[0] == FOREST else "#FFFFFF",) + p[1:] for p in body], size, o, vb)
    holes = Image.alpha_composite(Image.new("RGBA", (size, size), (0, 0, 0, 255)), mask).convert("L")
    out = color.copy()
    out.putalpha(ImageChops.multiply(color.getchannel("A"), holes))
    return out


# ---------------------------------------------------------------- ICO
def _bmp_entry(img):
    """32-bit BGRA DIB with an AND mask: readable by every Windows version and resource tool."""
    w, h = img.size
    px = img.tobytes("raw", "BGRA")
    rows = [px[y * w * 4:(y + 1) * w * 4] for y in range(h)][::-1]
    stride = ((w + 31) // 32) * 4
    alpha = img.getchannel("A").tobytes()
    mask = b""
    for y in range(h - 1, -1, -1):
        bits = bytearray(stride)
        for x in range(w):
            if alpha[y * w + x] == 0:
                bits[x // 8] |= 0x80 >> (x % 8)
        mask += bytes(bits)
    hdr = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(px) + len(mask), 0, 0, 0, 0)
    return hdr + b"".join(rows) + mask


def ico_bytes(images):
    """A .ico from square RGBA images: BMP frames below 256 px, PNG at 256 (as brand/export.py)."""
    entries = []
    for img in images:
        s = img.width
        if s >= 256:
            b = io.BytesIO()
            img.save(b, "PNG", optimize=True)
            data = b.getvalue()
        else:
            data = _bmp_entry(img)
        entries.append((s, data))
    out = struct.pack("<HHH", 0, 1, len(entries))
    off = 6 + 16 * len(entries)
    blobs = b""
    for s, data in entries:
        out += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(data), off + len(blobs))
        blobs += data
    return out + blobs
