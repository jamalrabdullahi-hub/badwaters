using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;

namespace BadWaters.Sim.Command;

/// <summary>A war between two nations (the persistent world has no fixed theatres).</summary>
public sealed record War(string A, string B, DateTime Started)
{
    public string Key => FightZone.Key(A, B);
    public bool Involves(string n) => A == n || B == n;
}

/// <summary>
/// Fight zones: a war spins up a local fight wherever the two nations' forces can actually reach each other. The zone
/// is generated from the persistent world order of battle (data/core/world_oob.json, sides = nation ids) as an
/// ordinary ScenarioDef, so everything built on a theatre (commanders, logistics, missions, inspector) works on it.
/// What persists is the world: a zone's results are written back to World.Forces and the zone is rebuilt from it.
/// </summary>
public static class FightZone
{
    /// <summary>Forces further than this from every enemy asset stay out of the fight (game tuning).</summary>
    public const double ContactKm = 2000;

    public static string Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";
    public static (string A, string B) Parse(string key) { var p = key.Split(':'); return (p[0], p[1]); }

    private static IEnumerable<(double Lon, double Lat)> Points(ScenarioDef w, string nation) =>
        w.Sites.Where(s => s.Side == nation && s.Class != "population-centre").Select(s => (s.Lon, s.Lat))
            .Concat(w.Units.Where(u => u.Side == nation && u.Kind is "ship" or "sub").Select(u => (u.Lon, u.Lat)))
            .Concat(w.Batteries.Where(b => b.Side == nation).Select(b => (b.Lon, b.Lat)))
            .Concat(w.Launchers.Where(l => l.Side == nation).Select(l => (l.Lon, l.Lat)));

    /// <summary>The fight between A and B: every asset of either within ContactKm of the other's, as a scenario.
    /// Blue is the player's nation when it is involved (else A). Null when the two can't reach each other.</summary>
    public static ScenarioDef? Build(ScenarioDef world, string a, string b, string? player, Func<string, string> nationName, bool atWar = true)
    {
        var blue = player == b ? b : a; var red = blue == a ? b : a;
        var pa = Points(world, a).ToList(); var pb = Points(world, b).ToList();
        if (pa.Count == 0 || pb.Count == 0) return null;
        bool Near(double lon, double lat, List<(double Lon, double Lat)> other) => other.Any(o => Pacing.DistanceKm(lon, lat, o.Lon, o.Lat) <= ContactKm);
        bool In(string side, double lon, double lat) => side == a ? Near(lon, lat, pb) : side == b && Near(lon, lat, pa);
        if (!pa.Any(p => Near(p.Lon, p.Lat, pb))) return null; // nowhere near each other: no fight
        return Assemble(world, $"war-{a}-{b}", $"{nationName(blue)} vs {nationName(red)}", Key(a, b), blue, red, nationName, In,
            $"War between {nationName(a)} and {nationName(b)}: every force within reach of the other is in this fight.");
    }

    /// <summary>Peacetime view of the player's own forces and the nearest foreign nation's within reach (track only).</summary>
    public static ScenarioDef? Home(ScenarioDef world, string player, Func<string, string> nationName)
    {
        var mine = Points(world, player).ToList();
        if (mine.Count == 0) return null;
        var other = world.Sites.Select(s => s.Side).Concat(world.Units.Select(u => u.Side)).Distinct().Where(n => n != player)
            .Select(n => (n, d: Points(world, n).Select(p => mine.Min(m => Pacing.DistanceKm(m.Lon, m.Lat, p.Lon, p.Lat))).DefaultIfEmpty(double.MaxValue).Min()))
            .Where(x => x.d <= ContactKm).OrderBy(x => x.d).Select(x => x.n).FirstOrDefault();
        if (other == null)
            return Assemble(world, $"home-{player}", $"{nationName(player)} · home forces", "", player, "", nationName, (s, _, _) => s == player, "Your forces in peacetime.");
        var po = Points(world, other).ToList();
        return Assemble(world, $"home-{player}", $"{nationName(player)} · home waters", "", player, other, nationName,
            (s, lon, lat) => s == player || (s == other && mine.Any(m => Pacing.DistanceKm(m.Lon, m.Lat, lon, lat) <= ContactKm)),
            $"Peacetime: your forces and {nationName(other)}'s within reach. Not at war: track only.");
    }

