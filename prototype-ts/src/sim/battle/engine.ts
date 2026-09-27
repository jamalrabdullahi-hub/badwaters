// Real-time 2D battle engine shared by naval engagements, air battles and strategic missile strikes.
// Units live on a local plane in km (x east, y north). One tick = 1 simulated second.
import type { MunitionRole, NationId, Stats } from "../types";
import { rng } from "../campaign/core";

export type Side = 0 | 1;
export type UKind = "ship" | "sub" | "air" | "ground" | "base" | "uav";
export type Mission = "cap" | "strike" | "aew" | "mpa" | "patrol" | "hold" | "rtb" | "sub";

export const MACH = 0.343; // km/s
export const KTS = 1.852 / 3600; // km/s per knot
/** A base covers square kilometres: one warhead wrecks a hangar or a runway section, not the base. */
export const BASE_DILUTION = 0.15;

export interface BWeapon {
  designId: string;
  name: string;
  count: number;
  start: number;
  s: Stats;
}

export interface BUnit {
  id: string;
  side: Side;
  nation: NationId;
  name: string;
  kind: UKind;
  designId: string;
  campaignUnitId?: string;
  campaignBaseId?: string;
  x: number;
  y: number;
  alt: number; // m
  speed: number; // km/s
  wx?: number;
  wy?: number;
  hp: number;
  maxHp: number;
  rcs: number;
  radarKm: number;
  sensorAlt: number; // m
  channels: number;
  ew: number;
  ciwsKm: number;
  ciwsPk: number;
  laserKw: number;
  weapons: BWeapon[];
  alive: boolean;
  landed: boolean;
  mission: Mission;
  homeX: number;
  homeY: number;
  fuel: number; // s
  targetId?: string;
  cooldown: number;
  emitting: boolean;
  datalink: number;
  mobility: number;
  hardening: number;
  agility: number;
  aircraft: number; // aircraft in this flight (air/uav)
  parked: { campaignUnitId: string; aircraft: number; lost: number }[]; // bases
  revealedUntil: number; // subs: datum after firing
  asw: boolean;
  aew: boolean;
  lastHitBy?: Side;
}

export interface BMissile {
  id: string;
  side: Side;
  designId: string;
  name: string;
  s: Stats;
  role: MunitionRole;
  x: number;
  y: number;
  alt: number;
  speed: number; // km/s
  targetId: string;
  targetIsMissile: boolean;
  aimX: number;
  aimY: number;
  launcherId: string;
  distLeft: number;
  total: number;
  flown: number;
  alive: boolean;
  apogee: number; // m, ballistic
  trail: [number, number][];
}

export interface LogLine {
  t: number;
  side: Side | -1;
  text: string;
  cls: "h" | "f" | "w" | "n";
}

export interface Doctrine {
  offense: "auto" | "manual";
  defense: "free" | "tight" | "hold";
  salvo: number;
}

export interface BattleState {
  id: string;
  kind: "strike" | "engagement";
  title: string;
  nations: [NationId, NationId];
  playerSide: Side | -1;
  lat0: number;
  lon0: number;
  t: number;
  units: BUnit[];
  missiles: BMissile[];
  tracks: [Set<string>, Set<string>];
  log: LogLine[];
  over: boolean;
  winner: Side | -1;
  doctrine: [Doctrine, Doctrine];
  sat: [number, number]; // 0 none, 1 periodic fixes, 2 continuous
  nextId: number;
  rand: () => number;
  timeLimit: number;
  quietSince: number;
  objectiveId?: string; // strike target base unit id
  counts: { launched: [number, number]; intercepts: [number, number]; hits: [number, number]; decoyed: [number, number] };
  ix: Index;
}

/** Per-tick lookup tables so hot loops avoid linear scans. */
interface Index {
  um: Map<string, BUnit>;
  mm: Map<string, BMissile>;
  assigned: Map<string, number>; // interceptors in flight per threat
  guided: Map<string, number>; // guided interceptors in flight per launcher (fire channels)
  incoming: Map<string, number>; // offensive weapons in flight per target
}

function reindex(b: BattleState) {
  const ix: Index = { um: new Map(), mm: new Map(), assigned: new Map(), guided: new Map(), incoming: new Map() };
  for (const u of b.units) ix.um.set(u.id, u);
  for (const m of b.missiles) {
    if (!m.alive) continue;
    ix.mm.set(m.id, m);
    if (isInterceptor(m.role)) {
      ix.assigned.set(m.targetId, (ix.assigned.get(m.targetId) ?? 0) + 1);
      if (m.s.sarh || m.flown < 10) ix.guided.set(m.launcherId, (ix.guided.get(m.launcherId) ?? 0) + 1);
    } else if (!m.targetIsMissile) {
      ix.incoming.set(m.targetId, (ix.incoming.get(m.targetId) ?? 0) + 1);
    }
  }
  b.ix = ix;
}

const other = (s: Side): Side => (s === 0 ? 1 : 0);
const dist = (a: { x: number; y: number }, b: { x: number; y: number }) => Math.hypot(a.x - b.x, a.y - b.y);
const horizon = (h1: number, h2: number) => 4.12 * (Math.sqrt(Math.max(1, h1)) + Math.sqrt(Math.max(1, h2)));

