"""Generates data/core/economy/*.json: resources, goods, processes, facility types, bill templates and rules.

GAME CONTENT. Quantities, ratios, energy and labour are abstract game units chosen for play and balance. They are
not real process data, engineering bills or plant specifications. Munitions stay abstract ("energetics").
Chains exist to make industrial capability matter: ore is not metal, metal is not an aerospace alloy.
"""
import json, os

OUT = os.path.join(os.path.dirname(__file__), "..", "data", "core", "economy")
os.makedirs(OUT, exist_ok=True)
def dump(name, obj):
    json.dump(obj, open(os.path.join(OUT, name), "w", encoding="utf8"), indent=1, ensure_ascii=False)

# ------------------------------------------------------------------ goods
GOODS = {}
def G(i, name, tier, weight="general", price=1.0, storable=True, cls=""):
    """tier: 0 raw, 1 intermediate, 2 refined, 3 industrial material, 4 part class, 5 finished/energy/service."""
    assert i not in GOODS, i
    GOODS[i] = {"id": i, "name": name, "tier": tier, "weight": weight, "price": price, "storable": storable, "class": cls}

# ------------------------------------------------------------------ facility types
FAC = {}
SPARES_SCALE = 0.3
def F(i, name, cls, policy, cap=10, energy=1.0, labour=None, build=None, months=12, cost=10, decay=0.01, spares=None, storage=200, supplies_energy=0.0, construction=0.0, protected_note=""):
    FAC[i] = {"id": i, "name": name, "class": cls, "targetClass": policy, "capacity": cap, "energy": energy,
              "labour": labour or {"general": 20}, "build": build or {"cement": 20, "structural-steel": 10, "heavy-machinery": 5},
              # spares per level per month to hold condition: kept to a few percent of output (SPARES_SCALE)
              "buildMonths": months, "buildCost": cost, "decay": decay, "spares": {k: round(v * SPARES_SCALE, 3) for k, v in (spares or {"spares-general": 1}).items()},
              "storage": storage, "suppliesEnergy": supplies_energy, "constructionCapacity": construction}

# ------------------------------------------------------------------ processes
PROC = []
def P(i, fac, inputs, outputs, energy=1.0, labour=None, tech=None, tooling=None, deposit=None, subs=None):
    PROC.append({"id": i, "facility": fac, "inputs": inputs, "outputs": outputs, "energy": energy,
                 "labour": labour or {}, "tech": tech or {}, "tooling": tooling, "deposit": deposit, "substitutes": subs or []})

def S(good, alt, ratio=1.0, penalty=0.2):
    """Substitute: `alt` may replace `good` at `ratio` alt per good; output is reduced by `penalty`."""
    return {"good": good, "alt": alt, "ratio": ratio, "penalty": penalty}

# ------------------------------------------------------------------ resources (the user's 64)
RES = []
def R(i, name, cat, method, raw=None, raw_name=None, weight="bulk", price=0.5):
    raw = raw or i
    RES.append({"id": i, "name": name, "category": cat, "method": method, "rawGood": raw})
    if raw not in GOODS: G(raw, raw_name or name, 0, weight, price)

# energy & hydrocarbons
R("crude-oil", "Crude oil", "energy", "well", price=1.0)
R("natural-gas", "Natural gas", "energy", "well", price=0.8)
R("coal", "Coal", "energy", "mine", price=0.3)
R("uranium", "Uranium ore", "energy", "mine", "uranium-ore", price=2)
R("thorium", "Thorium ore", "energy", "mine", "thorium-ore", price=1.5)
R("oil-shale", "Oil shale / bitumen", "energy", "mine", price=0.4)
# bulk minerals & construction
for i, n, p in [("iron-ore", "Iron ore", 0.2), ("manganese", "Manganese ore", 0.6), ("chromium", "Chromite ore", 0.7), ("nickel", "Nickel ore", 1.2),
                ("cobalt", "Cobalt ore", 2.5), ("copper", "Copper ore", 1.0), ("bauxite", "Bauxite", 0.3), ("zinc", "Zinc ore", 0.6), ("lead", "Lead ore", 0.5),
                ("tin", "Tin ore", 1.5), ("titanium", "Titanium minerals (ilmenite, rutile)", 1.0), ("vanadium", "Vanadium ore", 2), ("molybdenum", "Molybdenum ore", 2),
                ("tungsten", "Tungsten ore", 3), ("magnesium", "Magnesite / magnesium brine", 0.6), ("silicon", "Quartz (silicon ore)", 0.4),
                ("limestone", "Limestone / calcium minerals", 0.1), ("phosphate", "Phosphate rock", 0.3), ("potash", "Potash", 0.3)]:
    R(i, n, "bulk", "mine", i + ("" if i.endswith("ore") or i in ("bauxite", "limestone", "phosphate", "potash") else "-ore"), n, price=p)
# specialty & strategic
for i, n, p, m in [("lithium", "Lithium (brine, spodumene)", 3, "brine"), ("graphite", "Natural graphite", 1.5, "mine"), ("rare-earths", "Rare-earth ore", 3, "mine"),
                   ("tantalum", "Tantalum ore (coltan)", 6, "mine"), ("niobium", "Niobium ore", 4, "mine"), ("zirconium", "Zircon sand", 1.5, "mine"),
                   ("beryllium", "Beryl ore", 6, "mine"), ("antimony", "Antimony ore", 3, "mine"), ("pgm", "Platinum-group metal ore", 15, "mine"),
                   ("silver", "Silver ore", 8, "mine"), ("gold", "Gold ore", 20, "mine")]:
    R(i, n, "strategic", m, i + "-ore", n, "general", p)
# by-product strategic metals: listed resources, recovered from other chains (no deposit of their own)
for i, n in [("hafnium", "Hafnium (from zirconium)"), ("gallium", "Gallium (from alumina)"), ("germanium", "Germanium (from zinc and coal ash)"),
             ("indium", "Indium (from zinc)"), ("tellurium", "Tellurium (from copper)")]:
    RES.append({"id": i, "name": n, "category": "strategic", "method": "byproduct", "rawGood": i + "-crude"})
    G(i + "-crude", n + " (crude)", 1, "general", 8)
# industrial / chemical feedstocks
for i, n, m, p in [("sulfur", "Sulfur", "mine", 0.2), ("fluorspar", "Fluorspar", "mine", 0.8), ("salt", "Salt", "mine", 0.1), ("silica-sand", "Silica sand", "mine", 0.1),
                   ("boron", "Boron minerals", "mine", 1.0), ("helium", "Helium", "well", 5), ("industrial-carbon", "Industrial carbon (petroleum coke, carbon black)", "plant", 0.5),
                   ("natural-rubber", "Natural rubber", "farm", 1.2), ("timber", "Industrial timber / cellulose", "forest", 0.3)]:
    R(i, n, "feedstock", m, i if i not in ("helium",) else "helium-raw", n, "bulk", p)
# agricultural / biological
for i, n, m in [("potable-water", "Potable water", "waterworks"), ("freshwater", "Freshwater", "waterworks"), ("biomass", "Agricultural biomass", "farm"),
                ("grain", "Food and feed grains", "farm"), ("veg-oil", "Vegetable oils", "farm"), ("animal-products", "Animal products / hides", "farm"),
                ("fertilizer-feedstock", "Fertilizer feedstocks", "plant"), ("biomass-fuel", "Biomass fuels", "farm")]:
    R(i, n, "agri", m, i, n, "bulk", 0.2)
GOODS["potable-water"]["storable"] = False
GOODS["freshwater"]["storable"] = False

