// Builds a battle from the campaign state: a strategic strike on a base, or an engagement in a theatre.
import { THEATRE_BY_ID } from "../../data/nations";
import { findPart } from "../../data/parts";
import { distKm, statsOf, unitLatLon, unitTheatre } from "../campaign/core";
import type { Base, Campaign, Design, NationId, PendingBattle, Stats, Unit } from "../types";
import { createBattle, KTS, MACH, type BattleState, type BUnit, type BWeapon, type Mission, type Side } from "./engine";
import { isLand, makeProj, seaPoint } from "./geo";
import { rng } from "../campaign/core";

type Pools = Record<NationId, Record<string, number>>;

function satLevel(c: Campaign, n: NationId): number {
  const k = c.nations[n].known;
  return k.includes("sen_leo") ? 2 : k.includes("sen_sat") ? 1 : 0;
}

function weaponsFor(c: Campaign, d: Design, pool: Record<string, number>, mult: number): BWeapon[] {
  const out: BWeapon[] = [];
  for (const e of d.loadout) {
    const md = c.designs[e.designId];
    const s = md ? statsOf(c, md.id) : undefined;
    if (!md || !s) continue;
    const want = Math.round(e.count * mult);
    const n = Math.min(want, pool[e.designId] ?? 0);
    pool[e.designId] = (pool[e.designId] ?? 0) - n;
    if (n > 0) out.push({ designId: md.id, name: md.name, count: n, start: n, s });
  }
  return out;
}

function baseUnit(side: Side, nation: NationId, c: Campaign, b: Base, x: number, y: number, pool: Record<string, number>): BUnit {
  const d = b.designId ? c.designs[b.designId] : undefined;
  const s: Stats = (d && statsOf(c, d.id)) || { cost: 0, devCost: 0, devMonths: 0, buildMonths: 0, upkeep: 0, hp: 600 };
  const hp = (s.hp ?? 600) * (1 - b.damage * 0.8);
  return {
    id: `b-${b.id}`, side, nation, name: b.name, kind: "base", designId: d?.id ?? "", campaignBaseId: b.id,
    x, y, alt: 0, speed: 0, hp, maxHp: s.hp ?? 600, rcs: 5000, radarKm: s.radarKm ?? 0, sensorAlt: 40,
    channels: s.channels ?? 0, ew: 0.05, ciwsKm: s.ciwsKm ?? 0, ciwsPk: s.ciwsPk ?? 0, laserKw: s.laserKw ?? 0,
    weapons: d ? weaponsFor(c, d, pool, 1) : [], alive: true, landed: false, mission: "hold", homeX: x, homeY: y, fuel: 1e9,
    cooldown: 0, emitting: (s.radarKm ?? 0) > 0, datalink: 1, mobility: 0, hardening: s.hardening ?? 0, agility: 0,
    aircraft: 0, parked: [], revealedUntil: 0, asw: false, aew: false,
  };
}

function surfaceUnit(side: Side, c: Campaign, u: Unit, x: number, y: number, pool: Record<string, number>): BUnit | null {
  const d = c.designs[u.designId];
  const s = statsOf(c, u.designId);
  if (!d || !s) return null;
  const kind = d.category === "ship" ? (s.sub ? "sub" : "ship") : "ground";
  const hull = d.category === "ship" ? d.slots.hull : "";
  const ground = kind === "ground";
  const role = d.category === "vehicle" ? findPart("vehicle", "role", d.slots.role)?.a.role : undefined;
  return {
    id: `u-${u.id}`, side, nation: u.nation, name: u.name, kind, designId: d.id, campaignUnitId: u.id,
    x, y, alt: kind === "sub" ? -30 : 0, speed: ground ? 0 : (s.speedKts ?? 20) * KTS * 0.8,
    hp: (s.hp ?? 50) * (1 - u.damage), maxHp: s.hp ?? 50, rcs: s.rcs ?? 10, radarKm: (s.radarKm ?? 0) * (0.6 + 0.4 * u.readiness),
    sensorAlt: s.mastM ?? 20, channels: s.channels ?? 1, ew: s.ew ?? 0, ciwsKm: s.ciwsKm ?? 0, ciwsPk: s.ciwsPk ?? 0, laserKw: s.laserKw ?? 0,
    weapons: weaponsFor(c, d, pool, 1), alive: true, landed: false, mission: kind === "sub" ? "sub" : "hold",
    homeX: x, homeY: y, fuel: 1e9, cooldown: 0, emitting: (s.radarKm ?? 0) > 0 && role !== "tel" && role !== "drone",
    datalink: s.dataLink ?? 0, mobility: s.mobility ?? 0, hardening: 0, agility: 0, aircraft: 0, parked: [],
    revealedUntil: 0, asw: ["cor", "ffg", "ddg", "cg"].includes(hull), aew: false,
  };
}

