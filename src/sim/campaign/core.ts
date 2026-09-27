import { budgetAt, NATIONS, THEATRE_BY_ID } from "../../data/nations";
import { evaluate, type DesignEval } from "../design/stats";
import type { Base, Campaign, Design, NationId, Unit } from "../types";
import { START_YEAR } from "../types";

export const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
export const END_MONTH = (2050 - START_YEAR) * 12 + 11;

export const yearOf = (month: number) => START_YEAR + Math.floor(month / 12);
export const dateLabel = (month: number) => `${MONTHS[month % 12]} ${yearOf(month)}`;
export const pairKey = (a: NationId, b: NationId) => (a < b ? `${a}|${b}` : `${b}|${a}`);

export function tension(c: Campaign, a: NationId, b: NationId): number {
  if (a === b) return 0;
  return c.tension[pairKey(a, b)] ?? 15;
}
export function addTension(c: Campaign, a: NationId, b: NationId, d: number) {
  if (a === b) return;
  const k = pairKey(a, b);
  c.tension[k] = Math.max(0, Math.min(100, (c.tension[k] ?? 15) + d));
}

export function eraName(year: number): string {
  if (year < 2001) return "Unipolar Moment";
  if (year < 2014) return "Long War";
  if (year < 2030) return "Great-Power Return";
  return "Autonomy & Hypersonics";
}

/** Mulberry32: small deterministic RNG. */
export function rng(seed: number) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
export const monthRng = (c: Campaign, salt = 0) => rng((c.seed * 2654435761) ^ (c.month * 40503 + salt * 977));

export function newId(c: Campaign, prefix: string): string {
  c.nextId += 1;
  return `${prefix}${c.nextId.toString(36)}`;
}

// ---------- design evaluation cache
// Campaigns are cloned every month, so identity alone would miss; fall back to a content signature per id.
const evalCache = new WeakMap<Design, DesignEval>();
const sigCache = new Map<string, { sig: string; e: DesignEval }>();
export function evalDesign(c: Campaign, d: Design): DesignEval {
  let e = evalCache.get(d);
  if (e) return e;
  const sig = JSON.stringify([d.slots, d.params, d.loadout]);
  const hit = sigCache.get(d.id);
  if (hit && hit.sig === sig) e = hit.e;
  else {
    e = evaluate(d, (id) => c.designs[id]);
    sigCache.set(d.id, { sig, e });
  }
  evalCache.set(d, e);
  return e;
}
export const statsOf = (c: Campaign, designId: string) => {
  const d = c.designs[designId];
  return d ? evalDesign(c, d).stats : undefined;
};

export function knownSet(c: Campaign, n: NationId): Set<string> {
  return new Set(c.nations[n].known);
}

/** Current budget modifier product from timed modifiers. */
export function budgetMod(c: Campaign, n: NationId): number {
  return c.nations[n].mods.filter((m) => m.until >= c.month).reduce((p, m) => p * m.mult, 1);
}
export function monthlyIncome(c: Campaign, n: NationId): number {
  return (budgetAt(n, yearOf(c.month) + (c.month % 12) / 12) * budgetMod(c, n)) / 12;
}
export function monthlyUpkeep(c: Campaign, n: NationId): number {
  let m = 0;
  for (const u of c.units) {
    if (u.nation !== n) continue;
    const s = statsOf(c, u.designId);
    if (s) m += s.upkeep;
  }
  for (const b of c.bases) {
    if (b.nation !== n || !b.designId) continue;
    const s = statsOf(c, b.designId);
    if (s) m += s.upkeep;
  }
  return m / 1000; // $B
}

export function unitsOf(c: Campaign, n: NationId): Unit[] {
  return c.units.filter((u) => u.nation === n);
}
export function basesOf(c: Campaign, n: NationId): Base[] {
  return c.bases.filter((b) => b.nation === n);
}

export function unitLatLon(c: Campaign, u: Unit): [number, number] | null {
  const loc = u.location;
  if (loc.kind === "base") {
    const b = c.bases.find((x) => x.id === loc.baseId);
    return b ? [b.lat, b.lon] : null;
  }
  if (loc.kind === "sea") {
    const t = THEATRE_BY_ID[loc.theatreId];
    return t ? [t.lat, t.lon] : null;
  }
  return null;
}

export function unitTheatre(c: Campaign, u: Unit): string | null {
  const loc = u.location;
  if (loc.kind === "sea") return loc.theatreId;
  if (loc.kind === "base") return c.bases.find((b) => b.id === loc.baseId)?.theatre ?? null;
  return null;
}

/** Great-circle distance in km. */
export function distKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371;
  const toR = Math.PI / 180;
  const dLat = (lat2 - lat1) * toR;
  const dLon = (lon2 - lon1) * toR;
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(lat1 * toR) * Math.cos(lat2 * toR) * Math.sin(dLon / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(a));
}

export function atWar(c: Campaign, a: NationId, b: NationId): boolean {
  return c.wars.some((w) => (w.a === a && w.b === b) || (w.a === b && w.b === a));
}
export function enemiesOf(c: Campaign, n: NationId): NationId[] {
  return c.wars.filter((w) => w.a === n || w.b === n).map((w) => (w.a === n ? w.b : w.a));
}

export function nationName(n: NationId) {
  return NATIONS[n].name;
}

export function pushNews(c: Campaign, text: string) {
  c.news.unshift(`${dateLabel(c.month)} · ${text}`);
  if (c.news.length > 200) c.news.length = 200;
}

/** Military value used for AI decisions and scoring: sum of unit costs in $B. */
export function forceValue(c: Campaign, n: NationId): number {
  let v = 0;
  for (const u of c.units) if (u.nation === n) v += (statsOf(c, u.designId)?.cost ?? 0) * (1 - u.damage);
  return v / 1000;
}
