import { useEffect, useMemo, useState } from "react";
import { SPECS } from "../../data/parts";
import { TECH_BY_ID } from "../../data/techs";
import { blankDesign, deleteDraft, saveDesign, startDevelopment } from "../../sim/campaign/actions";
import { evalDesign, knownSet } from "../../sim/campaign/core";
import { evaluate } from "../../sim/design/stats";
import type { Campaign, Category, Design, Stats } from "../../sim/types";
import { CATEGORIES } from "../../sim/types";
import { fmtM, Hint, Modal, NumField, Panel, Stat, Tag } from "../components/common";
import { commit, getState, setState, toast } from "../store";

const STATUS_KIND: Record<string, string> = { service: "good", developing: "warn", draft: "dim", obsolete: "dim" };

interface Row { k: keyof Stats | "perAircraft" | "rate"; label: string; unit?: string; max?: number; hint?: string; fmt?: (v: number, s: Stats) => string; invert?: boolean }

const ROWS: Record<Category, Row[]> = {
  missile: [
    { k: "rangeKm", label: "Range", unit: "km", max: 2500, hint: "Maximum flight distance. Air-to-air missiles get a bonus from launch speed and altitude." },
    { k: "mach", label: "Speed", unit: "Mach", max: 12, hint: "Faster weapons leave defenders less time and are harder for guns and lasers to kill." },
    { k: "pk", label: "Hit probability", max: 1, fmt: (v) => `${Math.round(v * 100)}%`, hint: "Chance to hit once the weapon reaches its target, before decoys and interceptors." },
    { k: "warhead", label: "Warhead", unit: "kg", max: 1000 },
    { k: "rcs", label: "Radar signature", unit: "m²", max: 1, invert: true, hint: "Smaller is stealthier. Detection range scales with the fourth root of RCS." },
    { k: "softkillResist", label: "Decoy resistance", max: 1, fmt: (v) => `${Math.round(v * 100)}%`, hint: "Resistance to chaff, jammers and offboard decoys." },
    { k: "maneuver", label: "Terminal manoeuvre", max: 1, fmt: (v) => `${Math.round(v * 100)}%`, hint: "Makes the weapon harder to intercept." },
    { k: "interceptAir", label: "vs aircraft & cruise", max: 1, fmt: (v) => `${Math.round(v * 100)}%` },
    { k: "interceptBallistic", label: "vs ballistic", max: 1, fmt: (v) => `${Math.round(v * 100)}%` },
    { k: "interceptHyper", label: "vs hypersonic", max: 1, fmt: (v) => `${Math.round(v * 100)}%` },
    { k: "cost", label: "Unit cost", max: 30, fmt: (v) => fmtM(v), invert: true },
    { k: "rate", label: "Output per order", fmt: (v) => `${v.toFixed(0)}/mo`, hint: "Rounds one production order delivers per fully funded month." },
  ],
  aircraft: [
    { k: "radiusKm", label: "Combat radius", unit: "km", max: 4500 },
    { k: "mach", label: "Speed", unit: "Mach", max: 2.5 },
    { k: "rcs", label: "Radar signature", unit: "m²", max: 10, invert: true, hint: "Front-aspect RCS. 5th-gen fighters sit around 0.005 m² with internal weapons only." },
    { k: "radarKm", label: "Radar", unit: "km", max: 480, hint: "Detection range against a 1 m² target." },
    { k: "channels", label: "Fire channels", max: 8 },
    { k: "ew", label: "Self-protection", max: 0.5, fmt: (v) => `${Math.round(v * 100)}%` },
    { k: "hardpoints", label: "Hardpoint units", max: 48, hint: "Weapon capacity: small missiles use 1, tactical 2, strike 4, heavy 8." },
    { k: "perAircraft", label: "Cost per aircraft", fmt: (v) => fmtM(v) },
    { k: "cost", label: "Squadron cost", fmt: (v) => fmtM(v) },
    { k: "upkeep", label: "Upkeep", fmt: (v) => `${fmtM(v)}/mo` },
  ],
  ship: [
    { k: "speedKts", label: "Speed", unit: "kn", max: 40 },
    { k: "radarKm", label: "Radar", unit: "km", max: 480 },
    { k: "channels", label: "Fire channels", max: 24, hint: "Guided interceptors the ship can control at once. Saturation raids beat ships with few channels." },
    { k: "cells", label: "Missile cells", max: 160 },
    { k: "ciwsKm", label: "Point defence range", unit: "km", max: 20 },
    { k: "ew", label: "EW / decoys", max: 0.5, fmt: (v) => `${Math.round(v * 100)}%` },
    { k: "airWing", label: "Air wing", unit: "aircraft", max: 90 },
    { k: "rcs", label: "Radar signature", unit: "m²", max: 200000, invert: true },
    { k: "hp", label: "Damage capacity", max: 3500 },
    { k: "cost", label: "Unit cost", fmt: (v) => fmtM(v) },
    { k: "buildMonths", label: "Build time", unit: "months", max: 120, invert: true },
    { k: "upkeep", label: "Upkeep", fmt: (v) => `${fmtM(v)}/mo` },
  ],
  vehicle: [
    { k: "cells", label: "Launch tubes", max: 48 },
    { k: "radarKm", label: "Radar", unit: "km", max: 320 },
    { k: "channels", label: "Fire channels", max: 16 },
    { k: "mobility", label: "Mobility", max: 1, fmt: (v) => `${Math.round(v * 100)}%`, hint: "Chance to have moved before a pre-planned strike arrives." },
    { k: "ciwsKm", label: "Point defence", unit: "km", max: 12 },
    { k: "hp", label: "Toughness", max: 200 },
    { k: "cost", label: "Battery cost", fmt: (v) => fmtM(v) },
    { k: "upkeep", label: "Upkeep", fmt: (v) => `${fmtM(v)}/mo` },
  ],
  drone: [
    { k: "rangeKm", label: "Range", unit: "km", max: 2500 },
    { k: "radiusKm", label: "Radius", unit: "km", max: 2000 },
    { k: "mach", label: "Speed", unit: "Mach", max: 1 },
    { k: "warhead", label: "Warhead", unit: "kg", max: 300 },
    { k: "pk", label: "Hit probability", max: 1, fmt: (v) => `${Math.round(v * 100)}%` },
    { k: "radarKm", label: "Sensor", unit: "km", max: 200 },
    { k: "rcs", label: "Radar signature", unit: "m²", max: 1, invert: true },
    { k: "cost", label: "Cost", fmt: (v) => fmtM(v) },
    { k: "rate", label: "Output per order", fmt: (v) => `${v.toFixed(0)}/mo` },
  ],
  base: [
    { k: "hp", label: "Infrastructure", max: 12000, hint: "How much punishment the base absorbs before it's knocked out." },
    { k: "hardening", label: "Aircraft protection", max: 1, fmt: (v) => `${Math.round(v * 100)}%` },
    { k: "radarKm", label: "Early warning", unit: "km", max: 1100 },
    { k: "cells", label: "Silos / pads", max: 60 },
    { k: "ciwsKm", label: "Point defence", unit: "km", max: 12 },
    { k: "cost", label: "Build cost", fmt: (v) => fmtM(v) },
    { k: "buildMonths", label: "Build time", unit: "months", max: 40, invert: true },
  ],
};

