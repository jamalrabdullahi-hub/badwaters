"""Builds each theatre scenario's logistics block (data/core/scenarios.json → "logistics") and its rear depots.

Inputs (civil atlas only): data/core/geo/roads.json, roads-minor.json, rail.json, ports.json (Natural Earth) and the
ETOPO 2022 elevation grid (tools/geo-src, public domain). For every supply point (bases, batteries, launchers,
depots) it records elevation, distance to the nearest road or rail line and an efficiency factor; it then builds
named corridors (Highway X01, Rail Q01, Sea B-C, Air bridge A1) and places 1-2 FICTIONAL rear depots per side at
road/rail junctions well behind the front. No real military, fuel or energy sites are read or placed.
All numbers are game tuning.
"""
import json, math, os
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
HERE = os.path.dirname(os.path.abspath(__file__))
CORE = os.path.join(HERE, "..", "data", "core")
GEO = os.path.join(CORE, "geo")
ETOPO = os.path.join(HERE, "geo-src", "ETOPO_2022_v1_60s_N90W180_surface.tif")
EFF = json.load(open(os.path.join(CORE, "theatre", "logistics.json"), encoding="utf-8-sig"))["efficiency"]


def km(a, b):
    la1, lo1, la2, lo2 = map(math.radians, (a[1], a[0], b[1], b[0]))
    h = math.sin((la2 - la1) / 2) ** 2 + math.cos(la1) * math.cos(la2) * math.sin((lo2 - lo1) / 2) ** 2
    return 6371 * 2 * math.asin(math.sqrt(h))


def lines(name, box):
    out = []
    for ln in json.load(open(os.path.join(GEO, name), encoding="utf-8-sig"))["lines"]:
        pts = [(ln[i], ln[i + 1]) for i in range(0, len(ln) - 1, 2)]
        if any(box[0] <= x <= box[2] and box[1] <= y <= box[3] for x, y in pts): out.append(pts)
    return out


def seg_dist(p, a, b):
    # planar approximation in degrees scaled by latitude, fine for route snapping at theatre scale
    kx = math.cos(math.radians(p[1]))
    ax, ay, bx, by, px, py = a[0] * kx, a[1], b[0] * kx, b[1], p[0] * kx, p[1]
    dx, dy = bx - ax, by - ay
    t = 0 if dx == dy == 0 else max(0, min(1, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy)) * 111


def nearest(p, polylines):
    best = 1e9
    for pl in polylines:
        for a, b in zip(pl, pl[1:]): best = min(best, seg_dist(p, a, b))
    return best


class Elev:
    def __init__(self):
        self.ok = os.path.exists(ETOPO)
        if self.ok:
            img = Image.open(ETOPO)
            self.a = np.asarray(img, dtype=np.float32)
            self.h, self.w = self.a.shape

    def at(self, lon, lat):
        if not self.ok: return 0.0
        r = int((90 - lat) / 180 * self.h); c = int((lon + 180) / 360 * self.w)
        return float(self.a[min(self.h - 1, max(0, r)), min(self.w - 1, max(0, c))])


def factor(off_km, elev):
    f = 1 - EFF["offRoutePer50Km"] * (off_km / 50) - EFF["perKmAbove"] * max(0, (elev - EFF["altStartM"]) / 1000)
    return round(max(EFF["min"], min(1, f)), 3)


