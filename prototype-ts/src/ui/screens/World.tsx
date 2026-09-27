import { useState } from "react";
import { NATIONS } from "../../data/nations";
import { playerDeclareWar, playerOfferPeace } from "../../sim/campaign/actions";
import { atWar, dateLabel, forceValue, monthlyIncome, tension } from "../../sim/campaign/core";
import type { Campaign, NationId } from "../../sim/types";
import { NATION_IDS } from "../../sim/types";
import { fmtB, Hint, Meter, NationChip, Panel, Tag } from "../components/common";
import { commit, toast } from "../store";

export function WorldScreen({ c }: { c: Campaign }) {
  const [confirm, setConfirm] = useState<NationId | null>(null);
  const others = NATION_IDS.filter((n) => n !== c.player);
  return (
    <div className="grid" style={{ gridTemplateColumns: "minmax(0, 1.5fr) minmax(320px, 1fr)" }}>
      <div className="col">
        <Panel title="Nations" flush>
          <table className="t">
            <thead><tr><th>Nation</th><th className="num">Budget</th><th className="num">Forces</th><th className="num">Techs</th><th style={{ width: 170 }}><Hint title="Tension" text="0–100. Crises start at 75, AI nations may attack at 85, and you can declare war from 60. Tension drifts back toward its historical baseline.">Tension with you</Hint></th><th>Status</th><th /></tr></thead>
            <tbody>
              {others.map((n) => {
                const t = tension(c, c.player, n);
                const war = atWar(c, c.player, n);
                return (
                  <tr key={n}>
                    <td><NationChip n={n} /></td>
                    <td className="num">{fmtB(monthlyIncome(c, n) * 12, 0)}</td>
                    <td className="num">{fmtB(forceValue(c, n), 0)}</td>
                    <td className="num">{c.nations[n].known.length}</td>
                    <td><div className="row"><Meter v={t / 100} kind={t >= 75 ? "bad" : undefined} /><span className="mono small">{Math.round(t)}</span></div></td>
                    <td>{war ? <Tag kind="war">War</Tag> : t >= 75 ? <Tag kind="crisis">Crisis</Tag> : NATIONS[n].bloc === NATIONS[c.player].bloc ? <Tag kind="good">Aligned</Tag> : <Tag kind="dim">Peace</Tag>}</td>
                    <td className="right">
                      {war
                        ? <button className="btn small" onClick={() => { const r = playerOfferPeace(c, n); commit(r.c, r.accepted ? "Ceasefire agreed." : "Offer rejected. −2 prestige."); }}>Offer ceasefire</button>
                        : confirm === n
                          ? <span className="row" style={{ justifyContent: "flex-end" }}><button className="btn small danger" onClick={() => { if (commit(playerDeclareWar(c, n), `War declared on ${NATIONS[n].name}.`)) setConfirm(null); }}>Confirm war</button><button className="btn small" onClick={() => setConfirm(null)}>Cancel</button></span>
                          : <button className="btn small danger" disabled={t < 60} onClick={() => (t < 60 ? toast("Tension must be at least 60.") : setConfirm(n))}>Declare war…</button>}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </Panel>
        <Panel title="Tension matrix">
          <div style={{ overflowX: "auto" }}>
            <table className="t" style={{ width: "auto" }}>
              <thead><tr><th />{NATION_IDS.map((n) => <th key={n} className="num">{n}</th>)}</tr></thead>
              <tbody>
                {NATION_IDS.map((a) => (
                  <tr key={a}>
                    <td><NationChip n={a} name={false} />{a}</td>
                    {NATION_IDS.map((b) => {
                      if (a === b) return <td key={b} />;
                      const t = tension(c, a, b);
                      const war = atWar(c, a, b);
                      return <td key={b} className="num mono" style={{ background: war ? "rgba(229,72,77,0.35)" : `rgba(240,164,58,${(t / 100) * 0.35})` }}>{war ? "WAR" : Math.round(t)}</td>;
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      </div>
      <div className="col">
        <Panel title="Battle history" sub={`${c.battles.length}`} scroll style={{ maxHeight: "calc(100vh - 140px)" }}>
          {c.battles.slice(0, 60).map((b) => (
            <div key={b.id} className="inbox-item" style={{ gridTemplateColumns: "1fr auto" }}>
              <span className="ttl">{b.title}</span><span className="when">{dateLabel(b.month)}</span>
              <p style={{ gridColumn: "1 / -1" }}>{b.winner ? <><NationChip n={b.winner} /> advantage. </> : "Inconclusive. "}{b.summary}</p>
            </div>
          ))}
          {!c.battles.length && <div className="dim small">No battles yet.</div>}
        </Panel>
      </div>
    </div>
  );
}