# ------------------------------------------------------------------ intermediate / refined / industrial goods
for i, n, t, w, p in [
    # energy
    ("electricity", "Electricity", 5, "grid", 0.1), ("diesel", "Diesel", 2, "bulk", 1.5), ("jet-fuel", "Jet fuel", 2, "bulk", 1.8), ("naphtha", "Naphtha", 2, "bulk", 1.2),
    ("heavy-fuel", "Heavy fuel oil", 2, "bulk", 0.8), ("lng", "Liquefied natural gas", 2, "bulk", 1.2), ("methane", "Pipeline gas", 2, "bulk", 0.9), ("coke", "Coke", 1, "bulk", 0.5),
    ("shale-oil", "Shale oil", 1, "bulk", 0.9), ("uranium-concentrate", "Uranium concentrate", 1, "general", 20), ("nuclear-fuel", "Nuclear fuel (abstract)", 3, "high", 80),
    ("thorium-concentrate", "Thorium concentrate", 1, "general", 10), ("lubricants", "Lubricants", 3, "general", 3),
    # ferrous
    ("iron-concentrate", "Iron concentrate", 1, "bulk", 0.4), ("pig-iron", "Pig iron", 2, "bulk", 0.8), ("steel", "Crude steel", 2, "bulk", 1.2),
    ("structural-steel", "Structural steel", 3, "bulk", 1.5), ("alloy-steel", "Alloy steel", 3, "general", 3), ("armour-steel", "Armour steel", 3, "general", 5),
    ("ferromanganese", "Ferromanganese", 2, "general", 2), ("ferrochrome", "Ferrochrome", 2, "general", 2.5), ("ferrovanadium", "Ferrovanadium", 2, "general", 6),
    ("ferromolybdenum", "Ferromolybdenum", 2, "general", 6), ("ferroniobium", "Ferroniobium", 2, "general", 8),
    # non-ferrous
    ("nickel-matte", "Nickel matte", 1, "general", 3), ("nickel-metal", "Nickel", 2, "general", 6), ("cobalt-hydroxide", "Cobalt hydroxide", 1, "general", 5), ("cobalt-metal", "Cobalt", 2, "general", 12),
    ("copper-concentrate", "Copper concentrate", 1, "bulk", 1.5), ("copper-cathode", "Copper cathode", 2, "general", 3), ("copper-products", "Copper wire and products", 3, "general", 4),
    ("alumina", "Alumina", 1, "bulk", 1), ("aluminium", "Aluminium", 2, "general", 2.5), ("aerospace-aluminium", "Aerospace aluminium alloys", 3, "general", 6),
    ("zinc-metal", "Zinc", 2, "general", 2), ("lead-metal", "Lead", 2, "general", 1.5), ("tin-metal", "Tin", 2, "general", 8), ("solder", "Solder", 3, "general", 6),
    ("titanium-tetrachloride", "Titanium tetrachloride", 1, "general", 3), ("titanium-sponge", "Titanium sponge", 2, "general", 10), ("titanium-alloy", "Titanium alloys", 3, "general", 18),
    ("vanadium-pentoxide", "Vanadium pentoxide", 1, "general", 5), ("mo-concentrate", "Molybdenum concentrate", 1, "general", 5), ("molybdenum-metal", "Molybdenum", 2, "general", 12),
    ("apt", "Ammonium paratungstate (APT)", 1, "general", 8), ("tungsten-powder", "Tungsten powder", 2, "general", 15), ("tungsten-carbide", "Tungsten carbide", 3, "general", 25),
    ("tungsten-heavy-alloy", "Tungsten heavy alloy", 3, "general", 22), ("magnesium-metal", "Magnesium", 2, "general", 3),
    ("metallurgical-silicon", "Metallurgical silicon", 2, "general", 2.5), ("polysilicon", "Polysilicon (high-purity silicon feedstock)", 3, "general", 20),
    ("silicon-wafers", "Silicon wafers", 3, "high", 60), ("superalloy", "Nickel superalloys", 3, "general", 40),
    # strategic
    ("lithium-carbonate", "Lithium carbonate", 2, "general", 12), ("lithium-hydroxide", "Lithium hydroxide", 3, "general", 15), ("purified-graphite", "Purified graphite", 2, "general", 6),
    ("re-concentrate", "Rare-earth concentrate", 1, "general", 6), ("re-oxides", "Separated rare-earth oxides", 2, "general", 25), ("re-metals", "Rare-earth metals", 3, "general", 40),
    ("tantalum-powder", "Tantalum powder", 2, "general", 40), ("niobium-concentrate", "Niobium concentrate", 1, "general", 6), ("zirconium-metal", "Zirconium", 2, "general", 20),
    ("hafnium-metal", "Hafnium", 3, "high", 200), ("gallium-metal", "High-purity gallium", 3, "high", 120), ("germanium-metal", "Germanium", 3, "high", 150),
    ("indium-metal", "Indium", 3, "high", 90), ("tellurium-metal", "Tellurium", 3, "high", 70), ("beryllium-metal", "Beryllium", 3, "high", 150),
    ("antimony-metal", "Antimony", 2, "general", 8), ("pgm-refined", "Refined platinum-group metals", 3, "high", 300), ("silver-metal", "Silver", 2, "high", 60), ("gold-metal", "Gold", 2, "high", 400),
    # chemicals, gases, ceramics
    ("sulfuric-acid", "Sulfuric acid", 2, "bulk", 0.3), ("hydrofluoric-acid", "Hydrofluoric acid", 2, "general", 2), ("fluorochemicals", "Fluorochemicals", 3, "general", 6),
    ("chlorine", "Chlorine", 2, "general", 0.5), ("caustic-soda", "Caustic soda", 2, "general", 0.5), ("industrial-gases", "Industrial gases (oxygen, nitrogen, argon)", 3, "general", 0.5),
    ("helium-gas", "Purified helium", 3, "high", 20), ("specialty-chemicals", "Specialty chemical feedstocks", 3, "general", 8), ("electronic-chemicals", "Semiconductor-grade materials", 3, "high", 40),
    ("boron-compounds", "Boron compounds", 2, "general", 4), ("ceramic-feedstock", "Ceramic / mineral feedstocks", 2, "general", 2), ("technical-ceramics", "Technical ceramics", 3, "general", 20),
    ("glass", "Glass", 2, "bulk", 0.6), ("optical-glass", "Optical glass", 3, "general", 20), ("ir-optics", "Infrared optics", 4, "high", 120), ("high-purity-metals", "High-purity metals", 3, "high", 60),
    ("lime", "Lime", 1, "bulk", 0.2), ("cement", "Cement", 2, "bulk", 0.4), ("fertilizer", "Fertilizer", 2, "bulk", 0.8), ("ammonia", "Ammonia", 2, "bulk", 0.6),
    ("polymers", "Polymers and resins", 3, "general", 2), ("pan-precursor", "Carbon fibre precursor", 3, "general", 6), ("carbon-fibre", "Carbon fibre", 3, "general", 30),
    ("composites", "Composite structures material", 3, "general", 35), ("rubber-goods", "Rubber goods and seals", 3, "general", 3), ("pulp", "Pulp and cellulose", 2, "bulk", 0.6),
    ("sawn-timber", "Construction timber", 2, "bulk", 0.5), ("energetics", "Energetics (abstract)", 3, "general", 15), ("biofuel", "Biofuel", 2, "bulk", 1.2),
    # food & civilian
    ("food", "Food", 4, "bulk", 1), ("consumer-goods", "Consumer goods", 5, "general", 3),
    # part classes (what component bills are made of)
    ("chips", "Integrated circuits", 4, "high", 80), ("power-electronics", "Power electronics", 4, "high", 30), ("rf-modules", "RF modules (compound semiconductors)", 4, "high", 150),
    ("ir-sensors", "Infrared sensor elements", 4, "high", 200), ("capacitors", "High-reliability capacitors", 4, "high", 20), ("pcbs", "Circuit boards", 4, "general", 10),
    ("cable-harness", "Cabling and harnesses", 4, "general", 4), ("batteries", "Battery cells", 4, "general", 20), ("magnets", "Permanent magnets", 4, "general", 30),
    ("electric-motors", "Electric motors", 4, "general", 20), ("precision-parts", "Precision machined parts", 4, "general", 25), ("bearings", "Bearings", 4, "general", 10),
    ("actuators", "Actuators", 4, "general", 40), ("hydraulics", "Hydraulic systems", 4, "general", 20), ("turbine-parts", "Hot-section turbine parts", 4, "high", 120),
    ("fuel-systems", "Fuel systems", 4, "general", 25), ("displays", "Displays", 4, "general", 30), ("armour-plate", "Armour plate and panels", 4, "bulk", 6),
    ("heavy-machinery", "Heavy machinery", 4, "bulk", 12), ("electrical-equipment", "Electrical equipment", 4, "general", 10),
    # tooling (enables production of precision goods)
    ("machine-tools", "Machine tools", 4, "general", 60), ("cutting-tools", "Carbide cutting tools", 4, "general", 20), ("litho-tools", "Lithography and fab tools", 4, "high", 400),
    # spares
    ("spares-general", "General spares", 5, "general", 8), ("spares-aero", "Aviation spares", 5, "general", 40), ("spares-naval", "Naval spares", 5, "general", 20),
    ("spares-electronic", "Electronic spares", 5, "high", 60),
]:
    G(i, n, t, w, p)
