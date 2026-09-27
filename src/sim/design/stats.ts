import { SIZE_ORDER, SIZE_UNITS, SPECS, findPart } from "../../data/parts";
import { TECH_BY_ID } from "../../data/techs";
import type { Design, Issue, MunitionRole, Part, SizeClass, Stats } from "../types";

export interface DesignEval {
  stats: Stats;
  issues: Issue[];
}

type Lookup = (id: string) => Design | undefined;

const num = (v: unknown, d = 0): number => (typeof v === "number" ? v : d);
const str = (v: unknown, d = ""): string => (typeof v === "string" ? v : d);

export function sizeOfWeight(kg: number): SizeClass {
  if (kg <= 250) return "S";
  if (kg <= 900) return "M";
  if (kg <= 1700) return "L";
  return "XL";
}

export function sizeFits(size: SizeClass, max: SizeClass): boolean {
  return SIZE_ORDER.indexOf(size) <= SIZE_ORDER.indexOf(max);
}

/** Resolve every slot to a part, falling back to the first part if a design references something unknown. */
function parts(d: Design): Record<string, Part> {
  const out: Record<string, Part> = {};
  for (const slot of SPECS[d.category].slots) {
    out[slot.id] = findPart(d.category, slot.id, d.slots[slot.id]) ?? slot.parts[0];
  }
  return out;
}

function param(d: Design, id: string): number {
  const spec = SPECS[d.category].params.find((x) => x.id === id);
  const v = d.params[id];
  return typeof v === "number" ? v : spec ? spec.def : 0;
}

/** Evaluate a design. Pass `known` to also flag parts whose tech the nation lacks. */
export function evaluate(d: Design, lookup: Lookup, known?: Set<string>): DesignEval {
  const issues: Issue[] = [];
  const ps = parts(d);
  if (known) {
    for (const part of Object.values(ps)) {
      if (part.tech && !known.has(part.tech)) {
        issues.push({ sev: "error", text: `${part.name} needs ${TECH_BY_ID[part.tech]?.name ?? part.tech}.` });
      }
    }
  }
  let stats: Stats;
  switch (d.category) {
    case "missile": stats = missileStats(d, ps, issues); break;
    case "aircraft": stats = aircraftStats(d, ps, issues, known); break;
    case "ship": stats = shipStats(d, ps, issues); break;
    case "vehicle": stats = vehicleStats(d, ps, issues); break;
    case "drone": stats = droneStats(d, ps, issues); break;
    case "base": stats = baseStats(d, ps, issues); break;
  }
  if (SPECS[d.category].hasLoadout) checkLoadout(d, stats, lookup, issues, ps);
  return { stats, issues };
}

// ------------------------------------------------------------------ missiles
const SIZE_RCS: Record<SizeClass, number> = { S: 0.03, M: 0.08, L: 0.15, XL: 0.8 };
const ROLE_PROPS: Record<string, string[]> = {
  ashm: ["solid", "turbojet", "turbofan", "ramjet", "scram"],
  lacm: ["solid", "turbojet", "turbofan", "ramjet", "scram"],
  sam: ["solid"],
  aam: ["solid", "ramjet"],
  bmd: ["solid"],
  srbm: ["liquid", "solidb"],
  asbm: ["liquid", "solidb"],
  hgv: ["glide", "scram"],
  arm: ["solid", "ramjet", "turbojet"],
  torp: ["torpedo"],
};

