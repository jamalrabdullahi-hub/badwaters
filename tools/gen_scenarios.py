"""Generates data/core/scenarios.json. Positions are approximate public map locations; all performance
is game ratings from designs.json / presets.json. Run: python tools/gen_scenarios.py"""
import json, math, os, random
root = os.path.join(os.path.dirname(__file__), "..", "data", "core")
path = os.path.join(root, "scenarios.json")
existing = {s["id"]: s for s in json.load(open(path, encoding="utf8"))} if os.path.exists(path) else {}
rnd = random.Random(42)

def km(a, b):
    mid = math.radians((a[1] + b[1]) / 2)
    return math.hypot((b[0] - a[0]) * 111 * math.cos(mid), (b[1] - a[1]) * 111)

# game reach tables (must match Sim/Theatre/Profiles.cs Pacing.StrikeReachKm with the design's reach rating)
REACH = {"df-15b": 40 + 80 * 7, "df-17": 40 + 80 * 10, "type-12": 40 + 80 * 8}

def site(i, n, side, lon, lat, cls="military-base"): return {"id": i, "name": n, "side": side, "class": cls, "lon": lon, "lat": lat}

blue_bases = [site("naha-ab", "Naha Air Base", "blue", 127.65, 26.20), site("nyutabaru-ab", "Nyutabaru Air Base", "blue", 131.45, 32.08),
    site("tsuiki-ab", "Tsuiki Air Base", "blue", 131.04, 33.68), site("kanoya-ab", "Kanoya Air Base", "blue", 130.84, 31.37),
    site("sasebo-nb", "Sasebo Naval Base", "blue", 129.72, 33.16), site("amami-gs", "Amami garrison", "blue", 129.49, 28.39),
    site("miyako-gs", "Miyako garrison", "blue", 125.30, 24.78), site("ishigaki-gs", "Ishigaki garrison", "blue", 124.18, 24.40)]
red_bases = [site("longtian-ab", "Longtian Air Base", "red", 119.66, 25.68), site("shuimen-ab", "Shuimen Air Base", "red", 120.08, 26.99),
    site("luqiao-ab", "Luqiao Air Base", "red", 121.43, 28.56), site("jinjiang-ab", "Jinjiang Air Base", "red", 118.59, 24.80),
    site("zhangzhou-ab", "Zhangzhou Air Base", "red", 117.60, 24.55), site("wuyishan-ab", "Wuyishan Air Base", "red", 118.00, 27.70),
    site("ningbo-nb", "Ningbo naval base", "red", 121.85, 29.95), site("daishan-ab", "Daishan Air Base", "red", 122.20, 30.28)]
cities = [site("naha-city", "Naha", "blue", 127.72, 26.24, "population-centre"), site("kagoshima", "Kagoshima", "blue", 130.56, 31.60, "population-centre"),
    site("fukuoka", "Fukuoka", "blue", 130.40, 33.59, "population-centre"), site("nagasaki", "Nagasaki", "blue", 129.87, 32.75, "population-centre"),
    site("shanghai", "Shanghai", "red", 121.47, 31.23, "population-centre"), site("fuzhou", "Fuzhou", "red", 119.30, 26.08, "population-centre"),
    site("xiamen", "Xiamen", "red", 118.09, 24.48, "population-centre"), site("wenzhou", "Wenzhou", "red", 120.70, 28.00, "population-centre"),
    site("ningbo", "Ningbo", "red", 121.55, 29.87, "population-centre")]
bases = {b["id"]: b for b in blue_bases + red_bases}

def battery(i, n, side, lon, lat, radar, launcher, interceptor, stock):
    return {"id": i, "name": n, "side": side, "lon": lon, "lat": lat, "radar": radar, "launcher": launcher, "interceptor": interceptor, "stock": stock}

batteries = [battery(f"pac3-{b}", f"PAC-3 battery ({bases[b]['name'].split()[0]})", "blue", bases[b]["lon"] + 0.05, bases[b]["lat"] - 0.04, "gs-radar", "gs-launcher", "sam-hit", 16)
             for b in ["naha-ab", "nyutabaru-ab", "tsuiki-ab", "kanoya-ab", "sasebo-nb", "miyako-gs"]]
batteries += [battery(f"maya-{k}", f"JS {n} (Maya-class)" if k < 2 else f"JS {n} (Kongo-class)", "blue", lon, lat, "maya-ddg", "maya-ddg", "sm-2", 24)
              for k, (n, lon, lat) in enumerate([("Maya", 126.2, 27.0), ("Haguro", 128.3, 29.5), ("Kirishima", 124.8, 25.9)])]
batteries += [battery(f"hq9-{b}", f"HQ-9B battery ({bases[b]['name'].split()[0]})", "red", bases[b]["lon"] - 0.05, bases[b]["lat"] + 0.04, "hq-9-radar", "hq-9-tel", "hq-9b", 16)
              for b in ["longtian-ab", "shuimen-ab", "luqiao-ab", "jinjiang-ab", "zhangzhou-ab", "ningbo-nb"]]
