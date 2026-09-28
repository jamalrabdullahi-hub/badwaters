"""Generates starter engineering content for data/core. Values are game-balanced, not real data.
Run: python tools/gen_content.py  (edit this file or the JSON directly; mods override by id)."""
import json, os
root = os.path.join(os.path.dirname(__file__), "..", "data", "core")
def dump(name, obj):
    path = os.path.join(root, name); os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf8") as f: json.dump(obj, f, indent=1, ensure_ascii=False)

dump("engineering/axes.json", [
 {"id":"mass","name":"Weight","unit":"t"},{"id":"volume","name":"Space","unit":"slots"},
 {"id":"power","name":"Power","unit":"pts"},{"id":"cooling","name":"Cooling","unit":"pts"},
 {"id":"crew","name":"Crew","unit":"people"},{"id":"data","name":"Data bandwidth","unit":"pts"}])
dump("engineering/ratings.json", [{"id":i,"name":n,"aggregate":a,"cap":10} for i,n,a in [
 ("detection","Detection","max"),("air-defence","Air defence","sum"),("strike","Strike","sum"),("anti-ship","Anti-ship","sum"),
 ("anti-sub","Anti-submarine","sum"),("survivability","Survivability","sum"),("signature","Stealth","sum"),("speed","Speed","max"),
 ("endurance","Endurance","sum"),("accuracy","Accuracy","max"),("reach","Reach","max"),("punch","Punch","max"),
 ("jam-resistance","Jam resistance","sum"),("ew","Electronic warfare","sum"),("networking","Networking","max"),
 ("agility","Agility","max"),("payload","Payload","sum"),("reliability","Reliability","min"),("mobility","Mobility","max"),
 ("fire-channels","Fire channels","sum"),("field","Field performance","sum")]])

fields = [("materials",10),("structures",10),("power-systems",10),("gas-turbines",10),("jet-engines",10),("rocket-motors",10),
 ("navigation",10),("flight-control",10),("radar",10),("ir-systems",10),("sonar",10),("computing",10),("datalinks",10),
 ("ew-systems",10),("payloads",10),("fuzing",10),("thermal",10),("low-observables",10),("autonomy",10),("manufacturing",10),
 ("testing",10),("doctrine",10)]
dump("tech_fields.json", [{"id":f,"name":f.replace("-"," ").title(),"maxLevel":m} for f,m in fields])

goods = ["steel","aluminium","titanium","composites","rare-materials","semiconductors","rf-modules","precision-electronics","energetics","fuel","engine-parts"]
dump("goods.json", [{"id":g,"name":g.replace("-"," ").title()} for g in goods])

F = lambda i,n,prod="",cap=10,inp=None: {"id":i,"name":n,"produces":prod,"capacityPerLevel":cap,"inputsPerWork":inp or {},"outputPerWork":1}
dump("factories.json", [
 F("steel-mill","Steel mill","steel",40,{"rare-materials":0.05}), F("aluminium-plant","Aluminium plant","aluminium",25),
 F("titanium-works","Titanium works","titanium",8,{"rare-materials":0.2}), F("composite-factory","Composite factory","composites",12,{"rare-materials":0.1}),
 F("semiconductor-fab","Semiconductor fab","semiconductors",10,{"rare-materials":0.3}), F("rf-module-plant","RF module plant","rf-modules",8,{"semiconductors":0.8}),
 F("electronics-plant","Precision electronics","precision-electronics",15,{"semiconductors":0.5}), F("energetics-plant","Energetics plant","energetics",20),
 F("refinery","Refinery","fuel",50), F("engine-parts-plant","Engine parts plant","engine-parts",10,{"titanium":0.4,"steel":0.2}),
 F("shipyard","Shipyard","",30,{"steel":1.0,"precision-electronics":0.1}),
 F("aerospace-assembly","Aerospace assembly","",20,{"aluminium":0.4,"composites":0.4,"engine-parts":0.3,"precision-electronics":0.2}),
 F("missile-works","Missile works","",25,{"aluminium":0.2,"energetics":0.4,"precision-electronics":0.3}),
 F("radar-factory","Radar factory","",8,{"rf-modules":1.0,"precision-electronics":0.5}),
 F("vehicle-plant","Vehicle plant","",20,{"steel":0.6,"precision-electronics":0.1}),
 F("avionics-factory","Avionics factory","",10,{"precision-electronics":0.8,"semiconductors":0.3})])

C=[]
def c(i,n,fam,kinds,req,uses,adds,port=None,cost=1,fac=None,work=0,offers=None,needs=None):
    C.append({"id":i,"name":n,"family":fam,"fitsKinds":kinds,"requires":req,"uses":uses,"adds":adds,"port":port,"cost":cost,"factory":fac,"workUnits":work,"offers":offers or [],"needs":needs or []})
