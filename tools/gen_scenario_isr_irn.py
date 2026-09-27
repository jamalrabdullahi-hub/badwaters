"""Adds the Israel vs Iran scenario to data/core/scenarios.json (run after gen_scenarios.py).
Positions are approximate public map locations; all performance is game ratings. Nuclear forces
stay abstract: no nuclear sites or strategic forces are modelled. Run: python tools/gen_scenario_isr_irn.py"""
import json, math, os, random

root = os.path.join(os.path.dirname(__file__), "..", "data", "core")
path = os.path.join(root, "scenarios.json")
existing = {s["id"]: s for s in json.load(open(path, encoding="utf8"))}
rnd = random.Random(7)

def km(a, b):
    mid = math.radians((a[1] + b[1]) / 2)
    return math.hypot((b[0] - a[0]) * 111 * math.cos(mid), (b[1] - a[1]) * 111)

def RK(r): return 40 + 80 * r + max(0, r - 8) * 400  # must match Pacing.StrikeReachKm
REACH = {"emad": RK(10), "ghadr": RK(10), "kheibar-shekan": RK(10), "shahed-136": RK(10), "paveh": RK(10)}
SPEED = {"emad": 10, "ghadr": 10, "kheibar-shekan": 10, "shahed-136": 1, "paveh": 3}

def site(i, n, side, lon, lat, cls="military-base"):
    typ = "" if cls != "military-base" else "navalbase" if i.endswith("-nb") else "garrison" if i.endswith("-gs") else "airbase"
    return {"id": i, "name": n, "side": side, "class": cls, "lon": lon, "lat": lat, "type": typ}

def battery(i, n, side, lon, lat, radar, launcher, interceptor, stock, platform="ground"):
    return {"id": i, "name": n, "side": side, "lon": lon, "lat": lat, "radar": radar, "launcher": launcher, "interceptor": interceptor, "stock": stock, "platform": platform}

units = []
def add(side, kind, preset, name, n, base, centre, spread, patrol):
    for i in range(1, n + 1):
        lon = centre[0] + rnd.uniform(-spread, spread); lat = centre[1] + rnd.uniform(-spread, spread) * 0.8
        units.append({"id": f"{side}-{preset.lower()}-{len([u for u in units if u['preset'] == preset]) + 1:02d}", "name": f"{name} #{i:02d}",
                      "side": side, "kind": kind, "preset": preset, "lon": round(lon, 3), "lat": round(lat, 3), "patrolKm": patrol, "base": base})

def F(preset, count, mission, formation="Wedge"): return {"preset": preset, "count": count, "mission": mission, "formation": formation}
def cap(side, name, lon, lat, flights, dur=9000): return {"side": side, "name": name, "stationLon": lon, "stationLat": lat, "start": 0, "duration": dur, "flights": flights}
def pkg(side, name, target, tot, flights, ip=80): return {"side": side, "name": name, "target": target, "tot": tot, "ipKm": ip, "flights": flights}

il_bases = [site("nevatim-ab", "Nevatim Air Base", "blue", 35.01, 31.21), site("ramon-ab", "Ramon Air Base", "blue", 34.67, 30.78),
    site("hatzerim-ab", "Hatzerim Air Base", "blue", 34.66, 31.23), site("telnof-ab", "Tel Nof Air Base", "blue", 34.82, 31.84),
    site("ramatdavid-ab", "Ramat David Air Base", "blue", 35.18, 32.67), site("palmachim-ab", "Palmachim Air Base", "blue", 34.69, 31.90),
    site("hatzor-ab", "Hatzor Air Base", "blue", 34.73, 31.76), site("haifa-nb", "Haifa naval base", "blue", 35.00, 32.83)]
