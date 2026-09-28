# Included by gen_parts.py. Submarine expansion and crew accommodation parts. Simple game trade-offs only:
# depth, quieting and submerged endurance are abstract 0-10 ratings, not engineering data.

GRADE_OPTS = [OPT("grade", "Hull steel grade", [
    CH("std", "Standard steel", note="cheap, shallow"),
    CH("hy", "High-yield steel", {"mass": 1.15}, adds={"depth": 2}, cost=1.4, note="deeper, heavier"),
    CH("ti", "Titanium", {"mass": 0.85}, adds={"depth": 4, "quieting": 0.5}, cost=3.0, note="deepest, very costly"),
], "hy")]
HULL_OPTS = [OPT("layout", "Hull layout", [
    CH("single", "Single hull", note="lighter, more room"),
    CH("double", "Double hull", {"mass": 1.25}, adds={"survivability": 1, "quieting": -0.5}, cost=1.2, note="tougher, a bit noisier"),
], "single")]
BATT_OPTS = [OPT("chem", "Battery type", [
    CH("pb", "Lead-acid", note="standard"),
    CH("li", "Lithium-ion", {"mass": 0.7}, adds={"submerged-endurance": 1.5, "survivability": -0.3}, cost=2.0, note="more endurance, fire risk"),
], "pb")]
AIP_OPTS = [OPT("cycle", "AIP type", [
    CH("stirling", "Stirling engine", note="simple, some noise"),
    CH("fuelcell", "Fuel cell", adds={"quieting": 1, "submerged-endurance": 0.5}, cost=1.5, note="quiet, costly"),
    CH("closed", "Closed-cycle diesel", {"mass": 1.1}, adds={"submerged-endurance": 1, "quieting": -0.5}, cost=0.9, note="more power, louder"),
], "stirling")]
PROP_SUB_OPTS = [OPT("prop", "Propulsor", [
    CH("skew7", "7-blade skewed propeller", note="standard"),
    CH("pumpjet", "Shrouded pump-jet", {"mass": 1.4}, adds={"quieting": 1.5, "speed": 0.5}, cost=1.8, sy=1.2, note="quiet and fast, heavy"),
    CH("rim", "Rim-driven propulsor", {"mass": 1.1}, add={"power": 1.5}, adds={"quieting": 1}, cost=2.2, note="quiet, needs power"),
], "skew7")]
MAST_OPTS = [OPT("role", "Mast role", [
    CH("search", "Search", adds={"detection": 0.5}),
    CH("attack", "Attack (small head)", adds={"quieting": 0.3, "detection": -0.3}, sx=0.6, sy=0.6)], "search")]
TOWED_OPTS = [OPT("line", "Array line", [
    CH("thin", "Thin line", adds={"anti-sub": 0.5}),
    CH("fat", "Fat line", {"mass": 1.5}, adds={"anti-sub": 1.2}, cost=1.4, sx=1.6)], "thin")]
BATT_SCALE = SC("Battery size", 0.6, 1.8, {"submerged-endurance": 1}, vis=0)
ROOM_SCALE = SC("Room size", 0.6, 1.6, {"anti-ship": 1}, vis=0)
BERTH_SCALE = SC("Berth count", 0.5, 2.0, {}, vis=0.2)


def spokes(n, r, f="steel"):
    return [L([(0, 0), (r * _m.cos(k * 2 * _m.pi / n), r * _m.sin(k * 2 * _m.pi / n))], f) for k in range(n)]