S,A,M,G=["ship"],["aircraft"],["munition"],["ground"]
# structure / materials
c("mat-steel","Steel structure","materials",S+G,{"materials":1},{"mass":0},{"survivability":1},cost=0)
c("mat-composite","Composite structure","materials",A+M,{"materials":4},{"mass":-2},{"signature":1},cost=5,fac="aerospace-assembly",work=2)
c("mat-ram","Radar-absorbent coating","materials",S+A+M+G,{"low-observables":5},{"mass":1},{"signature":3},cost=8,fac="aerospace-assembly",work=3)
c("str-armoured-citadel","Armoured citadel","structure",S,{"structures":3},{"mass":300,"volume":2},{"survivability":2},cost=40,fac="shipyard",work=10)
# power
c("pw-gen-diesel","Diesel generators","power",S+G,{"power-systems":1},{"mass":60,"volume":2,"power":-12},{},cost=10,fac="shipyard",work=3)
c("pw-gen-turbine","Gas-turbine generators","power",S,{"power-systems":4,"gas-turbines":3},{"mass":90,"volume":3,"power":-30},{},cost=35,fac="shipyard",work=5)
c("pw-iep","Integrated electric plant","power",S,{"power-systems":7},{"mass":200,"volume":5,"power":-60,"cooling":4},{"endurance":1},cost=160,fac="shipyard",work=20)
c("pw-apu","Auxiliary power unit","power",A+G,{"power-systems":2},{"mass":1,"power":-6},{},cost=2)
c("pw-battery","Thermal battery","power",M,{"power-systems":2},{"mass":0.01,"power":-1},{},cost=0.05)
# propulsion
c("pr-cogag","Gas turbines (combined)","propulsion",S,{"gas-turbines":3},{"mass":400,"volume":6,"crew":20},{"speed":7,"endurance":3},cost=90,fac="shipyard",work=15)
c("pr-diesel","Diesel propulsion","propulsion",S,{"power-systems":1},{"mass":250,"volume":5,"crew":15},{"speed":5,"endurance":6},cost=40,fac="shipyard",work=10)
c("pr-nuclear","Naval reactor","propulsion",S,{"power-systems":8},{"mass":2000,"volume":20,"crew":80,"power":-80,"cooling":-10},{"speed":8,"endurance":10},cost=900,fac="shipyard",work=120)
c("pr-aip","Air-independent propulsion","propulsion",S,{"power-systems":6},{"mass":150,"volume":4,"crew":8},{"speed":4,"endurance":6,"signature":2},cost=120,fac="shipyard",work=20)
c("pr-turbofan","Turbofan engines","propulsion",A,{"jet-engines":4},{"mass":3,"volume":2,"crew":0,"power":-6},{"speed":7,"endurance":6,"agility":6},cost=12,fac="aerospace-assembly",work=8)
c("pr-turbofan-adv","Advanced turbofan","propulsion",A,{"jet-engines":7},{"mass":3,"volume":2,"cooling":1,"power":-10},{"speed":8,"endurance":7,"agility":8},cost=20,fac="aerospace-assembly",work=12)
c("pr-rocket","Solid rocket motor","propulsion",M,{"rocket-motors":2},{"mass":0.2,"volume":1},{"speed":8,"reach":3},cost=0.2,fac="missile-works",work=0.5)
c("pr-cruise","Small turbofan (cruise)","propulsion",M,{"jet-engines":3},{"mass":0.15,"volume":1},{"speed":3,"reach":8},cost=0.4,fac="missile-works",work=0.8)
c("pr-truck","Wheeled drivetrain","propulsion",G,{"structures":1},{"mass":4,"crew":1},{"mobility":6},cost=1,fac="vehicle-plant",work=1)
# navigation
c("nv-ins","Inertial navigation","navigation",S+A+M+G,{"navigation":2},{"mass":0.01,"power":0.2},{"accuracy":3},cost=0.1)
c("nv-gnss","Satellite navigation","navigation",A+M+G,{"navigation":4},{"mass":0.01,"power":0.1},{"accuracy":5},cost=0.05)
c("nv-star","Star navigation","navigation",A+M,{"navigation":6},{"mass":0.02,"power":0.3},{"accuracy":6,"jam-resistance":2},cost=0.4)
c("nv-terrain","Terrain matching","navigation",A+M,{"navigation":5,"radar":3},{"mass":0.02,"power":0.3},{"accuracy":6,"jam-resistance":1},cost=0.3)
c("nv-fusion","Anti-jam navigation fusion","navigation",A+M+S,{"navigation":8,"computing":6},{"mass":0.02,"power":0.3},{"accuracy":7,"jam-resistance":3},cost=0.6)
# guidance / control
c("gc-autopilot","Autopilot","guidance",M,{"flight-control":2},{"mass":0.01,"power":0.1},{"accuracy":1},cost=0.05)
c("gc-fbw","Digital flight controls","guidance",A,{"flight-control":5},{"mass":0.2,"power":0.5},{"agility":2},cost=3)
c("gc-fire-control","Fire-control system","guidance",S+G,{"computing":3},{"mass":5,"volume":1,"power":2},{"fire-channels":2},cost=15,offers=["fire-control"])
# sensors
c("sn-radar-search","Rotating search radar","sensors",S+G,{"radar":2},{"mass":8,"power":4,"cooling":1},{"detection":4},port="mast-sensor",cost=20,fac="radar-factory",work=4)
c("sn-radar-pesa","Phased-array radar","sensors",S+G,{"radar":5},{"mass":60,"power":10,"cooling":4},{"detection":6,"fire-channels":4},port="mast-sensor",cost=180,fac="radar-factory",work=15,offers=["fire-control"])
c("sn-radar-aesa","Active array radar","sensors",S+G,{"radar":7},{"mass":50,"power":16,"cooling":8},{"detection":8,"fire-channels":8},port="mast-sensor",cost=260,fac="radar-factory",work=25,offers=["fire-control"])
c("sn-radar-aesa-air","Airborne active array","sensors",A,{"radar":7},{"mass":0.3,"power":4,"cooling":3},{"detection":7,"fire-channels":4},port="nose-sensor",cost=12,fac="radar-factory",work=6,offers=["fire-control"])
c("sn-radar-pd-air","Airborne pulse-Doppler radar","sensors",A,{"radar":3},{"mass":0.3,"power":2,"cooling":1},{"detection":5,"fire-channels":1},port="nose-sensor",cost=4,fac="radar-factory",work=2,offers=["fire-control"])
c("sn-irst","Infrared search & track","sensors",A+S,{"ir-systems":5},{"mass":0.1,"power":0.5},{"detection":4},cost=2)
c("sn-sonar-hull","Hull sonar","sensors",S,{"sonar":2},{"mass":30,"power":2},{"anti-sub":3},port="bow-sonar",cost=20)
c("sn-sonar-towed","Towed array","sensors",S,{"sonar":5},{"mass":20,"power":1},{"anti-sub":3},cost=30)
c("sk-radar","Radar seeker","sensors",M,{"radar":4},{"mass":0.02,"power":0.2},{"accuracy":6},cost=0.3,fac="missile-works",work=0.5)
c("sk-ir","Imaging IR seeker","sensors",M,{"ir-systems":6},{"mass":0.02,"power":0.2},{"accuracy":7,"jam-resistance":2},cost=0.4,fac="missile-works",work=0.6)
c("sk-ir8","IR seeker (level 8)","sensors",M,{"ir-systems":8},{"mass":0.02,"power":0.2},{"accuracy":8,"jam-resistance":3},cost=0.6,fac="missile-works",work=0.8)
c("sk-emitter","Emitter-homing seeker","sensors",M,{"ew-systems":4},{"mass":0.02,"power":0.1},{"accuracy":6},cost=0.3)
# computing
c("cp-cms-basic","Combat management (basic)","computing",S+G,{"computing":2},{"mass":5,"power":2,"crew":6},{"fire-channels":2,"networking":2},cost=20,offers=["datalink-bus","fire-control"])
c("cp-cms-integrated","Integrated combat system","computing",S+G,{"computing":6},{"mass":10,"power":6,"cooling":2,"crew":10},{"fire-channels":8,"networking":6},cost=150,fac="avionics-factory",work=10,offers=["datalink-bus","fire-control"])
c("cp-mission","Mission computer","computing",A,{"computing":4},{"mass":0.05,"power":1},{"networking":3},cost=2,fac="avionics-factory",work=1,offers=["datalink-bus","weapon-bus"])
c("cp-fusion","Sensor fusion computer","computing",A+S,{"computing":7},{"mass":0.1,"power":2,"cooling":1},{"detection":1,"networking":6},cost=8,fac="avionics-factory",work=3,offers=["datalink-bus"])
c("cp-flight","Munition flight computer","computing",M,{"computing":3},{"mass":0.005,"power":0.1},{"accuracy":1},cost=0.05)
# comms
c("cm-radio","Tactical radios","comms",S+A+G,{"datalinks":1},{"mass":0.1,"power":0.5},{"networking":1},cost=1)
c("cm-datalink","Tactical datalink","comms",S+A+G,{"datalinks":4},{"mass":0.1,"power":1,"data":-4},{"networking":4},cost=3,needs=["datalink-bus"])
c("cm-satcom","Satellite communications","comms",S+A+G,{"datalinks":6},{"mass":0.3,"power":2,"data":-8},{"networking":6},cost=6,needs=["datalink-bus"])
c("cm-weapon-link","Weapon datalink","comms",M,{"datalinks":5},{"mass":0.005,"power":0.1},{"accuracy":1,"jam-resistance":1},cost=0.1)
# ew
c("ew-chaff","Chaff & flares","ew",S+A+G,{"ew-systems":1},{"mass":0.5},{"ew":1,"survivability":1},cost=1)
c("ew-jammer","Self-protection jammer","ew",S+A+G,{"ew-systems":4},{"mass":1,"power":3,"cooling":1},{"ew":3},cost=12,fac="avionics-factory",work=3)
c("ew-decoys","Offboard decoys","ew",S,{"ew-systems":6},{"mass":5},{"ew":2,"survivability":1},cost=20)
c("ew-eccm","ECCM package","ew",S+A+M+G,{"ew-systems":5},{"mass":0.01,"power":0.2},{"jam-resistance":2},cost=0.5)
# payload
c("pl-blast","Blast warhead","payload",M,{"payloads":1},{"mass":0.2,"volume":1},{"punch":5},cost=0.05,fac="missile-works",work=0.3)
c("pl-penetrator","Penetrator warhead","payload",M,{"payloads":4},{"mass":0.25,"volume":1},{"punch":7},cost=0.1,fac="missile-works",work=0.4)
c("pl-shaped","Shaped anti-ship warhead","payload",M,{"payloads":3},{"mass":0.2,"volume":1},{"punch":6,"anti-ship":3},cost=0.08,fac="missile-works",work=0.3)
c("pl-frag","Fragmentation warhead","payload",M,{"payloads":2},{"mass":0.05,"volume":1},{"punch":3,"air-defence":3},cost=0.04,fac="missile-works",work=0.2)
# ballistic and interceptor parts (game ratings only)
c("pr-rocket-2stage","Two-stage rocket motor","propulsion",M,{"rocket-motors":5},{"mass":1.5,"volume":4},{"speed":9,"reach":7},cost=0.6,fac="missile-works",work=1.5)
c("pr-rocket-mrbm","Medium-range rocket motor","propulsion",M,{"rocket-motors":7},{"mass":2.2,"volume":5},{"speed":10,"reach":10},cost=1.0,fac="missile-works",work=2.5)
c("pr-rocket-exo","Exo-atmospheric booster","propulsion",M,{"rocket-motors":8},{"mass":0.35,"volume":2.5},{"speed":10,"reach":10},cost=0.8,fac="missile-works",work=2)
c("pr-piston-drone","Small piston engine (long endurance)","propulsion",M,{"jet-engines":1},{"mass":0.1,"volume":2},{"speed":1,"reach":10},cost=0.05,fac="missile-works",work=0.2)
c("pr-cruise-lr","Long-range cruise turbojet","propulsion",M,{"jet-engines":4},{"mass":0.3,"volume":2},{"speed":3,"reach":10},cost=0.5,fac="missile-works",work=1)
c("pr-rocket-dual","Dual-pulse rocket motor","propulsion",M,{"rocket-motors":5},{"mass":0.3,"volume":2},{"speed":9,"reach":5},cost=0.3,fac="missile-works",work=0.8)
c("gc-rv-manoeuvre","Manoeuvring re-entry body","guidance",M,{"flight-control":6},{"mass":0.3,"volume":1,"power":0.2},{"agility":4,"accuracy":2},cost=0.3,fac="missile-works",work=0.6)
c("ew-penaids","Penetration aids (decoys)","ew",M,{"ew-systems":4},{"mass":0.3,"volume":2},{"ew":3},cost=0.2,fac="missile-works",work=0.3)
c("pl-hit","Hit-to-kill vehicle","payload",M,{"payloads":5,"flight-control":5},{"mass":0.04,"volume":1,"power":0.2},{"punch":2,"accuracy":8,"agility":5},cost=0.3,fac="missile-works",work=0.6)
# fuzing
c("fz-impact","Impact fuze","fuzing",M,{"fuzing":1},{},{},cost=0.005)
c("fz-proximity","Proximity fuze","fuzing",M,{"fuzing":3},{"power":0.05},{"accuracy":1},cost=0.02)
c("fz-delay","Delayed fuze","fuzing",M,{"fuzing":4},{},{"punch":1},cost=0.02)
# thermal
c("th-cooling","Liquid cooling plant","thermal",S+G,{"thermal":3},{"mass":20,"power":2,"cooling":-12},{},cost=15)
c("th-cooling-adv","High-capacity cooling","thermal",S+G,{"thermal":6},{"mass":30,"power":3,"cooling":-25},{},cost=40)
c("th-air-ecs","Aircraft thermal management","thermal",A,{"thermal":4},{"mass":0.2,"power":1,"cooling":-6},{},cost=3)
# interfaces
c("if-cell-block","Launch cell block (8)","interfaces",S+G,{"structures":4},{"mass":30,"volume":2},{},port="cell-block",cost=12,fac="shipyard",work=3,offers=["launch-cell"])
c("if-rail","Launch rails","interfaces",A+G,{"structures":1},{"mass":0.1},{},cost=0.2,offers=["launch-rail"])
c("if-bay","Internal weapons bay","interfaces",A,{"low-observables":5},{"mass":0.3,"volume":1},{"signature":1},cost=4,offers=["launch-bay"])
c("if-canister","Launch canisters (4)","interfaces",S+G,{"structures":2},{"mass":8,"volume":1},{},port="deck-launcher",cost=4,offers=["launch-cell"])
# reliability / manufacturing / testing / doctrine (programme-level components)
c("rl-modular","Modular maintenance design","reliability",S+A+G,{"testing":3},{"volume":1},{"reliability":7},cost=5)
c("rl-basic","Basic maintainability","reliability",S+A+M+G,{},{},{"reliability":5},cost=0)
c("mf-automated","Automated production process","manufacturing",S+A+M+G,{"manufacturing":6},{},{},cost=2)
c("ts-extended","Extended test campaign","testing",S+A+M+G,{"testing":4},{},{"reliability":1},cost=10)
c("dc-sop","Standard operating procedures","doctrine",S+A+G,{"doctrine":2},{},{"reliability":1},cost=1)
# weapons & crew fittings
c("wp-gun-medium","Medium gun","payload",S,{"structures":2},{"mass":40,"volume":2,"crew":6},{"strike":1,"air-defence":1},port="fore-gun",cost=15)
c("wp-point-defence","Point-defence mount","payload",S+G,{"radar":4},{"mass":10,"power":2},{"air-defence":2},port="pd-mount",cost=20)
c("sy-hangar","Helicopter hangar","structure",S,{"structures":2},{"mass":150,"volume":4,"crew":20},{"anti-sub":2},port="hangar",cost=20)
c("sy-crew-std","Standard crew spaces","structure",S,{},{"volume":2,"crew":-240},{},cost=10)
c("sy-crew-small","Small crew spaces","structure",S,{},{"volume":1,"crew":-60},{},cost=4)
dump("components.json", C)