/** Spawn a squadron's airborne flights. Returns the flights and the aircraft left on the ground. */
function airFlights(side: Side, c: Campaign, u: Unit, x: number, y: number, pool: Record<string, number>, mission: Mission | "auto", share: number, targetId?: string): { flights: BUnit[]; parked: number } {
  const d = c.designs[u.designId];
  const s = statsOf(c, u.designId);
  if (!d || !s) return { flights: [], parked: 0 };
  const sqn = d.params.sqn ?? 12;
  const avail = Math.max(0, Math.round(sqn * (1 - u.damage) * (0.5 + 0.5 * u.readiness)));
  const aew = !!s.aew;
  const uav = d.category === "drone";
  let mis: Mission = "cap";
  const role = d.category === "aircraft" ? d.slots.role : d.slots.type;
  if (mission !== "auto") mis = mission;
  else if (aew) mis = "aew";
  else if (role === "mpa") mis = "mpa";
  else if (role === "st" || role === "ucav") mis = "strike";
  else if (role === "mr") mis = "cap"; // split below between CAP and strike
  else if (role === "recon" || role === "male") mis = "patrol";
  else mis = "cap";
  const flying = aew ? Math.min(avail, 1) : Math.round(avail * share);
  const per = aew || role === "mpa" ? 1 : 2;
  const flights: BUnit[] = [];
  const agility = d.category === "aircraft" ? (findPart("aircraft", "frame", d.slots.frame)?.a.agility as number) ?? 0.5 : 0.3;
  const speed = (s.mach ?? 0.8) * MACH * (s.mach && s.mach > 1.2 ? 0.7 : 0.9);
  for (let i = 0; i < flying; i += per) {
    const n = Math.min(per, flying - i);
    const k = flights.length;
    const offensive = d.loadout.some((e) => { const r = statsOf(c, e.designId)?.role; return !!r && r !== "aam" && r !== "sam" && r !== "bmd"; });
    const fm: Mission = mission === "auto" && role === "mr" && offensive && k % 2 === 1 ? "strike" : mis;
    flights.push({
      id: `a-${u.id}-${k}`, side, nation: u.nation, name: flights.length ? `${u.name} #${k + 1}` : u.name, kind: uav ? "uav" : "air",
      designId: d.id, campaignUnitId: u.id, x: x + (k % 3) * 2, y: y + Math.floor(k / 3) * 2,
      alt: mis === "mpa" ? 2500 : role === "owa" ? 1000 : uav && s.mastM ? s.mastM : 9000, speed,
      hp: n, maxHp: n, rcs: s.rcs ?? 5, radarKm: s.radarKm ?? 0, sensorAlt: mis === "mpa" ? 2500 : 9000,
      channels: s.channels ?? 1, ew: s.ew ?? 0.1, ciwsKm: 0, ciwsPk: 0, laserKw: 0,
      weapons: weaponsFor(c, d, pool, n), alive: true, landed: false, mission: fm,
      homeX: x, homeY: y, fuel: ((s.radiusKm ?? 600) * 2) / Math.max(0.05, speed) + 1800, targetId, cooldown: k * 3,
      emitting: (s.radarKm ?? 0) > 0, datalink: s.dataLink ?? 1, mobility: 0, hardening: 0, agility,
      aircraft: n, parked: [], revealedUntil: 0, asw: mis === "mpa", aew,
    });
  }
  return { flights, parked: Math.max(0, avail - flying) };
}

function stockPools(c: Campaign): Pools {
  const p = {} as Pools;
  for (const n of Object.keys(c.nations) as NationId[]) p[n] = { ...c.nations[n].stock };
  return p;
}

