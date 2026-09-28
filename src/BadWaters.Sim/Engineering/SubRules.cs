namespace BadWaters.Sim.Engineering;

/// <summary>
/// Submarine rules, simple and deterministic: a sub needs a pressure hull, ballast and a propulsor; quieting adds
/// to stealth; anything sticking up from the casing other than the sail and masts costs a little quieting.
/// Depth, quieting and submerged endurance are abstract game ratings.
/// </summary>
public static class SubRules
{
    public static bool IsSub(FrameDef f) => f.Kind == "ship" && f.Hull?.Style == "sub";

    public static void Apply(FrameDef f, DesignDef d, Catalog cat, Dictionary<string, double> ratings, List<Issue> issues)
    {
        if (!IsSub(f) || d.Parts.Count == 0) return;
        var parts = d.Parts.Select(pl => cat.Parts.GetValueOrDefault(pl.Part)).Where(p => p != null).Select(p => p!).ToList();
        bool Has(Func<VisualPart, bool> q) => parts.Any(q);
        if (!Has(p => p.Category == "Pressure hull" && p.Adds.ContainsKey("depth"))) issues.Add(new(IssueKind.MissingInterface, "Submarine has no pressure hull section."));
        if (!Has(p => p.Category == "Ballast")) issues.Add(new(IssueKind.MissingInterface, "Submarine has no ballast tanks."));
        if (!Has(p => p.Id is "sub-propulsor" or "pump-jet")) issues.Add(new(IssueKind.MissingInterface, "Submarine has no propulsor."));
        // casing clutter: topside parts that aren't sails or masts
        var clutter = parts.Count(p => p.Topside && p.Category is not ("Sub masts" or "Superstructure" or "Submarine fittings"));
        if (clutter > 0) ratings["quieting"] = ratings.GetValueOrDefault("quieting") - 0.5 * clutter;
        if (ratings.TryGetValue("quieting", out var q)) ratings["signature"] = ratings.GetValueOrDefault("signature") + 0.5 * q;
    }
}
