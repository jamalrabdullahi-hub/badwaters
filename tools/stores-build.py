"""Writes data/core/theatre/stores.json: aircraft stores, station layouts per airframe class and default loadouts.

Real store names identify them (see DISCLAIMER.md); every number is an abstract 0-10 game rating tuned for play,
not a real characteristic. Airframe classes come from the symbology mapping (FTR, STK, 5GEN, BMR, MPA).
"""
import json, os
HERE = os.path.dirname(os.path.abspath(__file__))
CORE = os.path.join(HERE, "..", "data", "core")

W = ["USA", "JPN", "ISR", "KOR", "GBR"]
US_ = ["USA", "JPN", "ISR", "KOR"]
S = []
def st(i, n, kind, cls, nations, punch=0, acc=0, air=0): S.append({"id": i, "name": n, "kind": kind, "cls": cls, "punch": punch, "accuracy": acc, "air": air, "nations": nations})
# air-to-air
st("aim-120", "AIM-120 AMRAAM", "aam", 1, W, air=8); st("aim-9x", "AIM-9X Sidewinder", "aam", 1, W, air=7)
st("aam-4b", "AAM-4B", "aam", 1, ["JPN"], air=8); st("meteor", "Meteor", "aam", 1, ["GBR", "FRA"], air=8); st("mica", "MICA", "aam", 1, ["FRA"], air=7)
st("python-5", "Python-5", "aam", 1, ["ISR"], air=7); st("pl-15", "PL-15", "aam", 1, ["CHN"], air=8); st("pl-10", "PL-10", "aam", 1, ["CHN"], air=7)
st("r-77", "R-77", "aam", 1, ["RUS"], air=7); st("r-73", "R-73", "aam", 1, ["RUS", "IRN"], air=6); st("fakour-90", "Fakour-90", "aam", 1, ["IRN"], air=5)
# bombs
st("gbu-31", "GBU-31 JDAM (heavy)", "bomb", 3, US_, punch=8, acc=7); st("gbu-32", "GBU-32 JDAM (medium)", "bomb", 2, US_, punch=6, acc=7)
st("gbu-38", "GBU-38 JDAM (light)", "bomb", 1, US_, punch=5, acc=7); st("gbu-39", "GBU-39 SDB", "bomb", 1, ["USA", "ISR"], punch=3, acc=8)
st("spice-2000", "SPICE 2000", "bomb", 3, ["ISR"], punch=8, acc=8); st("paveway-iv", "Paveway IV", "bomb", 2, ["GBR"], punch=6, acc=7)
st("aasm", "AASM Hammer", "bomb", 2, ["FRA"], punch=6, acc=7); st("ls-6", "LS-6 glide bomb", "bomb", 2, ["CHN"], punch=6, acc=6)
st("kab-500", "KAB-500", "bomb", 2, ["RUS"], punch=6, acc=6); st("qased", "Qased glide bomb", "bomb", 2, ["IRN"], punch=6, acc=5)
# stand-off air-to-ground
st("jassm", "AGM-158 JASSM", "agm", 3, ["USA"], punch=8, acc=8); st("storm-shadow", "Storm Shadow", "agm", 3, ["GBR", "FRA"], punch=8, acc=8)
st("delilah", "Delilah", "agm", 2, ["ISR"], punch=5, acc=8); st("kd-88", "KD-88", "agm", 2, ["CHN"], punch=6, acc=6)
st("kd-20", "KD-20", "agm", 3, ["CHN"], punch=8, acc=7); st("kh-59", "Kh-59", "agm", 3, ["RUS"], punch=7, acc=7)
# anti-radiation
st("agm-88", "AGM-88 HARM", "arm", 2, US_, punch=4, acc=7); st("ld-10", "LD-10", "arm", 2, ["CHN"], punch=4, acc=6); st("kh-31p", "Kh-31P", "arm", 2, ["RUS"], punch=4, acc=6)
# anti-ship
st("agm-84", "AGM-84 Harpoon", "asm", 2, ["USA", "JPN", "KOR"], punch=6, acc=6); st("lrasm", "AGM-158C LRASM", "asm", 3, ["USA"], punch=8, acc=8)
st("asm-2", "ASM-2", "asm", 2, ["JPN"], punch=6, acc=6); st("yj-83k", "YJ-83K", "asm", 2, ["CHN"], punch=6, acc=6)
st("kh-31a", "Kh-31A", "asm", 2, ["RUS"], punch=5, acc=6); st("exocet", "AM39 Exocet", "asm", 2, ["FRA"], punch=6, acc=6); st("nasr", "Nasr", "asm", 2, ["IRN"], punch=4, acc=5)
# pods and tanks (everyone)
st("tgp", "Targeting pod", "pod", 2, []); st("ecm", "ECM pod", "pod", 2, []); st("tank", "Drop tank", "tank", 3, [])

