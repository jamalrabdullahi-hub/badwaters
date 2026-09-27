import { FIELDS, TECHS, TECH_BY_ID } from "../../data/techs";
import { pinTech, setFocus, setRdShare } from "../../sim/campaign/actions";
import { monthlyIncome, yearOf } from "../../sim/campaign/core";
import { effectiveCost, fieldTarget, isAvailable, rpPerMonth } from "../../sim/campaign/research";
import type { Campaign } from "../../sim/types";
import { NATION_IDS } from "../../sim/types";
import { NATIONS } from "../../data/nations";
import { fmtB, Hint, Meter, Panel } from "../components/common";
import { commit } from "../store";

export function ResearchScreen({ c }: { c: Campaign }) {
  const me = c.nations[c.player];
  const year = yearOf(c.month);
  const rp = rpPerMonth(c, c.player);
  return (
    <div className="col">
      <div className="grid" style={{ gridTemplateColumns: "minmax(0, 1fr) minmax(0, 1fr)" }}>
        <Panel title="R&D budget" sub={`${rp.toFixed(0)} RP / month`}>
          <div className="param">
            <Hint title="R&D share" text="Share of the monthly budget spent on research. Returns diminish: doubling spending gives about 40% more research points."><label>R&D share</label></Hint>
            <input type="range" min={0.05} max={0.5} step={0.01} value={me.rdShare} onChange={(e) => commit(setRdShare(c, Number(e.target.value)))} />
            <span className="mono">{Math.round(me.rdShare * 100)}%</span>
          </div>
          <div className="dim small">{fmtB(monthlyIncome(c, c.player) * me.rdShare * 12, 2)} per year on research. Techs from beyond the current year ({year}) cost much more. Techs other nations already have are cheaper.</div>
        </Panel>
        <Panel title="Rivals" sub="techs known">
          <div className="row wrap" style={{ gap: 14 }}>
            {NATION_IDS.map((n) => (
              <span key={n} className="small"><span className="nation-chip" style={{ background: NATIONS[n].color }} />{NATIONS[n].adjective} <b className="mono">{c.nations[n].known.length}</b></span>
            ))}
          </div>
        </Panel>
      </div>
      <div className="fields">
        {FIELDS.map((f) => {
          const target = fieldTarget(c, c.player, f.id);
          const cost = target ? effectiveCost(c, c.player, target) : 0;
          const techs = TECHS.filter((t) => t.field === f.id).sort((a, b) => a.year - b.year);
          return (
            <Panel key={f.id} title={f.short} sub={`focus ${me.focus[f.id]}`}>
              <input type="range" min={0} max={5} step={1} value={me.focus[f.id]} onChange={(e) => commit(setFocus(c, f.id, Number(e.target.value)))} aria-label={`${f.name} focus`} />
              {target ? (
                <div className="col" style={{ gap: 4 }}>
                  <span className="small">{target.name}</span>
                  <Meter v={me.rp[f.id] / cost} kind="friend" />
                  <span className="tiny mono dim">{Math.floor(me.rp[f.id])} / {cost} RP{me.target[f.id] ? " · pinned" : ""}</span>
                </div>
              ) : <span className="dim small">Field complete.</span>}
              <div className="col" style={{ gap: 5, maxHeight: "calc(100vh - 380px)", overflow: "auto", paddingRight: 2 }}>
                {techs.map((t) => {
                  const known = me.known.includes(t.id);
                  const avail = !known && isAvailable(c, c.player, t);
                  const cls = known ? "known" : avail ? "avail" : "locked";
                  const cur = target?.id === t.id;
                  const pinned = me.target[f.id] === t.id;
                  const reqs = t.requires.filter((r) => !me.known.includes(r)).map((r) => TECH_BY_ID[r]?.name).join(", ");
                  return (
                    <Hint key={t.id} icon={false} title={t.name} text={<>
                      {t.desc}
                      <br /><span className="dim">Historical year {t.year} · {known ? "known" : `${effectiveCost(c, c.player, t)} RP now`}</span>
                      {reqs && <><br /><span className="bad">Needs: {reqs}</span></>}
                      {avail && <><br /><span className="accent">{pinned ? "Click to unpin" : "Click to research next"}</span></>}
                    </>}>
                      <button className={`tech ${cls} ${cur ? "current" : ""} ${pinned ? "pinned" : ""}`} disabled={!avail}
                        onClick={() => commit(pinTech(c, f.id, pinned ? null : t.id))}>
                        <span className="y">{known ? "✓" : t.year}</span>
                        <span>{t.name}</span>
                        <small>{known ? "known" : avail ? `${effectiveCost(c, c.player, t)} RP` : "locked"}</small>
                      </button>
                    </Hint>
                  );
                })}
              </div>
            </Panel>
          );
        })}
      </div>
    </div>
  );
}
