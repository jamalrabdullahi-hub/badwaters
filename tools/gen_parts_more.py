# Included by gen_parts.py. Option sets (dropdown choices on placed parts), size scaling rules, and about
# 220 more low-res parts. All effects are simple game trade-offs, not engineering data.

def CH(i, name, mul=None, add=None, adds=None, cost=1.0, sx=1.0, sy=1.0, note=""):
    return {"id": i, "name": name, "usesMul": mul or {}, "usesAdd": add or {}, "adds": adds or {}, "costMul": cost, "shapeX": sx, "shapeY": sy, "note": note}
def OPT(i, name, choices, default): return {"id": i, "name": name, "choices": choices, "default": default}
def SC(label="Size", lo=0.6, hi=1.6, ratings=None, vis=1.0): return {"label": label, "min": lo, "max": hi, "ratings": ratings or {}, "visibility": vis}

# ---------------- option sets ----------------
ENGINE_OPTS = [
    OPT("type", "Engine type", [
        CH("turbojet", "Turbojet", {"mass": 0.9}, adds={"speed": 1, "endurance": -2}, cost=0.8, note="fast, thirsty"),
        CH("lbtf", "Low-bypass turbofan", note="balanced fighter engine"),
        CH("hbtf", "High-bypass turbofan", {"mass": 1.3}, adds={"speed": -2, "endurance": 2, "signature": -0.5}, sx=1.4, note="efficient, big fan"),
        CH("turboprop", "Turboprop", {"mass": 0.8}, adds={"speed": -3, "endurance": 3, "agility": -1, "field": 1}, cost=0.6, note="slow, long endurance, short fields"),
        CH("propfan", "Propfan", {"mass": 1.1}, adds={"speed": -2, "endurance": 3}, cost=0.9),
        CH("ramjet", "Ramjet", {"mass": 0.7}, adds={"speed": 3, "endurance": -3, "agility": -2, "field": -2}, cost=1.5, note="needs speed to run"),
        CH("scramjet", "Scramjet", {"mass": 0.8, "cooling": 1.5}, {"cooling": 2}, adds={"speed": 4, "endurance": -4, "agility": -3, "field": -3}, cost=3.0, note="very fast, very limited"),
        CH("electric", "Electric motor", {"mass": 1.2}, adds={"speed": -4, "endurance": -1, "signature": 1}, cost=0.7, note="quiet and cool, slow"),
    ], "lbtf"),
    OPT("spools", "Spools / fan stages", [
        CH("1", "Single spool", {"mass": 0.9}, adds={"reliability": 1, "endurance": -0.5}, cost=0.8),
        CH("2", "Two spools"),
        CH("3", "Three spools", {"mass": 1.1}, adds={"endurance": 1}, cost=1.3),
    ], "2"),
    OPT("ab", "Afterburner", [
        CH("none", "None", {"mass": 0.9}, adds={"speed": -1, "signature": 0.5}, cost=0.9),
        CH("std", "Afterburner"),
        CH("adaptive", "Adaptive cycle", {"mass": 1.1, "cooling": 1.3}, adds={"speed": 0.5, "endurance": 1}, cost=1.6),
    ], "std"),
    OPT("nozzle", "Nozzle", [
        CH("round", "Round"),
        CH("tvc", "2D thrust vectoring", {"mass": 1.1}, adds={"agility": 1, "signature": 0.5}, cost=1.3, sx=1.1),
        CH("lo", "Serrated low-signature", adds={"signature": 1, "speed": -0.5}, cost=1.2),
    ], "round"),
]
ENGINE_SCALE = SC("Engine size", 0.6, 1.6, {"speed": 0.6, "endurance": 0.3}, vis=0.5)
GEAR_OPTS = [OPT("type", "Gear type", [
    CH("normal", "Normal"),
    CH("stol", "STOL (long-stroke)", {"mass": 1.3}, adds={"field": 2}, cost=1.2, sx=1.2, note="short, rough strips"),
    CH("carrier", "Carrier (reinforced)", {"mass": 1.5}, adds={"field": 1}, cost=1.3, sx=1.2, note="arrested landings"),
    CH("rough", "Rough-field (low pressure tyres)", {"mass": 1.2}, adds={"field": 1}, cost=1.1, sx=1.3),
    CH("skid", "Skids", {"mass": 0.4}, adds={"field": -1}, cost=0.4, note="helicopters and light drones"),
    CH("float", "Floats", {"mass": 1.6}, adds={"field": 1, "speed": -1}, cost=1.2, sx=1.5, note="water operations"),
], "normal")]
GEAR_SCALE = SC("Gear ratio (strut length)", 0.8, 1.4, {"field": 1.5}, vis=0.3)
RADOME_OPTS = [OPT("shape", "Radome shape", [
    CH("ogive", "Ogive"),
    CH("hemi", "Hemispherical", adds={"detection": 0.5, "signature": -0.5, "speed": -0.5}, sy=0.8),
    CH("faceted", "Faceted low-signature", adds={"detection": -0.5, "signature": 1}, cost=1.3),
    CH("conformal", "Conformal panel", adds={"detection": -1, "signature": 1.5}, cost=1.5, sy=0.6),
], "ogive")]
ARRAY_SCALE = SC("Array size", 0.6, 1.8, {"detection": 0.5, "fire-channels": 1})
GUN_OPTS = [OPT("housing", "Housing", [
    CH("open", "Open mount", {"mass": 0.8}, adds={"signature": -0.5}, cost=0.8),
    CH("shield", "Gun shield"),
    CH("stealth", "Stealth cupola", {"mass": 1.15}, adds={"signature": 0.5}, cost=1.2),
], "shield")]
GUN_SCALE = SC("Calibre / mount size", 0.8, 1.3, {"anti-ship": 1, "strike": 1}, vis=0.5)
MATERIAL_OPTS = [OPT("material", "Material", [
    CH("steel", "Steel"),
    CH("alu", "Aluminium", {"mass": 0.7}, adds={"survivability": -0.5}, cost=1.2, note="light, burns"),
    CH("composite", "Composite", {"mass": 0.6}, adds={"signature": 0.5}, cost=1.6, note="light, radar-absorbent"),
    CH("armoured", "Armoured", {"mass": 1.6}, adds={"survivability": 1}, cost=1.4),
], "steel")]
BLOCK_SCALE = SC("Block size", 0.6, 1.8, {}, vis=1.0)
HATCH_OPTS = [OPT("hatch", "Hatches", [
    CH("std", "Standard"), CH("lo", "Low-signature", adds={"signature": 0.5}, cost=1.2),
    CH("armoured", "Armoured", {"mass": 1.2}, adds={"survivability": 0.5}, cost=1.2)], "std")]
PROP_OPTS = [OPT("pitch", "Blade pitch", [
    CH("fixed", "Fixed pitch", cost=0.8, adds={"endurance": -0.5}), CH("variable", "Variable pitch"),
    CH("scimitar", "Scimitar low-noise", adds={"signature": 0.5}, cost=1.3)], "variable")]
ROTOR_OPTS = [OPT("blades", "Blade design", [
    CH("std", "Standard"), CH("composite", "Composite, swept tips", {"mass": 0.9}, adds={"speed": 0.5, "signature": 0.5}, cost=1.3),
    CH("fold", "Folding (shipboard)", {"mass": 1.1}, adds={"field": 1}, cost=1.2)], "std")]