export function log(b: BattleState, side: Side | -1, text: string, cls: LogLine["cls"] = "n") {
  b.log.unshift({ t: b.t, side, text, cls });
  if (b.log.length > 400) b.log.length = 400;
}

export function createBattle(init: {
  id: string; kind: "strike" | "engagement"; title: string; nations: [NationId, NationId]; playerSide: Side | -1;
  lat0: number; lon0: number; units: BUnit[]; sat: [number, number]; seed: number; objectiveId?: string; timeLimit?: number;
}): BattleState {
  const b: BattleState = {
    ...init,
    t: 0, missiles: [], tracks: [new Set(), new Set()], log: [], over: false, winner: -1,
    doctrine: [{ offense: "auto", defense: "free", salvo: 4 }, { offense: "auto", defense: "free", salvo: 4 }],
    nextId: 1, rand: rng(init.seed), timeLimit: init.timeLimit ?? 6 * 3600, quietSince: 0,
    counts: { launched: [0, 0], intercepts: [0, 0], hits: [0, 0], decoyed: [0, 0] },
    ix: { um: new Map(), mm: new Map(), assigned: new Map(), guided: new Map(), incoming: new Map() },
  };
  reindex(b);
  detect(b);
  return b;
}

// ---------------------------------------------------------------- classification
type ThreatClass = "air" | "cruise" | "ballistic" | "hyper" | "torp" | "none";
function missileClass(m: BMissile): ThreatClass {
  switch (m.role) {
    case "srbm": case "asbm": return "ballistic";
    case "hgv": return "hyper";
    case "torp": return "torp";
    case "sam": case "aam": case "bmd": return "none";
    default: return "cruise";
  }
}
const isInterceptor = (r?: MunitionRole) => r === "sam" || r === "aam" || r === "bmd";
const isOffensive = (r?: MunitionRole) => !!r && !isInterceptor(r);

function capability(s: Stats, cls: ThreatClass | "unit-air"): number {
  if (cls === "unit-air" || cls === "air" || cls === "cruise") return s.interceptAir ?? 0;
  if (cls === "ballistic") return s.interceptBallistic ?? 0;
  if (cls === "hyper") return s.interceptHyper ?? 0;
  return 0;
}

/** Can this interceptor physically engage a threat at its current altitude? */
function altOk(s: Stats, threatAlt: number, cls: ThreatClass | "unit-air"): boolean {
  if (s.role === "aam") return threatAlt < 25000 && threatAlt > -1;
  if (s.exo) return threatAlt > 50000 || (cls === "hyper" && (s.interceptHyper ?? 0) >= 0.5);
  return threatAlt < 30000 && threatAlt > -1;
}

function validOffTarget(s: Stats, t: BUnit): boolean {
  if (!t.alive || t.landed) return false;
  switch (s.role) {
    case "ashm": case "asbm": return t.kind === "ship";
    case "hgv": return t.kind === "ship" || t.kind === "base" || t.kind === "ground";
    case "lacm": case "srbm": return t.kind === "base" || t.kind === "ground";
    case "arm": return t.emitting && t.radarKm > 0 && (t.kind === "ship" || t.kind === "ground" || t.kind === "base");
    case "loiter":
      if (s.boat) return t.kind === "ship";
      if (s.arm) return t.emitting && t.radarKm > 0 && t.kind !== "air" && t.kind !== "uav" && t.kind !== "sub";
      if (s.fixedOnly) return t.kind === "base" || t.kind === "ground";
      return t.kind === "ship" || t.kind === "ground" || t.kind === "base";
    case "torp": return t.kind === "ship" || t.kind === "sub";
    default: return false;
  }
}

// ---------------------------------------------------------------- detection
function detect(b: BattleState) {
  for (const side of [0, 1] as Side[]) {
    const tr = new Set<string>();
    const prev = b.tracks[side];
    const sensors = b.units.filter((u) => u.side === side && u.alive && !u.landed);
    const enemies = b.units.filter((u) => u.side !== side && u.alive && !u.landed);
    // satellites: continuous ship tracking, or a 5-minute fix every 30 minutes
    const satFix = b.sat[side] === 2 || (b.sat[side] === 1 && b.t % 1800 < 300);
    for (const e of enemies) {
      if (e.kind === "base") { tr.add(e.id); continue; } // fixed installations are always on the target list
      if (satFix && e.kind === "ship") { tr.add(e.id); continue; }
      for (const s of sensors) {
        if (canSee(b, s, e.x, e.y, e.alt, e.rcs, e)) { tr.add(e.id); break; }
      }
    }
    const radars = sensors.filter((s) => s.radarKm > 0 && s.emitting);
    for (const m of b.missiles) {
      if (m.side === side || !m.alive) continue;
      if (m.role === "torp") { const tu = b.ix.um.get(m.targetId); if (tu && dist(tu, m) < 6) tr.add(m.id); continue; }
      if (prev.has(m.id)) { tr.add(m.id); continue; } // once held, a missile track is kept until it dies
      for (const s of radars) {
        if (canSee(b, s, m.x, m.y, m.alt, m.s.rcs ?? 0.1)) { tr.add(m.id); break; }
      }
    }
    b.tracks[side] = tr;
  }
}

