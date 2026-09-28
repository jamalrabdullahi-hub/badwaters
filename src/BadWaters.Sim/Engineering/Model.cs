namespace BadWaters.Sim.Engineering;

// Generic engineering model. Nothing here knows about any specific ship, aircraft or missile:
// frames provide budgets and ports, components consume budgets and add capability ratings.
// All numbers are game-balanced values, not real-world data (see CLAUDE.md).

/// <summary>The 18 engineering families shared by every system type.</summary>
public static class Families
{
    public static readonly string[] All =
    {
        "structure", "materials", "power", "propulsion", "navigation", "guidance", "sensors", "computing",
        "comms", "ew", "payload", "fuzing", "thermal", "interfaces", "reliability", "manufacturing", "testing", "doctrine",
    };
}

/// <summary>A resource axis a frame provides and components consume (mass, volume, power, cooling, crew, data...).</summary>
public sealed record BudgetAxis(string Id, string Name, string Unit);

public sealed record ComponentTemplate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Family { get; init; }
    /// <summary>Frame kinds this component may go into: "ship", "aircraft", "munition", "ground".</summary>
    public string[] FitsKinds { get; init; } = Array.Empty<string>();
    /// <summary>Tech field levels required, e.g. { "ir-systems": 8 }.</summary>
    public Dictionary<string, int> Requires { get; init; } = new();
    /// <summary>Budget consumption per axis (negative values provide, e.g. a generator gives power).</summary>
    public Dictionary<string, double> Uses { get; init; } = new();
    /// <summary>Capability contributions, e.g. { "detection": 3 }.</summary>
    public Dictionary<string, double> Adds { get; init; } = new();
    /// <summary>Port type this component needs on its frame, e.g. "mast-sensor".</summary>
    public string? Port { get; init; }
    /// <summary>Ports this component itself offers to others (e.g. a combat computer offers "datalink-bus").</summary>
    public string[] Offers { get; init; } = Array.Empty<string>();
    /// <summary>Other port types that must exist somewhere on the design (e.g. a seeker needs "fire-control").</summary>
    public string[] Needs { get; init; } = Array.Empty<string>();
    public double Cost { get; init; }
    public string? Factory { get; init; }
    public double WorkUnits { get; init; }
    public Dictionary<string, double> Inputs { get; init; } = new();
    public double Reliability { get; init; } = 5;
}

public sealed record FrameDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; } // ship | aircraft | munition | ground
    public string Type { get; init; } = "";    // e.g. "destroyer", "frigate", "strike-fighter", "launcher-vehicle"
    public Dictionary<string, double> Budgets { get; init; } = new();
    /// <summary>Port type → number of slots, e.g. { "cell-block": 2, "mast-sensor": 1 }.</summary>
    public Dictionary<string, int> Ports { get; init; } = new();
    public Dictionary<string, double> Base { get; init; } = new();
    public Dictionary<string, int> Requires { get; init; } = new();
    public double Cost { get; init; }
    public string? Factory { get; init; }
    public double WorkUnits { get; init; }
    /// <summary>Outline for the designer's blueprint (ships and ground systems).</summary>
    public HullDef? Hull { get; init; }
}

/// <summary>Hull outline: length and beam in metres and a drawing style (warship, merchant, carrier, sub, vehicle, tracked, pad).</summary>
public sealed record HullDef(double Length, double Beam, string Style = "warship", double Span = 0)
{
    /// <summary>Widest extent (beam, or wingspan / fin span).</summary>
    public double Width => Math.Max(Beam, Span);
}

/// <summary>One primitive of a part's low-res drawing, in metres, top-down (front = +y). t: rect | circle | poly | line.</summary>
public sealed record ShapePrim
{
    public string T { get; init; } = "rect";
    public double X { get; init; }
    public double Y { get; init; }
    public double W { get; init; }
    public double H { get; init; }
    public double R { get; init; }
    public double[] P { get; init; } = Array.Empty<double>();
    public string Fill { get; init; } = "steel";
}

