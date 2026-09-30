"""Builds data/core/world_oob.json: the one world order of battle (every base, unit, battery, launcher, depot, supply
route and neutral sea lane), keyed by nation instead of scenario side.

Source: the theatre scenarios in data/core/scenarios.json (run tools/logistics-build.py first so depots, routes and
terrain factors exist). Each scenario side becomes its nation; ids that collide between scenarios (depots, routes)
are prefixed with the nation. Scripts and opening ATO lines are dropped: in the persistent world the commanders
decide. Sandbox scenarios without a theatre are left alone. Game content, no new real-world data.
"""
import json, os
CORE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data", "core")

nations = json.load(open(os.path.join(CORE, "nations.json"), encoding="utf-8-sig"))
nations = nations if isinstance(nations, list) else nations["nations"]
by_name = {n["name"]: n["id"] for n in nations} | {n["id"]: n["id"] for n in nations}
S = json.load(open(os.path.join(CORE, "scenarios.json"), encoding="utf-8-sig"))

world = {"id": "world", "name": "World", "brief": "The persistent world order of battle.", "sites": [], "batteries": [], "launchers": [], "units": [],
         "logistics": {"nodes": {}, "corridors": []}, "neutral": []}
seen = {k: set() for k in ("sites", "batteries", "launchers", "units")}
for sc in S:
    if not sc.get("theatre"): continue
    nat = {side: by_name[name] for side, name in sc["sides"].items()}
    ren = {}
    for s in sc["sites"]:
        if s.get("type") == "depot": ren[s["id"]] = f"{nat[s['side']].lower()}-{s['id'].split('-', 1)[1]}"
    def rid(x): return ren.get(x, x)
    for key in ("sites", "batteries", "launchers", "units"):
        for item in sc.get(key, []):
            item = dict(item, side=nat[item["side"]])
            item["id"] = rid(item["id"])
            if "base" in item: item["base"] = rid(item["base"])
            if item["id"] in seen[key]: continue
            seen[key].add(item["id"]); world[key].append(item)
    L = sc.get("logistics") or {}
    for nid, tr in (L.get("nodes") or {}).items(): world["logistics"]["nodes"][rid(nid)] = tr
    for c in L.get("corridors") or []:
        n = nat[c["side"]]
        world["logistics"]["corridors"].append(dict(c, id=f"{n.lower()}-{c['id']}", side=n, a=rid(c["a"]), b=rid(c["b"])))
    world["neutral"] += sc.get("neutral", [])

json.dump(world, open(os.path.join(CORE, "world_oob.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print({k: len(world[k]) for k in ("sites", "batteries", "launchers", "units")}, "routes", len(world["logistics"]["corridors"]),
      "nations", sorted({s["side"] for s in world["sites"]}))