TAIL_OPTS = [OPT("control", "Control surfaces", [
    CH("std", "Conventional"), CH("allmoving", "All-moving", {"mass": 1.1}, adds={"agility": 0.5}, cost=1.2),
    CH("lo", "Edge-aligned", adds={"signature": 0.5}, cost=1.2)], "std")]
PYLON_OPTS = [OPT("rack", "Rack", [
    CH("single", "Single store"), CH("twin", "Twin rack", {"mass": 1.4}, adds={"payload": 0.5, "signature": -0.5}, cost=1.2, sx=1.5),
    CH("triple", "Triple ejector rack", {"mass": 1.8}, adds={"payload": 1, "signature": -1, "speed": -0.5}, cost=1.4, sx=1.8)], "single")]
SONAR_OPTS = [OPT("mode", "Mode", [
    CH("passive", "Passive", adds={"signature": 0.5}, cost=0.8), CH("active", "Active / passive"),
    CH("lf", "Low-frequency active", {"mass": 1.3, "power": 1.5}, adds={"anti-sub": 1, "signature": -0.5}, cost=1.4)], "active")]
TANK_SCALE = SC("Tank size", 0.6, 1.6, {"endurance": 1}, vis=0.6)

def attach(ids, options=None, scaling=None):
    for p in PARTS:
        if p["id"] in ids:
            if options: p["options"] = options
            if scaling: p["scaling"] = scaling

attach(["ac-engine", "ac-engine-adv", "ac-engine-pod"], ENGINE_OPTS, ENGINE_SCALE)
attach(["ac-radar-aesa", "ac-radar-pd", "rad-dome"], RADOME_OPTS, ARRAY_SCALE)
attach(["rad-array-4face", "rad-array-pesa", "rad-bar", "rad-rot-large", "g-radar-panel", "g-radar-rot"], None, ARRAY_SCALE)
attach([p["id"] for p in PARTS if p["category"] == "Guns"], GUN_OPTS, GUN_SCALE)
attach([p["id"] for p in PARTS if p["category"] == "Superstructure"], MATERIAL_OPTS, BLOCK_SCALE)
attach([p["id"] for p in PARTS if p["id"].startswith("vls-")], HATCH_OPTS, None)
attach(["ac-tail-single", "ac-tail-twin", "ac-tail-canted", "ac-canards"], TAIL_OPTS, SC("Surface size", 0.7, 1.4, {"agility": 1}, vis=0.6))
attach(["ac-pylon"], PYLON_OPTS, None)
attach(["ac-droptank", "ac-conformal"], None, TANK_SCALE)
attach(["sonar-bow", "sonar-towed", "sonar-vds"], SONAR_OPTS, SC("Array size", 0.7, 1.5, {"anti-sub": 1}, vis=0))
attach(["ac-rotor"], ROTOR_OPTS, SC("Rotor diameter", 0.7, 1.5, {"endurance": 0.5}, vis=0.5))

A, S, G = ["aircraft"], ["ship"], ["ground"]

# ================= AIRCRAFT (~95) =================
def gear(i, name, wheels, spread, strut, extra=None):
    shapes = [R(0, 0, 0.18, strut, "dark")]
    for k in range(wheels):
        x = (k - (wheels - 1) / 2) * spread
        shapes += [R(x, 0.25 if wheels > 2 and k % 2 else 0, 0.32, 0.7, "black")]
    part(i, name, "Landing gear", A, shapes + (extra or []), {"mass": 0.15 * max(1, wheels / 2)}, {"field": 0.5 * max(1, wheels / 2)}, 0.3 * wheels, options=GEAR_OPTS, scaling=GEAR_SCALE)
gear("ac-gear-nose", "Nose gear (single wheel)", 1, 0, 1.2)
gear("ac-gear-nose-twin", "Nose gear (twin wheels)", 2, 0.4, 1.2)
gear("ac-gear-main", "Main gear (single wheel)", 1, 0, 1.6)
gear("ac-gear-main-twin", "Main gear (twin wheels)", 2, 0.4, 1.6)
gear("ac-gear-bogie4", "Main gear bogie (4 wheels)", 4, 0.45, 2.0)
gear("ac-gear-bogie6", "Main gear bogie (6 wheels)", 6, 0.45, 2.2)
gear("ac-gear-tail", "Tail wheel", 1, 0, 0.6)
part("ac-gear-skids", "Skid gear", "Landing gear", A, [R(-1, 0, 0.15, 3.5, "dark"), R(1, 0, 0.15, 3.5, "dark"), R(0, 0.8, 2, 0.1, "dark"), R(0, -0.8, 2, 0.1, "dark")], {"mass": 0.05}, {}, 0.1, options=GEAR_OPTS, scaling=GEAR_SCALE)
part("ac-gear-floats", "Float gear", "Landing gear", A, [P([(-1.4, 3), (-1, 3.2), (-0.8, -2.8), (-1.3, -3)], "light"), P([(1.4, 3), (1, 3.2), (0.8, -2.8), (1.3, -3)], "light")], {"mass": 0.4}, {"field": 1, "speed": -1}, 0.8, options=GEAR_OPTS, scaling=GEAR_SCALE)
part("ac-gear-ski", "Ski gear", "Landing gear", A, [R(-1, 0, 0.3, 2.6, "light"), R(1, 0, 0.3, 2.6, "light")], {"mass": 0.1}, {"field": 1}, 0.3)
for i, n, w, h in [("ac-nozzle-round", "Round nozzle", 1.0, 0.8), ("ac-nozzle-2d", "2D vectoring nozzle", 1.2, 0.9), ("ac-nozzle-serrated", "Serrated nozzle", 1.1, 0.7), ("ac-nozzle-twin", "Twin nozzles", 2.4, 0.8)]:
    part(i, n, "Engines", A, [R(0, 0, w, h, "black"), R(0, h / 2, w, 0.1, "steel")], {"mass": 0.1}, {"agility": 0.5} if "2d" in i else {"signature": 0.5} if "serrated" in i else {}, 0.5, scaling=SC("Nozzle size", 0.7, 1.4, {}, vis=0.5))
for i, n, comp, w, h in [("ac-engine-small", "Small turbofan", "pr-turbofan", 0.8, 3.2), ("ac-engine-large", "Large turbofan", "pr-turbofan-adv", 1.3, 5.0),
                          ("ac-engine-highpod", "High-bypass pod", "pr-turbofan", 2.2, 4.5), ("ac-engine-turboprop", "Turboprop nacelle", "pr-turbofan", 0.9, 3.5),
                          ("ac-engine-liftfan", "Lift fan", None, 1.6, 1.6), ("ac-engine-apu-jet", "Cruise sustainer (small jet)", "pr-turbofan", 0.6, 2.2)]:
    shapes = [R(0, 0, w, h, "steel"), C(0, h / 2 - w / 4, w / 2.4, "dark")]
    part(i, n, "Engines", A, shapes, {"mass": 0.3} if comp is None else {}, {"field": 2} if "lift" in i else {}, 2, comp=comp, options=ENGINE_OPTS if comp else None, scaling=ENGINE_SCALE)