def build(sc, elev):
    pts = [(s["id"], s["name"], s["side"], s["lon"], s["lat"], s.get("type", ""), s["class"]) for s in sc["sites"] if s["class"] in ("military-base", "military", "logistics")]
    pts += [(b["id"], b["name"], b["side"], b["lon"], b["lat"], "battery", "") for b in sc["batteries"] if b.get("platform", "ground") == "ground"]
    pts += [(l["id"], l["name"], l["side"], l["lon"], l["lat"], "launcher", "") for l in sc["launchers"] if l.get("platform", "ground") == "ground"]
    lons = [p[3] for p in pts]; lats = [p[4] for p in pts]
    box = (min(lons) - 6, min(lats) - 6, max(lons) + 6, max(lats) + 6)
    roads = lines("roads.json", box); minor = lines("roads-minor.json", box); rail = lines("rail.json", box)
    ports = [(p["lon"], p["lat"]) for p in json.load(open(os.path.join(GEO, "ports.json"), encoding="utf-8-sig"))["points"] if box[0] <= p["lon"] <= box[2] and box[1] <= p["lat"] <= box[3]]

    # fictional rear depots: next to the side's own base furthest from the enemy (inside its own territory), 1-2 per side
    sites = sc["sites"] = [s for s in sc["sites"] if s.get("type") != "depot"]
    for side in ("blue", "red"):
        own = [p for p in pts if p[2] == side and p[5] in ("airbase", "navalbase", "garrison")]
        foe = [(p[3], p[4]) for p in pts if p[2] != side and p[5] in ("airbase", "navalbase", "garrison")]
        if not own: continue
        own.sort(key=lambda p: -(min(km((p[3], p[4]), f) for f in foe) if foe else 0))
        chosen = []
        for p in own:
            on_land = any(elev.at(p[3] + dx, p[4] + dy) > 5 for dx in (-0.2, -0.12, 0, 0.12, 0.2) for dy in (-0.15, -0.08, 0, 0.08, 0.15))
            if on_land and all(km((p[3], p[4]), (c[3], c[4])) > 150 for c in chosen): chosen.append(p)
            if len(chosen) == 2: break
        for i, p in enumerate(chosen):
            did = f"{side}-depot-{i + 1}"
            offs = [(dx, dy) for dx in (0.12, -0.12, 0.2, -0.2, 0) for dy in (-0.08, 0.08, 0.15, -0.15, 0)]
            dx, dy = next((o for o in offs if elev.at(p[3] + o[0], p[4] + o[1]) > 5), (0.05, 0.05)) # on land
            lon, lat = round(p[3] + dx, 3), round(p[4] + dy, 3)
            sites.append({"id": did, "name": f"{'Blue' if side == 'blue' else 'Red'} Theatre Depot {i + 1}", "side": side, "class": "logistics", "lon": lon, "lat": lat, "type": "depot"})
            pts.append((did, f"Depot {i + 1}", side, lon, lat, "depot", "logistics"))

    nodes = {}
    for pid, name, side, lon, lat, typ, cls in pts:
        e = elev.at(lon, lat)
        port = min((km((lon, lat), q) for q in ports), default=1e9)
        # access to the route network: a road, a rail line, or a port (islands and coastal sites are supplied by sea)
        off = min(nearest((lon, lat), roads), nearest((lon, lat), minor) + 10, nearest((lon, lat), rail), port + 5 if typ in ("navalbase", "garrison", "airbase", "depot") else 1e9)
        nodes[pid] = {"elev": round(e), "offRouteKm": round(off, 1), "factor": factor(off, e)}

    corridors = []; n = {"road": 0, "rail": 0, "sea": 0, "air": 0}
    code = {"road": "X", "rail": "Q", "sea": "S", "air": "A"}
    def add(mode, side, a, b, length, name=None):
        n[mode] += 1
        cid = f"{side[0]}-{mode}-{n[mode]:02d}"
        label = name or {"road": f"Highway {code[mode]}{n[mode]:02d}", "rail": f"Rail line {code[mode]}{n[mode]:02d}", "air": f"Air bridge {code[mode]}{n[mode]}"}[mode]
        corridors.append({"id": cid, "name": label, "mode": mode, "side": side, "a": a, "b": b, "km": round(length)})
    P = {p[0]: p for p in pts}
    for side in ("blue", "red"):
        mine = [p for p in pts if p[2] == side]
        depots = [p for p in mine if p[5] == "depot"]
        hubs = [p for p in mine if p[5] in ("airbase", "navalbase", "garrison")]
        for h in hubs:
            src = min(depots, key=lambda d: km((d[3], d[4]), (h[3], h[4]))) if depots else None
            if src is None: continue
            d = km((src[3], src[4]), (h[3], h[4]))
            # land link if both ends are near the road net and close enough, else sea (both near a port) or air
            land = nodes[h[0]]["offRouteKm"] < 120 and nodes[src[0]]["offRouteKm"] < 60 and d < 1500
            near_port = lambda p: ports and min(km((p[3], p[4]), q) for q in ports) < 60
            if land:
                add("road", side, src[0], h[0], d * 1.25)
                if nearest((h[3], h[4]), rail) < 30 and nearest((src[3], src[4]), rail) < 30: add("rail", side, src[0], h[0], d * 1.2)
            else:
                # overseas or island hub: sealift from the side's naval base (or the depot) to it
                nb = min((p for p in mine if p[5] == "navalbase" and p[0] != h[0]), key=lambda p: km((p[3], p[4]), (h[3], h[4])), default=src)
                add("sea", side, nb[0], h[0], km((nb[3], nb[4]), (h[3], h[4])) * 1.3, f"Sealift {nb[1][:16]} – {h[1][:16]}")
                if nb is not src: add("road", side, src[0], nb[0], km((src[3], src[4]), (nb[3], nb[4])) * 1.25)
            add("air", side, src[0], h[0], d)
        # last legs: batteries and launchers to their nearest hub by road (off-route distance shows in their factor)
        for p in mine:
            if p[5] not in ("battery", "launcher"): continue
            hub = min(hubs, key=lambda h: km((h[3], h[4]), (p[3], p[4])), default=None)
            if hub is None: continue
            add("road", side, hub[0], p[0], km((hub[3], hub[4]), (p[3], p[4])) * 1.3 + nodes[p[0]]["offRouteKm"], f"Supply road {hub[1][:16]} – {p[1][:16]}")
    # sea-served hubs are supplied at their own quay: no off-route penalty; their batteries/launchers measure to the hub
    sea_served = {c["b"] for c in corridors if c["mode"] == "sea"}
    for pid in sea_served:
        nodes[pid]["offRouteKm"] = min(nodes[pid]["offRouteKm"], 5.0); nodes[pid]["factor"] = factor(nodes[pid]["offRouteKm"], nodes[pid]["elev"])
    for c in corridors:
        if c["name"].startswith("Supply road") and c["a"] in sea_served:
            p = P[c["b"]]; h = P[c["a"]]
            nodes[p[0]]["offRouteKm"] = min(nodes[p[0]]["offRouteKm"], round(km((h[3], h[4]), (p[3], p[4])), 1))
            nodes[p[0]]["factor"] = factor(nodes[p[0]]["offRouteKm"], nodes[p[0]]["elev"])
    sc["logistics"] = {"nodes": nodes, "corridors": corridors}
    return len(nodes), len(corridors)


if __name__ == "__main__":
    path = os.path.join(CORE, "scenarios.json")
    S = json.load(open(path, encoding="utf-8-sig"))
    elev = Elev()
    for sc in S:
        if sc.get("theatre"):
            nn, nc = build(sc, elev)
            print(f"{sc['id']:10} {nn:4} supply points  {nc:4} corridors")
    json.dump(S, open(path, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
