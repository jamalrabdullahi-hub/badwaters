import type { Category, Param, Part, Slot } from "../sim/types";

const p = (id: string, name: string, tech: string | undefined, desc: string, a: Part["a"]): Part => ({ id, name, tech, desc, a });
const prm = (id: string, name: string, unit: string, min: number, max: number, step: number, def: number, hint: string): Param => ({
  id, name, unit, min, max, step, def, hint,
});

export interface CategorySpec {
  id: Category;
  name: string;
  plural: string;
  slots: Slot[];
  params: Param[];
  hasLoadout: boolean;
}

// ============================ MISSILES ============================
const missile: CategorySpec = {
  id: "missile",
  name: "Missile",
  plural: "Missiles",
  hasLoadout: false,
  slots: [
    {
      id: "role", name: "Role", parts: [
        p("ashm", "Anti-ship", "mis_solid", "Attacks ships at sea.", { role: "ashm" }),
        p("lacm", "Land-attack cruise", "mis_turbojet", "Attacks bases and fixed sites.", { role: "lacm" }),
        p("sam", "Surface-to-air", "def_sam_m", "Shoots down aircraft, drones and cruise missiles.", { role: "sam" }),
        p("aam", "Air-to-air", "mis_solid", "Carried by fighters against aircraft and drones.", { role: "aam" }),
        p("bmd", "Ballistic missile defence", "def_bmd1", "Interceptor built to hit ballistic warheads.", { role: "bmd" }),
        p("srbm", "Ballistic (land attack)", "mis_srbm", "Very fast, hard to stop, hits bases.", { role: "srbm" }),
        p("asbm", "Anti-ship ballistic", "mis_asbm", "Ballistic missile with a terminal seeker for ships.", { role: "asbm" }),
        p("hgv", "Hypersonic strike", "mis_hgv", "Mach 8+ glide or scramjet weapon for ships and bases.", { role: "hgv" }),
        p("arm", "Anti-radiation", "mis_solid", "Homes on enemy radars: SAM sites and ship radars.", { role: "arm" }),
        p("torp", "Heavyweight torpedo", undefined, "Launched from submarine tubes. Silent until it hits, and only decoys stop it.", { role: "torp" }),
      ],
    },
    {
      id: "prop", name: "Propulsion", parts: [
        p("solid", "Solid rocket", "mis_solid", "Fast (Mach 3–4) but short range for its weight.", { k: 0.1056, e: 1.24, mach: 3.5, costMul: 1.4, kind: "rocket" }),
        p("turbojet", "Turbojet", "mis_turbojet", "Subsonic, efficient. Classic anti-ship missile engine.", { k: 0.472, e: 1, mach: 0.85, costMul: 1.0, kind: "air" }),
        p("turbofan", "Turbofan", "mis_turbofan", "Subsonic and very long range. Cruise missiles.", { k: 2.83, e: 1, mach: 0.75, costMul: 1.1, kind: "air" }),
        p("ramjet", "Ramjet", "mis_ramjet", "Supersonic cruise at Mach 2.5–3.", { k: 0.162, e: 1, mach: 2.8, costMul: 1.6, kind: "air" }),
        p("liquid", "Liquid ballistic booster", "mis_srbm", "Cheap and simple ballistic booster (Scud lineage). Slow to prepare.", { k: 0.0168, e: 1.2, mach: 5, costMul: 0.6, kind: "ballistic" }),
        p("solidb", "Solid ballistic booster", "mis_mrbm", "Solid-fuel ballistic missile. Longer range and always ready.", { k: 0.027, e: 1.2, mach: 6, costMul: 0.9, kind: "ballistic" }),
        p("glide", "Boost-glide vehicle", "mis_hgv", "Rocket boost, then a Mach 9 glide at 40 km altitude.", { k: 0.026, e: 1.2, mach: 9, costMul: 2.2, kind: "glide" }),
        p("scram", "Scramjet", "mis_scram", "Mach 6 air-breathing cruise.", { k: 0.12, e: 1, mach: 6, costMul: 2.4, kind: "air" }),
        p("torpedo", "Torpedo propulsion", undefined, "Thermal or electric underwater propulsion, around 50 knots.", { k: 0.0436, e: 1, mach: 0.075, costMul: 0.8, kind: "torp" }),
      ],
    },
    {
      id: "seeker", name: "Seeker", parts: [
        p("sonar", "Active/passive sonar", undefined, "Torpedo homing head. Only for torpedoes.", { pk: 0.62, sk: 0.4, costMul: 1.0, torpOnly: true }),
        p("ins", "Inertial only", "mis_srbm", "Hits fixed targets. Accuracy suffers at long range.", { pk: 0.55, sk: 0.9, costMul: 0.5, fixedOnly: true }),
        p("sarh", "Semi-active radar", "mis_solid", "Needs the launcher's radar to illuminate until impact. Ties up fire channels.", { pk: 0.55, sk: 0.2, costMul: 1.2, sarh: true }),
        p("ir", "Infrared", "mis_solid", "Heat-seeking. Short range, easily decoyed by flares.", { pk: 0.6, sk: 0.35, costMul: 1.0 }),
        p("arh", "Active radar", "mis_arh", "Fire-and-forget radar homing.", { pk: 0.66, sk: 0.3, costMul: 1.3 }),
        p("gps", "GPS/INS", "mis_gps", "Precise against fixed targets. Useless against ships.", { pk: 0.9, sk: 0.7, costMul: 0.9, fixedOnly: true }),
        p("iir", "Imaging infrared", "mis_iir", "Passive and hard to decoy.", { pk: 0.72, sk: 0.6, costMul: 1.4 }),
        p("rf", "Passive RF (anti-radiation)", "mis_solid", "Homes on radar emissions. Only for anti-radiation missiles.", { pk: 0.62, sk: 0.4, costMul: 1.1, armOnly: true }),
        p("multi", "Multi-mode", "mis_multimode", "Radar + IR + RF. Very hard to spoof.", { pk: 0.8, sk: 0.82, costMul: 1.8 }),
        p("htk", "Hit-to-kill vehicle", "def_htk", "Kinetic kill vehicle for ballistic missile defence.", { pk: 0.78, sk: 0.8, costMul: 2.6, bmd: 0.8 }),
        p("exo", "Exo-atmospheric kill vehicle", "def_exo", "Kills ballistic missiles in space, far from the target.", { pk: 0.82, sk: 0.9, costMul: 6, bmd: 0.88, exo: true }),
        p("gpi", "Glide-phase kill vehicle", "def_glide", "Built to catch manoeuvring hypersonic gliders.", { pk: 0.8, sk: 0.9, costMul: 7, bmd: 0.85, hyper: 0.6 }),
        p("ai", "AI target recognition", "mis_ai", "Autonomous target selection. Ignores decoys.", { pk: 0.88, sk: 0.95, costMul: 2.2 }),
      ],
    },
    {
      id: "airframe", name: "Airframe", parts: [
        p("conv", "Conventional", undefined, "Standard airframe.", { rcsMul: 1, man: 0.1, costMul: 1, struct: 0.3 }),
        p("skim", "Sea-skimming", "mis_seaskim", "Flies at wave-top height, under the radar horizon.", { rcsMul: 1, man: 0.15, costMul: 1.1, struct: 0.3, skim: true }),
        p("weave", "Terminal weave", "mis_ramjet", "High-g evasive moves in the final seconds.", { rcsMul: 1, man: 0.35, costMul: 1.2, struct: 0.32, skim: true }),
        p("lo", "Low-observable", "mis_lo", "Stealth shaping. Defenders detect it very late.", { rcsMul: 0.08, man: 0.2, costMul: 1.8, struct: 0.33, skim: true }),
        p("marv", "Manoeuvring re-entry", "mis_marv", "Ballistic warhead that manoeuvres as it comes down.", { rcsMul: 1, man: 0.5, costMul: 1.5, struct: 0.3 }),
      ],
    },
    {
      id: "link", name: "Datalink", parts: [
        p("none", "None", undefined, "Pre-programmed flight.", { pkAdd: 0, costMul: 1 }),
        p("oneway", "One-way uplink", "sen_link", "Mid-course updates from the launcher.", { pkAdd: 0.04, costMul: 1.05 }),
        p("twoway", "Two-way datalink", "mis_wlink", "Retargeting in flight and reports back what it sees.", { pkAdd: 0.08, costMul: 1.12 }),
      ],
    },
  ],
  params: [
    prm("weight", "Launch weight", "kg", 60, 20000, 10, 700, "Heavier missiles fly farther and hit harder but need bigger launchers. Up to 250 kg: small (4 per VLS cell). Up to 900 kg: tactical. Up to 1,700 kg: strike-length. Heavier: ground TEL or large-diameter cell only."),
    prm("warhead", "Warhead", "kg", 5, 1200, 5, 220, "More warhead means more damage per hit but less fuel and range."),
  ],
};

