"""Generates the economic geography: regions, deposits, nation economies per start date, world-market suppliers and
the sector -> facility mapping used to seed facilities procedurally.

GAME CONTENT. Regions use public admin-level names and rough centroids. Deposits are country/region-level
endowments from general public knowledge with game-scaled sizes (1-5); they are not mine locations. GDP and
population are rounded public figures used only to scale the game economy. No real facility locations are used:
the game places its own facilities inside regions from these profiles (see EconomySeeder).
"""
import json, os

OUT = os.path.join(os.path.dirname(__file__), "..", "data", "core", "economy")
def dump(name, obj):
    json.dump(obj, open(os.path.join(OUT, name), "w", encoding="utf8"), indent=1, ensure_ascii=False)

# region: (id suffix, name, lon, lat, coastal, industry weight, capital?)  deposits: {resource: size 1-5}
REG = {
 "USA": [("ne", "Northeast", -75, 41, 1, 4, 1, {"coal": 2, "natural-gas": 2, "timber": 1}),
         ("mw", "Midwest", -88, 41.5, 0, 4, 0, {"iron-ore": 3, "coal": 4, "grain": 5, "limestone": 3, "biomass": 4, "freshwater": 5}),
         ("tx", "Texas and Gulf Coast", -96, 30, 1, 4, 0, {"crude-oil": 5, "natural-gas": 5, "helium": 3, "salt": 4, "sulfur": 3, "grain": 2, "animal-products": 3}),
         ("se", "Southeast", -83, 33, 1, 3, 0, {"phosphate": 4, "timber": 4, "silica-sand": 3, "titanium": 1, "zirconium": 1, "grain": 2, "veg-oil": 3}),
         ("mt", "Mountain West", -110, 40, 0, 2, 0, {"copper": 4, "molybdenum": 4, "uranium": 2, "coal": 4, "gold": 3, "silver": 2, "lithium": 1, "beryllium": 3, "potash": 1, "vanadium": 1}),
         ("wc", "West Coast", -120, 37, 1, 5, 0, {"crude-oil": 2, "rare-earths": 2, "boron": 4, "grain": 3, "veg-oil": 2, "timber": 3}),
         ("ak", "Alaska", -150, 63, 1, 1, 0, {"crude-oil": 3, "natural-gas": 3, "zinc": 3, "lead": 2, "gold": 2, "timber": 2})],
 "RUS": [("cen", "Central Russia", 37.6, 55.7, 0, 5, 1, {"iron-ore": 4, "grain": 3, "limestone": 3, "freshwater": 4}),
         ("vol", "Volga and Urals", 56, 56, 0, 4, 0, {"crude-oil": 3, "iron-ore": 3, "chromium": 2, "titanium": 3, "vanadium": 3, "copper": 3, "potash": 5, "bauxite": 1, "magnesium": 2}),
         ("wsb", "West Siberia", 73, 61, 0, 2, 0, {"crude-oil": 5, "natural-gas": 5, "helium": 1}),
         ("esb", "East Siberia", 100, 62, 0, 2, 0, {"nickel": 5, "cobalt": 3, "pgm": 5, "copper": 3, "gold": 4, "coal": 5, "uranium": 2, "timber": 5, "helium": 3}),
         ("fe", "Far East", 135, 50, 1, 2, 0, {"gold": 3, "tin": 2, "tungsten": 2, "coal": 3, "timber": 4, "natural-gas": 2}),
         ("nw", "Northwest and Kola", 33, 67, 1, 2, 0, {"phosphate": 4, "rare-earths": 2, "nickel": 2, "iron-ore": 2, "timber": 3, "tantalum": 1, "niobium": 1}),
         ("sth", "South Russia", 40, 46, 1, 3, 0, {"grain": 5, "coal": 2, "veg-oil": 4, "animal-products": 3})],
 "CHN": [("nth", "North China", 116, 39, 1, 5, 1, {"coal": 5, "iron-ore": 4, "magnesium": 5, "fluorspar": 4, "molybdenum": 3, "grain": 4, "salt": 3}),
         ("ne", "Northeast China", 125, 44, 1, 3, 0, {"crude-oil": 3, "grain": 4, "graphite": 4, "molybdenum": 2, "timber": 3, "iron-ore": 2}),
         ("est", "East China", 120, 31, 1, 5, 0, {"grain": 4, "silica-sand": 3, "fluorspar": 2, "animal-products": 3}),
         ("sth", "South China", 113, 23, 1, 5, 0, {"tin": 3, "zinc": 3, "rare-earths": 3, "lead": 2, "grain": 3, "natural-rubber": 1}),
         ("cen", "Central China", 113, 29, 0, 4, 0, {"tungsten": 5, "antimony": 5, "graphite": 3, "rare-earths": 2, "lithium": 2, "grain": 4, "limestone": 4}),
         ("sw", "Southwest China", 104, 29, 0, 3, 0, {"natural-gas": 3, "titanium": 4, "vanadium": 4, "phosphate": 4, "lithium": 3, "silicon": 3, "tin": 2}),
         ("nwc", "Northwest China", 87, 43, 0, 2, 0, {"crude-oil": 3, "natural-gas": 3, "coal": 4, "nickel": 2, "beryllium": 2, "silicon": 4}),
         ("imn", "Inner Mongolia", 111, 41, 0, 2, 0, {"rare-earths": 5, "coal": 5, "iron-ore": 3, "niobium": 1, "graphite": 2, "animal-products": 3})],
 "GBR": [("se", "South East England", 0, 51.5, 1, 5, 1, {"grain": 3, "silica-sand": 2}),
         ("mid", "Midlands and North", -2, 53.5, 1, 4, 0, {"coal": 3, "limestone": 3, "salt": 3, "potash": 2, "grain": 2}),
         ("sco", "Scotland", -4, 56.5, 1, 2, 0, {"timber": 2, "animal-products": 2, "freshwater": 3}),
         ("nsea", "North Sea", 1.5, 57.5, 1, 1, 0, {"crude-oil": 4, "natural-gas": 4}),
         ("wal", "Wales", -3.8, 52.3, 1, 1, 0, {"coal": 2, "limestone": 2})],
 "FRA": [("idf", "Île-de-France", 2.3, 48.8, 0, 5, 1, {"grain": 3}),
         ("ne", "North-East France", 6, 48.5, 0, 3, 0, {"iron-ore": 2, "coal": 2, "salt": 3, "potash": 2, "grain": 3}),
         ("ra", "Rhône-Alpes", 5, 45.5, 0, 4, 0, {"freshwater": 5, "silica-sand": 2}),
         ("sw", "South-West France", 0, 44, 1, 3, 0, {"natural-gas": 2, "sulfur": 2, "grain": 4, "timber": 3, "veg-oil": 2}),
         ("nw", "North-West France", -1.5, 48, 1, 2, 0, {"grain": 3, "animal-products": 4}),
         ("med", "Mediterranean France", 5, 43.5, 1, 3, 0, {"bauxite": 1, "salt": 2})],
 "ISR": [("cen", "Central Israel", 34.8, 32.1, 1, 5, 1, {"natural-gas": 3}),
         ("nth", "Northern Israel", 35.2, 32.8, 1, 2, 0, {"grain": 1, "freshwater": 1}),
         ("neg", "Negev", 34.9, 30.8, 0, 2, 0, {"phosphate": 4, "potash": 4, "salt": 3, "magnesium": 3})],
 "IRN": [("teh", "Tehran", 51.4, 35.7, 0, 5, 1, {"grain": 2}),
         ("khz", "Khuzestan", 49, 31, 1, 3, 0, {"crude-oil": 5, "natural-gas": 3, "sulfur": 2}),
         ("isf", "Isfahan and Kerman", 53, 31, 0, 3, 0, {"copper": 4, "zinc": 3, "lead": 3, "iron-ore": 3, "chromium": 2, "uranium": 1, "molybdenum": 1}),
         ("sth", "Bushehr and South", 52, 28, 1, 2, 0, {"natural-gas": 5, "crude-oil": 3, "sulfur": 2, "salt": 2}),
         ("nw", "Azerbaijan provinces", 46.3, 38, 0, 2, 0, {"grain": 3, "copper": 2}),
         ("est", "Khorasan", 59.6, 36.3, 0, 1, 0, {"coal": 2, "iron-ore": 2, "gold": 1})],
 "EGY": [("cai", "Cairo and Delta", 31.2, 30.5, 1, 5, 1, {"grain": 4, "freshwater": 3}),
         ("alx", "Alexandria and West", 29.9, 31, 1, 3, 0, {"natural-gas": 4, "crude-oil": 2, "salt": 2}),
         ("suz", "Suez and Sinai", 32.5, 29.5, 1, 2, 0, {"crude-oil": 2, "silica-sand": 3, "manganese": 1}),
         ("upp", "Upper Egypt", 32.8, 25.7, 0, 1, 0, {"phosphate": 4, "iron-ore": 2, "gold": 1, "grain": 2})],
 "TUR": [("mar", "Marmara", 29, 41, 1, 5, 0, {"grain": 2}),
         ("ank", "Central Anatolia", 32.9, 39.9, 0, 3, 1, {"boron": 5, "grain": 4, "salt": 3}),
         ("aeg", "Aegean", 27.1, 38.4, 1, 3, 0, {"boron": 3, "antimony": 2, "coal": 2, "silica-sand": 2}),
         ("bls", "Black Sea", 37, 41, 1, 2, 0, {"copper": 2, "coal": 2, "timber": 2, "zinc": 1}),
         ("se", "Southeast Anatolia", 39, 37.5, 0, 1, 0, {"chromium": 4, "crude-oil": 1, "phosphate": 2}),
         ("med", "Mediterranean Turkey", 33, 36.9, 1, 2, 0, {"chromium": 2, "veg-oil": 3, "grain": 2})],
 "SAU": [("riy", "Riyadh and Central", 46.7, 24.7, 0, 4, 1, {"silica-sand": 3}),
         ("est", "Eastern Province", 50, 26.3, 1, 4, 0, {"crude-oil": 5, "natural-gas": 5, "sulfur": 4, "salt": 2}),
         ("wst", "Western Province", 39.2, 21.5, 1, 3, 0, {"gold": 2, "copper": 2, "zinc": 1}),
         ("nth", "Northern Province", 41, 29, 0, 1, 0, {"phosphate": 4, "bauxite": 3})],
 "PRK": [("pyo", "Pyongyang", 125.8, 39, 1, 5, 1, {"coal": 3, "grain": 2}),
         ("nth", "North Hamgyong", 129.5, 41.5, 1, 2, 0, {"iron-ore": 3, "magnesium": 5, "coal": 3, "graphite": 3, "rare-earths": 2}),
         ("est", "South Hamgyong and Kangwon", 127.5, 39.8, 1, 2, 0, {"zinc": 3, "lead": 2, "tungsten": 2, "gold": 2, "molybdenum": 1})],
 "KOR": [("cap", "Seoul Capital Area", 127, 37.5, 1, 5, 1, {"silica-sand": 1}),
         ("se", "Gyeongsang", 129, 35.5, 1, 5, 0, {"limestone": 2, "grain": 1}),
         ("cc", "Chungcheong", 127.4, 36.5, 0, 3, 0, {"limestone": 3, "grain": 2}),
         ("jl", "Jeolla", 126.9, 35, 1, 2, 0, {"grain": 3, "salt": 1})],
 "JPN": [("kan", "Kanto", 139.7, 35.7, 1, 5, 1, {"limestone": 2}),
         ("ksi", "Kansai", 135.5, 34.7, 1, 5, 0, {"limestone": 1}),
         ("chb", "Chubu", 137, 35.2, 1, 5, 0, {"silica-sand": 2, "freshwater": 3}),
         ("kyu", "Kyushu", 130.5, 32.8, 1, 3, 0, {"limestone": 3, "gold": 1, "sulfur": 1, "grain": 1}),
         ("toh", "Tohoku", 140.8, 38.5, 1, 2, 0, {"timber": 3, "grain": 3, "freshwater": 3}),
         ("hok", "Hokkaido", 142.5, 43.2, 1, 1, 0, {"timber": 3, "grain": 2, "animal-products": 2, "coal": 1}),
         ("chg", "Chugoku", 133, 34.5, 1, 3, 0, {"limestone": 2, "silica-sand": 1})],
 "DEU": [("nth", "Northern Germany", 10, 53.5, 1, 4, 0, {"natural-gas": 1, "salt": 3, "potash": 3, "grain": 3}),
         ("nrw", "Rhine-Ruhr", 7.5, 51.4, 0, 5, 0, {"coal": 3, "limestone": 3}),
         ("bav", "Bavaria", 11.5, 48.6, 0, 4, 0, {"graphite": 1, "grain": 3, "freshwater": 3}),
         ("est", "Eastern Germany", 13, 51.5, 0, 3, 1, {"coal": 4, "potash": 3, "silica-sand": 2}),
         ("bw", "Baden-Württemberg", 9, 48.7, 0, 5, 0, {"salt": 2})],
 "UKR": [("kyv", "Kyiv and Centre", 30.5, 50.4, 0, 4, 1, {"grain": 5, "veg-oil": 4, "uranium": 1}),
         ("don", "Donbas", 38, 48, 0, 4, 0, {"coal": 5, "natural-gas": 1, "salt": 3}),
         ("dni", "Dnipro", 35, 48, 0, 4, 0, {"iron-ore": 5, "manganese": 4, "titanium": 3, "zirconium": 3, "uranium": 2, "grain": 4}),
         ("sth", "South and Odesa", 30.7, 46.5, 1, 3, 0, {"grain": 5, "veg-oil": 3}),
         ("wst", "Western Ukraine", 24, 49.8, 0, 2, 0, {"natural-gas": 1, "timber": 2, "potash": 1})],
}

