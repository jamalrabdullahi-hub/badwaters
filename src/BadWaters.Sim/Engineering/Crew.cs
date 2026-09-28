namespace BadWaters.Sim.Engineering;

// Crew requirements: a simple game rule. The hull needs a base crew that grows with its size, each part needs
// crew by its category and weight (bigger parts need more), accommodation parts provide berths, and the hull
// comes with some built-in berths. Automation research trims the requirement. Uncrewed frames need none.

/// <summary>One entry of data/core/engineering/crew.json: "hull:kind", "cat:Category", "uncrewed" or "automation".</summary>
public sealed record CrewRule
{
    public required string Id { get; init; }
    public double Base { get; init; }
    public double PerTonne { get; init; }
    /// <summary>Hull rules: built-in berths as a multiple of the hull crew.</summary>
    public double Berths { get; init; }
    public string[] Types { get; init; } = Array.Empty<string>();
    public string Field { get; init; } = "";
    public int From { get; init; }
    public double PerLevel { get; init; }
    public double Max { get; init; }
}

public static class CrewRules
{
    public static bool Uncrewed(FrameDef f, Catalog cat) =>
        cat.Crew.TryGetValue("uncrewed", out var u) && u.Types.Contains(f.Type);

    public static CrewRule? HullRule(FrameDef f, Catalog cat) =>
        f.Hull?.Style == "sub" && cat.Crew.TryGetValue("hull:sub", out var s) ? s : cat.Crew.GetValueOrDefault("hull:" + f.Kind);

    /// <summary>Hull crew: base + per tonne of the frame's weight budget.</summary>
    public static double HullCrew(FrameDef f, Catalog cat)
    {
        if (Uncrewed(f, cat)) return 0;
        var r = HullRule(f, cat);
        return r == null ? 0 : Math.Round(r.Base + r.PerTonne * f.Budgets.GetValueOrDefault("mass"));
    }

    /// <summary>Crew a part needs from its category and (already size-scaled) weight; bigger parts need more.</summary>
    public static double PartCrew(VisualPart p, double mass, double scale, Catalog cat) =>
        cat.Crew.TryGetValue("cat:" + p.Category, out var r) ? r.Base + r.PerTonne * Math.Max(0, mass) / Math.Sqrt(Math.Max(0.1, scale)) : 0;

    /// <summary>Multiplier on crew needed from automation research (1 = none).</summary>
    public static double Automation(Catalog cat, IReadOnlyDictionary<string, int>? tech)
    {
        if (tech == null || !cat.Crew.TryGetValue("automation", out var a)) return 1;
        return 1 - Math.Min(a.Max, a.PerLevel * Math.Max(0, tech.GetValueOrDefault(a.Field) - a.From));
    }
}
