namespace BadWaters.Sim.Engineering;

// Shape rules: a small, deterministic game rule set that turns a design's outline into rating modifiers.
// It is NOT a radar cross-section or aerodynamic model. It counts simple things about the drawn shape
// (vertices, how many distinct edge directions, area compared with the standard hull, wing aspect, sweep,
// tails) and applies fixed bonuses and penalties, so clean aligned shapes are stealthier, huge or cluttered
// shapes are not, and a bare four-point flying wing trades agility for its stealth.

/// <summary>Default side profiles (length along +Y to the bow/nose, height Z up) for each style.</summary>
public static class SideGeometry
{
    public static List<(double Y, double Z)> Profile(HullDef h)
    {
        double L = h.Length, B = h.Beam;
        switch (h.Style)
        {
            case "sub":
                return new() { (L / 2, 0), (.45 * L, .35 * B), (.3 * L, .5 * B), (-.35 * L, .5 * B), (-.48 * L, .2 * B), (-.5 * L, 0), (-.48 * L, -.2 * B), (-.35 * L, -.5 * B), (.3 * L, -.5 * B), (.45 * L, -.35 * B) };
            case "fighter" or "delta" or "stealth" or "canard-delta" or "large" or "uav" or "hale" or "male" or "flying-wing":
                var f = B / 2;
                var tail = h.Style is "flying-wing" ? f * 1.1 : f * 2.6;
                return new() { (L / 2, 0), (.35 * L, f * .8), (.15 * L, f * 1.5), (-.25 * L, f * 1.1), (-.42 * L, tail), (-.5 * L, tail), (-.5 * L, -f * .5), (.2 * L, -f * .8), (.4 * L, -f * .4) };
            case "vehicle" or "tracked" or "pad":
                return new() { (L / 2, 0), (L / 2, B * .7), (-L / 2, B * .7), (-L / 2, 0) };
            case "missile":
                return new() { (L / 2, 0), (.5 * L - 2 * B, B / 2), (-.5 * L, B / 2), (-.5 * L, -B / 2), (.5 * L - 2 * B, -B / 2) };
            case "tumblehome":
            {
                double fb = B * .35, d = B * .25;
                return new() { (L / 2 - .06 * L, fb), (-L / 2, fb), (-L / 2, -d * .3), (-L / 2 + .05 * L, -d), (L / 2 - .12 * L, -d), (L / 2, -d * .1) };
            }
            default:
            {
                double fb = B * .35, d = B * .25;
                return new() { (L / 2, fb * 1.25), (-L / 2, fb), (-L / 2, -d * .3), (-L / 2 + .05 * L, -d), (L / 2 - .15 * L, -d), (L / 2 - .08 * L, -d * .6) };
            }
        }
    }
}

public static class ShapeRules
{
    public sealed record Result(Dictionary<string, double> Ratings, double Mass, List<string> Notes);

    public static List<(double X, double Y)> TopHalf(FrameDef f, DesignDef d) =>
        d.Outline.Count >= 6 ? d.Outline.Chunk(2).Select(c => (c[0], c[1])).ToList() : HullGeometry.Outline(f.Hull!);

    public static List<(double Y, double Z)> Side(FrameDef f, DesignDef d) =>
        d.Side.Count >= 6 ? d.Side.Chunk(2).Select(c => (c[0], c[1])).ToList() : SideGeometry.Profile(f.Hull!);

