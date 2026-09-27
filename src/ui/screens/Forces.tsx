import { useMemo, useState } from "react";
import { THEATRES, THEATRE_BY_ID } from "../../data/nations";
import { decommission, embarkOn, moveUnit } from "../../sim/campaign/actions";
import { statsOf } from "../../sim/campaign/core";
import type { Campaign, Category, Unit } from "../../sim/types";
import { fmtM, Meter, Panel, Seg, Stat, Tag } from "../components/common";
import { commit, setState } from "../store";

type Filter = "all" | Category;

export function locText(c: Campaign, u: Unit): string {
  const l = u.location;
  if (u.embarkedOn) {
    const cv = c.units.find((x) => x.id === u.embarkedOn);
    return `aboard ${cv?.name ?? "carrier"}`;
  }
  if (l.kind === "base") return c.bases.find((b) => b.id === l.baseId)?.name ?? "?";
  if (l.kind === "sea") return `At sea: ${THEATRE_BY_ID[l.theatreId]?.name}`;
  const to = l.to.kind === "base" ? c.bases.find((b) => b.id === (l.to as { baseId: string }).baseId)?.name : THEATRE_BY_ID[(l.to as { theatreId: string }).theatreId]?.name;
  return `Transit → ${to} (${l.monthsLeft} mo)`;
}

