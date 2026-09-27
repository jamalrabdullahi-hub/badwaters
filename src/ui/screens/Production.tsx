import { useState } from "react";
import { SPECS } from "../../data/parts";
import { cancelOrder, orderProduction } from "../../sim/campaign/actions";
import { evalDesign, monthlyIncome, monthlyUpkeep, statsOf } from "../../sim/campaign/core";
import { projectDemand } from "../../sim/campaign/economy";
import type { Campaign } from "../../sim/types";
import { fmtB, fmtM, Hint, Meter, NumField, Panel, Stat } from "../components/common";
import { commit } from "../store";

export function ProductionScreen({ c }: { c: Campaign }) {
  const me = c.nations[c.player];
  const income = monthlyIncome(c, c.player);
  const upkeep = monthlyUpkeep(c, c.player);
  const rd = income * me.rdShare;
  const { dev, prod } = projectDemand(c, c.player);
  const surplus = income - upkeep - rd - dev - prod;
  const buildable = Object.values(c.designs).filter((d) => d.nation === c.player && d.status === "service" && d.category !== "base").sort((a, b) => a.category.localeCompare(b.category) || a.name.localeCompare(b.name));
  const bases = c.bases.filter((b) => b.nation === c.player);
  const baseDesigns = Object.values(c.designs).filter((d) => d.nation === c.player && d.category === "base" && d.status === "service");
  const [sel, setSel] = useState(buildable[0]?.id ?? "");
  const [qty, setQty] = useState(1);
  const [dest, setDest] = useState(bases[0]?.id ?? "");
  const [bd, setBd] = useState(baseDesigns[0]?.id ?? "");
  const [bt, setBt] = useState(bases[0]?.id ?? "");
  const d = c.designs[sel];
  const s = d ? evalDesign(c, d).stats : undefined;
  const munition = !!d && (d.category === "missile" || (d.category === "drone" && s?.role === "loiter"));
  const max = income;
  const bar = (v: number) => <Meter v={v / max} kind={v < 0 ? "bad" : undefined} thin />;

  return (
    <div className="grid" style={{ gridTemplateColumns: "minmax(0, 1fr) minmax(0, 1.4fr)" }}>
      <div className="col">
        <Panel title="Monthly budget" sub={`${fmtB(income * 12)}/yr`}>
          <Stat label="Income" value={fmtB(income, 2)} v={1} kind="good" />
          <Stat label="Upkeep" value={fmtB(upkeep, 2)} v={upkeep / max} hint="Crews, fuel, maintenance and training for every unit and base. Retire old units to cut it." />
          <Stat label="R&D" value={fmtB(rd, 2)} v={rd / max} />
          <Stat label="Development" value={fmtB(dev, 2)} v={dev / max} />
          <Stat label="Production" value={fmtB(prod, 2)} v={prod / max} />
          <div className="stat"><span><b>{surplus >= 0 ? "Surplus" : "Shortfall"}</b></span><b className={surplus < 0 ? "bad" : "good"}>{fmtB(surplus, 2)}</b>{bar(surplus)}</div>
          <div className="dim small">Reserve {fmtB(me.treasury, 2)}. If demand exceeds what you can pay, every project slows down by the same share. A deep deficit costs prestige.</div>
        </Panel>
        <Panel title="New order">
          <div className="slot">
            <label>Design</label>
            <select value={sel} onChange={(e) => setSel(e.target.value)}>
              {buildable.map((x) => <option key={x.id} value={x.id}>{SPECS[x.category].name} · {x.name}</option>)}
            </select>
          </div>
          <div className="slot"><label>Quantity</label><NumField value={qty} min={1} max={munition ? 5000 : 40} step={1} onChange={setQty} /></div>
          {!munition && <div className="slot"><label>Deliver to</label><select value={dest} onChange={(e) => setDest(e.target.value)}>{bases.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}</select></div>}
          {s && (
            <div className="small dim">
              {fmtM(s.cost)} each · total {fmtM(s.cost * qty)} ·{" "}
              {munition ? `about ${Math.round(1 / s.buildMonths)} per month when fully funded` : `${s.buildMonths} months each, built one after another`} · upkeep {fmtM(s.upkeep)}/mo per unit
            </div>
          )}
          <button className="btn primary" disabled={!d} onClick={() => commit(orderProduction(c, sel, qty, dest), "Order placed.")}>Place order</button>
        </Panel>
        <Panel title="Rebuild a base">
          <div className="dim small">Upgrade a base to one of your base designs: harder shelters, early-warning radars, point defence or missile silos. Design new ones in the Design Bureau.</div>
          <div className="slot"><label>Base</label><select value={bt} onChange={(e) => setBt(e.target.value)}>{bases.map((b) => <option key={b.id} value={b.id}>{b.name} ({c.designs[b.designId ?? ""]?.name ?? "basic"})</option>)}</select></div>
          <div className="slot"><label>Design</label><select value={bd} onChange={(e) => setBd(e.target.value)}>{baseDesigns.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</select></div>
          {bd && <div className="small dim">{fmtM(statsOf(c, bd)?.cost ?? 0)} · {statsOf(c, bd)?.buildMonths} months</div>}
          <button className="btn" disabled={!bd || !bt} onClick={() => commit(orderProduction(c, bd, 1, bt), "Base works ordered.")}>Order works</button>
        </Panel>
      </div>
      <Panel title="Orders" sub={`${me.orders.length} active`} flush>
        <table className="t">
          <thead><tr><th>Design</th><th className="num">Qty left</th><th style={{ width: 140 }}>Current unit</th><th className="num"><Hint title="Monthly cost" text="What this order costs per fully funded month.">$/mo</Hint></th><th>Destination</th><th /></tr></thead>
          <tbody>
            {me.orders.map((o) => {
              const od = c.designs[o.designId];
              const os = statsOf(c, o.designId);
              const monthly = os ? (os.cost / Math.max(0.001, o.monthsPerUnit)) * Math.min(1, o.qty * o.monthsPerUnit) : 0;
              return (
                <tr key={o.id}>
                  <td>{od?.name}</td>
                  <td className="num">{o.qty.toLocaleString()}</td>
                  <td>{o.monthsPerUnit >= 1 ? <Meter v={o.progress / o.monthsPerUnit} /> : <span className="dim small">{Math.round(1 / o.monthsPerUnit)}/mo</span>}</td>
                  <td className="num">{fmtM(monthly)}</td>
                  <td className="small dim">{o.destBaseId ? c.bases.find((b) => b.id === o.destBaseId)?.name : "stockpile"}</td>
                  <td><button className="btn small danger" onClick={() => commit(cancelOrder(c, o.id), "Order cancelled.")}>Cancel</button></td>
                </tr>
              );
            })}
          </tbody>
        </table>
        {!me.orders.length && <div className="empty">No orders. Pick an in-service design on the left.</div>}
        <div style={{ padding: 12 }}>
          <div className="label">In development</div>
          {Object.values(c.designs).filter((x) => x.nation === c.player && x.status === "developing").map((x) => {
            const xs = evalDesign(c, x).stats;
            return <Stat key={x.id} label={x.name} value={`${Math.ceil(x.devMonthsLeft)} mo left · ${fmtM(xs.devCost / Math.max(1, xs.devMonths))}/mo`} v={1 - x.devMonthsLeft / Math.max(1, xs.devMonths)} kind="friend" />;
          })}
        </div>
      </Panel>
    </div>
  );
}