function rowValue(r: Row, s: Stats, d: Design): number | undefined {
  if (r.k === "perAircraft") return s.cost / Math.max(1, d.params.sqn ?? 1);
  if (r.k === "rate") return s.buildMonths > 0 ? 1 / s.buildMonths : undefined;
  const v = s[r.k as keyof Stats];
  return typeof v === "number" ? v : undefined;
}

export function DesignScreen({ c }: { c: Campaign }) {
  const focus = getState().designFocus;
  const [cat, setCat] = useState<Category>(() => (focus && c.designs[focus]?.category) || "missile");
  const [work, setWork] = useState<Design | null>(() => (focus && c.designs[focus] ? structuredClone(c.designs[focus]) : null));
  const [compare, setCompare] = useState<string | null>(null);
  useEffect(() => { if (focus) setState({ designFocus: null }); }, [focus]);
  const known = useMemo(() => knownSet(c, c.player), [c]);
  const mine = Object.values(c.designs).filter((d) => d.nation === c.player && d.category === cat);
  const order = { developing: 0, draft: 1, service: 2, obsolete: 3 };
  mine.sort((a, b) => order[a.status] - order[b.status] || b.year - a.year || a.name.localeCompare(b.name));

  const open = (d: Design) => setWork(structuredClone(d));
  const saved = work?.id ? c.designs[work.id] : undefined;
  const editable = !saved || saved.status === "draft" || saved.status === "service";

  return (
    <div className="grid" style={{ gridTemplateColumns: "250px minmax(0, 1fr) 320px", height: "100%" }}>
      <Panel title="Designs" flush style={{ minHeight: 0 }}>
        <div style={{ padding: 10, display: "flex", flexDirection: "column", gap: 8 }}>
          <select value={cat} onChange={(e) => { setCat(e.target.value as Category); setWork(null); }} aria-label="Category">
            {CATEGORIES.map((k) => <option key={k} value={k}>{SPECS[k].plural}</option>)}
          </select>
          <button className="btn primary" onClick={() => setWork(blankDesign(c, cat))}>New {SPECS[cat].name.toLowerCase()}</button>
        </div>
        <div style={{ overflow: "auto", maxHeight: "calc(100vh - 230px)" }}>
          {mine.map((d) => (
            <button key={d.id} className={`nav-item ${work?.id === d.id ? "on" : ""}`} style={{ width: "100%", fontSize: 13, display: "grid", gridTemplateColumns: "1fr auto", gap: 2 }} onClick={() => open(d)}>
              <span style={{ color: "var(--ink)" }}>{d.name}</span>
              <Tag kind={STATUS_KIND[d.status]}>{d.status === "developing" ? `${Math.ceil(d.devMonthsLeft)} mo` : d.status}</Tag>
              <span className="tiny faint">{d.year} · {c.units.filter((u) => u.designId === d.id).length || ""}{c.units.some((u) => u.designId === d.id) ? " in service" : ""}</span>
            </button>
          ))}
          {!mine.length && <div className="empty">No {SPECS[cat].plural.toLowerCase()} yet.</div>}
        </div>
      </Panel>

      {work ? <Editor c={c} work={work} setWork={setWork} known={known} editable={editable} /> : (
        <Panel title="Design Bureau" style={{ gridColumn: "2 / 4" }}><div className="dim">Pick a design on the left to inspect or modify it, or start a new one. Editing an in-service design creates a new variant in the same family: it needs development before you can build it.</div></Panel>
      )}

      {work && <StatsPanel c={c} work={work} setWork={setWork} known={known} saved={saved} onCompare={() => setCompare("")} />}
      {compare !== null && work && <CompareModal c={c} a={work} bId={compare} setB={setCompare} onClose={() => setCompare(null)} />}
    </div>
  );
}