export function ForcesScreen({ c }: { c: Campaign }) {
  const [filter, setFilter] = useState<Filter>("all");
  const [sel, setSel] = useState<string | null>(null);
  const [q, setQ] = useState("");
  const mine = useMemo(() => c.units.filter((u) => u.nation === c.player), [c]);
  const list = mine
    .filter((u) => filter === "all" || c.designs[u.designId]?.category === filter)
    .filter((u) => !q || `${u.name} ${c.designs[u.designId]?.name} ${locText(c, u)}`.toLowerCase().includes(q.toLowerCase()))
    .sort((a, b) => locText(c, a).localeCompare(locText(c, b)) || a.name.localeCompare(b.name));
  const u = sel ? c.units.find((x) => x.id === sel) : undefined;
  const stock = Object.entries(c.nations[c.player].stock).filter(([, k]) => k > 0).sort((a, b) => (c.designs[a[0]]?.name ?? "").localeCompare(c.designs[b[0]]?.name ?? ""));

  return (
    <div className="grid" style={{ gridTemplateColumns: "minmax(0, 1.5fr) minmax(320px, 1fr)", height: "100%" }}>
      <Panel
        title="Forces"
        sub={`${list.length} of ${mine.length}`}
        flush
        style={{ minHeight: 0 }}
        actions={<>
          <Seg<Filter> value={filter} onChange={setFilter} options={[["all", "All"], ["ship", "Ships"], ["aircraft", "Aircraft"], ["vehicle", "Batteries"], ["drone", "Drones"]]} />
          <input type="search" placeholder="Filter" value={q} onChange={(e) => setQ(e.target.value)} style={{ width: 150 }} />
        </>}
      >
        <div style={{ overflow: "auto", maxHeight: "calc(100vh - 160px)" }}>
          <table className="t">
            <thead><tr><th>Name</th><th>Class</th><th>Location</th><th style={{ width: 90 }}>Readiness</th><th style={{ width: 80 }}>Damage</th></tr></thead>
            <tbody>
              {list.map((x) => {
                const d = c.designs[x.designId];
                return (
                  <tr key={x.id} className={`click ${sel === x.id ? "sel" : ""}`} onClick={() => setSel(x.id)}>
                    <td>{x.name}</td>
                    <td className="dim">{d?.name}</td>
                    <td className="small">{locText(c, x)}</td>
                    <td><Meter v={x.readiness} kind={x.readiness < 0.6 ? "bad" : "good"} thin /></td>
                    <td>{x.damage > 0 ? <Meter v={x.damage} kind="bad" thin /> : <span className="faint small">none</span>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          {!list.length && <div className="empty">No units match.</div>}
        </div>
      </Panel>
      <div className="col" style={{ minHeight: 0 }}>
        {u ? <UnitDetail c={c} u={u} onGone={() => setSel(null)} /> : <Panel title="Unit"><div className="dim">Select a unit to see its details and give orders.</div></Panel>}
        <Panel title="Munitions stockpile" sub={`${stock.reduce((a, [, k]) => a + k, 0).toLocaleString()} rounds`} scroll style={{ maxHeight: 360 }}>
          <table className="t">
            <tbody>
              {stock.map(([id, k]) => {
                const s = statsOf(c, id);
                return <tr key={id}><td>{c.designs[id]?.name ?? id}</td><td><Tag kind="dim">{s?.role}</Tag></td><td className="num">{k.toLocaleString()}</td></tr>;
              })}
            </tbody>
          </table>
        </Panel>
      </div>
    </div>
  );
}

function UnitDetail({ c, u, onGone }: { c: Campaign; u: Unit; onGone: () => void }) {
  const d = c.designs[u.designId];
  const s = statsOf(c, u.designId);
  const [dest, setDest] = useState("");
  const [confirm, setConfirm] = useState(false);
  if (!d || !s) return null;
  const myBases = c.bases.filter((b) => b.nation === c.player);
  const carriers = c.units.filter((x) => x.nation === c.player && (statsOf(c, x.designId)?.airWing ?? 0) > 0);
  const embarked = c.units.filter((x) => x.embarkedOn === u.id);
  const carrierCapable = !!s.carrier && s.carrier !== "none" && d.category !== "ship";
  const loadout = d.loadout.map((e) => `${e.count}× ${c.designs[e.designId]?.name ?? "?"}`).join(", ");
  return (
    <Panel title={u.name} sub={d.name}>
      <Stat label="Location" value={locText(c, u)} />
      <Stat label="Readiness" value={`${Math.round(u.readiness * 100)}%`} v={u.readiness} kind="good" />
      <Stat label="Damage" value={`${Math.round(u.damage * 100)}%`} v={u.damage} kind="bad" />
      <Stat label="Upkeep" value={`${fmtM(s.upkeep)}/mo`} />
      {s.speedKts !== undefined && <Stat label="Speed" value={`${s.speedKts} kn`} />}
      {s.radarKm ? <Stat label="Radar" value={`${s.radarKm} km`} hint="Detection range against a 1 m² target. Range scales with the fourth root of the target's radar cross-section, and low fliers are hidden by the horizon." /> : null}
      {loadout && <div className="small"><span className="label">Loadout</span><br />{loadout}</div>}
      {embarked.length > 0 && <div className="small"><span className="label">Air wing</span><br />{embarked.map((x) => x.name).join(", ")}</div>}
      <div className="row wrap">
        <select value={dest} onChange={(e) => setDest(e.target.value)} aria-label="Destination">
          <option value="">Move to…</option>
          <optgroup label="Bases">{myBases.map((b) => <option key={b.id} value={`base:${b.id}`}>{b.name}</option>)}</optgroup>
          {d.category === "ship" && <optgroup label="Deploy to sea">{THEATRES.map((t) => <option key={t.id} value={`sea:${t.id}`}>{t.name}</option>)}</optgroup>}
        </select>
        <button className="btn" disabled={!dest} onClick={() => {
          const [k, id] = dest.split(":");
          commit(moveUnit(c, u.id, k === "base" ? { kind: "base", baseId: id } : { kind: "sea", theatreId: id }), "Orders sent.");
        }}>Move</button>
      </div>
      {carrierCapable && (
        <div className="row wrap">
          {u.embarkedOn
            ? <button className="btn" onClick={() => commit(embarkOn(c, u.id, null), "Squadron disembarked.")}>Disembark</button>
            : carriers.map((cv) => <button key={cv.id} className="btn small" onClick={() => commit(embarkOn(c, u.id, cv.id), `Embarked on ${cv.name}.`)}>Embark on {cv.name}</button>)}
        </div>
      )}
      <div className="row wrap">
        <button className="btn small" onClick={() => setState({ screen: "design", designFocus: d.id })}>Open design</button>
        {!confirm ? <button className="btn small danger" onClick={() => setConfirm(true)}>Decommission…</button> : (
          <>
            <span className="small bad">Scrap {u.name}? Saves {fmtM(s.upkeep)}/mo.</span>
            <button className="btn small danger" onClick={() => { if (commit(decommission(c, u.id), `${u.name} decommissioned.`)) onGone(); }}>Confirm</button>
            <button className="btn small" onClick={() => setConfirm(false)}>Keep</button>
          </>
        )}
      </div>
    </Panel>
  );
}