function canSee(b: BattleState, s: BUnit, x: number, y: number, alt: number, rcs: number, e?: BUnit): boolean {
  const d = Math.hypot(s.x - x, s.y - y);
  if (e?.kind === "sub") {
    if (e.revealedUntil > b.t && d < 150) return true;
    if (s.kind === "ship" && d < (s.asw ? 22 : 12)) return true;
    if (s.kind === "air" && s.mission === "mpa" && d < 45) return true;
    if (s.kind === "sub" && d < 30) return true;
    return false;
  }
  if (s.kind === "sub") return d < 25 && alt < 100; // periscope / passive sonar on surface ships
  if (s.radarKm <= 0 || !s.emitting) return false;
  if (d > s.radarKm * 4) return false; // nothing in this game has an RCS over 256 m² … except ships, handled by the horizon
  const jam = e ? e.ew * 0.45 : 0;
  const r = s.radarKm * Math.pow(Math.max(1e-4, rcs), 0.25) * (1 - jam) * (0.5 + 0.5 * (s.hp / s.maxHp));
  if (d > r) return false;
  return d <= horizon(s.sensorAlt, alt);
}

/** Does this unit have its own firing solution (own radar), or a shared one via CEC? */
function hasSolution(b: BattleState, u: BUnit, x: number, y: number, alt: number, rcs: number, id: string): boolean {
  if (!b.tracks[u.side].has(id)) return false;
  if (u.datalink >= 2) return true;
  return canSee(b, u, x, y, alt, rcs, b.ix.um.get(id));
}

// ---------------------------------------------------------------- launching
export function launch(b: BattleState, u: BUnit, w: BWeapon, targetId: string, targetIsMissile: boolean): BMissile | null {
  if (w.count <= 0) return null;
  const tgt = targetIsMissile ? b.ix.mm.get(targetId) : b.ix.um.get(targetId);
  if (!tgt) return null;
  w.count--;
  const s = w.s;
  const kind = s.role === "srbm" || s.role === "asbm" ? "ballistic" : s.role === "hgv" ? "glide" : "other";
  const range = s.rangeKm ?? 50;
  const total = Math.max(1, dist(u, tgt));
  const m: BMissile = {
    id: `m${b.nextId++}`, side: u.side, designId: w.designId, name: w.name, s, role: s.role ?? "sam",
    x: u.x, y: u.y, alt: u.kind === "sub" ? -20 : Math.max(u.alt, 5), speed: (s.mach ?? 1) * MACH,
    targetId, targetIsMissile, aimX: tgt.x, aimY: tgt.y, launcherId: u.id,
    distLeft: range * 1.05, total, flown: 0, alive: true,
    apogee: kind === "ballistic" ? Math.min(300000, Math.max(30000, total * 250)) : kind === "glide" ? 40000 : 0,
    trail: [],
  };
  b.missiles.push(m);
  b.ix.mm.set(m.id, m);
  if (isInterceptor(m.role)) {
    b.ix.assigned.set(targetId, (b.ix.assigned.get(targetId) ?? 0) + 1);
    b.ix.guided.set(u.id, (b.ix.guided.get(u.id) ?? 0) + 1);
  } else if (!targetIsMissile) b.ix.incoming.set(targetId, (b.ix.incoming.get(targetId) ?? 0) + 1);
  b.counts.launched[u.side]++;
  u.cooldown = u.kind === "air" || u.kind === "uav" ? 1 : u.kind === "ship" ? 2 : u.kind === "sub" ? 20 : 4;
  if (u.kind === "sub" && s.role !== "torp") u.revealedUntil = b.t + 300;
  return m;
}

/** Player order: fire `n` rounds of a weapon at a tracked target. Returns an error message or null. */
export function manualLaunch(b: BattleState, unitId: string, designId: string, targetId: string, n: number): string | null {
  const u = b.units.find((x) => x.id === unitId && x.alive);
  if (!u) return "That unit is gone.";
  const w = u.weapons.find((x) => x.designId === designId);
  if (!w || w.count <= 0) return "No rounds left of that weapon.";
  const t = b.units.find((x) => x.id === targetId && x.alive);
  if (!t) return "Target is gone.";
  if (!b.tracks[u.side].has(t.id)) return "No track on that target.";
  if (!validOffTarget(w.s, t)) return `${w.name} can't engage that kind of target.`;
  if (dist(u, t) > (w.s.rangeKm ?? 0)) return `Out of range: ${Math.round(dist(u, t))} km, max ${w.s.rangeKm} km.`;
  if (u.datalink < 1 && !canSee(b, u, t.x, t.y, t.alt, t.rcs, t)) return "This unit has no datalink and can't see the target itself.";
  let fired = 0;
  for (let i = 0; i < n && w.count > 0; i++) if (launch(b, u, w, t.id, false)) fired++;
  u.cooldown = 0;
  log(b, u.side, `${u.name}: ${fired}× ${w.name} → ${t.name}`, "f");
  return null;
}