    private static ScenarioDef Assemble(ScenarioDef w, string id, string name, string warKey, string blue, string red, Func<string, string> nationName,
        Func<string, double, double, bool> inZone, string brief)
    {
        string Side(string nation) => nation == blue ? "blue" : "red";
        var sites = w.Sites.Where(s => (s.Side == blue || s.Side == red) && s.Class != "population-centre" && inZone(s.Side, s.Lon, s.Lat)).ToList();
        var siteIds = sites.Select(s => s.Id).ToHashSet();
        var units = w.Units.Where(u => (u.Side == blue || u.Side == red) && (u.Kind is "air" or "aew" ? siteIds.Contains(u.Base) : inZone(u.Side, u.Lon, u.Lat))).ToList();
        var bats = w.Batteries.Where(b => (b.Side == blue || b.Side == red) && inZone(b.Side, b.Lon, b.Lat)).ToList();
        var lns = w.Launchers.Where(l => (l.Side == blue || l.Side == red) && inZone(l.Side, l.Lon, l.Lat)).ToList();
        var pts = sites.Select(s => (s.Lon, s.Lat)).Concat(units.Select(u => (u.Lon, u.Lat))).Concat(bats.Select(b => (b.Lon, b.Lat))).Concat(lns.Select(l => (l.Lon, l.Lat))).ToList();
        if (pts.Count == 0) pts.Add((0, 0));
        var (x0, x1, y0, y1) = (pts.Min(p => p.Lon), pts.Max(p => p.Lon), pts.Min(p => p.Lat), pts.Max(p => p.Lat));
        // protected places of both nations inside the zone come along: defended, never targets
        var cities = w.Sites.Where(s => s.Class == "population-centre" && (s.Side == blue || s.Side == red) && s.Lon >= x0 - 1 && s.Lon <= x1 + 1 && s.Lat >= y0 - 1 && s.Lat <= y1 + 1);
        sites.AddRange(cities);
        var ids = sites.Select(s => s.Id).Concat(bats.Select(b => b.Id)).Concat(lns.Select(l => l.Id)).ToHashSet();
        var lg = w.Logistics == null ? null : new ScenarioLogistics
        {
            Nodes = w.Logistics.Nodes.Where(n => ids.Contains(n.Key)).ToDictionary(n => n.Key, n => n.Value),
            Corridors = w.Logistics.Corridors.Where(c => ids.Contains(c.A) && ids.Contains(c.B) && (c.Side == blue || c.Side == red)).Select(c => c with { Side = Side(c.Side) }).ToList(),
        };
        var span = Math.Max(x1 - x0, (y1 - y0) * 1.6);
        return new ScenarioDef
        {
            Id = id, Name = name, Brief = brief, Theatre = warKey,
            Center = new[] { (x0 + x1) / 2, (y0 + y1) / 2 }, Zoom = (float)Math.Clamp(40 / Math.Max(1, span), 1.2, 6),
            Sides = red == "" ? new() { ["blue"] = nationName(blue), ["red"] = "—" } : new() { ["blue"] = nationName(blue), ["red"] = nationName(red) },
            Sites = sites.Select(s => s with { Side = Side(s.Side) }).ToList(),
            Units = units.Select(u => u with { Side = Side(u.Side) }).ToList(),
            Batteries = bats.Select(b => b with { Side = Side(b.Side) }).ToList(),
            Launchers = lns.Select(l => l with { Side = Side(l.Side) }).ToList(),
            Logistics = lg,
            Neutral = w.Neutral.Where(l => l.Any(p => p[0] >= x0 - 3 && p[0] <= x1 + 3 && p[1] >= y0 - 3 && p[1] <= y1 + 3)).ToList(),
        };
    }

    /// <summary>A zone's footprint for the strategic map and trade: centre and radius (km).</summary>
    public static (double Lon, double Lat, double Km) Footprint(ScenarioDef zone)
    {
        var pts = zone.Sites.Where(s => s.Class != "population-centre").Select(s => (s.Lon, s.Lat)).ToList();
        if (pts.Count == 0) return (zone.Center[0], zone.Center[1], 0);
        var (cx, cy) = (zone.Center[0], zone.Center[1]);
        return (cx, cy, pts.Max(p => Pacing.DistanceKm(cx, cy, p.Lon, p.Lat)) + 250);
    }
}