ir_bases = [site("tabriz-ab", "Tabriz Air Base", "red", 46.24, 38.13), site("hamadan-ab", "Hamadan Air Base", "red", 48.65, 35.21),
    site("dezful-ab", "Dezful Air Base", "red", 48.40, 32.43), site("isfahan-ab", "Isfahan Air Base", "red", 51.87, 32.75),
    site("shiraz-ab", "Shiraz Air Base", "red", 52.59, 29.54), site("bandarabbas-nb", "Bandar Abbas naval base", "red", 56.33, 27.15),
    site("kermanshah-gs", "Kermanshah missile base", "red", 47.15, 34.40), site("khorramabad-gs", "Khorramabad missile base", "red", 48.40, 33.55)]
cities = [site("tel-aviv", "Tel Aviv", "blue", 34.78, 32.08, "population-centre"), site("jerusalem", "Jerusalem", "blue", 35.21, 31.77, "population-centre"),
    site("haifa", "Haifa", "blue", 34.99, 32.79, "population-centre"), site("beersheba", "Beersheba", "blue", 34.79, 31.25, "population-centre"),
    site("tehran", "Tehran", "red", 51.39, 35.69, "population-centre"), site("tabriz", "Tabriz", "red", 46.29, 38.08, "population-centre"),
    site("isfahan", "Isfahan", "red", 51.67, 32.65, "population-centre"), site("kermanshah", "Kermanshah", "red", 47.07, 34.31, "population-centre"),
    site("shiraz", "Shiraz", "red", 52.53, 29.59, "population-centre")]
B = {b["id"]: b for b in il_bases + ir_bases}
def near(b, dx, dy): return B[b]["lon"] + dx, B[b]["lat"] + dy

batteries = [
    battery("arrow3-palmachim", "Arrow 3 battery (Palmachim)", "blue", *near("palmachim-ab", 0.04, 0.03), "green-pine", "gs-launcher", "arrow-3", 4),
    battery("arrow3-negev", "Arrow 3 battery (Negev)", "blue", *near("nevatim-ab", -0.1, -0.2), "green-pine", "gs-launcher", "arrow-3", 4),
    battery("arrow2-hatzerim", "Arrow 2 battery (Hatzerim)", "blue", *near("hatzerim-ab", 0.04, 0.03), "green-pine", "gs-launcher", "arrow-2", 6),
    battery("arrow2-ramatdavid", "Arrow 2 battery (Ramat David)", "blue", *near("ramatdavid-ab", 0.04, -0.03), "green-pine", "gs-launcher", "arrow-2", 6),
    battery("ds-telnof", "David's Sling battery (Tel Nof)", "blue", *near("telnof-ab", 0.04, 0.02), "elm-2084", "gs-launcher", "davids-sling", 8),
    battery("ds-haifa", "David's Sling battery (Haifa)", "blue", *near("haifa-nb", 0.04, -0.03), "elm-2084", "gs-launcher", "davids-sling", 8)]
batteries += [battery(f"idome-{b}", f"Iron Dome ({B[b]['name'].split()[0]} {B[b]['name'].split()[1] if B[b]['name'].split()[1] not in ('Air', 'naval') else ''})".replace(" )", ")"), "blue", *near(b, -0.04, 0.03), "elm-2084", "gs-launcher", "iron-dome", 20)
              for b in ["nevatim-ab", "ramon-ab", "hatzerim-ab", "telnof-ab", "ramatdavid-ab", "haifa-nb"]]
batteries += [battery("bavar-isfahan", "Bavar-373 battery (Isfahan)", "red", *near("isfahan-ab", 0.05, 0.03), "bavar-radar", "ir-tel", "bavar-373", 16),
    battery("s300-hamadan", "S-300PMU-2 battery (Hamadan)", "red", *near("hamadan-ab", -0.04, 0.03), "s-300-radar", "ir-tel", "s-300pmu2", 16),
    battery("s300-bandarabbas", "S-300PMU-2 battery (Bandar Abbas)", "red", *near("bandarabbas-nb", 0.04, 0.05), "s-300-radar", "ir-tel", "s-300pmu2", 16)]
batteries += [battery(f"khordad-{b}", f"Khordad-15 battery ({B[b]['name'].split()[0]})", "red", *near(b, 0.05, -0.03), "bavar-radar", "ir-tel", "khordad-15", 12)
              for b in ["kermanshah-gs", "khorramabad-gs", "dezful-ab", "tabriz-ab", "shiraz-ab"]]

