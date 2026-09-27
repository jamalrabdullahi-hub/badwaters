import { describe, expect, it } from "vitest";
import { isLegitimateTarget, NUCLEAR_POLICY, PROTECTED_TARGET_MESSAGE, TARGET_POLICY } from "../policy";
import { newCampaign } from "../campaign/create";
import { planStrike } from "../campaign/actions";
import { declareWar, planAiStrike } from "../campaign/war";
import { strikeCandidates } from "../battle/setup";
import type { Base } from "../types";

describe("design intent: targeting policy", () => {
  it("protects population centres and civilian shelters", () => {
    expect(isLegitimateTarget("population-centre")).toBe(false);
    expect(isLegitimateTarget("civil-shelter")).toBe(false);
    expect(TARGET_POLICY["population-centre"]).toBe("protected");
  });

  it("refuses unknown target classes", () => {
    expect(isLegitimateTarget("city")).toBe(false);
    expect(isLegitimateTarget("")).toBe(false);
  });

  it("allows military and war-sustaining infrastructure", () => {
    for (const k of ["military-force", "military-base", "command-military", "industrial", "logistics", "strategic-reserve"]) {
      expect(isLegitimateTarget(k)).toBe(true);
    }
  });

  it("blocks player strikes, AI strikes and candidate lists on a protected site", () => {
    const c = newCampaign("USA", 5);
    declareWar(c, "USA", "IRN", "test");
    const city = { id: "ir-city", nation: "IRN", name: "Test city", lat: 29.6, lon: 52.5, type: "city", theatre: "GULF", damage: 0 } as unknown as Base;
    c.bases.push(city);
    expect(planStrike(c, city.id, [c.units[0].id])).toBe(PROTECTED_TARGET_MESSAGE);
    expect(strikeCandidates(c, "USA", city)).toEqual([]);
    declareWar(c, "IRN", "USA", "test");
    const p = planAiStrike(c, "USA", "IRN");
    if (p && p.kind === "strike") expect(p.targetBaseId).not.toBe(city.id);
  });

  it("keeps nuclear weapons abstract", () => {
    expect(NUCLEAR_POLICY.targetingGameplay).toBe(false);
    expect(NUCLEAR_POLICY.outcome).toBe("campaign-ends");
  });
});
