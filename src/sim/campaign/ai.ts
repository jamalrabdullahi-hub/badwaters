import { PROGRAMMES } from "../../data/aiTemplates";
import { budgetAt, THEATRE_BY_ID } from "../../data/nations";
import { SIZE_UNITS, SPECS } from "../../data/parts";
import { evaluate, sizeFits } from "../design/stats";
import type { Campaign, Design, FieldId, MunitionRole, NationId, Stats, Unit } from "../types";
import { distKm, evalDesign, knownSet, monthlyIncome, monthlyUpkeep, monthRng, newId, statsOf, yearOf } from "./core";
import { designId } from "./create";

export const AI_FOCUS: Record<NationId, Record<FieldId, number>> = {
  USA: { MIS: 2, SEN: 3, AIR: 3, NAV: 2, UNM: 2, DEF: 2, ADV: 2 },
  RUS: { MIS: 3, SEN: 2, AIR: 2, NAV: 2, UNM: 1, DEF: 3, ADV: 1 },
  CHN: { MIS: 3, SEN: 2, AIR: 2, NAV: 3, UNM: 2, DEF: 2, ADV: 1 },
  FRA: { MIS: 2, SEN: 2, AIR: 3, NAV: 2, UNM: 1, DEF: 2, ADV: 1 },
  GBR: { MIS: 2, SEN: 2, AIR: 2, NAV: 3, UNM: 1, DEF: 2, ADV: 1 },
  ISR: { MIS: 2, SEN: 3, AIR: 2, NAV: 1, UNM: 3, DEF: 4, ADV: 2 },
  IRN: { MIS: 4, SEN: 1, AIR: 1, NAV: 1, UNM: 4, DEF: 2, ADV: 0 },
};

const IDENTITY: Record<string, string[]> = {
  missile: ["role"], aircraft: ["role", "basing"], ship: ["hull"], vehicle: ["chassis", "role"], drone: ["type"], base: ["type"],
};
const FRAME_FAMILY = [["g3", "g4", "g45", "g5", "g6"], ["large"], ["bomber", "lobomber"]];

const cellBased = (d: Design) => d.category === "ship" || d.category === "vehicle" || d.category === "base";

function munitionScore(s: Stats): number {
  const r = s.rangeKm ?? 1, pk = s.pk ?? 0.5;
  switch (s.role) {
    case "sam": return r * pk * (1 + (s.interceptBallistic ?? 0));
    case "bmd": return (s.interceptBallistic ?? 0) * Math.pow(r, 0.3) * pk;
    case "ashm": case "asbm": case "hgv": return Math.sqrt(r) * pk * Math.sqrt(s.mach ?? 1) * (1 + (s.softkillResist ?? 0)) * (1 + (s.maneuver ?? 0));
    default: return Math.sqrt(r) * pk * (1 + (s.softkillResist ?? 0) * 0.5);
  }
}

/** Best in-service munition of a role that fits a platform. */
function bestMunition(c: Campaign, n: NationId, role: MunitionRole, maxSize: string, platform: Design): Design | undefined {
  let best: Design | undefined;
  let bs = -1;
  for (const d of Object.values(c.designs)) {
    if (d.nation !== n || d.status !== "service" || (d.category !== "missile" && d.category !== "drone")) continue;
    const s = evalDesign(c, d).stats;
    if (s.role !== role || !s.size || !sizeFits(s.size, maxSize as never)) continue;
    if (platform.category === "aircraft" && (role === "sam" || role === "bmd")) continue;
    if (role === "torp" && platform.category !== "ship") continue;
    const sc = munitionScore(s);
    if (sc > bs) { bs = sc; best = d; }
  }
  return best;
}

export function autoLoadout(c: Campaign, n: NationId, d: Design, mix: [MunitionRole, number][]) {
  const s = evaluate({ ...d, loadout: [] }, (id) => c.designs[id]).stats;
  const cap = (cellBased(d) ? s.cells : s.hardpoints) ?? 0;
  const out: Design["loadout"] = [];
  for (const [role, share] of mix) {
    const m = bestMunition(c, n, role, s.maxSize ?? "M", d);
    if (!m) continue;
    const ms = evalDesign(c, m).stats;
    const per = cellBased(d) ? (ms.size === "S" ? 0.25 : 1) : SIZE_UNITS[ms.size ?? "M"];
    const count = Math.floor((cap * share) / per);
    if (count > 0) out.push({ designId: m.id, count });
  }
  d.loadout = out;
}