REGIONS, DEPOSITS = [], []
for nat, rs in REG.items():
    for sfx, name, lon, lat, coast, ind, cap, deps in rs:
        rid = f"{nat}-{sfx}"
        REGIONS.append({"id": rid, "nation": nat, "name": name, "lon": lon, "lat": lat, "coastal": bool(coast), "industry": ind, "capital": bool(cap)})
        # every region has farmland, forest, water and the common construction minerals at some level
        full = {"potable-water": 4, "freshwater": 4, "limestone": 3, "silica-sand": 3, "salt": 1, "biomass": 2, "biomass-fuel": 1, "animal-products": 1, "veg-oil": 1}
        full.update(deps)
        for res, size in full.items():
            DEPOSITS.append({"id": f"{rid}-{res}", "region": rid, "resource": res, "size": size, "reserve": size * 250000, "grade": 1.0})
# land links between regions of one nation (chain in listed order, plus capital to all)
LINKS = []
for nat, rs in REG.items():
    ids = [f"{nat}-{r[0]}" for r in rs]
    cap = next(f"{nat}-{r[0]}" for r in rs if r[6])
    for a, b in zip(ids, ids[1:]): LINKS.append({"id": f"{a}~{b}", "a": a, "b": b, "mode": "land", "capacity": 400})
    for r in ids:
        if r != cap and not any((l["a"], l["b"]) in ((cap, r), (r, cap)) for l in LINKS): LINKS.append({"id": f"{cap}~{r}", "a": cap, "b": r, "mode": "land", "capacity": 300})

