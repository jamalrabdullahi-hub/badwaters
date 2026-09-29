"""One-time build of the Strategic Map's real-Earth layers from Natural Earth (public domain).

Reads tools/geo-src/*.geojson and SR_50M.tif (download: see docs in this file's README section below), and writes
data/core/geo/*.json plus data/core/geo/earth.png.

POLICY: only general atlas cartography is read. The allow-list below is the complete set of inputs. No power, energy,
pipeline, water, telecom or military facility data is imported, and military / dual-use airports are dropped.

earth.png (Mercator, lat 84..-80, 4096 wide): R = shaded relief, G = ocean depth band at sea / urban area on land,
B = lake.
"""
import json, math, os
from PIL import Image, ImageDraw

Image.MAX_IMAGE_PIXELS = None
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "geo-src")
OUT = os.path.join(HERE, "..", "data", "core", "geo")
ALLOW = {
    "ne_10m_admin_0_boundary_lines_land", "ne_10m_admin_0_disputed_areas", "ne_50m_admin_1_states_provinces_lines",
    "ne_10m_populated_places_simple", "ne_50m_urban_areas", "ne_10m_roads", "ne_10m_railroads", "ne_10m_ports",
    "ne_10m_airports", "ne_50m_rivers_lake_centerlines", "ne_50m_lakes", "ne_10m_geography_marine_polys",
    "ne_10m_bathymetry_K_200", "ne_10m_bathymetry_I_2000", "ne_10m_bathymetry_G_4000",
}


def load(name):
    assert name in ALLOW, f"{name} is not on the allow-list"
    return json.load(open(os.path.join(SRC, name + ".geojson"), encoding="utf8"))["features"]


def simplify(pts, tol):
    """Douglas-Peucker on [(lon, lat)]."""
    if len(pts) < 3: return pts
    keep = [False] * len(pts); keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        a, b = stack.pop()
        (x1, y1), (x2, y2) = pts[a], pts[b]
        dx, dy = x2 - x1, y2 - y1; L = math.hypot(dx, dy) or 1e-12
        best, bi = 0, -1
        for i in range(a + 1, b):
            d = abs(dy * (pts[i][0] - x1) - dx * (pts[i][1] - y1)) / L
            if d > best: best, bi = d, i
        if best > tol: keep[bi] = True; stack += [(a, bi), (bi, b)]
    return [p for p, k in zip(pts, keep) if k]


def lines(geom):
    t, c = geom["type"], geom["coordinates"]
    if t == "LineString": return [c]
    if t == "MultiLineString": return c
    if t == "Polygon": return c
    if t == "MultiPolygon": return [r for p in c for r in p]
    return []


def flat(pts): return [round(v, 3) for p in pts for v in p[:2]]


sizes = {}
def write(name, obj):
    p = os.path.join(OUT, name)
    json.dump(obj, open(p, "w", encoding="utf8"), separators=(",", ":"), ensure_ascii=False)
    sizes[name] = os.path.getsize(p)


def line_layer(out, feats, tol, keep=lambda p: True, min_pts=2):
    res = []
    for f in feats:
        if not f["geometry"] or not keep(f["properties"]): continue
        for ln in lines(f["geometry"]):
            s = simplify([tuple(p[:2]) for p in ln], tol)
            if len(s) >= min_pts: res.append(flat(s))
    write(out, {"source": "Natural Earth (public domain)", "lines": res})
    print(f"{out:14} {len(res):6} lines")


# ---- lines
line_layer("borders.json", load("ne_10m_admin_0_boundary_lines_land"), 0.02)
line_layer("disputed.json", load("ne_10m_admin_0_disputed_areas"), 0.02)
line_layer("admin1.json", load("ne_50m_admin_1_states_provinces_lines"), 0.03)
line_layer("roads.json", load("ne_10m_roads"), 0.03, lambda p: p["type"] in ("Major Highway", "Beltway") and p["scalerank"] <= 5)
# finer road net for zoomed-in theatre views: the remaining highways, secondary highways and roads (still civil atlas data)
line_layer("roads-minor.json", load("ne_10m_roads"), 0.01, lambda p: (p["type"] == "Major Highway" and p["scalerank"] > 5) or p["type"] in ("Secondary Highway", "Road"))
line_layer("rail.json", load("ne_10m_railroads"), 0.03, lambda p: p["scalerank"] <= 5)
line_layer("rivers.json", load("ne_50m_rivers_lake_centerlines"), 0.03, lambda p: (p.get("scalerank") or 9) <= 6)

