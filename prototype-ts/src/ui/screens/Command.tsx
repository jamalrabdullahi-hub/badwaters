import { useState } from "react";
import { NATIONS } from "../../data/nations";
import { FIELDS } from "../../data/techs";
import { dateLabel, forceValue, monthlyIncome, monthlyUpkeep, tension } from "../../sim/campaign/core";
import { projectDemand } from "../../sim/campaign/economy";
import { effectiveCost, fieldTarget, rpPerMonth } from "../../sim/campaign/research";
import { dismissInbox, resolveInbox } from "../../sim/campaign/actions";
import { buildBattle } from "../../sim/battle/setup";
import type { Campaign, InboxItem } from "../../sim/types";
import { NATION_IDS } from "../../sim/types";
import { fmtB, Hint, Meter, Panel, Tag } from "../components/common";
import { defaultRegion, RegionPicker, WorldMap } from "../components/WorldMap";
import { commit, setState } from "../store";

const KIND_LABEL: Record<string, string> = { crisis: "Crisis", war: "War", budget: "Budget", tech: "Tech", fleet: "Fleet", battle: "Battle", intel: "Intel", world: "World" };

export function CommandScreen({ c }: { c: Campaign }) {
  const me = c.nations[c.player];
  const income = monthlyIncome(c, c.player);
  const upkeep = monthlyUpkeep(c, c.player);
  const { dev, prod } = projectDemand(c, c.player);
  const myUnits = c.units.filter((u) => u.nation === c.player);
  const readiness = myUnits.reduce((a, u) => a + u.readiness, 0) / Math.max(1, myUnits.length);
  const rivals = NATION_IDS.filter((n) => n !== c.player).map((n) => ({ n, t: tension(c, c.player, n) })).sort((a, b) => b.t - a.t);
  const [region, setRegion] = useState(defaultRegion(c.player));

  const items = [...c.inbox].sort((a, b) => {
    const open = (i: InboxItem) => ((i.choices && !i.resolved) || i.data?.pending ? 1 : 0);
    return open(b) - open(a) || b.priority - a.priority || b.month - a.month;
  });
  const pinned = items.filter((i) => (i.choices && !i.resolved) || i.data?.pending);
  const routine = items.filter((i) => !pinned.includes(i));

  const committed = upkeep + income * me.rdShare + dev + prod;
  return (
    <div className="grid" style={{ gridTemplateColumns: "minmax(0, 1.6fr) minmax(340px, 1fr)", gridTemplateRows: "auto auto 1fr" }}>
      <div className="kpis" style={{ gridColumn: "1 / -1" }}>
        <div className="kpi">
          <Hint title="Force value" text="Replacement cost of all your ships, squadrons, batteries and drones, reduced for damage."><span>FORCE VALUE</span></Hint>
          <b>{fmtB(forceValue(c, c.player), 0)}</b>
          <span>{myUnits.length} units · {Object.values(me.stock).reduce((a, b) => a + b, 0).toLocaleString()} munitions</span>
        </div>
        <div className="kpi">
          <Hint title="Readiness" text="Average training and maintenance state. Units at sea lose readiness; units at base recover it if upkeep is paid."><span>READINESS</span></Hint>
          <b>{Math.round(readiness * 100)}%</b>
          <Meter v={readiness} kind={readiness < 0.6 ? "bad" : "good"} thin />
        </div>
        <div className="kpi">
          <Hint title="Budget committed" text="Upkeep, R&D, development and production as a share of this month's income. Over 100% draws down the reserve."><span>BUDGET COMMITTED</span></Hint>
          <b className={committed > income ? "bad" : ""}>{Math.round((committed / income) * 100)}%</b>
          <span>{fmtB(committed * 12, 1)} of {fmtB(income * 12, 1)} per year</span>
          <Meter v={committed / income} kind={committed > income ? "bad" : undefined} thin />
        </div>
        <div className="kpi">
          <Hint title="Highest tension" text="0–100. At 75 a crisis starts. At 85 an AI nation may open fire. You can't declare war below 60."><span>HIGHEST TENSION</span></Hint>
          <b className={rivals[0].t >= 75 ? "bad" : ""}>{Math.round(rivals[0].t)}</b>
          <span>{NATIONS[rivals[0].n].name}</span>
          <Meter v={rivals[0].t / 100} kind={rivals[0].t >= 75 ? "bad" : undefined} thin />
        </div>
        <div className="kpi">
          <Hint title="Research" text="Research points per month. Diminishing returns on money: doubling R&D spending gives about 40% more points."><span>RESEARCH</span></Hint>
          <b>{rpPerMonth(c, c.player).toFixed(0)} RP</b>
          <span>{me.known.length} techs · R&D share {Math.round(me.rdShare * 100)}%</span>
        </div>
      </div>

      <Panel title="Decisions" sub={`${pinned.length} need you`} scroll style={{ gridRow: "2 / 4", maxHeight: "calc(100vh - 240px)" }}>
        {pinned.map((i) => <InboxRow key={i.id} c={c} i={i} />)}
        {!pinned.length && <div className="dim small">Nothing needs a decision. Routine reports are below.</div>}
        <div className="label" style={{ marginTop: 6 }}>Reports</div>
        {routine.slice(0, 40).map((i) => <InboxRow key={i.id} c={c} i={i} />)}
      </Panel>

      <Panel title="Theatre" actions={<RegionPicker value={region} onChange={setRegion} />}>
        <WorldMap c={c} region={region} height={300} onBase={() => setState({ screen: "operations" })} />
      </Panel>

      <div className="grid" style={{ gridTemplateColumns: "1fr 1fr" }}>
        <Panel title="Pipeline" sub={`${me.orders.length} orders`} scroll style={{ maxHeight: 360 }}>
          {Object.values(c.designs).filter((d) => d.nation === c.player && d.status === "developing").map((d) => (
            <div key={d.id} className="stat"><span>{d.name}</span><span className="mono dim small">dev · {Math.ceil(d.devMonthsLeft)} mo</span></div>
          ))}
          {me.orders.slice(0, 12).map((o) => {
            const d = c.designs[o.designId];
            return (
              <div key={o.id} className="stat">
                <span>{d?.name ?? "?"}</span><span className="mono dim small">×{o.qty}</span>
                <Meter v={o.monthsPerUnit >= 1 ? o.progress / o.monthsPerUnit : 1} thin />
              </div>
            );
          })}
          {!me.orders.length && <div className="dim small">No production orders. Order units from Production.</div>}
        </Panel>
        <Panel title="Research" sub={`${rpPerMonth(c, c.player).toFixed(0)} RP/mo`} scroll style={{ maxHeight: 360 }}>
          {FIELDS.map((f) => {
            const t = fieldTarget(c, c.player, f.id);
            const cost = t ? effectiveCost(c, c.player, t) : 1;
            return (
              <div key={f.id} className="stat">
                <span className="small">{t ? t.name : <span className="dim">All {f.short.toLowerCase()} researched</span>}</span>
                <span className="mono dim tiny">{f.short}</span>
                <Meter v={t ? me.rp[f.id] / cost : 1} kind="friend" thin />
              </div>
            );
          })}
        </Panel>
      </div>
      <Panel title="News" scroll style={{ gridColumn: 2, maxHeight: 220 }}>
        {c.news.slice(0, 30).map((n, i) => <div key={i} className="small dim">{n}</div>)}
      </Panel>
    </div>
  );
}