# nation economies. GDP in game billions (rounded public nominal USD), population in millions.
# profile: sector tiers 0-5 for 2020; earlier dates apply the ERA factors.
NAT = {  # gdp 1980, 2000, 2020 | pop 1980, 2000, 2020 | heavy metals specialty electronics semis aero naval land munitions | mix
 "USA": ((2860, 10250, 21060), (227, 282, 331), (3, 3, 3, 4, 5, 5, 4, 4, 5), {"coal": .2, "gas": .4, "nuclear": .19, "hydro": .07, "renewable": .12, "oil": .02}),
 "RUS": ((940, 260, 1490), (139, 146, 144), (4, 4, 4, 2, 1, 4, 3, 4, 4), {"gas": .45, "coal": .15, "nuclear": .2, "hydro": .18, "oil": .01, "renewable": .01}),
 "CHN": ((190, 1210, 14690), (981, 1263, 1411), (5, 5, 5, 5, 3, 3, 5, 4, 4), {"coal": .6, "gas": .03, "hydro": .17, "nuclear": .05, "renewable": .14, "oil": .01}),
 "GBR": ((600, 1660, 2700), (56, 59, 67), (2, 2, 1, 3, 2, 4, 2, 2, 3), {"gas": .4, "nuclear": .16, "renewable": .36, "coal": .02, "oil": .01, "hydro": .02}),
 "FRA": ((700, 1360, 2630), (54, 59, 67), (2, 2, 2, 3, 2, 4, 3, 3, 3), {"nuclear": .7, "hydro": .12, "gas": .07, "renewable": .1, "coal": .01}),
 "ISR": ((23, 130, 410), (3.9, 6.3, 9.2), (1, 1, 1, 3, 3, 3, 1, 2, 3), {"gas": .7, "coal": .2, "renewable": .07, "oil": .03}),
 "IRN": ((95, 110, 240), (39, 66, 84), (2, 2, 1, 1, 0, 1, 1, 2, 3), {"gas": .8, "oil": .1, "hydro": .06, "nuclear": .02, "coal": .02}),
 "EGY": ((22, 100, 380), (43, 68, 102), (2, 1, 0, 1, 0, 0, 1, 1, 1), {"gas": .75, "oil": .15, "hydro": .08, "renewable": .02}),
 "TUR": ((68, 270, 720), (44, 64, 84), (3, 2, 1, 2, 0, 2, 3, 3, 3), {"coal": .35, "gas": .3, "hydro": .25, "renewable": .1}),
 "SAU": ((165, 190, 700), (10, 21, 35), (2, 2, 0, 1, 0, 0, 0, 1, 1), {"oil": .4, "gas": .6}),
 "PRK": ((13, 11, 18), (17, 23, 26), (2, 1, 1, 0, 0, 1, 1, 2, 3), {"coal": .5, "hydro": .5}),
 "KOR": ((65, 560, 1640), (38, 47, 52), (4, 3, 2, 5, 5, 3, 5, 4, 3), {"coal": .4, "nuclear": .27, "gas": .28, "renewable": .05}),
 "JPN": ((1100, 4970, 5040), (117, 127, 126), (4, 4, 3, 5, 4, 3, 5, 3, 2), {"gas": .37, "coal": .3, "nuclear": .05, "hydro": .08, "renewable": .15, "oil": .05}),
 "DEU": ((950, 1950, 3890), (78, 82, 83), (3, 3, 2, 4, 3, 3, 2, 4, 3), {"coal": .3, "renewable": .43, "gas": .15, "nuclear": .12}),
 "UKR": ((150, 32, 156), (50, 49, 44), (3, 3, 2, 1, 0, 2, 1, 2, 2), {"nuclear": .5, "coal": .3, "gas": .08, "hydro": .06, "renewable": .06}),
}
SECTORS = ["heavy", "metals", "specialty", "electronics", "semis", "aero", "naval", "land", "munitions"]
# era factors per sector (1980, 2000); 2020 = 1. Nations listed override (e.g. China and Korea rose fast).
ERA = {"default": {1980: {"electronics": .5, "semis": .4}, 2000: {"electronics": .8, "semis": .8}},
       "CHN": {1980: {"heavy": .6, "metals": .5, "specialty": .5, "electronics": .1, "semis": 0, "aero": .5, "naval": .3, "land": .7, "munitions": .8}, 2000: {"electronics": .5, "semis": .2, "naval": .5, "aero": .6}},
       "KOR": {1980: {"electronics": .3, "semis": .1, "naval": .5, "heavy": .6}, 2000: {"semis": .8}},
       "RUS": {1980: {"heavy": 1.2, "aero": 1.2, "naval": 1.3, "munitions": 1.2, "land": 1.2}, 2000: {"aero": .8, "naval": .6, "electronics": .8}},
       "UKR": {1980: {"heavy": 1.3, "aero": 1.3, "naval": 2, "land": 1.3}, 2000: {"naval": 1.5}},
       "JPN": {1980: {"semis": .8, "electronics": .9}},
       "USA": {1980: {"semis": .8, "electronics": .8}}}