MR = "aim-120|pl-15|r-77|aam-4b|meteor|mica|fakour-90"
SR = "aim-9x|pl-10|r-73|python-5|mica"
HEAVY = "gbu-31|spice-2000|kab-500|ls-6|qased|aasm|paveway-iv"
MED = "gbu-32|ls-6|kab-500|qased|aasm|paveway-iv"
SMALL = "gbu-39|gbu-38|ls-6|kab-500|qased|paveway-iv|aasm"
STANDOFF = "jassm|storm-shadow|kd-20|kh-59|delilah|kd-88"
ARM = "agm-88|ld-10|kh-31p"
ASM = "agm-84|asm-2|yj-83k|kh-31a|exocet|nasr"
ASM_H = "lrasm|agm-84|asm-2|yj-83k|kh-31a|exocet|nasr"

def S_(i, n, cls, kinds, q=1): return {"id": i, "name": n, "maxCls": cls, "kinds": kinds, "maxQty": q}
def L(*slots): return [{"station": a, "store": b, "qty": c} for a, b, c in slots]
AAM = ["aam"]; HP = ["aam", "bomb", "agm", "arm", "asm", "tank", "pod"]

T = {
 "fighter": {"name": "Fighter", "stations": [S_("1", "L wingtip", 1, AAM), S_("2", "L outer", 1, AAM), S_("3", "L inner", 3, HP), S_("4", "L fuselage", 1, AAM),
             S_("5", "Centreline", 3, ["tank", "bomb", "pod"]), S_("6", "R fuselage", 1, AAM), S_("7", "R inner", 3, HP), S_("8", "R outer", 1, AAM), S_("9", "R wingtip", 1, AAM)],
   "loadouts": {"Air-to-air": L(("1", SR, 1), ("9", SR, 1), ("2", MR, 1), ("8", MR, 1), ("4", MR, 1), ("6", MR, 1), ("5", "tank", 1)),
                "Strike": L(("1", SR, 1), ("9", SR, 1), ("4", MR, 1), ("6", MR, 1), ("3", HEAVY, 1), ("7", HEAVY, 1), ("5", "tank", 1)),
                "SEAD": L(("1", SR, 1), ("9", SR, 1), ("4", MR, 1), ("6", MR, 1), ("3", ARM, 1), ("7", ARM, 1), ("5", "ecm", 1)),
                "Anti-ship": L(("1", SR, 1), ("9", SR, 1), ("4", MR, 1), ("6", MR, 1), ("3", ASM, 1), ("7", ASM, 1), ("5", "tank", 1)),
                "Recon": L(("1", SR, 1), ("9", SR, 1), ("4", MR, 1), ("6", MR, 1), ("5", "tgp", 1))}},
 "strike": {"name": "Strike fighter", "stations": [S_("1", "L wingtip / rail", 1, AAM), S_("2", "L outer", 1, AAM), S_("3", "L inner (heavy)", 3, HP), S_("4", "L conformal", 2, ["bomb", "aam"], 3),
             S_("5", "Centreline", 3, ["tank", "bomb", "pod", "agm"]), S_("6", "R conformal", 2, ["bomb", "aam"], 3), S_("7", "R inner (heavy)", 3, HP), S_("8", "R outer", 1, AAM),
             S_("9", "R wingtip / rail", 1, AAM), S_("10", "Pod station L", 2, ["pod"]), S_("11", "Pod station R", 2, ["pod"])],
   "loadouts": {"Air-to-air": L(("1", SR, 1), ("9", SR, 1), ("2", MR, 1), ("8", MR, 1), ("4", MR, 2), ("6", MR, 2), ("5", "tank", 1)),
                "Strike": L(("1", SR, 1), ("9", SR, 1), ("2", MR, 1), ("8", MR, 1), ("3", STANDOFF, 1), ("7", STANDOFF, 1), ("4", SMALL, 3), ("6", SMALL, 3), ("5", "tank", 1), ("10", "tgp", 1)),
                "SEAD": L(("1", SR, 1), ("9", SR, 1), ("2", MR, 1), ("8", MR, 1), ("3", ARM, 1), ("7", ARM, 1), ("4", SMALL, 3), ("6", SMALL, 3), ("11", "ecm", 1)),
                "Anti-ship": L(("1", SR, 1), ("9", SR, 1), ("2", MR, 1), ("8", MR, 1), ("3", ASM, 1), ("7", ASM, 1), ("5", "tank", 1)),
                "Recon": L(("1", SR, 1), ("9", SR, 1), ("2", MR, 1), ("8", MR, 1), ("10", "tgp", 1), ("5", "tank", 1))}},
 "stealth": {"name": "Stealth fighter (internal bays)", "stations": [S_("B1", "Main bay L", 2, ["aam", "bomb", "agm", "arm", "asm"], 2), S_("B2", "Main bay R", 2, ["aam", "bomb", "agm", "arm", "asm"], 2),
             S_("S1", "Side bay L", 1, AAM), S_("S2", "Side bay R", 1, AAM), S_("W1", "L wing (not stealthy)", 3, HP), S_("W2", "R wing (not stealthy)", 3, HP)],
   "loadouts": {"Air-to-air": L(("B1", MR, 2), ("B2", MR, 2), ("S1", SR, 1), ("S2", SR, 1)),
                "Strike": L(("B1", MED, 1), ("B2", MED, 1), ("S1", MR, 1), ("S2", MR, 1)),
                "SEAD": L(("B1", ARM, 1), ("B2", ARM, 1), ("S1", MR, 1), ("S2", MR, 1)),
                "Anti-ship": L(("B1", ASM, 1), ("B2", ASM, 1), ("S1", MR, 1), ("S2", MR, 1)),
                "Recon": L(("B1", MR, 2), ("B2", MR, 2))}},
 "bomber": {"name": "Bomber", "stations": [S_("1", "Bay / pylon 1", 3, ["bomb", "agm", "asm"], 4), S_("2", "Bay / pylon 2", 3, ["bomb", "agm", "asm"], 4), S_("3", "Bay / pylon 3", 3, ["bomb", "agm", "asm"], 4)],
   "loadouts": {"Strike": L(("1", STANDOFF, 2), ("2", HEAVY, 4), ("3", STANDOFF, 2)), "Anti-ship": L(("1", ASM_H, 2), ("2", ASM_H, 2), ("3", ASM_H, 2)),
                "SEAD": L(("1", STANDOFF, 2), ("2", STANDOFF, 2), ("3", STANDOFF, 2))}},
 "patrol": {"name": "Maritime patrol", "stations": [S_("W1", "L wing", 2, ["asm"], 2), S_("W2", "R wing", 2, ["asm"], 2), S_("BAY", "Weapons bay", 2, ["asm", "bomb"], 4)],
   "loadouts": {"Recon": L(("W1", ASM, 1), ("W2", ASM, 1)), "Anti-ship": L(("W1", ASM, 2), ("W2", ASM, 2), ("BAY", ASM, 2)), "Strike": L(("W1", ASM, 2), ("W2", ASM, 2))}},
}
CLASS = {"FTR": "fighter", "STK": "strike", "5GEN": "stealth", "BMR": "bomber", "SB": "bomber", "MPA": "patrol", "UCAV": "stealth"}
sym = json.load(open(os.path.join(CORE, "theatre", "symbology.json"), encoding="utf-8-sig"))
presets = {p["id"]: p for p in json.load(open(os.path.join(CORE, "presets.json"), encoding="utf-8-sig"))}
pmap = {pid: CLASS[c] for pid, c in sym["presets"].items() if c in CLASS and pid in presets}
out = {"_comment": "Built by tools/stores-build.py. Abstract game ratings (0-10), not real characteristics. store alternatives 'a|b' resolve by the aircraft's nation.",
       "stores": {s["id"]: s for s in S}, "templates": {k: {"id": k, **v} for k, v in T.items()}, "presets": pmap, "nations": {pid: presets[pid]["nation"] for pid in pmap}}
json.dump(out, open(os.path.join(CORE, "theatre", "stores.json"), "w", encoding="utf8"), indent=1, ensure_ascii=False)
print(len(S), "stores,", len(T), "templates,", len(pmap), "aircraft")