def fr(i,n,kind,typ,budgets,ports,base=None,req=None,cost=0,fac=None,work=0):
    return {"id":i,"name":n,"kind":kind,"type":typ,"budgets":budgets,"ports":ports,"base":base or {},"requires":req or {},"cost":cost,"factory":fac,"workUnits":work}
SH=[]
ships=[("patrol-boat","Patrol boat",300,3,1),("fac","Fast attack craft",500,4,1),("corvette","Corvette",1800,8,2),("opv","Offshore patrol vessel",2000,8,1),
 ("frigate-asw","Anti-submarine frigate",4500,14,3),("frigate-gp","General-purpose frigate",5000,15,3),("destroyer-ad","Air-defence destroyer",8500,32,4),
 ("destroyer-gp","Multi-mission destroyer",7500,30,4),("cruiser","Cruiser",11000,38,5),("carrier-cat","Catapult carrier",90000,80,4),
 ("carrier-stovl","STOVL carrier",40000,50,3),("amphib-assault","Amphibious assault ship",40000,45,2),("landing-dock","Dock landing ship",20000,30,2),
 ("mine-warfare","Mine countermeasures vessel",700,4,1),("intel-ship","Intelligence collection ship",3000,10,1),("command-ship","Command ship",18000,30,2),
 ("oiler","Fleet oiler",40000,20,1),("stores-ship","Ammunition & stores ship",40000,20,1),("hospital-ship","Hospital ship",20000,20,1),
 ("icebreaker","Icebreaking patrol ship",6000,12,1),("ssk","Diesel-electric submarine",3000,10,1),("ssn","Nuclear attack submarine",8000,16,2),
 ("ssgn","Guided-missile submarine",17000,24,4),("ssbn","Deterrent submarine",18000,24,2),("usv","Uncrewed surface vessel",500,3,1),("xluuv","Large uncrewed underwater vehicle",80,2,0),
 ("dinghy","Rigid inflatable boat",5,1,0),("midget-sub","Midget submarine",150,3,0),("trimaran","Littoral combat ship (trimaran)",3200,14,1),
 ("catamaran","Fast attack catamaran",250,4,1),("destroyer-stealth","Stealth destroyer (tumblehome)",15000,40,5),("frigate-stealth","Stealth frigate",4200,15,2)]