function jitter(rand: () => number, x: number, y: number, r: number): [number, number] {
  const a = rand() * Math.PI * 2;
  const d = Math.sqrt(rand()) * r;
  return [x + Math.cos(a) * d, y + Math.sin(a) * d];
}

const isAir = (c: Campaign, u: Unit) => {
  const d = c.designs[u.designId];
  return !!d && (d.category === "aircraft" || (d.category === "drone" && !!statsOf(c, d.id)?.uav));
};

export function buildBattle(c: Campaign, p: PendingBattle): BattleState {
  return p.kind === "strike" ? buildStrike(c, p) : buildEngagement(c, p);
}

// ---------------------------------------------------------------- strike
function buildStrike(c: Campaign, p: Extract<PendingBattle, { kind: "strike" }>): BattleState {
  const rand = rng(c.seed ^ (c.month * 131 + p.id.length * 7));
  const target = c.bases.find((b) => b.id === p.targetBaseId)!;
  const sources = p.sources.map((id) => c.units.find((u) => u.id === id)).filter((u): u is Unit => !!u);
  const pts = sources.map((u) => unitLatLon(c, u)).filter((x): x is [number, number] => !!x);
  const cLat = (target.lat + pts.reduce((a, q) => a + q[0], 0) / Math.max(1, pts.length)) / 2;
  const cLon = (target.lon + pts.reduce((a, q) => a + q[1], 0) / Math.max(1, pts.length)) / 2;
  const proj = makeProj(cLat, cLon);
  const pools = stockPools(c);
  const units: BUnit[] = [];
  const A: Side = 0, D: Side = 1;

  // defender: target base + everything based there + nearby air defences, fighters and ships
  const [tx, ty] = proj.toXY(target.lat, target.lon);
  const bu = baseUnit(D, p.defender, c, target, tx, ty, pools[p.defender]);
  units.push(bu);
  for (const u of c.units) {
    if (u.nation !== p.defender || u.location.kind === "transit" || u.embarkedOn && !c.units.find((x) => x.id === u.embarkedOn && x.location.kind === "sea")) continue;
    const ll = unitLatLon(c, u);
    if (!ll) continue;
    const dk = distKm(ll[0], ll[1], target.lat, target.lon);
    const d = c.designs[u.designId];
    if (!d) continue;
    const atTarget = u.location.kind === "base" && u.location.baseId === target.id;
    if (isAir(c, u)) {
      const s = statsOf(c, d.id);
      const fighter = d.slots.role === "as" || d.slots.role === "mr" || d.slots.type === "cca";
      if (atTarget) {
        const { flights, parked } = airFlights(D, c, u, tx, ty, pools[p.defender], fighter ? "cap" : "auto", fighter ? 0.5 : s?.aew ? 1 : 0, bu.id);
        units.push(...flights.filter((f) => f.mission === "cap" || f.mission === "aew"));
        bu.parked.push({ campaignUnitId: u.id, aircraft: parked + flights.filter((f) => f.mission !== "cap" && f.mission !== "aew").reduce((a, f) => a + f.aircraft, 0), lost: 0 });
      } else if ((fighter && dk < Math.min(900, s?.radiusKm ?? 600)) || (s?.aew && dk < 1200)) {
        const [x, y] = proj.toXY(ll[0], ll[1]);
        const { flights } = airFlights(D, c, u, x, y, pools[p.defender], s?.aew ? "aew" : "cap", 0.4, bu.id);
        units.push(...flights);
      }
    } else if (d.category === "vehicle" || d.category === "ship") {
      const s = statsOf(c, d.id);
      const defensive = (s?.channels ?? 0) > 0 && d.loadout.some((e) => { const r = statsOf(c, e.designId)?.role; return r === "sam" || r === "bmd"; });
      if (atTarget) {
        const [x, y] = d.category === "vehicle" ? jitter(rand, tx, ty, 12) : [tx + 3, ty + 3];
        const su = surfaceUnit(D, c, u, x, y, pools[p.defender]);
        if (su) units.push(su);
      } else if (defensive && dk < (u.location.kind === "sea" ? 700 : 350)) {
        let lat = ll[0], lon = ll[1];
        if (u.location.kind === "sea") [lat, lon] = seaPoint(lat, lon, rand);
        const [x, y] = proj.toXY(lat, lon);
        const [jx, jy] = jitter(rand, x, y, u.location.kind === "sea" ? 40 : 8);
        const su = surfaceUnit(D, c, u, jx, jy, pools[p.defender]);
        if (su) units.push(su);
      }
    }
  }

  // attacker: the chosen launch sources
  for (const u of sources) {
    const ll = unitLatLon(c, u);
    if (!ll) continue;
    let [lat, lon] = ll;
    const d = c.designs[u.designId];
    if (!d) continue;
    if (d.category === "ship" && u.location.kind === "sea") [lat, lon] = seaPoint(lat, lon, rand);
    const [x, y] = proj.toXY(lat, lon);
    if (isAir(c, u)) {
      const { flights } = airFlights(A, c, u, x, y, pools[p.attacker], "strike", 0.7, bu.id);
      units.push(...flights);
    } else {
      const [jx, jy] = jitter(rand, x, y, d.category === "vehicle" ? 15 : 20);
      const su = surfaceUnit(A, c, u, jx, jy, pools[p.attacker]);
      if (su) { su.mission = "strike"; su.targetId = bu.id; units.push(su); }
    }
  }
  // attacker AEW support if in range
  for (const u of c.units) {
    if (u.nation !== p.attacker || !isAir(c, u) || !statsOf(c, u.designId)?.aew) continue;
    const ll = unitLatLon(c, u);
    if (!ll || distKm(ll[0], ll[1], target.lat, target.lon) > 1500) continue;
    const [x, y] = proj.toXY(ll[0], ll[1]);
    units.push(...airFlights(A, c, u, x, y, pools[p.attacker], "aew", 1).flights);
    break;
  }

  return createBattle({
    id: p.id, kind: "strike", title: p.title, nations: [p.attacker, p.defender],
    playerSide: c.player === p.attacker ? 0 : c.player === p.defender ? 1 : -1,
    lat0: cLat, lon0: cLon, units, sat: [satLevel(c, p.attacker), satLevel(c, p.defender)],
    seed: c.seed + c.month * 17, objectiveId: bu.id, timeLimit: 10 * 3600,
  });
}

