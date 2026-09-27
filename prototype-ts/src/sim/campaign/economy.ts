import type { Campaign, NationId } from "../types";
import { dateLabel, evalDesign, monthlyIncome, monthlyUpkeep, newId, statsOf } from "./core";
import { nextUnitName } from "./create";

export interface EconReport {
  income: number;
  upkeep: number;
  rd: number;
  dev: number;
  prod: number;
  funded: number; // 0..1 share of project demand that was paid
  completed: string[];
  designsReady: string[];
}

/** Money demanded this month by development projects and production orders ($B). */
export function projectDemand(c: Campaign, n: NationId) {
  let dev = 0, prod = 0;
  for (const d of Object.values(c.designs)) {
    if (d.nation !== n || d.status !== "developing") continue;
    const s = evalDesign(c, d).stats;
    dev += s.devCost / Math.max(1, s.devMonths) / 1000;
  }
  for (const o of c.nations[n].orders) {
    const s = statsOf(c, o.designId);
    if (!s || o.qty <= 0) continue;
    prod += (s.cost / Math.max(0.001, o.monthsPerUnit)) / 1000 * Math.min(1, o.qty * o.monthsPerUnit);
  }
  return { dev, prod };
}

export function processEconomy(c: Campaign, n: NationId): EconReport {
  const ns = c.nations[n];
  const income = monthlyIncome(c, n);
  const upkeep = monthlyUpkeep(c, n);
  const rd = income * ns.rdShare;
  const { dev, prod } = projectDemand(c, n);
  const free = income - upkeep - rd + Math.max(0, ns.treasury) * 0.5;
  const demand = dev + prod;
  const funded = demand <= 0 ? 1 : Math.max(0, Math.min(1, free / demand));
  const spent = demand * funded;
  ns.treasury += income - upkeep - rd - spent;
  if (ns.treasury < -income * 3) ns.prestige -= 1;

  const report: EconReport = { income, upkeep, rd, dev, prod, funded, completed: [], designsReady: [] };

  // development
  for (const d of Object.values(c.designs)) {
    if (d.nation !== n || d.status !== "developing") continue;
    d.devMonthsLeft -= funded;
    if (d.devMonthsLeft <= 0) {
      const nd = { ...d, status: "service" as const, devMonthsLeft: 0 };
      c.designs[d.id] = nd;
      report.designsReady.push(d.name);
    }
  }

  // production
  for (const o of ns.orders) {
    const d = c.designs[o.designId];
    if (!d || o.qty <= 0) continue;
    o.progress += funded;
    while (o.qty > 0 && o.progress >= o.monthsPerUnit) {
      o.progress -= o.monthsPerUnit;
      o.qty--;
      if (d.category === "missile" || (d.category === "drone" && evalDesign(c, d).stats.role === "loiter")) {
        ns.stock[d.id] = (ns.stock[d.id] ?? 0) + 1;
      } else if (d.category === "base") {
        const base = c.bases.find((b) => b.id === o.destBaseId);
        if (base && base.nation === n) { base.designId = d.id; base.damage = 0; report.completed.push(`${base.name} rebuilt to ${d.name}`); }
      } else {
        const base = c.bases.find((b) => b.id === o.destBaseId && b.nation === n) ?? c.bases.find((b) => b.nation === n);
        if (!base) continue;
        c.units.push({
          id: newId(c, "u"), nation: n, designId: d.id, name: nextUnitName(c, n, d),
          location: { kind: "base", baseId: base.id }, damage: 0, readiness: 0.6, count: 1,
        });
        report.completed.push(`${d.name} delivered to ${base.name}`);
      }
    }
  }
  ns.orders = ns.orders.filter((o) => o.qty > 0);
  return report;
}

/** Readiness, repairs and transit for one nation's units. */
export function processUnits(c: Campaign, n: NationId) {
  const upkeepShort = monthlyUpkeep(c, n) > monthlyIncome(c, n) * (1 - c.nations[n].rdShare) && c.nations[n].treasury < 0;
  for (const u of c.units) {
    if (u.nation !== n) continue;
    if (u.embarkedOn) {
      const carrier = c.units.find((x) => x.id === u.embarkedOn);
      if (carrier) u.location = carrier.location;
    }
    const loc = u.location;
    if (loc.kind === "transit") {
      loc.monthsLeft -= 1;
      if (loc.monthsLeft <= 0) u.location = loc.to;
      u.readiness = Math.max(0.3, u.readiness - 0.02);
    } else if (loc.kind === "sea") {
      u.readiness = Math.max(0.3, u.readiness - 0.015);
    } else {
      u.readiness = Math.min(1, u.readiness + (upkeepShort ? -0.03 : 0.06));
      const base = c.bases.find((b) => b.id === loc.baseId);
      const d = c.designs[u.designId];
      const goodYard = d?.category === "ship" ? base?.type === "naval" : true;
      if (u.damage > 0) u.damage = Math.max(0, u.damage - (goodYard ? 0.12 : 0.04));
    }
  }
  for (const b of c.bases) if (b.nation === n && b.damage > 0) b.damage = Math.max(0, b.damage - 0.08);
}

export function monthStamp(c: Campaign) {
  return dateLabel(c.month);
}