GOODS["electricity"]["storable"] = False

# ------------------------------------------------------------------ facility types
L_ = lambda g=0, s=0, e=0, sc=0: {k: v for k, v in {"general": g, "skilled": s, "engineer": e, "scientist": sc}.items() if v}
HB = {"cement": 40, "structural-steel": 30, "heavy-machinery": 15, "electrical-equipment": 5}          # heavy plant build bill
LB = {"cement": 15, "structural-steel": 10, "heavy-machinery": 4, "electrical-equipment": 4}           # light plant
HT = {"cement": 25, "structural-steel": 15, "heavy-machinery": 8, "electrical-equipment": 10, "machine-tools": 5}   # high-tech plant
FAB = {"cement": 30, "structural-steel": 20, "electrical-equipment": 20, "litho-tools": 10, "machine-tools": 5}
# extraction
F("mine", "Mine", "extraction", "industrial", 12, 1.5, L_(60, 10, 2), HB, 24, 40, 0.012, {"spares-general": 2, "diesel": 2})
F("oil-field", "Oil field", "extraction", "industrial", 20, 1, L_(20, 10, 3), HB, 18, 60, 0.01, {"spares-general": 2})
F("gas-field", "Gas field", "extraction", "industrial", 20, 1, L_(15, 10, 3), HB, 18, 50, 0.01, {"spares-general": 2})
F("brine-works", "Brine and evaporation works", "extraction", "industrial", 10, 1, L_(20, 6, 2), LB, 18, 30)
F("farm", "Agricultural region", "extraction", "civil-economy", 30, 0.5, L_(120, 5), {"heavy-machinery": 3, "sawn-timber": 5}, 12, 10, 0.008, {"spares-general": 1, "diesel": 1})
F("forest", "Forestry region", "extraction", "civil-economy", 20, 0.3, L_(40, 3), {"heavy-machinery": 2}, 12, 8)
F("water-works", "Water works", "extraction", "water-infrastructure", 40, 1, L_(15, 5, 1), LB, 18, 20)
# processing and refining
F("concentrator", "Ore concentrator", "processing", "industrial", 15, 2, L_(40, 10, 2), HB, 18, 30)
F("coke-plant", "Coking plant", "processing", "industrial", 20, 1, L_(30, 6, 1), HB, 18, 25)
F("blast-furnace", "Ironworks", "processing", "industrial", 20, 3, L_(60, 20, 4), HB, 30, 80, 0.012)
F("steelworks", "Steelworks", "processing", "industrial", 20, 3, L_(60, 25, 5), HB, 30, 80, 0.012)
F("rolling-mill", "Rolling and alloy mill", "processing", "industrial", 15, 2, L_(40, 25, 6), HB, 24, 60)
F("ferroalloy-plant", "Ferroalloy smelter", "processing", "industrial", 10, 4, L_(30, 10, 3), HB, 24, 40)
F("base-metal-smelter", "Base-metal smelter and refinery", "processing", "industrial", 12, 3, L_(40, 15, 4), HB, 30, 60)
F("hydromet-plant", "Hydrometallurgical refinery", "processing", "industrial", 8, 3, L_(30, 20, 6), HB, 30, 70)
F("alumina-refinery", "Alumina refinery", "processing", "industrial", 15, 3, L_(40, 15, 4), HB, 30, 70)
F("aluminium-smelter", "Aluminium smelter", "processing", "industrial", 12, 8, L_(40, 15, 4), HB, 30, 90)
F("titanium-plant", "Titanium chlorination and sponge plant", "processing", "industrial", 6, 6, L_(30, 20, 8), HB, 36, 120, 0.014)
F("specialty-refinery", "Specialty metals refinery", "processing", "industrial", 6, 4, L_(20, 20, 10, 2), HT, 36, 120)
F("re-separation", "Rare-earth separation plant", "processing", "industrial", 5, 4, L_(20, 20, 10, 3), HT, 36, 120)
F("precious-refinery", "Precious metals refinery", "processing", "industrial", 5, 2, L_(10, 15, 5), HT, 24, 60)
F("oil-refinery", "Oil refinery", "processing", "industrial", 25, 3, L_(40, 30, 8), HB, 36, 150, 0.012)
F("gas-plant", "Gas processing and LNG plant", "processing", "industrial", 20, 3, L_(20, 20, 6), HB, 36, 120)
F("shale-retort", "Oil-shale retort", "processing", "industrial", 10, 4, L_(30, 10, 3), HB, 24, 50)
F("chemical-plant", "Chemical plant", "processing", "industrial", 15, 3, L_(30, 25, 8, 2), HB, 30, 80)
F("petrochemical", "Petrochemical complex", "processing", "industrial", 15, 3, L_(30, 25, 8, 2), HB, 36, 120)
F("air-separation", "Air separation and gas plant", "processing", "industrial", 20, 5, L_(10, 10, 3), LB, 18, 30)
F("fertilizer-plant", "Fertilizer plant", "processing", "industrial", 20, 4, L_(30, 10, 3), HB, 24, 50)
F("cement-plant", "Cement and lime works", "processing", "industrial", 25, 3, L_(40, 8, 2), HB, 24, 40)
F("glass-works", "Glass and ceramics works", "processing", "industrial", 15, 3, L_(30, 15, 4, 1), LB, 24, 40)
F("polysilicon-plant", "Polysilicon plant", "processing", "industrial", 6, 8, L_(20, 25, 10, 3), HT, 36, 150)
F("wafer-plant", "Crystal growth and wafer plant", "processing", "industrial", 5, 5, L_(10, 25, 12, 4), HT, 30, 200)
F("carbon-fibre-plant", "Carbon fibre and composites plant", "processing", "industrial", 6, 5, L_(20, 25, 10, 3), HT, 30, 120)
F("battery-materials", "Battery materials plant", "processing", "industrial", 8, 3, L_(20, 20, 8, 2), HT, 30, 90)
F("nuclear-fuel-plant", "Nuclear fuel plant (abstract)", "processing", "industrial", 4, 6, L_(20, 30, 20, 8), HT, 48, 400, 0.01)
F("pulp-mill", "Pulp and sawmill", "processing", "civil-economy", 20, 2, L_(40, 8, 2), LB, 18, 30)
F("rubber-plant", "Rubber and seals plant", "processing", "industrial", 15, 1, L_(30, 10, 2), LB, 18, 25)
F("food-processing", "Food processing", "processing", "civil-economy", 40, 1, L_(60, 10, 2), LB, 12, 20)
F("light-industry", "Light industry park", "processing", "civil-economy", 40, 2, L_(120, 30, 5), LB, 18, 40)
# high-tech and parts
F("semiconductor-fab", "Semiconductor fab", "high-tech", "industrial", 4, 10, L_(20, 60, 40, 15), FAB, 48, 800, 0.015, {"spares-electronic": 3, "electronic-chemicals": 1})
F("compound-semi-fab", "Compound semiconductor fab", "high-tech", "industrial", 3, 8, L_(10, 40, 30, 12), FAB, 42, 500, 0.015, {"spares-electronic": 2})
F("electronics-plant", "Electronics assembly plant", "high-tech", "industrial", 20, 3, L_(80, 60, 20, 3), HT, 24, 80)
F("optics-works", "Optics and IR sensor works", "high-tech", "industrial", 5, 3, L_(20, 30, 15, 6), HT, 30, 120)
F("magnet-works", "Magnet and motor works", "high-tech", "industrial", 10, 3, L_(40, 30, 8, 2), HT, 24, 70)
F("battery-plant", "Battery cell plant", "high-tech", "industrial", 10, 4, L_(40, 30, 10, 3), HT, 30, 120)
F("machine-works", "Precision machine works", "high-tech", "industrial", 15, 3, L_(60, 50, 15, 2), HT, 24, 80)
F("tooling-plant", "Machine-tool and tooling plant", "high-tech", "industrial", 6, 3, L_(40, 50, 20, 4), HT, 30, 120)
F("fab-tool-maker", "Fab tool maker", "high-tech", "industrial", 1, 5, L_(20, 60, 60, 25), FAB, 60, 1500, 0.01, {"spares-electronic": 3})
F("heavy-engineering", "Heavy engineering works", "high-tech", "industrial", 15, 3, L_(100, 60, 15, 1), HB, 30, 80)
F("superalloy-mill", "Superalloy and forging works", "high-tech", "industrial", 5, 6, L_(30, 40, 15, 4), HT, 36, 200)
F("munitions-plant", "Munitions and energetics plant (abstract)", "defence", "industrial", 10, 3, L_(60, 40, 10, 2), HT, 30, 100)
# defence assembly (the old factory ids)
F("engine-works", "Engine works", "defence", "industrial", 6, 4, L_(60, 80, 30, 6), HT, 36, 300, 0.012, {"spares-aero": 2})
F("avionics-factory", "Avionics and combat systems plant", "defence", "industrial", 8, 3, L_(40, 80, 40, 8), HT, 30, 200, 0.012, {"spares-electronic": 2})
F("radar-factory", "Radar and sensor works", "defence", "industrial", 6, 4, L_(40, 80, 40, 10), HT, 30, 250, 0.012, {"spares-electronic": 2})
F("aerospace-assembly", "Aerospace assembly", "defence", "industrial", 4, 4, L_(200, 250, 80, 10), HT, 48, 600, 0.012, {"spares-aero": 3})
F("shipyard", "Shipyard", "defence", "industrial", 4, 4, L_(400, 250, 60, 5), HB, 60, 800, 0.01, {"spares-naval": 3})
F("vehicle-plant", "Military vehicle plant", "defence", "industrial", 6, 3, L_(200, 150, 30, 3), HB, 36, 300)
F("missile-works", "Missile and munitions assembly (abstract)", "defence", "industrial", 6, 3, L_(80, 100, 40, 8), HT, 36, 300)
# energy
for i, n, fuel, e_out, cost in [("power-coal", "Coal power station", "coal", 60, 60), ("power-gas", "Gas power station", "methane", 50, 40), ("power-oil", "Oil power station", "heavy-fuel", 40, 40),
                                ("power-hydro", "Hydroelectric station", None, 40, 90), ("power-nuclear", "Nuclear power station (abstract)", "nuclear-fuel", 120, 400), ("power-renewable", "Wind and solar farm", None, 20, 30)]:
    F(i, n, "energy", "energy", e_out / 10, 0, L_(20, 15, 5), HB, 36 if i != "power-nuclear" else 72, cost, 0.01, {"spares-general": 2}, 0, e_out)