ECON = []
for nat, (gdp, pop, prof, mix) in NAT.items():
    for k, year in enumerate((1980, 2000, 2020)):
        f = dict(ERA["default"].get(year, {})); f.update(ERA.get(nat, {}).get(year, {}))
        tiers = {s: max(0, min(5, round(prof[i] * f.get(s, 1)))) for i, s in enumerate(SECTORS)}
        ECON.append({"id": f"{nat}-{year}", "nation": nat, "year": year, "gdp": gdp[k], "population": pop[k], "sectors": tiers, "energyMix": mix,
                     "defenceShare": 0.02, "stockpileMonths": 3})

# world market: non-playable exporters reached through sea lanes (game abstraction of the rest of the world)
SUPPLIERS = [
 ("chile", "Chile", -71.6, -33, {"copper-concentrate": 800, "copper-cathode": 300, "lithium-carbonate": 60, "mo-concentrate": 20}),
 ("peru", "Peru", -77, -12, {"copper-concentrate": 400, "zinc-metal": 100, "silver-metal": 3, "gold-metal": 1}),
 ("drc", "DR Congo", 12.3, -6, {"cobalt-hydroxide": 80, "copper-cathode": 150, "tantalum-powder": 2}),
 ("australia", "Australia", 115.8, -32, {"iron-ore": 5000, "bauxite": 1500, "alumina": 300, "lithium-ore": 400, "coal": 2000, "natural-gas": 400, "rare-earths-ore": 40, "zirconium-ore": 40, "gold-metal": 2}),
 ("brazil", "Brazil", -46.3, -24, {"iron-ore": 3000, "ferroniobium": 40, "niobium-concentrate": 60, "bauxite": 500, "grain": 800, "veg-oil": 300, "graphite-ore": 60}),
 ("south-africa", "South Africa", 31, -29.9, {"pgm-refined": 1, "ferrochrome": 200, "manganese-ore": 800, "ferromanganese": 60, "vanadium-pentoxide": 20, "coal": 800, "gold-metal": 1}),
 ("indonesia", "Indonesia", 106.8, -6.1, {"nickel-ore": 1500, "nickel-matte": 60, "tin-metal": 40, "coal": 2000, "natural-rubber": 300, "veg-oil": 600, "bauxite": 300}),
 ("guinea", "Guinea", -13.7, 9.5, {"bauxite": 1500}),
 ("kazakhstan", "Kazakhstan", 71.4, 51.2, {"uranium-concentrate": 20, "chromium-ore": 200, "crude-oil": 800, "ferrochrome": 60}),
 ("canada", "Canada", -79.4, 43.7, {"potash": 1500, "uranium-concentrate": 10, "nickel-metal": 40, "aluminium": 200, "crude-oil": 2000, "timber": 1500, "grain": 800}),
 ("gulf-states", "Gulf states", 54.4, 24.5, {"crude-oil": 4000, "lng": 600, "aluminium": 200, "sulfur": 300, "fertilizer": 300}),
 ("norway", "Norway", 5.3, 60.4, {"crude-oil": 800, "natural-gas": 800, "aluminium": 100, "magnesium-metal": 10}),
 ("taiwan", "Taiwan", 121.5, 25, {"chips": 400, "silicon-wafers": 40, "pcbs": 200, "displays": 60}),
 ("malaysia", "Malaysia", 101.7, 3.1, {"natural-rubber": 200, "chips": 40, "tin-metal": 20, "re-oxides": 4}),
 ("india", "India", 72.8, 19, {"iron-ore": 800, "grain": 600, "specialty-chemicals": 40, "consumer-goods": 300}),
 ("mexico", "Mexico", -99.1, 19.4, {"silver-metal": 5, "fluorspar": 300, "crude-oil": 600, "consumer-goods": 200}),
]
SUP = [{"id": i, "name": n, "lon": lo, "lat": la, "exports": ex} for i, n, lo, la, ex in SUPPLIERS]