for blades in (2, 3, 4, 6, 8):
    sh = [C(0, 0, 0.3, "dark")]
    import math as _m
    for k in range(blades):
        a = 2 * _m.pi * k / blades
        sh.append(L([(0, 0), (1.6 * _m.cos(a), 1.6 * _m.sin(a))], "dark"))
    part(f"ac-prop-{blades}", f"Propeller ({blades} blades)", "Engines", A, sh, {"mass": 0.05 * blades}, {"endurance": 0.3 * blades / 2}, 0.2 * blades, options=PROP_OPTS, scaling=SC("Propeller diameter", 0.7, 1.5, {"endurance": 0.5}, vis=0.3))
part("ac-prop-contra", "Contra-rotating propellers", "Engines", A, [C(0, 0, 0.35, "dark"), L([(-1.7, 0), (1.7, 0)], "dark"), L([(0, -1.7), (0, 1.7)], "dark"), L([(-1.2, -1.2), (1.2, 1.2)], "steel"), L([(-1.2, 1.2), (1.2, -1.2)], "steel")], {"mass": 0.4}, {"endurance": 1, "speed": 0.5}, 2, options=PROP_OPTS)
for i, n, blades, r in [("ac-rotor-2", "Main rotor (2 blades)", 2, 5), ("ac-rotor-4", "Main rotor (4 blades)", 4, 6.5), ("ac-rotor-5", "Main rotor (5 blades)", 5, 7), ("ac-rotor-tail", "Tail rotor", 4, 1.3), ("ac-rotor-coax", "Coaxial rotors", 6, 6), ("ac-tiltrotor", "Tilt-rotor nacelle", 3, 5.5)]:
    sh = [C(0, 0, r, "light")]
    for k in range(blades):
        a = 2 * _m.pi * k / blades
        sh.append(L([(0, 0), (r * _m.cos(a), r * _m.sin(a))], "dark"))
    part(i, n, "Rotors", A, sh, {"mass": 0.1 * r}, {"field": 2} if "tail" not in i else {}, r, options=ROTOR_OPTS, scaling=SC("Rotor diameter", 0.7, 1.5, {"endurance": 0.5}, vis=0.5))
for i, n, sh, adds in [
    ("ac-tail-v", "V-tail", [P([(-0.2, 0), (-2.2, -1.6), (-2.2, -2.0), (0, -1.0)], "dark"), P([(0.2, 0), (2.2, -1.6), (2.2, -2.0), (0, -1.0)], "dark")], {"signature": 0.5}),
    ("ac-tail-t", "T-tail", [R(0, 0, 0.2, 2.6, "dark"), R(0, -1.1, 4.4, 0.6, "dark")], {"endurance": 0.5}),
    ("ac-tail-h", "H-tail (twin end-plate fins)", [R(0, -1, 5, 0.6, "dark"), R(-2.5, -1, 0.25, 1.8, "dark"), R(2.5, -1, 0.25, 1.8, "dark")], {"agility": 0.5}),
    ("ac-tail-cruciform", "Cruciform tail", [R(0, 0, 0.2, 2.4, "dark"), R(0, -0.4, 3.6, 0.5, "dark")], {}),
    ("ac-fin-ventral", "Ventral fins", [R(-0.6, 0, 0.12, 1.4, "dark"), R(0.6, 0, 0.12, 1.4, "dark")], {"agility": 0.3}),
    ("ac-twin-boom", "Twin tail booms", [R(-1.8, 0, 0.35, 6, "steel"), R(1.8, 0, 0.35, 6, "steel"), R(0, -2.8, 3.8, 0.5, "dark")], {"endurance": 0.5}),
    ("ac-tailplane", "Horizontal tailplanes", [P([(0.3, 0.4), (2.6, -0.6), (2.6, -1.0), (0.3, -0.8)], "dark"), P([(-0.3, 0.4), (-2.6, -0.6), (-2.6, -1.0), (-0.3, -0.8)], "dark")], {"agility": 0.5})]:
    part(i, n, "Airframe", A, sh, {"mass": 0.25}, adds, 0.6, options=TAIL_OPTS, scaling=SC("Surface size", 0.7, 1.4, {"agility": 1}, vis=0.6))
for i, n, sh, adds in [
    ("ac-winglets", "Winglets", [R(-0.1, 0, 0.12, 1.0, "dark"), R(0.1, 0, 0.12, 1.0, "dark")], {"endurance": 0.5}),
    ("ac-tip-pods", "Wingtip pods", [P([(0, 1.2), (0.2, 0.6), (0.2, -1.2), (-0.2, -1.2), (-0.2, 0.6)], "light")], {"detection": 0.5}),
    ("ac-lerx", "Leading-edge root extensions", [P([(0, 3), (0.9, 0), (0.9, -0.5), (0, -0.5)], "steel")], {"agility": 1}),
    ("ac-flaps", "Trailing-edge flaps", [R(0, 0, 3, 0.4, "dark"), L([(-1.5, 0), (1.5, 0)], "light")], {"field": 1}),
    ("ac-slats", "Leading-edge slats", [R(0, 0, 3, 0.25, "light")], {"field": 0.5, "agility": 0.5}),
    ("ac-spoilers", "Spoilers", [R(-0.6, 0, 0.8, 0.3, "dark"), R(0.6, 0, 0.8, 0.3, "dark")], {"field": 0.5}),
    ("ac-fences", "Wing fences", [R(0, 0, 0.1, 1.2, "dark")], {"agility": 0.3}),
    ("ac-droop-tips", "Drooping wingtips", [P([(0, 0.5), (0.8, 0.2), (0.8, -0.5), (0, -0.5)], "steel")], {"speed": 0.5}),
    ("ac-fold-hinge", "Wing-fold hinge (carrier)", [R(0, 0, 0.3, 2.4, "dark"), L([(0, -1.2), (0, 1.2)], "light")], {"field": 1})]:
    part(i, n, "Wing devices", A, sh, {"mass": 0.1}, adds, 0.5)
for i, n, sh, adds in [
    ("ac-intake-pitot", "Pitot intake", [C(0, 0, 0.5, "black"), C(0, 0, 0.3, "dark")], {"speed": 0.5}),
    ("ac-intake-caret", "Caret intakes (pair)", [P([(-1.3, 0.8), (-0.8, 1.2), (-0.8, -0.8), (-1.2, -0.8)], "dark"), P([(1.3, 0.8), (0.8, 1.2), (0.8, -0.8), (1.2, -0.8)], "dark")], {"speed": 0.5, "signature": 0.3}),
    ("ac-intake-dorsal", "Dorsal S-duct intake", [P([(-0.7, 0.8), (0.7, 0.8), (0.5, -0.8), (-0.5, -0.8)], "dark")], {"signature": 1, "speed": -0.5}),
    ("ac-intake-scoop", "Belly scoop intake", [R(0, 0, 1.2, 1.4, "dark")], {}),
    ("ac-vent-s", "NACA vent (small)", [P([(-0.1, 0.3), (0.1, 0.3), (0.2, -0.3), (-0.2, -0.3)], "black")], {}),
    ("ac-vent-m", "NACA vent (medium)", [P([(-0.15, 0.5), (0.15, 0.5), (0.3, -0.5), (-0.3, -0.5)], "black")], {}),
    ("ac-vent-l", "Cooling louvres", [R(0, 0, 0.8, 0.8, "dark")] + [L([(-0.4, -0.3 + k * 0.15), (0.4, -0.3 + k * 0.15)], "black") for k in range(5)], {})]:
    part(i, n, "Airframe", A, sh, {"mass": 0.05}, adds, 0.2)