function InboxRow({ c, i }: { c: Campaign; i: InboxItem }) {
  const pend = i.data?.pending ? c.pending.find((p) => p.id === i.data!.pending) : undefined;
  return (
    <div className="inbox-item">
      <Tag kind={i.kind}>{KIND_LABEL[i.kind]}</Tag>
      <span className="ttl">{i.title}</span>
      <span className="when">{dateLabel(i.month)}</span>
      <p>{i.body}</p>
      {(i.choices || pend || !i.choices) && (
        <div className="acts">
          {i.choices && !i.resolved && i.choices.map((ch) => (
            <Hint key={ch.id} title={ch.label} text={ch.hint} icon={false}>
              <button className={`btn small ${ch.id === "war" ? "danger" : ""}`} onClick={() => commit(resolveInbox(c, i.id, ch.id))}>{ch.label}</button>
            </Hint>
          ))}
          {i.resolved && <span className="dim small">Decided: {i.choices?.find((x) => x.id === i.resolved)?.label}</span>}
          {pend && <button className="btn small primary" onClick={() => setState({ battle: { state: buildBattle(c, pend), pending: pend } })}>Take command</button>}
          {!i.choices && !pend && <button className="btn small" onClick={() => commit(dismissInbox(c, i.id))}>Dismiss</button>}
        </div>
      )}
    </div>
  );
}
