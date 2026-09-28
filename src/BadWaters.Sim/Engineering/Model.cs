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
public sealed record HullDef(double Length, double Beam, string Style = "warship");

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
            "vehicle" or "tracked" or "pad" => 1,
            _ => t < 0.2 ? 0.78 + 1.1 * t : t < 0.58 ? 1 : Math.Max(0, 1 - Math.Pow((t - 0.58) / 0.42, 1.6)),
        };
    }

    public static bool Inside(HullDef h, double x, double y, double margin = 0.5) =>
        Math.Abs(y) <= h.Length / 2 + margin && Math.Abs(x) <= h.Beam / 2 * Profile(h.Style, h.Length, y) + margin;
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
}

public enum IssueKind { OverBudget, NoPort, MissingInterface, TechTooLow, WrongKind, UnknownPart }
public sealed record Issue(IssueKind Kind, string Text);

public sealed record Evaluation(
    IReadOnlyDictionary<string, (double Used, double Available)> Budgets,
    IReadOnlyDictionary<string, double> Ratings,
    IReadOnlyList<Issue> Issues,
    double Cost,
    IReadOnlyDictionary<string, double> ProductionWork,
    IReadOnlyDictionary<string, (double Weight, double Cost)>? Breakdown = null)
{
    public bool Valid => Issues.Count == 0;
}
