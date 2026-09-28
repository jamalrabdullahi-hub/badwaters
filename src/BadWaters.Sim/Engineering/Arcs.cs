namespace BadWaters.Sim.Engineering;

/// <summary>
/// Firing arcs blocked by taller parts (RTW style). Every part has a height class; a weapon's arc is swept in
/// small steps and each ray is blocked if a taller part's footprint sits across it. The result is a coverage
/// fraction and the free/blocked sectors for drawing. Game geometry only.
/// </summary>
public static class ArcGeometry
{
    /// <summary>Height class by category (overridable per part with "height").</summary>
    public static double Height(VisualPart p) => p.Height >= 0 ? p.Height : p.Category switch
    {
        "Masts" => 5,
        "Superstructure" or "Funnels" => 4,
        "Aviation" => p.Topside ? 4 : 0, // hangars and islands stand tall; flight decks are flat
        "Sensors" or "Electronics" => p.Topside ? 3 : 0,
        "Point defence" => 2,
        "Guns" or "Launchers" => 1,
        "Boats" or "Deck gear" => 1,
        _ => 0,
    };

    public sealed record Sector(double From, double To, bool Blocked);

    /// <summary>Arc coverage of the part at <paramref name="index"/> (1 = fully free) and its sectors in degrees (0 = bow, clockwise).</summary>
    public static (double Coverage, List<Sector> Sectors) Coverage(DesignDef d, int index, Catalog cat, double reach)
    {
        var pl = d.Parts[index];
        if (!cat.Parts.TryGetValue(pl.Part, out var p) || p.Arc <= 0) return (1, new());
        var h = Height(p);
        var blockers = d.Parts.Select((q, i) => (q, i)).Where(x => x.i != index && cat.Parts.TryGetValue(x.q.Part, out var bp) && Height(bp) > h)
            .Select(x => (x.q.X, x.q.Y, R: cat.Parts[x.q.Part].Extent * 0.75 * x.q.Scale)).ToList();
        const double step = 3;
        var sectors = new List<Sector>();
        int free = 0, total = 0;
        for (var a = -p.Arc; a <= p.Arc + 1e-9; a += step)
        {
            var deg = pl.Rot + a;
            var rad = deg * Math.PI / 180;
            var (dx, dy) = (Math.Sin(rad), Math.Cos(rad)); // 0° = bow (+y), clockwise
            var blocked = blockers.Any(b =>
            {
                var (bx, by) = (b.X - pl.X, b.Y - pl.Y);
                var t = bx * dx + by * dy; // distance along the ray to the closest point
                if (t <= 0 || t > reach) return false;
                var off = Math.Abs(bx * dy - by * dx);
                return off < b.R;
            });
            total++; if (!blocked) free++;
            if (sectors.Count > 0 && sectors[^1].Blocked == blocked) sectors[^1] = sectors[^1] with { To = deg + step / 2 };
            else sectors.Add(new Sector(deg - step / 2, deg + step / 2, blocked));
        }
        return (total == 0 ? 1 : (double)free / total, sectors);
    }
}