export function setWaypoint(b: BattleState, unitId: string, x: number, y: number) {
  const u = b.units.find((z) => z.id === unitId);
  if (!u || !u.alive) return;
  if (u.kind === "base" || u.kind === "ground") return;
  u.wx = x; u.wy = y;
  if (u.kind === "air" || u.kind === "uav") u.mission = u.mission === "rtb" ? "patrol" : u.mission;
}

// ---------------------------------------------------------------- AI: offense
function incomingOn(b: BattleState, id: string): number {
  return b.ix.incoming.get(id) ?? 0;
}

function offense(b: BattleState) {
  for (const u of b.units) {
    if (!u.alive || u.landed || u.cooldown > 0) continue;
    if (b.doctrine[u.side].offense === "manual" && u.side === b.playerSide) continue;
    const weapons = u.weapons.filter((w) => w.count > 0 && isOffensive(w.s.role));
    if (!weapons.length) continue;
    if ((u.kind === "air" || u.kind === "uav") && u.mission !== "strike" && u.mission !== "mpa" && u.mission !== "cap") continue;
    const enemies = b.units.filter((e) => e.side !== u.side && b.tracks[u.side].has(e.id));
    for (const w of weapons) {
      const cands = enemies
        .filter((e) => validOffTarget(w.s, e) && dist(u, e) <= (w.s.rangeKm ?? 0) * 0.95)
        .filter((e) => u.datalink >= 1 || canSee(b, u, e.x, e.y, e.alt, e.rcs, e));
      if (!cands.length) continue;
      // prefer the strike objective, then high-value targets, then the closest
      cands.sort((p, q) => score(b, q) - score(b, p) || dist(u, p) - dist(u, q));
      const t = cands[0];
      const already = incomingOn(b, t.id);
      const perHit = Math.max(1, (w.s.warhead ?? 100) * (1 + Math.max(0, (w.s.mach ?? 1) - 2) * 0.15));
      const strike = b.kind === "strike" && u.side !== b.units.find((x) => x.id === b.objectiveId)?.side;
      const eff = t.kind === "base" ? perHit * BASE_DILUTION : perHit;
      // strike packages saturate their objective; otherwise fire enough to kill with a margin for interceptions
      const needed = strike && t.id === b.objectiveId ? Infinity : Math.ceil((t.hp / eff) * 2.5) + (t.kind === "ship" ? 2 : 0);
      if (already >= needed) continue;
      const salvo = Math.min(w.count, needed - already, strike || u.kind === "air" ? w.count : b.doctrine[u.side].salvo);
      let fired = 0;
      for (let i = 0; i < salvo; i++) if (launch(b, u, w, t.id, false)) fired++;
      if (fired) {
        log(b, u.side, `${u.name} fires ${fired}× ${w.name} at ${t.name}`, "f");
        u.cooldown = u.kind === "ground" ? 30 : u.kind === "sub" ? 60 : 10;
        if (u.kind === "air" && u.mission === "strike" && !u.weapons.some((x) => x.count > 0 && isOffensive(x.s.role))) u.mission = "rtb";
        break;
      }
    }
  }
}

function score(b: BattleState, t: BUnit): number {
  let s = t.maxHp;
  if (t.id === b.objectiveId) s += 1e6;
  if (t.kind === "ship" && t.maxHp > 2000) s += 5000; // carriers
  return s;
}

// ---------------------------------------------------------------- AI: defence
interface Threat { id: string; x: number; y: number; alt: number; rcs: number; speed: number; cls: ThreatClass | "unit-air"; missile?: BMissile; unit?: BUnit; cost: number; }

function threatsFor(b: BattleState, side: Side): Threat[] {
  const out: Threat[] = [];
  for (const m of b.missiles) {
    if (!m.alive || m.side === side || !b.tracks[side].has(m.id)) continue;
    const cls = missileClass(m);
    if (cls === "none" || cls === "torp") continue;
    out.push({ id: m.id, x: m.x, y: m.y, alt: m.alt, rcs: m.s.rcs ?? 0.1, speed: m.speed, cls, missile: m, cost: m.s.cost });
  }
  if (b.doctrine[side].defense === "free") {
    for (const u of b.units) {
      if (!u.alive || u.landed || u.side === side || (u.kind !== "air" && u.kind !== "uav") || !b.tracks[side].has(u.id)) continue;
      out.push({ id: u.id, x: u.x, y: u.y, alt: u.alt, rcs: u.rcs, speed: u.speed, cls: "unit-air", unit: u, cost: 50 });
    }
  }
  return out;
}

function assigned(b: BattleState, id: string): number {
  return b.ix.assigned.get(id) ?? 0;
}

function inFlightGuided(b: BattleState, u: BUnit): number {
  return b.ix.guided.get(u.id) ?? 0;
}

