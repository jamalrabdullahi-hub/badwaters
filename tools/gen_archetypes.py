"""Generates data/core/component_archetypes.json: designable component types for the component designer.
Each archetype has base game stats and parameters (sliders with linear/size effects, choices with
multipliers). All values are game tuning. Run: python tools/gen_archetypes.py"""
import json, os
root = os.path.join(os.path.dirname(__file__), "..", "data", "core")

def CH(i, name, mul=None, add=None, adds=None, cost=1.0, note=""):
    return {"id": i, "name": name, "usesMul": mul or {}, "usesAdd": add or {}, "adds": adds or {}, "costMul": cost, "note": note}
def SL(i, name, lo, hi, default, step, uses=None, adds=None, cost=0, scale=None, tech=""):
    return {"id": i, "name": name, "kind": "slider", "min": lo, "max": hi, "default": default, "step": step, "uses": uses or {}, "adds": adds or {}, "cost": cost, "scaleUses": scale or {}, "techField": tech}
def CHOICE(i, name, choices, default): return {"id": i, "name": name, "kind": "choice", "choices": choices, "defaultChoice": default}
def ARCH(i, name, family, fits, uses, adds, req, cost, visual, params, factory=None, port=None, offers=None, category="Custom", topside=False):
    return {"id": i, "name": name, "family": family, "fitsKinds": fits, "uses": uses, "adds": adds, "requires": req, "cost": cost, "factory": factory,
            "port": port, "offers": offers or [], "visual": visual, "category": category, "topside": topside, "params": params}

TECH = lambda field: SL("tech", "Technology level", 1, 10, 5, 1, adds={"reliability": 0.2}, cost=0.2, tech=field)

