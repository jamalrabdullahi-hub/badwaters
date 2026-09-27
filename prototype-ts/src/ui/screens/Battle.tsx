import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { Feature, FeatureCollection, MultiPolygon, Polygon } from "geojson";
import { NATIONS } from "../../data/nations";

import { removePending } from "../../sim/campaign/actions";
import { manualLaunch, runToEnd, setWaypoint, step, type BattleState, type BUnit, type Side } from "../../sim/battle/engine";
import { landFine, makeProj } from "../../sim/battle/geo";
import { applyBattle } from "../../sim/battle/results";
import { Meter, Seg } from "../components/common";
import { commit, getState, setState, toast, useApp } from "../store";

type Mode = "select" | "fire";
const SPEEDS: [number, string][] = [[0, "❚❚"], [1, "×1"], [10, "×10"], [60, "×60"], [300, "×300"]];

function fmtT(t: number) {
  const h = Math.floor(t / 3600), m = Math.floor((t % 3600) / 60), s = Math.floor(t % 60);
  return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
}

/** Land rings near the battle, pre-projected to local km. */
function useLandKm(b: BattleState) {
  return useMemo(() => {
    const proj = makeProj(b.lat0, b.lon0);
    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    for (const u of b.units) { minX = Math.min(minX, u.x); maxX = Math.max(maxX, u.x); minY = Math.min(minY, u.y); maxY = Math.max(maxY, u.y); }
    const pad = 800;
    const [la0, lo0] = proj.toLatLon(minX - pad, minY - pad);
    const [la1, lo1] = proj.toLatLon(maxX + pad, maxY + pad);
    const f = landFine() as Feature<Polygon | MultiPolygon> | FeatureCollection<Polygon | MultiPolygon>;
    const geoms = "features" in f ? f.features.map((x) => x.geometry) : [f.geometry];
    const rings: Float32Array[] = [];
    for (const g of geoms) {
      const polys = g.type === "Polygon" ? [g.coordinates] : g.coordinates;
      for (const poly of polys) for (const ring of poly) {
        let inside = false;
        for (const [lon, lat] of ring) if (lat >= la0 && lat <= la1 && lon >= lo0 && lon <= lo1) { inside = true; break; }
        if (!inside) continue;
        const arr = new Float32Array(ring.length * 2);
        ring.forEach(([lon, lat], i) => { const [x, y] = proj.toXY(lat, lon); arr[i * 2] = x; arr[i * 2 + 1] = y; });
        rings.push(arr);
      }
    }
    return { rings, bbox: { minX, maxX, minY, maxY } };
  }, [b]);
}