    public static double Area(IReadOnlyList<(double X, double Y)> poly)
    {
        double a = 0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) a += (poly[j].X + poly[i].X) * (poly[j].Y - poly[i].Y);
        return Math.Abs(a) / 2;
    }

    /// <summary>Number of distinct edge directions (10° buckets, direction ignored).</summary>
    public static int Directions(IReadOnlyList<(double X, double Y)> pts, bool closed)
    {
        var set = new HashSet<int>();
        var n = closed ? pts.Count : pts.Count - 1;
        for (var i = 0; i < n; i++)
        {
            var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
            if (Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y) < 1e-6) continue;
            var ang = (Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI + 180) % 180;
            set.Add((int)Math.Round(ang / 10) % 18);
        }
        return set.Count;
    }

    private static List<(double X, double Y)> Full(List<(double X, double Y)> half) =>
        half.Concat(half.AsEnumerable().Reverse().Select(p => (X: -p.X, p.Y))).ToList();

    public static Result Evaluate(FrameDef f, DesignDef d, Catalog cat)
    {
        var r = new Dictionary<string, double>();
        var notes = new List<string>();
        void Add(string rating, double v, string why) { r[rating] = r.GetValueOrDefault(rating) + v; notes.Add($"{(v > 0 ? "+" : "")}{v:0.#} {rating}: {why}"); }
        if (f.Hull == null || f.Kind == "munition") return new(r, 0, notes);

        var half = TopHalf(f, d);
        var full = Full(half);
        var std = Area(Full(HullGeometry.Outline(f.Hull)));
        var area = Area(full);
        var ratio = std > 0 ? area / std : 1;
        var n = half.Count;
        var dirs = Directions(half, closed: false);
        var side = Side(f, d);
        var sideDirs = Directions(side, closed: true);

        // stealth: aligned, simple outlines help; cluttered or oversized ones hurt
        if (dirs <= 3) Add("signature", 1, $"top outline uses only {dirs} edge directions");
        else if (dirs >= 7) Add("signature", -1, $"top outline has {dirs} edge directions");
        if (n > 12) Add("signature", -1, $"{n} outline vertices (cluttered)");
        if (sideDirs <= 3) Add("signature", 0.5, "clean side profile");
        else if (sideDirs >= 8) Add("signature", -0.5, $"side profile has {sideDirs} edge directions");
        if (ratio > 1.8) Add("signature", -2, $"outline {ratio:P0} of the standard size");
        else if (ratio > 1.3) Add("signature", -1, $"outline {ratio:P0} of the standard size");
        if (f.Kind == "aircraft" && f.Hull.Width > 30) Add("signature", -1, $"very large airframe ({f.Hull.Width:0} m span)");

        // bigger outline = more structure to carry
        var mass = ratio > 1 ? (f.Budgets.GetValueOrDefault("mass") * 0.25) * (ratio - 1) : 0;
        if (mass > 0) notes.Add($"+{mass:0.#} t structure: outline {ratio:P0} of standard");

        if (f.Kind == "aircraft")
        {
            var span = full.Max(p => p.X) * 2;
            var aspect = area > 0 ? span * span / area : 0;
            if (aspect > 8) Add("endurance", 1, $"high aspect ratio ({aspect:0.#})");
            else if (aspect < 3) Add("endurance", -1, $"low aspect ratio ({aspect:0.#})");
            // leading-edge sweep: the edge reaching the wingtip from the vertex before it
            var tip = half.IndexOf(half.OrderByDescending(p => p.X).First());
            if (tip > 0)
            {
                var (a, b) = (half[tip - 1], half[tip]);
                var sweep = Math.Abs(Math.Atan2(a.Y - b.Y, b.X - a.X) * 180 / Math.PI);
                if (sweep > 45) Add("speed", 1, $"leading edge swept {sweep:0}°");
                else if (sweep < 15) Add("speed", -1, $"straight wing ({sweep:0}° sweep)");
            }
            var tails = d.Parts.Any(p => p.Part.Contains("tail") || p.Part.Contains("canard"));
            if (!tails) Add("agility", -1, "no tail or canard surfaces fitted");
            if (n <= 5) Add("agility", -1, $"only {n} outline points: crude planform, poor handling");
        }
        else if (f.Kind == "ship")
        {
            var beam = full.Max(p => p.X) * 2;
            var len = full.Max(p => p.Y) - full.Min(p => p.Y);
            var lb = beam > 0 ? len / beam : 0;
            if (lb > 8) Add("speed", 1, $"slender hull (L/B {lb:0.#})");
            else if (lb < 4.5) Add("speed", -1, $"beamy hull (L/B {lb:0.#})");
        }
        return new(r, mass, notes);
    }
}