# logistics, knowledge, construction, military
F("port", "Seaport", "logistics", "logistics", 100, 1, L_(200, 40, 5), HB, 36, 150, 0.008, {"spares-general": 2}, 5000)
F("airport", "Cargo airport", "logistics", "logistics", 20, 1, L_(100, 40, 5), HB, 30, 120, 0.008, {"spares-general": 1}, 500)
F("rail-yard", "Rail and road freight hub", "logistics", "logistics", 60, 1, L_(120, 20, 3), HB, 24, 60, 0.008, {"spares-general": 2}, 2000)
F("depot", "Strategic depot", "logistics", "strategic-reserve", 0, 0.5, L_(40, 10, 1), LB, 18, 30, 0.006, {"spares-general": 1}, 8000)
F("university", "University", "knowledge", "education-civil", 0, 1, L_(50, 50, 30, 30), LB, 36, 60, 0.006, {"spares-general": 0.5}, 0)
F("defence-academy", "Defence academy", "knowledge", "military-base", 0, 1, L_(40, 50, 30, 10), LB, 36, 60, 0.006, {"spares-general": 0.5}, 0)
F("research-lab", "National research laboratory", "knowledge", "industrial", 0, 2, L_(20, 40, 40, 40), HT, 36, 150, 0.008, {"spares-electronic": 1}, 0)
F("data-centre", "Data centre", "knowledge", "industrial", 0, 10, L_(10, 30, 20, 2), HT, 18, 120, 0.012, {"spares-electronic": 3}, 0)
F("construction-industry", "Construction industry", "construction", "civil-economy", 0, 1, L_(400, 100, 20), LB, 12, 30, 0.01, {"spares-general": 3, "diesel": 3}, 1000, 0, 100)
F("military-base", "Military base", "military", "military-base", 0, 1, L_(100, 100, 20), HB, 36, 120, 0.01, {"spares-general": 2}, 2000)

# ------------------------------------------------------------------ processes
TA = lambda **kw: kw  # tech helper
# extraction (one per resource with a deposit)
EXTRACT = {"mine": "mine", "well": None, "brine": "brine-works", "farm": "farm", "forest": "forest", "waterworks": "water-works", "plant": None}
for r in RES:
    m = r["method"]
    if m == "byproduct": continue
    fac = EXTRACT.get(m)
    if r["id"] in ("crude-oil",): fac = "oil-field"
    if r["id"] in ("natural-gas", "helium"): fac = "gas-field"
    if r["id"] == "industrial-carbon": continue       # produced by the refinery chain
    if r["id"] == "fertilizer-feedstock": continue    # produced by the chemical chain
    if fac is None or r["id"] == "grain": continue   # grain comes from grow-crops (needs fertilizer and fuel)
    P("extract-" + r["id"], fac, {}, {r["rawGood"]: 10}, 1.0, {}, deposit=r["id"])

# energy chains
P("refine-crude", "oil-refinery", {"crude-oil": 10}, {"diesel": 3.5, "jet-fuel": 2, "naphtha": 2, "heavy-fuel": 1.5, "industrial-carbon": 0.5, "sulfur": 0.3, "lubricants": 0.2}, 2,
  subs=[S("crude-oil", "shale-oil", 1.1, 0.1)])
P("process-gas", "gas-plant", {"natural-gas": 10}, {"methane": 8, "sulfur": 0.3}, 1.5)
P("liquefy-gas", "gas-plant", {"methane": 10}, {"lng": 9}, 3)
P("recover-helium", "gas-plant", {"helium-raw": 10}, {"helium-gas": 3}, 2, tech=TA(**{"power-systems": 2}))
P("retort-shale", "shale-retort", {"oil-shale": 10}, {"shale-oil": 4}, 3)
P("make-coke", "coke-plant", {"coal": 10}, {"coke": 7}, 1)
P("mill-uranium", "concentrator", {"uranium-ore": 10}, {"uranium-concentrate": 1}, 2)
P("mill-thorium", "concentrator", {"thorium-ore": 10}, {"thorium-concentrate": 1}, 2)
P("make-nuclear-fuel", "nuclear-fuel-plant", {"uranium-concentrate": 4, "fluorochemicals": 1, "zirconium-metal": 0.5}, {"nuclear-fuel": 1}, 10, tech=TA(**{"power-systems": 6}),
  subs=[S("uranium-concentrate", "thorium-concentrate", 1.3, 0.3)])