function missileStats(d: Design, ps: Record<string, Part>, issues: Issue[]): Stats {
  const role = str(ps.role.a.role) as MunitionRole;
  const prop = ps.prop.a;
  const seek = ps.seeker.a;
  const frame = ps.airframe.a;
  const W = param(d, "weight");
  const wh = param(d, "warhead");
  const f = 1 - num(frame.struct, 0.3) - wh / W;
  if (f < 0.1) issues.push({ sev: "error", text: "Warhead is too heavy for this missile. Not enough room for fuel." });
  const kind = str(prop.kind);
  let range = num(prop.k) * Math.max(0.02, f) * Math.pow(W, num(prop.e, 1));
  if (kind === "rocket") {
    range /= 1 + Math.pow(W / 1800, 1.5); // big rockets hit drag and staging limits
    if (frame.skim) range *= 0.35; // sea-skimming rockets burn out in dense air
  }
  if (ps.prop.id === "ramjet" && frame.skim) range *= 0.6;
  if (role === "aam") range *= 2.2; // launched fast and high
  // Without mid-course updates, semi-active and IR interceptors are limited by what they can see, not by fuel.
  if ((role === "sam" || role === "aam") && (seek.sarh || ps.seeker.id === "ir") && ps.link.id === "none") range *= 0.45;
  if (role === "bmd" && seek.exo) range *= 1.6;
  let mach = num(prop.mach);
  if (kind === "ballistic") mach = Math.min(14, mach + range / 400);
  if (kind === "glide") mach = Math.min(20, mach + range / 2000);
  if (kind === "rocket" && W > 1000) mach += 0.5;
  const size = sizeOfWeight(W);

  if (!ROLE_PROPS[role]?.includes(ps.prop.id)) {
    issues.push({ sev: "error", text: `${ps.prop.name} can't power a ${ps.role.name.toLowerCase()} missile.` });
  }
  const interceptor = role === "sam" || role === "aam" || role === "bmd";
  if (seek.fixedOnly && (role === "ashm" || role === "asbm" || interceptor || role === "arm")) {
    issues.push({ sev: "error", text: `${ps.seeker.name} can only hit fixed targets.` });
  }
  if (seek.armOnly && role !== "arm") issues.push({ sev: "error", text: "A passive RF seeker only works on an anti-radiation missile." });
  if (role === "arm" && !["rf", "multi", "ai"].includes(ps.seeker.id)) issues.push({ sev: "error", text: "An anti-radiation missile needs an RF, multi-mode or AI seeker." });
  if (typeof seek.bmd === "number" && role !== "bmd") issues.push({ sev: "error", text: `${ps.seeker.name} is only for ballistic missile defence.` });
  if (role === "bmd" && !["arh", "htk", "exo", "gpi", "multi"].includes(ps.seeker.id)) issues.push({ sev: "error", text: "A BMD interceptor needs an active, hit-to-kill or exo seeker." });
  if (seek.sarh && !interceptor) issues.push({ sev: "error", text: "Semi-active homing only works for SAMs and AAMs." });
  if (ps.airframe.id === "marv" && !(role === "srbm" || role === "asbm")) issues.push({ sev: "error", text: "A manoeuvring re-entry vehicle only fits ballistic missiles." });
  if ((ps.airframe.id === "skim" || ps.airframe.id === "weave" || ps.airframe.id === "lo") && (interceptor || kind === "ballistic" || kind === "glide")) {
    issues.push({ sev: "error", text: `${ps.airframe.name} airframe only suits cruise missiles.` });
  }
  if (role === "aam" && !sizeFits(size, "M")) issues.push({ sev: "error", text: "Too heavy for a fighter. Air-to-air missiles must be 900 kg or less." });
  if (wh < 5) issues.push({ sev: "warn", text: "Warhead is tiny." });
  if (role === "asbm" && !["arh", "iir", "multi", "ai"].includes(ps.seeker.id)) issues.push({ sev: "error", text: "An anti-ship ballistic missile needs a terminal seeker." });
  if (seek.torpOnly && role !== "torp") issues.push({ sev: "error", text: "A sonar homing head only works on a torpedo." });
  if (role === "torp" && !["sonar", "ai"].includes(ps.seeker.id)) issues.push({ sev: "error", text: "A torpedo needs a sonar or AI homing head." });
  if (role === "torp" && ps.airframe.id !== "conv") issues.push({ sev: "error", text: "Torpedoes use the conventional airframe." });

  let alt = 300;
  const skim = !!frame.skim;
  if (role === "ashm" || role === "lacm" || role === "arm") alt = skim ? 6 : kind === "rocket" ? 12000 : 300;
  if (kind === "ballistic") alt = Math.min(300, Math.max(40, range / 4)) * 1000;
  if (kind === "glide") alt = 40000;
  if (kind === "torp") alt = -40;
  if (ps.prop.id === "scram") alt = 25000;

  let man = num(frame.man);
  if (mach > 2 && kind === "air") man += 0.2;
  if (kind === "glide") man = Math.max(man, 0.75);

  let interceptAir = 0, interceptBallistic = 0, interceptHyper = 0;
  if (role === "sam") { interceptAir = 1; interceptBallistic = 0.15; interceptHyper = 0.03; }
  if (role === "aam") { interceptAir = 1; }
  if (role === "bmd") {
    interceptAir = 0.6;
    interceptBallistic = num(seek.bmd, ps.seeker.id === "arh" ? 0.5 : 0.6);
    interceptHyper = num(seek.hyper, 0.12);
  }

  const pk = Math.min(0.97, num(seek.pk) + num(ps.link.a.pkAdd));
  const cost = Math.max(0.02, 1.6 * Math.pow(W / 1000, 0.7) * num(prop.costMul, 1) * num(seek.costMul, 1) * num(frame.costMul, 1) * num(ps.link.a.costMul, 1));
  return {
    cost: round(cost, 2),
    devCost: Math.round(40 + cost * 50),
    devMonths: Math.round(18 + (W > 2000 ? 12 : 0) + (kind === "glide" || ps.prop.id === "scram" ? 12 : 0)),
    buildMonths: round(0.02 + cost * 0.004, 4),
    upkeep: 0,
    role, size, rangeKm: Math.round(range), mach: round(mach, 1), pk: round(pk, 2), warhead: wh,
    rcs: kind === "torp" ? 0 : round(SIZE_RCS[size] * num(frame.rcsMul, 1), 4), alt,
    softkillResist: num(seek.sk), maneuver: round(Math.min(0.9, man), 2),
    interceptAir, interceptBallistic, interceptHyper,
    exo: !!seek.exo, sarh: !!seek.sarh, fixedOnly: !!seek.fixedOnly,
  };
}