// ============================ AIRCRAFT ============================
const aircraft: CategorySpec = {
  id: "aircraft",
  name: "Aircraft",
  plural: "Aircraft",
  hasLoadout: true,
  slots: [
    {
      id: "frame", name: "Airframe", parts: [
        p("g3", "3rd-generation fighter", undefined, "MiG-21 / F-4 era. Cheap, big radar signature.", { rcs: 8, agility: 0.4, cap: 8, internal: 0, maxSize: "M", cost: 18, mach: 2.0, radius: 550, fighter: true }),
        p("g4", "4th-generation fighter", "air_gen4", "F-15 / F-16 / Su-27 / MiG-29.", { rcs: 5, agility: 0.7, cap: 12, internal: 0, maxSize: "M", cost: 35, mach: 2.0, radius: 800, fighter: true }),
        p("g45", "4.5-generation fighter", "air_gen45", "Rafale / Typhoon / Super Hornet / J-10C.", { rcs: 1.5, agility: 0.8, cap: 16, internal: 0, maxSize: "L", cost: 70, mach: 1.8, radius: 900, fighter: true }),
        p("g5", "5th-generation fighter", "air_gen5", "F-35 / F-22 / J-20 / Su-57. Internal bays keep it stealthy.", { rcs: 0.005, agility: 0.85, cap: 16, internal: 8, maxSize: "M", cost: 100, mach: 1.7, radius: 1100, fighter: true }),
        p("g6", "6th-generation fighter", "air_gen6", "Tailless air dominance fighter that controls drone wingmen.", { rcs: 0.0015, agility: 0.9, cap: 20, internal: 16, maxSize: "L", cost: 260, mach: 2.0, radius: 1600, fighter: true }),
        p("large", "Large support airframe", "air_aew", "Airliner-sized: AEW&C, tanker or maritime patrol.", { rcs: 60, agility: 0.1, cap: 8, internal: 8, maxSize: "M", cost: 180, mach: 0.75, radius: 1800, fighter: false }),
        p("bomber", "Heavy bomber", undefined, "B-52 / Tu-95 / Tu-22M / H-6. Carries heavy cruise missiles.", { rcs: 80, agility: 0.1, cap: 48, internal: 16, maxSize: "XL", cost: 220, mach: 0.9, radius: 3500, fighter: false }),
        p("lobomber", "Stealth bomber", "air_bomber_lo", "B-2 / B-21 / H-20. Penetrates defended airspace.", { rcs: 0.001, agility: 0.1, cap: 32, internal: 32, maxSize: "XL", cost: 900, mach: 0.85, radius: 4500, fighter: false }),
      ],
    },
    {
      id: "role", name: "Mission", parts: [
        p("as", "Air superiority", undefined, "Flies combat air patrol and hunts enemy aircraft and cruise missiles.", { mission: "cap" }),
        p("mr", "Multirole", undefined, "Fights air-to-air, then strikes with what's left.", { mission: "multi" }),
        p("st", "Strike", undefined, "Flies to launch range of its target and fires stand-off weapons.", { mission: "strike" }),
        p("aew", "AEW&C", "air_aew", "Flying radar that shares tracks with the whole force.", { mission: "aew" }),
        p("mpa", "Maritime patrol", undefined, "Finds ships and submarines, carries anti-ship missiles.", { mission: "mpa" }),
      ],
    },
    {
      id: "engine", name: "Engines", parts: [
        p("tj", "Turbojets", undefined, "Thirsty and loud.", { radiusMul: 0.85, machMul: 1, costMul: 0.9 }),
        p("tf", "Low-bypass turbofans", "air_gen4", "Standard fighter engines.", { radiusMul: 1, machMul: 1, costMul: 1 }),
        p("sc", "Supercruise turbofans", "air_gen5", "Mach 1.5 without afterburner.", { radiusMul: 1.08, machMul: 1.1, costMul: 1.2 }),
        p("aca", "Adaptive-cycle engines", "air_adaptive", "Around 30% more range and more power for sensors.", { radiusMul: 1.3, machMul: 1.1, costMul: 1.4 }),
      ],
    },
    {
      id: "radar", name: "Radar", parts: [
        p("pd", "Pulse-Doppler", "sen_pd", "Mechanically scanned look-down radar.", { km: 90, ch: 1, costAdd: 3 }),
        p("pesa", "PESA", "sen_pesa", "Electronic scanning with several targets at once.", { km: 120, ch: 2, costAdd: 6 }),
        p("aesa", "AESA", "sen_aesa", "Long range, many channels, hard to detect.", { km: 160, ch: 4, costAdd: 10 }),
        p("gan", "GaN AESA", "sen_gan", "Next-generation array.", { km: 210, ch: 6, costAdd: 14 }),
        p("dome", "Rotodome AEW radar", "air_aew", "Only for AEW&C: sees low fliers 350 km away.", { km: 350, ch: 0, costAdd: 60, aew: true }),
        p("aewa", "AESA AEW radar", "air_aesaaew", "Only for AEW&C: E-7 / E-2D class.", { km: 470, ch: 0, costAdd: 90, aew: true }),
      ],
    },
    {
      id: "basing", name: "Basing", parts: [
        p("land", "Land-based", undefined, "Needs a runway.", { carrier: "none", costMul: 1 }),
        p("stovl", "STOVL", undefined, "Short take-off, vertical landing. Flies from small carriers and amphibious ships. Supersonic STOVL needs the tech.", { carrier: "stovl", costMul: 1.25, radiusMul: 0.8 }),
        p("stobar", "Carrier (ski-jump)", undefined, "Navalised for ski-jump carriers: shorter range and lighter take-off weight.", { carrier: "stobar", costMul: 1.1, radiusMul: 0.8 }),
        p("catobar", "Carrier (catapult)", "nav_catobar", "Navalised for catapult carriers.", { carrier: "catobar", costMul: 1.12, radiusMul: 0.95 }),
      ],
    },
    {
      id: "ew", name: "Self-protection", parts: [
        p("none", "Chaff & flares", undefined, "Basic countermeasures.", { ew: 0.1, costAdd: 0 }),
        p("jam", "Internal jammer", "sen_ew", "Degrades enemy fire-control radars.", { ew: 0.25, costAdd: 4 }),
        p("drfm", "DRFM suite", "sen_drfm", "Spoofs radars and radar seekers.", { ew: 0.4, costAdd: 8 }),
      ],
    },
  ],
  params: [
    prm("payload", "Payload", "kg", 0, 30000, 100, 4000, "Weapons weight. Anything beyond the internal bay capacity hangs outside and ruins stealth."),
    prm("sqn", "Squadron size", "aircraft", 2, 24, 1, 12, "Aircraft per squadron. The squadron is the unit you buy and base."),
  ],
};