for pid, fac, fuel, q in [("gen-coal", "power-coal", "coal", 6), ("gen-gas", "power-gas", "methane", 4), ("gen-oil", "power-oil", "heavy-fuel", 4),
                          ("gen-hydro", "power-hydro", None, 0), ("gen-nuclear", "power-nuclear", "nuclear-fuel", 0.05), ("gen-renewable", "power-renewable", None, 0)]:
    P(pid, fac, {fuel: q} if fuel else {}, {"electricity": 10}, 0, subs=[S("methane", "lng", 1.05, 0.0)] if fuel == "methane" else None)
# ferrous
P("concentrate-iron", "concentrator", {"iron-ore": 10}, {"iron-concentrate": 6}, 1)
P("smelt-iron", "blast-furnace", {"iron-concentrate": 10, "coke": 5, "lime": 2}, {"pig-iron": 8}, 2, subs=[S("coke", "methane", 1.5, 0.1)])
P("make-steel", "steelworks", {"pig-iron": 10, "industrial-gases": 1, "lime": 1}, {"steel": 9}, 3)
P("roll-structural", "rolling-mill", {"steel": 10}, {"structural-steel": 9}, 1.5)
P("make-alloy-steel", "rolling-mill", {"steel": 10, "ferromanganese": 0.5, "ferrochrome": 1, "nickel-metal": 0.5, "ferromolybdenum": 0.2, "ferrovanadium": 0.2}, {"alloy-steel": 9}, 2,
  tech=TA(materials=2), subs=[S("ferrovanadium", "ferroniobium", 1, 0.05)])
P("make-armour-steel", "rolling-mill", {"alloy-steel": 10, "nickel-metal": 0.5, "ferromolybdenum": 0.3}, {"armour-steel": 9}, 2, tech=TA(materials=4))
P("smelt-mn", "ferroalloy-plant", {"manganese-ore": 10, "coke": 3}, {"ferromanganese": 4}, 4)
P("smelt-cr", "ferroalloy-plant", {"chromium-ore": 10, "coke": 3}, {"ferrochrome": 4}, 4)
P("smelt-v", "ferroalloy-plant", {"vanadium-pentoxide": 5, "aluminium": 1}, {"ferrovanadium": 4}, 4)
P("smelt-mo", "ferroalloy-plant", {"mo-concentrate": 5}, {"ferromolybdenum": 4}, 3)
P("smelt-nb", "ferroalloy-plant", {"niobium-concentrate": 5, "aluminium": 1}, {"ferroniobium": 4}, 4)
# non-ferrous
P("smelt-nickel", "base-metal-smelter", {"nickel-ore": 10, "sulfuric-acid": 1}, {"nickel-matte": 3}, 3)
P("refine-nickel", "hydromet-plant", {"nickel-matte": 5, "sulfuric-acid": 1}, {"nickel-metal": 4, "cobalt-hydroxide": 0.2}, 3)
P("leach-cobalt", "hydromet-plant", {"cobalt-ore": 10, "sulfuric-acid": 2}, {"cobalt-hydroxide": 2}, 3)
P("refine-cobalt", "hydromet-plant", {"cobalt-hydroxide": 5}, {"cobalt-metal": 4}, 3, tech=TA(materials=2))
P("concentrate-copper", "concentrator", {"copper-ore": 10}, {"copper-concentrate": 3}, 1.5)
P("smelt-copper", "base-metal-smelter", {"copper-concentrate": 10, "industrial-gases": 1}, {"copper-cathode": 8, "sulfuric-acid": 3, "tellurium-crude": 0.02, "silver-metal": 0.01}, 3)
P("draw-copper", "rolling-mill", {"copper-cathode": 10}, {"copper-products": 9}, 1.5)
P("refine-bauxite", "alumina-refinery", {"bauxite": 10, "caustic-soda": 1}, {"alumina": 4, "gallium-crude": 0.01}, 2)
P("smelt-aluminium", "aluminium-smelter", {"alumina": 10, "industrial-carbon": 2, "fluorochemicals": 0.2}, {"aluminium": 5}, 12)
P("alloy-aero-aluminium", "rolling-mill", {"aluminium": 10, "magnesium-metal": 0.5, "copper-cathode": 0.5, "lithium-carbonate": 0.1}, {"aerospace-aluminium": 9}, 2, tech=TA(materials=3))
P("smelt-zinc", "base-metal-smelter", {"zinc-ore": 10, "sulfuric-acid": 1}, {"zinc-metal": 5, "indium-crude": 0.02, "germanium-crude": 0.01}, 4)
P("smelt-lead", "base-metal-smelter", {"lead-ore": 10}, {"lead-metal": 6, "silver-metal": 0.02, "antimony-metal": 0.05}, 2)
P("smelt-tin", "base-metal-smelter", {"tin-ore": 10}, {"tin-metal": 4}, 3)
P("make-solder", "rolling-mill", {"tin-metal": 6, "lead-metal": 2, "silver-metal": 0.1}, {"solder": 8}, 1, subs=[S("lead-metal", "tin-metal", 1, 0.05)])
P("chlorinate-titanium", "titanium-plant", {"titanium-ore": 10, "chlorine": 4, "industrial-carbon": 1}, {"titanium-tetrachloride": 5}, 4, tech=TA(materials=3))
P("reduce-titanium", "titanium-plant", {"titanium-tetrachloride": 10, "magnesium-metal": 3}, {"titanium-sponge": 3}, 10, tech=TA(materials=4))
P("alloy-titanium", "superalloy-mill", {"titanium-sponge": 10, "aluminium": 0.6, "ferrovanadium": 0.4}, {"titanium-alloy": 9}, 6, tech=TA(materials=5))
P("roast-vanadium", "specialty-refinery", {"vanadium-ore": 10}, {"vanadium-pentoxide": 2}, 3)
P("concentrate-mo", "concentrator", {"molybdenum-ore": 10}, {"mo-concentrate": 2}, 2)
P("refine-mo", "specialty-refinery", {"mo-concentrate": 5}, {"molybdenum-metal": 3}, 4, tech=TA(materials=3))
P("make-apt", "specialty-refinery", {"tungsten-ore": 10, "caustic-soda": 1}, {"apt": 2}, 3, tech=TA(materials=3))
P("reduce-tungsten", "specialty-refinery", {"apt": 5, "industrial-gases": 1}, {"tungsten-powder": 3}, 5, tech=TA(materials=4))
P("make-carbide", "tooling-plant", {"tungsten-powder": 8, "industrial-carbon": 1, "cobalt-metal": 0.8}, {"tungsten-carbide": 8}, 4, tech=TA(materials=5, manufacturing=4))
P("make-heavy-alloy", "superalloy-mill", {"tungsten-powder": 9, "nickel-metal": 0.7, "ferromanganese": 0.2}, {"tungsten-heavy-alloy": 9}, 4, tech=TA(materials=5))
P("make-magnesium", "specialty-refinery", {"magnesium-ore": 10}, {"magnesium-metal": 2}, 12)
P("smelt-silicon", "ferroalloy-plant", {"silicon-ore": 10, "industrial-carbon": 3}, {"metallurgical-silicon": 4}, 10)
P("purify-silicon", "polysilicon-plant", {"metallurgical-silicon": 10, "chlorine": 2}, {"polysilicon": 6}, 20, tech=TA(materials=6))
P("grow-wafers", "wafer-plant", {"polysilicon": 10, "technical-ceramics": 0.5}, {"silicon-wafers": 6}, 10, tech=TA(materials=7, manufacturing=6))
P("make-superalloy", "superalloy-mill", {"nickel-metal": 6, "cobalt-metal": 1, "ferrochrome": 1.5, "ferroniobium": 0.3, "molybdenum-metal": 0.4, "tungsten-powder": 0.3, "re-metals": 0.05},
  {"superalloy": 8}, 8, tech=TA(materials=7, **{"jet-engines": 5}), subs=[S("cobalt-metal", "nickel-metal", 1.2, 0.15)])