function hasErrors(c: Campaign, d: Design, known: Set<string>) {
  return evaluate(d, (id) => c.designs[id], known).issues.some((i) => i.sev === "error");
}

/** Greedy modernisation: best known valid part per slot, newest munitions in the loadout. */
export function upgradeDesign(c: Campaign, n: NationId, d: Design): Design | null {
  const known = knownSet(c, n);
  const nd: Design = JSON.parse(JSON.stringify(d));
  for (const slot of SPECS[d.category].slots) {
    if (IDENTITY[d.category].includes(slot.id)) continue;
    let parts = slot.parts;
    if (d.category === "aircraft" && slot.id === "frame") parts = parts.filter((p) => FRAME_FAMILY.find((f) => f.includes(d.slots.frame))?.includes(p.id));
    const prev = nd.slots[slot.id];
    for (const part of [...parts].reverse()) {
      if (part.tech && !known.has(part.tech)) continue;
      if (part.id === "none" && prev !== "none") continue;
      nd.slots[slot.id] = part.id;
      if (!hasErrors(c, nd, known)) break;
      nd.slots[slot.id] = prev;
    }
  }
  // newest munitions of the same roles
  if (SPECS[d.category].hasLoadout && nd.loadout.length) {
    const before = nd.loadout;
    const maxSize = evaluate({ ...nd, loadout: [] }, (id) => c.designs[id]).stats.maxSize ?? "M";
    nd.loadout = nd.loadout.map((e) => {
      const role = statsOf(c, e.designId)?.role;
      const m = role ? bestMunition(c, n, role, maxSize, nd) : undefined;
      return m ? { designId: m.id, count: e.count } : e;
    });
    const merged = new Map<string, number>();
    nd.loadout.forEach((e) => merged.set(e.designId, (merged.get(e.designId) ?? 0) + e.count));
    nd.loadout = [...merged].map(([designId, count]) => ({ designId, count }));
    if (hasErrors(c, nd, known)) nd.loadout = before;
  }
  if (hasErrors(c, nd, known)) return null;
  const same = JSON.stringify(nd.slots) === JSON.stringify(d.slots) && JSON.stringify(nd.loadout) === JSON.stringify(d.loadout);
  return same ? null : nd;
}

export function latestOfLineage(c: Campaign, lineage: string, status?: "service"): Design | undefined {
  let best: Design | undefined;
  for (const d of Object.values(c.designs)) {
    if (d.lineage !== lineage) continue;
    if (status && d.status !== status) continue;
    if (d.status === "draft" || d.status === "obsolete") continue;
    if (!best || d.year > best.year || (d.year === best.year && d.id > best.id)) best = d;
  }
  return best;
}

function successorName(d: Design, year: number) {
  return `${d.name.replace(/ \(\d{4}\)$/, "")} (${year})`;
}

function launchProgrammes(c: Campaign, n: NationId) {
  const year = yearOf(c.month);
  const ns = c.nations[n];
  for (const p of PROGRAMMES) {
    if (!p.nations.includes(n) || year < p.minYear || !p.techs.every((t) => ns.known.includes(t))) continue;
    const firstKey = p.designs[0].key;
    if (c.designs[designId(n, `${p.id}-${firstKey}`)]) continue;
    const known = knownSet(c, n);
    for (const seed of p.designs) {
      const spec = SPECS[seed.cat];
      const ov = p.overrides?.[n]?.[seed.key];
      const params: Record<string, number> = {};
      spec.params.forEach((pp) => (params[pp.id] = ov?.params?.[pp.id] ?? seed.params[pp.id] ?? pp.def));
      const d: Design = {
        id: designId(n, `${p.id}-${seed.key}`), nation: n, category: seed.cat, name: p.names[n]?.[seed.key] ?? seed.name,
        year, slots: { ...seed.slots, ...(ov?.slots ?? {}) }, params,
        loadout: (seed.load ?? []).map(([k, count]) => ({ designId: designId(n, `${p.id}-${k}`), count })),
        status: "developing", devMonthsLeft: 0, lineage: `${n}:${p.id}-${seed.key}`,
      };
      if (p.autoLoad?.[seed.key]) autoLoadout(c, n, d, p.autoLoad[seed.key]);
      // bring the fresh design up to today's technology
      const up = upgradeDesign(c, n, d);
      const final = up ?? d;
      final.devMonthsLeft = evalDesign(c, final).stats.devMonths;
      if (hasErrors(c, final, known) && !up) {
        // keep going: dev designs referencing sibling munitions still in development are fine
      }
      c.designs[final.id] = final;
    }
    for (const [k, v] of p.targets[n] ?? []) ns.forceTargets[`${n}:${p.id}-${k}`] = v;
  }
}

