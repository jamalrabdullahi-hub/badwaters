import { NATIONS, START_TENSION, THEATRE_BY_ID } from "../../data/nations";
import { buildBattle, strikeCandidates } from "../battle/setup";
import { runToEnd } from "../battle/engine";
import { applyBattle } from "../battle/results";
import type { Campaign, NationId, PendingBattle } from "../types";
import { NATION_IDS } from "../types";
import { isLegitimateTarget, targetClassOfBase } from "../policy";
import { addTension, atWar, dateLabel, forceValue, monthRng, newId, pairKey, pushNews, statsOf, tension, unitTheatre } from "./core";

const BASELINE: Record<string, number> = Object.fromEntries(START_TENSION.map(([a, b, v]) => [pairKey(a, b), v]));

export function declareWar(c: Campaign, a: NationId, b: NationId, reason: string) {
  if (atWar(c, a, b)) return;
  c.wars.push({ id: newId(c, "w"), a, b, started: c.month, score: 0, theatres: [] });
  addTension(c, a, b, 100);
  pushNews(c, `WAR: ${NATIONS[a].name} and ${NATIONS[b].name} are at war. ${reason}`);
  if (a === c.player || b === c.player) {
    const enemy = a === c.player ? b : a;
    c.inbox.push({
      id: newId(c, "war"), month: c.month, kind: "war", priority: 100,
      title: `War with ${NATIONS[enemy].name}`,
      body: `${dateLabel(c.month)}. ${reason} Plan strikes from Operations. Expect incoming missiles and enemy fleets hunting yours.`,
    });
  }
}

export function endWar(c: Campaign, id: string, reason: string) {
  const w = c.wars.find((x) => x.id === id);
  if (!w) return;
  c.wars = c.wars.filter((x) => x.id !== id);
  const winner = w.score > 1 ? w.a : w.score < -1 ? w.b : null;
  const loser = winner === w.a ? w.b : winner === w.b ? w.a : null;
  if (winner && loser) { c.nations[winner].prestige += 12; c.nations[loser].prestige -= 12; }
  c.tension[pairKey(w.a, w.b)] = 55;
  const text = winner ? `${NATIONS[winner].name} comes out ahead (war score ${Math.abs(w.score).toFixed(1)}).` : "Neither side can claim victory.";
  pushNews(c, `Ceasefire between ${NATIONS[w.a].name} and ${NATIONS[w.b].name}. ${text}`);
  if (w.a === c.player || w.b === c.player) {
    c.inbox.push({ id: newId(c, "peace"), month: c.month, kind: "war", priority: 90, title: "Ceasefire", body: `${dateLabel(c.month)}. ${reason} ${text}` });
  }
}

function crisis(c: Campaign, a: NationId, b: NationId) {
  const key = `crisis-${pairKey(a, b)}`;
  const open = c.inbox.find((i) => i.data?.handler === "crisis" && i.data?.pair === pairKey(a, b) && !i.resolved);
  if (open) return;
  const enemy = a === c.player ? b : a;
  c.inbox.push({
    id: newId(c, key), month: c.month, kind: "crisis", priority: 95,
    title: `Crisis with ${NATIONS[enemy].name}`,
    body: `${dateLabel(c.month)}. Tension with ${NATIONS[enemy].name} has reached ${Math.round(tension(c, a, b))}. Their forces are on alert, and the next move is yours.`,
    choices: [
      { id: "calm", label: "De-escalate", hint: "Tension −20, prestige −3" },
      { id: "hold", label: "Hold firm", hint: "Prestige +1. They may still attack." },
      { id: "war", label: "Go to war", hint: "Opens hostilities. Strikes become available." },
    ],
    data: { handler: "crisis", pair: pairKey(a, b), enemy },
  });
}

export function resolveCrisisChoice(c: Campaign, enemy: NationId, choice: string) {
  const p = c.player;
  if (choice === "calm") { addTension(c, p, enemy, -20); c.nations[p].prestige -= 3; }
  if (choice === "hold") c.nations[p].prestige += 1;
  if (choice === "war") declareWar(c, p, enemy, `${NATIONS[p].name} chose war after a crisis.`);
}

/** Pick the enemy base with the most strike weapons in reach, and all launchers that can hit it. */
export function planAiStrike(c: Campaign, attacker: NationId, defender: NationId): PendingBattle | null {
  let best: { baseId: string; sources: string[]; weight: number; name: string } | null = null;
  for (const b of c.bases) {
    if (b.nation !== defender) continue;
    const cls = targetClassOfBase(b.type);
    if (!cls || !isLegitimateTarget(cls)) continue; // the AI obeys the same targeting policy (src/sim/policy.ts)
    const cands = strikeCandidates(c, attacker, b);
    const weight = cands.reduce((a, x) => a + x.weapons.reduce((q, w) => q + w.count, 0), 0) * (b.type === "air" || b.type === "missile" ? 1.3 : 1);
    if (weight > (best?.weight ?? 0)) best = { baseId: b.id, sources: cands.slice(0, 14).map((x) => x.unit.id), weight, name: b.name };
  }
  if (!best || best.weight < 4) return null;
  return {
    id: newId(c, "bt"), kind: "strike", attacker, defender, targetBaseId: best.baseId, sources: best.sources,
    title: `${NATIONS[attacker].adjective} strike on ${best.name}`,
  };
}

function engagementsFor(c: Campaign, a: NationId, b: NationId): PendingBattle[] {
  const seaA = new Set<string>(), seaB = new Set<string>();
  for (const u of c.units) {
    if (u.location.kind !== "sea" || c.designs[u.designId]?.category !== "ship") continue;
    if (u.nation === a) seaA.add(u.location.theatreId);
    if (u.nation === b) seaB.add(u.location.theatreId);
  }
  const out: PendingBattle[] = [];
  for (const t of seaA) if (seaB.has(t)) out.push({ id: newId(c, "bt"), kind: "engagement", a, b, theatreId: t, title: `Battle of the ${THEATRE_BY_ID[t]?.name ?? t}` });
  return out;
}