batteries += [battery(f"pla-ddg-{k}", n, "red", lon, lat, r, r, "hhq-9", 24)
              for k, (n, lon, lat, r) in enumerate([("Nanchang (Type 055)", 123.2, 28.5, "type-055"), ("Changsha (Type 052D)", 122.5, 26.5, "type-052d"), ("Hefei (Type 052D)", 121.9, 24.9, "type-052d")])]

launchers = [
    {"id": "rf-ningde", "name": "Rocket Force brigade (Ningde)", "side": "red", "lon": 119.5, "lat": 26.9, "missile": "df-17", "stock": 12},
    {"id": "rf-putian", "name": "Rocket Force brigade (Putian)", "side": "red", "lon": 119.2, "lat": 25.4, "missile": "df-15b", "stock": 12},
    {"id": "rf-wenzhou", "name": "Rocket Force brigade (Wenzhou)", "side": "red", "lon": 120.4, "lat": 27.9, "missile": "df-17", "stock": 12},
    {"id": "ssm-ishigaki", "name": "Type 12 regiment (Ishigaki)", "side": "blue", "lon": 124.10, "lat": 24.47, "missile": "type-12", "stock": 12},
    {"id": "ssm-miyako", "name": "Type 12 regiment (Miyako)", "side": "blue", "lon": 125.22, "lat": 24.83, "missile": "type-12", "stock": 12},
    {"id": "ssm-amami", "name": "Type 12 regiment (Amami)", "side": "blue", "lon": 129.40, "lat": 28.33, "missile": "type-12", "stock": 12}]

# scripted salvos: each launcher fires pairs at the enemy bases it can reach, round robin
script = []
for l in launchers:
    enemy = [b for b in (red_bases if l["side"] == "blue" else blue_bases) if km((l["lon"], l["lat"]), (b["lon"], b["lat"])) <= REACH[l["missile"]] - 5]
    if not enemy: continue
    times = [30, 150, 300, 450, 600, 750] if l["side"] == "red" else [90, 240, 420, 600, 780, 900]
    for k, t in enumerate(times):
        script.append({"t": t + rnd.randint(0, 20), "launcher": l["id"], "target": enemy[k % len(enemy)]["id"], "count": 2})
script.sort(key=lambda s: s["t"])

units = []
def add(side, kind, preset, name, n, base, centre, spread, patrol):
    for i in range(1, n + 1):
        lon = centre[0] + rnd.uniform(-spread, spread); lat = centre[1] + rnd.uniform(-spread, spread) * 0.8
        units.append({"id": f"{side}-{preset.lower()}-{len([u for u in units if u['preset'] == preset]) + 1:02d}", "name": f"{name} #{i:02d}",
                      "side": side, "kind": kind, "preset": preset, "lon": round(lon, 3), "lat": round(lat, 3), "patrolKm": patrol, "base": base})

# Japan (Blue)
add("blue", "air", "JPF15J", "F-15J Eagle", 12, "naha-ab", (126.3, 26.4), 1.0, 60)
add("blue", "air", "USF35A", "F-35A Lightning II", 4, "nyutabaru-ab", (129.6, 30.6), 1.0, 60)
add("blue", "air", "USF35A", "F-35A Lightning II", 4, "naha-ab", (127.0, 26.6), 0.6, 60)
add("blue", "air", "JPF2A", "F-2A", 8, "naha-ab", (127.4, 26.8), 0.6, 60)
add("blue", "air", "JPKC46A", "KC-46A Pegasus", 2, "naha-ab", (127.8, 26.9), 0.3, 40)
add("blue", "air", "JPP1", "P-1 patrol", 4, "kanoya-ab", (127.5, 29.0), 1.5, 90)
add("blue", "aew", "JPE2D", "E-2D Hawkeye", 2, "naha-ab", (127.0, 27.3), 0.6, 50)
add("blue", "ship", "JPDDG-KONGO", "Kongo-class", 2, "sasebo-nb", (127.4, 28.3), 1.0, 15)
add("blue", "ship", "JPDD-AKIZUKI", "Akizuki-class", 4, "sasebo-nb", (126.6, 27.8), 1.2, 15)
add("blue", "ship", "JPDDH-183", "JS Kaga (Izumo-class)", 1, "sasebo-nb", (128.2, 28.0), 0.2, 12)
add("blue", "air", "USF35B", "F-35B Lightning II", 6, "sasebo-nb", (127.9, 28.2), 0.6, 45)
add("blue", "sub", "JPSS-TAIGEI", "Taigei-class", 4, "sasebo-nb", (125.5, 27.2), 1.5, 10)
# China (Red)
add("red", "air", "CNJ10C", "J-10C", 8, "longtian-ab", (121.0, 25.4), 0.8, 60)
add("red", "air", "CNJ16", "J-16", 8, "shuimen-ab", (121.8, 26.9), 0.8, 60)
add("red", "air", "CNJ20", "J-20", 6, "luqiao-ab", (123.0, 28.0), 0.9, 70)
add("red", "air", "CNJ11B", "J-11B", 8, "jinjiang-ab", (120.4, 24.4), 0.7, 60)
add("red", "air", "CNH6K", "H-6K", 6, "wuyishan-ab", (122.0, 29.0), 0.8, 80)
add("red", "aew", "CNKJ500", "KJ-500", 2, "shuimen-ab", (121.3, 27.6), 0.5, 50)
add("red", "air", "CNY20U", "Y-20U", 2, "wuyishan-ab", (119.0, 27.5), 0.3, 40)
add("red", "ship", "CNCV-001", "Liaoning", 1, "ningbo-nb", (124.2, 29.0), 0.2, 12)
add("red", "air", "CNJ15", "J-15", 8, "ningbo-nb", (124.4, 29.2), 0.6, 45)
add("red", "ship", "CNFFG-054A", "Type 054A", 4, "ningbo-nb", (123.0, 27.3), 1.2, 15)
add("red", "ship", "CNDDG-052D", "Type 052D", 2, "ningbo-nb", (123.8, 28.2), 0.8, 15)
add("red", "sub", "CNSSK-039A", "Type 039A", 4, "ningbo-nb", (123.9, 26.4), 1.5, 10)