for i, n, sh, uses, adds, comp in [
    ("ac-eots", "Electro-optical targeting (EOTS)", [P([(0, 0.4), (0.3, 0), (0, -0.2), (-0.3, 0)], "glass")], {"mass": 0.1, "power": 0.5}, {"strike": 1, "detection": 1}, None),
    ("ac-sar-pod", "SAR ground-mapping pod", [R(0, 0, 0.5, 3.5, "light"), R(0, 0, 0.3, 2.8, "glass")], {"mass": 0.3, "power": 3}, {"detection": 2}, None),
    ("ac-recon-pod", "Reconnaissance pod", [R(0, 0, 0.45, 3.0, "light"), C(0, 1.0, 0.15, "glass"), C(0, 0, 0.15, "glass")], {"mass": 0.3, "power": 1}, {"detection": 2}, None),
    ("ac-laser-pod", "Laser designator pod", [R(0, 0, 0.35, 2.2, "light"), C(0, 1.1, 0.15, "red")], {"mass": 0.2, "power": 0.5}, {"strike": 1}, None),
    ("ac-mad-boom", "MAD tail boom (anti-submarine)", [R(0, 0, 0.2, 3.0, "light"), C(0, -1.5, 0.15, "dark")], {"mass": 0.1}, {"anti-sub": 2}, None),
    ("ac-sonobuoys", "Sonobuoy launchers", [R(0, 0, 0.8, 1.6, "dark")] + [C(-0.2 + (k % 2) * 0.4, -0.6 + (k // 2) * 0.4, 0.12, "light") for k in range(8)], {"mass": 0.3}, {"anti-sub": 2}, None),
    ("ac-sla-radar", "Side-looking radar fairing", [R(0, 0, 0.5, 6.0, "light")], {"mass": 0.4, "power": 4, "cooling": 2}, {"detection": 3}, None),
    ("ac-dorsal-satcom", "Dorsal satcom fairing", [P([(0, 1.0), (0.5, 0.2), (0.5, -1.0), (-0.5, -1.0), (-0.5, 0.2)], "light")], {}, {}, "cm-satcom"),
    ("ac-iff", "IFF antenna array", [R(0, 0, 0.6, 0.2, "light")], {"mass": 0.02, "power": 0.2}, {"networking": 0.5}, None),
    ("ac-gps-patch", "Satellite navigation antenna", [C(0, 0, 0.15, "light")], {}, {}, None),
    ("ac-radalt", "Radar altimeter", [R(0, 0, 0.3, 0.3, "dark")], {"mass": 0.02, "power": 0.1}, {"field": 0.3}, None),
    ("ac-esm-tips", "ESM wingtip receivers", [R(-0.1, 0, 0.2, 0.8, "light"), R(0.1, 0, 0.2, 0.8, "light")], {"mass": 0.05, "power": 0.5}, {"ew": 1}, None),
    ("ac-das", "Distributed aperture sensor", [C(0, 0, 0.2, "glass")], {"mass": 0.02, "power": 0.3}, {"detection": 0.5, "survivability": 0.5}, None),
    ("ac-mws", "Missile warning sensors", [C(-0.2, 0, 0.12, "glass"), C(0.2, 0, 0.12, "glass")], {"mass": 0.02, "power": 0.2}, {"survivability": 1}, None)]:
    part(i, n, "Sensors & pods", A, sh, uses, adds, 1.5, comp=comp)
for i, n, sh, uses, adds in [
    ("ac-pylon-inner", "Inner wing pylon (heavy)", [R(0, 0, 0.25, 1.6, "dark")], {"mass": 0.08}, {"payload": 1.5, "signature": -1}),
    ("ac-pylon-outer", "Outer wing pylon (light)", [R(0, 0, 0.15, 1.0, "dark")], {"mass": 0.04}, {"payload": 0.5, "signature": -0.5}),
    ("ac-pylon-fuselage", "Fuselage station", [R(0, 0, 0.3, 1.4, "dark")], {"mass": 0.05}, {"payload": 1, "signature": -0.5}),
    ("ac-bay-door", "Bomb bay doors", [R(-0.4, 0, 0.7, 4, "steel"), R(0.4, 0, 0.7, 4, "steel")], {"mass": 0.3}, {"payload": 1}),
    ("ac-cargo-ramp", "Rear cargo ramp", [P([(-1.4, 0), (1.4, 0), (1.1, -3), (-1.1, -3)], "steel")], {"mass": 0.5}, {"payload": 1, "field": 0.5}),
    ("ac-side-door", "Side cargo door", [R(0, 0, 0.2, 2.0, "dark")], {"mass": 0.1}, {}),
    ("ac-gun-pod", "Gun pod", [R(0, 0, 0.4, 2.8, "steel"), R(0, 1.5, 0.12, 0.5, "black")], {"mass": 0.4}, {"air-defence": 0.5, "strike": 0.5, "signature": -0.5}),
    ("ac-buddy-pod", "Buddy refuelling pod", [R(0, 0, 0.5, 3.2, "light"), C(0, -1.7, 0.25, "dark")], {"mass": 0.4}, {"endurance": 1}),
    ("ac-drogue-pods", "Wing refuelling drogue pods", [R(0, 0, 0.6, 3.4, "light")], {"mass": 0.5}, {"endurance": 1.5}),
    ("ac-tank-1000", "Drop tank (small)", [P([(0, 1.6), (0.25, 1.2), (0.25, -1.2), (0, -1.6), (-0.25, -1.2), (-0.25, 1.2)], "light")], {"mass": 0.2}, {"endurance": 0.6, "signature": -0.5}),
    ("ac-tank-2000", "Drop tank (medium)", [P([(0, 2.2), (0.3, 1.6), (0.3, -1.6), (0, -2.2), (-0.3, -1.6), (-0.3, 1.6)], "light")], {"mass": 0.3}, {"endurance": 1, "signature": -1}),
    ("ac-tank-3000", "Drop tank (large)", [P([(0, 2.8), (0.38, 2.0), (0.38, -2.0), (0, -2.8), (-0.38, -2.0), (-0.38, 2.0)], "light")], {"mass": 0.45}, {"endurance": 1.5, "signature": -1, "speed": -0.5}),
    ("ac-internal-tank", "Internal fuel cell", [R(0, 0, 0.8, 1.8, "dark")], {"mass": 0.3, "volume": 1}, {"endurance": 1})]:
    part(i, n, "Stores", A, sh, uses, adds, 0.8, options=PYLON_OPTS if "pylon" in i else None, scaling=TANK_SCALE if "tank" in i else None)
for i, n, sh, uses, adds in [
    ("ac-canopy-frameless", "Frameless bubble canopy", [P([(0, 1.4), (0.5, 0.6), (0.45, -1.0), (-0.45, -1.0), (-0.5, 0.6)], "glass")], {"mass": 0.1}, {"agility": 0.3}),
    ("ac-canopy-tandem", "Tandem stepped canopy", [P([(0, 2.0), (0.45, 1.2), (0.45, -1.8), (-0.45, -1.8), (-0.45, 1.2)], "glass"), R(0, 0, 0.9, 0.1, "dark")], {"mass": 0.2}, {"strike": 1}),
    ("ac-cockpit-windows", "Flight deck windows (large aircraft)", [R(0, 0, 2.0, 0.6, "glass"), L([(-0.5, -0.3), (-0.5, 0.3)], "dark"), L([(0.5, -0.3), (0.5, 0.3)], "dark")], {"mass": 0.1}, {}),
    ("ac-crew-door", "Crew entrance", [R(0, 0, 0.8, 0.2, "dark")], {"mass": 0.02}, {}),
    ("ac-nav-lights", "Navigation lights", [C(-0.3, 0, 0.08, "red"), C(0.3, 0, 0.08, "glass")], {}, {}),
    ("ac-anti-ice", "Anti-ice leading edges", [R(0, 0, 3.0, 0.12, "light")], {"mass": 0.05, "power": 0.5}, {"reliability": 0.5}),
    ("ac-chaff-large", "Large countermeasure dispensers", [R(0, 0, 0.6, 1.2, "dark")] + [C(-0.15, -0.4 + k * 0.4, 0.1, "light") for k in range(3)], {"mass": 0.1}, {"survivability": 1}),
    ("ac-drag-chute", "Drag chute housing", [C(0, 0, 0.3, "dark")], {"mass": 0.05}, {"field": 1}),
    ("ac-hook-heavy", "Heavy arrestor hook", [R(0, 0, 0.18, 2.2, "dark"), R(0, -1.1, 0.6, 0.15, "dark")], {"mass": 0.15}, {"field": 1})]:
    part(i, n, "Cockpit & fittings", A, sh, uses, adds, 0.3)

# ================= SHIPS (~105) =================
for i, n, r, bl in [("gun-20", "20 mm cannon", 0.5, 1.4), ("gun-25", "25 mm chain gun", 0.6, 1.6), ("gun-35", "35 mm twin gun", 0.9, 2.0), ("gun-40", "40 mm gun", 1.0, 2.2), ("gun-57-twin", "Twin 57 mm mount", 1.8, 3.4)]:
    part(i, n, "Guns", S, turret(r, bl, twin="twin" in i or "35" in i), {"mass": 2 + r * 6}, {"air-defence": 0.5 + r / 2, "anti-ship": 0.5}, 1 + r * 3, arc=160, options=GUN_OPTS, scaling=GUN_SCALE)
part("asw-mortar", "Anti-submarine rocket launcher", "Launchers", S, [R(0, 0, 2.2, 2.2, "steel")] + [C(-0.6 + (k % 3) * 0.6, -0.5 + (k // 3) * 0.6, 0.2, "dark") for k in range(6)], {"mass": 10}, {"anti-sub": 1.5}, 6, arc=150)
part("decoy-rocket", "Decoy rocket launcher", "Electronics", S, [R(0, 0, 1.8, 1.4, "dark")] + [C(-0.6 + k * 0.4, 0, 0.15, "light") for k in range(4)], {"mass": 2}, {"ew": 1, "survivability": 0.5}, 3)
for w in (4, 6, 8, 10, 12):
    for h in (4, 8, 12, 16):
        part(f"house-{w}x{h}", f"Deckhouse {w}×{h} m", "Superstructure", S, block(w, h, "light", min(w, h) * 0.12) + [R(0, h / 2 - 0.3, w * 0.6, 0.35, "glass")] if h >= 8 else block(w, h, "light", min(w, h) * 0.12),
             {"mass": round(w * h * 0.35)}, {}, round(w * h * 0.05, 1), True, options=MATERIAL_OPTS, scaling=BLOCK_SCALE)
part("bridge-stealth", "Stealth bridge", "Superstructure", S, [P([(-6, -4), (6, -4), (4.5, 3), (0, 4.5), (-4.5, 3)], "light"), R(0, 2.8, 6, 0.4, "glass")], {"mass": 45}, {"signature": 1}, 12, True, options=MATERIAL_OPTS, scaling=BLOCK_SCALE)
part("bridge-wings", "Open bridge with wings", "Superstructure", S, block(9, 5, "light", 1) + [R(-6, 1.5, 3, 1.2, "light"), R(6, 1.5, 3, 1.2, "light"), R(0, 2.2, 7, 0.4, "glass")], {"mass": 35}, {"detection": 0.5}, 6, True, options=MATERIAL_OPTS, scaling=BLOCK_SCALE)
part("hangar-block", "Integrated hangar superstructure", "Superstructure", S, block(16, 18, "light", 2) + [R(0, -8.8, 12, 0.4, "dark")], {"mass": 110}, {"anti-sub": 1}, 20, True, "sy-hangar", options=MATERIAL_OPTS, scaling=BLOCK_SCALE)
for i, n, sh, adds in [
    ("mast-tripod", "Tripod mast", [C(0, 1, 0.4, "dark"), C(-0.9, -0.6, 0.4, "dark"), C(0.9, -0.6, 0.4, "dark"), R(0, 0, 3, 0.3, "light")], {"detection": 1}),
    ("mast-quad", "Quadruped mast", [R(0, 0, 3, 3, "dark"), L([(-1.5, -1.5), (1.5, 1.5)], "light"), L([(-1.5, 1.5), (1.5, -1.5)], "light"), R(0, 0, 1.2, 1.2, "light")], {"detection": 1}),
    ("mast-yard", "Pole mast with yardarm", [C(0, 0, 0.3, "dark"), R(0, 0, 5, 0.2, "light")], {"networking": 0.5}),
    ("mast-tower", "Radar tower", [R(0, 0, 4, 4, "light"), R(0, 0, 2.4, 2.4, "dark")], {"detection": 2}),
    ("mast-comms", "Communications mast", [C(0, 0, 0.5, "dark")] + [L([(0, 0), (1.6 * _m.cos(a), 1.6 * _m.sin(a))], "light") for a in (0, 2.1, 4.2)], {"networking": 1})]:
    part(i, n, "Masts", S, sh, {"mass": 8}, adds, 3, True, scaling=SC("Mast size", 0.7, 1.5, {"detection": 0.5}))
for i, n, sh in [("funnel-raked", "Raked funnel", [P([(-1.8, -3), (1.8, -3), (1.4, 3.4), (-1.4, 3.4)], "steel"), R(0, 0.8, 2, 3.6, "black")]),
                 ("funnel-cap", "Capped funnel", [R(0, 0, 3.6, 5, "steel"), R(0, 0, 4.2, 1, "dark"), R(0, 0, 2, 3, "black")]),
                 ("funnel-side", "Side-exhaust uptakes", [R(-3, 0, 1.2, 3, "steel"), R(3, 0, 1.2, 3, "steel")])]:
    part(i, n, "Funnels", S, sh, {"mass": 7}, {"signature": 0.5} if "side" in i else {}, 1.5, True, scaling=SC("Funnel size", 0.7, 1.5, {}, vis=1))
for i, n, sh, uses, adds, comp, top in [
    ("eo-director", "EO/IR fire-control director", [C(0, 0, 0.9, "light"), R(0, 0.6, 1.2, 0.6, "glass")], {"mass": 2, "power": 1}, {"fire-channels": 1, "detection": 1}, None, True),
    ("irst-ship", "Shipboard IRST", [C(0, 0, 0.7, "dark"), C(0, 0, 0.35, "glass")], {}, {}, "sn-irst", True),
    ("nav-xband", "X-band navigation radar", [R(0, 0, 2.6, 0.35, "light"), C(0, 0, 0.3, "dark")], {"mass": 0.4, "power": 0.3}, {"detection": 1}, None, True),
    ("surf-search", "Surface-search radar", [R(0, 0, 3.4, 0.5, "light"), C(0, 0, 0.4, "dark")], {"mass": 1.5, "power": 1}, {"detection": 2}, None, True),
    ("aesa-3face", "Active array (3 faces)", [R(0, 0, 7, 7, "light"), R(0, 3.6, 4, 0.5, "glass"), R(3.6, -1, 0.5, 4, "glass"), R(-3.6, -1, 0.5, 4, "glass")], {"mass": 8}, {}, "sn-radar-aesa", True),
    ("aesa-panel", "Fixed array panel (single)", [R(0, 0, 4, 0.8, "light"), R(0, 0.3, 3.2, 0.3, "glass")], {"mass": 5, "power": 4, "cooling": 2}, {"detection": 3, "fire-channels": 1}, None, True),
    ("fc-radar-2", "Fire-control radar (twin dish)", [C(-0.6, 0, 0.6, "light"), C(0.6, 0, 0.6, "light")], {"mass": 2, "power": 2}, {"fire-channels": 2}, None, True),
    ("fc-radar-cw", "Illuminator (tracking)", [C(0, 0, 0.8, "light"), R(0, 0.7, 1.4, 0.3, "glass")], {"mass": 1.5, "power": 1.5}, {"fire-channels": 1}, None, True),
    ("sonar-hull-mid", "Hull-mounted sonar (midships)", [P([(0, 2.5), (1.2, 0.5), (1, -2), (-1, -2), (-1.2, 0.5)], "glass")], {"mass": 15}, {"anti-sub": 2}, None, False),
    ("sonar-flank", "Flank array sonar", [R(0, 0, 0.5, 20, "glass")], {"mass": 10}, {"anti-sub": 2, "signature": 0.3}, None, False),
    ("sonar-mine", "Mine-avoidance sonar", [C(0, 0, 0.8, "glass")], {"mass": 2}, {"anti-sub": 0.5}, None, False),
    ("towed-decoy", "Towed torpedo decoy", [R(0, 0, 1.6, 2, "light"), L([(0, -1), (0, -4)], "dark")], {"mass": 3}, {"survivability": 1}, None, False),
    ("esm-mast", "Masthead ESM", [C(0, 0, 0.9, "light"), C(0, 0, 0.45, "glass")], {"mass": 1, "power": 1}, {"ew": 1, "detection": 0.5}, None, True)]:
    part(i, n, "Sensors", S, sh, uses, adds, 5, top, comp, arc=150 if "fc" in i or "director" in i else 0,
         options=SONAR_OPTS if i.startswith("sonar") else None, scaling=ARRAY_SCALE if "aesa" in i or "radar" in i or "search" in i else None)
for i, n, sh, uses, adds in [
    ("vls-half-8", "Half-length VLS, 8 cells", cells(8, 0.6, 2), {"mass": 20, "volume": 1}, {"air-defence": 0.5}),
    ("vls-half-16", "Half-length VLS, 16 cells", cells(16, 0.6, 2), {"mass": 38, "volume": 1}, {"air-defence": 1}),
    ("can-2x2-low", "Low-profile canisters (2×2)", canisters(2) + [R(0, -3.6, 2.4, 0.4, "dark")], {"mass": 9, "volume": 1}, {"anti-ship": 1}),
    ("tt-single", "Single torpedo tube", [C(0, 0, 0.5), R(0, 2, 0.45, 4)], {"mass": 1.5}, {"anti-sub": 0.5}),
    ("cell-triple", "Triple-pack launch cell", [R(0, 0, 2, 2, "deck")] + [C(-0.5 + k * 0.5, 0, 0.2, "dark") for k in range(3)], {"mass": 6}, {"air-defence": 0.5}),
    ("mine-rails-2", "Mine rails (twin)", [L([(-1.6, -8), (-1.6, 8)], "dark"), L([(-0.6, -8), (-0.6, 8)], "dark"), L([(0.6, -8), (0.6, 8)], "dark"), L([(1.6, -8), (1.6, 8)], "dark")], {"mass": 6}, {}),
    ("decoy-6", "Decoy launcher (6 tubes)", [R(0, 0, 1.6, 1.2, "dark")] + [C(-0.5 + (k % 3) * 0.5, -0.25 + (k // 3) * 0.5, 0.18, "light") for k in range(6)], {"mass": 1.5}, {"ew": 1}),
    ("decoy-12", "Decoy launcher (12 tubes)", [R(0, 0, 2.2, 1.6, "dark")] + [C(-0.75 + (k % 4) * 0.5, -0.5 + (k // 4) * 0.5, 0.18, "light") for k in range(12)], {"mass": 2.5}, {"ew": 1.5, "survivability": 0.5}),
    ("decoy-vertical", "Vertical decoy cells", [R(0, 0, 1.4, 1.4, "dark")] + [C(-0.3 + (k % 2) * 0.6, -0.3 + (k // 2) * 0.6, 0.2, "light") for k in range(4)], {"mass": 1.5}, {"ew": 1})]:
    part(i, n, "Launchers", S, sh, uses, adds, 4, offers=["launch-cell"] if "vls" in i or "can" in i or "cell" in i else [], arc=180 if "can" in i else 0)
for i, n, sh, uses, adds, top in [
    ("glide-slope", "Helicopter glide-slope indicator", [R(0, 0, 1.2, 0.6, "light"), R(0, 0, 0.8, 0.2, "glass")], {"mass": 0.3}, {"anti-sub": 0.2}, True),
    ("hangar-door", "Hangar door", [R(0, 0, 8, 0.6, "dark")], {"mass": 4}, {}, False),
    ("flyco", "Flight control station", [block(4, 3, "light", 0.5)[0], R(0, 1.2, 3, 0.4, "glass")], {"mass": 6}, {"anti-sub": 0.3}, True),
    ("uav-rail", "Drone launch rail", [R(0, 0, 0.8, 8, "dark"), R(0, 3.8, 2, 0.4, "light")], {"mass": 3}, {"detection": 1}, False),
    ("uav-hangar", "Drone hangar (small)", block(6, 6, "light", 1) + [R(0, -2.9, 4, 0.3, "dark")], {"mass": 12}, {"detection": 1}, True),
    ("catapult-short", "Short catapult", [R(0, 0, 1.2, 60, "dark"), R(0, 29, 3, 2, "light")], {"mass": 180, "power": 3}, {"strike": 1}, False),
    ("jbd", "Jet blast deflector", [R(0, 0, 12, 1.2, "steel")], {"mass": 20}, {}, False),
    ("tie-downs", "Tie-down grid", [R(0, 0, 12, 12, "deck")] + [C(-5 + (k % 6) * 2, -5 + (k // 6) * 2, 0.15, "dark") for k in range(36)], {"mass": 2}, {}, False),
    ("vtol-pad", "VTOL landing spot", [R(0, 0, 18, 18, "deck"), C(0, 0, 6, "dark"), C(0, 0, 5.5, "deck"), L([(-3, 0), (3, 0)], "light")], {"mass": 15}, {"strike": 0.5}, False)]:
    part(i, n, "Aviation", S, sh, uses, adds, 3, top)
for i, n, sh, uses in [
    ("davit-twin", "Twin boat davits", [R(-1.2, 0, 0.5, 5, "dark"), R(1.2, 0, 0.5, 5, "dark")], {"mass": 4}),
    ("boat-bay", "Enclosed boat bay", block(5, 9, "light", 1) + [R(0, -4.4, 4, 0.3, "dark")], {"mass": 10}),
    ("stern-ramp", "Stern launch ramp", [P([(-2.5, 3), (2.5, 3), (2, -3), (-2, -3)], "deck"), L([(0, 3), (0, -3)], "dark")], {"mass": 8}),
    ("well-gate", "Well-deck stern gate", [R(0, 0, 14, 1, "steel")], {"mass": 30}),
    ("lcac-spot", "Landing craft spot", [R(0, 0, 14, 26, "deck"), L([(-7, -13), (7, 13)], "dark")], {"mass": 5}),
    ("lifeboat", "Enclosed lifeboat", [P([(0, 4.5), (1.4, 3), (1.4, -4), (-1.4, -4), (-1.4, 3)], "red")], {"mass": 4}),
    ("liferaft-cluster", "Life raft cluster", [C(-0.5 + (k % 3) * 0.5, -0.4 + (k // 3) * 0.8, 0.35, "red") for k in range(6)], {"mass": 1.5})]:
    part(i, n, "Boats", S, sh, uses, {}, 1, True if "lifeboat" in i else False)
for i, n, sh, uses, adds in [
    ("winch", "Deck winch", [R(0, 0, 1.2, 0.8, "dark"), C(0, 0, 0.35, "light")], {"mass": 1}, {}),
    ("capstan-large", "Large capstan", [C(0, 0, 0.9, "dark"), C(0, 0, 0.5, "steel")], {"mass": 2}, {}),
    ("windlass", "Anchor windlass", [R(0, 0, 3, 1.2, "dark"), C(-1, 0, 0.5, "steel"), C(1, 0, 0.5, "steel")], {"mass": 4}, {}),
    ("fairleads", "Fairleads", [R(-0.5, 0, 0.3, 0.8, "dark"), R(0.5, 0, 0.3, 0.8, "dark")], {"mass": 0.3}, {}),
    ("cleats", "Mooring cleats", [R(0, 0, 1, 0.3, "dark")], {"mass": 0.1}, {}),
    ("vent-s", "Ventilation cowl (small)", [C(0, 0, 0.4, "steel"), C(0, 0.1, 0.2, "black")], {"mass": 0.2}, {}),
    ("vent-m", "Ventilation cowl (medium)", [C(0, 0, 0.7, "steel"), C(0, 0.2, 0.35, "black")], {"mass": 0.5}, {}),
    ("vent-l", "Ventilation intake (large)", [R(0, 0, 2.4, 1.2, "steel")] + [L([(-1, -0.4 + k * 0.2), (1, -0.4 + k * 0.2)], "black") for k in range(5)], {"mass": 1.5}, {}),
    ("whip-cluster", "Whip antenna cluster", [C(-1 + k * 0.5, 0, 0.12, "dark") for k in range(5)], {"mass": 0.3}, {"networking": 0.5}),
    ("searchlight", "Searchlight", [C(0, 0, 0.4, "light"), C(0, 0.2, 0.2, "glass")], {"mass": 0.2}, {}),
    ("flag-mast", "Ensign staff", [C(0, 0, 0.15, "dark"), R(0.4, 0.3, 0.8, 0.5, "red")], {"mass": 0.05}, {}),
    ("ammo-hoist", "Ammunition hoist", [R(0, 0, 1.6, 1.6, "dark"), R(0, 0, 1, 1, "steel")], {"mass": 2}, {"reliability": 0.3}),
    ("ras-kingpost", "Replenishment kingpost", [C(0, 0, 0.6, "dark"), L([(0, 0), (4, 0)], "dark"), L([(0, 0), (-4, 0)], "dark")], {"mass": 6}, {"endurance": 0.5}),
    ("gangway", "Accommodation ladder", [R(0, 0, 1.0, 6, "steel")] + [L([(-0.5, -3 + k * 0.6), (0.5, -3 + k * 0.6)], "dark") for k in range(10)], {"mass": 1}, {}),
    ("anchor-single", "Single anchor", [C(0, 0, 0.7, "dark"), L([(0, 0), (0, 3)], "dark")], {"mass": 4}, {}),
    ("bow-thruster", "Bow thruster tunnel", [R(0, 0, 3, 1.2, "black")], {"mass": 5, "power": 1}, {"agility": 0.5}),
    ("fuel-station", "Refuelling station", [R(0, 0, 2, 2, "light"), C(0, 0, 0.5, "red")], {"mass": 2}, {"endurance": 0.3}),
    ("damage-control", "Damage-control locker", [R(0, 0, 1.4, 1.0, "red")], {"mass": 0.5}, {"survivability": 0.3}),
    ("washdown", "CBRN wash-down system", [L([(-4, 0), (4, 0)], "glass")] + [C(-4 + k * 2, 0, 0.12, "glass") for k in range(5)], {"mass": 2}, {"survivability": 0.5}),
    ("deck-markings", "Deck markings", [L([(-3, 0), (3, 0)], "light"), L([(0, -3), (0, 3)], "light")], {}, {})]:
    part(i, n, "Deck gear", S, sh, uses, adds, 0.5)
for i, n, sh, uses, adds in [
    ("sail-tall", "Tall submarine sail", [P([(0, 5), (1.6, 2), (1.4, -5), (-1.4, -5), (-1.6, 2)], "dark")], {"mass": 35}, {}),
    ("sail-blended", "Blended low-drag sail", [P([(0, 6), (1.3, 1), (1.0, -6), (-1.0, -6), (-1.3, 1)], "dark")], {"mass": 28}, {"signature": 0.5}),
    ("sail-planes", "Sail planes", [R(0, 0, 8, 0.6, "dark")], {"mass": 4}, {"agility": 0.5}),
    ("bow-planes", "Retractable bow planes", [R(-2.2, 0, 2, 0.5, "dark"), R(2.2, 0, 2, 0.5, "dark")], {"mass": 4}, {"agility": 0.5}),
    ("x-rudder", "X-form rudder", [L([(-3, -3), (3, 3)], "dark"), L([(-3, 3), (3, -3)], "dark")], {"mass": 6}, {"agility": 1}),
    ("pump-jet", "Pump-jet propulsor", [C(0, 0, 1.8, "dark"), C(0, 0, 1.2, "black")], {"mass": 20}, {"signature": 1, "speed": 0.5}),
    ("dry-deck", "Dry deck shelter", [P([(0, 7), (2, 5), (2, -7), (-2, -7), (-2, 5)], "steel")], {"mass": 30}, {}),
    ("towed-fairing", "Towed array fairing", [R(0, 0, 0.5, 12, "dark")], {"mass": 3}, {"anti-sub": 1}),
    ("uuv-hatch", "Drone lock-out hatch", [C(0, 0, 1.0, "steel"), C(0, 0, 0.6, "dark")], {"mass": 3}, {"detection": 0.5}),
    ("tube-row-4", "Vertical tubes (4)", [R(0, 0, 2.6, 7, "dark")] + [C(0, -2.6 + k * 1.7, 0.6, "steel") for k in range(4)], {"mass": 30}, {"strike": 1}),
    ("tube-row-6", "Vertical tubes (6)", [R(0, 0, 2.6, 10, "dark")] + [C(0, -4 + k * 1.6, 0.6, "steel") for k in range(6)], {"mass": 45}, {"strike": 1.5})]:
    part(i, n, "Submarine fittings", S, sh, uses, adds, 5)

# ================= GROUND (~25) =================
for axles in (2, 4, 5):
    part(f"g-wheels-{axles}", f"Wheel set ({axles} axles)", "Vehicle", G, [R(-1.3, -((axles - 1) * 2) / 2 + k * 2, 0.4, 1.1, "black") for k in range(axles)] + [R(1.3, -((axles - 1) * 2) / 2 + k * 2, 0.4, 1.1, "black") for k in range(axles)], {"mass": 0.4 * axles}, {"mobility": 0.3 * axles}, 0.3 * axles)
for i, n, sh, uses, adds, top in [
    ("g-tracks-wide", "Wide track run", [R(-1.7, 0, 0.8, 7, "black"), R(1.7, 0, 0.8, 7, "black")], {"mass": 6}, {"mobility": 1.5, "survivability": 1}, False),
    ("g-turret-cannon", "Cannon turret", turret(1.2, 3.0), {"mass": 4, "power": 0.5}, {"air-defence": 1}, False),
    ("g-turret-missile", "Missile turret (4)", [R(0, 0, 2.4, 2.4, "light")] + [R(-0.9 + k * 0.6, 0.6, 0.4, 1.6, "dark") for k in range(4)], {"mass": 5, "power": 1}, {"air-defence": 1.5}, False),
    ("g-cab-crew", "Crew cab (4 doors)", [R(0, 0, 2.5, 3.2, "steel"), R(0, 1.3, 2.1, 0.5, "glass")], {"mass": 2.5}, {}, False),
    ("g-cab-lo", "Low-profile armoured cab", [P([(-1.3, -1.4), (1.3, -1.4), (1.1, 1.4), (-1.1, 1.4)], "dark")], {"mass": 3.5}, {"survivability": 1, "signature": 0.5}, False),
    ("g-bed-short", "Short flatbed", [R(0, 0, 2.5, 4, "deck")], {"mass": 0.6}, {}, False),
    ("g-bed-long", "Long flatbed", [R(0, 0, 2.5, 8, "deck")], {"mass": 1.2}, {}, False),
    ("g-jacks", "Levelling jacks", [C(-1.4, 2.5, 0.3, "dark"), C(1.4, 2.5, 0.3, "dark"), C(-1.4, -2.5, 0.3, "dark"), C(1.4, -2.5, 0.3, "dark")], {"mass": 0.6}, {}, False),
    ("g-spare-wheel", "Spare wheel", [C(0, 0, 0.55, "black"), C(0, 0, 0.2, "steel")], {"mass": 0.2}, {"reliability": 0.2}, False),
    ("g-jerrycans", "Jerry can rack", [R(-0.3 + k * 0.3, 0, 0.25, 0.5, "deck") for k in range(3)], {"mass": 0.1}, {"endurance": 0.2}, False),
    ("g-ladder", "Access ladder", [R(0, 0, 0.5, 1.6, "steel")], {"mass": 0.05}, {}, False),
    ("g-gen-small", "Small generator", [R(0, 0, 1.2, 1.0, "light")], {"mass": 0.8, "power": -20}, {}, False),
    ("g-gen-large", "Large generator trailer", [R(0, 0, 2.2, 3.6, "light"), R(0, -1.4, 1.4, 0.4, "dark")], {"mass": 4, "power": -120}, {}, False),
    ("g-mast-tall", "Tall radar mast", [C(0, 0, 0.5, "dark"), C(0, 0, 0.25, "light")], {"mass": 1.5}, {"detection": 1}, True),
    ("g-satcom-dish", "Satcom dish", [C(0, 0, 1.2, "light"), C(0, 0, 0.3, "dark")], {"mass": 0.6, "power": 1}, {"networking": 2}, True),
    ("g-rack-6", "Launcher rack (6 tubes)", [R(0, 0, 2.4, 3.0, "light")] + [C(-0.8 + (k % 3) * 0.8, -0.5 + (k // 3) * 1.0, 0.3, "dark") for k in range(6)], {"mass": 7}, {"air-defence": 1.5}, False),
    ("g-rack-12", "Launcher pod (12 tubes)", [R(0, 0, 2.4, 3.6, "light")] + [C(-0.9 + (k % 4) * 0.6, -1.0 + (k // 4) * 1.0, 0.22, "dark") for k in range(12)], {"mass": 10}, {"air-defence": 2, "strike": 1}, False),
    ("g-rack-quad-box", "Quad box launcher", [R(0, 0, 2.6, 6, "light"), L([(-1.3, 0), (1.3, 0)], "dark"), L([(0, -3), (0, 3)], "dark")], {"mass": 9}, {"strike": 1.5}, False),
    ("g-camo-screen", "Signature screen", [R(0, 0, 3, 5, "deck")], {"mass": 0.5}, {"signature": 1}, False),
    ("g-decoy-inflatable", "Inflatable decoy", [R(0, 0, 2.5, 7, "light"), L([(-1.25, -3.5), (1.25, 3.5)], "dark")], {"mass": 0.3}, {"survivability": 0.5}, False),
    ("g-ew-mast", "EW antenna mast", [C(0, 0, 0.4, "dark")] + [L([(0, 0), (1.2 * _m.cos(a), 1.2 * _m.sin(a))], "light") for a in (0, 1.6, 3.1, 4.7)], {"mass": 1, "power": 2}, {"ew": 1}, True),
    ("g-optics", "Optical sensor head", [C(0, 0, 0.5, "dark"), C(0, 0.2, 0.2, "glass")], {"mass": 0.3, "power": 0.3}, {"detection": 1}, True),
    ("g-filtration", "CBRN filtration unit", [R(0, 0, 1.0, 0.8, "light")], {"mass": 0.4, "power": 0.3}, {"survivability": 0.3}, False),
    ("g-cable-reel", "Cable reel", [C(0, 0, 0.6, "dark"), C(0, 0, 0.25, "steel")], {"mass": 0.3}, {"networking": 0.3}, False)]:
    part(i, n, "Vehicle", G, sh, uses, adds, 1, top, scaling=ARRAY_SCALE if "radar" in n.lower() else None)