export function processTensionAndWar(c: Campaign) {
  const r = monthRng(c, 5);
  // drift and friction
  for (let i = 0; i < NATION_IDS.length; i++) for (let j = i + 1; j < NATION_IDS.length; j++) {
    const a = NATION_IDS[i], b = NATION_IDS[j];
    if (atWar(c, a, b)) continue;
    const k = pairKey(a, b);
    const base = BASELINE[k] ?? 15;
    let t = c.tension[k] ?? 15;
    t += (base - t) * 0.015 + (r() - 0.5) * 1.5;
    if (NATIONS[a].bloc === NATIONS[b].bloc) t = Math.min(t, 25);
    c.tension[k] = Math.max(0, Math.min(100, t));
  }
  // fleets operating near a rival's bases add friction
  for (const u of c.units) {
    if (u.location.kind !== "sea") continue;
    const th = u.location.theatreId;
    for (const n of NATION_IDS) {
      if (n === u.nation || NATIONS[n].bloc === NATIONS[u.nation].bloc) continue;
      if (c.bases.some((b) => b.nation === n && b.theatre === th) && tension(c, n, u.nation) > 35) addTension(c, n, u.nation, 0.08);
    }
  }
  // crises and war declarations
  for (let i = 0; i < NATION_IDS.length; i++) for (let j = i + 1; j < NATION_IDS.length; j++) {
    const a = NATION_IDS[i], b = NATION_IDS[j];
    if (atWar(c, a, b)) continue;
    const t = tension(c, a, b);
    if (t < 75) continue;
    const player = a === c.player || b === c.player;
    if (player) {
      crisis(c, a, b);
      const ai = a === c.player ? b : a;
      if (t >= 85 && r() < ((t - 85) / 15) * 0.25 && forceValue(c, ai) > forceValue(c, c.player) * 0.15) {
        declareWar(c, ai, c.player, `${NATIONS[ai].name} opens fire after months of rising tension.`);
      }
    } else if (r() < ((t - 75) / 25) * 0.1) {
      declareWar(c, r() < 0.5 ? a : b, r() < 0.5 ? b : a, "A long crisis boils over.");
    }
  }
  // wars: AI-vs-AI battles are fought headless, capped per month to keep turns fast
  let aiBattles = 0;
  const AI_BATTLE_CAP = 3;
  for (const w of [...c.wars]) {
    const months = c.month - w.started;
    const playerWar = w.a === c.player || w.b === c.player;
    for (const [att, def] of [[w.a, w.b], [w.b, w.a]] as [NationId, NationId][]) {
      if (att === c.player) continue;
      if (r() < (playerWar ? 0.6 : 0.35)) {
        if (!playerWar && aiBattles >= AI_BATTLE_CAP) continue;
        const p = planAiStrike(c, att, def);
        if (p) {
          if (playerWar) { c.pending.push(p); c.inbox.push(incomingItem(c, p)); }
          else { aiBattles++; const b = buildBattle(c, p); runToEnd(b, 3); applyBattle(c, b); }
        }
      }
    }
    for (const e of engagementsFor(c, w.a, w.b)) {
      if (playerWar) {
        if (!c.pending.some((p) => p.kind === "engagement" && p.theatreId === (e as { theatreId: string }).theatreId)) {
          c.pending.push(e);
          c.inbox.push(incomingItem(c, e));
        }
      } else if (aiBattles < AI_BATTLE_CAP) { aiBattles++; const b = buildBattle(c, e); runToEnd(b, 3); applyBattle(c, b); }
    }
    const pEnd = months < 4 ? 0 : 0.06 + Math.min(0.4, Math.abs(w.score) / 40) + months * 0.008;
    if (r() < pEnd) endWar(c, w.id, "Exhausted and under pressure from the world, both sides accept a ceasefire.");
  }
}

function incomingItem(c: Campaign, p: PendingBattle) {
  const isDef = p.kind === "strike" && p.defender === c.player;
  return {
    id: `pend-${p.id}`, month: c.month, kind: "battle" as const, priority: 99,
    title: p.kind === "strike" ? (isDef ? `Incoming: ${p.title}` : p.title) : `Contact: enemy fleet in ${unitTheatreName(p)}`,
    body: p.kind === "strike"
      ? `${dateLabel(c.month)}. Launch warnings from early-warning radars. Take command of the defence from Operations, or it will be fought automatically when you end the month.`
      : `${dateLabel(c.month)}. Your ships and an enemy force are operating in the same waters. Take command in Operations, or the battle will be fought automatically.`,
    data: { pending: p.id },
  };
}

function unitTheatreName(p: PendingBattle) {
  return p.kind === "engagement" ? THEATRE_BY_ID[p.theatreId]?.name ?? p.theatreId : "";
}

export function offerPeace(c: Campaign, enemy: NationId): boolean {
  const w = c.wars.find((x) => (x.a === c.player && x.b === enemy) || (x.b === c.player && x.a === enemy));
  if (!w) return false;
  const mine = w.a === c.player ? w.score : -w.score;
  const months = c.month - w.started;
  const accept = mine > 0.5 || months > 8 || monthRng(c, 99)() < 0.3;
  if (accept) endWar(c, w.id, `${NATIONS[enemy].name} accepts your offer of a ceasefire.`);
  else c.nations[c.player].prestige -= 2;
  return accept;
}

export { unitTheatre, statsOf };
