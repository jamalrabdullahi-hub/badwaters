import type { FieldId, Tech } from "../sim/types";

export const FIELDS: { id: FieldId; name: string; short: string }[] = [
  { id: "MIS", name: "Missiles & Propulsion", short: "Missiles" },
  { id: "SEN", name: "Sensors, C4I & EW", short: "Sensors" },
  { id: "AIR", name: "Aerospace", short: "Aerospace" },
  { id: "NAV", name: "Naval Systems", short: "Naval" },
  { id: "UNM", name: "Unmanned Systems", short: "Unmanned" },
  { id: "DEF", name: "Air & Missile Defence", short: "Defence" },
  { id: "ADV", name: "Directed Energy & Future", short: "Future" },
];

const t = (id: string, name: string, field: FieldId, year: number, cost: number, requires: string[], desc: string): Tech => ({
  id, name, field, year, cost, requires, desc,
});

export const TECHS: Tech[] = [
  // ---- Missiles & propulsion
  t("mis_solid", "Solid rocket motors", "MIS", 1960, 40, [], "Storable, instant-ready rocket motors. Basis of every SAM and early anti-ship missile."),
  t("mis_seaskim", "Sea-skimming autopilot", "MIS", 1972, 60, ["mis_solid"], "Radar altimeters let missiles fly 5 m above the waves, under the enemy radar horizon."),
  t("mis_turbojet", "Missile turbojets", "MIS", 1972, 70, ["mis_solid"], "Air-breathing sustainers give subsonic anti-ship missiles 100+ km of range."),
  t("mis_ramjet", "Ramjet propulsion", "MIS", 1976, 110, ["mis_turbojet"], "Supersonic cruise at Mach 2–3. Harder to intercept, heavier and shorter-ranged."),
  t("mis_arh", "Active radar seekers", "MIS", 1976, 80, ["mis_solid"], "Fire-and-forget terminal homing. Frees fire-control channels after launch."),
  t("mis_turbofan", "Small turbofans", "MIS", 1981, 120, ["mis_turbojet"], "Efficient engines that give cruise missiles 1,000+ km of range."),
  t("mis_srbm", "Short-range ballistic missiles", "MIS", 1965, 90, ["mis_solid"], "Road-mobile ballistic missiles out to ~300 km."),
  t("mis_mrbm", "Medium-range ballistic missiles", "MIS", 1978, 160, ["mis_srbm"], "Ballistic reach of 1,000–2,000 km. Needs large launchers."),
  t("mis_iir", "Imaging infrared seekers", "MIS", 1988, 130, ["mis_arh"], "Passive seekers that are hard to jam or decoy."),
  t("mis_gps", "GPS/INS guidance", "MIS", 1993, 120, ["mis_turbofan"], "Accurate mid-course navigation for land-attack weapons."),
  t("mis_marv", "Manoeuvring re-entry vehicles", "MIS", 1996, 220, ["mis_mrbm"], "Terminal manoeuvres that defeat simple interceptors and improve accuracy."),
  t("mis_wlink", "Weapon datalinks", "MIS", 1998, 150, ["mis_arh"], "In-flight retargeting. Better hit chance against stale or moving targets."),
  t("mis_lo", "Low-observable missiles", "MIS", 2006, 260, ["mis_turbofan", "mis_iir"], "Stealthy airframes (JASSM/LRASM class) detected late by defenders."),
  t("mis_multimode", "Multi-mode seekers", "MIS", 2009, 240, ["mis_iir", "mis_wlink"], "Radar + IR + passive RF seekers. Very resistant to decoys."),
  t("mis_asbm", "Anti-ship ballistic missiles", "MIS", 2012, 380, ["mis_marv"], "Ballistic missiles that can hit ships at sea from 1,500+ km."),
  t("mis_hgv", "Hypersonic glide vehicles", "MIS", 2019, 520, ["mis_marv"], "Mach 8+ gliders that manoeuvre in the upper atmosphere, where few interceptors reach."),
  t("mis_scram", "Scramjet cruise missiles", "MIS", 2023, 560, ["mis_ramjet", "mis_hgv"], "Air-breathing hypersonic cruise missiles."),
  t("mis_ai", "AI target recognition", "MIS", 2031, 520, ["mis_multimode"], "Seekers that pick out real targets from decoys on their own."),
  t("mis_swarm", "Cooperative munitions", "MIS", 2037, 640, ["mis_ai"], "Salvos that share data and split up to saturate defences."),

  // ---- Sensors, C4I, EW
  t("sen_pd", "Pulse-Doppler radar", "SEN", 1970, 60, [], "Look-down/shoot-down radar that can separate low targets from sea clutter."),
  t("sen_ew", "Electronic warfare suites", "SEN", 1975, 70, [], "Jammers and chaff that reduce enemy detection and seduce seekers."),
  t("sen_link", "Tactical datalinks", "SEN", 1980, 90, ["sen_pd"], "Link 11/16: a shared track picture across the force."),
  t("sen_pesa", "Passive phased arrays", "SEN", 1983, 140, ["sen_pd"], "Multi-target tracking with many simultaneous engagements (SPY-1 class)."),
  t("sen_irst", "Infrared search & track", "SEN", 1985, 90, ["sen_pd"], "Passive detection of aircraft, including stealthy ones, at short range."),
  t("sen_sat", "Satellite ISR & targeting", "SEN", 1995, 220, ["sen_link"], "Periodic satellite fixes on enemy fleets for over-the-horizon targeting."),
  t("sen_aesa", "Active phased arrays (AESA)", "SEN", 2000, 220, ["sen_pesa"], "Longer range, more channels, low probability of intercept."),
  t("sen_cec", "Cooperative engagement", "SEN", 2002, 240, ["sen_link", "sen_pesa"], "Fire SAMs on tracks held by other ships or aircraft, beyond your own radar horizon."),
  t("sen_drfm", "DRFM jamming", "SEN", 2006, 200, ["sen_ew"], "Digital jammers that spoof radars and seekers."),
  t("sen_gan", "GaN AESA", "SEN", 2015, 300, ["sen_aesa"], "Gallium-nitride arrays with a big jump in range and reliability."),
  t("sen_leo", "LEO sensor constellations", "SEN", 2027, 420, ["sen_sat"], "Hundreds of small satellites give near-continuous tracking of ships."),
  t("sen_passive", "Passive multistatic sensing", "SEN", 2031, 460, ["sen_gan"], "Detect stealth platforms using other emitters' signals."),
  t("sen_aibm", "AI battle management", "SEN", 2035, 520, ["sen_cec", "sen_leo"], "Automated threat evaluation and weapon assignment across the force."),

  // ---- Aerospace
  t("air_tanker", "Aerial refuelling", "AIR", 1960, 40, [], "Extends combat radius of strike packages."),
  t("air_aew", "Airborne early warning", "AIR", 1965, 60, [], "Radar aircraft that see low fliers far beyond the surface horizon."),
  t("air_gen4", "4th-generation airframes", "AIR", 1974, 100, [], "Agile fighters with pulse-Doppler radar and BVR missiles (F-15, Su-27)."),
  t("air_fbw", "Fly-by-wire", "AIR", 1978, 80, ["air_gen4"], "Relaxed-stability designs with better agility and payload."),
  t("air_lo1", "First-generation stealth", "AIR", 1983, 200, ["air_fbw"], "Faceted low-observable shaping. Tiny radar cross-section, poor aerodynamics."),
  t("air_bomber_lo", "Stealth bombers", "AIR", 1989, 260, ["air_lo1"], "Flying-wing bombers that penetrate dense air defences."),
  t("air_gen45", "4.5-generation fighters", "AIR", 1996, 180, ["air_fbw", "sen_pesa"], "Advanced avionics, reduced signature, more hardpoints (Rafale, Typhoon, Super Hornet)."),
  t("air_gen5", "5th-generation fighters", "AIR", 2005, 380, ["air_gen45", "air_lo1"], "All-aspect stealth, sensor fusion, internal weapons bays (F-22, F-35, J-20)."),
  t("air_aesaaew", "AESA AEW&C", "AIR", 2010, 220, ["air_aew", "sen_aesa"], "Long-range air and missile warning (E-2D, E-7)."),
  t("air_stovl5", "Supersonic STOVL", "AIR", 2012, 300, ["air_gen5"], "Stealth fighters that fly from small carriers and amphibious ships."),
  t("air_adaptive", "Adaptive-cycle engines", "AIR", 2030, 420, ["air_gen5"], "Around 30% more range and supercruise."),
  t("air_gen6", "6th-generation air dominance", "AIR", 2035, 640, ["air_adaptive", "unm_cca"], "Tailless, very-low-observable fighters that control drone wingmen."),

  // ---- Naval
  t("nav_gasturb", "Gas turbine propulsion", "NAV", 1965, 50, [], "Compact, fast-starting propulsion for warships."),
  t("nav_nuke", "Naval nuclear propulsion", "NAV", 1955, 300, [], "Unlimited range for submarines and carriers."),
  t("nav_catobar", "CATOBAR carrier ops", "NAV", 1955, 200, [], "Catapults and arrestor gear for heavy fixed-wing air wings."),
  t("nav_combat", "Integrated combat systems", "NAV", 1983, 160, ["sen_pesa"], "Aegis-class systems: automatic, many-channel air defence."),
  t("nav_vls", "Vertical launch systems", "NAV", 1986, 150, ["mis_solid"], "Magazines of standard cells: fast salvos and flexible loadouts."),
  t("nav_vls_sl", "Strike-length VLS", "NAV", 1988, 110, ["nav_vls"], "Long cells that hold cruise missiles and large interceptors."),
  t("nav_aip", "Air-independent propulsion", "NAV", 1995, 200, [], "Diesel submarines that stay submerged for weeks."),
  t("nav_stealth", "Signature-reduced hulls", "NAV", 1995, 160, ["nav_gasturb"], "Faceted superstructures that cut RCS by 90%."),
  t("nav_iep", "Integrated electric propulsion", "NAV", 2010, 260, ["nav_gasturb"], "Pooled electric power for big radars and directed-energy weapons."),
  t("nav_emals", "Electromagnetic catapults", "NAV", 2017, 300, ["nav_catobar", "nav_iep"], "Higher sortie rates and heavier drones from carrier decks."),
  t("nav_ldvls", "Large-diameter VLS", "NAV", 2025, 280, ["nav_vls_sl"], "Cells that fit hypersonic and ballistic weapons."),

  // ---- Unmanned
  t("unm_uav", "Tactical UAVs", "UNM", 1985, 60, [], "Small reconnaissance drones that spot targets for strikes."),
  t("unm_harpy", "Anti-radiation loitering munitions", "UNM", 1989, 110, ["unm_uav"], "Drones that loiter over air defences and dive onto radars (Harpy)."),
  t("unm_male", "MALE UAVs", "UNM", 1995, 140, ["unm_uav"], "Long-endurance surveillance drones (Predator class)."),
  t("unm_ucav", "Armed UAVs", "UNM", 2001, 180, ["unm_male"], "Drones that carry precision weapons."),
  t("unm_owa", "Mass one-way attack drones", "UNM", 2014, 160, ["unm_harpy", "mis_gps"], "Cheap long-range kamikaze drones built by the thousand (Shahed class)."),
  t("unm_usv", "Unmanned surface vessels", "UNM", 2019, 220, ["unm_male"], "Explosive and patrol drone boats."),
  t("unm_cca", "Collaborative combat aircraft", "UNM", 2027, 380, ["unm_ucav", "air_gen5"], "Jet drones flying as loyal wingmen to manned fighters."),
  t("unm_swarm", "Drone swarms", "UNM", 2030, 420, ["unm_owa", "sen_aibm"], "Hundreds of cooperating drones. Overwhelms point defences."),
  t("unm_xluuv", "Autonomous submarines", "UNM", 2031, 460, ["unm_usv", "nav_aip"], "Extra-large UUVs for mining, strike and scouting."),

  // ---- Air & missile defence
  t("def_sam_m", "Medium-range SAMs", "DEF", 1970, 70, ["mis_solid"], "Area air defence out to ~50 km."),
  t("def_hardening", "Hardened aircraft shelters", "DEF", 1975, 80, [], "Concrete shelters that protect parked aircraft from strikes."),
  t("def_ciws", "Gun CIWS", "DEF", 1978, 70, [], "20–30 mm radar-guided guns: the last line against sea-skimmers."),
  t("def_sam_l", "Long-range SAMs", "DEF", 1982, 150, ["def_sam_m", "sen_pesa"], "Patriot / S-300 / SM-2ER class. 150+ km against aircraft."),
  t("def_bmd1", "Terminal ballistic defence", "DEF", 1991, 180, ["def_sam_l"], "Modified SAMs that can hit ballistic warheads in their final seconds."),
  t("def_ram", "Missile CIWS", "DEF", 1992, 110, ["def_ciws", "mis_iir"], "RAM/SeaRAM: IR-guided point-defence missiles out to 9 km."),
  t("def_deep", "Deep underground facilities", "DEF", 1995, 160, ["def_hardening"], "Tunnel complexes that protect missiles and aircraft from most strikes."),
  t("def_decoy", "Active offboard decoys", "DEF", 1996, 120, ["sen_ew"], "Nulka-type hovering decoys that pull seekers away from ships."),
  t("def_htk", "Hit-to-kill interceptors", "DEF", 2001, 260, ["def_bmd1"], "PAC-3 / Arrow 2 class. Direct collision kills ballistic warheads."),
  t("def_exo", "Exo-atmospheric interceptors", "DEF", 2006, 360, ["def_htk"], "SM-3 / Arrow 3: kills MRBMs in space, far from the target."),
  t("def_cram", "Counter-rocket & drone defence", "DEF", 2010, 180, ["def_sam_m", "sen_aesa"], "Iron Dome class: cheap interceptors against rockets and drones."),
  t("def_glide", "Glide-phase interceptors", "DEF", 2032, 560, ["def_exo", "sen_leo"], "Interceptors that can catch hypersonic gliders mid-flight."),

  // ---- Directed energy & future
  t("adv_pulse", "Pulsed power storage", "ADV", 2016, 200, ["nav_iep"], "Capacitor banks and batteries that feed energy weapons."),
  t("adv_laser1", "Tactical lasers (60 kW)", "ADV", 2018, 260, ["adv_pulse"], "Burns down drones and small boats for pennies per shot."),
  t("adv_hpm", "High-power microwave", "ADV", 2025, 320, ["adv_pulse"], "Fries drone swarms' electronics in a wide cone."),
  t("adv_laser2", "High-energy lasers (300 kW)", "ADV", 2027, 420, ["adv_laser1"], "Can kill subsonic cruise missiles at close range."),
  t("adv_railgun", "Electromagnetic railgun", "ADV", 2030, 480, ["adv_pulse", "nav_iep"], "Mach 6 guided projectiles: cheap missile defence and strike."),
  t("adv_quantum", "Quantum navigation", "ADV", 2037, 420, ["mis_ai"], "GPS-independent precision navigation that can't be jammed."),
  t("adv_laser3", "Megawatt-class lasers", "ADV", 2041, 700, ["adv_laser2"], "Kills supersonic missiles and aircraft at the horizon."),
];

export const TECH_BY_ID: Record<string, Tech> = Object.fromEntries(TECHS.map((x) => [x.id, x]));

/** Research cost at a given year: researching ahead of history is expensive (as in Rule the Waves). */
export function techCost(tech: Tech, year: number): number {
  const ahead = Math.max(0, tech.year - year);
  const behind = Math.max(0, year - tech.year);
  const aheadMul = 1 + ahead * 0.35 + ahead * ahead * 0.04;
  const behindMul = Math.max(0.35, 1 - behind * 0.04); // catching up on old tech is cheaper
  return Math.round(tech.cost * aheadMul * behindMul);
}