/** Latest non-draft design of every lineage for a nation, in one pass. */
function latestByLineage(c: Campaign, n: NationId): Map<string, Design> {
  const m = new Map<string, Design>();
  for (const d of Object.values(c.designs)) {
    if (d.nation !== n || d.status === "draft" || d.status === "obsolete") continue;
    const b = m.get(d.lineage);
    if (!b || d.year > b.year || (d.year === b.year && d.id > b.id)) m.set(d.lineage, d);
  }
  return m;
}

function modernise(c: Campaign, n: NationId) {
  const year = yearOf(c.month);
  const latest = latestByLineage(c, n);
  const ordered = [...latest.keys()].sort((a, b) => (latest.get(a)!.category === "missile" ? 0 : 1) - (latest.get(b)!.category === "missile" ? 0 : 1));
  const r = monthRng(c, n.charCodeAt(0));
  for (const L of ordered) {
    const cur = latest.get(L);
    if (!cur || cur.status !== "service" || cur.category === "base") continue;
    if (year - cur.year < 5 || r() > 0.08) continue;
    const up = upgradeDesign(c, n, cur);
    if (!up) continue;
    up.id = newId(c, `${n}-d`);
    up.year = year;
    up.name = successorName(cur, year);
    up.status = "developing";
    up.devMonthsLeft = Math.round(evalDesign(c, up).stats.devMonths * 0.6);
    c.designs[up.id] = up;
  }
}

function lineageOf(c: Campaign, u: Unit) {
  return c.designs[u.designId]?.lineage ?? "";
}

function homeBaseFor(c: Campaign, n: NationId, d: Design) {
  const want = d.category === "ship" ? "naval" : d.category === "aircraft" || d.category === "drone" ? "air" : "missile";
  const bases = c.bases.filter((b) => b.nation === n);
  const load = (id: string) => c.units.filter((u) => u.location.kind === "base" && u.location.baseId === id).length;
  const pool = bases.filter((b) => b.type === want);
  const list = pool.length ? pool : bases;
  return list.sort((a, b) => load(a.id) - load(b.id))[0];
}