# hull outline for the designer: length and beam in metres, drawing style
HULL={"patrol-boat":(40,7,"warship"),"fac":(55,8.5,"warship"),"corvette":(90,13,"warship"),"opv":(85,13,"warship"),"frigate-asw":(135,17,"warship"),
 "frigate-gp":(140,18,"warship"),"destroyer-ad":(155,20,"warship"),"destroyer-gp":(150,19,"warship"),"cruiser":(173,17,"warship"),"carrier-cat":(333,41,"carrier"),
 "carrier-stovl":(280,39,"carrier"),"amphib-assault":(257,32,"carrier"),"landing-dock":(208,32,"merchant"),"mine-warfare":(60,10,"warship"),"intel-ship":(90,14,"merchant"),
 "command-ship":(190,25,"merchant"),"oiler":(230,32,"merchant"),"stores-ship":(210,32,"merchant"),"hospital-ship":(270,32,"merchant"),"icebreaker":(110,25,"merchant"),
 "ssk":(70,8,"sub"),"ssn":(115,10,"sub"),"ssgn":(170,13,"sub"),"ssbn":(170,13,"sub"),"usv":(45,7,"warship"),"xluuv":(16,2.5,"sub"),
 "dinghy":(8,3,"boat"),"midget-sub":(28,3.5,"sub"),"trimaran":(127,31.6,"trimaran"),"catamaran":(50,12,"catamaran"),"destroyer-stealth":(186,24.6,"tumblehome"),"frigate-stealth":(125,16,"tumblehome")}
