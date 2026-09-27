import { BASE_SEEDS, NATIONS, START_TENSION } from "../../data/nations";
import { BASE_DESIGN_KEYS, STARTS } from "../../data/starts";
import { FIELDS, TECHS } from "../../data/techs";
import { SPECS } from "../../data/parts";
import type { Campaign, Design, FieldId, NationId, NationState, Unit } from "../types";
import { NATION_IDS, START_YEAR } from "../types";
import { dateLabel, pairKey } from "./core";

export const designId = (n: NationId, key: string) => `${n}-${key}`;

function startKnown(n: NationId): string[] {
  const info = NATIONS[n];
  const cutoff = START_YEAR - info.techLag;
  const set = new Set(TECHS.filter((t) => t.year <= cutoff).map((t) => t.id));
  info.extraKnown.forEach((t) => set.add(t));
  info.forbidden.forEach((t) => set.delete(t));
  return [...set];
}

const zeroFields = (v: number) => Object.fromEntries(FIELDS.map((f) => [f.id, v])) as Record<FieldId, number>;

/** Class-appropriate unit names: ships take names from the nation's list, squadrons get numbers. */
export function nextUnitName(c: Campaign, n: NationId, d: Design): string {
  if (d.category === "ship") {
    const used = new Set(c.units.filter((u) => u.nation === n).map((u) => u.name));
    const big = ["cvn", "cvl", "lhd"].includes(d.slots.hull);
    const list = (big && NATIONS[n].names.carrier) || NATIONS[n].names.ship;
    const prefix = n === "USA" ? "USS " : n === "GBR" ? "HMS " : "";
    for (const nm of list) if (!used.has(prefix + nm)) return prefix + nm;
    let i = 2;
    while (used.has(`${prefix}${list[i % list.length]} ${Math.floor(i / list.length) + 1}`)) i++;
    return `${prefix}${list[i % list.length]} ${Math.floor(i / list.length) + 1}`;
  }
  const same = c.units.filter((u) => u.nation === n && u.designId === d.id).length + 1;
  const short = d.name.split(" ")[0];
  if (d.category === "aircraft" || d.category === "drone") return `${short} Sqn ${same}`;
  return `${short} Bty ${same}`;
}

export function newCampaign(player: NationId, seed = Date.now() % 1_000_000): Campaign {
  const c: Campaign = {
    version: 1, seed, player, month: 0,
    nations: {} as Record<NationId, NationState>,
    tension: {}, designs: {}, units: [], bases: [], wars: [], inbox: [], news: [], battles: [],
    firedEvents: [], pending: [], nextId: 1,
  };

  for (const n of NATION_IDS) {
    const smallRd = n === "ISR" || n === "IRN";
    c.nations[n] = {
      id: n, treasury: 0, rdShare: smallRd ? 0.25 : 0.2, prestige: 0, mods: [],
      known: startKnown(n), rp: zeroFields(0), focus: zeroFields(2), target: {},
      stock: {}, orders: [], lastDesignRefresh: {}, forceTargets: {},
    };
    // designs
    for (const s of STARTS[n].designs) {
      const spec = SPECS[s.cat];
      const params: Record<string, number> = {};
      spec.params.forEach((p) => (params[p.id] = s.params[p.id] ?? p.def));
      const d: Design = {
        id: designId(n, s.key), nation: n, category: s.cat, name: s.name, year: START_YEAR,
        slots: { ...s.slots }, params, loadout: (s.load ?? []).map(([k, count]) => ({ designId: designId(n, k), count })),
        status: s.status ?? "service", devMonthsLeft: s.devLeft ?? 0, lineage: `${n}:${s.key}`,
      };
      c.designs[d.id] = d;
    }
    for (const [k, v] of STARTS[n].stock) c.nations[n].stock[designId(n, k)] = v;
  }

  for (const b of BASE_SEEDS) {
    const key = BASE_DESIGN_KEYS[b.nation][b.type];
    c.bases.push({ ...b, designId: key ? designId(b.nation, key) : undefined, damage: 0 });
  }

  for (const n of NATION_IDS) {
    for (const f of STARTS[n].forces) {
      const d = c.designs[designId(n, f.key)];
      if (!d) throw new Error(`Unknown start design ${n}:${f.key}`);
      for (let i = 0; i < f.n; i++) {
        const loc: Unit["location"] = f.at.startsWith("sea:") ? { kind: "sea", theatreId: f.at.slice(4) } : { kind: "base", baseId: f.at };
        const u: Unit = { id: `u${c.nextId++}`, nation: n, designId: d.id, name: nextUnitName(c, n, d), location: loc, damage: 0, readiness: 0.85, count: 1 };
        c.units.push(u);
        for (const [wk, wn] of f.wing ?? []) {
          const wd = c.designs[designId(n, wk)];
          for (let j = 0; j < wn; j++) {
            c.units.push({ id: `u${c.nextId++}`, nation: n, designId: wd.id, name: nextUnitName(c, n, wd), location: loc, damage: 0, readiness: 0.85, count: 1, embarkedOn: u.id });
          }
        }
      }
      const lineage = `${n}:${f.key}`;
      c.nations[n].forceTargets[lineage] = (c.nations[n].forceTargets[lineage] ?? 0) + f.n * (f.wing ? 1 : 1);
      for (const [wk, wn] of f.wing ?? []) {
        const wl = `${n}:${wk}`;
        c.nations[n].forceTargets[wl] = (c.nations[n].forceTargets[wl] ?? 0) + wn * f.n;
      }
    }
  }

  for (let i = 0; i < NATION_IDS.length; i++)
    for (let j = i + 1; j < NATION_IDS.length; j++) c.tension[pairKey(NATION_IDS[i], NATION_IDS[j])] = 15;
  for (const [a, b, v] of START_TENSION) c.tension[pairKey(a, b)] = v;

  const info = NATIONS[player];
  c.inbox.push({
    id: "welcome", month: 0, kind: "world", priority: 5,
    title: `You command the ${info.adjective} naval, air and strategic forces`,
    body: `${dateLabel(0)}. ${info.pitch} Set research priorities, modernise your designs, and keep the budget in the black. Every month you end moves the clock toward 2050.`,
  });
  c.news.push(`${dateLabel(0)} · Campaign begins. The Cold War is ending; nobody knows what comes next.`);
  return c;
}