launchers = [
    {"id": "irgc-kermanshah", "name": "IRGC missile brigade (Kermanshah)", "side": "red", "lon": 47.12, "lat": 34.44, "missile": "emad", "stock": 12},
    {"id": "irgc-khorramabad", "name": "IRGC missile brigade (Khorramabad)", "side": "red", "lon": 48.36, "lat": 33.58, "missile": "kheibar-shekan", "stock": 12},
    {"id": "irgc-tabriz", "name": "IRGC missile brigade (Tabriz)", "side": "red", "lon": 46.30, "lat": 38.10, "missile": "ghadr", "stock": 12},
    {"id": "irgc-drones", "name": "Drone launch site (Hamadan)", "side": "red", "lon": 48.70, "lat": 35.24, "missile": "shahed-136", "stock": 32},
    {"id": "irgc-cruise", "name": "Cruise missile unit (Dezful)", "side": "red", "lon": 48.45, "lat": 32.40, "missile": "paveh", "stock": 8}]

# Israel (Blue)
add("blue", "air", "ILF35I", "F-35I Adir", 12, "nevatim-ab", (35.3, 31.4), 0.4, 50)
add("blue", "air", "ILF15I", "F-15I Ra'am", 10, "hatzerim-ab", (35.0, 31.4), 0.4, 50)
add("blue", "air", "ILF16I", "F-16I Sufa", 8, "ramon-ab", (35.2, 30.9), 0.4, 50)
add("blue", "air", "ILF16I", "F-16I Sufa", 8, "ramatdavid-ab", (35.4, 32.6), 0.4, 50)
add("blue", "air", "ILKC707", "Re'em tanker", 3, "nevatim-ab", (35.5, 31.3), 0.3, 40)
add("blue", "aew", "ILG550", "G550 Eitam", 2, "nevatim-ab", (34.9, 31.6), 0.3, 40)
add("blue", "ship", "ILSAAR6", "Sa'ar 6 corvette", 4, "haifa-nb", (34.4, 32.6), 0.5, 15)
add("blue", "sub", "ILDOLPHIN", "Dolphin-class", 2, "haifa-nb", (34.0, 32.2), 0.8, 10)
# Iran (Red)
add("red", "air", "IRF14AM", "F-14AM Tomcat", 12, "isfahan-ab", (50.5, 33.5), 0.8, 60)
add("red", "air", "IRMIG29", "MiG-29A", 10, "tabriz-ab", (46.8, 37.2), 0.6, 60)
add("red", "air", "IRF4E", "F-4E Phantom II", 8, "hamadan-ab", (48.0, 34.6), 0.6, 60)
add("red", "air", "IRF4E", "F-4E Phantom II", 8, "bandarabbas-nb", (56.0, 27.6), 0.5, 60)
add("red", "air", "IRSU24", "Su-24MK", 8, "shiraz-ab", (52.2, 29.9), 0.5, 60)
add("red", "air", "IRMIG29", "MiG-29A", 6, "dezful-ab", (48.2, 32.7), 0.5, 60)
add("red", "ship", "IRJAMARAN", "Moudge-class frigate", 3, "bandarabbas-nb", (56.3, 26.8), 0.4, 15)
add("red", "sub", "IRKILO", "Kilo-class", 3, "bandarabbas-nb", (56.8, 26.5), 0.6, 10)

# Iran's plan: slow drones and cruise missiles first, ballistic salvos timed to land with them (TOT 90:00),
# after two earlier ballistic raids.
fires = []
def fire(l, tgt, n, tot, aim=""):
    L = next(x for x in launchers if x["id"] == l)
    d = km((L["lon"], L["lat"]), (B[tgt]["lon"], B[tgt]["lat"]))
    assert d <= REACH[L["missile"]], (l, tgt, d)
    flight = d / (0.25 * SPEED[L["missile"]])
    fires.append({"side": L["side"], "launcher": l, "target": tgt, "count": n, "tot": round(max(tot, flight + 90)), "aimpoint": aim})