for i,n,disp,vol,cells in ships:
    usable = disp*0.22
    SH.append(fr(f"hull-{i}",n,"ship",i,{"mass":round(usable),"volume":vol,"power":0,"cooling":4,"crew":0,"data":2},
      {"mast-sensor":1 if not i.startswith(("ss","xl")) else 0,"cell-block":cells,"deck-launcher":2,"fore-gun":1 if disp>1000 and not i.startswith("ss") else 0,
       "pd-mount":2 if disp>1500 else 0,"hangar":2 if i.startswith(("carrier","amphib")) else (1 if disp>3000 and not i.startswith("ss") else 0),"bow-sonar":1},
      {"survivability":min(6,1+disp/4000),"endurance":2},{"structures":2},round(disp*0.08),"shipyard",round(disp/300)))
    SH[-1]["budgets"]["topside"]=round(usable*0.25)
    L,B,st=HULL[i]; SH[-1]["hull"]={"length":L,"beam":B,"style":st}
    if st in ("trimaran","catamaran"): SH[-1]["hull"]={"length":L,"beam":B*0.3,"span":B,"style":st}
dump("frames/ships.json", SH)
AF=[
 fr("af-light-fighter","Light fighter airframe","aircraft","light-multirole",{"mass":6,"volume":6,"power":0,"cooling":2,"data":1},{"nose-sensor":1},{"agility":3,"payload":3},{"structures":3},20,"aerospace-assembly",20),
 fr("af-heavy-fighter","Heavy fighter airframe","aircraft","heavy-fighter",{"mass":10,"volume":9,"power":0,"cooling":3,"data":1},{"nose-sensor":1},{"agility":2,"payload":5,"endurance":2},{"structures":4},35,"aerospace-assembly",30),
 fr("af-stealth-fighter","Low-observable fighter airframe","aircraft","stealth-multirole",{"mass":9,"volume":8,"power":0,"cooling":2,"data":2},{"nose-sensor":1},{"agility":2,"payload":3,"signature":3},{"structures":6,"low-observables":5},60,"aerospace-assembly",45),
 fr("af-flying-wing","Flying-wing bomber airframe","aircraft","stealth-bomber",{"mass":30,"volume":20,"power":0,"cooling":4,"data":2},{"nose-sensor":1},{"payload":8,"endurance":5,"signature":4},{"structures":7,"low-observables":7,"flight-control":7},300,"aerospace-assembly",200),
 fr("af-carrier-fighter","Carrier fighter airframe","aircraft","carrier-multirole",{"mass":9,"volume":8,"power":0,"cooling":2,"data":1},{"nose-sensor":1},{"agility":2,"payload":4},{"structures":4},40,"aerospace-assembly",32),
 fr("af-large","Large support airframe","aircraft","support",{"mass":40,"volume":25,"power":0,"cooling":6,"data":4},{"nose-sensor":1},{"endurance":6,"payload":4},{"structures":3},150,"aerospace-assembly",80),
 fr("af-uav","Uncrewed airframe","aircraft","ucav",{"mass":3,"volume":4,"power":0,"cooling":1,"data":1},{"nose-sensor":1},{"endurance":6,"payload":2},{"structures":3,"autonomy":4},10,"aerospace-assembly",8),
 fr("af-hale","High-altitude long-endurance (HALE) airframe","aircraft","hale",{"mass":6,"volume":8,"power":0,"cooling":2,"data":2},{"nose-sensor":1},{"endurance":9,"payload":2},{"structures":4,"autonomy":5},40,"aerospace-assembly",30),
 fr("af-male","Medium-altitude long-endurance (MALE) airframe","aircraft","male",{"mass":2,"volume":4,"power":0,"cooling":1,"data":1},{"nose-sensor":1},{"endurance":7,"payload":2},{"structures":3,"autonomy":4},12,"aerospace-assembly",10),
 fr("af-ucav-wing","Low-observable UCAV (flying wing)","aircraft","ucav-lo",{"mass":5,"volume":6,"power":0,"cooling":2,"data":2},{"nose-sensor":1},{"signature":4,"payload":3,"endurance":4},{"structures":5,"low-observables":6,"autonomy":6},45,"aerospace-assembly",35),
 fr("af-delta-heavy","Heavy delta interceptor airframe","aircraft","interceptor",{"mass":12,"volume":10,"power":0,"cooling":3,"data":1},{"nose-sensor":1},{"agility":1,"payload":4,"endurance":3},{"structures":4},45,"aerospace-assembly",35),
 fr("af-canard-delta","Canard-delta fighter airframe","aircraft","multirole",{"mass":8,"volume":7,"power":0,"cooling":2,"data":1},{"nose-sensor":1},{"agility":3,"payload":4},{"structures":5},40,"aerospace-assembly",32),
 fr("af-lo-heavy","Low-observable heavy fighter airframe","aircraft","stealth-air-superiority",{"mass":11,"volume":9,"power":0,"cooling":3,"data":2},{"nose-sensor":1},{"agility":3,"payload":3,"signature":3},{"structures":6,"low-observables":6},70,"aerospace-assembly",55)]