for i, n, sh, uses, adds, cat, opts, sc, cost in [
    # pressure hull and buoyancy
    ("ph-fwd", "Pressure hull section (forward)", [P([(0, 6), (3, 3), (3, -6), (-3, -6), (-3, 3)], "dark")], {"mass": 90, "volume": 1}, {"depth": 3}, "Pressure hull", GRADE_OPTS + HULL_OPTS, None, 20),
    ("ph-mid", "Pressure hull section (midbody)", [R(0, 0, 6, 12, "dark"), L([(-3, 0), (3, 0)], "black")], {"mass": 140}, {"depth": 3}, "Pressure hull", GRADE_OPTS + HULL_OPTS, None, 25),
    ("ph-aft", "Pressure hull section (aft)", [P([(-3, 6), (3, 6), (2, -5), (-2, -5)], "dark")], {"mass": 80}, {"depth": 3}, "Pressure hull", GRADE_OPTS + HULL_OPTS, None, 18),
    ("ph-plug", "Hull plug (extra compartment)", [R(0, 0, 6, 6, "dark")], {"mass": 60, "volume": -3}, {"depth": 2.5}, "Pressure hull", GRADE_OPTS, None, 10),
    ("anechoic", "Anechoic tile coating", [L([(-3, -6), (3, -6)], "black"), L([(-3, 0), (3, 0)], "black"), L([(-3, 6), (3, 6)], "black")], {"mass": 25}, {"quieting": 1.5}, "Pressure hull", None, None, 12),
    ("rafted-deck", "Rafted machinery deck", [R(0, 0, 4, 6, "steel"), R(0, 0, 3, 5, "dark")], {"mass": 35}, {"quieting": 1.5}, "Pressure hull", None, None, 15),
    ("decoupling", "Hull decoupling layer", [R(0, 0, 5, 10, "deck")], {"mass": 15}, {"quieting": 0.7}, "Pressure hull", None, None, 8),
    ("ballast-main", "Main ballast tanks", [R(-2.4, 0, 1.2, 8, "black"), R(2.4, 0, 1.2, 8, "black")], {"mass": 30}, {"survivability": 0.3}, "Ballast", None, None, 4),
    ("ballast-trim", "Trim and compensating tanks", [R(0, 0, 2.4, 2.4, "black"), C(0, 0, 0.6, "steel")], {"mass": 8}, {"agility": 0.3}, "Ballast", None, None, 2),
    # propulsion (abstract)
    ("sub-diesel-gen", "Diesel generator set", [R(0, 0, 3.6, 4, "steel"), C(-0.8, 0, 0.5, "dark"), C(0.8, 0, 0.5, "dark")], {"mass": 45, "power": -6}, {"endurance": 1, "quieting": -0.5}, "Sub propulsion", None, None, 10),
    ("sub-battery", "Battery section", [R(0, 0, 4, 6, "light")] + [L([(-2, -3 + k), (2, -3 + k)], "dark") for k in range(7)], {"mass": 60}, {"submerged-endurance": 2}, "Sub propulsion", BATT_OPTS, BATT_SCALE, 12),
    ("sub-aip", "AIP module", [R(0, 0, 5, 7, "steel"), C(0, 1.5, 1.0, "light"), C(0, -1.5, 1.0, "light")], {"mass": 80, "power": -3}, {"submerged-endurance": 3, "quieting": 0.3}, "Sub propulsion", AIP_OPTS, None, 40),
    ("sub-reactor", "Reactor compartment (abstract)", [R(0, 0, 6, 9, "dark"), C(0, 0, 2, "yellow")], {"mass": 700, "power": -60, "cooling": -6}, {"submerged-endurance": 8, "endurance": 4, "speed": 1, "quieting": -1}, "Sub propulsion", None, None, 400),
    ("sub-motor", "Main propulsion motor", [C(0, 0, 1.6, "steel"), C(0, 0, 0.8, "dark")], {"mass": 30, "power": 4}, {"speed": 0.5}, "Sub propulsion", None, None, 8),
    ("sub-propulsor", "Propulsor", [C(0, 0, 1.6, "dark")] + spokes(7, 1.5), {"mass": 15}, {"speed": 0.5}, "Sub propulsion", PROP_SUB_OPTS, None, 6),
    # sensors and masts
    ("sonar-sphere", "Bow sonar sphere", [C(0, 0, 3, "glass"), C(0, 0, 2.2, "dark")], {"mass": 40, "power": 3}, {"anti-sub": 2, "detection": 1}, "Sub sensors", SONAR_OPTS, None, 25),
    ("sonar-chin", "Chin array", [P([(-2.5, 1), (2.5, 1), (2, -1), (-2, -1)], "glass")], {"mass": 15, "power": 1}, {"anti-sub": 1}, "Sub sensors", None, None, 10),
    ("sonar-flank", "Flank arrays (pair)", [R(-2.8, 0, 0.4, 14, "glass"), R(2.8, 0, 0.4, 14, "glass")], {"mass": 20, "power": 1}, {"anti-sub": 1.5}, "Sub sensors", None, None, 18),
    ("sonar-wide", "Wide-aperture array", [R(-2.9, 0, 0.3, 20, "glass"), R(2.9, 0, 0.3, 20, "glass")] + [C(s * 2.9, y, 0.3, "light") for s in (-1, 1) for y in (-8, 0, 8)], {"mass": 28, "power": 2}, {"anti-sub": 2}, "Sub sensors", None, None, 28),
    ("towed-reel", "Towed array reel", [C(0, 0, 1.2, "dark"), C(0, 0, 0.5, "steel")], {"mass": 8, "power": 0.5}, {"anti-sub": 1}, "Sub sensors", TOWED_OPTS, None, 10),
    ("sail-array", "Sail sonar array", [R(0, 0, 1.6, 2.4, "glass")], {"mass": 4}, {"anti-sub": 0.5}, "Sub sensors", None, None, 4),
    ("mine-avoid", "Mine-avoidance sonar", [C(0, 0, 0.8, "glass")], {"mass": 3, "power": 0.5}, {"survivability": 0.4}, "Sub sensors", None, None, 4),
    ("snorkel", "Snorkel mast", [C(0, 0, 0.5, "dark"), C(0, 0.2, 0.25, "steel")], {"mass": 3}, {"endurance": 0.5, "quieting": -0.2}, "Sub masts", None, None, 2),
    ("photonic-mast", "Optronic mast", [C(0, 0, 0.6, "dark"), C(0, 0.25, 0.3, "glass")], {"mass": 2, "power": 0.5}, {"detection": 1}, "Sub masts", MAST_OPTS, None, 5),
    ("periscope", "Periscope", [C(0, 0, 0.4, "steel"), C(0, 0.15, 0.2, "glass")], {"mass": 1.5}, {"detection": 0.5}, "Sub masts", MAST_OPTS, None, 2),
    ("esm-mast", "ESM mast", [C(0, 0, 0.5, "dark")] + spokes(4, 0.8, "light"), {"mass": 1.5, "power": 0.5}, {"ew": 1}, "Sub masts", None, None, 4),
    ("comms-mast", "Communications mast", [C(0, 0, 0.4, "dark"), L([(0, 0), (0, 1)], "light")], {"mass": 1, "power": 0.3}, {"networking": 1}, "Sub masts", None, None, 3),
    ("wire-buoy", "Floating wire buoy", [C(0, 0, 0.7, "yellow"), L([(0, 0), (0, -4)], "dark")], {"mass": 1}, {"networking": 1, "quieting": 0.2}, "Sub masts", None, None, 3),
    # weapons rooms
    ("tube-nest-4", "Bow tube nest (4)", [R(0, 0, 4, 3, "dark")] + [C(-1.4 + k * 0.93, 0.8, 0.4, "black") for k in range(4)], {"mass": 40}, {"anti-ship": 2, "anti-sub": 1}, "Torpedo room", None, None, 15),
    ("tube-nest-6", "Bow tube nest (6)", [R(0, 0, 5, 3.4, "dark")] + [C(-2 + k * 0.8, 0.8, 0.35, "black") for k in range(6)], {"mass": 60}, {"anti-ship": 3, "anti-sub": 1.5}, "Torpedo room", None, None, 22),
    ("reload-room", "Weapons reload room", [R(0, 0, 5, 7, "steel")] + [R(-1.5 + k, 0, 0.5, 6, "dark") for k in range(4)], {"mass": 50}, {"anti-ship": 1, "endurance": 0.5}, "Torpedo room", None, ROOM_SCALE, 10),
    ("mine-rail", "Mine stowage rail", [R(0, 0, 1.2, 10, "steel")] + [C(0, -4 + k * 2, 0.45, "dark") for k in range(5)], {"mass": 20}, {"strike": 0.5}, "Torpedo room", None, None, 6),
    ("cm-launcher", "Countermeasure launcher", [R(0, 0, 1.6, 1.6, "dark"), C(0, 0, 0.4, "light")], {"mass": 3}, {"survivability": 0.8}, "Torpedo room", None, None, 5),
    ("uuv-bay", "Drone bay", [P([(0, 5), (1.8, 3), (1.8, -5), (-1.8, -5), (-1.8, 3)], "light")], {"mass": 25, "power": 1}, {"detection": 1, "anti-sub": 0.5}, "Torpedo room", None, None, 20),
    # control
    ("control-room", "Control room", [R(0, 0, 5, 5, "steel"), C(0, 0, 0.8, "glass")], {"mass": 20, "power": 1, "crew": 12}, {"fire-channels": 1, "networking": 1}, "Sub control", None, SC("Room size", 0.7, 1.5, {"fire-channels": 1}, vis=0), 10),
    ("escape-trunk", "Escape trunk", [C(0, 0, 1.0, "yellow"), C(0, 0, 0.5, "dark")], {"mass": 4}, {"survivability": 0.5}, "Sub control", None, None, 3),
    ("rescue-seat", "Rescue seat (mating ring)", [C(0, 0, 1.3, "steel"), C(0, 0, 0.9, "black")], {"mass": 3}, {"survivability": 0.3}, "Sub control", None, None, 2),
    ("galley-mess", "Galley and mess", [R(0, 0, 4, 4, "light"), R(0, 0, 3, 1, "dark")], {"mass": 10, "crew": -6}, {"endurance": 0.5}, "Accommodation", None, None, 3),
]:
    part(i, n, cat, S, sh, uses, adds, cost, options=opts, scaling=sc)

# berths: crew spaces are parts that provide crew (negative use); the berth slider sizes them
for i, n, fits, w, h, berths, mass in [
    ("berth-sub", "Submarine berthing module", S, 4, 5, 20, 12),
    ("berth-small", "Crew berths (small)", S, 5, 6, 30, 20),
    ("berth-large", "Crew berths (large)", S, 8, 10, 80, 50),
    ("berth-flag", "Flag and staff quarters", S, 8, 6, 30, 30),
    ("g-crew-shelter", "Crew shelter module", G, 2.4, 3, 6, 2),
]:
    part(i, n, "Accommodation", fits, [R(0, 0, w, h, "light")] + [L([(-w / 2, -h / 2 + k * h / 4), (w / 2, -h / 2 + k * h / 4)], "dark") for k in range(1, 4)],
         {"mass": mass, "volume": 1, "crew": -berths}, {"endurance": 0.2}, round(mass / 5, 1), scaling=BERTH_SCALE)