// ------------------------------------------------------------------ aircraft
function aircraftStats(d: Design, ps: Record<string, Part>, issues: Issue[], known?: Set<string>): Stats {
  const fr = ps.frame.a, role = ps.role, eng = ps.engine.a, rad = ps.radar.a, bas = ps.basing.a, ew = ps.ew.a;
  const payload = param(d, "payload");
  const sqn = param(d, "sqn");
  const capKg = num(fr.cap) * 600;
  const internalKg = num(fr.internal) * 250;
  let rcs = num(fr.rcs);
  if (payload > capKg) issues.push({ sev: "error", text: `Payload is over the airframe limit of ${capKg.toLocaleString()} kg.` });
  if (num(fr.internal) > 0 && payload > internalKg && rcs < 1) {
    rcs = 1.2;
    issues.push({ sev: "warn", text: `Payload is over the internal bay limit of ${internalKg.toLocaleString()} kg. External stores push RCS up to 1.2 m².` });
  }
  if (rad.aew && role.id !== "aew") issues.push({ sev: "error", text: "An AEW radar needs the AEW&C mission." });
  if (role.id === "aew" && !rad.aew) issues.push({ sev: "error", text: "The AEW&C mission needs a rotodome or AESA AEW radar." });
  if (role.id === "aew" && ps.frame.id !== "large") issues.push({ sev: "error", text: "The AEW&C mission needs a large support airframe." });
  if (role.id === "mpa" && ps.frame.id !== "large") issues.push({ sev: "warn", text: "Maritime patrol works best on a large airframe." });
  const carrier = str(bas.carrier, "none") as Stats["carrier"];
  if (carrier !== "none" && (ps.frame.id === "bomber" || ps.frame.id === "lobomber")) issues.push({ sev: "error", text: "Bombers can't operate from carriers." });
  if (carrier === "stovl" && ["g45", "g5", "g6"].includes(ps.frame.id) && known && !known.has("air_stovl5")) {
    issues.push({ sev: "error", text: "A supersonic STOVL fighter needs Supersonic STOVL tech." });
  }
  if (carrier === "stovl" && ps.frame.id === "large") issues.push({ sev: "error", text: "Large aircraft can't do STOVL." });
  const radius = num(fr.radius) * num(eng.radiusMul, 1) * num(bas.radiusMul, 1) * (1 - 0.25 * Math.min(1, payload / Math.max(1, capKg))) * (carrier !== "none" && ps.frame.id === "large" ? 0.6 : 1);
  const per = (num(fr.cost) + num(rad.costAdd) + num(ew.costAdd)) * num(eng.costMul, 1) * num(bas.costMul, 1);
  const gen = ps.frame.id;
  const devMonths = gen === "g6" ? 84 : gen === "g5" ? 72 : gen === "lobomber" ? 84 : gen === "g45" ? 54 : 40;
  const hardpoints = Math.min(num(fr.cap), Math.floor(payload / 250));
  return {
    cost: Math.round(per * sqn),
    devCost: Math.round(per * 30 + 250),
    devMonths,
    buildMonths: round(6 + sqn * 0.8, 1),
    upkeep: round(per * sqn * 0.016, 2),
    hp: sqn,
    rcs, radarKm: num(rad.km), mastM: 9000, channels: Math.max(1, num(rad.ch)), ew: num(ew.ew),
    hardpoints, maxSize: str(fr.maxSize, "M") as SizeClass,
    radiusKm: Math.round(radius), mach: round(num(fr.mach) * num(eng.machMul, 1), 2),
    carrier, aew: !!rad.aew, stealth: rcs < 0.1 ? 1 : 0,
    dataLink: ps.radar.id === "pd" ? 0 : ps.radar.id === "gan" || ps.radar.id === "aewa" ? 2 : 1,
  };
}