# planform for the designer: length, fuselage width, span, style
for f,(L,B,S,st) in zip(AF,[(15,1.8,10,"delta"),(19,2.4,13,"fighter"),(15.7,2.4,11,"stealth"),(21,4,52,"flying-wing"),(17,2.2,12.5,"fighter"),(45,3.8,40,"large"),(12,1.2,16,"uav"),
    (14.5,1.2,40,"hale"),(11,1.0,20,"male"),(11.6,2,18.9,"flying-wing"),(22,2.6,13.5,"delta"),(15.3,2,10.9,"canard-delta"),(20.4,2.6,13.5,"stealth")]):
    f["hull"]={"length":L,"beam":B,"span":S,"style":st}
dump("frames/aircraft.json", AF)
MB=[
 fr("mb-small","Small munition body","munition","small",{"mass":0.3,"volume":4,"power":0,"cooling":0},{},{"reach":0},{"structures":1},0.1,"missile-works",0.5),
 fr("mb-medium","Medium munition body","munition","medium",{"mass":0.6,"volume":6,"power":0,"cooling":0},{},{},{"structures":2},0.2,"missile-works",1),
 fr("mb-large","Large munition body","munition","large",{"mass":1.2,"volume":8,"power":0,"cooling":0},{},{},{"structures":2},0.4,"missile-works",2),
 fr("mb-heavy","Heavy munition body","munition","heavy",{"mass":4,"volume":10,"power":0,"cooling":0},{},{},{"structures":3},1,"missile-works",4)]
