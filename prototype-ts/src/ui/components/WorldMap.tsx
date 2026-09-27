import { geoGraticule10, geoNaturalEarth1, geoPath } from "d3-geo";
import { useMemo, useState } from "react";
import { NATIONS, THEATRES } from "../../data/nations";
import { landCoarse } from "../../sim/battle/geo";
import { atWar, statsOf } from "../../sim/campaign/core";
import type { Base, Campaign, NationId } from "../../sim/types";

export const REGIONS: Record<string, { name: string; bounds: [[number, number], [number, number]] }> = {
  world: { name: "World", bounds: [[-150, -30], [165, 78]] },
  europe: { name: "Europe & Med", bounds: [[-35, 28], [55, 74]] },
  mideast: { name: "Middle East", bounds: [[25, 8], [70, 42]] },
  asia: { name: "East Asia", bounds: [[95, 5], [160, 50]] },
};

const W = 1000, H = 520;

export function WorldMap(props: {
  c: Campaign;
  region: string;
  height?: number;
  selectedBase?: string | null;
  onBase?: (b: Base) => void;
  highlight?: (b: Base) => boolean;
}) {
  const { c } = props;
  const reg = REGIONS[props.region] ?? REGIONS.world;
  const [hover, setHover] = useState<string | null>(null);

  const { proj, land, grat } = useMemo(() => {
    const [[x0, y0], [x1, y1]] = reg.bounds;
    // fit to corner points: a polygon box would be read by d3 as its spherical complement
    const box = { type: "MultiPoint" as const, coordinates: [[x0, y0], [x1, y0], [x1, y1], [x0, y1], [(x0 + x1) / 2, y0], [(x0 + x1) / 2, y1]] };
    const proj = geoNaturalEarth1().fitSize([W, H], box);
    const path = geoPath(proj);
    return { proj, land: path(landCoarse() as never) ?? "", grat: path(geoGraticule10()) ?? "" };
  }, [reg]);

  const me = c.player;
  // fleets at sea by theatre and nation
  const atSea = new Map<string, Map<NationId, number>>();
  for (const u of c.units) {
    if (u.location.kind !== "sea" || u.embarkedOn || c.designs[u.designId]?.category !== "ship") continue;
    const m = atSea.get(u.location.theatreId) ?? new Map<NationId, number>();
    m.set(u.nation, (m.get(u.nation) ?? 0) + 1);
    atSea.set(u.location.theatreId, m);
  }
  const unitsAt = (b: Base) => c.units.filter((u) => u.location.kind === "base" && u.location.baseId === b.id && !u.embarkedOn).length;
  const hovered = hover ? c.bases.find((b) => b.id === hover) : undefined;

  return (
    <div className="map" style={{ height: props.height ?? 360 }}>
      <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="xMidYMid meet" role="img" aria-label="Strategic map">
        <path className="grat" d={grat} />
        <path className="land" d={land} />
        {THEATRES.map((t) => {
          const p = proj([t.lon, t.lat]);
          if (!p) return null;
          const fleets = atSea.get(t.id);
          return (
            <g key={t.id} className="th" transform={`translate(${p[0]},${p[1]})`}>
              <circle r={22} />
              <text y={-26} textAnchor="middle">{t.name}</text>
              {fleets && [...fleets].map(([n, k], i) => {
                const hostile = n !== me && atWar(c, n, me);
                return (
                  <g key={n} transform={`translate(${(i - (fleets.size - 1) / 2) * 16},0)`}>
                    {n === me ? <circle r={6} fill="none" stroke="var(--friend)" strokeWidth={2} /> : <path d="M0,-7 L7,0 L0,7 L-7,0 Z" fill="none" stroke={hostile ? "var(--hostile)" : NATIONS[n].color} strokeWidth={2} />}
                    <text y={18} textAnchor="middle" style={{ fill: n === me ? "var(--friend)" : NATIONS[n].color }}>{k}</text>
                  </g>
                );
              })}
            </g>
          );
        })}
        {c.bases.map((b) => {
          const p = proj([b.lon, b.lat]);
          if (!p) return null;
          const mine = b.nation === me;
          const hostile = !mine && atWar(c, b.nation, me);
          const sel = props.selectedBase === b.id;
          const hi = props.highlight?.(b);
          const size = 5 + Math.min(5, unitsAt(b) / 3);
          return (
            <g key={b.id} className="base" transform={`translate(${p[0]},${p[1]})`} onClick={() => props.onBase?.(b)} onMouseEnter={() => setHover(b.id)} onMouseLeave={() => setHover(null)}>
              <rect x={-size} y={-size} width={size * 2} height={size * 2} fill={NATIONS[b.nation].color} fillOpacity={b.damage > 0.3 ? 0.35 : 0.85}
                stroke={sel ? "var(--accent)" : hostile ? "var(--hostile)" : "#0b0c0e"} strokeWidth={sel || hostile ? 2.5 : 1} transform={b.type === "missile" ? "rotate(45)" : undefined} />
              {hi && <circle r={size + 6} fill="none" stroke="var(--accent)" strokeDasharray="2 2" />}
            </g>
          );
        })}
        {hovered && (() => {
          const p = proj([hovered.lon, hovered.lat]);
          if (!p) return null;
          const stats = hovered.designId ? statsOf(c, hovered.designId) : undefined;
          return (
            <g transform={`translate(${Math.min(W - 230, p[0] + 12)},${Math.max(14, p[1] - 30)})`} pointerEvents="none">
              <rect width={220} height={46} rx={3} fill="#0f1113" stroke="#3a4048" />
              <text x={8} y={16} style={{ fill: "var(--ink)", fontWeight: 600 }}>{hovered.name}</text>
              <text x={8} y={31}>{NATIONS[hovered.nation].adjective} {hovered.type} base · {unitsAt(hovered)} units</text>
              <text x={8} y={42}>damage {Math.round(hovered.damage * 100)}% · protection {Math.round((stats?.hardening ?? 0) * 100)}%</text>
            </g>
          );
        })()}
      </svg>
    </div>
  );
}

export function RegionPicker({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <span className="seg">
      {Object.entries(REGIONS).map(([k, r]) => <button key={k} className={k === value ? "on" : ""} onClick={() => onChange(k)}>{r.name}</button>)}
    </span>
  );
}

export function defaultRegion(n: NationId): string {
  return n === "CHN" ? "asia" : n === "ISR" || n === "IRN" ? "mideast" : n === "GBR" || n === "FRA" || n === "RUS" ? "europe" : "world";
}