function Editor({ c, work, setWork, known, editable }: { c: Campaign; work: Design; setWork: (d: Design) => void; known: Set<string>; editable: boolean }) {
  const spec = SPECS[work.category];
  const set = (patch: Partial<Design>) => setWork({ ...work, ...patch });
  const munitions = Object.values(c.designs).filter((d) => d.nation === c.player && (d.category === "missile" || (d.category === "drone" && d.slots.type !== undefined && evalDesign(c, d).stats.role === "loiter")) && d.status !== "obsolete");
  return (
    <div className="col" style={{ minHeight: 0, overflow: "auto" }}>
      <Panel title={<input type="text" value={work.name} disabled={!editable} onChange={(e) => set({ name: e.target.value })} style={{ font: "700 15px var(--f-head)", letterSpacing: "0.05em", width: 360 }} aria-label="Design name" />} sub={`${spec.name} · ${work.year}`}>
        <Profile c={c} d={work} />
      </Panel>
      <Panel title="Components">
        {spec.slots.map((slot) => {
          const cur = slot.parts.find((p) => p.id === work.slots[slot.id]) ?? slot.parts[0];
          return (
            <div key={slot.id} className="slot">
              <label htmlFor={`slot-${slot.id}`}>{slot.name}</label>
              <select id={`slot-${slot.id}`} value={cur.id} disabled={!editable} onChange={(e) => set({ slots: { ...work.slots, [slot.id]: e.target.value } })}>
                {slot.parts.map((p) => {
                  const locked = !!p.tech && !known.has(p.tech);
                  return <option key={p.id} value={p.id} disabled={locked}>{p.name}{locked ? ` (needs ${TECH_BY_ID[p.tech!]?.name})` : ""}</option>;
                })}
              </select>
              <div className="partdesc">{cur.desc}</div>
            </div>
          );
        })}
      </Panel>
      <Panel title="Dimensions">
        {spec.params.map((p) => (
          <div key={p.id} className="param">
            <Hint title={p.name} text={p.hint}><label>{p.name}</label></Hint>
            <input type="range" min={p.min} max={p.max} step={p.step} value={work.params[p.id] ?? p.def} disabled={!editable} onChange={(e) => set({ params: { ...work.params, [p.id]: Number(e.target.value) } })} />
            <span className="row" style={{ gap: 4 }}>
              <NumField value={work.params[p.id] ?? p.def} min={p.min} max={p.max} step={p.step} width={76} onChange={(v) => set({ params: { ...work.params, [p.id]: v } })} />
              <span className="tiny dim">{p.unit}</span>
            </span>
          </div>
        ))}
      </Panel>
      {spec.hasLoadout && (
        <Panel title="Loadout" sub="per unit">
          {work.loadout.map((e, i) => (
            <div key={i} className="row">
              <select className="grow" value={e.designId} disabled={!editable} onChange={(ev) => { const l = [...work.loadout]; l[i] = { ...e, designId: ev.target.value }; set({ loadout: l }); }}>
                {munitions.map((m) => <option key={m.id} value={m.id}>{m.name} · {evalDesign(c, m).stats.role} · {evalDesign(c, m).stats.size}{m.status !== "service" ? ` (${m.status})` : ""}</option>)}
              </select>
              <NumField value={e.count} min={1} max={500} step={1} width={70} onChange={(v) => { const l = [...work.loadout]; l[i] = { ...e, count: v }; set({ loadout: l }); }} />
              <button className="btn small" disabled={!editable} onClick={() => set({ loadout: work.loadout.filter((_, j) => j !== i) })} aria-label="Remove">✕</button>
            </div>
          ))}
          {editable && munitions.length > 0 && <button className="btn small" onClick={() => set({ loadout: [...work.loadout, { designId: munitions[0].id, count: 4 }] })}>Add weapon</button>}
        </Panel>
      )}
    </div>
  );
}

