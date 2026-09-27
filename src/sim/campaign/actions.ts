// Player commands. Each takes the current campaign and returns a new one (or an error string).
import { THEATRE_BY_ID } from "../../data/nations";
import { SPECS } from "../../data/parts";
import type { Campaign, Design, FieldId, NationId, PendingBattle, UnitLocation } from "../types";
import { evaluate } from "../design/stats";
import { distKm, knownSet, newId, statsOf, tension, unitLatLon, yearOf } from "./core";
import { transitMonths } from "./ai";
import { resolveEventChoice } from "./events";
import { clone } from "./turn";
import { isLegitimateTarget, PROTECTED_TARGET_MESSAGE, targetClassOfBase } from "../policy";
import { declareWar, offerPeace, resolveCrisisChoice } from "./war";

export type Result = Campaign | string;

export function setRdShare(c0: Campaign, share: number): Campaign {
  const c = clone(c0);
  c.nations[c.player].rdShare = Math.max(0.05, Math.min(0.5, share));
  return c;
}

export function setFocus(c0: Campaign, f: FieldId, v: number): Campaign {
  const c = clone(c0);
  c.nations[c.player].focus[f] = Math.max(0, Math.min(5, v));
  return c;
}

export function pinTech(c0: Campaign, f: FieldId, techId: string | null): Campaign {
  const c = clone(c0);
  if (techId) c.nations[c.player].target[f] = techId;
  else delete c.nations[c.player].target[f];
  return c;
}

export function blankDesign(c: Campaign, category: Design["category"]): Design {
  const spec = SPECS[category];
  const known = knownSet(c, c.player);
  const slots: Record<string, string> = {};
  for (const s of spec.slots) slots[s.id] = (s.parts.find((p) => !p.tech || known.has(p.tech)) ?? s.parts[0]).id;
  const params: Record<string, number> = {};
  spec.params.forEach((p) => (params[p.id] = p.def));
  return {
    id: "", nation: c.player, category, name: `New ${spec.name.toLowerCase()}`, year: yearOf(c.month),
    slots, params, loadout: [], status: "draft", devMonthsLeft: 0, lineage: "",
  };
}

/** Save a draft (new or existing). Returns the new campaign and the saved id. */
export function saveDesign(c0: Campaign, d: Design): { c: Campaign; id: string } {
  const c = clone(c0);
  const existing = d.id ? c.designs[d.id] : undefined;
  if (existing && existing.status !== "draft") {
    // editing an in-service design makes a new variant in the same family
    const id = newId(c, `${c.player}-d`);
    c.designs[id] = { ...structuredClone(d), id, status: "draft", year: yearOf(c.month), lineage: existing.lineage };
    return { c, id };
  }
  const id = d.id || newId(c, `${c.player}-d`);
  c.designs[id] = { ...structuredClone(d), id, status: "draft", nation: c.player, lineage: d.lineage || `${c.player}:${id}` };
  return { c, id };
}

export function deleteDraft(c0: Campaign, id: string): Result {
  const d = c0.designs[id];
  if (!d || d.status !== "draft") return "Only drafts can be deleted.";
  const c = clone(c0);
  delete c.designs[id];
  return c;
}

export function startDevelopment(c0: Campaign, id: string): Result {
  const d = c0.designs[id];
  if (!d || d.status !== "draft") return "Only drafts can go into development.";
  const { stats, issues } = evaluate(d, (x) => c0.designs[x], knownSet(c0, c0.player));
  const err = issues.find((i) => i.sev === "error");
  if (err) return `Fix the design first: ${err.text}`;
  const c = clone(c0);
  const nd = c.designs[id];
  if (stats.devMonths <= 0) { nd.status = "service"; nd.devMonthsLeft = 0; }
  else { nd.status = "developing"; nd.devMonthsLeft = stats.devMonths; }
  return c;
}

export function orderProduction(c0: Campaign, designId: string, qty: number, baseId: string): Result {
  const d = c0.designs[designId];
  if (!d || d.status !== "service") return "Only designs in service can be built.";
  if (d.nation !== c0.player) return "That isn't your design.";
  const s = statsOf(c0, designId);
  if (!s) return "Unknown design.";
  const munition = d.category === "missile" || (d.category === "drone" && s.role === "loiter");
  if (!munition && !c0.bases.some((b) => b.id === baseId && b.nation === c0.player)) return "Choose one of your bases for delivery.";
  const c = clone(c0);
  c.nations[c.player].orders.push({ id: newId(c, "o"), designId, qty: Math.max(1, Math.round(qty)), monthsPerUnit: s.buildMonths, progress: 0, destBaseId: munition ? "" : baseId });
  return c;
}

export function cancelOrder(c0: Campaign, orderId: string): Campaign {
  const c = clone(c0);
  c.nations[c.player].orders = c.nations[c.player].orders.filter((o) => o.id !== orderId);
  return c;
}

export function moveUnit(c0: Campaign, unitId: string, to: { kind: "base"; baseId: string } | { kind: "sea"; theatreId: string }): Result {
  const u = c0.units.find((x) => x.id === unitId);
  if (!u || u.nation !== c0.player) return "Unit not found.";
  if (u.embarkedOn) return "This squadron is embarked. Move its carrier instead, or disembark it at a base first.";
  const d = c0.designs[u.designId];
  if (to.kind === "sea" && d?.category !== "ship") return "Only ships can deploy to sea.";
  if (to.kind === "base") {
    const b = c0.bases.find((x) => x.id === to.baseId);
    if (!b || b.nation !== c0.player) return "Choose one of your own bases.";
  }
  const from = unitLatLon(c0, u);
  const dest = to.kind === "base" ? c0.bases.find((b) => b.id === to.baseId)! : THEATRE_BY_ID[to.theatreId];
  const months = from ? transitMonths(from[0], from[1], dest.lat, dest.lon) : 1;
  const c = clone(c0);
  const cu = c.units.find((x) => x.id === unitId)!;
  const loc: UnitLocation = months > 0 ? { kind: "transit", to, monthsLeft: months } : to;
  cu.location = loc;
  for (const w of c.units) if (w.embarkedOn === cu.id) w.location = loc;
  return c;
}

