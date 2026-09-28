// One-time conversion: Natural Earth 50m land (world-atlas topojson) -> rings + triangles JSON.
// Triangulated here with earcut so the game can draw the whole world as one mesh.
// Run from repo root: node tools/geo-convert.mjs
import { readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
const require = createRequire(new URL("../prototype-ts/package.json", import.meta.url));
const { feature } = require("topojson-client");
const earcut = (await import(new URL("../prototype-ts/node_modules/earcut/src/earcut.js", import.meta.url))).default;
for (const res of ["50m", "110m"]) {
const topo = JSON.parse(readFileSync(`prototype-ts/node_modules/world-atlas/land-${res}.json`, "utf8"));
const land = feature(topo, topo.objects.land);
const r3 = (v) => Math.round(v * 1000) / 1000;
const rings = [], polys = [];
let tris = 0;
for (const f of land.features ?? [land]) {
  const g = f.geometry;
  for (const p of g.type === "Polygon" ? [g.coordinates] : g.coordinates) {
    // polygons sitting on the date line (Russia's Chukotka, Fiji) mix -180 and +180: shift their far-west vertices
    // east by 360 BEFORE triangulating so the triangles tile the real shape (Antarctica spans every longitude: skip)
    const xs = p[0].map(([x]) => x), ys = p[0].map(([, y]) => y);
    const wrap = Math.max(...xs) > 170 && Math.min(...xs) < -170 && Math.min(...ys) > -60;
    const sx = (x) => (wrap && x < -150 ? x + 360 : x);
    const outer = p[0].map(([x, y]) => [r3(sx(x)), r3(Math.max(-80, Math.min(84, y)))]);
    if (outer.length < 4) continue;
    const holes = p.slice(1).map((h) => h.map(([x, y]) => [r3(sx(x)), r3(y)]));
    const flat = [], holeIdx = [];
    for (const [x, y] of outer) flat.push(x, y);
    for (const h of holes) { holeIdx.push(flat.length / 2); for (const [x, y] of h) flat.push(x, y); }
    const idx = earcut(flat, holeIdx.length ? holeIdx : undefined);
    tris += idx.length / 3;
    rings.push(outer);
    polys.push({ v: flat, t: idx });
  }
}
writeFileSync(`data/core/geo/land-${res}.json`, JSON.stringify({ source: `Natural Earth ${res} land (public domain)`, rings, polys }));
console.log(res, "rings", rings.length, "triangles", tris);
}