// ------------------------------------------------------------------ ships
function shipStats(d: Design, ps: Record<string, Part>, issues: Issue[]): Stats {
  const h = ps.hull.a, pr = ps.prop.a, rd = ps.radar.a, cms = ps.cms.a, ln = ps.launcher.a, pd = ps.pd.a, ew = ps.ew.a, sg = ps.sig.a;
  const disp = param(d, "disp"), cells = param(d, "cells"), cans = param(d, "canisters"), wing = param(d, "wing");
  const sub = !!h.sub;
  if (disp < num(h.minD) || disp > num(h.maxD)) issues.push({ sev: "error", text: `${ps.hull.name} displacement must be ${num(h.minD).toLocaleString()}–${num(h.maxD).toLocaleString()} t.` });
  if (sub && !pr.sub) issues.push({ sev: "error", text: `${ps.prop.name} can't power a submarine.` });
  if (!sub && (ps.prop.id === "aip")) issues.push({ sev: "error", text: "AIP is for submarines only." });
  if (!sub && ps.prop.id === "diesel" && !["fac", "cor"].includes(ps.hull.id)) issues.push({ sev: "warn", text: "Diesel-only propulsion makes a large warship slow." });
  if (sub && ps.pd.id !== "none") issues.push({ sev: "error", text: "Submarines can't mount point defence." });
  if (sub && wing > 0) issues.push({ sev: "error", text: "Submarines can't carry aircraft." });
  const deck = str(h.deck);
  if (deck === "none" && wing > 0) issues.push({ sev: "error", text: "This hull has no flight deck." });
  if (deck === "helo" && wing > 4) issues.push({ sev: "error", text: "A helicopter deck holds 4 aircraft at most." });
  if (sub && ln.maxSize === "XL" && disp < 7000) issues.push({ sev: "warn", text: "Large-diameter tubes need a big submarine." });

  const refD = (num(h.minD) + num(h.maxD)) / 2;
  let speed = num(h.spd) * num(pr.spdMul, 1) * Math.pow(Math.max(0.2, disp / refD), 0.03);
  if (!sub && ps.prop.id === "diesel") speed *= 0.8;
  const powerMw = (disp / 1000) * num(pr.mw) * (sub ? 0.5 : 1) + 1;
  const powerUse = (sub ? 0.2 : num(rd.mw)) + num(pd.mw) + cells * 0.005;
  if (powerUse > powerMw) issues.push({ sev: "error", text: `Power demand ${powerUse.toFixed(1)} MW is over the ${powerMw.toFixed(1)} MW supply. Use integrated electric or nuclear propulsion.` });
  else if (powerUse > powerMw * 0.9) issues.push({ sev: "warn", text: "Power margin is under 10%." });

  const perAc = deck === "catobar" ? 330 : deck === "stovl" ? 290 : 60;
  const usable = disp * num(h.usable);
  const used = (sub ? 10 : num(rd.w)) + num(pd.w) + cells * num(ln.wPer) + cans * 3 + wing * perAc + 20;
  const margin = (usable - used) / usable;
  if (margin < 0) issues.push({ sev: "error", text: `Overweight: payload ${Math.round(used).toLocaleString()} t exceeds ${Math.round(usable).toLocaleString()} t usable.` });
  else if (margin < 0.05) issues.push({ sev: "warn", text: `Growth margin is only ${(margin * 100).toFixed(1)}%. Future refits will be hard.` });

  const cost = disp * num(h.cpt) * num(pr.costMul, 1) * num(sg.costMul, 1) + num(rd.cost) + num(cms.cost) + num(pd.cost) + num(ew.cost) + cells * num(ln.costPer) + cans;
  return {
    cost: Math.round(cost),
    devCost: Math.round(100 + cost * 0.35),
    devMonths: Math.round(24 + Math.pow(disp, 0.25)),
    buildMonths: Math.round(10 + Math.pow(disp, 0.4)),
    upkeep: round(cost * 0.012, 2),
    hp: Math.round(Math.pow(disp, 0.8) / 3 * (sub ? 0.7 : 1)),
    speedKts: round(speed, 1),
    radarKm: sub ? 25 : num(rd.km), mastM: sub ? 8 : Math.round(12 + Math.pow(disp, 0.33)),
    channels: sub ? 4 : Math.max(1, Math.min(num(rd.ch), num(cms.ch))),
    ew: num(ew.ew), ciwsKm: num(pd.km), ciwsPk: num(pd.pk), laserKw: num(pd.laser),
    cells: cells + cans, maxSize: str(ln.maxSize, "M") as SizeClass,
    airWing: deck === "helo" ? 0 : wing, displacement: disp,
    powerMw: round(powerMw, 1), powerUse: round(powerUse, 1), weight: Math.round(used), weightMax: Math.round(usable),
    rcs: sub ? 0.001 : Math.round(disp * 2 * num(sg.rcsMul, 1)), navy: true, sub,
    carrier: deck === "catobar" ? "catobar" : deck === "stovl" ? "stovl" : "none",
    dataLink: num(cms.link), stealth: sub ? 1 : 1 - num(sg.rcsMul, 1),
  };
}