def F(preset, count, mission, formation="Wedge"): return {"preset": preset, "count": count, "mission": mission, "formation": formation}
def cap(side, name, lon, lat, flights, dur=7200): return {"side": side, "name": name, "stationLon": lon, "stationLat": lat, "start": 0, "duration": dur, "flights": flights}
def pkg(side, name, target, tot, flights, ip=60): return {"side": side, "name": name, "target": target, "tot": tot, "ipKm": ip, "flights": flights}
ato = [
    cap("blue", "Naha CAP", 126.6, 26.4, [F("JPF15J", 2, "CAP", "LineAbreast")]),
    cap("blue", "Amami CAP", 128.3, 28.6, [F("JPF15J", 2, "CAP", "LineAbreast")]),
    cap("blue", "West Kyushu CAP", 128.8, 31.2, [F("USF35A", 2, "CAP", "Wedge")]),
    cap("blue", "AEW orbit", 127.2, 27.4, [F("JPE2D", 1, "AEW", "Trail")]),
    pkg("blue", "Package KATANA", "shuimen-ab", 3600, [F("JPF2A", 4, "Strike", "Box"), F("USF35A", 2, "Escort", "Wedge"), F("USF35A", 2, "SEAD", "Trail"), F("JPKC46A", 1, "Tanker", "Trail")]),
    cap("red", "Taiwan Strait CAP", 121.8, 25.8, [F("CNJ11B", 2, "CAP", "LineAbreast")]),
    cap("red", "Wenzhou CAP", 122.3, 27.2, [F("CNJ10C", 2, "CAP", "Wedge")]),
    cap("red", "East Sea CAP", 123.2, 28.8, [F("CNJ20", 2, "CAP", "Wedge")]),
    cap("red", "AEW orbit", 121.9, 27.4, [F("CNKJ500", 1, "AEW", "Trail")]),
    pkg("red", "Package TIANLONG", "naha-ab", 3000, [F("CNJ16", 4, "Strike", "Wedge"), F("CNJ20", 2, "Escort", "Wedge"), F("CNJ16", 2, "SEAD", "Trail")]),
    pkg("red", "Package HAILONG", "miyako-gs", 4500, [F("CNH6K", 2, "Strike", "Trail"), F("CNJ10C", 2, "Escort", "EchelonRight")], ip=80),
]
existing["jpn-chn"] = {"id": "jpn-chn", "name": "East China Sea: Japan vs China", "theatre": "ECS",
    "brief": "Test conflict in the East China Sea. China (Red) opens with Rocket Force salvos on Japanese bases; Japan (Blue) answers with Type 12 regiments. Both sides defend with land SAMs and air-defence ships. Cities are protected and never targeted.",
    "center": [125.2, 28.6], "zoom": 1.9, "sides": {"blue": "Japan", "red": "China"},
    "sites": blue_bases + red_bases + cities, "batteries": batteries, "launchers": launchers, "script": script, "units": units, "ato": ato}
json.dump(list(existing.values()), open(path, "w", encoding="utf8"), indent=1, ensure_ascii=False)
print(f"jpn-chn: {len(units)} units + {len(batteries)} batteries + {len(launchers)} launchers, {len(blue_bases)}+{len(red_bases)} bases, {len(script)} salvos")