A = [
    ARCH("ac-engine", "Aircraft engine", "propulsion", ["aircraft"], {"mass": 3, "volume": 2, "power": -6}, {"speed": 6, "endurance": 5, "agility": 5}, {"jet-engines": 3}, 20, "ac-engine", [
        SL("size", "Size", 0.6, 1.6, 1, 0.05, adds={"speed": 2, "endurance": 1, "agility": 1}, cost=20, scale={"mass": 2, "volume": 2, "power": 1}),
        CHOICE("type", "Type", [
            CH("turbojet", "Turbojet", {"mass": 0.9}, adds={"speed": 1, "endurance": -2}, cost=0.8),
            CH("lbtf", "Low-bypass turbofan"),
            CH("hbtf", "High-bypass turbofan", {"mass": 1.3}, adds={"speed": -2, "endurance": 2, "signature": -0.5}),
            CH("turboprop", "Turboprop", {"mass": 0.8}, adds={"speed": -3, "endurance": 3, "agility": -1, "field": 1}, cost=0.6),
            CH("propfan", "Propfan", {"mass": 1.1}, adds={"speed": -2, "endurance": 3}, cost=0.9),
            CH("ramjet", "Ramjet", {"mass": 0.7}, adds={"speed": 3, "endurance": -3, "agility": -2, "field": -2}, cost=1.5),
            CH("scramjet", "Scramjet", {"mass": 0.8}, {"cooling": 2}, adds={"speed": 4, "endurance": -4, "agility": -3, "field": -3}, cost=3),
            CH("electric", "Electric", {"mass": 1.2}, adds={"speed": -4, "endurance": -1, "signature": 1}, cost=0.7)], "lbtf"),
        SL("bypass", "Bypass ratio", 0, 8, 1, 0.5, uses={"mass": 0.15, "volume": 0.2}, adds={"endurance": 0.4, "speed": -0.3}),
        SL("spools", "Spools / fan stages", 1, 3, 2, 1, uses={"mass": 0.3}, adds={"endurance": 0.5, "reliability": -0.3}, cost=5),
        CHOICE("ab", "Afterburner", [CH("none", "None", {"mass": 0.9}, adds={"speed": -1, "signature": 0.5}), CH("std", "Afterburner"),
                                     CH("adaptive", "Adaptive cycle", {"mass": 1.1}, adds={"speed": 0.5, "endurance": 1}, cost=1.6)], "std"),
        CHOICE("hot", "Hot-section materials", [CH("nickel", "Nickel alloys"), CH("cmc", "Ceramic matrix", {"mass": 0.85}, adds={"speed": 0.5, "endurance": 0.5}, cost=1.5),
                                                CH("cheap", "Low-cost", {"mass": 1.1}, adds={"speed": -0.5, "reliability": -0.5}, cost=0.7)], "nickel"),
        TECH("jet-engines")], "aerospace-assembly", category="Custom engines"),
    ARCH("ship-plant", "Ship propulsion plant", "propulsion", ["ship"], {"mass": 400, "volume": 6, "crew": 20}, {"speed": 7, "endurance": 3}, {"gas-turbines": 3}, 90, "funnel-twin", [
        SL("power", "Installed power", 0.5, 2, 1, 0.05, adds={"speed": 2}, cost=60, scale={"mass": 1, "volume": 1, "crew": 0.5}),
        CHOICE("type", "Plant type", [CH("diesel", "Diesel", {"mass": 0.9}, adds={"speed": -1, "endurance": 2}, cost=0.6),
                                      CH("codag", "CODAG", adds={"endurance": 1}), CH("cogag", "COGAG", adds={"speed": 0.5}, cost=1.1),
                                      CH("iep", "Integrated electric", {"mass": 1.15}, adds={"endurance": 1, "signature": 0.5}, cost=1.5),
                                      CH("nuclear", "Nuclear (abstract)", {"mass": 2.2, "crew": 1.5}, adds={"endurance": 5, "speed": 1}, cost=4)], "codag"),
        SL("shafts", "Shafts", 1, 4, 2, 1, uses={"mass": 30, "volume": 0.5}, adds={"survivability": 0.5, "speed": 0.2}, cost=5),
        CHOICE("quiet", "Quieting", [CH("std", "Standard"), CH("rafted", "Rafted mounts", {"mass": 1.1}, adds={"signature": 1, "anti-sub": 0.5}, cost=1.3)], "std"),
        TECH("gas-turbines")], "shipyard", category="Custom propulsion"),
    ARCH("generator", "Power generator", "power", ["ship", "ground"], {"mass": 60, "volume": 2, "power": -12}, {}, {"power-systems": 2}, 10, "g-gen-large", [
        SL("output", "Output", 0.5, 3, 1, 0.1, cost=10, scale={"mass": 0.9, "volume": 0.8, "power": 1}),
        CHOICE("type", "Type", [CH("diesel", "Diesel"), CH("gt", "Gas turbine", {"mass": 0.8}, cost=1.4),
                                CH("fuelcell", "Fuel cell", {"mass": 1.2}, adds={"signature": 0.5}, cost=2)], "diesel"),
        SL("redundancy", "Redundancy", 0, 3, 0, 1, uses={"mass": 15, "volume": 0.3}, adds={"reliability": 1, "survivability": 0.3}, cost=4),
        TECH("power-systems")], "shipyard", category="Custom power"),
    ARCH("radar-array", "Radar array", "sensors", ["ship", "ground"], {"mass": 50, "power": 16, "cooling": 8}, {"detection": 7, "fire-channels": 6}, {"radar": 6}, 220, "rad-array-4face", [
        SL("faces", "Faces", 1, 4, 4, 1, uses={"mass": 10, "power": 3, "cooling": 1.5}, adds={"fire-channels": 1.5, "detection": 0.3}, cost=40),
        SL("aperture", "Aperture size", 0.6, 2, 1, 0.05, adds={"detection": 2}, cost=80, scale={"mass": 2, "power": 1.5, "cooling": 1.5}),
        CHOICE("band", "Band", [CH("s", "S band"), CH("x", "X band", adds={"fire-channels": 1, "detection": -0.5}),
                                CH("dual", "Dual band", {"mass": 1.3, "power": 1.3}, adds={"detection": 1, "fire-channels": 2}, cost=1.5)], "s"),
        CHOICE("cooling", "Cooling", [CH("air", "Air cooled", {"cooling": 0.8}, adds={"detection": -0.5}, cost=0.8), CH("liquid", "Liquid cooled")], "liquid"),
        SL("hardening", "Hardening", 0, 3, 0, 1, uses={"mass": 8}, adds={"survivability": 0.5}, cost=10),
        TECH("radar")], "radar-factory", "mast-sensor", ["fire-control"], "Custom sensors", True),
    ARCH("radome", "Aircraft radar", "sensors", ["aircraft"], {"mass": 0.3, "power": 4, "cooling": 3}, {"detection": 7, "fire-channels": 4}, {"radar": 5}, 30, "ac-radar-aesa", [
        SL("aperture", "Aperture size", 0.6, 1.5, 1, 0.05, adds={"detection": 2, "fire-channels": 1}, cost=20, scale={"mass": 2, "power": 1.5, "cooling": 1.5}),
        CHOICE("shape", "Radome shape", [CH("ogive", "Ogive"), CH("hemi", "Hemispherical", adds={"detection": 0.5, "signature": -0.5, "speed": -0.5}),
                                         CH("faceted", "Faceted low-signature", adds={"detection": -0.5, "signature": 1}, cost=1.3),
                                         CH("conformal", "Conformal", adds={"detection": -1, "signature": 1.5}, cost=1.5)], "ogive"),
        CHOICE("type", "Array type", [CH("mech", "Mechanically scanned", {"mass": 1.2}, adds={"detection": -1, "fire-channels": -2}, cost=0.5),
                                      CH("pesa", "Passive array", adds={"fire-channels": -1}, cost=0.8), CH("aesa", "Active array")], "aesa"),
        TECH("radar")], "avionics-factory", category="Custom sensors"),
    ARCH("launch-block", "Launch cell block", "interfaces", ["ship", "ground"], {"mass": 30, "volume": 2}, {}, {"structures": 4}, 12, "vls-8", [
        SL("cells", "Cells", 4, 64, 8, 4, adds={"strike": 0.08, "air-defence": 0.08}, cost=1.5, scale={"mass": 1, "volume": 0.7}),
        CHOICE("length", "Cell length", [CH("short", "Short (self-defence)", {"mass": 0.6, "volume": 0.6}, adds={"strike": -0.5}, cost=0.7),
                                          CH("tactical", "Tactical"), CH("strike", "Strike length", {"mass": 1.4, "volume": 1.4}, adds={"strike": 0.5}, cost=1.3)], "tactical"),
        CHOICE("hatch", "Hatches", [CH("std", "Standard"), CH("lo", "Low-signature", adds={"signature": 0.5}, cost=1.2),
                                    CH("armoured", "Armoured", {"mass": 1.2}, adds={"survivability": 0.5}, cost=1.2)], "std"),
        TECH("structures")], "shipyard", "cell-block", ["launch-cell"], "Custom launchers"),
    ARCH("cooling-plant", "Cooling plant", "thermal", ["ship", "ground"], {"mass": 20, "power": 2, "cooling": -12}, {}, {"thermal": 3}, 15, "vent-l", [
        SL("capacity", "Capacity", 0.5, 3, 1, 0.1, cost=15, scale={"mass": 0.9, "power": 0.9, "cooling": 1}),
        CHOICE("type", "Type", [CH("air", "Forced air", {"cooling": 0.7}, cost=0.6), CH("liquid", "Chilled water"),
                                CH("twophase", "Two-phase", {"mass": 0.9, "cooling": 1.3}, cost=1.6)], "liquid"),
        TECH("thermal")], "shipyard", category="Custom thermal"),
    ARCH("sonar", "Sonar", "sensors", ["ship"], {"mass": 20, "power": 0.5, "cooling": 0.3}, {"anti-sub": 2}, {"sonar": 3}, 12, "sonar-bow", [
        SL("aperture", "Array size", 0.6, 2, 1, 0.05, adds={"anti-sub": 1.5}, cost=10, scale={"mass": 1.8, "power": 1.2}),
        CHOICE("mode", "Mode", [CH("passive", "Passive", adds={"signature": 0.5}, cost=0.8), CH("active", "Active / passive"),
                                CH("lf", "Low-frequency active", {"mass": 1.3, "power": 1.5}, adds={"anti-sub": 1, "signature": -0.5}, cost=1.4)], "active"),
        CHOICE("mount", "Mounting", [CH("hull", "Hull"), CH("towed", "Towed", {"mass": 1.4}, adds={"anti-sub": 1.5}, cost=1.5),
                                     CH("vds", "Variable depth", {"mass": 1.6, "power": 1.3}, adds={"anti-sub": 2}, cost=1.8)], "hull"),
        TECH("sonar")], "shipyard", category="Custom sensors"),
    ARCH("datalink", "Datalink", "comms", ["ship", "aircraft", "ground"], {"mass": 0.1, "power": 1, "data": -4}, {"networking": 4}, {"datalinks": 4}, 3, "datalink-ant", [
        SL("bandwidth", "Bandwidth", 1, 4, 1, 0.5, uses={"data": -2, "power": 0.5}, adds={"networking": 0.8}, cost=2),
        CHOICE("security", "Security", [CH("std", "Standard"), CH("lpi", "Low probability of intercept", adds={"signature": 0.5}, cost=1.4),
                                        CH("ajam", "Anti-jam", {"power": 1.2}, adds={"jam-resistance": 2}, cost=1.5)], "std"),
        TECH("datalinks")], None, category="Custom comms"),
]
json.dump(A, open(os.path.join(root, "component_archetypes.json"), "w", encoding="utf8"), indent=1, ensure_ascii=False)
print(f"{len(A)} archetypes")