# which facility types a sector tier seeds, and at what level per tier point (EconomySeeder reads this)
SECTOR_FAC = {
 "heavy": ["coke-plant", "blast-furnace", "steelworks", "rolling-mill", "cement-plant", "chemical-plant", "air-separation", "fertilizer-plant", "glass-works",
           "heavy-engineering", "petrochemical", "pulp-mill", "rubber-plant", "concentrator"],
 "metals": ["ferroalloy-plant", "base-metal-smelter", "hydromet-plant", "alumina-refinery", "aluminium-smelter", "precious-refinery"],
 "specialty": ["specialty-refinery", "re-separation", "titanium-plant", "superalloy-mill", "battery-materials", "carbon-fibre-plant"],
 "electronics": ["electronics-plant", "magnet-works", "battery-plant", "optics-works", "machine-works", "tooling-plant"],
 "semis": ["semiconductor-fab", "compound-semi-fab", "wafer-plant", "polysilicon-plant"],
 "aero": ["aerospace-assembly", "engine-works", "avionics-factory", "radar-factory"],
 "naval": ["shipyard"], "land": ["vehicle-plant"], "munitions": ["munitions-plant", "missile-works"],
}
dump("seeding.json", {
    "sectorFacilities": SECTOR_FAC,
    # special cases expressed as data
    "requiresCoast": ["shipyard", "port"],
    # basic industry every nation sizes to at least its own demand (tiers only scale advanced capability)
    "basic": ["gas-plant", "oil-refinery", "coke-plant", "concentrator", "cement-plant", "food-processing", "light-industry", "pulp-mill",
              "water-works", "fertilizer-plant", "farm", "forest", "air-separation", "heavy-engineering", "rubber-plant", "glass-works",
              "chemical-plant", "shale-retort", "brine-works"],
    "minTier": {"fab-tool-maker": 5, "nuclear-fuel-plant": 1, "compound-semi-fab": 2, "titanium-plant": 2, "re-separation": 2},
    "topTierExtras": {"semis": ["fab-tool-maker"]},
    "always": {"construction-industry": 1, "rail-yard": 1, "military-base": 1, "food-processing": 1, "light-industry": 1, "water-works": 1},
    "capital": {"airport": 1, "depot": 2, "defence-academy": 1, "research-lab": 1, "data-centre": 1},
    "coastal": {"port": 1},
    "universityMinIndustry": 3,
    "extraction": {"mine": "mine", "well": "oil-field", "gas": "gas-field", "brine": "brine-works", "farm": "farm", "forest": "forest", "waterworks": "water-works"},
    "energyMix": {"coal": "power-coal", "gas": "power-gas", "oil": "power-oil", "hydro": "power-hydro", "nuclear": "power-nuclear", "renewable": "power-renewable"},
    # electricity demand in game units per GDP billion (sets how many power station levels a nation starts with)
    "energyPerGdp": 0.06,
    "jitterDeg": 1.2,
    # general industrial output per GDP billion per year (game units): sizes the civilian and dual-use industry
    "industrialBasket": {"structural-steel": 3, "cement": 5, "polymers": 1.5, "chips": 0.02, "electrical-equipment": 0.3, "heavy-machinery": 0.2,
                         "aluminium": 0.4, "copper-products": 0.2, "fertilizer": 0.5, "glass": 0.5, "diesel": 1, "jet-fuel": 0.2, "sulfuric-acid": 0.2,
                         "spares-general": 0.1, "batteries": 0.02, "machine-tools": 0.005, "pulp": 0.3, "rubber-goods": 0.1, "pcbs": 0.05, "lubricants": 0.05},
    # baseline defence output: representative design and units per month per sector tier point
    "sectorDesigns": {"aero": {"design": "bomber-lo", "perTier": 0.1}, "naval": {"design": "ddg-1998", "perTier": 0.02},
                      "land": {"design": "gs-launcher", "perTier": 0.5}, "munitions": {"design": "sam-interceptor", "perTier": 5},
                      "electronics": {"design": "gs-radar", "perTier": 0.2}},
})
# default sanctions per start date: a game abstraction of public sanctions regimes. good "*" = everything,
# "strategic" = the restricted strategic goods; global = an enforcer's sanction every other seller follows.
def X(by, target, good="*", glob=False): return {"by": by, "target": target, "good": good, "global": glob}
WEST = ["USA", "GBR", "FRA", "DEU", "JPN", "KOR"]
TRADE = [
 {"id": "1980", "year": 1980, "sanctions": [X("USA", "IRN"), X("USA", "PRK"), X("KOR", "PRK")] + [X(w, "RUS", "strategic") for w in WEST + ["TUR", "ISR"]]
                                            + [X(w, "CHN", "strategic") for w in WEST]},
 {"id": "2000", "year": 2000, "sanctions": [X("USA", "IRN", glob=True), X("USA", "PRK", glob=True)] + [X(w, "CHN", "strategic") for w in ["USA", "GBR", "FRA", "DEU"]]},
 {"id": "2020", "year": 2020, "sanctions": [X("USA", "IRN", glob=True), X("USA", "PRK", glob=True), X("ISR", "IRN"), X("IRN", "ISR")]
                                            + [X(w, "RUS", "strategic") for w in WEST + ["UKR"]] + [X("USA", "CHN", "litho-tools", True), X("USA", "CHN", "strategic")]},
]
dump("trade.json", TRADE)
dump("regions.json", REGIONS)
dump("region_links.json", LINKS)
dump("deposits.json", DEPOSITS)
dump("nation_economies.json", ECON)
dump("suppliers.json", SUP)
print(f"regions {len(REGIONS)} · links {len(LINKS)} · deposits {len(DEPOSITS)} · economies {len(ECON)} · suppliers {len(SUP)}")