function defense(b: BattleState) {
  for (const side of [0, 1] as Side[]) {
    if (b.doctrine[side].defense === "hold") continue;
    const threats = threatsFor(b, side);
    if (!threats.length) continue;
    const friends = b.units.filter((u) => u.side === side && u.alive && !u.landed);
    const shootersPool = friends.filter((u) => u.weapons.some((w) => w.count > 0 && isInterceptor(w.s.role)));
    if (!shootersPool.length) continue;
    // closest to any friend first
    const ttd = new Map<string, number>();
    for (const t of threats) {
      let md = Infinity;
      for (const f of friends) { const d = dist(f, t); if (d < md) md = d; }
      ttd.set(t.id, md / Math.max(0.01, t.speed));
    }
    threats.sort((p, q) => ttd.get(p.id)! - ttd.get(q.id)!);
    for (const t of threats) {
      const want = t.cls === "unit-air" ? 1 : t.cls === "ballistic" || t.cls === "hyper" ? 2 : t.cost < 0.2 ? 1 : 2;
      let have = assigned(b, t.id);
      if (have >= want) continue;
      const shooters: { u: BUnit; w: BWeapon; sc: number }[] = [];
      for (const u of shootersPool) {
        if (u.cooldown > 0) continue;
        if ((u.kind === "air" || u.kind === "uav") && u.mission === "strike") continue;
        const d = dist(u, t);
        for (const w of u.weapons) {
          if (w.count <= 0 || !isInterceptor(w.s.role)) continue;
          const cap = capability(w.s, t.cls);
          if (cap <= 0.05) continue;
          if (d > (w.s.rangeKm ?? 0) * 0.9) continue;
          if (!altOk(w.s, t.alt, t.cls)) continue;
          if (w.s.role !== "aam" && inFlightGuided(b, u) >= u.channels) continue;
          if (w.s.role === "aam" && u.kind !== "air" && u.kind !== "uav") continue;
          if (!hasSolution(b, u, t.x, t.y, t.alt, t.rcs, t.id)) continue;
          // cheaper interceptors for cheap threats; better capability first
          const sc = cap * (w.s.pk ?? 0.5) / Math.pow(Math.max(0.05, w.s.cost), t.cost < 0.5 ? 0.7 : 0.15);
          shooters.push({ u, w, sc });
        }
      }
      shooters.sort((p, q) => q.sc - p.sc);
      for (const sh of shooters) {
        if (have >= want) break;
        if (sh.u.cooldown > 0) continue;
        if (launch(b, sh.u, sh.w, t.id, !!t.missile)) {
          have++;
          sh.u.cooldown = sh.u.kind === "ship" ? 1 : sh.u.kind === "air" ? 2 : 2;
        }
      }
    }
  }
}

function pointDefense(b: BattleState, dt: number) {
  for (const u of b.units) {
    if (!u.alive || u.landed || u.ciwsKm <= 0) continue;
    let shots = u.laserKw > 0 ? 3 : 2;
    for (const m of b.missiles) {
      if (!m.alive || m.side === u.side || shots <= 0) continue;
      const cls = missileClass(m);
      if (cls === "none" || cls === "torp") continue;
      if (dist(u, m) > u.ciwsKm || m.alt > 20000) continue;
      shots--;
      let p = u.ciwsPk;
      if ((m.s.mach ?? 1) > 2) p *= u.laserKw >= 1000 ? 0.85 : 0.55;
      if (cls === "ballistic") p *= 0.25;
      if (cls === "hyper") p *= 0.2;
      if (m.role === "loiter" && u.laserKw > 0) p = Math.min(0.95, p * 1.3);
      p *= 1 - (m.s.maneuver ?? 0) * 0.4;
      if (b.rand() < 1 - Math.pow(1 - p * 0.5, dt)) {
        m.alive = false;
        b.counts.intercepts[u.side]++;
        log(b, u.side, `${u.name} point defence kills ${m.name}`, "f");
      }
    }
  }
}

function asw(b: BattleState) {
  for (const u of b.units) {
    if (!u.alive || u.landed || !u.asw) continue;
    for (const e of b.units) {
      if (!e.alive || e.side === u.side || e.kind !== "sub") continue;
      const range = u.kind === "air" ? 40 : 20;
      if (dist(u, e) > range || !b.tracks[u.side].has(e.id)) continue;
      if (b.rand() < 0.2) {
        const dmg = e.maxHp * (0.35 + b.rand() * 0.4);
        e.hp -= dmg;
        e.lastHitBy = u.side;
        log(b, u.side, `${u.name} ASW attack hits ${e.name}`, "f");
        if (e.hp <= 0) kill(b, e, `${e.name} sunk by ASW attack`);
      }
    }
  }
}

// ---------------------------------------------------------------- movement & missions
function stepUnits(b: BattleState, dt: number) {
  for (const u of b.units) {
    if (!u.alive || u.landed) continue;
    u.cooldown = Math.max(0, u.cooldown - dt);
    if (u.kind === "air" || u.kind === "uav") airAI(b, u, dt);
    else if (u.kind === "sub") subAI(b, u);
    else if (u.kind === "ship" && every(b, 30, dt)) shipAI(b, u);
    if (u.wx !== undefined && u.wy !== undefined && u.speed > 0) {
      const d = Math.hypot(u.wx - u.x, u.wy - u.y);
      const step = u.speed * dt;
      if (d <= step) { u.x = u.wx; u.y = u.wy; if (u.kind === "ship" || u.kind === "sub") { u.wx = undefined; u.wy = undefined; } }
      else { u.x += ((u.wx - u.x) / d) * step; u.y += ((u.wy - u.y) / d) * step; }
    }
  }
}

