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
}

public enum IssueKind { OverBudget, NoPort, MissingInterface, TechTooLow, WrongKind, UnknownPart }
public sealed record Issue(IssueKind Kind, string Text);

public sealed record Evaluation(
    IReadOnlyDictionary<string, (double Used, double Available)> Budgets,
    IReadOnlyDictionary<string, double> Ratings,
    IReadOnlyList<Issue> Issues,
    double Cost,
    IReadOnlyDictionary<string, double> ProductionWork)
{
    public bool Valid => Issues.Count == 0;
}