# strategic
P("make-lithium-carbonate", "battery-materials", {"lithium-ore": 10, "sulfuric-acid": 1}, {"lithium-carbonate": 2}, 3)
P("make-lithium-hydroxide", "battery-materials", {"lithium-carbonate": 5, "caustic-soda": 1}, {"lithium-hydroxide": 4}, 3, tech=TA(materials=4))
P("purify-graphite", "battery-materials", {"graphite-ore": 10, "hydrofluoric-acid": 0.5}, {"purified-graphite": 4}, 5, tech=TA(materials=4),
  subs=[S("graphite-ore", "industrial-carbon", 1.2, 0.1)])
P("concentrate-re", "concentrator", {"rare-earths-ore": 10}, {"re-concentrate": 2}, 2)
P("separate-re", "re-separation", {"re-concentrate": 5, "specialty-chemicals": 2, "sulfuric-acid": 2}, {"re-oxides": 3}, 6, tech=TA(materials=7))
P("reduce-re", "re-separation", {"re-oxides": 5, "fluorochemicals": 1}, {"re-metals": 4}, 8, tech=TA(materials=7))
P("refine-tantalum", "specialty-refinery", {"tantalum-ore": 10, "hydrofluoric-acid": 1}, {"tantalum-powder": 1}, 4, tech=TA(materials=5))
P("concentrate-nb", "concentrator", {"niobium-ore": 10}, {"niobium-concentrate": 2}, 2)
P("refine-zirconium", "specialty-refinery", {"zirconium-ore": 10, "chlorine": 2}, {"zirconium-metal": 3, "hafnium-crude": 0.05}, 6, tech=TA(materials=5))
P("refine-hafnium", "specialty-refinery", {"hafnium-crude": 1}, {"hafnium-metal": 0.8}, 6, tech=TA(materials=7))
P("refine-gallium", "specialty-refinery", {"gallium-crude": 1}, {"gallium-metal": 0.8}, 4, tech=TA(materials=6))
P("refine-germanium", "specialty-refinery", {"germanium-crude": 1}, {"germanium-metal": 0.8}, 4, tech=TA(materials=6))
P("refine-indium", "specialty-refinery", {"indium-crude": 1}, {"indium-metal": 0.8}, 4, tech=TA(materials=5))
P("refine-tellurium", "specialty-refinery", {"tellurium-crude": 1}, {"tellurium-metal": 0.8}, 4, tech=TA(materials=5))
P("refine-beryllium", "specialty-refinery", {"beryllium-ore": 10, "sulfuric-acid": 1}, {"beryllium-metal": 1}, 6, tech=TA(materials=6))
P("smelt-antimony", "base-metal-smelter", {"antimony-ore": 10}, {"antimony-metal": 4}, 2)
P("refine-pgm", "precious-refinery", {"pgm-ore": 10, "chlorine": 1}, {"pgm-refined": 0.5}, 4, tech=TA(materials=4))
P("refine-silver", "precious-refinery", {"silver-ore": 10}, {"silver-metal": 1}, 2)
P("refine-gold", "precious-refinery", {"gold-ore": 10}, {"gold-metal": 0.3}, 2)
P("purify-metals", "specialty-refinery", {"copper-cathode": 2, "aluminium": 2, "tantalum-powder": 0.2, "electronic-chemicals": 0.5}, {"high-purity-metals": 3}, 6, tech=TA(materials=6))
# chemicals, gases, ceramics
P("make-sulfuric", "chemical-plant", {"sulfur": 4}, {"sulfuric-acid": 10}, 0.5)
P("make-hf", "chemical-plant", {"fluorspar": 10, "sulfuric-acid": 5}, {"hydrofluoric-acid": 5}, 2)
P("make-fluorochem", "chemical-plant", {"hydrofluoric-acid": 5, "chlorine": 2}, {"fluorochemicals": 4}, 3, tech=TA(materials=3))
P("chlor-alkali", "chemical-plant", {"salt": 10, "freshwater": 2}, {"chlorine": 5, "caustic-soda": 5}, 6)
P("separate-air", "air-separation", {}, {"industrial-gases": 10}, 5)
P("make-specialty-chem", "chemical-plant", {"naphtha": 4, "sulfuric-acid": 2, "chlorine": 2}, {"specialty-chemicals": 4}, 4, tech=TA(materials=3))
P("make-electronic-chem", "chemical-plant", {"specialty-chemicals": 4, "hydrofluoric-acid": 2, "industrial-gases": 2, "helium-gas": 0.2}, {"electronic-chemicals": 2}, 6, tech=TA(materials=6))
P("make-boron", "chemical-plant", {"boron": 10, "sulfuric-acid": 2}, {"boron-compounds": 4}, 2)
P("make-ceramic-feed", "glass-works", {"zirconium-ore": 2, "alumina": 4, "silica-sand": 4}, {"ceramic-feedstock": 8}, 2)
P("make-tech-ceramics", "glass-works", {"ceramic-feedstock": 8, "boron-compounds": 1, "industrial-carbon": 1}, {"technical-ceramics": 5}, 8, tech=TA(materials=5))
P("make-glass", "glass-works", {"silica-sand": 10, "caustic-soda": 1, "lime": 1}, {"glass": 9}, 3)
P("make-optical-glass", "glass-works", {"glass": 8, "boron-compounds": 0.5, "re-oxides": 0.2}, {"optical-glass": 5}, 4, tech=TA(**{"ir-systems": 3}))
P("burn-lime", "cement-plant", {"limestone": 10}, {"lime": 6}, 2)
P("make-cement", "cement-plant", {"limestone": 10, "silica-sand": 2}, {"cement": 9}, 3)
P("make-ammonia", "fertilizer-plant", {"methane": 5}, {"ammonia": 6}, 4, subs=[S("methane", "coal", 2, 0.2)])
P("make-fertilizer", "fertilizer-plant", {"ammonia": 4, "phosphate": 4, "potash": 3, "sulfuric-acid": 2}, {"fertilizer": 10, "fertilizer-feedstock": 2}, 2)
P("make-polymers", "petrochemical", {"naphtha": 10, "industrial-gases": 1}, {"polymers": 8}, 4, subs=[S("naphtha", "methane", 1.3, 0.1)])
P("make-pan", "petrochemical", {"naphtha": 6, "ammonia": 2}, {"pan-precursor": 5}, 5, tech=TA(materials=4))
P("make-carbon-fibre", "carbon-fibre-plant", {"pan-precursor": 10, "industrial-gases": 2}, {"carbon-fibre": 4}, 12, tech=TA(materials=6))
P("make-composites", "carbon-fibre-plant", {"carbon-fibre": 6, "polymers": 4}, {"composites": 8}, 4, tech=TA(materials=5))
P("make-rubber-goods", "rubber-plant", {"natural-rubber": 6, "industrial-carbon": 2, "sulfur": 0.5}, {"rubber-goods": 8}, 2, subs=[S("natural-rubber", "polymers", 1, 0.15)])
P("make-pulp", "pulp-mill", {"timber": 10, "caustic-soda": 0.5}, {"pulp": 5, "sawn-timber": 4}, 2)
P("make-energetics", "munitions-plant", {"ammonia": 4, "sulfuric-acid": 3, "specialty-chemicals": 2, "pulp": 1}, {"energetics": 4}, 4, tech=TA(payloads=2))
P("make-biofuel", "food-processing", {"veg-oil": 5, "biomass-fuel": 5}, {"biofuel": 6}, 2)
P("process-food", "food-processing", {"grain": 8, "animal-products": 2, "veg-oil": 1, "freshwater": 2}, {"food": 10}, 1)
P("grow-crops", "farm", {"fertilizer": 1, "diesel": 0.5}, {"grain": 4, "veg-oil": 1, "biomass": 2}, 0.2, deposit="grain")
P("make-consumer-goods", "light-industry", {"polymers": 3, "steel": 2, "chips": 0.1, "pulp": 1, "copper-products": 0.5}, {"consumer-goods": 10}, 3)
# parts
P("make-chips", "semiconductor-fab", {"silicon-wafers": 1, "electronic-chemicals": 1, "high-purity-metals": 0.3, "industrial-gases": 1, "helium-gas": 0.1}, {"chips": 4}, 20,
  tech=TA(computing=5, manufacturing=6), tooling="litho-tools")