function StatsPanel({ c, work, setWork, known, saved, onCompare }: { c: Campaign; work: Design; setWork: (d: Design | null) => void; known: Set<string>; saved?: Design; onCompare: () => void }) {
  const { stats, issues } = useMemo(() => evaluate(work, (id) => c.designs[id], known), [work, c, known]);
  const dirty = !saved || JSON.stringify([saved.name, saved.slots, saved.params, saved.loadout]) !== JSON.stringify([work.name, work.slots, work.params, work.loadout]);
  const errors = issues.filter((i) => i.sev === "error");
  const rows = ROWS[work.category].filter((r) => rowValue(r, stats, work) !== undefined && !(r.k.toString().startsWith("intercept") && !rowValue(r, stats, work)));

  const doSave = () => {
    const { c: nc, id } = saveDesign(c, work);
    commit(nc, saved && saved.status === "service" ? "Saved as a new variant (draft)." : "Draft saved.");
    setWork(structuredClone(nc.designs[id]));
    return { nc, id };
  };
  return (
    <div className="col" style={{ minHeight: 0, overflow: "auto" }}>
      <Panel title="Performance" sub={stats.role ? `${stats.role.toUpperCase()} · size ${stats.size}` : stats.maxSize ? `max weapon ${stats.maxSize}` : undefined}>
        {rows.map((r) => {
          const v = rowValue(r, stats, work)!;
          const txt = r.fmt ? r.fmt(v, stats) : `${v.toLocaleString(undefined, { maximumFractionDigits: 2 })}${r.unit ? ` ${r.unit}` : ""}`;
          const m = r.max ? (r.invert ? 1 - Math.min(1, v / r.max) : v / r.max) : undefined;
          return <Stat key={String(r.k)} label={r.label} value={txt} v={m} hint={r.hint} />;
        })}
        {stats.weightMax !== undefined && <Stat label="Weight used" value={`${stats.weight?.toLocaleString()} / ${stats.weightMax.toLocaleString()} t`} v={(stats.weight ?? 0) / stats.weightMax} kind={(stats.weight ?? 0) > stats.weightMax ? "bad" : undefined} hint="Payload weight against the hull's usable margin. Keep 5% spare for future refits." />}
        {stats.powerMw !== undefined && <Stat label="Power" value={`${stats.powerUse} / ${stats.powerMw} MW`} v={(stats.powerUse ?? 0) / stats.powerMw} kind={(stats.powerUse ?? 0) > stats.powerMw ? "bad" : undefined} hint="Radars, lasers and railguns draw electrical power. Integrated electric propulsion gives the most." />}
        {stats.loadUsed !== undefined && <Stat label="Loadout" value={`${stats.loadUsed} / ${(stats.cells ?? stats.hardpoints) ?? 0}`} v={stats.loadUsed / Math.max(1, (stats.cells ?? stats.hardpoints) ?? 1)} kind={stats.loadUsed > ((stats.cells ?? stats.hardpoints) ?? 0) ? "bad" : undefined} />}
      </Panel>
      <Panel title="Programme">
        <Stat label="Development" value={stats.devMonths ? `${fmtM(stats.devCost)} · ${stats.devMonths} mo` : "none needed"} hint="Paid monthly while the design is in development. Starts when you press Start development." />
        {work.category !== "missile" && !(work.category === "drone" && stats.role === "loiter") && <Stat label="Build time" value={`${stats.buildMonths} months`} />}
        {issues.map((i, k) => <div key={k} className={`issue ${i.sev}`}>{i.text}</div>)}
        {!issues.length && <div className="issue info">No problems found.</div>}
        <div className="row wrap">
          <button className="btn" disabled={!dirty || saved?.status === "developing"} onClick={doSave}>{saved?.status === "service" ? "Save as variant" : "Save draft"}</button>
          <button className="btn primary" disabled={errors.length > 0 || saved?.status === "developing" || (saved?.status === "service" && !dirty)} onClick={() => {
            let base = c, id = work.id;
            if (dirty || !saved) { const r = saveDesign(c, work); base = r.c; id = r.id; }
            const r = startDevelopment(base, id);
            if (typeof r === "string") { toast(r); return; }
            commit(r, stats.devMonths ? `Development started: ${stats.devMonths} months.` : "Design approved.");
            setWork(structuredClone(r.designs[id]));
          }}>Start development</button>
          <button className="btn" onClick={onCompare}>Compare…</button>
          {saved?.status === "draft" && <button className="btn danger" onClick={() => { if (commit(deleteDraft(c, saved.id), "Draft deleted.")) setWork(null); }}>Delete</button>}
        </div>
      </Panel>
    </div>
  );
}