function nearestTrack(b: BattleState, u: BUnit, pred: (e: BUnit) => boolean): BUnit | undefined {
  let best: BUnit | undefined;
  let bd = Infinity;
  for (const e of b.units) {
    if (!e.alive || e.landed || e.side === u.side || !b.tracks[u.side].has(e.id) || !pred(e)) continue;
    const d = dist(u, e);
    if (d < bd) { bd = d; best = e; }
  }
  return best;
}

function airAI(b: BattleState, u: BUnit, dt: number) {
  u.fuel -= dt;
  const home = Math.hypot(u.homeX - u.x, u.homeY - u.y);
  if (u.mission !== "rtb" && u.fuel < home / Math.max(0.05, u.speed) + 600) {
    u.mission = "rtb";
    log(b, u.side, `${u.name} bingo fuel, returning to base`, "n");
  }
  if (u.fuel <= 0) { kill(b, u, `${u.name} ran out of fuel`); return; }
  if (!every(b, 4, dt) && u.mission !== "rtb") return; // decisions every few seconds; movement continues
  switch (u.mission) {
    case "rtb":
      u.wx = u.homeX; u.wy = u.homeY;
      if (home < 5) { u.landed = true; }
      break;
    case "aew": {
      const threat = nearestTrack(b, u, () => true);
      const ax = threat ? threat.x : u.homeX + 1;
      const ay = threat ? threat.y : u.homeY;
      const dd = Math.max(1, Math.hypot(ax - u.homeX, ay - u.homeY));
      const orbit = Math.min(150, dd * 0.3);
      u.wx = u.homeX + ((ax - u.homeX) / dd) * orbit + Math.cos(b.t / 300) * 20;
      u.wy = u.homeY + ((ay - u.homeY) / dd) * orbit + Math.sin(b.t / 300) * 20;
      break;
    }
    case "cap": {
      const bandit = nearestTrack(b, u, (e) => e.kind === "air" || e.kind === "uav");
      const hasAam = u.weapons.some((w) => w.count > 0 && w.s.role === "aam");
      if (bandit && hasAam && Math.hypot(bandit.x - u.homeX, bandit.y - u.homeY) < 600) { u.wx = bandit.x; u.wy = bandit.y; }
      else if (!hasAam && u.weapons.every((w) => w.count === 0)) u.mission = "rtb";
      else if (u.wx === undefined || Math.hypot((u.wx ?? 0) - u.x, (u.wy ?? 0) - u.y) < 3) {
        const a = b.rand() * Math.PI * 2;
        const cx = (u.targetId ? b.units.find((x) => x.id === u.targetId) : undefined) ?? { x: u.homeX, y: u.homeY };
        u.wx = cx.x + Math.cos(a) * 60; u.wy = cx.y + Math.sin(a) * 60;
      }
      break;
    }
    case "strike": {
      const obj = u.targetId ? b.units.find((x) => x.id === u.targetId && x.alive) : undefined;
      const t = obj ?? nearestTrack(b, u, (e) => u.weapons.some((w) => w.count > 0 && validOffTarget(w.s, e)));
      if (!u.weapons.some((w) => w.count > 0 && isOffensive(w.s.role))) { u.mission = "rtb"; break; }
      if (t) {
        const maxR = Math.max(...u.weapons.filter((w) => w.count > 0 && isOffensive(w.s.role)).map((w) => w.s.rangeKm ?? 0));
        const d = dist(u, t);
        if (d > maxR * 0.8) { u.wx = t.x; u.wy = t.y; }
        else { u.wx = u.x; u.wy = u.y; b.tracks[u.side].add(t.id); }
      } else { u.mission = "rtb"; }
      break;
    }
    case "mpa": {
      const ship = nearestTrack(b, u, (e) => e.kind === "ship" || e.kind === "sub");
      if (ship && dist(u, ship) > 60) { u.wx = ship.x; u.wy = ship.y; }
      else if (!ship && (u.wx === undefined || Math.hypot((u.wx ?? 0) - u.x, (u.wy ?? 0) - u.y) < 3)) {
        const a = b.rand() * Math.PI * 2;
        u.wx = u.x + Math.cos(a) * 150; u.wy = u.y + Math.sin(a) * 150;
      }
      break;
    }
    default:
      break;
  }
}

/** Surface ships with anti-ship weapons close to firing range on the nearest tracked enemy ship. */
function shipAI(b: BattleState, u: BUnit) {
  if (u.side === b.playerSide && u.wx !== undefined) return;
  const ranges = u.weapons.filter((w) => w.count > 0 && (w.s.role === "ashm" || w.s.role === "hgv")).map((w) => w.s.rangeKm ?? 0);
  if (!ranges.length) return;
  const t = nearestTrack(b, u, (e) => e.kind === "ship");
  if (!t) return;
  const reach = Math.max(...ranges) * 0.85;
  if (dist(u, t) > reach) { u.wx = t.x; u.wy = t.y; }
  else { u.wx = undefined; u.wy = undefined; }
}

