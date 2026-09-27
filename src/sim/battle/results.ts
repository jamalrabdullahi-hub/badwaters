import { NATIONS } from "../../data/nations";
import { atWar, dateLabel, pushNews, statsOf } from "../campaign/core";
import type { Campaign, NationId } from "../types";
import type { BattleState } from "./engine";

export interface BattleOutcome {
  lossesB: [number, number]; // $B destroyed per side
  unitsLost: [string[], string[]];
  expended: [Record<string, number>, Record<string, number>];
  summary: string;
}

/** Apply a finished battle to the campaign: losses, damage, munitions, war score, prestige. */
export function applyBattle(c: Campaign, b: BattleState): BattleOutcome {
  const lossesB: [number, number] = [0, 0];
  const unitsLost: [string[], string[]] = [[], []];
  const expended: [Record<string, number>, Record<string, number>] = [{}, {}];
  const aircraftLost = new Map<string, number>();

  for (const u of b.units) {
    const side = u.side;
    for (const w of u.weapons) {
      const used = w.start - w.count;
      if (used > 0) expended[side][w.designId] = (expended[side][w.designId] ?? 0) + used;
    }
    if (u.kind === "base") {
      const base = c.bases.find((x) => x.id === u.campaignBaseId);
      if (base) base.damage = Math.min(1, Math.max(base.damage, 1 - Math.max(0, u.hp) / u.maxHp));
      for (const p of u.parked) if (p.lost > 0) aircraftLost.set(p.campaignUnitId, (aircraftLost.get(p.campaignUnitId) ?? 0) + p.lost);
      continue;
    }
    if (!u.campaignUnitId) continue;
    if (u.kind === "air" || u.kind === "uav") {
      const lost = u.maxHp - Math.max(0, u.alive ? u.aircraft : 0);
      if (lost > 0) aircraftLost.set(u.campaignUnitId, (aircraftLost.get(u.campaignUnitId) ?? 0) + lost);
      continue;
    }
    const cu = c.units.find((x) => x.id === u.campaignUnitId);
    if (!cu) continue;
    const cost = (statsOf(c, cu.designId)?.cost ?? 0) / 1000;
    const newDmg = u.alive ? Math.min(0.95, 1 - Math.max(0, u.hp) / u.maxHp) : 1;
    lossesB[side] += cost * Math.max(0, newDmg - cu.damage);
    cu.damage = newDmg;
    if (!u.alive) unitsLost[side].push(cu.name);
  }

  for (const [id, lost] of aircraftLost) {
    const cu = c.units.find((x) => x.id === id);
    if (!cu) continue;
    const d = c.designs[cu.designId];
    const sqn = d?.params.sqn ?? 12;
    const side = b.nations[0] === cu.nation ? 0 : 1;
    const cost = (statsOf(c, cu.designId)?.cost ?? 0) / 1000;
    const frac = Math.min(1 - cu.damage, lost / sqn);
    lossesB[side] += cost * frac;
    cu.damage = Math.min(1, cu.damage + frac);
    if (cu.damage >= 0.999) unitsLost[side].push(cu.name);
  }

  // remove destroyed units; squadrons embarked on a sunk carrier go down with it
  const dead = new Set(c.units.filter((u) => u.damage >= 0.999).map((u) => u.id));
  for (const u of c.units) if (u.embarkedOn && dead.has(u.embarkedOn)) dead.add(u.id);
  c.units = c.units.filter((u) => !dead.has(u.id));

  b.nations.forEach((n, i) => {
    const st = c.nations[n].stock;
    for (const [id, k] of Object.entries(expended[i])) st[id] = Math.max(0, (st[id] ?? 0) - k);
  });

  const [n0, n1] = b.nations;
  const war = c.wars.find((w) => (w.a === n0 && w.b === n1) || (w.a === n1 && w.b === n0));
  if (war) {
    const delta = lossesB[1] - lossesB[0];
    war.score += war.a === n0 ? delta : -delta;
  }
  const win = b.winner;
  if (win !== -1) {
    c.nations[b.nations[win]].prestige += 3;
    c.nations[b.nations[win === 0 ? 1 : 0]].prestige -= 3;
  }
  const lostTxt = (i: 0 | 1) => (unitsLost[i].length ? unitsLost[i].slice(0, 6).join(", ") + (unitsLost[i].length > 6 ? ` and ${unitsLost[i].length - 6} more` : "") : "nothing");
  const summary = `${NATIONS[n0].adjective} losses: $${lossesB[0].toFixed(2)}B (${lostTxt(0)}). ${NATIONS[n1].adjective} losses: $${lossesB[1].toFixed(2)}B (${lostTxt(1)}). ` +
    `Missiles launched ${b.counts.launched[0]} / ${b.counts.launched[1]}, intercepts ${b.counts.intercepts[0]} / ${b.counts.intercepts[1]}.`;
  const winner = win === -1 ? null : b.nations[win];
  c.battles.unshift({ id: b.id, month: c.month, title: b.title, summary, winner });
  pushNews(c, `${b.title}: ${winner ? `${NATIONS[winner].adjective} success` : "inconclusive"}.`);
  if (b.nations.includes(c.player)) {
    c.inbox.push({
      id: `rep-${b.id}`, month: c.month, kind: "battle", priority: 60,
      title: `After-action report: ${b.title}`,
      body: `${dateLabel(c.month)}. ${summary}`,
    });
  }
  return { lossesB, unitsLost, expended, summary };
}

export function isParty(c: Campaign, n: NationId, b: BattleState) {
  return b.nations.includes(n) && atWar(c, b.nations[0], b.nations[1]);
}