// ------------------------------------------------------------------ vehicles
function vehicleStats(d: Design, ps: Record<string, Part>, issues: Issue[]): Stats {
  const ch = ps.chassis.a, rd = ps.radar.a, pd = ps.pd.a;
  const n = param(d, "launchers");
  const role = str(ps.role.a.role);
  if (role === "sam" && ps.radar.id === "none") issues.push({ sev: "warn", text: "An air-defence battery with no radar can only fire on shared tracks." });
  const cost = num(ch.cost) * n + num(rd.cost) + num(pd.cost);
  return {
    cost: Math.round(cost),
    devCost: Math.round(60 + cost * 2),
    devMonths: 24,
    buildMonths: round(6 + n * 0.5, 1),
    upkeep: round(cost * 0.012, 2),
    hp: Math.round(num(ch.hp) * Math.pow(n, 0.6)),
    radarKm: num(rd.km), mastM: 18, channels: Math.max(role === "sam" ? 1 : 0, num(rd.ch)),
    ciwsKm: num(pd.km), ciwsPk: num(pd.pk), laserKw: num(pd.laser),
    cells: num(ch.cap) * n, maxSize: str(ch.maxSize, "M") as SizeClass,
    mobility: num(ch.mob), rcs: 10, dataLink: ps.radar.id === "pd" || ps.radar.id === "none" ? 1 : 2,
  };
}