function subAI(b: BattleState, u: BUnit) {
  if (u.side === b.playerSide && u.wx !== undefined) return;
  const t = nearestTrack(b, u, (e) => e.kind === "ship");
  if (t) {
    const torp = u.weapons.find((w) => w.count > 0 && w.s.role === "torp");
    const stand = torp ? (torp.s.rangeKm ?? 30) * 0.6 : 80;
    const d = dist(u, t);
    if (d > stand) { u.wx = t.x; u.wy = t.y; }
  }
}

// ---------------------------------------------------------------- missiles
function stepMissiles(b: BattleState, dt: number) {
  for (const m of b.missiles) {
    if (!m.alive) continue;
    const tgt = m.targetIsMissile ? b.ix.mm.get(m.targetId) : b.ix.um.get(m.targetId);
    const tAlive = !!tgt && (m.targetIsMissile ? (tgt as BMissile).alive : (tgt as BUnit).alive && !(tgt as BUnit).landed);
    if (!tAlive) {
      if (isInterceptor(m.role)) { m.alive = false; continue; } // self-destruct
      // offensive: fly on to last aimpoint
    } else {
      const t = tgt as { x: number; y: number };
      if (isInterceptor(m.role)) {
        const tv = m.targetIsMissile ? (tgt as BMissile).speed : (tgt as BUnit).speed;
        const lead = Math.min(30, dist(m, t) / Math.max(0.2, m.speed));
        const tt = tgt as BMissile;
        const dirX = m.targetIsMissile ? tt.aimX - tt.x : ((tgt as BUnit).wx ?? t.x) - t.x;
        const dirY = m.targetIsMissile ? tt.aimY - tt.y : ((tgt as BUnit).wy ?? t.y) - t.y;
        const dl = Math.hypot(dirX, dirY) || 1;
        m.aimX = t.x + (dirX / dl) * tv * lead;
        m.aimY = t.y + (dirY / dl) * tv * lead;
      } else if (!m.s.fixedOnly) {
        // seekers home on the target; INS/GPS weapons fly to the coordinates given at launch
        m.aimX = t.x; m.aimY = t.y;
      }
    }
    const dx = m.aimX - m.x, dy = m.aimY - m.y;
    const d = Math.hypot(dx, dy);
    const step = m.speed * dt;
    if (every(b, 6, dt)) { m.trail.push([m.x, m.y]); if (m.trail.length > 24) m.trail.shift(); }
    // altitude profile
    if (m.apogee > 0) {
      const p = Math.min(1, m.flown / Math.max(1, m.total));
      if (m.role === "hgv") m.alt = m.total - m.flown < 60 ? Math.max(20, (m.total - m.flown) * 600) : m.apogee;
      else m.alt = Math.max(20, m.apogee * 4 * p * (1 - p));
    } else if (isInterceptor(m.role) && tAlive) {
      const ta = (tgt as { alt: number }).alt;
      m.alt += (ta - m.alt) * Math.min(1, dt * 0.3);
    } else if (!isInterceptor(m.role)) {
      m.alt = m.s.alt ?? m.alt;
    }
    if (d <= step + 0.3) {
      m.x = m.aimX; m.y = m.aimY;
      resolveArrival(b, m, tAlive ? tgt : undefined);
      continue;
    }
    m.x += (dx / d) * step;
    m.y += (dy / d) * step;
    m.flown += step;
    m.distLeft -= step;
    if (m.distLeft <= 0) m.alive = false;
  }
  if (every(b, 30, dt)) b.missiles = b.missiles.filter((m) => m.alive);
}

function resolveArrival(b: BattleState, m: BMissile, tgt: BUnit | BMissile | undefined) {
  m.alive = false;
  if (!tgt) return;
  const r = b.rand();
  if (m.targetIsMissile) {
    const t = tgt as BMissile;
    const cls = missileClass(t);
    const p = (m.s.pk ?? 0.5) * capability(m.s, cls) * (1 - (t.s.maneuver ?? 0) * 0.5) * ((t.s.rcs ?? 1) < 0.01 ? 0.8 : 1);
    if (r < p) {
      t.alive = false;
      b.counts.intercepts[m.side]++;
      log(b, m.side, `${m.name} intercepts ${t.name}`, "f");
    }
    return;
  }
  const t = tgt as BUnit;
  if (t.kind === "air" || t.kind === "uav") {
    const p = (m.s.pk ?? 0.5) * (1 - t.ew * 0.5) * (1 - t.agility * 0.2);
    if (r < p) {
      t.hp -= 1;
      t.aircraft = Math.max(0, t.aircraft - 1);
      t.lastHitBy = m.side;
      b.counts.hits[m.side]++;
      log(b, m.side, `${m.name} shoots down an aircraft of ${t.name}`, "f");
      if (t.hp <= 0) kill(b, t, `${t.name} destroyed`);
    }
    return;
  }
  // soft kill: decoys and jamming
  const sk = t.ew * (1 - (m.s.softkillResist ?? 0.3));
  if ((t.kind === "ship" || t.kind === "ground" || t.kind === "base") && b.rand() < sk) {
    b.counts.decoyed[t.side]++;
    log(b, t.side, `${t.name} decoys ${m.name}`, "f");
    return;
  }
  // mobile launchers may have moved since targeting
  if (t.kind === "ground" && m.s.fixedOnly && b.rand() < t.mobility * Math.min(1, m.flown / 200)) {
    log(b, t.side, `${m.name} hits an empty launch site: ${t.name} had relocated`, "f");
    return;
  }
  let p = m.s.pk ?? 0.6;
  if (t.kind === "base") p = Math.min(0.97, p + 0.2);
  if (r < p) {
    const raw = (m.s.warhead ?? 100) * (1 + Math.max(0, (m.s.mach ?? 1) - 2) * 0.15) * (m.role === "torp" ? 2.5 : 1);
    const dmg = t.kind === "base" ? raw * BASE_DILUTION : raw;
    t.hp -= dmg;
    t.lastHitBy = m.side;
    b.counts.hits[m.side]++;
    log(b, m.side, `${m.name} hits ${t.name}`, "h");
    if (t.kind === "base") {
      for (const pk of t.parked) {
        const lossP = (1 - t.hardening) * Math.min(1, raw / 600);
        if (pk.aircraft - pk.lost > 0 && b.rand() < lossP) pk.lost += 1 + Math.floor(b.rand() * 2 * (1 - t.hardening));
        pk.lost = Math.min(pk.aircraft, pk.lost);
      }
      // a damaged base loses radar and point defence
      if (t.hp < t.maxHp * 0.5) { t.radarKm *= 0.6; t.ciwsPk *= 0.6; }
    }
    if (t.hp <= 0) kill(b, t, t.kind === "base" ? `${t.name} knocked out` : `${t.name} destroyed`);
  }
}

