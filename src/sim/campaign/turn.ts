import { NATIONS } from "../../data/nations";
import { buildBattle } from "../battle/setup";
import { runToEnd } from "../battle/engine";
import { applyBattle } from "../battle/results";
import type { Campaign } from "../types";
import { NATION_IDS } from "../types";
import { aiMonth, offerProgrammes, pruneDesigns } from "./ai";
import { dateLabel, END_MONTH, forceValue, monthlyIncome, pushNews } from "./core";
import { processEconomy, processUnits } from "./economy";
import { runEvents } from "./events";
import { doResearch } from "./research";
import { processTensionAndWar } from "./war";

export function clone(c: Campaign): Campaign {
  return structuredClone(c);
}

export function finalScore(c: Campaign): number {
  const n = c.nations[c.player];
  return Math.round(n.prestige * 10 + forceValue(c, c.player) * 2 + n.known.length * 3 + Math.max(0, n.treasury) * 2);
}

/** Advance the campaign one month. Returns a new campaign object. */
export function advanceMonth(prev: Campaign): Campaign {
  const c = clone(prev);
  if (c.gameOver) return c;

  // battles the player didn't take command of are fought automatically
  for (const p of c.pending) {
    const b = buildBattle(c, p);
    runToEnd(b, 2);
    applyBattle(c, b);
  }
  c.pending = [];
  c.inbox = c.inbox.filter((i) => !(i.data?.pending));

  for (const n of NATION_IDS) {
    const learned = doResearch(c, n);
    if (n === c.player) {
      for (const t of learned) {
        c.inbox.push({ id: `tech-${t.id}`, month: c.month, kind: "tech", priority: 40, title: `Research complete: ${t.name}`, body: t.desc });
      }
    } else if (learned.length && NATIONS[n]) {
      for (const t of learned) if (t.year >= 2000) pushNews(c, `Intelligence: ${NATIONS[n].name} has mastered ${t.name}.`);
    }
    const rep = processEconomy(c, n);
    if (n === c.player) {
      for (const name of rep.designsReady) c.inbox.push({ id: `ready-${c.month}-${name}`, month: c.month, kind: "tech", priority: 45, title: `Development complete: ${name}`, body: "The design is in service. Order it from Production." });
      if (rep.completed.length) c.inbox.push({ id: `deliv-${c.month}`, month: c.month, kind: "fleet", priority: 15, title: `${rep.completed.length} deliveries this month`, body: rep.completed.slice(0, 12).join(". ") + (rep.completed.length > 12 ? " …" : "") });
      if (rep.funded < 0.999) c.inbox.push({ id: `short-${c.month}`, month: c.month, kind: "budget", priority: 70, title: `Projects underfunded: ${Math.round(rep.funded * 100)}% paid`, body: `Upkeep $${rep.upkeep.toFixed(2)}B and R&D $${rep.rd.toFixed(2)}B leave too little for development and production this month. Cut orders, retire old units, or lower the R&D share.` });
    }
    processUnits(c, n);
    if (n !== c.player) aiMonth(c, n);
  }
  offerProgrammes(c);
  if (c.month % 12 === 11) pruneDesigns(c);

  runEvents(c);
  processTensionAndWar(c);

  c.month += 1;
  for (const n of NATION_IDS) c.nations[n].mods = c.nations[n].mods.filter((m) => m.until >= c.month);
  // prune old, finished inbox items
  c.inbox = c.inbox.filter((i) => c.month - i.month < 18 || (i.choices && !i.resolved));
  if (c.inbox.length > 120) c.inbox = c.inbox.slice(-120);

  const me = c.nations[c.player];
  if (me.prestige < -60) {
    c.gameOver = { reason: "The government has lost confidence in you and you've been relieved of command.", score: finalScore(c) };
  } else if (me.treasury < -monthlyIncome(c, c.player) * 24) {
    c.gameOver = { reason: "Your forces are bankrupt. Parliament has dissolved the command.", score: finalScore(c) };
  } else if (c.month > END_MONTH) {
    c.gameOver = { reason: `It is ${dateLabel(c.month)}. Your term ends with the campaign.`, score: finalScore(c) };
  }
  return c;
}