// ============================ SHIPS ============================
const ship: CategorySpec = {
  id: "ship",
  name: "Ship",
  plural: "Ships",
  hasLoadout: true,
  slots: [
    {
      id: "hull", name: "Hull type", parts: [
        p("fac", "Fast attack craft", undefined, "Missile boat. Cheap, fast, fragile.", { minD: 150, maxD: 700, spd: 38, cpt: 0.1, deck: "none", sub: false, usable: 0.3 }),
        p("cor", "Corvette", undefined, "Coastal combatant.", { minD: 700, maxD: 2500, spd: 30, cpt: 0.12, deck: "helo", sub: false, usable: 0.2 }),
        p("ffg", "Frigate", undefined, "Escort and ASW workhorse.", { minD: 2500, maxD: 7000, spd: 29, cpt: 0.11, deck: "helo", sub: false, usable: 0.2 }),
        p("ddg", "Destroyer", undefined, "Multi-mission air-defence and strike ship.", { minD: 6000, maxD: 13000, spd: 31, cpt: 0.11, deck: "helo", sub: false, usable: 0.21 }),
        p("cg", "Cruiser", undefined, "Large missile ship and air-defence commander.", { minD: 9000, maxD: 28000, spd: 31, cpt: 0.1, deck: "helo", sub: false, usable: 0.22 }),
        p("lhd", "Amphibious assault ship", undefined, "Big-deck amphib. Operates helicopters and STOVL jets.", { minD: 18000, maxD: 45000, spd: 22, cpt: 0.065, deck: "stovl", sub: false, usable: 0.3 }),
        p("cvl", "Light / ski-jump carrier", undefined, "STOVL or ski-jump carrier.", { minD: 18000, maxD: 70000, spd: 28, cpt: 0.07, deck: "stovl", sub: false, usable: 0.3 }),
        p("cvn", "Fleet carrier (catapults)", "nav_catobar", "Full-size catapult carrier.", { minD: 30000, maxD: 110000, spd: 31, cpt: 0.045, deck: "catobar", sub: false, usable: 0.3 }),
        p("ssk", "Diesel submarine", undefined, "Quiet coastal submarine. Hard to find.", { minD: 400, maxD: 4500, spd: 20, cpt: 0.22, deck: "none", sub: true, usable: 0.12 }),
        p("ssn", "Nuclear attack submarine", "nav_nuke", "Fast, unlimited range, cruise-missile capable.", { minD: 2500, maxD: 26000, spd: 30, cpt: 0.3, deck: "none", sub: true, usable: 0.1 }),
      ],
    },
    {
      id: "prop", name: "Propulsion", parts: [
        p("steam", "Steam turbines", undefined, "Old but powerful.", { spdMul: 1, mw: 0.8, costMul: 0.9, sub: false }),
        p("cogag", "Gas turbines (COGAG)", "nav_gasturb", "Standard modern warship plant.", { spdMul: 1.02, mw: 1.3, costMul: 1, sub: false }),
        p("diesel", "Diesel-electric", undefined, "For submarines and patrol craft. Economical but slow.", { spdMul: 0.9, mw: 0.6, costMul: 0.8, sub: true }),
        p("aip", "Air-independent (AIP)", "nav_aip", "Submarine stays submerged for weeks. Quieter.", { spdMul: 0.95, mw: 0.7, costMul: 1.1, sub: true, quiet: 0.3 }),
        p("iep", "Integrated electric", "nav_iep", "Pools all power for big radars, lasers and railguns.", { spdMul: 1.0, mw: 5, costMul: 1.2, sub: false }),
        p("nuke", "Nuclear reactor", "nav_nuke", "Unlimited range and lots of power.", { spdMul: 1.05, mw: 3, costMul: 1.9, sub: true }),
      ],
    },
    {
      id: "radar", name: "Radar", parts: [
        p("r2d", "2D air search", undefined, "Rotating search radar.", { km: 150, ch: 1, mw: 0.3, w: 20, cost: 20 }),
        p("r3d", "3D search radar", "sen_pd", "Height-finding search radar.", { km: 200, ch: 2, mw: 0.6, w: 35, cost: 50 }),
        p("pesa", "PESA multifunction", "sen_pesa", "SPY-1 class. Tracks hundreds of targets.", { km: 320, ch: 6, mw: 2, w: 120, cost: 250 }),
        p("aesa", "AESA multifunction", "sen_aesa", "Sampson / APAR / Type 346 class.", { km: 380, ch: 10, mw: 3, w: 110, cost: 300 }),
        p("gan", "GaN AESA", "sen_gan", "SPY-6 class: large jump in range and sensitivity.", { km: 480, ch: 14, mw: 4.5, w: 130, cost: 380 }),
      ],
    },
    {
      id: "cms", name: "Combat system", parts: [
        p("man", "Manual plot", undefined, "Operators track targets by hand.", { ch: 2, link: 0, cost: 10 }),
        p("ntds", "Automated (NTDS)", "sen_link", "Computer-assisted with datalink.", { ch: 4, link: 1, cost: 60 }),
        p("aegis", "Integrated (Aegis class)", "nav_combat", "Fully automatic air defence.", { ch: 12, link: 1, cost: 200 }),
        p("cec", "CEC-enabled", "sen_cec", "Fires on tracks held by any unit in the force.", { ch: 16, link: 2, cost: 260 }),
        p("ai", "AI battle management", "sen_aibm", "Machine-speed threat evaluation.", { ch: 24, link: 2, cost: 320 }),
      ],
    },
    {
      id: "launcher", name: "Missile launchers", parts: [
        p("tubes", "Torpedo tubes", undefined, "Submarine tubes: torpedoes, encapsulated anti-ship and cruise missiles. Cells = magazine rounds.", { unit: 1, maxSize: "L", wPer: 3, costPer: 1, rate: 0.3 }),
        p("rail", "Rail launchers", undefined, "Twin-arm launchers. Slow to reload. Each 'cell' is a magazine round.", { unit: 2, maxSize: "M", wPer: 2.5, costPer: 0.8, rate: 0.5 }),
        p("heavy", "Heavy deck launchers", undefined, "Soviet-style inclined box launchers for big anti-ship missiles. Slow, heavy, hard to reload.", { unit: 8, maxSize: "XL", wPer: 14, costPer: 3, rate: 0.4 }),
        p("vls", "Tactical VLS", "nav_vls", "Standard cells for medium missiles.", { unit: 2, maxSize: "M", wPer: 2.8, costPer: 1.2, rate: 1 }),
        p("vlssl", "Strike-length VLS", "nav_vls_sl", "Long cells for cruise missiles and big interceptors.", { unit: 4, maxSize: "L", wPer: 3.6, costPer: 1.6, rate: 1 }),
        p("ldvls", "Large-diameter VLS", "nav_ldvls", "Fits hypersonic and ballistic weapons.", { unit: 8, maxSize: "XL", wPer: 9, costPer: 4, rate: 1 }),
      ],
    },
    {
      id: "pd", name: "Point defence", parts: [
        p("none", "None", undefined, "No close-in defence.", { km: 0, pk: 0, mw: 0, w: 0, cost: 0 }),
        p("gun", "Gun CIWS ×2", "def_ciws", "Phalanx / Goalkeeper / AK-630.", { km: 2, pk: 0.45, mw: 0.1, w: 16, cost: 20 }),
        p("ram", "Missile CIWS ×2", "def_ram", "RAM / HQ-10.", { km: 9, pk: 0.6, mw: 0.1, w: 20, cost: 40 }),
        p("laser1", "Laser 60 kW + gun", "adv_laser1", "Cheap shots against drones and boats.", { km: 5, pk: 0.55, mw: 0.5, w: 30, cost: 60, laser: 60 }),
        p("laser2", "Laser 300 kW + RAM", "adv_laser2", "Burns down subsonic missiles.", { km: 9, pk: 0.72, mw: 2.2, w: 45, cost: 120, laser: 300 }),
        p("rail", "Railgun + laser", "adv_railgun", "Guided hypervelocity rounds plus a laser.", { km: 18, pk: 0.8, mw: 6, w: 90, cost: 260, laser: 300 }),
        p("laser3", "Megawatt laser", "adv_laser3", "Kills supersonic missiles at the horizon.", { km: 20, pk: 0.86, mw: 12, w: 80, cost: 320, laser: 1000 }),
      ],
    },
    {
      id: "ew", name: "Electronic warfare", parts: [
        p("chaff", "Chaff launchers", undefined, "Basic decoys.", { ew: 0.12, cost: 5 }),
        p("jam", "Active jammer", "sen_ew", "SLQ-32 class.", { ew: 0.22, cost: 30 }),
        p("decoy", "Jammer + offboard decoys", "def_decoy", "Nulka-type hovering decoys.", { ew: 0.34, cost: 60 }),
        p("drfm", "DRFM suite + decoys", "sen_drfm", "Spoofs seekers and fire-control radars.", { ew: 0.45, cost: 90 }),
      ],
    },
    {
      id: "sig", name: "Signature", parts: [
        p("conv", "Conventional", undefined, "Boxy superstructure.", { rcsMul: 1, costMul: 1 }),
        p("red", "Reduced signature", "nav_stealth", "Angled surfaces and covered fittings.", { rcsMul: 0.3, costMul: 1.06 }),
        p("lo", "Stealth hull", "nav_stealth", "Zumwalt-style tumblehome. Seekers struggle to lock.", { rcsMul: 0.08, costMul: 1.18 }),
      ],
    },
  ],
  params: [
    prm("disp", "Displacement", "t", 150, 110000, 50, 8500, "Full-load tonnage. Must fit the hull type's range."),
    prm("cells", "Missile cells", "cells", 0, 160, 1, 64, "Launcher cells, or magazine rounds for rails and tubes. One missile per cell. Small (S) missiles quad-pack four to a cell."),
    prm("canisters", "Deck canisters", "tubes", 0, 16, 1, 8, "Extra deck box launchers. They add cells of the same size class."),
    prm("wing", "Air wing", "aircraft", 0, 90, 1, 2, "Aircraft carried. Surface combatants: helicopters only (no combat value yet). Carriers and amphibs: jets."),
  ],
};

