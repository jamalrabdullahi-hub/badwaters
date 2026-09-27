import { NATIONS } from "../../data/nations";
import { FIELDS, TECHS, TECH_BY_ID, techCost } from "../../data/techs";
import type { Campaign, FieldId, NationId, Tech } from "../types";
import { NATION_IDS } from "../types";
import { monthlyIncome, yearOf } from "./core";

export function rdBudget(c: Campaign, n: NationId): number {
  return monthlyIncome(c, n) * c.nations[n].rdShare; // $B / month
}

/** Research points per month: diminishing returns on money, scaled by national efficiency. */
export function rpPerMonth(c: Campaign, n: NationId): number {
  return 30 * Math.sqrt(Math.max(0, rdBudget(c, n))) * NATIONS[n].rdEff;
}

export function isAvailable(c: Campaign, n: NationId, t: Tech): boolean {
  const k = c.nations[n].known;
  return !k.includes(t.id) && t.requires.every((r) => k.includes(r));
}

/** Cost at the current date, cheaper when other nations already have it (diffusion and espionage). */
export function effectiveCost(c: Campaign, n: NationId, t: Tech): number {
  const others = NATION_IDS.filter((o) => o !== n && c.nations[o].known.includes(t.id)).length;
  return Math.round(techCost(t, yearOf(c.month)) * Math.max(0.6, 1 - 0.08 * others));
}

export function fieldTarget(c: Campaign, n: NationId, f: FieldId): Tech | undefined {
  const pinned = c.nations[n].target[f];
  if (pinned) {
    const t = TECH_BY_ID[pinned];
    if (t && isAvailable(c, n, t)) return t;
  }
  let best: Tech | undefined;
  let bc = Infinity;
  for (const t of TECHS) {
    if (t.field !== f || !isAvailable(c, n, t)) continue;
    const cost = effectiveCost(c, n, t);
    if (cost < bc) { bc = cost; best = t; }
  }
  return best;
}

/** Monthly research. Returns techs learned this month. */
export function doResearch(c: Campaign, n: NationId): Tech[] {
  const ns = c.nations[n];
  const rp = rpPerMonth(c, n);
  const totalW = FIELDS.reduce((a, f) => a + ns.focus[f.id], 0) || 1;
  const learned: Tech[] = [];
  for (const f of FIELDS) {
    ns.rp[f.id] += (rp * ns.focus[f.id]) / totalW;
    const t = fieldTarget(c, n, f.id);
    if (!t) { ns.rp[f.id] = Math.min(ns.rp[f.id], 400); continue; }
    const cost = effectiveCost(c, n, t);
    if (ns.rp[f.id] >= cost) {
      ns.rp[f.id] -= cost;
      ns.known.push(t.id);
      if (ns.target[f.id] === t.id) delete ns.target[f.id];
      learned.push(t);
    }
  }
  return learned;
}
