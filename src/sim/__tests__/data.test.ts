import { describe, expect, it } from "vitest";
import { TECHS, TECH_BY_ID } from "../../data/techs";
import { SPECS } from "../../data/parts";
import { newCampaign } from "../campaign/create";
import { evaluate } from "../design/stats";
import { NATION_IDS } from "../types";

describe("tech tree", () => {
  it("has unique ids and valid prerequisites", () => {
    expect(new Set(TECHS.map((t) => t.id)).size).toBe(TECHS.length);
    for (const t of TECHS) for (const r of t.requires) expect(TECH_BY_ID[r], `${t.id} requires ${r}`).toBeDefined();
  });
  it("parts reference real techs", () => {
    for (const spec of Object.values(SPECS))
      for (const slot of spec.slots)
        for (const p of slot.parts) if (p.tech) expect(TECH_BY_ID[p.tech], `${spec.id}.${slot.id}.${p.id} -> ${p.tech}`).toBeDefined();
  });
});

describe("1990 start", () => {
  const c = newCampaign("USA", 1);
  for (const n of NATION_IDS) {
    it(`${n} in-service designs are legal with ${n}'s known tech`, () => {
      const known = new Set(c.nations[n].known);
      const bad: string[] = [];
      for (const d of Object.values(c.designs)) {
        if (d.nation !== n || d.status === "draft") continue;
        const knownPlusDev = d.status === "developing" ? undefined : known;
        const { issues } = evaluate(d, (id) => c.designs[id], knownPlusDev);
        for (const i of issues) if (i.sev === "error") bad.push(`${d.name}: ${i.text}`);
      }
      expect(bad).toEqual([]);
    });
  }

  it("calibrated missiles land near real-world ranges", () => {
    const r = (id: string) => evaluate(c.designs[id], (x) => c.designs[x]).stats.rangeKm ?? 0;
    expect(r("USA-harpoon")).toBeGreaterThan(100);
    expect(r("USA-harpoon")).toBeLessThan(160);
    expect(r("USA-tlam")).toBeGreaterThan(1300);
    expect(r("USA-sm2")).toBeGreaterThan(120);
    expect(r("USA-sm2")).toBeLessThan(220);
    expect(r("FRA-mm40")).toBeLessThan(90);
    expect(r("IRN-shahab1")).toBeGreaterThan(250);
    expect(r("IRN-shahab1")).toBeLessThan(400);
  });

  it("prints a stat sheet", () => {
    const rows: string[] = [];
    for (const d of Object.values(c.designs)) {
      if (d.category !== "missile") continue;
      const s = evaluate(d, (x) => c.designs[x]).stats;
      rows.push(`${d.name.padEnd(30)} ${String(s.size).padEnd(3)} ${String(s.rangeKm).padStart(5)} km  M${s.mach}  pk ${s.pk}  $${s.cost}M`);
    }
    console.log(rows.join("\n"));
  });
});
