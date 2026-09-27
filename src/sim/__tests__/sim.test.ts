/// <reference types="node" />
import { describe, expect, it } from "vitest";
import { newCampaign } from "../campaign/create";
import { advanceMonth } from "../campaign/turn";
import { planAiStrike, declareWar } from "../campaign/war";
import { buildBattle } from "../battle/setup";
import { runToEnd } from "../battle/engine";
import { applyBattle } from "../battle/results";
import { dateLabel, forceValue, monthlyIncome, monthlyUpkeep } from "../campaign/core";
import { NATION_IDS } from "../types";

describe("battles", () => {
  it("Iran can strike Israel and Israel defends", () => {
    const c = newCampaign("ISR", 7);
    c.designs["IRN-shahab1"].params.weight = 16000; // Shahab-3 class reach
    c.designs["IRN-shahab1"].params.warhead = 1000;
    declareWar(c, "IRN", "ISR", "test");
    const p = planAiStrike(c, "IRN", "ISR");
    expect(p).not.toBeNull();
    const b = buildBattle(c, p!);
    expect(b.units.length).toBeGreaterThan(3);
    runToEnd(b);
    expect(b.over).toBe(true);
    const out = applyBattle(c, b);
    console.log(b.title, "t=", b.t, out.summary);
    console.log(b.log.slice(0, 15).reverse().map((l) => `${l.t}s ${l.text}`).join("\n"));
    expect(b.counts.launched[0]).toBeGreaterThan(0);
  });

  it("naval engagement in the Arabian Sea runs to completion", () => {
    const c = newCampaign("USA", 3);
    c.units.filter((u) => u.nation === "IRN" && c.designs[u.designId].category === "ship").forEach((u) => (u.location = { kind: "sea", theatreId: "ARAB" }));
    declareWar(c, "USA", "IRN", "test");
    const b = buildBattle(c, { id: "t1", kind: "engagement", a: "USA", b: "IRN", theatreId: "ARAB", title: "test" });
    const t0 = performance.now();
    runToEnd(b);
    const out = applyBattle(c, b);
    console.log("engagement", Math.round(performance.now() - t0), "ms", "t=", b.t, out.summary);
    expect(b.over).toBe(true);
  });
});

describe("campaign", () => {
  it("runs 1990 → 2050 without crashing", () => {
    let c = newCampaign((process.env.BW_NATION as never) ?? "GBR", 11);
    const t0 = performance.now();
    const snap: string[] = [];
    const stop = Number(process.env.BW_MONTHS ?? 12 * 61);
    while (!c.gameOver && c.month < stop) {
      c = advanceMonth(c);
      if (c.month % 120 === 0) {
        snap.push(`${dateLabel(c.month)}: ` + NATION_IDS.map((n) => `${n} $${forceValue(c, n).toFixed(0)}B/${c.units.filter((u) => u.nation === n).length}u/t${c.nations[n].treasury.toFixed(0)}/k${c.nations[n].known.length}/inc${monthlyIncome(c, n).toFixed(1)}/up${monthlyUpkeep(c, n).toFixed(1)}`).join("  "));
      }
    }
    console.log(snap.join("\n"));
    console.log("wars fought:", c.battles.length, "battles; designs:", Object.keys(c.designs).length, "time", Math.round(performance.now() - t0), "ms");
    console.log(c.news.slice(0, 25).join("\n"));
    expect(c.month).toBeGreaterThan(Math.min(600, stop - 12));
  }, 600000);
});