/// <summary>
/// A visual part you drag onto a design (gun mount, VLS block, mast, funnel, deckhouse...). It has its own
/// game stats and may bring a component's stats with it. Topside parts count against the topside budget.
/// </summary>
public sealed record VisualPart
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Category { get; init; } = "";
    public string[] FitsKinds { get; init; } = Array.Empty<string>();
    public List<ShapePrim> Shapes { get; init; } = new();
    public Dictionary<string, double> Uses { get; init; } = new();
    public Dictionary<string, double> Adds { get; init; } = new();
    public double Cost { get; init; }
    public bool Topside { get; init; }
    public string? Component { get; init; }
    public string[] Offers { get; init; } = Array.Empty<string>();
    /// <summary>Firing arc either side of the direction the part faces, degrees (0 = no arc).</summary>
    public double Arc { get; init; }
    /// <summary>Height class for arc blocking; -1 = from the category (see ArcGeometry.Height).</summary>
    public double Height { get; init; } = -1;

    /// <summary>Radius of the part's drawing, metres (for picking and bounds).</summary>
    public double Extent => Shapes.Count == 0 ? 1 : Shapes.Max(s => s.T switch
    {
        "circle" => Math.Sqrt(s.X * s.X + s.Y * s.Y) + s.R,
        "rect" => Math.Sqrt(Math.Pow(Math.Abs(s.X) + s.W / 2, 2) + Math.Pow(Math.Abs(s.Y) + s.H / 2, 2)),
        _ => s.P.Chunk(2).Max(c => Math.Sqrt(c[0] * c[0] + c[1] * c[1])),
    });
}

/// <summary>A part placed on a design: position in metres from the hull centre (x to starboard, y to the bow), rotation in degrees.</summary>
public sealed record Placement(string Part, double X, double Y, double Rot = 0);

/// <summary>Hull outline shared by the evaluator (bounds checks) and the designer (drawing).</summary>
public static class HullGeometry
{
    /// <summary>Half-width at y (metres from centre, +y = bow) as a fraction of half-beam.</summary>
    public static double Profile(string style, double length, double y)
    {
        var t = Math.Clamp((y + length / 2) / length, 0, 1); // 0 = stern, 1 = bow
        return style switch
        {
            "sub" => Math.Pow(Math.Max(0, 1 - Math.Pow(2 * t - 1, 2)), 0.35),
            "merchant" => t < 0.12 ? 0.9 + t : t < 0.8 ? 1 : Math.Max(0.25, 1 - Math.Pow((t - 0.8) / 0.2, 2) * 0.75),
            "tumblehome" => t < 0.15 ? 0.85 + t : t < 0.5 ? 1 : Math.Max(0, 1 - (t - 0.5) / 0.5),
            "boat" => t < 0.1 ? 0.9 : t < 0.6 ? 1 : Math.Max(0, Math.Cos((t - 0.6) / 0.4 * Math.PI / 2)),
            "vehicle" or "tracked" or "pad" => 1,
            _ => t < 0.2 ? 0.78 + 1.1 * t : t < 0.58 ? 1 : Math.Max(0, 1 - Math.Pow((t - 0.58) / 0.42, 1.6)),
        };
    }