P("make-rf-modules", "compound-semi-fab", {"gallium-metal": 0.3, "silicon-wafers": 0.5, "electronic-chemicals": 0.5, "high-purity-metals": 0.2}, {"rf-modules": 2}, 15,
  tech=TA(radar=5, manufacturing=5), tooling="litho-tools")
P("make-power-electronics", "electronics-plant", {"chips": 0.5, "copper-products": 1, "capacitors": 0.5, "pcbs": 1}, {"power-electronics": 4}, 3, tech=TA(**{"power-systems": 3}))
P("make-ir-sensors", "optics-works", {"germanium-metal": 0.2, "tellurium-metal": 0.1, "indium-metal": 0.1, "chips": 0.3}, {"ir-sensors": 2}, 6, tech=TA(**{"ir-systems": 5}))
P("make-ir-optics", "optics-works", {"germanium-metal": 0.5, "optical-glass": 1}, {"ir-optics": 2}, 4, tech=TA(**{"ir-systems": 4}))
P("make-capacitors", "electronics-plant", {"tantalum-powder": 0.3, "silver-metal": 0.05, "technical-ceramics": 0.3}, {"capacitors": 4}, 2, subs=[S("tantalum-powder", "aluminium", 3, 0.3)])
P("make-pcbs", "electronics-plant", {"copper-products": 2, "glass": 1, "polymers": 1, "gold-metal": 0.01}, {"pcbs": 5}, 2)
P("make-harness", "electronics-plant", {"copper-products": 4, "polymers": 2, "silver-metal": 0.02}, {"cable-harness": 5}, 1)
P("make-displays", "electronics-plant", {"glass": 2, "indium-metal": 0.05, "chips": 0.3, "re-oxides": 0.05}, {"displays": 3}, 3)
P("make-batteries", "battery-plant", {"lithium-hydroxide": 1, "purified-graphite": 1, "nickel-metal": 0.8, "cobalt-metal": 0.3, "copper-products": 0.5, "aluminium": 0.5}, {"batteries": 4}, 6,
  tech=TA(**{"power-systems": 4}), subs=[S("cobalt-metal", "ferromanganese", 1.5, 0.2)])
P("make-magnets", "magnet-works", {"re-metals": 1, "steel": 2, "boron-compounds": 0.2}, {"magnets": 4}, 4, tech=TA(materials=5), subs=[S("re-metals", "cobalt-metal", 1.5, 0.35)])
P("make-motors", "magnet-works", {"magnets": 1, "copper-products": 2, "alloy-steel": 2, "bearings": 0.5}, {"electric-motors": 3}, 3)
P("make-precision-parts", "machine-works", {"alloy-steel": 3, "titanium-alloy": 0.5, "aerospace-aluminium": 1}, {"precision-parts": 4}, 3, tech=TA(manufacturing=3), tooling="machine-tools")
P("make-bearings", "machine-works", {"alloy-steel": 3, "lubricants": 0.2}, {"bearings": 4}, 2, tooling="cutting-tools")
P("make-actuators", "machine-works", {"electric-motors": 1, "precision-parts": 1, "power-electronics": 0.5, "hydraulics": 0.3}, {"actuators": 2}, 3, tech=TA(**{"flight-control": 3}), tooling="machine-tools")
P("make-hydraulics", "machine-works", {"alloy-steel": 2, "rubber-goods": 1, "lubricants": 0.5, "precision-parts": 0.5}, {"hydraulics": 3}, 2, tooling="machine-tools")
P("make-turbine-parts", "superalloy-mill", {"superalloy": 3, "technical-ceramics": 0.5, "precision-parts": 0.5}, {"turbine-parts": 2}, 8, tech=TA(**{"jet-engines": 5}), tooling="machine-tools")
P("make-fuel-systems", "machine-works", {"aerospace-aluminium": 1, "precision-parts": 1, "rubber-goods": 0.5, "electric-motors": 0.3}, {"fuel-systems": 2}, 2, tooling="machine-tools")
P("make-armour-plate", "rolling-mill", {"armour-steel": 8, "technical-ceramics": 1}, {"armour-plate": 8}, 3, subs=[S("technical-ceramics", "armour-steel", 2, 0.1)])
P("make-heavy-machinery", "heavy-engineering", {"structural-steel": 6, "alloy-steel": 2, "electric-motors": 1, "hydraulics": 0.5, "bearings": 0.5}, {"heavy-machinery": 4}, 4)
P("make-electrical-equipment", "heavy-engineering", {"copper-products": 3, "structural-steel": 2, "power-electronics": 0.5, "polymers": 1}, {"electrical-equipment": 4}, 3)
P("make-machine-tools", "tooling-plant", {"alloy-steel": 4, "precision-parts": 1, "chips": 0.2, "electric-motors": 0.5}, {"machine-tools": 1}, 4, tech=TA(manufacturing=4))
P("make-cutting-tools", "tooling-plant", {"tungsten-carbide": 2, "alloy-steel": 1}, {"cutting-tools": 3}, 2, tech=TA(manufacturing=3))
P("make-litho-tools", "fab-tool-maker", {"precision-parts": 4, "optical-glass": 2, "chips": 1, "electric-motors": 2, "machine-tools": 1}, {"litho-tools": 0.3}, 10, tech=TA(manufacturing=8, computing=7))
# spares
P("make-spares-general", "heavy-engineering", {"alloy-steel": 2, "bearings": 0.5, "rubber-goods": 0.5}, {"spares-general": 3}, 2)
P("make-spares-aero", "machine-works", {"precision-parts": 1, "turbine-parts": 0.2, "actuators": 0.2, "chips": 0.1}, {"spares-aero": 1}, 3, tooling="machine-tools")
P("make-spares-naval", "heavy-engineering", {"alloy-steel": 2, "electric-motors": 0.3, "hydraulics": 0.3, "power-electronics": 0.2}, {"spares-naval": 2}, 2)
P("make-spares-electronic", "electronics-plant", {"chips": 0.3, "pcbs": 0.5, "rf-modules": 0.05, "capacitors": 0.2}, {"spares-electronic": 1}, 2)