function kill(b: BattleState, u: BUnit, text: string) {
  if (!u.alive) return;
  u.alive = false;
  u.hp = Math.min(u.hp, 0);
  log(b, u.side, text, "w");
}

// ---------------------------------------------------------------- tick
/** True when the clock crossed a multiple of `period` during this tick. */
const every = (b: BattleState, period: number, dt: number) => Math.floor(b.t / period) !== Math.floor((b.t - dt) / period);

export function step(b: BattleState, dt = 1) {
  if (b.over) return;
  b.t += dt;
  reindex(b);
  stepUnits(b, dt);
  stepMissiles(b, dt);
  if (every(b, 3, dt)) detect(b);
  if (every(b, 2, dt)) defense(b);
  pointDefense(b, dt);
  if (every(b, 5, dt)) offense(b);
  if (every(b, 30, dt)) asw(b);
  checkEnd(b);
}

function sidePower(b: BattleState, side: Side) {
  let offensiveRounds = 0, alive = 0;
  for (const u of b.units) {
    if (u.side !== side || !u.alive) continue;
    alive++;
    if (u.landed) continue;
    for (const w of u.weapons) if (isOffensive(w.s.role)) offensiveRounds += w.count;
  }
  return { offensiveRounds, alive };
}

function checkEnd(b: BattleState) {
  const inFlight = b.missiles.some((m) => m.alive && !isInterceptor(m.role));
  const airborne = b.units.some((u) => u.alive && !u.landed && (u.kind === "air" || u.kind === "uav") && (u.mission === "strike" || u.mission === "mpa"));
  if (inFlight || airborne) b.quietSince = b.t;
  const p0 = sidePower(b, 0), p1 = sidePower(b, 1);
  let end = false;
  if (b.kind === "strike") {
    const obj = b.units.find((u) => u.id === b.objectiveId);
    const attacker = obj ? other(obj.side) : 0;
    const ap = attacker === 0 ? p0 : p1;
    if (!inFlight && !airborne && (ap.offensiveRounds === 0 || b.t - b.quietSince > 900)) end = true;
    if (obj && !obj.alive && !inFlight) end = true;
  } else {
    if (p0.alive === 0 || p1.alive === 0) end = true;
    if (b.t - b.quietSince > 9000) end = true;
    if (p0.offensiveRounds === 0 && p1.offensiveRounds === 0 && !inFlight) end = true;
  }
  if (b.t >= b.timeLimit) end = true;
  if (end) finish(b);
}

function finish(b: BattleState) {
  b.over = true;
  const lost = [0, 0];
  for (const u of b.units) {
    const frac = u.alive ? Math.max(0, 1 - u.hp / u.maxHp) : 1;
    lost[u.side] += frac * u.maxHp;
  }
  if (b.kind === "strike") {
    const obj = b.units.find((u) => u.id === b.objectiveId);
    if (obj) {
      const attacker = other(obj.side);
      const dmg = 1 - Math.max(0, obj.hp) / obj.maxHp;
      b.winner = dmg >= 0.35 ? attacker : obj.side;
      log(b, -1, `Strike complete: ${obj.name} ${Math.round(dmg * 100)}% damaged`, "w");
    }
  } else {
    b.winner = lost[0] < lost[1] * 0.8 ? 0 : lost[1] < lost[0] * 0.8 ? 1 : -1;
    log(b, -1, "Engagement over", "w");
  }
}

/** Run to completion without rendering (AI-vs-AI battles, auto-resolve). */
export function runToEnd(b: BattleState, dt = 1, maxSteps = 40000) {
  let n = 0;
  while (!b.over && n++ < maxSteps) step(b, dt);
  if (!b.over) finish(b);
}