export function BattleView() {
  const bs = useApp((s) => s.battle)!;
  const b = bs.state;
  const me: Side | -1 = b.playerSide;
  const cvs = useRef<HTMLCanvasElement>(null);
  const view = useRef({ cx: 0, cy: 0, scale: 1 });
  const [speed, setSpeed] = useState(10);
  const speedRef = useRef(speed);
  speedRef.current = speed;
  const [sel, setSel] = useState<string | null>(null);
  const selRef = useRef<string | null>(null);
  selRef.current = sel;
  const [weapon, setWeapon] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(4);
  const [mode, setMode] = useState<Mode>("select");
  const [, setTick] = useState(0);
  const land = useLandKm(b);

  // fit view on open
  useEffect(() => {
    const el = cvs.current;
    if (!el) return;
    const { minX, maxX, minY, maxY } = land.bbox;
    const w = el.clientWidth, h = el.clientHeight;
    view.current = { cx: (minX + maxX) / 2, cy: (minY + maxY) / 2, scale: Math.min(w / Math.max(200, (maxX - minX) * 1.25), h / Math.max(200, (maxY - minY) * 1.25)) };
  }, [land]);

  const enemyColor = useCallback((u: { side: Side; nation: string }) => {
    if (me === -1) return u.side === 0 ? "#5ba8e4" : "#e5484d";
    return u.side === me ? getComputedStyle(document.documentElement).getPropertyValue("--friend").trim() || "#5ba8e4" : getComputedStyle(document.documentElement).getPropertyValue("--hostile").trim() || "#e5484d";
  }, [me]);

  // simulation + render loop
  useEffect(() => {
    let raf = 0, last = performance.now(), acc = 0, lastUi = 0;
    const colors = { friend: enemyColor({ side: me === -1 ? 0 : me, nation: "" }), hostile: enemyColor({ side: me === 1 ? 0 : 1, nation: "" }) };
    const loop = (now: number) => {
      const dtReal = Math.min(0.25, (now - last) / 1000);
      last = now;
      if (!b.over && speedRef.current > 0) {
        acc += dtReal * speedRef.current;
        let n = 0;
        while (acc >= 1 && n < 400) { step(b, 1); acc -= 1; n++; }
        if (n >= 400) acc = 0;
      }
      draw(cvs.current, b, view.current, land.rings, selRef.current, colors);
      if (now - lastUi > 200) { lastUi = now; setTick((t) => t + 1); }
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(raf);
  }, [b, land, me, enemyColor]);

  useEffect(() => {
    const k = (e: KeyboardEvent) => {
      if (e.code === "Space") { e.preventDefault(); setSpeed((s) => (s === 0 ? 10 : 0)); }
      if (e.key === "Escape") { setMode("select"); setWeapon(null); }
    };
    window.addEventListener("keydown", k);
    return () => window.removeEventListener("keydown", k);
  }, []);

  const toKm = (ev: { clientX: number; clientY: number }): [number, number] => {
    const el = cvs.current!;
    const r = el.getBoundingClientRect();
    const zoom = Number((document.getElementById("root") as HTMLElement).style.zoom || 1);
    const sx = (ev.clientX / zoom - r.left) , sy = (ev.clientY / zoom - r.top);
    const v = view.current;
    return [v.cx + (sx - r.width / 2) / v.scale, v.cy - (sy - r.height / 2) / v.scale];
  };
  const pick = (x: number, y: number): BUnit | undefined => {
    const v = view.current;
    let best: BUnit | undefined, bd = 14 / v.scale;
    for (const u of b.units) {
      if (!u.alive || u.landed) continue;
      if (me !== -1 && u.side !== me && !b.tracks[me].has(u.id)) continue;
      const d = Math.hypot(u.x - x, u.y - y);
      if (d < bd) { bd = d; best = u; }
    }
    return best;
  };

  const drag = useRef<{ x: number; y: number; cx: number; cy: number; moved: boolean } | null>(null);
  const onDown = (e: React.MouseEvent) => {
    if (e.button !== 0) return;
    drag.current = { x: e.clientX, y: e.clientY, cx: view.current.cx, cy: view.current.cy, moved: false };
  };
  const onMove = (e: React.MouseEvent) => {
    const d = drag.current;
    if (!d) return;
    const dx = e.clientX - d.x, dy = e.clientY - d.y;
    if (Math.abs(dx) + Math.abs(dy) > 4) d.moved = true;
    view.current.cx = d.cx - dx / view.current.scale;
    view.current.cy = d.cy + dy / view.current.scale;
  };
  const onUp = (e: React.MouseEvent) => {
    const d = drag.current;
    drag.current = null;
    if (!d || d.moved || e.button !== 0) return;
    const [x, y] = toKm(e);
    const u = pick(x, y);
    if (mode === "fire" && sel && weapon) {
      if (!u || u.side === me) { toast("Click an enemy track to fire at."); return; }
      const err = manualLaunch(b, sel, weapon, u.id, salvo);
      if (err) toast(err);
      else { setMode("select"); }
      return;
    }
    setSel(u && (me === -1 || u.side === me) ? u.id : u ? u.id : null);
    setWeapon(null);
  };
  const onContext = (e: React.MouseEvent) => {
    e.preventDefault();
    if (!sel) return;
    const u = b.units.find((x) => x.id === sel);
    if (!u || (me !== -1 && u.side !== me)) return;
    const [x, y] = toKm(e);
    setWaypoint(b, sel, x, y);
  };
  const onWheel = (e: React.WheelEvent) => {
    const [x, y] = toKm(e);
    const v = view.current;
    const f = e.deltaY < 0 ? 1.2 : 1 / 1.2;
    v.scale = Math.max(0.02, Math.min(40, v.scale * f));
    const [x2, y2] = toKm(e);
    v.cx += x - x2; v.cy += y - y2;
  };

  const finish = () => {
    const c = getState().campaign;
    if (!c) { setState({ battle: null }); return; }
    if (!b.over) runToEnd(b, 2);
    const nc = removePending(c, bs.pending.id);
    applyBattle(nc, b);
    commit(nc);
    setState({ battle: null, screen: "command" });
  };

  const su = sel ? b.units.find((u) => u.id === sel) : undefined;
  const mine = me === -1 ? b.units.filter((u) => u.side === 0) : b.units.filter((u) => u.side === me);
  const theirs = me === -1 ? b.units.filter((u) => u.side === 1) : b.units.filter((u) => u.side !== me);
  const known = me === -1 ? theirs : theirs.filter((u) => b.tracks[me].has(u.id));
  const inbound = me === -1 ? 0 : b.missiles.filter((m) => m.alive && m.side !== me && b.tracks[me].has(m.id) && m.role !== "sam" && m.role !== "aam" && m.role !== "bmd").length;
  const doc = me === -1 ? b.doctrine[0] : b.doctrine[me];
  const myN = me === -1 ? b.nations[0] : b.nations[me];
  const theirN = me === -1 ? b.nations[1] : b.nations[me === 0 ? 1 : 0];
  const winnerName = b.winner === -1 ? null : NATIONS[b.nations[b.winner]].name;

  return (
    <div className="battle">
      <div className="bar">
        <b style={{ fontFamily: "var(--f-head)", fontSize: 17, letterSpacing: "0.05em" }}>{b.title}</b>
        <span className="mono dim">{fmtT(b.t)}</span>
        <Seg<number> value={speed} onChange={setSpeed} options={SPEEDS} />
        {inbound > 0 && <span className="tag bad">{inbound} inbound</span>}
        <span className="grow" />
        <span className="small dim">Defence</span>
        <Seg value={doc.defense} onChange={(v) => { doc.defense = v; setTick((t) => t + 1); }} options={[["free", "Free"], ["tight", "Tight"], ["hold", "Hold"]]} />
        <span className="small dim">Offence</span>
        <Seg value={doc.offense} onChange={(v) => { doc.offense = v; setTick((t) => t + 1); }} options={[["auto", "Auto"], ["manual", "Manual"]]} />
        <span className="small dim">Salvo</span>
        <Seg<number> value={doc.salvo} onChange={(v) => { doc.salvo = v; setSalvo(v); setTick((t) => t + 1); }} options={[[2, "2"], [4, "4"], [8, "8"]]} />
        <button className="btn" onClick={finish}>{b.over ? "Return to campaign" : "Resolve automatically"}</button>
      </div>
      <div className="plot" onMouseDown={onDown} onMouseMove={onMove} onMouseUp={onUp} onMouseLeave={() => (drag.current = null)} onContextMenu={onContext} onWheel={onWheel}>
        <canvas ref={cvs} />
        {mode === "fire" && <div className="mode">FIRE: click an enemy track · Esc cancels</div>}
        <div className="legend">
          <span style={{ color: "var(--friend)" }}>○ surface  ⌒ air  ⌣ sub  □ ground</span>
          <span style={{ color: "var(--hostile)" }}>◇ surface  ^ air  v sub  ◈ ground</span>
          <span>right-click: move · wheel: zoom · drag: pan · Space: pause</span>
        </div>
      </div>
      <aside className="side">
        <div style={{ padding: 12, borderBottom: "1px solid var(--line)" }} className="col">
          <div className="row between"><span><span className="nation-chip" style={{ background: "var(--friend)" }} />{NATIONS[myN].name}</span><span className="mono small">{mine.filter((u) => u.alive).length}/{mine.length} units</span></div>
          <div className="row between"><span><span className="nation-chip" style={{ background: "var(--hostile)" }} />{NATIONS[theirN].name}</span><span className="mono small">{known.filter((u) => u.alive).length} tracked</span></div>
          <div className="small mono dim">launched {b.counts.launched[0]}/{b.counts.launched[1]} · intercepts {b.counts.intercepts[0]}/{b.counts.intercepts[1]} · hits {b.counts.hits[0]}/{b.counts.hits[1]}</div>
          {b.over && (
            <div className={`issue ${b.winner === me ? "info" : "warn"}`}>
              <b>Battle over.</b> {winnerName ? `${winnerName} holds the advantage.` : "Inconclusive."} Return to apply losses and munitions spent to the campaign.
            </div>
          )}
        </div>
        {su ? (
          <div style={{ padding: 12 }} className="col">
            <div className="row between"><b>{su.name}</b><span className={`tag ${su.side === me ? "friend" : "bad"}`}>{su.kind}</span></div>
            <div className="small dim">{su.alive ? su.mission : "destroyed"}{su.kind === "air" || su.kind === "uav" ? ` · ${su.aircraft} aircraft · fuel ${Math.max(0, Math.round(su.fuel / 60))} min` : ""}</div>
            <div className="stat"><span>Integrity</span><b>{Math.round(Math.max(0, su.hp / su.maxHp) * 100)}%</b><Meter v={su.hp / su.maxHp} kind={su.hp / su.maxHp < 0.5 ? "bad" : "good"} thin /></div>
            {su.radarKm > 0 && <div className="small mono dim">radar {Math.round(su.radarKm)} km · {su.channels} channels{su.ciwsKm ? ` · CIWS ${su.ciwsKm} km` : ""}</div>}
            {(me === -1 || su.side === me) && su.weapons.map((w) => (
              <div key={w.designId} className={`weapon ${weapon === w.designId ? "sel" : ""}`}>
                <span>{w.name} <span className="dim small">{w.s.role} · {w.s.rangeKm} km</span></span>
                <span className="mono">{w.count}/{w.start}</span>
                <Meter v={w.count / Math.max(1, w.start)} thin />
                {w.s.role !== "sam" && w.s.role !== "aam" && w.s.role !== "bmd" && w.count > 0 && su.alive && (
                  <button className="btn small" style={{ gridColumn: "1 / -1" }} onClick={() => { setWeapon(w.designId); setMode("fire"); }}>Fire {Math.min(salvo, w.count)}…</button>
                )}
              </div>
            ))}
            {(me === -1 || su.side === me) && su.kind !== "base" && su.kind !== "ground" && <div className="small dim">Right-click the plot to send {su.name} there.</div>}
          </div>
        ) : (
          <div style={{ padding: 12 }} className="small dim">
            Click one of your units to see its weapons and give orders. Air defence fights on its own under the doctrine above. With offence on Auto, launchers fire at targets in range; on Manual, you pick the targets.
          </div>
        )}
        <div style={{ padding: "0 12px 12px" }} className="col">
          <div className="label">Your forces</div>
          {mine.filter((u) => u.alive && !u.landed).slice(0, 40).map((u) => (
            <button key={u.id} className={`nav-item ${sel === u.id ? "on" : ""}`} style={{ padding: "4px 8px", fontSize: 12.5 }} onClick={() => setSel(u.id)}>
              {u.name}<span className="k">{Math.round((u.hp / u.maxHp) * 100)}%</span>
            </button>
          ))}
        </div>
      </aside>
      <div className="log" aria-live="polite">
        {b.log.slice(0, 120).map((l, i) => {
          const cls = l.cls === "w" ? "w" : l.side === -1 ? "" : (me === -1 ? l.side === 0 : l.side === me) ? "f" : "h";
          return <div key={i}><span className="dim">{fmtT(l.t)}</span><span className={cls}>{l.text}</span></div>;
        })}
      </div>
    </div>
  );
}

// ---------------------------------------------------------------- canvas drawing
function draw(el: HTMLCanvasElement | null, b: BattleState, v: { cx: number; cy: number; scale: number }, rings: Float32Array[], sel: string | null, col: { friend: string; hostile: string }) {
  if (!el) return;
  const dpr = window.devicePixelRatio || 1;
  const w = el.clientWidth, h = el.clientHeight;
  if (el.width !== Math.round(w * dpr) || el.height !== Math.round(h * dpr)) { el.width = Math.round(w * dpr); el.height = Math.round(h * dpr); }
  const g = el.getContext("2d")!;
  g.setTransform(dpr, 0, 0, dpr, 0, 0);
  g.fillStyle = "#15191d";
  g.fillRect(0, 0, w, h);
  const X = (x: number) => w / 2 + (x - v.cx) * v.scale;
  const Y = (y: number) => h / 2 - (y - v.cy) * v.scale;
  const me = b.playerSide;

  // grid
  const stepKm = v.scale > 2 ? 25 : v.scale > 0.6 ? 100 : v.scale > 0.15 ? 250 : 1000;
  g.strokeStyle = "#1d2227";
  g.lineWidth = 1;
  g.beginPath();
  const x0 = Math.floor((v.cx - w / 2 / v.scale) / stepKm) * stepKm, x1 = v.cx + w / 2 / v.scale;
  const y0 = Math.floor((v.cy - h / 2 / v.scale) / stepKm) * stepKm, y1 = v.cy + h / 2 / v.scale;
  for (let x = x0; x <= x1; x += stepKm) { g.moveTo(X(x), 0); g.lineTo(X(x), h); }
  for (let y = y0; y <= y1; y += stepKm) { g.moveTo(0, Y(y)); g.lineTo(w, Y(y)); }
  g.stroke();

  // land
  g.fillStyle = "#23272c";
  g.strokeStyle = "#3a4047";
  for (const r of rings) {
    g.beginPath();
    for (let i = 0; i < r.length; i += 2) { const px = X(r[i]), py = Y(r[i + 1]); if (i === 0) g.moveTo(px, py); else g.lineTo(px, py); }
    g.closePath();
    g.fill();
    g.stroke();
  }

  // scale bar
  g.fillStyle = "#8c887f";
  g.font = "11px JetBrains Mono, monospace";
  g.fillRect(16, h - 50, stepKm * v.scale, 2);
  g.fillText(`${stepKm} km`, 16, h - 56);

  const visible = (id: string, side: Side) => me === -1 || side === me || b.tracks[me].has(id);
  const colorOf = (side: Side) => (me === -1 ? (side === 0 ? col.friend : col.hostile) : side === me ? col.friend : col.hostile);

  // selected unit rings
  const su = sel ? b.units.find((u) => u.id === sel) : undefined;
  if (su && su.alive) {
    g.setLineDash([4, 5]);
    if (su.radarKm > 0) { g.strokeStyle = "rgba(91,168,228,0.35)"; g.beginPath(); g.arc(X(su.x), Y(su.y), su.radarKm * v.scale, 0, Math.PI * 2); g.stroke(); }
    const maxW = Math.max(0, ...su.weapons.filter((x) => x.count > 0).map((x) => x.s.rangeKm ?? 0));
    if (maxW > 0) { g.strokeStyle = "rgba(240,164,58,0.45)"; g.beginPath(); g.arc(X(su.x), Y(su.y), maxW * v.scale, 0, Math.PI * 2); g.stroke(); }
    g.setLineDash([]);
    if (su.wx !== undefined && su.wy !== undefined) { g.strokeStyle = "rgba(240,164,58,0.7)"; g.beginPath(); g.moveTo(X(su.x), Y(su.y)); g.lineTo(X(su.wx), Y(su.wy)); g.stroke(); }
  }

  // missiles
  for (const m of b.missiles) {
    if (!m.alive || !visible(m.id, m.side)) continue;
    const c = colorOf(m.side);
    const interceptor = m.role === "sam" || m.role === "aam" || m.role === "bmd";
    g.strokeStyle = c;
    g.globalAlpha = 0.35;
    g.beginPath();
    m.trail.forEach(([tx, ty], i) => (i === 0 ? g.moveTo(X(tx), Y(ty)) : g.lineTo(X(tx), Y(ty))));
    g.lineTo(X(m.x), Y(m.y));
    g.stroke();
    g.globalAlpha = 1;
    g.fillStyle = c;
    g.beginPath();
    g.arc(X(m.x), Y(m.y), interceptor ? 1.6 : m.role === "srbm" || m.role === "hgv" || m.role === "asbm" ? 3.2 : 2.4, 0, Math.PI * 2);
    g.fill();
  }

  // units
  for (const u of b.units) {
    if (u.landed || !visible(u.id, u.side)) continue;
    const x = X(u.x), y = Y(u.y);
    const c = colorOf(u.side);
    const friend = me === -1 ? u.side === 0 : u.side === me;
    g.strokeStyle = u.alive ? c : "#5d5a54";
    g.fillStyle = c;
    g.lineWidth = u.id === sel ? 2.6 : 1.8;
    const s = u.kind === "base" ? 8 : u.kind === "ship" && u.maxHp > 2000 ? 8 : 6;
    g.beginPath();
    switch (u.kind) {
      case "ship": if (friend) g.arc(x, y, s, 0, Math.PI * 2); else { g.moveTo(x, y - s); g.lineTo(x + s, y); g.lineTo(x, y + s); g.lineTo(x - s, y); g.closePath(); } break;
      case "sub": if (friend) g.arc(x, y, s, 0, Math.PI); else { g.moveTo(x - s, y); g.lineTo(x, y + s); g.lineTo(x + s, y); } break;
      case "air": case "uav": if (friend) g.arc(x, y + 2, s, Math.PI, 0); else { g.moveTo(x - s, y + 3); g.lineTo(x, y - s + 1); g.lineTo(x + s, y + 3); } break;
      case "ground": if (friend) g.rect(x - s, y - s, s * 2, s * 2); else { g.moveTo(x, y - s); g.lineTo(x + s, y); g.lineTo(x, y + s); g.lineTo(x - s, y); g.closePath(); g.moveTo(x + 1.5, y); g.arc(x, y, 1.5, 0, Math.PI * 2); } break;
      case "base": g.rect(x - s, y - s, s * 2, s * 2); g.moveTo(x - s, y - s); g.lineTo(x + s, y + s); g.moveTo(x + s, y - s); g.lineTo(x - s, y + s); break;
    }
    g.stroke();
    if (u.kind === "ship" && u.alive) { g.beginPath(); g.arc(x, y, 1.6, 0, Math.PI * 2); g.fill(); }
    if (!u.alive) { g.strokeStyle = "#e5484d"; g.beginPath(); g.moveTo(x - s, y - s); g.lineTo(x + s, y + s); g.stroke(); }
    if (v.scale > 0.35 || u.id === sel || u.kind === "base") {
      g.fillStyle = u.alive ? "#b9b4aa" : "#5d5a54";
      g.font = "10.5px JetBrains Mono, monospace";
      g.fillText(u.name, x + s + 4, y - 4);
    }
    if (u.alive && u.hp < u.maxHp && u.kind !== "air") {
      g.fillStyle = "#2a2e34"; g.fillRect(x - 10, y + s + 4, 20, 3);
      g.fillStyle = u.hp / u.maxHp < 0.5 ? "#e5484d" : "#7bc47f"; g.fillRect(x - 10, y + s + 4, 20 * Math.max(0, u.hp / u.maxHp), 3);
    }
  }
}