// ------------------------------------------------------------------ drones
function droneStats(d: Design, ps: Record<string, Part>, issues: Issue[]): Stats {
  const ty = ps.type.a, en = ps.engine.a, gd = ps.guide.a, sg = ps.sig.a;
  const W = param(d, "weight"), wh = param(d, "warhead"), sqn = param(d, "sqn");
  const munition = ty.kind === "munition";
  const boat = !!ty.boat;
  const f = 0.55 - wh / W;
  if (f < 0.08) issues.push({ sev: "error", text: "Payload is too heavy for this airframe." });
  let range = num(en.k) * Math.max(0.02, f) * Math.pow(W, 0.6);
  let mach = num(en.mach);
  if (boat) { mach = 0.06; range *= 0.3; }
  if (munition && wh <= 0) issues.push({ sev: "error", text: "A one-way attack drone needs a warhead." });
  if (munition && ps.guide.id === "sar") issues.push({ sev: "error", text: "A SAR radar is too big for an expendable drone." });
  if (!munition && ps.guide.id === "arm") issues.push({ sev: "error", text: "An anti-radiation seeker only suits one-way attack drones." });
  if (boat && gd.fixedOnly) issues.push({ sev: "error", text: "A drone boat needs a camera or AI to find ships." });
  if (ty.fighter && ps.engine.id !== "tf" && ps.engine.id !== "tj") issues.push({ sev: "error", text: "A combat aircraft drone needs a jet engine." });
  const rcs = num(sg.rcs) * Math.sqrt(Math.max(0.1, W / 200));
  const base = 0.02 * Math.pow(W / 100, 0.8) * num(en.costMul, 1) * num(gd.costMul, 1) * num(sg.costMul, 1);
  const alt = boat ? 0 : ps.engine.id === "tf" ? 8000 : ps.engine.id === "tj" ? 300 : 1200;
  if (munition) {
    const cost = Math.max(0.005, base);
    return {
      cost: round(cost, 3), devCost: Math.round(20 + cost * 200), devMonths: 12,
      buildMonths: round(0.004 + cost * 0.02, 4), upkeep: 0,
      role: "loiter", size: sizeOfWeight(W), rangeKm: Math.round(range), mach: round(mach, 2),
      pk: num(gd.pk), warhead: wh, rcs: round(rcs, 3), alt, softkillResist: gd.arm ? 0.5 : ps.guide.id === "ai" ? 0.9 : 0.4,
      maneuver: 0.05, fixedOnly: !!gd.fixedOnly, arm: !!gd.arm, boat,
    };
  }
  const per = base * 40;
  return {
    cost: round(per * sqn, 1), devCost: Math.round(80 + per * 5), devMonths: ty.fighter ? 48 : 24,
    buildMonths: round(4 + sqn * 0.5, 1), upkeep: round(per * sqn * 0.012, 2),
    hp: sqn, rcs: round(rcs, 3), radarKm: num(gd.radar), mastM: alt, channels: 1, ew: 0.05,
    hardpoints: Math.floor(wh / 150), maxSize: W > 3000 ? "M" : "S", radiusKm: Math.round(range / 2),
    mach: round(mach, 2), carrier: "none", uav: true, recon: !!ty.recon, stealth: num(sg.rcs) < 0.05 ? 1 : 0, dataLink: 1,
  };
}