// ============================ VEHICLES ============================
const vehicle: CategorySpec = {
  id: "vehicle",
  name: "Vehicle battery",
  plural: "Vehicles",
  hasLoadout: true,
  slots: [
    {
      id: "chassis", name: "Chassis", parts: [
        p("fixed", "Semi-fixed launchers", undefined, "Cheap, but found and hit easily.", { mob: 0.05, cap: 4, maxSize: "L", cost: 6, hp: 60 }),
        p("truck6", "6×6 truck", undefined, "Light and mobile.", { mob: 0.45, cap: 4, maxSize: "M", cost: 4, hp: 25 }),
        p("truck8", "8×8 heavy truck", undefined, "Carries heavier missiles.", { mob: 0.5, cap: 4, maxSize: "L", cost: 7, hp: 30 }),
        p("tracked", "Tracked carrier", undefined, "Off-road and armoured.", { mob: 0.4, cap: 4, maxSize: "M", cost: 8, hp: 45 }),
        p("mzkt", "Heavy ballistic TEL", "mis_srbm", "MAZ/MZKT-class launcher for XL ballistic and heavy anti-ship missiles.", { mob: 0.35, cap: 1, maxSize: "XL", cost: 14, hp: 35 }),
        p("container", "Container-disguised", "unm_owa", "Launchers hidden in shipping containers.", { mob: 0.65, cap: 4, maxSize: "L", cost: 6, hp: 20 }),
      ],
    },
    {
      id: "role", name: "Battery role", parts: [
        p("sam", "Air defence", undefined, "Radar-guided SAM battery.", { role: "sam" }),
        p("tel", "Strike launcher (TEL)", undefined, "Launches ballistic or cruise missiles.", { role: "tel" }),
        p("drone", "Drone launcher", "unm_harpy", "Launches loitering munitions and attack drones.", { role: "drone" }),
        p("shore", "Coastal anti-ship", undefined, "Shore-based anti-ship missile battery.", { role: "shore" }),
      ],
    },
    {
      id: "radar", name: "Radar", parts: [
        p("none", "None (cued)", undefined, "Relies on external tracks.", { km: 0, ch: 0, cost: 0 }),
        p("pd", "Engagement radar", "sen_pd", "Mechanical tracking radar.", { km: 120, ch: 2, cost: 15 }),
        p("pesa", "Phased-array engagement radar", "sen_pesa", "Patriot / S-300 class.", { km: 180, ch: 6, cost: 40 }),
        p("aesa", "AESA radar", "sen_aesa", "Many channels, hard to jam.", { km: 240, ch: 10, cost: 60 }),
        p("gan", "GaN AESA", "sen_gan", "LTAMDS / Nebo class.", { km: 320, ch: 16, cost: 80 }),
      ],
    },
    {
      id: "pd", name: "Point defence", parts: [
        p("none", "None", undefined, "", { km: 0, pk: 0, cost: 0 }),
        p("gun", "AA guns", "def_ciws", "Radar-directed cannon.", { km: 3, pk: 0.4, cost: 8 }),
        p("cram", "C-RAM interceptors", "def_cram", "Iron Dome-style cheap interceptors.", { km: 12, pk: 0.75, cost: 30 }),
        p("laser", "Laser 60 kW", "adv_laser1", "Iron Beam class.", { km: 6, pk: 0.65, cost: 30, laser: 60 }),
        p("hpm", "High-power microwave", "adv_hpm", "Kills drone swarms wholesale.", { km: 3, pk: 0.9, cost: 35 }),
      ],
    },
  ],
  params: [
    prm("launchers", "Launchers per battery", "launchers", 1, 12, 1, 4, "Each launcher has tubes set by the chassis. Small (S) missiles pack four to a tube."),
  ],
};