function produce(c: Campaign, n: NationId) {
  const ns = c.nations[n];
  const income = monthlyIncome(c, n);
  if (ns.treasury < -income * 2) return;
  // force size follows the budget; a nation sitting on cash grows beyond its historical targets
  const cashBoost = Math.max(1, Math.min(1.8, 1 + ns.treasury / Math.max(1, income * 24)));
  const scale = Math.max(0.35, Math.min(6, Math.pow(budgetAt(n, yearOf(c.month)) / budgetAt(n, 1990), 0.8) * cashBoost));
  const maxOrders = Math.max(1, Math.round(income * 1.5 * cashBoost));
  let platformOrders = ns.orders.filter((o) => c.designs[o.designId]?.category !== "missile" && statsOf(c, o.designId)?.role === undefined).length;

  const counts = new Map<string, number>();
  for (const u of c.units) if (u.nation === n) counts.set(lineageOf(c, u), (counts.get(lineageOf(c, u)) ?? 0) + 1);
  for (const o of ns.orders) {
    const L = c.designs[o.designId]?.lineage ?? "";
    counts.set(L, (counts.get(L) ?? 0) + o.qty);
  }
  const entries = Object.entries(ns.forceTargets).sort(() => monthRng(c, 3)() - 0.5);
  for (const [L, base] of entries) {
    if (platformOrders >= maxOrders) break;
    const target = Math.round(base * scale);
    if ((counts.get(L) ?? 0) >= target) continue;
    const d = latestOfLineage(c, L, "service");
    if (!d || d.category === "missile") continue;
    const s = evalDesign(c, d).stats;
    if (s.cost / 1000 > income * 8) continue; // too expensive to start now
    const home = homeBaseFor(c, n, d);
    if (!home) continue;
    ns.orders.push({ id: newId(c, "o"), designId: d.id, qty: 1, monthsPerUnit: s.buildMonths, progress: 0, destBaseId: home.id });
    platformOrders++;
  }

  // munitions: keep 1.3× what the force would load, within a spending cap
  const need = new Map<string, number>();
  for (const u of c.units) {
    if (u.nation !== n) continue;
    const d = c.designs[u.designId];
    if (!d) continue;
    for (const e of d.loadout) need.set(e.designId, (need.get(e.designId) ?? 0) + e.count);
  }
  let budget = income * 1000 * 0.25; // $M this month for new munition orders
  for (const [id, k] of need) {
    const md = c.designs[id];
    if (!md || md.status !== "service") continue;
    const have = (ns.stock[id] ?? 0) + ns.orders.filter((o) => o.designId === id).reduce((a, o) => a + o.qty, 0);
    const deficit = Math.ceil(k * 1.3) - have;
    if (deficit <= 0) continue;
    const s = evalDesign(c, md).stats;
    const qty = Math.min(deficit, Math.floor(budget / Math.max(0.01, s.cost)));
    if (qty <= 0) continue;
    budget -= qty * s.cost;
    ns.orders.push({ id: newId(c, "o"), designId: id, qty, monthsPerUnit: s.buildMonths, progress: 0, destBaseId: "" });
  }
}

function retire(c: Campaign, n: NationId) {
  const ns = c.nations[n];
  const income = monthlyIncome(c, n);
  const upkeep = monthlyUpkeep(c, n);
  const over = upkeep > income * (1 - ns.rdShare) * 0.8 || ns.treasury < -income * 3;
  if (!over) return;
  const candidates = c.units
    .filter((u) => u.nation === n && !u.embarkedOn && u.location.kind === "base")
    .sort((a, b) => (c.designs[a.designId]?.year ?? 0) - (c.designs[b.designId]?.year ?? 0) || (statsOf(c, b.designId)?.upkeep ?? 0) - (statsOf(c, a.designId)?.upkeep ?? 0));
  const cut = new Set(candidates.slice(0, 3).map((u) => u.id));
  for (const u of c.units) if (u.embarkedOn && cut.has(u.embarkedOn)) u.embarkedOn = undefined;
  c.units = c.units.filter((u) => !cut.has(u.id));
}

/** Put new carrier-capable squadrons aboard carriers that have room. */
function embark(c: Campaign, n: NationId) {
  const carriers = c.units.filter((u) => u.nation === n && (statsOf(c, u.designId)?.airWing ?? 0) > 0);
  for (const cv of carriers) {
    const cs = statsOf(c, cv.designId)!;
    const aboard = c.units.filter((u) => u.embarkedOn === cv.id);
    let room = (cs.airWing ?? 0) - aboard.reduce((a, u) => a + (c.designs[u.designId]?.params.sqn ?? 12), 0);
    if (room <= 0) continue;
    for (const u of c.units) {
      if (u.nation !== n || u.embarkedOn || u.location.kind !== "base") continue;
      const s = statsOf(c, u.designId);
      if (!s || !s.carrier || s.carrier === "none") continue;
      if (cs.carrier === "stovl" && s.carrier === "catobar") continue;
      const size = c.designs[u.designId]?.params.sqn ?? 12;
      if (size > room) continue;
      u.embarkedOn = cv.id;
      u.location = cv.location;
      room -= size;
    }
  }
}

export function transitMonths(fromLat: number, fromLon: number, toLat: number, toLon: number) {
  const d = distKm(fromLat, fromLon, toLat, toLon);
  return d < 1500 ? 0 : Math.max(1, Math.round(d / 9000));
}