function CompareModal({ c, a, bId, setB, onClose }: { c: Campaign; a: Design; bId: string; setB: (id: string) => void; onClose: () => void }) {
  const others = Object.values(c.designs).filter((d) => d.nation === c.player && d.category === a.category && d.id !== a.id);
  const b = bId ? c.designs[bId] : others[0];
  const sa = evaluate(a, (id) => c.designs[id]).stats;
  const sb = b ? evaluate(b, (id) => c.designs[id]).stats : undefined;
  return (
    <Modal title="Compare designs" onClose={onClose} wide>
      <div className="row"><span className="dim">Compare</span><b>{a.name}</b><span className="dim">with</span>
        <select value={b?.id ?? ""} onChange={(e) => setB(e.target.value)}>{others.map((d) => <option key={d.id} value={d.id}>{d.name} ({d.status})</option>)}</select>
      </div>
      {b && sb && (
        <table className="t">
          <thead><tr><th>Stat</th><th className="num">{a.name}</th><th className="num">{b.name}</th><th className="num">Difference</th></tr></thead>
          <tbody>
            {ROWS[a.category].map((r) => {
              const va = rowValue(r, sa, a), vb = rowValue(r, sb, b);
              if (va === undefined && vb === undefined) return null;
              const f = (v?: number, s?: Stats) => (v === undefined ? "—" : r.fmt && s ? r.fmt(v, s) : `${v.toLocaleString(undefined, { maximumFractionDigits: 2 })}${r.unit ? ` ${r.unit}` : ""}`);
              const diff = va !== undefined && vb !== undefined && vb !== 0 ? (va - vb) / Math.abs(vb) : undefined;
              const better = diff === undefined || Math.abs(diff) < 0.005 ? 0 : (diff > 0) !== !!r.invert && !["cost", "upkeep", "perAircraft", "buildMonths"].includes(String(r.k)) ? 1 : ["cost", "upkeep", "perAircraft", "buildMonths"].includes(String(r.k)) ? (diff < 0 ? 1 : -1) : -1;
              return (
                <tr key={String(r.k)}>
                  <td>{r.label}</td><td className="num">{f(va, sa)}</td><td className="num">{f(vb, sb)}</td>
                  <td className={`num ${better > 0 ? "good" : better < 0 ? "bad" : "dim"}`}>{diff === undefined ? "" : `${diff > 0 ? "+" : ""}${Math.round(diff * 100)}%`}</td>
                </tr>
              );
            })}
            {[...new Set([...Object.keys(a.slots), ...Object.keys(b.slots)])].map((k) => {
              const slot = SPECS[a.category].slots.find((s) => s.id === k);
              const pa = slot?.parts.find((p) => p.id === a.slots[k])?.name, pb = slot?.parts.find((p) => p.id === b.slots[k])?.name;
              return <tr key={k}><td className="dim">{slot?.name}</td><td className="num small">{pa}</td><td className="num small">{pb}</td><td className="num warn small">{pa !== pb ? "differs" : ""}</td></tr>;
            })}
          </tbody>
        </table>
      )}
      {!b && <div className="dim">No other {SPECS[a.category].plural.toLowerCase()} to compare with.</div>}
    </Modal>
  );
}