# ---- points (civil only)
def points(out, feats, keep, kind, rank_key="scalerank", name_key="name"):
    res = []
    for f in feats:
        p = f["properties"]
        if not keep(p) or not f["geometry"]: continue
        x, y = f["geometry"]["coordinates"][:2]
        res.append({"n": p.get(name_key) or "", "lon": round(x, 3), "lat": round(y, 3), "k": kind(p), "r": p.get(rank_key) or 9})
    write(out, {"source": "Natural Earth (public domain)", "points": res})
    print(f"{out:14} {len(res):6} points")

points("places.json", load("ne_10m_populated_places_simple"),
       lambda p: p["featurecla"] in ("Admin-0 capital", "Admin-0 capital alt") or (p["featurecla"] in ("Populated place", "Admin-1 capital", "Admin-1 region capital", "Admin-0 region capital") and p["scalerank"] <= 4),
       lambda p: "capital" if p["featurecla"].startswith("Admin-0 capital") else "city")
points("towns.json", load("ne_10m_populated_places_simple"),
       lambda p: p["featurecla"] in ("Populated place", "Admin-1 capital", "Admin-1 region capital", "Admin-0 region capital") and p["scalerank"] > 4,
       lambda p: "town")
points("ports.json", load("ne_10m_ports"), lambda p: p["scalerank"] <= 5, lambda p: "port")
points("airports.json", load("ne_10m_airports"), lambda p: p["type"] == "major", lambda p: "airport")  # drops every military / dual-use / spaceport entry

# ---- sea and strait names: label at the bounding-box centre of the largest ring
res = []
for f in load("ne_10m_geography_marine_polys"):
    p = f["properties"]
    if p["featurecla"] not in ("sea", "gulf", "strait", "channel", "ocean", "bay") or (p["scalerank"] or 9) > 5 or not f["geometry"]: continue
    ring = max(lines(f["geometry"]), key=len)
    xs = [q[0] for q in ring]; ys = [q[1] for q in ring]
    res.append({"n": p["name"], "lon": round((min(xs) + max(xs)) / 2, 2), "lat": round((min(ys) + max(ys)) / 2, 2), "k": p["featurecla"], "r": p["scalerank"]})
write("marine.json", {"source": "Natural Earth (public domain)", "points": res}); print(f"{'marine.json':14} {len(res):6} points")

# ---- raster: relief + depth + urban/lakes
W, H = 4096, 2048
relief = Image.open(os.path.join(SRC, "SR_50M.tif")).convert("L").resize((W, H), Image.LANCZOS)
def px(lon, lat): return ((lon + 180) / 360 * W, (90 - lat) / 180 * H)

def paint(img, feats, fill, hole):
    d = ImageDraw.Draw(img)
    for f in feats:
        g = f["geometry"]
        if not g: continue
        polys = [g["coordinates"]] if g["type"] == "Polygon" else g["coordinates"] if g["type"] == "MultiPolygon" else []
        for poly in polys:
            d.polygon([px(*q[:2]) for q in poly[0]], fill=fill)
            for h in poly[1:]: d.polygon([px(*q[:2]) for q in h], fill=hole)

depth = Image.new("L", (W, H), 0)
paint(depth, load("ne_10m_bathymetry_K_200"), 85, 0)
paint(depth, load("ne_10m_bathymetry_I_2000"), 170, 85)
paint(depth, load("ne_10m_bathymetry_G_4000"), 255, 170)
paint(depth, load("ne_50m_urban_areas"), 255, 0)  # G is unused on land: urban areas go there
mask = Image.new("L", (W, H), 0)
paint(mask, load("ne_50m_lakes"), 255, 0)
eq = Image.merge("RGB", (relief, depth, mask))
# reproject to Mercator (lat 84..-80, the map's range) so the game shader samples it with a straight linear lookup
def merc(lat): return math.degrees(math.log(math.tan(math.pi / 4 + math.radians(lat) / 2)))
YMAX, YMIN = merc(84), merc(-80)
H2 = round(W * (YMAX - YMIN) / 360)
out = Image.new("RGB", (W, H2))
for row in range(H2):
    y = YMAX - (row + 0.5) / H2 * (YMAX - YMIN)
    lat = math.degrees(2 * math.atan(math.exp(math.radians(y))) - math.pi / 2)
    src = min(H - 1, max(0, int((90 - lat) / 180 * H)))
    out.paste(eq.crop((0, src, W, src + 1)), (0, row))