// ---------------------------------------------------------------- engagement
function buildEngagement(c: Campaign, p: Extract<PendingBattle, { kind: "engagement" }>): BattleState {
  const rand = rng(c.seed ^ (c.month * 977 + p.id.length));
  const th = THEATRE_BY_ID[p.theatreId];
  const proj = makeProj(th.lat, th.lon);
  const pools = stockPools(c);
  const units: BUnit[] = [];
  const sides: [NationId, NationId] = [p.a, p.b];
  const centers: [number, number][] = [];

  sides.forEach((n, i) => {
    const side = i as Side;
    // fleet axis: shift the fleet 180 km from the theatre centre toward the nation's nearest base
    const home = c.bases.filter((b) => b.nation === n).sort((a, b) => distKm(a.lat, a.lon, th.lat, th.lon) - distKm(b.lat, b.lon, th.lat, th.lon))[0];
    let fx = 0, fy = 0;
    if (home) {
      const [hx, hy] = proj.toXY(home.lat, home.lon);
      const dl = Math.max(1, Math.hypot(hx, hy));
      fx = (hx / dl) * Math.min(110, dl * 0.5);
      fy = (hy / dl) * Math.min(110, dl * 0.5);
    } else { fx = side === 0 ? -110 : 110; }
    // keep the two fleets apart: if both home bases lie the same way, put side B on the far side
    if (i === 1 && Math.hypot(fx - centers[0][0], fy - centers[0][1]) < 200) { fx = -centers[0][0]; fy = -centers[0][1]; }
    const [flat, flon] = seaPoint(...proj.toLatLon(fx, fy), rand);
    const [cx, cy] = proj.toXY(flat, flon);
    centers.push([cx, cy]);

    for (const u of c.units) {
      if (u.nation !== n || u.location.kind === "transit") continue;
      const d = c.designs[u.designId];
      if (!d) continue;
      const atSeaHere = u.location.kind === "sea" && u.location.theatreId === p.theatreId;
      if (atSeaHere && !u.embarkedOn) {
        if (d.category === "ship") {
          let [x, y] = jitter(rand, cx, cy, 25);
          const ll = proj.toLatLon(x, y);
          if (isLand(ll[0], ll[1])) [x, y] = [cx, cy];
          const su = surfaceUnit(side, c, u, x, y, pools[n]);
          if (su) units.push(su);
        }
        continue;
      }
      if (u.embarkedOn) {
        const carrier = c.units.find((x) => x.id === u.embarkedOn);
        if (!carrier || carrier.location.kind !== "sea" || carrier.location.theatreId !== p.theatreId) continue;
        const cu = units.find((x) => x.campaignUnitId === carrier.id);
        if (!cu) continue;
        units.push(...airFlights(side, c, u, cu.x, cu.y, pools[n], "auto", 0.5).flights);
        continue;
      }
      if (u.location.kind !== "base") continue;
      const b = c.bases.find((x) => x.id === (u.location as { baseId: string }).baseId);
      if (!b) continue;
      const dk = distKm(b.lat, b.lon, th.lat, th.lon);
      const [bx, by] = proj.toXY(b.lat, b.lon);
      if (isAir(c, u)) {
        const s = statsOf(c, u.designId);
        if (dk < Math.min(1300, (s?.radiusKm ?? 600) * 0.9)) units.push(...airFlights(side, c, u, bx, by, pools[n], "auto", 0.5).flights);
      } else if (d.category === "vehicle" && dk < 450) {
        const [x, y] = jitter(rand, bx, by, 10);
        const su = surfaceUnit(side, c, u, x, y, pools[n]);
        if (su) units.push(su);
      }
    }
    // bases near the fight can be hit, and their radars help
    for (const b of c.bases) {
      if (b.nation !== n || distKm(b.lat, b.lon, th.lat, th.lon) > 450) continue;
      const [bx, by] = proj.toXY(b.lat, b.lon);
      units.push(baseUnit(side, n, c, b, bx, by, pools[n]));
    }
  });

  return createBattle({
    id: p.id, kind: "engagement", title: p.title, nations: [p.a, p.b],
    playerSide: c.player === p.a ? 0 : c.player === p.b ? 1 : -1,
    lat0: th.lat, lon0: th.lon, units, sat: [satLevel(c, p.a), satLevel(c, p.b)], seed: c.seed + c.month * 31, timeLimit: 10 * 3600,
  });
}