// ---------------------------------------------------------------- procedural profile drawings
function Profile({ c, d }: { c: Campaign; d: Design }) {
  const s = evaluate(d, (id) => c.designs[id]).stats;
  const ink = "var(--ink)", acc = "var(--accent)", dim = "#3a4048";
  const W = 640, H = 130;
  let body: React.ReactNode = null;
  if (d.category === "ship") {
    const disp = d.params.disp ?? 5000;
    const len = Math.min(600, 120 + Math.pow(disp, 0.42) * 6);
    const x0 = (W - len) / 2, wl = 92;
    if (s.sub) {
      body = <>
        <line x1={0} y1={wl - 20} x2={W} y2={wl - 20} stroke="var(--friend)" strokeDasharray="6 5" opacity={0.5} />
        <rect x={x0} y={wl - 14} width={len} height={26} rx={13} fill="#2a2f35" stroke={ink} />
        <rect x={x0 + len * 0.55} y={wl - 30} width={len * 0.08} height={18} rx={3} fill="#2a2f35" stroke={ink} />
        <rect x={x0 + len * 0.15} y={wl - 10} width={Math.min(len * 0.3, (d.params.cells ?? 0) * 3)} height={5} fill={acc} />
      </>;
    } else {
      const deck = SPECS.ship.slots[0].parts.find((p) => p.id === d.slots.hull)?.a.deck;
      const flat = deck === "catobar" || deck === "stovl";
      const cellsW = Math.min(len * 0.35, (d.params.cells ?? 0) * 1.6);
      body = <>
        <line x1={0} y1={wl} x2={W} y2={wl} stroke="var(--friend)" strokeDasharray="6 5" opacity={0.5} />
        <path d={`M${x0},${wl - 22} L${x0 + len},${wl - 30} L${x0 + len - 22},${wl + 12} L${x0 + 18},${wl + 12} Z`} fill="#2a2f35" stroke={ink} />
        {flat ? <>
          <rect x={x0 - 6} y={wl - 40} width={len + 10} height={10} fill="#2a2f35" stroke={ink} />
          <rect x={x0 + len * 0.62} y={wl - 66} width={len * 0.1} height={26} fill="#343a42" stroke={ink} />
          {Array.from({ length: Math.min(14, Math.round((d.params.wing ?? 0) / 5)) }).map((_, i) => <path key={i} d={`M${x0 + 20 + i * (len * 0.55 / 14)},${wl - 44} l8,-4 l8,4 z`} fill={acc} />)}
        </> : <>
          <rect x={x0 + len * 0.4} y={wl - 52} width={len * 0.18} height={24} fill="#343a42" stroke={ink} />
          <path d={`M${x0 + len * 0.47},${wl - 52} l4,-26 l6,0 l4,26 z`} fill="#343a42" stroke={ink} />
          {s.radarKm && s.radarKm >= 300 && <rect x={x0 + len * 0.43} y={wl - 48} width={8} height={12} fill="var(--friend)" />}
          <rect x={x0 + len * 0.7 - cellsW / 2} y={wl - 32} width={cellsW} height={5} fill={acc} />
          {s.ciwsKm ? <circle cx={x0 + len * 0.62} cy={wl - 56} r={4} fill="var(--good)" /> : null}
          {(d.params.wing ?? 0) > 0 && <rect x={x0 + 22} y={wl - 34} width={len * 0.14} height={10} fill="#343a42" stroke={ink} />}
        </>}
        <text x={x0} y={H - 4} fill="var(--dim)" fontSize={10} fontFamily="var(--f-mono)">{Math.round(len / 3.2)} m · {disp.toLocaleString()} t</text>
      </>;
    }
  } else if (d.category === "missile") {
    const w = d.params.weight ?? 500;
    const len = Math.min(560, 120 + Math.pow(w, 0.5) * 3.5);
    const r = Math.min(20, 4 + Math.pow(w, 0.33) * 0.9);
    const x0 = (W - len) / 2, y = H / 2;
    const col = s.role === "sam" || s.role === "aam" || s.role === "bmd" ? "var(--friend)" : s.role === "torp" ? "var(--good)" : acc;
    body = <>
      <path d={`M${x0},${y - r} L${x0 + len - r * 3},${y - r} Q${x0 + len},${y} ${x0 + len - r * 3},${y + r} L${x0},${y + r} Z`} fill="#2a2f35" stroke={ink} />
      <rect x={x0 + len - r * 3 - (d.params.warhead ?? 0) / 8} y={y - r + 2} width={Math.max(4, (d.params.warhead ?? 0) / 8)} height={r * 2 - 4} fill={col} opacity={0.8} />
      <path d={`M${x0 + 4},${y - r} l18,-${r + 10} l10,0 l-6,${r + 10} z M${x0 + 4},${y + r} l18,${r + 10} l10,0 l-6,-${r + 10} z`} fill="#343a42" stroke={ink} />
      {s.alt !== undefined && s.alt > 20000 && <path d={`M40,${H - 10} Q${W / 2},-30 ${W - 40},${H - 10}`} fill="none" stroke={dim} strokeDasharray="4 4" />}
      <text x={x0} y={H - 4} fill="var(--dim)" fontSize={10} fontFamily="var(--f-mono)">{w.toLocaleString()} kg · {s.rangeKm} km · Mach {s.mach}</text>
    </>;
  } else if (d.category === "aircraft") {
    const f = d.slots.frame;
    const big = f === "large" || f === "bomber" || f === "lobomber";
    const cx = W / 2, cy = H / 2;
    body = f === "lobomber"
      ? <path d={`M${cx - 200},${cy + 20} L${cx},${cy - 40} L${cx + 200},${cy + 20} L${cx + 120},${cy + 30} L${cx + 60},${cy + 10} L${cx},${cy + 30} L${cx - 60},${cy + 10} L${cx - 120},${cy + 30} Z`} fill="#2a2f35" stroke={ink} />
      : big ? <>
        <rect x={cx - 170} y={cy - 9} width={340} height={18} rx={9} fill="#2a2f35" stroke={ink} />
        <path d={`M${cx - 20},${cy - 8} L${cx - 70},${cy - 62} L${cx - 50},${cy - 62} L${cx + 30},${cy - 8} Z M${cx - 20},${cy + 8} L${cx - 70},${cy + 62} L${cx - 50},${cy + 62} L${cx + 30},${cy + 8} Z`} fill="#343a42" stroke={ink} />
        {s.aew && <ellipse cx={cx - 40} cy={cy - 20} rx={40} ry={7} fill="var(--friend)" opacity={0.7} />}
      </> : <>
        <path d={`M${cx + 150},${cy} L${cx + 60},${cy - 10} L${cx - 20},${cy - (f === "g5" || f === "g6" ? 70 : 55)} L${cx - 50},${cy - 55} L${cx - 30},${cy - 12} L${cx - 110},${cy - 10} L${cx - 130},${cy - (f === "g6" ? 10 : 34)} L${cx - 145},${cy - 30} L${cx - 140},${cy} L${cx - 145},${cy + 30} L${cx - 130},${cy + (f === "g6" ? 10 : 34)} L${cx - 110},${cy + 10} L${cx - 30},${cy + 12} L${cx - 50},${cy + 55} L${cx - 20},${cy + (f === "g5" || f === "g6" ? 70 : 55)} L${cx + 60},${cy + 10} Z`} fill="#2a2f35" stroke={ink} />
        {Array.from({ length: Math.min(8, Math.floor((s.hardpoints ?? 0) / 2)) }).map((_, i) => <rect key={i} x={cx - 40 + (i % 4) * 14} y={cy + (i < 4 ? -30 : 22)} width={10} height={4} fill={acc} />)}
      </>;
  } else if (d.category === "vehicle") {
    const n = Math.min(6, d.params.launchers ?? 1);
    body = <>
      {Array.from({ length: n }).map((_, i) => {
        const x = 30 + i * 100;
        return <g key={i}>
          <rect x={x} y={70} width={84} height={22} fill="#2a2f35" stroke={ink} />
          <rect x={x + 58} y={56} width={26} height={16} fill="#343a42" stroke={ink} />
          <rect x={x + 4} y={48} width={52} height={20} fill={acc} opacity={0.75} transform={`rotate(-${d.slots.role === "sam" ? 35 : 10} ${x + 4} 68)`} />
          <circle cx={x + 14} cy={96} r={6} fill="#111" stroke={ink} /><circle cx={x + 42} cy={96} r={6} fill="#111" stroke={ink} /><circle cx={x + 70} cy={96} r={6} fill="#111" stroke={ink} />
        </g>;
      })}
    </>;
  } else if (d.category === "drone") {
    const cx = W / 2, cy = H / 2;
    body = d.slots.type === "owa" || d.slots.type === "cca"
      ? <path d={`M${cx + 90},${cy} L${cx - 60},${cy - 70} L${cx - 70},${cy - 60} L${cx - 50},${cy} L${cx - 70},${cy + 60} L${cx - 60},${cy + 70} Z`} fill="#2a2f35" stroke={ink} />
      : d.slots.type === "usv"
        ? <path d={`M${cx - 120},${cy + 10} L${cx + 120},${cy + 10} L${cx + 90},${cy + 30} L${cx - 110},${cy + 30} Z`} fill="#2a2f35" stroke={ink} />
        : <><rect x={cx - 100} y={cy - 7} width={200} height={14} rx={7} fill="#2a2f35" stroke={ink} /><rect x={cx - 20} y={cy - 60} width={24} height={120} fill="#343a42" stroke={ink} /></>;
  } else {
    body = <>
      <rect x={60} y={50} width={520} height={16} fill="#2a2f35" stroke={ink} />
      {Array.from({ length: Math.min(12, Math.round((d.params.capacity ?? 20) / 8)) }).map((_, i) => <path key={i} d={`M${80 + i * 42},96 q16,-24 32,0 z`} fill={d.slots.hard === "none" ? "#2a2f35" : "#343a42"} stroke={ink} />)}
      {(d.params.pads ?? 0) > 0 && Array.from({ length: Math.min(12, d.params.pads ?? 0) }).map((_, i) => <circle key={i} cx={80 + i * 42} cy={30} r={6} fill={acc} />)}
    </>;
  }
  return <div className="profile"><svg viewBox={`0 0 ${W} ${H}`} role="img" aria-label={`${d.name} profile`}>{body}</svg></div>;
}