# ------------------------------------------------------------------ bill templates (component families, frames, parts)
# goods per 10 cost units of a component; facility = where the component is assembled. Abstract game classes only.
FAMILY = {
    "propulsion": ("engine-works", {"turbine-parts": 0.6, "precision-parts": 0.8, "fuel-systems": 0.4, "bearings": 0.3, "chips": 0.05, "cable-harness": 0.2}),
    "power": ("heavy-engineering", {"electrical-equipment": 1.2, "power-electronics": 0.3, "copper-products": 0.8, "batteries": 0.2}),
    "sensors": ("radar-factory", {"rf-modules": 0.3, "chips": 0.15, "power-electronics": 0.3, "pcbs": 0.5, "cable-harness": 0.3, "technical-ceramics": 0.2, "ir-sensors": 0.05}),
    "computing": ("avionics-factory", {"chips": 0.4, "pcbs": 0.6, "power-electronics": 0.2, "displays": 0.2, "cable-harness": 0.2}),
    "comms": ("avionics-factory", {"rf-modules": 0.15, "chips": 0.2, "pcbs": 0.5, "cable-harness": 0.3}),
    "navigation": ("avionics-factory", {"chips": 0.3, "precision-parts": 0.2, "pcbs": 0.4, "optical-glass": 0.1}),
    "guidance": ("avionics-factory", {"chips": 0.3, "precision-parts": 0.3, "actuators": 0.2, "ir-optics": 0.05, "pcbs": 0.3}),
    "ew": ("avionics-factory", {"rf-modules": 0.25, "chips": 0.25, "pcbs": 0.4, "power-electronics": 0.3}),
    "payload": ("munitions-plant", {"energetics": 1.0, "alloy-steel": 0.8, "tungsten-heavy-alloy": 0.05, "precision-parts": 0.2}),
    "fuzing": ("munitions-plant", {"chips": 0.1, "pcbs": 0.2, "precision-parts": 0.2, "batteries": 0.1}),
    "interfaces": ("machine-works", {"aerospace-aluminium": 0.6, "precision-parts": 0.4, "actuators": 0.2, "cable-harness": 0.2}),
    "structure": ("heavy-engineering", {"structural-steel": 2.0, "cable-harness": 0.2, "polymers": 0.3}),
    "materials": ("rolling-mill", {"composites": 0.4, "technical-ceramics": 0.3, "alloy-steel": 0.4}),
    "thermal": ("heavy-engineering", {"copper-products": 0.6, "aluminium": 0.6, "electric-motors": 0.3, "fluorochemicals": 0.1}),
    "reliability": ("electronics-plant", {"spares-electronic": 0.1}),
    "manufacturing": ("tooling-plant", {"machine-tools": 0.05}),
    "testing": ("electronics-plant", {"chips": 0.02}),
    "doctrine": ("", {}),
}
# per frame kind: assembly facility and goods per 10 tonnes of the frame's weight budget (ships) or per unit (others)
FRAME = {
    "ship": ("shipyard", {"structural-steel": 6, "armour-steel": 0.4, "electric-motors": 0.2, "cable-harness": 0.4, "hydraulics": 0.2, "polymers": 0.3}, "mass", 10),
    "aircraft": ("aerospace-assembly", {"aerospace-aluminium": 4, "composites": 3, "titanium-alloy": 1.5, "actuators": 1.5, "hydraulics": 0.8, "fuel-systems": 0.6, "cable-harness": 1.2, "rubber-goods": 0.3}, "mass", 1),
    "munition": ("missile-works", {"aerospace-aluminium": 1.5, "composites": 0.8, "precision-parts": 0.5, "actuators": 0.4, "cable-harness": 0.3}, "unit", 1),
    "ground": ("vehicle-plant", {"structural-steel": 3, "armour-steel": 0.8, "heavy-machinery": 0.5, "rubber-goods": 0.6, "hydraulics": 0.4, "cable-harness": 0.4}, "mass", 10),
}
# visual parts: category -> goods per 10 t of the part's weight
PART = {"Guns": {"alloy-steel": 6, "precision-parts": 1}, "Launchers": {"structural-steel": 5, "actuators": 0.5}, "Superstructure": {"structural-steel": 6, "aluminium": 2},
        "Sensors": {"rf-modules": 0.2, "structural-steel": 3}, "Masts": {"structural-steel": 5}, "Aviation": {"structural-steel": 5}, "Boats": {"aluminium": 4, "polymers": 2},
        "Point defence": {"alloy-steel": 4, "rf-modules": 0.1, "actuators": 0.5}, "Airframe": {"aerospace-aluminium": 5, "composites": 3}, "Engines": {"turbine-parts": 1, "precision-parts": 2},
        "Landing gear": {"alloy-steel": 4, "hydraulics": 2}, "Stores": {"aerospace-aluminium": 4}, "Vehicle": {"structural-steel": 5, "rubber-goods": 1},
        "Pressure hull": {"alloy-steel": 8}, "Sub propulsion": {"batteries": 1, "electric-motors": 1}, "Accommodation": {"structural-steel": 3, "polymers": 1}}
DEFAULT_PART = {"structural-steel": 4, "cable-harness": 0.3}
SPARES = {"ship": "spares-naval", "aircraft": "spares-aero", "munition": "spares-electronic", "ground": "spares-general"}
dump("bill_templates.json", {"families": {k: {"facility": f, "goods": g} for k, (f, g) in FAMILY.items()},
                             "frames": {k: {"facility": f, "goods": g, "per": per, "unit": u, "spares": SPARES[k]} for k, (f, g, per, u) in FRAME.items()},
                             "parts": PART, "defaultPart": DEFAULT_PART})

# ------------------------------------------------------------------ rules (all thresholds are data)
dump("economy_rules.json", {
    "defenceShareDefault": 0.02,
    "depression": {"windowMonths": 12, "gdpDrop": -0.10, "warOrEmbargoDrop": -0.25, "crisisStability": 35, "civilWarStability": 15, "recoverStability": 50},
    "stability": {"trendWeight": 60, "shortageWeight": 25, "unemploymentWeight": 15, "warRally": 5, "embargoPenalty": 5, "monthlyPull": 0.15},
    "gdp": {"civilianShare": 0.85, "trendGrowthAnnual": 0.02, "lossValueFactor": 1.0},
    "transport": {"bulk": 1.0, "general": 0.6, "high": 0.2, "grid": 0.0},
    "maintenance": {"readinessFloor": 0.2, "restorePerMonth": 0.08},
    # the rest of the world: any ordinary good can be bought up to this value per good per month (game units, scaled by
    # the buyer's GDP), subject to embargoes and sea-lane reach. Restricted goods only come from named suppliers or allies.
    "worldMarket": {"depthPerGdp": 0.004, "priceMarkup": 1.3,
                    "restricted": ["rf-modules", "litho-tools", "ir-sensors", "ir-optics", "turbine-parts", "superalloy", "energetics", "nuclear-fuel",
                                   "uranium-concentrate", "hafnium-metal", "gallium-metal", "germanium-metal", "beryllium-metal", "re-metals", "magnets",
                                   "titanium-alloy", "carbon-fibre", "composites", "machine-tools", "tungsten-heavy-alloy", "armour-steel", "armour-plate",
                                   "spares-aero", "spares-naval", "spares-electronic", "chips", "silicon-wafers", "electronic-chemicals", "helium-gas"]},
    "civilianBasket": {"food": 10, "consumer-goods": 4, "electricity": 30, "diesel": 3, "potable-water": 20},
})
# industrial-material categories (#59-64): made, not mined; listed so the resource screen shows them
for i, n, g in [("hp-silicon", "High-purity silicon feedstock", "polysilicon"), ("industrial-gases-cat", "Industrial gases", "industrial-gases"),
                ("specialty-chem-cat", "Specialty chemical feedstocks", "specialty-chemicals"), ("ceramic-cat", "Ceramic / mineral feedstocks", "ceramic-feedstock"),
                ("hp-metals", "High-purity metals", "high-purity-metals"), ("semi-grade", "Semiconductor-grade materials", "electronic-chemicals")]:
    RES.append({"id": i, "name": n, "category": "industrial", "method": "manufactured", "rawGood": g})
dump("resources.json", RES)
dump("goods.json", list(GOODS.values()))
dump("processes.json", PROC)
dump("facility_types.json", list(FAC.values()))
print(f"resources {len(RES)} · goods {len(GOODS)} · processes {len(PROC)} · facility types {len(FAC)}")

# sanity: every input and output is a known good, every process facility exists
for p in PROC:
    assert p["facility"] in FAC, p
    for g in list(p["inputs"]) + list(p["outputs"]) + ([p["tooling"]] if p["tooling"] else []): assert g in GOODS, (p["id"], g)
    for s in p["substitutes"]: assert s["alt"] in GOODS and s["good"] in p["inputs"], (p["id"], s)
for f in FAC.values():
    for g in list(f["build"]) + list(f["spares"]): assert g in GOODS, (f["id"], g)
for fam, (fac, gs) in FAMILY.items():
    assert fac == "" or fac in FAC, fam
    for g in gs: assert g in GOODS, (fam, g)
produced = {g for p in PROC for g in p["outputs"]}
unproduced = [g for g in GOODS if g not in produced]
print("goods with no process (import-only):", unproduced)
