"""Generates data/core/parts.json: low-res visual parts (like RTW .eqs shapes) for the designer.
Each part is a few simple shapes in metres (top-down, bow/front = +y) plus game stats. A part can
reference a component (it then brings that component's stats too). All values are game tuning.
Run: python tools/gen_parts.py"""
import json, os
root = os.path.join(os.path.dirname(__file__), "..", "data", "core")

def R(x, y, w, h, f="steel"): return {"t": "rect", "x": x, "y": y, "w": w, "h": h, "fill": f}
def C(x, y, r, f="steel"): return {"t": "circle", "x": x, "y": y, "r": r, "fill": f}
def P(pts, f="steel"): return {"t": "poly", "p": [c for xy in pts for c in xy], "fill": f}
def L(pts, f="dark"): return {"t": "line", "p": [c for xy in pts for c in xy], "fill": f}

PARTS = []
def part(i, name, cat, fits, shapes, uses=None, adds=None, cost=1.0, topside=False, comp=None, offers=None, arc=0, options=None, scaling=None, height=-1):
    PARTS.append({"id": i, "name": name, "category": cat, "fitsKinds": fits, "shapes": shapes, "uses": uses or {}, "adds": adds or {},
                  "cost": cost, "topside": topside, "component": comp, "offers": offers or [], "arc": arc,
                  "options": options or [], "scaling": scaling, "height": height})

S, G, A = ["ship"], ["ground"], ["aircraft"]

# ---- guns (arc = firing arc in degrees either side of the bow direction the part points)
def turret(r, barrel, bw=0.35, twin=False, stealth=False, f="steel"):
    body = P([(-r, -r * 0.8), (r, -r * 0.8), (r * 0.8, r * 0.7), (0, r * 1.05), (-r * 0.8, r * 0.7)], f) if stealth else C(0, 0, r, f)
    bars = [R(-bw * 1.4, r + barrel / 2, bw, barrel, "dark"), R(bw * 1.4, r + barrel / 2, bw, barrel, "dark")] if twin else [R(0, r + barrel / 2, bw, barrel, "dark")]
    return [body] + bars
part("gun-57", "57 mm gun mount", "Guns", S, turret(1.6, 3.2, stealth=True), {"mass": 14, "power": 0.3}, {"anti-ship": 1, "air-defence": 1}, 6, arc=150)
part("gun-76", "76 mm gun mount", "Guns", S, turret(1.9, 4.0, stealth=True), {"mass": 18, "power": 0.3}, {"anti-ship": 1, "air-defence": 1}, 8, arc=150)
part("gun-twin-76", "Twin 76 mm mount", "Guns", S, turret(2.2, 4.0, twin=True), {"mass": 30, "power": 0.5}, {"anti-ship": 2, "air-defence": 1}, 12, arc=150)
part("gun-100", "100 mm gun mount", "Guns", S, turret(2.3, 4.8, stealth=True), {"mass": 24, "power": 0.4}, {"anti-ship": 1, "strike": 1}, 10, arc=150)
part("gun-127", "127 mm gun mount", "Guns", S, turret(2.6, 6.0, stealth=True), {"mass": 40, "power": 0.5}, {"anti-ship": 2, "strike": 1}, 14, arc=150)
part("gun-130", "130 mm twin mount", "Guns", S, turret(2.9, 6.0, twin=True), {"mass": 60, "power": 0.6}, {"anti-ship": 2, "strike": 2}, 18, arc=150)
part("gun-155", "155 mm advanced gun", "Guns", S, turret(3.2, 8.0, stealth=True), {"mass": 90, "power": 1.0}, {"strike": 3}, 30, arc=150)
part("gun-30rws", "30 mm remote weapon station", "Guns", S + G, turret(0.7, 1.8), {"mass": 2, "power": 0.1}, {"air-defence": 1}, 1.5, arc=160)

# ---- point defence
part("ciws-gatling", "Gatling CIWS", "Point defence", S, [R(0, -0.4, 2.2, 2.2, "light"), C(0, 0.6, 0.9, "light"), R(0, 2.0, 0.4, 2.0, "dark")], {"mass": 6, "power": 1, "cooling": 0.3}, {"air-defence": 1, "survivability": 1}, 8, True, arc=170)
part("ciws-heavy", "Heavy gatling CIWS", "Point defence", S, [R(0, 0, 3.0, 3.0, "light"), R(0, 2.6, 0.5, 2.4, "dark")], {"mass": 10, "power": 1.5, "cooling": 0.5}, {"air-defence": 1, "survivability": 1}, 12, True, arc=170)
part("ram-21", "RAM launcher (21 cells)", "Point defence", S, [C(0, 0, 1.4), R(0, 0.6, 2.2, 2.0, "dark"), L([(-1.1, -0.2), (1.1, -0.2)], "light")], {"mass": 8, "power": 1, "cooling": 0.3}, {"air-defence": 2, "survivability": 1}, 14, True, arc=170)
part("searam", "SeaRAM (11 cells, own radar)", "Point defence", S, [R(0, 0, 2.4, 2.4, "light"), R(0, 1.1, 1.8, 1.0, "dark"), C(0, -0.6, 0.5, "glass")], {"mass": 9, "power": 1.5, "cooling": 0.5}, {"air-defence": 2, "survivability": 1}, 16, True, arc=170)
part("laser-mount", "Laser weapon mount", "Point defence", S, [R(0, 0, 2.6, 2.6, "light"), C(0, 0.4, 0.8, "glass")], {"mass": 12, "power": 6, "cooling": 5}, {"air-defence": 2}, 30, True, arc=170)
part("pd-30-twin", "Twin 30 mm AA mount", "Point defence", S, turret(1.3, 2.4, twin=True, f="light"), {"mass": 7, "power": 0.4}, {"air-defence": 1}, 5, arc=160)