function deploy(c: Campaign, n: NationId) {
  const r = monthRng(c, 11 + n.charCodeAt(1));
  const homeTheatres = [...new Set(c.bases.filter((b) => b.nation === n).map((b) => b.theatre))];
  const forward = n === "USA" ? ["ARAB", "MED", "WPAC"] : n === "CHN" && yearOf(c.month) > 2012 ? ["SCS", "WPAC"] : [];
  for (const u of c.units) {
    if (u.nation !== n || u.embarkedOn) continue;
    const d = c.designs[u.designId];
    if (d?.category !== "ship") continue;
    if (u.location.kind === "sea" && (u.readiness < 0.5 || r() < 0.12)) {
      const th = THEATRE_BY_ID[u.location.theatreId];
      const home = c.bases.filter((b) => b.nation === n && b.type === "naval").sort((a, b) => distKm(a.lat, a.lon, th.lat, th.lon) - distKm(b.lat, b.lon, th.lat, th.lon))[0];
      if (home) {
        const m = transitMonths(th.lat, th.lon, home.lat, home.lon);
        u.location = m ? { kind: "transit", to: { kind: "base", baseId: home.id }, monthsLeft: m } : { kind: "base", baseId: home.id };
      }
    } else if (u.location.kind === "base" && u.readiness > 0.8 && u.damage < 0.2 && r() < 0.12) {
      const base = c.bases.find((b) => b.id === (u.location as { baseId: string }).baseId);
      if (!base) continue;
      const pool = forward.length && r() < 0.5 ? forward : homeTheatres;
      const tid = pool[Math.floor(r() * pool.length)];
      const th = THEATRE_BY_ID[tid];
      const m = transitMonths(base.lat, base.lon, th.lat, th.lon);
      u.location = m ? { kind: "transit", to: { kind: "sea", theatreId: tid }, monthsLeft: m } : { kind: "sea", theatreId: tid };
    }
  }
}

export function aiMonth(c: Campaign, n: NationId) {
  c.nations[n].focus = { ...AI_FOCUS[n] };
  launchProgrammes(c, n);
  modernise(c, n);
  retire(c, n);
  produce(c, n);
  embark(c, n);
  deploy(c, n);
}

/** Remove superseded designs nobody uses: no units, orders, stock, or loadout references. */
export function pruneDesigns(c: Campaign) {
  const used = new Set<string>();
  for (const u of c.units) used.add(u.designId);
  for (const b of c.bases) if (b.designId) used.add(b.designId);
  for (const n of Object.values(c.nations)) {
    for (const o of n.orders) used.add(o.designId);
    for (const [id, k] of Object.entries(n.stock)) if (k > 0) used.add(id);
  }
  const latest = new Set<string>();
  for (const n of Object.keys(c.nations) as NationId[]) for (const d of latestByLineage(c, n).values()) latest.add(d.id);
  for (const d of Object.values(c.designs)) if (used.has(d.id) || latest.has(d.id) || d.status !== "service") for (const e of d.loadout) used.add(e.designId);
  for (const d of Object.values(c.designs)) {
    if (d.nation === c.player) continue; // never delete the player's designs
    if (d.status === "service" && !used.has(d.id) && !latest.has(d.id)) delete c.designs[d.id];
  }
}

/** The player's nation also gets new AI programmes offered as drafts, so the player sees what's possible. */
export function offerProgrammes(c: Campaign) {
  const n = c.player;
  const before = new Set(Object.keys(c.designs));
  launchProgrammes(c, n);
  const added = Object.keys(c.designs).filter((id) => !before.has(id));
  for (const id of added) c.designs[id] = { ...c.designs[id], status: "draft" };
  if (added.length) {
    c.inbox.push({
      id: `prog-${c.month}`, month: c.month, kind: "tech", priority: 35,
      title: `New design proposals: ${added.map((id) => c.designs[id].name).join(", ")}`,
      body: "Industry has drafted new designs from your latest research. Review them in the Design Bureau and start development when you're ready.",
    });
  }
  c.nations[n].forceTargets = {};
}