for t in ["nevatim-ab", "ramon-ab", "hatzerim-ab", "telnof-ab", "ramatdavid-ab", "nevatim-ab"]: fire("irgc-drones", t, 4, 5400)
fire("irgc-cruise", "ramatdavid-ab", 4, 5400); fire("irgc-cruise", "haifa-nb", 4, 5400)
fire("irgc-kermanshah", "ramon-ab", 2, 900, "runway"); fire("irgc-kermanshah", "nevatim-ab", 4, 5400, "shelters"); fire("irgc-kermanshah", "nevatim-ab", 4, 5460, "runway")
fire("irgc-khorramabad", "hatzerim-ab", 2, 2400); fire("irgc-khorramabad", "telnof-ab", 4, 5400); fire("irgc-khorramabad", "palmachim-ab", 4, 5460)
fire("irgc-tabriz", "palmachim-ab", 2, 3000); fire("irgc-tabriz", "ramatdavid-ab", 4, 5400); fire("irgc-tabriz", "haifa-nb", 4, 5460)

ato = [
    cap("blue", "Negev CAP", 35.7, 31.0, [F("ILF16I", 2, "CAP", "LineAbreast")]),
    cap("blue", "Northern CAP", 35.6, 32.9, [F("ILF16I", 2, "CAP", "LineAbreast")]),
    cap("blue", "Eastern barrier CAP", 38.5, 32.0, [F("ILF15I", 2, "CAP", "Wedge")]),
    cap("blue", "AEW orbit", 35.1, 31.7, [F("ILG550", 1, "AEW", "Trail")]),
    pkg("blue", "Package OPAL", "kermanshah-gs", 6300, [F("ILF35I", 4, "Strike", "Box"), F("ILF35I", 2, "Escort", "Wedge"), F("ILF35I", 2, "SEAD", "Trail"), F("ILKC707", 1, "Tanker", "Trail")]),
    pkg("blue", "Package ONYX", "hamadan-ab", 6600, [F("ILF15I", 4, "Strike", "Box"), F("ILF15I", 2, "SEAD", "Trail"), F("ILF15I", 2, "Escort", "Wedge"), F("ILKC707", 1, "Tanker", "Trail")]),
    pkg("blue", "Package JASPER", "khorramabad-gs", 7200, [F("ILF35I", 2, "Strike", "Trail"), F("ILKC707", 1, "Tanker", "Trail")], ip=60),
    cap("red", "Kermanshah CAP", 47.6, 34.1, [F("IRF14AM", 2, "CAP", "LineAbreast")]),
    cap("red", "Hamadan CAP", 48.3, 34.9, [F("IRF4E", 2, "CAP", "Wedge")]),
    cap("red", "Tabriz CAP", 46.8, 37.4, [F("IRMIG29", 2, "CAP", "LineAbreast")]),
    cap("red", "Dezful CAP", 48.0, 32.8, [F("IRMIG29", 2, "CAP", "Wedge")]),
] + fires

existing["isr-irn"] = {"id": "isr-irn", "name": "Middle East: Israel vs Iran", "theatre": "GULF",
    "brief": "Test conflict between Israel (Blue) and Iran (Red). Iran sends drones and cruise missiles first, then times ballistic salvos to arrive with them; Israel's layered air defence (Arrow 3, Arrow 2, David's Sling, Iron Dome) defends, then Israel strikes missile bases with tanker-supported packages. Cities are protected and never targeted; nuclear forces stay abstract.",
    "center": [43.5, 32.8], "zoom": 1.45, "sides": {"blue": "Israel", "red": "Iran"},
    "sites": il_bases + ir_bases + cities, "batteries": batteries, "launchers": launchers, "script": [], "units": units, "ato": ato}
json.dump(list(existing.values()), open(path, "w", encoding="utf8"), indent=1, ensure_ascii=False)
print(f"isr-irn: {len(units)} units + {len(batteries)} batteries + {len(launchers)} launchers, {len(il_bases)}+{len(ir_bases)} bases, {len(fires)} fire missions")