/** Units of `n` that can reach a target base with an offensive weapon, with the weapons and ranges that do. */
export function strikeCandidates(c: Campaign, n: NationId, target: Base): { unit: Unit; weapons: { name: string; count: number; range: number }[]; distKm: number }[] {
  const out: { unit: Unit; weapons: { name: string; count: number; range: number }[]; distKm: number }[] = [];
  for (const u of c.units) {
    if (u.nation !== n || u.location.kind === "transit" || u.damage > 0.7) continue;
    const ll = unitLatLon(c, u);
    const d = c.designs[u.designId];
    if (!ll || !d) continue;
    const dk = distKm(ll[0], ll[1], target.lat, target.lon);
    const s = statsOf(c, d.id);
    const air = isAir(c, u);
    const reach = air ? (s?.radiusKm ?? 0) : 0;
    const ws: { name: string; count: number; range: number }[] = [];
    for (const e of d.loadout) {
      const ms = statsOf(c, e.designId);
      const md = c.designs[e.designId];
      if (!ms || !md) continue;
      const land = ms.role === "lacm" || ms.role === "srbm" || ms.role === "hgv" || ms.role === "arm" || (ms.role === "loiter" && !ms.boat);
      if (!land) continue;
      const have = Math.min(e.count, c.nations[n].stock[e.designId] ?? 0);
      if (have <= 0) continue;
      if ((ms.rangeKm ?? 0) + reach >= dk) ws.push({ name: md.name, count: have, range: (ms.rangeKm ?? 0) + reach });
    }
    if (ws.length) out.push({ unit: u, weapons: ws, distKm: Math.round(dk) });
  }
  return out.sort((a, b) => a.distKm - b.distKm);
}

export { unitTheatre };