for f,(L,B,S) in zip(MB,[(3,0.2,0.6),(5,0.35,1.0),(6.2,0.5,1.4),(12,1.0,2.4)]):
    f["hull"]={"length":L,"beam":B,"span":S,"style":"missile"}
dump("frames/munitions.json", MB)
GR=[
 fr("gc-truck-6x6","6×6 truck chassis","ground","launcher-vehicle",{"mass":18,"volume":4,"power":0,"cooling":2,"crew":6},{"mast-sensor":1,"deck-launcher":2},{"mobility":2},{"structures":1},2,"vehicle-plant",3),
 fr("gc-truck-8x8","8×8 heavy chassis","ground","launcher-vehicle",{"mass":30,"volume":6,"power":0,"cooling":3,"crew":8},{"mast-sensor":1,"deck-launcher":4,"cell-block":1},{"mobility":1},{"structures":2},4,"vehicle-plant",5),
 fr("gc-tracked","Tracked chassis","ground","launcher-vehicle",{"mass":24,"volume":5,"power":0,"cooling":2,"crew":6},{"mast-sensor":1,"deck-launcher":2},{"mobility":1,"survivability":2},{"structures":2},5,"vehicle-plant",6),
 fr("gc-radar-trailer","Radar site vehicle set","ground","radar-unit",{"mass":230,"volume":8,"power":0,"cooling":4,"crew":10},{"mast-sensor":1},{"mobility":0},{"structures":1},2,"vehicle-plant",2),
 fr("gc-fixed-site","Fixed site","ground","fixed-site",{"mass":400,"volume":20,"power":0,"cooling":10,"crew":40},{"mast-sensor":2,"cell-block":4,"pd-mount":2},{"survivability":3},{},30,"vehicle-plant",20)]
