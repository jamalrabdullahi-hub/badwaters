"""Builds the map's contour lines from NOAA ETOPO 2022 (60 arc-sec surface elevation, public domain).

Input:  tools/geo-src/ETOPO_2022_v1_60s_N90W180_surface.tif (downloaded once; not shipped)
Output: data/core/geo/contours-major.json (every 1000 m on land, -1000/-3000 m at sea)
        data/core/geo/contours.json       (250 / 500 / 1500 / 2500 m ... the in-between land levels)

Deliberately coarse ("not extremely detailed"): the grid is averaged down to 0.1 degree (~11 km) before contouring,
then lines are chained and simplified, so the whole world costs a few MB and draws as one hairline mesh per layer.
General relief only: no site data of any kind is read.
"""
import json, math, os, sys
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "geo-src", "ETOPO_2022_v1_60s_N90W180_surface.tif")
OUT = os.path.join(HERE, "..", "data", "core", "geo")
STEP = 0.1  # degrees per contouring cell
MAJOR = [1000, 2000, 3000, 4000, 5000, 6000, -1000, -3000]
MINOR = [250, 500, 1500, 2500, 3500, 4500]


def load_grid():
    try:
        img = Image.open(SRC)
        a = np.asarray(img, dtype=np.float32)
    except Exception:
        import tifffile  # pip install tifffile imagecodecs (the file is deflate-compressed float)
        a = tifffile.imread(SRC).astype(np.float32)
    h, w = a.shape  # 10800 x 21600, rows from 90 N, columns from 180 W
    k = int(round(STEP * w / 360))
    a = a[: h - h % k, : w - w % k].reshape(h // k, k, w // k, k).mean(axis=(1, 3))
    return a


def segments(z, level):
    """Marching squares: return (N, 4) segments in grid coordinates (col, row)."""
    b = (z >= level).astype(np.uint8)
    idx = b[:-1, :-1] * 8 + b[:-1, 1:] * 4 + b[1:, 1:] * 2 + b[1:, :-1] * 1
    rows, cols = np.nonzero((idx != 0) & (idx != 15))
    tl, tr, br, bl = z[rows, cols], z[rows, cols + 1], z[rows + 1, cols + 1], z[rows + 1, cols]
    def interp(a, b): return np.clip((level - a) / np.where(b - a == 0, 1e-9, b - a), 0, 1)
    top = np.stack([cols + interp(tl, tr), rows + 0.0], 1)
    right = np.stack([cols + 1.0, rows + interp(tr, br)], 1)
    bottom = np.stack([cols + interp(bl, br), rows + 1.0], 1)
    left = np.stack([cols + 0.0, rows + interp(tl, bl)], 1)
    edges = {"t": top, "r": right, "b": bottom, "l": left}
    table = {1: ["lb"], 2: ["br"], 3: ["lr"], 4: ["tr"], 5: ["tl", "br"], 6: ["tb"], 7: ["tl"],
             8: ["tl"], 9: ["tb"], 10: ["tr", "lb"], 11: ["tr"], 12: ["lr"], 13: ["br"], 14: ["lb"]}
    out = []
    code = idx[rows, cols]
    for c, pairs in table.items():
        m = code == c
        if not m.any(): continue
        for p in pairs:
            out.append(np.concatenate([edges[p[0]][m], edges[p[1]][m]], 1))
    return np.concatenate(out) if out else np.zeros((0, 4))


def chain(segs):
    """Join segments sharing endpoints into polylines."""
    key = lambda x, y: (round(x, 4), round(y, 4))
    ends = {}
    for i, (x1, y1, x2, y2) in enumerate(segs):
        ends.setdefault(key(x1, y1), []).append(i); ends.setdefault(key(x2, y2), []).append(i)
    used = np.zeros(len(segs), bool)
    lines = []
    for i in range(len(segs)):
        if used[i]: continue
        used[i] = True
        x1, y1, x2, y2 = segs[i]
        line = [(x1, y1), (x2, y2)]
        for forward in (True, False):
            while True:
                end = line[-1] if forward else line[0]
                nxt = next((j for j in ends.get(key(*end), []) if not used[j]), None)
                if nxt is None: break
                used[nxt] = True
                a, b = (segs[nxt][0], segs[nxt][1]), (segs[nxt][2], segs[nxt][3])
                p = b if key(*a) == key(*end) else a
                if forward: line.append(p)
                else: line.insert(0, p)
        lines.append(line)
    return lines


def simplify(pts, tol):
    if len(pts) < 3: return pts
    if pts[0] == pts[-1] and len(pts) > 4:  # closed ring: the chord is zero, so simplify the two halves
        h = len(pts) // 2
        return simplify(pts[:h + 1], tol)[:-1] + simplify(pts[h:], tol)
    keep = [False] * len(pts); keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        a, b = stack.pop()
        (x1, y1), (x2, y2) = pts[a], pts[b]
        dx, dy = x2 - x1, y2 - y1; L = math.hypot(dx, dy) or 1e-12
        best, bi = 0, -1
        for i in range(a + 1, b):
            d = abs(dy * pts[i][0] - dx * pts[i][1] + x2 * y1 - y2 * x1) / L
            if d > best: best, bi = d, i
        if best > tol: keep[bi] = True; stack += [(a, bi), (bi, b)]
    return [p for p, k in zip(pts, keep) if k]


def build(z, levels, out, tol):
    res, n = [], 0
    for lv in levels:
        for line in chain(segments(z, lv)):
            ll = simplify([(round(-180 + (c + 0.5) * STEP, 3), round(90 - (r + 0.5) * STEP, 3)) for c, r in line], tol)
            if len(ll) < 3: continue  # drop specks
            res.append([v for p in ll for v in p]); n += len(ll)
    json.dump({"source": "NOAA ETOPO 2022 (public domain), contoured at 0.1 degree", "lines": res},
              open(os.path.join(OUT, out), "w", encoding="utf8"), separators=(",", ":"))
    print(f"{out:22} {len(res):6} lines {n:8} points {os.path.getsize(os.path.join(OUT, out)) / 1e6:.2f} MB")


if __name__ == "__main__":
    if not os.path.exists(SRC): sys.exit(f"missing {SRC}")
    z = load_grid()
    print("grid", z.shape)
    build(z, MAJOR, "contours-major.json", 0.03)
    build(z, MINOR, "contours.json", 0.03)