# ---- vertical launch (8 cells per module; drawn as the cell grid)
def cells(n, cw=0.75, rows=2):
    cols = n // rows
    shapes = [R(0, 0, cols * cw + 0.4, rows * cw + 0.4, "deck")]
    for i in range(cols):
        for j in range(rows):
            shapes.append(R(-(cols - 1) * cw / 2 + i * cw, -(rows - 1) * cw / 2 + j * cw, cw * 0.8, cw * 0.8, "dark"))
    return shapes
for n in (8, 16, 32, 48, 64):
    part(f"vls-{n}", f"VLS, {n} cells", "Launchers", S, cells(n, rows=2 if n <= 16 else 4), {"mass": 3.8 * n, "volume": max(1, n // 16)},
         {"air-defence": round(n / 16, 1), "strike": round(n / 16, 1)}, 1.5 * n, offers=["launch-cell"])
part("vls-strike-12", "Strike-length VLS, 12 cells", "Launchers", S, cells(12, cw=0.9, rows=3), {"mass": 60, "volume": 1}, {"strike": 2}, 20, offers=["launch-cell"])
part("vls-sub-12", "Submarine VLS, 12 tubes", "Launchers", S, [R(0, 0, 3.4, 5.0, "dark")] + [C(-0.8 + (i % 2) * 1.6, -2 + (i // 2) * 0.8, 0.3, "light") for i in range(12)], {"mass": 70, "volume": 2}, {"strike": 2}, 24, offers=["launch-cell"])

# ---- canisters and torpedo tubes
def canisters(n, ang=0.0):
    return [R(0, 0, 1.0 * n + 0.4, 6.4, "deck")] + [P([(-n / 2 + i + 0.1, -3), (-n / 2 + i + 0.9, -3), (-n / 2 + i + 0.9 + ang, 3), (-n / 2 + i + 0.1 + ang, 3)], "light") for i in range(n)]
part("can-2", "Anti-ship canisters (2)", "Launchers", S + G, canisters(2), {"mass": 5, "volume": 1}, {"anti-ship": 1}, 4, comp=None, offers=["launch-cell"], arc=180)
part("can-4", "Anti-ship canisters (4)", "Launchers", S, canisters(4), {"mass": 9, "volume": 1}, {"anti-ship": 2}, 7, offers=["launch-cell"], arc=180)
part("can-8-angled", "Angled canister racks (2×4)", "Launchers", S, canisters(4, 0.6) + [R(0, -4, 4.4, 0.4, "dark")], {"mass": 18, "volume": 2}, {"anti-ship": 3}, 14, offers=["launch-cell"], arc=180)
part("can-box-4", "Box launcher (4 cells)", "Launchers", S + G, [R(0, 0, 2.6, 5.0, "light"), L([(-1.3, 1.4), (1.3, 1.4)])], {"mass": 8, "volume": 1}, {"anti-ship": 1, "strike": 1}, 6, offers=["launch-cell"], arc=180)
part("tt-triple", "Triple torpedo tubes", "Launchers", S, [C(0, 0, 0.8), R(-0.5, 2, 0.4, 4), R(0, 2, 0.4, 4), R(0.5, 2, 0.4, 4)], {"mass": 4}, {"anti-sub": 1}, 3, arc=90)
part("tt-quad", "Quad torpedo tubes", "Launchers", S, [C(0, 0, 0.9), R(-0.75, 2, 0.4, 4), R(-0.25, 2, 0.4, 4), R(0.25, 2, 0.4, 4), R(0.75, 2, 0.4, 4)], {"mass": 6}, {"anti-sub": 1, "anti-ship": 1}, 5, arc=90)
part("asroc-box", "ASW rocket launcher (8)", "Launchers", S, [R(0, 0, 2.6, 3.2, "light")] + [R(-0.9 + (i % 4) * 0.6, -0.8 + (i // 4) * 1.6, 0.45, 1.3, "dark") for i in range(8)], {"mass": 20}, {"anti-sub": 2}, 12, arc=160)

# ---- radars and sensors (many bring the full component)
part("rad-array-4face", "Active array radar block (4 faces)", "Sensors", S, [R(0, 0, 9, 9, "light"), R(0, 4.6, 5, 0.5, "glass"), R(0, -4.6, 5, 0.5, "glass"), R(4.6, 0, 0.5, 5, "glass"), R(-4.6, 0, 0.5, 5, "glass")], {"mass": 10}, {}, 20, True, "sn-radar-aesa")
part("rad-array-pesa", "Phased array faces (4)", "Sensors", S, [R(0, 0, 8.5, 8.5, "light"), R(0, 4.3, 4.5, 0.5, "glass"), R(0, -4.3, 4.5, 0.5, "glass"), R(4.3, 0, 0.5, 4.5, "glass"), R(-4.3, 0, 0.5, 4.5, "glass")], {"mass": 8}, {}, 15, True, "sn-radar-pesa")
part("rad-rot-large", "Rotating air-search radar", "Sensors", S + G, [C(0, 0, 0.6, "dark"), R(0, 0, 7.5, 1.0, "light")], {"mass": 4}, {}, 6, True, "sn-radar-search")
part("rad-rot-small", "Rotating surface radar", "Sensors", S + G, [C(0, 0, 0.4, "dark"), R(0, 0, 4.0, 0.6, "light")], {"mass": 1, "power": 1}, {"detection": 3}, 2, True)
part("rad-bar", "Long-range bar radar", "Sensors", S, [C(0, 0, 0.8, "dark"), R(0, 0, 8.5, 1.6, "light"), L([(-4, 0), (4, 0)], "glass")], {"mass": 8, "power": 8, "cooling": 3}, {"detection": 7}, 25, True)
part("rad-dome", "Radome", "Sensors", S + G, [C(0, 0, 1.6, "light")], {"mass": 2, "power": 2}, {"detection": 4}, 4, True)
part("rad-fc", "Fire-control illuminator", "Sensors", S, [C(0, 0, 1.1, "light"), R(0, 0.9, 1.6, 0.6, "glass")], {"mass": 2, "power": 2, "cooling": 1}, {"fire-channels": 1}, 5, True, offers=["fire-control"], arc=150)
part("rad-nav", "Navigation radar", "Sensors", S, [R(0, 0, 2.2, 0.35, "light")], {"mass": 0.3, "power": 0.2}, {"detection": 1}, 0.5, True)
part("eo-sight", "Electro-optical sight", "Sensors", S + G, [C(0, 0, 0.6, "dark"), C(0, 0.3, 0.25, "glass")], {"mass": 0.3, "power": 0.2}, {"detection": 1}, 1, True)
part("sonar-bow", "Bow sonar dome", "Sensors", S, [P([(0, 3.5), (1.4, 0.5), (1.1, -2), (-1.1, -2), (-1.4, 0.5)], "glass")], {"mass": 20}, {}, 10, False, "sn-sonar-hull")
part("sonar-towed", "Towed array reel", "Sensors", S, [R(0, 0, 2.4, 2.0, "light"), C(0, 0, 0.7, "dark"), L([(0, -1), (0, -4)], "glass")], {"mass": 5}, {}, 8, False, "sn-sonar-towed")
part("sonar-vds", "Variable-depth sonar", "Sensors", S, [R(0, 0, 3, 3, "light"), P([(-1, -1.5), (1, -1.5), (0.6, -3), (-0.6, -3)], "glass")], {"mass": 25, "power": 1}, {"anti-sub": 2}, 12)

# ---- masts, funnels, superstructure
part("mast-lattice", "Lattice mast", "Masts", S, [R(0, 0, 2.2, 2.2, "dark"), L([(-1.1, -1.1), (1.1, 1.1)], "light"), L([(-1.1, 1.1), (1.1, -1.1)], "light")], {"mass": 6}, {"detection": 1}, 2, True)
part("mast-pole", "Pole mast", "Masts", S, [C(0, 0, 0.35, "dark"), R(0, 0, 3.0, 0.2, "light")], {"mass": 1}, {}, 0.5, True)
part("mast-enclosed", "Enclosed stealth mast", "Masts", S, [P([(-2, -2), (2, -2), (1.2, 2), (-1.2, 2)], "light")], {"mass": 10}, {"detection": 1, "signature": 1}, 6, True)
part("mast-integrated", "Integrated sensor mast", "Masts", S, [R(0, 0, 5, 5, "light"), R(0, 0, 3.2, 3.2, "glass")], {"mass": 25, "power": 4, "cooling": 2}, {"detection": 5, "signature": 1}, 30, True)
part("funnel-single", "Funnel", "Funnels", S, [P([(-1.6, -2.8), (1.6, -2.8), (1.3, 2.8), (-1.3, 2.8)], "steel"), R(0, 0, 1.8, 3.8, "black")], {"mass": 6}, {}, 1, True)
part("funnel-twin", "Twin uptakes", "Funnels", S, [R(-1.7, 0, 2.2, 4.4, "steel"), R(1.7, 0, 2.2, 4.4, "steel"), R(-1.7, 0, 1.2, 3.2, "black"), R(1.7, 0, 1.2, 3.2, "black")], {"mass": 9}, {}, 1.5, True)
part("funnel-stealth", "Stealth funnel", "Funnels", S, [P([(-2.4, -3), (2.4, -3), (1.6, 3), (-1.6, 3)], "light"), R(0, -0.4, 2.0, 2.4, "black")], {"mass": 8}, {"signature": 1}, 3, True)
part("funnel-small", "Exhaust uptake", "Funnels", S + G, [R(0, 0, 1.2, 1.6, "steel"), R(0, 0, 0.7, 1.0, "black")], {"mass": 1}, {}, 0.3, True)
def block(w, h, f="light", chamfer=0.0):
    c = chamfer
    return [P([(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2 - c), (w / 2 - c, h / 2), (-w / 2 + c, h / 2), (-w / 2, h / 2 - c)], f)]
part("bridge-s", "Bridge (small)", "Superstructure", S, block(7, 5, chamfer=1.5) + [R(0, 1.9, 5, 0.5, "glass")], {"mass": 20, "crew": 0}, {}, 3, True)
part("bridge-m", "Bridge (medium)", "Superstructure", S, block(11, 7, chamfer=2) + [R(0, 2.8, 8, 0.6, "glass")], {"mass": 40}, {}, 6, True)
part("bridge-l", "Bridge (large)", "Superstructure", S, block(15, 9, chamfer=2.5) + [R(0, 3.6, 11, 0.7, "glass")], {"mass": 70}, {}, 10, True)
for n, (w, h) in {"xs": (4, 4), "s": (6, 6), "m": (9, 10), "l": (12, 16), "long": (10, 28)}.items():
    part(f"house-{n}", f"Deckhouse ({n})", "Superstructure", S, block(w, h), {"mass": round(w * h * 0.35)}, {}, round(w * h * 0.05, 1), True)
part("cic-block", "Combat information centre", "Superstructure", S, block(9, 8, "light", 1) + [R(0, 0, 3, 3, "dark")], {"mass": 25}, {}, 12, True, "cp-cms-integrated")
part("stealth-block", "Stealth superstructure block", "Superstructure", S, [P([(-6, -8), (6, -8), (5, 6), (0, 9), (-5, 6)], "light")], {"mass": 90}, {"signature": 2}, 20, True)
part("sub-sail", "Submarine sail", "Superstructure", S, [P([(0, 4), (1.4, 1.5), (1.2, -4), (-1.2, -4), (-1.4, 1.5)], "dark"), R(0, 1.5, 7, 0.5, "dark")], {"mass": 30}, {}, 6, False)

# ---- aviation
part("helo-pad-s", "Helicopter pad (small)", "Aviation", S, [R(0, 0, 13, 14, "deck"), C(0, 0, 4.5, "dark"), C(0, 0, 4.0, "deck"), L([(-1.5, -2), (-1.5, 2)], "light"), L([(1.5, -2), (1.5, 2)], "light"), L([(-1.5, 0), (1.5, 0)], "light")], {"mass": 20}, {"anti-sub": 1}, 3)
part("helo-pad-l", "Flight deck (2 spots)", "Aviation", S, [R(0, 0, 17, 26, "deck"), C(0, 6, 4.5, "dark"), C(0, 6, 4.0, "deck"), C(0, -6, 4.5, "dark"), C(0, -6, 4.0, "deck")], {"mass": 40}, {"anti-sub": 1}, 6)
part("hangar-1", "Hangar (1 helicopter)", "Aviation", S, block(9, 12, "light") + [R(0, -5.8, 7, 0.4, "dark")], {"mass": 30}, {}, 8, True, "sy-hangar")
part("hangar-2", "Hangar (2 helicopters)", "Aviation", S, block(15, 13, "light") + [R(-3.6, -6.3, 6, 0.4, "dark"), R(3.6, -6.3, 6, 0.4, "dark")], {"mass": 60}, {"anti-sub": 1}, 15, True, "sy-hangar")
part("flightdeck-40", "Flight deck section (40 m)", "Aviation", S, [R(0, 0, 30, 40, "deck"), L([(0, -20), (0, 20)], "light")], {"mass": 400}, {}, 40)
part("angled-deck", "Angled deck extension", "Aviation", S, [P([(-12, -30), (6, -30), (22, 30), (4, 30)], "deck"), L([(-3, -30), (13, 30)], "light")], {"mass": 700}, {"strike": 1}, 80)
part("elevator", "Aircraft elevator", "Aviation", S, [R(0, 0, 16, 18, "deck"), L([(-8, -9), (8, -9), (8, 9), (-8, 9), (-8, -9)], "light")], {"mass": 120}, {}, 25)
part("catapult", "Catapult", "Aviation", S, [R(0, 0, 1.2, 90, "dark"), R(0, 44, 3, 2, "light")], {"mass": 300, "power": 5}, {"strike": 2}, 120)
part("ski-jump", "Ski-jump ramp", "Aviation", S, [P([(-12, -8), (12, -8), (10, 8), (-10, 8)], "deck"), L([(-10, 0), (10, 0)], "light")], {"mass": 200}, {"strike": 1}, 30)
part("arrest-gear", "Arresting wires", "Aviation", S, [L([(-12, -3), (12, -3)], "light"), L([(-12, -1), (12, -1)], "light"), L([(-12, 1), (12, 1)], "light"), L([(-12, 3), (12, 3)], "light")], {"mass": 60}, {}, 20)
part("island-s", "Carrier island (small)", "Aviation", S, block(10, 30, "light", 2) + [R(0, 8, 6, 6, "glass")], {"mass": 400}, {"detection": 2}, 60, True)
part("island-l", "Carrier island (large)", "Aviation", S, block(14, 45, "light", 3) + [R(0, 12, 9, 9, "glass"), C(0, -8, 3, "dark")], {"mass": 700}, {"detection": 3}, 100, True)

# ---- boats, EW, deck gear
part("rhib", "Rigid inflatable boat", "Boats", S, [P([(0, 3.5), (1, 2), (1, -3), (-1, -3), (-1, 2)], "dark")], {"mass": 2}, {}, 0.5, True)
part("launch-boat", "Ship's launch", "Boats", S, [P([(0, 5), (1.4, 3), (1.4, -4.5), (-1.4, -4.5), (-1.4, 3)], "light")], {"mass": 5}, {}, 1, True)
part("davit", "Boat davit", "Boats", S, [R(0, 0, 0.5, 5, "dark"), R(0, 2.5, 2.5, 0.4, "dark"), R(0, -2.5, 2.5, 0.4, "dark")], {"mass": 2}, {}, 0.5, True)
part("liferafts", "Life raft rack", "Boats", S, [R(0, 0, 1.2, 4, "deck")] + [C(0, -1.5 + i, 0.4, "red") for i in range(4)], {"mass": 1}, {}, 0.3)
part("satcom", "Satcom dome", "Electronics", S + G, [C(0, 0, 1.2, "light")], {"mass": 1, "power": 1}, {}, 4, True, "cm-satcom")
part("esm-array", "ESM array", "Electronics", S, [R(0, 0, 2.5, 2.5, "light"), C(0, 0, 0.8, "glass")], {"mass": 2, "power": 2}, {"ew": 1}, 8, True)
part("decoy-launcher", "Decoy launcher", "Electronics", S, [R(0, 0, 1.6, 1.6, "dark")] + [C(-0.4 + (i % 2) * 0.8, -0.4 + (i // 2) * 0.8, 0.25, "light") for i in range(4)], {"mass": 1}, {"ew": 1, "survivability": 1}, 3)
part("jammer", "Jammer antennas", "Electronics", S, [R(0, 0, 3, 1.2, "light"), R(-1, 0, 0.6, 0.8, "glass"), R(1, 0, 0.6, 0.8, "glass")], {"mass": 3, "power": 3, "cooling": 1}, {}, 12, True, "ew-jammer")
part("datalink-ant", "Datalink antenna", "Electronics", S + G, [C(0, 0, 0.5, "light"), L([(0, 0), (0, 1.2)], "light")], {"mass": 0.5, "power": 0.5}, {}, 3, True, "cm-datalink")
part("whips", "Whip antennas", "Electronics", S + G, [C(-0.8, 0, 0.15, "dark"), C(0, 0, 0.15, "dark"), C(0.8, 0, 0.15, "dark")], {"mass": 0.2}, {"networking": 1}, 0.3)
part("anchors", "Anchors and cable deck", "Deck gear", S, [C(-1.6, 0, 0.7, "dark"), C(1.6, 0, 0.7, "dark"), L([(-1.6, 0), (-1.6, 3)], "dark"), L([(1.6, 0), (1.6, 3)], "dark")], {"mass": 8}, {}, 0.5)
part("capstan", "Capstan", "Deck gear", S, [C(0, 0, 0.6, "dark")], {"mass": 1}, {}, 0.2)
part("bollards", "Bollards", "Deck gear", S, [C(-0.4, 0, 0.25, "dark"), C(0.4, 0, 0.25, "dark")], {"mass": 0.3}, {}, 0.1)
part("crane", "Deck crane", "Deck gear", S, [C(0, 0, 0.9, "light"), R(0, 3, 0.5, 6, "dark")], {"mass": 8}, {}, 2, True)
part("crane-large", "Heavy cargo crane", "Deck gear", S, [C(0, 0, 1.6, "light"), R(0, 7, 0.8, 14, "dark")], {"mass": 30}, {}, 6, True)
part("ras-station", "Replenishment station", "Deck gear", S, [R(0, 0, 2, 5, "light"), L([(1, -2), (3, 0), (1, 2)], "dark")], {"mass": 10}, {"endurance": 1}, 3, True)
part("mine-rails", "Mine rails", "Deck gear", S, [L([(-0.8, -8), (-0.8, 8)], "dark"), L([(0.8, -8), (0.8, 8)], "dark")], {"mass": 4}, {}, 1)
part("sub-planes", "Stern planes", "Deck gear", S, [P([(-5, 0), (5, 0), (4, -1.5), (-4, -1.5)], "dark")], {"mass": 5}, {}, 1)

# ---- ground systems (top-down vehicle bits; front = +y)
part("g-cab", "Truck cab", "Vehicle", G, [R(0, 0, 2.5, 2.4, "steel"), R(0, 0.9, 2.1, 0.5, "glass")], {"mass": 2, "crew": 0}, {}, 1)
part("g-cab-armoured", "Armoured cab", "Vehicle", G, [P([(-1.3, -1.2), (1.3, -1.2), (1.2, 1.2), (-1.2, 1.2)], "dark"), R(0, 0.9, 1.8, 0.4, "glass")], {"mass": 3}, {"survivability": 1}, 2)
part("g-bed", "Flatbed", "Vehicle", G, [R(0, 0, 2.5, 6, "deck")], {"mass": 1}, {}, 0.5)
part("g-outriggers", "Stabiliser outriggers", "Vehicle", G, [R(-1.6, 1.5, 0.8, 0.4, "dark"), R(1.6, 1.5, 0.8, 0.4, "dark"), R(-1.6, -1.5, 0.8, 0.4, "dark"), R(1.6, -1.5, 0.8, 0.4, "dark")], {"mass": 0.5}, {}, 0.3)
part("g-generator", "Generator set", "Vehicle", G, [R(0, 0, 2.2, 1.6, "light"), R(0, 0, 1.4, 0.4, "dark")], {"mass": 2}, {}, 1, False, "pw-gen-diesel")
part("g-shelter", "C2 shelter", "Vehicle", G, [R(0, 0, 2.4, 4.2, "light"), R(0, -1.9, 1.0, 0.3, "dark")], {"mass": 3}, {"networking": 1}, 3, False, "cp-cms-basic")
part("g-mast", "Telescopic antenna mast", "Vehicle", G, [C(0, 0, 0.4, "dark"), C(0, 0, 0.2, "light")], {"mass": 0.5}, {"networking": 1}, 1, True)
part("g-launcher-2", "Launcher rack (2 canisters)", "Vehicle", G, [R(-0.55, 0, 1.0, 6.5, "light"), R(0.55, 0, 1.0, 6.5, "light")], {"mass": 4}, {"air-defence": 1}, 3, offers=["launch-cell"], arc=180)
part("g-launcher-4", "Launcher rack (4 canisters)", "Vehicle", G, [R(-1.1 + i * 0.73, 0, 0.66, 6.5, "light") for i in range(4)], {"mass": 8}, {"air-defence": 2}, 5, offers=["launch-cell"], arc=180)
part("g-launcher-8", "Launcher pack (8 tubes)", "Vehicle", G, [R(0, 0, 2.4, 3.2, "light")] + [C(-0.9 + (i % 4) * 0.6, -0.5 + (i // 4) * 1.0, 0.25, "dark") for i in range(8)], {"mass": 10}, {"air-defence": 2, "strike": 1}, 8, offers=["launch-cell"], arc=180)
part("g-canister-bm", "Ballistic missile canister", "Vehicle", G, [R(0, 0, 1.6, 12, "light"), R(0, 5.6, 1.6, 0.8, "dark")], {"mass": 12}, {"strike": 2}, 10, offers=["launch-cell"])
part("g-radar-panel", "Radar panel (folding)", "Vehicle", G, [R(0, 0, 3.4, 1.0, "light"), R(0, 0.3, 3.0, 0.3, "glass")], {"mass": 3}, {}, 10, True, "sn-radar-pesa")
part("g-radar-rot", "Rotating radar head", "Vehicle", G, [C(0, 0, 0.4, "dark"), R(0, 0, 4.5, 0.8, "light")], {"mass": 2, "power": 2}, {"detection": 4}, 6, True)
part("g-turret-pd", "Point-defence turret", "Vehicle", G, turret(0.9, 2.4, twin=True), {"mass": 3, "power": 0.5}, {"air-defence": 1}, 4, arc=170)
part("g-wheels-3", "Wheel set (3 axles)", "Vehicle", G, [R(-1.3, -2 + i * 2, 0.4, 1.1, "black") for i in range(3)] + [R(1.3, -2 + i * 2, 0.4, 1.1, "black") for i in range(3)], {"mass": 1}, {"mobility": 1}, 0.5)
part("g-tracks", "Track run", "Vehicle", G, [R(-1.5, 0, 0.6, 6.5, "black"), R(1.5, 0, 0.6, 6.5, "black")], {"mass": 4}, {"mobility": 1, "survivability": 1}, 2)
part("g-camo-net", "Camouflage net kit", "Vehicle", G, [R(0, 0, 3, 3, "deck"), L([(-1.5, -1.5), (1.5, 1.5)], "dark"), L([(-1.5, 1.5), (1.5, -1.5)], "dark")], {"mass": 0.3}, {"signature": 1}, 0.3)

# ---- aircraft (top-down, nose = +y; sizes in metres for a ~15-20 m fighter)
M = ["munition"]
part("ac-canopy-1", "Canopy (single seat)", "Cockpit", A, [P([(0, 1.2), (0.45, 0.5), (0.4, -0.9), (-0.4, -0.9), (-0.45, 0.5)], "glass")], {"mass": 0.1}, {}, 1)
part("ac-canopy-2", "Canopy (two seats)", "Cockpit", A, [P([(0, 1.8), (0.45, 1.0), (0.42, -1.6), (-0.42, -1.6), (-0.45, 1.0)], "glass"), L([(-0.4, 0), (0.4, 0)], "dark")], {"mass": 0.2}, {"strike": 1}, 1.5)
part("ac-canopy-flat", "Flush cockpit (large aircraft)", "Cockpit", A, [P([(-0.9, 0), (0.9, 0), (0.6, 0.7), (-0.6, 0.7)], "glass")], {"mass": 0.1}, {}, 1)
part("ac-radar-aesa", "Nose radar (active array)", "Avionics", A, [P([(0, 1.4), (0.55, 0.3), (0.55, -0.4), (-0.55, -0.4), (-0.55, 0.3)], "light"), C(0, 0.1, 0.35, "glass")], {}, {}, 2, comp="sn-radar-aesa-air")
part("ac-radar-pd", "Nose radar (pulse-Doppler)", "Avionics", A, [P([(0, 1.3), (0.5, 0.3), (0.5, -0.3), (-0.5, -0.3), (-0.5, 0.3)], "light"), C(0, 0.1, 0.3, "dark")], {}, {}, 1, comp="sn-radar-pd-air")
part("ac-irst", "IRST ball", "Avionics", A, [C(0, 0, 0.25, "glass")], {}, {}, 1, comp="sn-irst")
part("ac-targeting-pod", "Targeting pod", "Stores", A, [R(0, 0, 0.35, 2.4, "light"), C(0, 1.2, 0.18, "glass")], {"mass": 0.25, "power": 0.5}, {"strike": 1}, 2)
part("ac-ew-pod", "Jammer pod", "Stores", A, [R(0, 0, 0.35, 3.0, "light"), R(0, 1.3, 0.3, 0.3, "dark")], {}, {"signature": -1}, 3, comp="ew-jammer")
part("ac-chaff", "Chaff and flare dispensers", "Avionics", A, [R(-0.2, 0, 0.25, 0.5, "dark"), R(0.2, 0, 0.25, 0.5, "dark")], {}, {}, 0.3, comp="ew-chaff")
part("ac-engine", "Turbofan", "Engines", A, [R(0, 0, 1.0, 4.2, "steel"), C(0, -2.2, 0.5, "black")], {}, {}, 4, comp="pr-turbofan")
part("ac-engine-adv", "Advanced turbofan", "Engines", A, [R(0, 0, 1.1, 4.4, "steel"), P([(-0.55, -2.2), (0.55, -2.2), (0.4, -2.6), (-0.4, -2.6)], "black")], {}, {}, 6, comp="pr-turbofan-adv")
part("ac-engine-pod", "Podded engine (large aircraft)", "Engines", A, [R(0, 0, 1.6, 4.5, "light"), C(0, 2.2, 0.7, "black")], {}, {}, 4, comp="pr-turbofan")
part("ac-intakes-side", "Side intakes (pair)", "Airframe", A, [P([(-1.2, 1), (-0.7, 1), (-0.7, -1), (-1.1, -1)], "dark"), P([(1.2, 1), (0.7, 1), (0.7, -1), (1.1, -1)], "dark")], {"mass": 0.2}, {}, 0.5)
part("ac-intake-chin", "Chin intake", "Airframe", A, [P([(-0.6, 0.6), (0.6, 0.6), (0.5, -0.8), (-0.5, -0.8)], "dark")], {"mass": 0.1}, {}, 0.3)
part("ac-intake-dsi", "Bump (DSI) intakes", "Airframe", A, [P([(-1.1, 0.8), (-0.7, 1.1), (-0.7, -0.9), (-1.0, -0.9)], "steel"), P([(1.1, 0.8), (0.7, 1.1), (0.7, -0.9), (1.0, -0.9)], "steel")], {"mass": 0.15}, {"signature": 1}, 1)
part("ac-tail-single", "Single fin", "Airframe", A, [P([(-0.12, 1.5), (0.12, 1.5), (0.1, -1.5), (-0.1, -1.5)], "dark")], {"mass": 0.2}, {}, 0.3)
part("ac-tail-twin", "Twin fins", "Airframe", A, [P([(-1.0, 1.4), (-0.8, 1.4), (-0.8, -1.4), (-1.0, -1.4)], "dark"), P([(1.0, 1.4), (0.8, 1.4), (0.8, -1.4), (1.0, -1.4)], "dark")], {"mass": 0.35}, {"agility": 1}, 0.6)
part("ac-tail-canted", "Canted twin fins", "Airframe", A, [P([(-1.2, 1.2), (-0.8, 1.4), (-0.7, -1.3), (-1.1, -1.4)], "dark"), P([(1.2, 1.2), (0.8, 1.4), (0.7, -1.3), (1.1, -1.4)], "dark")], {"mass": 0.35}, {"signature": 1, "agility": 1}, 1)
part("ac-canards", "Canards", "Airframe", A, [P([(0.5, 0.3), (2.0, -0.4), (2.0, -0.7), (0.5, -0.5)], "steel"), P([(-0.5, 0.3), (-2.0, -0.4), (-2.0, -0.7), (-0.5, -0.5)], "steel")], {"mass": 0.2}, {"agility": 2}, 1)
part("ac-bay", "Internal weapons bay", "Stores", A, [R(0, 0, 1.2, 4.0, "dark"), L([(-0.6, 0), (0.6, 0)], "light")], {}, {"payload": 1}, 3, comp="if-bay")
part("ac-bay-side", "Side bay (1 missile)", "Stores", A, [R(0, 0, 0.4, 2.2, "dark")], {"mass": 0.1}, {"payload": 0.5}, 1)
part("ac-pylon", "Wing pylon", "Stores", A, [R(0, 0, 0.18, 1.2, "dark")], {"mass": 0.05}, {"payload": 1, "signature": -1}, 0.3)
part("ac-rail", "Wingtip rail", "Stores", A, [R(0, 0, 0.12, 2.8, "light")], {}, {"payload": 0.5}, 0.3, comp="if-rail")
part("ac-droptank", "Drop tank", "Stores", A, [P([(0, 2.2), (0.3, 1.6), (0.3, -1.6), (0, -2.2), (-0.3, -1.6), (-0.3, 1.6)], "light")], {"mass": 0.3}, {"endurance": 1, "signature": -1}, 0.4)
part("ac-conformal", "Conformal fuel tank", "Stores", A, [R(0, 0, 0.5, 5.0, "steel")], {"mass": 0.4}, {"endurance": 1}, 1)
part("ac-probe", "Refuelling probe", "Airframe", A, [R(0, 0, 0.12, 1.4, "dark")], {"mass": 0.05}, {"endurance": 1}, 0.3)
part("ac-hook", "Arrestor hook", "Airframe", A, [R(0, 0, 0.12, 1.6, "dark"), R(0, -0.8, 0.4, 0.12, "dark")], {"mass": 0.1}, {}, 0.3)
part("ac-gun", "Internal cannon", "Stores", A, [R(0, 0, 0.2, 1.6, "black")], {"mass": 0.3}, {"air-defence": 1}, 1)
part("ac-rotodome", "AEW rotodome", "Avionics", A, [C(0, 0, 3.6, "light"), R(0, 0, 7.0, 0.7, "dark")], {"mass": 1.5, "power": 10, "cooling": 4}, {"detection": 9}, 25)
part("ac-erieye", "Dorsal AEW array", "Avionics", A, [R(0, 0, 0.8, 8.0, "light"), R(0, 0, 0.4, 7.2, "glass")], {"mass": 0.9, "power": 8, "cooling": 3}, {"detection": 8}, 18)
part("ac-sensor-turret", "EO sensor turret", "Avionics", A, [C(0, 0, 0.35, "dark"), C(0, 0.12, 0.15, "glass")], {"mass": 0.1, "power": 0.5}, {"detection": 2}, 2)
part("ac-satcom", "Satcom hump", "Avionics", A, [P([(0, 0.8), (0.4, 0.3), (0.4, -0.6), (-0.4, -0.6), (-0.4, 0.3)], "light")], {}, {}, 2, comp="cm-satcom")
part("ac-datalink", "Datalink antenna", "Avionics", A, [R(0, 0, 0.3, 0.5, "light")], {}, {}, 1, comp="cm-datalink")
part("ac-mission-computer", "Mission computer bay", "Avionics", A, [R(0, 0, 0.6, 0.8, "dark"), R(0, 0, 0.3, 0.5, "glass")], {}, {}, 1, comp="cp-mission")
part("ac-fusion-computer", "Sensor fusion core", "Avionics", A, [R(0, 0, 0.7, 0.9, "dark"), C(0, 0, 0.2, "glass")], {}, {}, 3, comp="cp-fusion")
part("ac-fbw", "Fly-by-wire computers", "Avionics", A, [R(0, 0, 0.5, 0.6, "dark")], {}, {}, 1, comp="gc-fbw")
part("ac-apu", "Auxiliary power unit", "Airframe", A, [R(0, 0, 0.5, 0.8, "steel"), C(0, -0.4, 0.12, "black")], {}, {}, 0.5, comp="pw-apu")
part("ac-ecs", "Cooling pack", "Airframe", A, [R(0, 0, 0.6, 0.6, "steel"), L([(-0.3, 0), (0.3, 0)], "dark")], {}, {}, 0.5, comp="th-air-ecs")
part("ac-rotor", "Rotor disc (uncrewed)", "Engines", A, [C(0, 0, 2.2, "light"), L([(-2.2, 0), (2.2, 0)], "dark"), L([(0, -2.2), (0, 2.2)], "dark")], {"mass": 0.4}, {"endurance": 1}, 2)


# option sets, scaling and ~220 more parts live in gen_parts_more.py
exec(open(os.path.join(os.path.dirname(__file__), "gen_parts_more.py"), encoding="utf8").read())

for p in PARTS:
    p["uses"] = {k: v for k, v in p["uses"].items() if v}
json.dump(PARTS, open(os.path.join(root, "parts.json"), "w", encoding="utf8"), indent=1, ensure_ascii=False)
print(f"{len(PARTS)} parts")