// ============================ DRONES ============================
const drone: CategorySpec = {
  id: "drone",
  name: "Drone",
  plural: "Drones",
  hasLoadout: true,
  slots: [
    {
      id: "type", name: "Type", parts: [
        p("owa", "One-way attack drone", "unm_harpy", "Expendable kamikaze drone launched in salvos.", { kind: "munition", recon: false }),
        p("recon", "Tactical recon UAV", "unm_uav", "Small spotter drone.", { kind: "uav", recon: true }),
        p("male", "MALE UAV", "unm_male", "Long endurance ISR. Finds ships and launchers.", { kind: "uav", recon: true }),
        p("ucav", "Armed UAV", "unm_ucav", "MALE drone with weapons.", { kind: "uav", recon: true, armed: true }),
        p("cca", "Collaborative combat aircraft", "unm_cca", "Jet loyal wingman with stealth shaping.", { kind: "uav", recon: false, armed: true, fighter: true }),
        p("usv", "Explosive drone boat", "unm_usv", "Sea-skimming expendable boat.", { kind: "munition", recon: false, boat: true }),
      ],
    },
    {
      id: "engine", name: "Engine", parts: [
        p("elec", "Electric", "unm_uav", "Quiet, short-ranged.", { k: 40, mach: 0.1, costMul: 1 }),
        p("piston", "Piston & propeller", undefined, "Cheap and long-ranged. Slow and noisy.", { k: 278, mach: 0.15, costMul: 1 }),
        p("tj", "Small turbojet", "mis_turbojet", "Fast attack drone.", { k: 90, mach: 0.55, costMul: 1.8 }),
        p("tf", "Turbofan", "unm_cca", "Efficient and fast.", { k: 200, mach: 0.8, costMul: 2.5 }),
      ],
    },
    {
      id: "guide", name: "Guidance / sensor", parts: [
        p("gps", "GPS/INS", "mis_gps", "Flies to fixed coordinates. Jamming degrades it.", { pk: 0.75, fixedOnly: true, radar: 0, costMul: 1 }),
        p("eo", "EO/IR camera + datalink", "unm_uav", "Operator or tracker guides it onto any target.", { pk: 0.7, radar: 30, costMul: 1.3 }),
        p("arm", "Anti-radiation seeker", "unm_harpy", "Hunts emitting radars.", { pk: 0.65, arm: true, radar: 0, costMul: 1.5 }),
        p("sar", "SAR/GMTI radar", "unm_male", "UAVs only: finds ships and vehicles at long range.", { pk: 0.7, radar: 180, costMul: 2 }),
        p("ai", "AI autonomy", "mis_ai", "Finds and picks targets itself. Immune to jamming.", { pk: 0.85, radar: 60, costMul: 2.2 }),
      ],
    },
    {
      id: "sig", name: "Signature", parts: [
        p("conv", "Conventional", undefined, "", { rcs: 0.2, costMul: 1 }),
        p("lo", "Low-observable", "mis_lo", "Stealth shaping.", { rcs: 0.02, costMul: 1.7 }),
      ],
    },
  ],
  params: [
    prm("weight", "Weight", "kg", 5, 8000, 5, 200, "Munitions: up to 250 kg is small (5 per truck rack), up to 900 kg is medium."),
    prm("warhead", "Warhead / payload", "kg", 0, 1500, 1, 50, "Explosive for munitions. Weapon payload for armed UAVs."),
    prm("sqn", "Unit size", "drones", 1, 24, 1, 6, "UAVs per squadron (ignored for expendable munitions)."),
  ],
};

