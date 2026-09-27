import { useMemo, useState } from "react";
import { NATIONS, THEATRE_BY_ID } from "../../data/nations";
import { planStrike } from "../../sim/campaign/actions";
import { atWar, dateLabel, enemiesOf, tension } from "../../sim/campaign/core";
import { buildBattle, strikeCandidates } from "../../sim/battle/setup";
import type { Base, Campaign } from "../../sim/types";
import { NationChip, Panel, Tag } from "../components/common";
import { defaultRegion, RegionPicker, WorldMap } from "../components/WorldMap";
import { setState, toast } from "../store";
import { locText } from "./Forces";

export function OperationsScreen({ c }: { c: Campaign }) {
  const [region, setRegion] = useState(defaultRegion(c.player));
  const [target, setTarget] = useState<Base | null>(null);
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const enemies = enemiesOf(c, c.player);
  const cands = useMemo(() => (target ? strikeCandidates(c, c.player, target) : []), [c, target]);

  const chooseBase = (b: Base) => {
    if (b.nation === c.player) { toast(`${b.name}: your base. Manage units there from Forces.`); return; }
    setTarget(b);
    const cs = strikeCandidates(c, c.player, b);
    setPicked(new Set(cs.slice(0, 8).map((x) => x.unit.id)));
  };
  const launch = () => {
    if (!target) return;
    const r = planStrike(c, target.id, [...picked]);
    if (typeof r === "string") { toast(r); return; }
    setState({ campaign: r.c, battle: { state: buildBattle(r.c, r.pending), pending: r.pending } });
  };
  const war = target ? atWar(c, c.player, target.nation) : false;

  return (
    <div className="grid" style={{ gridTemplateColumns: "minmax(0, 1.7fr) minmax(330px, 1fr)" }}>
      <div className="col">
        <Panel title="Strategic map" actions={<RegionPicker value={region} onChange={setRegion} />} sub="click an enemy base to plan a strike">
          <WorldMap c={c} region={region} height={470} selectedBase={target?.id} onBase={chooseBase} highlight={(b) => enemies.includes(b.nation)} />
          <div className="row wrap small dim" style={{ gap: 16 }}>
            <span><span className="nation-chip" style={{ background: "var(--friend)", borderRadius: "50%" }} />your fleet at sea</span>
            <span>◇ other fleets</span>
            <span>■ bases (◆ missile bases), sized by garrison</span>
            <span className="bad">red outline: at war</span>
          </div>
        </Panel>
        <Panel title="Pending battles" sub={`${c.pending.length}`}>
          {c.pending.map((p) => (
            <div key={p.id} className="row between">
              <span><Tag kind="battle">{p.kind === "strike" ? "Strike" : "Fleet"}</Tag> {p.kind === "engagement" ? `Enemy fleet in the ${THEATRE_BY_ID[p.theatreId]?.name}` : p.title}</span>
              <button className="btn small primary" onClick={() => setState({ battle: { state: buildBattle(c, p), pending: p } })}>Take command</button>
            </div>
          ))}
          {!c.pending.length && <div className="dim small">No battles waiting. Incoming attacks and fleet contacts appear here during wars.</div>}
        </Panel>
      </div>
      <div className="col">
        <Panel title="Wars & crises">
          {c.wars.filter((w) => w.a === c.player || w.b === c.player).map((w) => {
            const enemy = w.a === c.player ? w.b : w.a;
            const score = w.a === c.player ? w.score : -w.score;
            return <div key={w.id} className="row between"><span><Tag kind="war">War</Tag> <NationChip n={enemy} /></span><span className={`mono ${score >= 0 ? "good" : "bad"}`}>{score >= 0 ? "+" : ""}{score.toFixed(1)} · since {dateLabel(w.started)}</span></div>;
          })}
          {(Object.keys(NATIONS) as (keyof typeof NATIONS)[]).filter((n) => n !== c.player && !atWar(c, n, c.player) && tension(c, n, c.player) >= 60).map((n) => (
            <div key={n} className="row between"><span><Tag kind="warn">Tension</Tag> <NationChip n={n} /></span><span className="mono warn">{Math.round(tension(c, n, c.player))}</span></div>
          ))}
          {!c.wars.some((w) => w.a === c.player || w.b === c.player) && <div className="dim small">At peace. Declare war from the World screen once tension with a rival passes 60.</div>}
          <div className="label" style={{ marginTop: 6 }}>Other wars</div>
          {c.wars.filter((w) => w.a !== c.player && w.b !== c.player).map((w) => <div key={w.id} className="small"><NationChip n={w.a} /> vs <NationChip n={w.b} /></div>)}
        </Panel>
        <Panel title={target ? `Strike: ${target.name}` : "Strike planner"} sub={target ? NATIONS[target.nation].name : undefined}>
          {!target && <div className="dim small">Select an enemy base on the map. You'll see every launcher, ship and squadron that can reach it with land-attack weapons: cruise and ballistic missiles, anti-radiation missiles and one-way attack drones.</div>}
          {target && !war && <div className="issue warn">You're not at war with {NATIONS[target.nation].name}. You can plan, but can't launch.</div>}
          {target && (
            <>
              <div className="col" style={{ maxHeight: 360, overflow: "auto", gap: 4 }}>
                {cands.map((x) => (
                  <label key={x.unit.id} className="row" style={{ alignItems: "flex-start", padding: "4px 0", borderBottom: "1px solid #25282d" }}>
                    <input type="checkbox" checked={picked.has(x.unit.id)} onChange={(e) => { const s = new Set(picked); if (e.target.checked) s.add(x.unit.id); else s.delete(x.unit.id); setPicked(s); }} />
                    <span className="grow">
                      <b>{x.unit.name}</b> <span className="dim small">{c.designs[x.unit.designId]?.name}</span><br />
                      <span className="small dim">{locText(c, x.unit)} · {x.distKm.toLocaleString()} km</span><br />
                      <span className="small">{x.weapons.map((w) => `${w.count}× ${w.name} (${w.range.toLocaleString()} km)`).join(", ")}</span>
                    </span>
                  </label>
                ))}
                {!cands.length && <div className="dim small">Nothing you have can reach this target with land-attack weapons. Longer-range missiles, bombers or forward-deployed ships would.</div>}
              </div>
              <div className="row">
                <button className="btn primary" disabled={!war || !picked.size} onClick={launch}>Launch strike ({picked.size} units)</button>
                <button className="btn" onClick={() => setTarget(null)}>Clear</button>
              </div>
            </>
          )}
        </Panel>
      </div>
    </div>
  );
}