    /// <summary>Right-hand half of the outline (x ≥ 0) from the bow/nose (y = +L/2) to the stern/tail.</summary>
    public static List<(double X, double Y)> Outline(HullDef h)
    {
        double L = h.Length, S = h.Span > 0 ? h.Span : h.Beam, fw = h.Beam / 2;
        List<(double, double)> P(params (double, double)[] pts) => pts.ToList();
        switch (h.Style)
        {
            case "fighter":
                return P((0, .5 * L), (fw, .3 * L), (fw, .08 * L), (S / 2, -.16 * L), (S / 2, -.25 * L), (fw, -.22 * L), (fw * .9, -.36 * L), (S * .3, -.45 * L), (S * .3, -.5 * L), (fw * .6, -.5 * L));
            case "delta":
                return P((0, .5 * L), (fw, .25 * L), (fw, .05 * L), (S / 2, -.38 * L), (S / 2, -.46 * L), (fw, -.46 * L), (fw * .7, -.5 * L));
            case "stealth":
                return P((0, .5 * L), (fw * 1.1, .3 * L), (fw * 1.3, .1 * L), (S / 2, -.17 * L), (S / 2, -.27 * L), (fw * 1.3, -.25 * L), (S * .28, -.42 * L), (S * .28, -.5 * L), (fw, -.47 * L));
            case "flying-wing":
                return P((0, .5 * L), (S / 2, -.2 * L), (S / 2 * .92, -.5 * L), (S * .3, -.36 * L), (S * .16, -.5 * L), (0, -.38 * L));
            case "large":
                return P((0, .5 * L), (fw, .44 * L), (fw, .12 * L), (S / 2, -.1 * L), (S / 2, -.16 * L), (fw, -.05 * L), (fw, -.36 * L), (S * .2, -.45 * L), (S * .2, -.5 * L), (fw * .4, -.5 * L));
            case "hale":
                return P((0, .5 * L), (fw, .42 * L), (fw, .08 * L), (S / 2, .04 * L), (S / 2, -.02 * L), (fw, -.04 * L), (fw * .6, -.42 * L), (S * .12, -.5 * L), (fw * .3, -.5 * L));
            case "male":
                return P((0, .5 * L), (fw, .38 * L), (fw, .12 * L), (S / 2, .06 * L), (S / 2, -.04 * L), (fw, -.06 * L), (fw * .5, -.4 * L), (S * .16, -.5 * L), (fw * .3, -.5 * L));
            case "canard-delta":
                return P((0, .5 * L), (fw, .3 * L), (S * .28, .12 * L), (fw, .1 * L), (fw, .02 * L), (S / 2, -.38 * L), (S / 2, -.46 * L), (fw, -.46 * L), (fw * .7, -.5 * L));
            case "trimaran":
                return P((0, .5 * L), (.15 * S, .2 * L), (.15 * S, .05 * L), (.5 * S, .02 * L), (.5 * S, -.45 * L), (.3 * S, -.5 * L), (0, -.5 * L));
            case "catamaran":
                return P((0, .3 * L), (.28 * S, .5 * L), (.5 * S, .35 * L), (.5 * S, -.5 * L), (0, -.5 * L));
            case "uav":
                return P((0, .5 * L), (fw, .4 * L), (fw, .1 * L), (S / 2, .05 * L), (S / 2, -.05 * L), (fw, -.08 * L), (fw * .6, -.4 * L), (S * .18, -.46 * L), (S * .18, -.5 * L), (fw * .3, -.5 * L));
            case "missile":
                var r = fw;
                return P((0, .5 * L), (r * .45, .5 * L - r), (r * .85, .5 * L - 2 * r), (r, .5 * L - 3.2 * r), (r, -.5 * L + .12 * L), (S / 2, -.5 * L + .03 * L), (S / 2, -.5 * L), (r, -.5 * L));
            default:
                var n = 48;
                return Enumerable.Range(0, n + 1).Select(i => L / 2 - L * i / n).Select(y => (h.Beam / 2 * Profile(h.Style, L, y), y)).ToList();
        }
    }

    /// <summary>Is the point on the outline (with a margin in metres)?</summary>
    public static bool Inside(HullDef h, double x, double y, double margin = 0.5) => Inside(Outline(h), x, y, margin);

    public static bool Inside(List<(double X, double Y)> half, double x, double y, double margin = 0.5)
    {
        var poly = half.Concat(half.AsEnumerable().Reverse().Select(p => (X: -p.X, p.Y))).ToList();
        bool In(double px, double py)
        {
            var inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].Y > py) != (poly[j].Y > py) && px < (poly[j].X - poly[i].X) * (py - poly[i].Y) / (poly[j].Y - poly[i].Y + 1e-12) + poly[i].X) inside = !inside;
            return inside;
        }
        if (In(x, y)) return true;
        // within the margin of the outline: try a few nudged points
        foreach (var (dx, dy) in new[] { (margin, 0.0), (-margin, 0.0), (0.0, margin), (0.0, -margin) }) if (In(x + dx, y + dy)) return true;
        return false;
    }
}

/// <summary>How a capability rating aggregates across components: "sum", "max", "min".</summary>
public sealed record RatingRule(string Id, string Name, string Aggregate, double Cap = 10);

public sealed record DesignDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Frame { get; init; }
    public List<string> Components { get; init; } = new();
    public string Lineage { get; init; } = "";
    /// <summary>Visual parts placed on the blueprint.</summary>
    public List<Placement> Parts { get; init; } = new();
    /// <summary>Cosmetic outline override: right-half vertices [x0, y0, x1, y1, ...] in metres. Budgets and bounds ignore it.</summary>
    public List<double> Outline { get; init; } = new();
    /// <summary>Side profile override: full polygon [y0, z0, y1, z1, ...] in metres (y along the length to the bow, z up).</summary>
    public List<double> Side { get; init; } = new();
}

public enum IssueKind { OverBudget, NoPort, MissingInterface, TechTooLow, WrongKind, UnknownPart }
public sealed record Issue(IssueKind Kind, string Text);

public sealed record Evaluation(
    IReadOnlyDictionary<string, (double Used, double Available)> Budgets,
    IReadOnlyDictionary<string, double> Ratings,
    IReadOnlyList<Issue> Issues,
    double Cost,
    IReadOnlyDictionary<string, double> ProductionWork,
    IReadOnlyDictionary<string, (double Weight, double Cost)>? Breakdown = null,
    IReadOnlyList<string>? Notes = null)
{
    public bool Valid => Issues.Count == 0;
}