for g,(L,B,st) in zip(GR,[(10,2.5,"vehicle"),(12,2.7,"vehicle"),(7.5,3.4,"tracked"),(14,3,"vehicle"),(60,60,"pad")]):
    g["hull"]={"length":L,"beam":B,"style":st}; g["budgets"]["topside"]=round(g["budgets"]["mass"]*0.5)
dump("frames/ground.json", GR)

dump("theatres.json", [{"id":i,"name":n} for i,n in [("NATL","North Atlantic"),("ARCT","Arctic"),("MED","Mediterranean"),("RED","Red Sea"),("GULF","Persian Gulf"),
 ("IND","Indian Ocean"),("SCS","South China Sea"),("ECS","East China Sea"),("WPAC","Western Pacific"),("NPAC","North Pacific"),("BLK","Black Sea"),("BALT","Baltic")]])
R=lambda i,n,path,th,ck,tp,days:{"id":i,"name":n,"path":path,"theatres":th,"chokepoints":ck,"throughput":tp,"transitDays":days}
dump("trade_routes.json", [
 R("east-asia-europe","East Asia – Europe (Suez)",[[122.5,31],[123,27],[119,22],[114,17],[108,10],[104,1.3],[97,5],[80,5.5],[58,13],[43.4,12.6],[38,21],[32.5,30],[25,34],[5,37],[-5.6,36],[-10,40],[-9,46],[-5,48.5],[-1,50.2],[4,51.9]],["ECS","SCS","IND","RED","MED","NATL"],["Malacca","Bab-el-Mandeb","Suez","Gibraltar"],120,32),
 R("gulf-asia","Persian Gulf – East Asia",[[51.5,26.5],[56.3,26.5],[59,23],[70,15],[80,5.5],[97,5],[104,1.3],[108,10],[114,17],[119,20.5],[122,22],[126,29],[129,33]],["GULF","IND","SCS","ECS"],["Hormuz","Malacca"],100,22),
 R("gulf-europe","Persian Gulf – Europe",[[51.5,26.5],[56.3,26.5],[59,23],[57,17],[51,13],[43.4,12.6],[38,21],[32.5,30],[25,34],[5,37],[-5.6,36],[-10,40],[-9,46],[-5,48.5],[-1,50.2],[4,51.9]],["GULF","IND","RED","MED","NATL"],["Hormuz","Bab-el-Mandeb","Suez","Gibraltar"],80,24),
 R("north-atlantic","North Atlantic",[[-74,40.5],[-50,45],[-25,50],[-5,50],[4,51.9]],["NATL"],["English Channel"],110,12),
 R("north-pacific","North Pacific",[[-122.4,37.8],[-150,45],[170,45],[140,35]],["NPAC","WPAC"],[],90,16),
 R("east-asia-australia","East Asia – Australia",[[122.5,31],[124,26],[122.5,20],[125,12],[119.5,4],[118.5,-3],[117,-8.5],[125,-14],[150,-24],[153,-30],[151.3,-34]],["ECS","SCS","WPAC"],["Taiwan Strait","Lombok"],50,14),
 R("black-sea","Black Sea – Mediterranean",[[30.7,46.5],[29,41.2],[26.4,40.2],[25,34]],["BLK","MED"],["Bosporus","Dardanelles"],30,4),
 R("baltic","Baltic – North Sea",[[24.7,59.6],[19,57.5],[14.5,55.5],[12.7,55.9],[11.5,57.2],[10.7,58],[8,57.9],[6.5,55],[4,51.9]],["BALT","NATL"],["Danish Straits"],40,5),
 R("arctic","Northern Sea Route",[[33,69],[60,74],[100,77],[150,72],[-169,66]],["ARCT"],["Bering Strait"],15,20),
 R("cape-route","Cape of Good Hope",[[58,13],[52,5],[42,-12],[36,-25],[20,-36],[12,-20],[3,-2],[-10,5],[-18,15],[-18,28],[-10,34],[-5.6,36]],["IND","NATL"],["Cape of Good Hope"],60,40)])
print("content written")