export function embarkOn(c0: Campaign, sqnId: string, carrierId: string | null): Result {
  const c = clone(c0);
  const u = c.units.find((x) => x.id === sqnId && x.nation === c.player);
  if (!u) return "Squadron not found.";
  if (carrierId === null) {
    const cv = c.units.find((x) => x.id === u.embarkedOn);
    if (!cv || cv.location.kind !== "base") return "The carrier must be in port to disembark.";
    u.embarkedOn = undefined;
    u.location = cv.location;
    return c;
  }
  const cv = c.units.find((x) => x.id === carrierId && x.nation === c.player);
  const cs = cv ? statsOf(c, cv.designId) : undefined;
  const ss = statsOf(c, u.designId);
  if (!cv || !cs || !(cs.airWing && cs.airWing > 0)) return "That ship can't carry aircraft.";
  if (!ss?.carrier || ss.carrier === "none") return "This squadron isn't carrier-capable.";
  if (cs.carrier === "stovl" && ss.carrier === "catobar") return "Catapult aircraft need a catapult carrier.";
  if (u.location.kind !== "base" || cv.location.kind !== "base" || u.location.baseId !== cv.location.baseId) return "Squadron and carrier must be at the same base.";
  const used = c.units.filter((x) => x.embarkedOn === cv.id).reduce((a, x) => a + (c.designs[x.designId]?.params.sqn ?? 12), 0);
  if (used + (c.designs[u.designId]?.params.sqn ?? 12) > cs.airWing) return `Not enough deck space (${cs.airWing - used} aircraft free).`;
  u.embarkedOn = cv.id;
  u.location = cv.location;
  return c;
}

export function decommission(c0: Campaign, unitId: string): Result {
  const u = c0.units.find((x) => x.id === unitId);
  if (!u || u.nation !== c0.player) return "Unit not found.";
  const c = clone(c0);
  for (const w of c.units) if (w.embarkedOn === unitId) { w.embarkedOn = undefined; }
  c.units = c.units.filter((x) => x.id !== unitId);
  c.nations[c.player].prestige -= 0.5;
  return c;
}

export function resolveInbox(c0: Campaign, itemId: string, choiceId: string): Campaign {
  const c = clone(c0);
  const it = c.inbox.find((i) => i.id === itemId);
  if (!it || it.resolved) return c;
  it.resolved = choiceId;
  const h = it.data?.handler;
  if (h === "crisis") resolveCrisisChoice(c, it.data!.enemy as NationId, choiceId);
  else if (typeof h === "string") resolveEventChoice(c, h, choiceId);
  return c;
}

export function dismissInbox(c0: Campaign, itemId: string): Campaign {
  const c = clone(c0);
  c.inbox = c.inbox.filter((i) => i.id !== itemId || (i.choices && !i.resolved));
  return c;
}

export function playerDeclareWar(c0: Campaign, enemy: NationId): Result {
  if (tension(c0, c0.player, enemy) < 60) return "Tension is too low. Your government won't approve a war below 60.";
  const c = clone(c0);
  declareWar(c, c.player, enemy, "You ordered the opening strikes.");
  c.nations[c.player].prestige -= 4;
  return c;
}

export function playerOfferPeace(c0: Campaign, enemy: NationId): { c: Campaign; accepted: boolean } {
  const c = clone(c0);
  const accepted = offerPeace(c, enemy);
  return { c, accepted };
}

export function planStrike(c0: Campaign, targetBaseId: string, sources: string[]): { c: Campaign; pending: PendingBattle } | string {
  const target = c0.bases.find((b) => b.id === targetBaseId);
  if (!target) return "Unknown target.";
  const cls = targetClassOfBase(target.type);
  if (!cls || !isLegitimateTarget(cls)) return PROTECTED_TARGET_MESSAGE; // see src/sim/policy.ts
  const enemy = target.nation;
  const war = c0.wars.some((w) => (w.a === c0.player && w.b === enemy) || (w.b === c0.player && w.a === enemy));
  if (!war) return "You can only strike a nation you're at war with.";
  if (!sources.length) return "Select at least one launcher or squadron.";
  const c = clone(c0);
  const pending: PendingBattle = { id: newId(c, "bt"), kind: "strike", attacker: c.player, defender: enemy, targetBaseId, sources, title: `Strike on ${target.name}` };
  return { c, pending };
}

export function removePending(c0: Campaign, id: string): Campaign {
  const c = clone(c0);
  c.pending = c.pending.filter((p) => p.id !== id);
  c.inbox = c.inbox.filter((i) => i.data?.pending !== id);
  return c;
}

export function kmBetween(c: Campaign, unitId: string, baseId: string): number {
  const u = c.units.find((x) => x.id === unitId);
  const b = c.bases.find((x) => x.id === baseId);
  const ll = u ? unitLatLon(c, u) : null;
  return ll && b ? distKm(ll[0], ll[1], b.lat, b.lon) : Infinity;
}