// ============================ BASES ============================
const base: CategorySpec = {
  id: "base",
  name: "Base",
  plural: "Bases",
  hasLoadout: true,
  slots: [
    {
      id: "type", name: "Base type", parts: [
        p("air", "Air base", undefined, "Hosts squadrons. Runways can be cratered.", { kind: "air", hp: 2700, cost: 400 }),
        p("naval", "Naval base", undefined, "Homeport, repairs and replenishment for ships.", { kind: "naval", hp: 3300, cost: 600 }),
        p("missile", "Missile base", undefined, "Silos and launch pads for strike missiles.", { kind: "missile", hp: 2100, cost: 300 }),
        p("radar", "Radar & air-defence site", undefined, "Early-warning radar hub.", { kind: "radar", hp: 1200, cost: 200 }),
      ],
    },
    {
      id: "hard", name: "Protection", parts: [
        p("none", "Open", undefined, "Aircraft parked in the open.", { hard: 0, hpMul: 1, cost: 0 }),
        p("rev", "Revetments & dispersal", undefined, "Berms and dispersal stands.", { hard: 0.25, hpMul: 1.15, cost: 80 }),
        p("has", "Hardened shelters", "def_hardening", "Concrete shelters.", { hard: 0.6, hpMul: 1.4, cost: 300 }),
        p("deep", "Deep underground", "def_deep", "Mountain tunnels and missile cities.", { hard: 0.85, hpMul: 2, cost: 900 }),
      ],
    },
    {
      id: "radar", name: "Early-warning radar", parts: [
        p("none", "None", undefined, "", { km: 0, ch: 0, cost: 0 }),
        p("ew", "Long-range surveillance", "sen_pd", "Big rotating radar.", { km: 350, ch: 2, cost: 60 }),
        p("pesa", "Large phased array", "sen_pesa", "Early warning against ballistic missiles.", { km: 800, ch: 4, cost: 250 }),
        p("gan", "GaN early-warning array", "sen_gan", "Sees aircraft and missiles very far out.", { km: 1100, ch: 8, cost: 400 }),
      ],
    },
    {
      id: "pd", name: "Point defence", parts: [
        p("none", "None", undefined, "", { km: 0, pk: 0, cost: 0 }),
        p("gun", "AA guns", "def_ciws", "", { km: 3, pk: 0.4, cost: 20 }),
        p("cram", "C-RAM", "def_cram", "", { km: 12, pk: 0.75, cost: 80 }),
        p("laser", "Laser 300 kW", "adv_laser2", "", { km: 9, pk: 0.75, cost: 150, laser: 300 }),
      ],
    },
  ],
  params: [
    prm("capacity", "Capacity", "units", 4, 150, 1, 40, "Squadrons, ships or batteries the base can support."),
    prm("pads", "Silos / launch pads", "pads", 0, 60, 1, 0, "Fixed launchers for large strike missiles or long-range SAMs."),
  ],
};

export const SPECS: Record<Category, CategorySpec> = { missile, aircraft, ship, vehicle, drone, base };

export function findPart(cat: Category, slotId: string, partId: string): Part | undefined {
  return SPECS[cat].slots.find((s) => s.id === slotId)?.parts.find((x) => x.id === partId);
}

export const SIZE_UNITS: Record<string, number> = { S: 1, M: 2, L: 4, XL: 8 };
export const SIZE_ORDER = ["S", "M", "L", "XL"];