// ------------------------------------------------------------------ bases
function baseStats(d: Design, ps: Record<string, Part>, _issues: Issue[]): Stats {
  const ty = ps.type.a, hd = ps.hard.a, rd = ps.radar.a, pd = ps.pd.a;
  const cap = param(d, "capacity"), pads = param(d, "pads");
  const cost = num(ty.cost) + num(hd.cost) * (cap / 40) + num(rd.cost) + num(pd.cost) + pads * 15;
  return {
    cost: Math.round(cost), devCost: 0, devMonths: 0,
    buildMonths: Math.round(12 + num(hd.hard) * 24), upkeep: round(cost * 0.006, 2),
    hp: Math.round(num(ty.hp) * num(hd.hpMul, 1) * Math.pow(cap / 40, 0.3)),
    hardening: num(hd.hard), radarKm: num(rd.km), mastM: 40, channels: Math.max(0, num(rd.ch)),
    ciwsKm: num(pd.km), ciwsPk: num(pd.pk), laserKw: num(pd.laser),
    cells: pads, maxSize: "XL", capacity: cap, baseKind: str(ty.kind),
  };
}

// ------------------------------------------------------------------ loadouts
function checkLoadout(d: Design, s: Stats, lookup: Lookup, issues: Issue[], ps: Record<string, Part>) {
  let used = 0;
  const cellBased = d.category === "ship" || d.category === "vehicle" || d.category === "base";
  const vRole = d.category === "vehicle" ? str(ps.role.a.role) : "";
  for (const e of d.loadout) {
    const m = lookup(e.designId);
    if (!m) { issues.push({ sev: "error", text: "The loadout references a deleted weapon." }); continue; }
    const ms = evaluate(m, lookup).stats;
    if (!ms.role) { issues.push({ sev: "error", text: `${m.name} isn't a munition.` }); continue; }
    const size = ms.size ?? "M";
    if (!sizeFits(size, s.maxSize ?? "M")) issues.push({ sev: "error", text: `${m.name} (size ${size}) is too big for these launchers (max ${s.maxSize}).` });
    used += (cellBased ? (size === "S" ? 0.25 : 1) : SIZE_UNITS[size]) * e.count;
    const r = ms.role;
    if (r === "torp" && d.category !== "ship") issues.push({ sev: "error", text: `${m.name} is a torpedo and can only be fired from ship or submarine tubes.` });
    if (d.category === "aircraft" && (r === "sam" || r === "bmd" || r === "asbm")) issues.push({ sev: "error", text: `${m.name} can't be launched from aircraft.` });
    if (d.category === "ship" && r === "aam") issues.push({ sev: "error", text: `${m.name} is an air-to-air missile.` });
    if (d.category === "ship" && ms.boat) issues.push({ sev: "warn", text: "Drone boats are launched from the ship's boat deck. They use up cells here." });
    if (d.category === "drone" && !(r === "aam" || r === "ashm" || r === "lacm" || r === "arm")) issues.push({ sev: "error", text: `A drone can't carry ${m.name}.` });
    if (d.category === "vehicle") {
      const ok =
        vRole === "sam" ? r === "sam" || r === "bmd" :
        vRole === "tel" ? r === "srbm" || r === "lacm" || r === "hgv" || r === "asbm" :
        vRole === "drone" ? r === "loiter" :
        vRole === "shore" ? r === "ashm" || r === "asbm" : false;
      if (!ok) issues.push({ sev: "error", text: `${m.name} doesn't match this battery's role.` });
    }
  }
  const cap = (cellBased ? s.cells : s.hardpoints) ?? 0;
  used = Math.ceil(used * 100) / 100;
  if (used > cap) issues.push({ sev: "error", text: cellBased ? `The loadout needs ${used} cells but only ${cap} are available.` : `The loadout needs ${used} hardpoint units but only ${cap} are available.` });
  s.loadUsed = used;
}

function round(v: number, dp: number): number {
  const m = Math.pow(10, dp);
  return Math.round(v * m) / m;
}
