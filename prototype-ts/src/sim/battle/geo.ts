import { geoContains } from "d3-geo";
import { feature } from "topojson-client";
import type { Feature, FeatureCollection, MultiPolygon, Polygon } from "geojson";
import type { Topology, GeometryCollection } from "topojson-specification";
import land110 from "world-atlas/land-110m.json";
import land50 from "world-atlas/land-50m.json";

type LandFeature = Feature<Polygon | MultiPolygon> | FeatureCollection<Polygon | MultiPolygon>;

let coarse: LandFeature | null = null;
let fine: LandFeature | null = null;

function toFeature(topo: unknown): LandFeature {
  const t = topo as Topology<{ land: GeometryCollection }>;
  return feature(t, t.objects.land) as unknown as LandFeature;
}

export function landCoarse(): LandFeature {
  if (!coarse) coarse = toFeature(land110);
  return coarse;
}
export function landFine(): LandFeature {
  if (!fine) fine = toFeature(land50);
  return fine;
}

export function isLand(lat: number, lon: number): boolean {
  return geoContains(landCoarse() as never, [lon, lat]);
}

/** Local equirectangular projection around (lat0, lon0) in km. */
export function makeProj(lat0: number, lon0: number) {
  const kx = 111.32 * Math.cos((lat0 * Math.PI) / 180);
  const ky = 110.57;
  return {
    toXY: (lat: number, lon: number): [number, number] => {
      let dl = lon - lon0;
      if (dl > 180) dl -= 360;
      if (dl < -180) dl += 360;
      return [dl * kx, (lat - lat0) * ky];
    },
    toLatLon: (x: number, y: number): [number, number] => [lat0 + y / ky, lon0 + x / kx],
  };
}

/** Nudge a point off land by searching outward in a spiral (sea placement for ships). */
export function seaPoint(lat: number, lon: number, rand: () => number): [number, number] {
  if (!isLand(lat, lon)) return [lat, lon];
  for (let r = 0.3; r < 8; r += 0.3) {
    for (let k = 0; k < 12; k++) {
      const a = rand() * Math.PI * 2;
      const la = lat + Math.sin(a) * r;
      const lo = lon + Math.cos(a) * r;
      if (!isLand(la, lo)) return [la, lo];
    }
  }
  return [lat, lon];
}