out.save(os.path.join(OUT, "earth.png"), optimize=True)
print(f"earth.png Mercator {W}x{H2}, y range {YMIN:.3f}..{YMAX:.3f}")
sizes["earth.png"] = os.path.getsize(os.path.join(OUT, "earth.png"))
# ---- satellite base: NASA Blue Marble Next Generation, topography + bathymetry (public domain, NASA Earth Observatory)
def mercator_rows(eq, width):
    w0, h0 = eq.size
    eq = eq.resize((width, h0 * width // w0), Image.LANCZOS)
    hh = eq.size[1]
    h2 = round(width * (YMAX - YMIN) / 360)
    out = Image.new(eq.mode, (width, h2))
    for row in range(h2):
        y = YMAX - (row + 0.5) / h2 * (YMAX - YMIN)
        lat = math.degrees(2 * math.atan(math.exp(math.radians(y))) - math.pi / 2)
        src = min(hh - 1, max(0, int((90 - lat) / 180 * hh)))
        out.paste(eq.crop((0, src, width, src + 1)), (0, row))
    return out

bm = os.path.join(SRC, "bluemarble-topo-bathy-21600.jpg")
if os.path.exists(bm):
    sat = mercator_rows(Image.open(bm).convert("RGB"), 8192)
    sat.save(os.path.join(OUT, "satellite.jpg"), quality=88, optimize=True)
    sizes["satellite.jpg"] = os.path.getsize(os.path.join(OUT, "satellite.jpg"))
    print(f"satellite.jpg Mercator {sat.size[0]}x{sat.size[1]}")
# ---- detail satellite tiles: the same Blue Marble at full resolution (21600 wide), Mercator, cut into 1024 px tiles
# (sat/<col>_<row>.jpg) that the game streams in only for the visible area when zoomed in. Built strip by strip to save memory.
if os.path.exists(bm):
    TW, T = 21600, 1024
    src = Image.open(bm).convert("RGB")
    sw, sh = src.size
    H2 = round(TW * (YMAX - YMIN) / 360)
    tdir = os.path.join(OUT, "sat"); os.makedirs(tdir, exist_ok=True)
    cols, rows = math.ceil(TW / T), math.ceil(H2 / T)
    total = 0
    for tr in range(rows):
        h = min(T, H2 - tr * T)
        strip = Image.new("RGB", (TW, h))
        for r in range(h):
            y = YMAX - (tr * T + r + 0.5) / H2 * (YMAX - YMIN)
            lat = math.degrees(2 * math.atan(math.exp(math.radians(y))) - math.pi / 2)
            s = min(sh - 1, max(0, int((90 - lat) / 180 * sh)))
            strip.paste(src.crop((0, s, sw, s + 1)).resize((TW, 1)), (0, r))
        for tc in range(cols):
            f = os.path.join(tdir, f"{tc}_{tr}.jpg")
            strip.crop((tc * T, 0, min(TW, tc * T + T), h)).save(f, quality=80, optimize=True)
            total += os.path.getsize(f)
    write("sat/tiles.json", {"source": "NASA Blue Marble Next Generation (public domain)", "width": TW, "height": H2, "tile": T, "cols": cols, "rows": rows, "yMax": YMAX, "yMin": YMIN})
    sizes["sat/ tiles"] = total
    print(f"sat tiles {cols}x{rows} of {T}px, {total / 1e6:.1f} MB")
print("sizes:", {k: f"{v / 1e6:.2f} MB" for k, v in sizes.items()}, f"total {sum(sizes.values()) / 1e6:.1f} MB")
