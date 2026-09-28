namespace BadWaters.Sim.Engineering;

// Part options and sizing: a placed part can pick choices (gear type, engine type, radome shape...) and a
// size/ratio slider. Each choice multiplies or adds to the part's uses, ratings and cost; size scales weight,
// space, power and cooling with area, ratings roughly with size, and bigger parts are easier to see.
// Fixed game rules for trade-offs, not engineering.

public sealed record OptionChoice
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Multipliers on the part's uses per axis ("*" = every axis).</summary>
    public Dictionary<string, double> UsesMul { get; init; } = new();
    public Dictionary<string, double> UsesAdd { get; init; } = new();
    public Dictionary<string, double> Adds { get; init; } = new();
    public double CostMul { get; init; } = 1;
    /// <summary>Drawing stretch (width, length) for this choice.</summary>
    public double ShapeX { get; init; } = 1;
    public double ShapeY { get; init; } = 1;
    public string Note { get; init; } = "";
}

public sealed record PartOption
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public List<OptionChoice> Choices { get; init; } = new();
    public string Default { get; init; } = "";
}

/// <summary>How a size slider affects a part (label, range, which ratings grow with size, visibility penalty).</summary>
public sealed record PartScaling
{
    public string Label { get; init; } = "Size";
    public double Min { get; init; } = 0.6;
    public double Max { get; init; } = 1.6;
    /// <summary>Ratings that grow with size: rating → exponent (adds[r] *= size^exp).</summary>
    public Dictionary<string, double> Ratings { get; init; } = new();
    /// <summary>Stealth lost per +100% size (0 = none).</summary>
    public double Visibility { get; init; } = 1;
}

public sealed record ResolvedPart(Dictionary<string, double> Uses, Dictionary<string, double> Adds, double Cost, double ShapeX, double ShapeY, double Scale, List<string> Notes);

public static class PartResolver
{
    public static string Choice(VisualPart p, Placement pl, PartOption o) =>
        pl.Options.TryGetValue(o.Id, out var c) && o.Choices.Any(x => x.Id == c) ? c : o.Default != "" ? o.Default : o.Choices.FirstOrDefault()?.Id ?? "";

    /// <summary>The part's effective stats with its component, options and size applied.</summary>
    public static ResolvedPart Resolve(VisualPart p, Placement pl, Catalog cat)
    {
        var uses = new Dictionary<string, double>(p.Uses);
        var adds = new Dictionary<string, double>(p.Adds);
        double cost = p.Cost, sx = 1, sy = 1;
        var notes = new List<string>();
        if (p.Component != null && cat.Components.TryGetValue(p.Component, out var c))
        {
            foreach (var (k, v) in c.Uses) uses[k] = uses.GetValueOrDefault(k) + v;
            foreach (var (k, v) in c.Adds) adds[k] = adds.GetValueOrDefault(k) + v;
            cost += c.Cost;
        }
        foreach (var o in p.Options)
        {
            var ch = o.Choices.FirstOrDefault(x => x.Id == Choice(p, pl, o));
            if (ch == null) continue;
            foreach (var k in uses.Keys.ToList())
                if (ch.UsesMul.TryGetValue(k, out var m) || ch.UsesMul.TryGetValue("*", out m)) uses[k] *= m;
            foreach (var (k, v) in ch.UsesAdd) uses[k] = uses.GetValueOrDefault(k) + v;
            foreach (var (k, v) in ch.Adds) adds[k] = adds.GetValueOrDefault(k) + v;
            cost *= ch.CostMul;
            sx *= ch.ShapeX; sy *= ch.ShapeY;
            if (ch.Note != "") notes.Add($"{o.Name}: {ch.Name} — {ch.Note}");
        }
        var s = 1.0;
        if (p.Scaling != null)
        {
            s = Math.Clamp(pl.Scale, p.Scaling.Min, p.Scaling.Max);
            if (Math.Abs(s - 1) > 1e-6)
            {
                foreach (var k in new[] { "mass", "volume", "power", "cooling" }) if (uses.ContainsKey(k)) uses[k] *= uses[k] < 0 ? s * s : s * s; // output and demand both scale with area
                foreach (var k in adds.Keys.ToList()) if (adds[k] > 0) adds[k] *= Math.Pow(s, p.Scaling.Ratings.GetValueOrDefault(k, 1));
                foreach (var (k, e) in p.Scaling.Ratings) if (!adds.ContainsKey(k)) adds[k] = (Math.Pow(s, e) - 1) * 2;
                if (s > 1 && p.Scaling.Visibility > 0) adds["signature"] = adds.GetValueOrDefault("signature") - p.Scaling.Visibility * (s - 1);
                cost *= Math.Pow(s, 1.5);
                notes.Add($"{p.Scaling.Label} ×{s:0.00}");
            }
        }
        return new(uses, adds, cost, sx * s, sy * s, s, notes);
    }
}

// ---------------- component designer ----------------

public sealed record ArchetypeParam
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Kind { get; init; } = "slider"; // slider | choice
    public double Min { get; init; }
    public double Max { get; init; } = 1;
    public double Default { get; init; }
    public double Step { get; init; } = 0.1;
    /// <summary>Slider: linear effect per unit above the default.</summary>
    public Dictionary<string, double> Uses { get; init; } = new();
    public Dictionary<string, double> Adds { get; init; } = new();
    public double Cost { get; init; }
    /// <summary>Slider: multiplicative effect (value / default)^exp on uses.</summary>
    public Dictionary<string, double> ScaleUses { get; init; } = new();
    /// <summary>Slider: raises the tech requirement of this field by (value - default).</summary>
    public string TechField { get; init; } = "";
    public List<OptionChoice> Choices { get; init; } = new();
    public string DefaultChoice { get; init; } = "";
}

/// <summary>A designable component type (engine, radar array, launcher cell block...): base stats plus parameters.</summary>
public sealed record ComponentArchetype
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Family { get; init; }
    public string[] FitsKinds { get; init; } = Array.Empty<string>();
    public Dictionary<string, double> Uses { get; init; } = new();
    public Dictionary<string, double> Adds { get; init; } = new();
    public Dictionary<string, int> Requires { get; init; } = new();
    public double Cost { get; init; }
    public string? Factory { get; init; }
    public string? Port { get; init; }
    public string[] Offers { get; init; } = Array.Empty<string>();
    /// <summary>Visual part used as this component's drawing (scaled by a parameter named "size" if present).</summary>
    public string Visual { get; init; } = "";
    public string Category { get; init; } = "Custom";
    public bool Topside { get; init; }
    public List<ArchetypeParam> Params { get; init; } = new();
}

public static class ComponentBuilder
{
    /// <summary>Build a component from an archetype and parameter values (sliders by value, choices by index). Deterministic.</summary>
    public static ComponentTemplate Build(ComponentArchetype a, string id, string name, IReadOnlyDictionary<string, double> values)
    {
        var uses = new Dictionary<string, double>(a.Uses);
        var adds = new Dictionary<string, double>(a.Adds);
        var req = new Dictionary<string, int>(a.Requires);
        var cost = a.Cost;
        foreach (var p in a.Params)
        {
            if (p.Kind == "choice")
            {
                var idx = (int)values.GetValueOrDefault(p.Id, Math.Max(0, p.Choices.FindIndex(c => c.Id == p.DefaultChoice)));
                var ch = p.Choices[Math.Clamp(idx, 0, p.Choices.Count - 1)];
                foreach (var k in uses.Keys.ToList())
                    if (ch.UsesMul.TryGetValue(k, out var m) || ch.UsesMul.TryGetValue("*", out m)) uses[k] *= m;
                foreach (var (k, v) in ch.UsesAdd) uses[k] = uses.GetValueOrDefault(k) + v;
                foreach (var (k, v) in ch.Adds) adds[k] = adds.GetValueOrDefault(k) + v;
                cost *= ch.CostMul;
                continue;
            }
            var val = Math.Clamp(values.GetValueOrDefault(p.Id, p.Default), p.Min, p.Max);
            var d = val - p.Default;
            foreach (var (k, e) in p.ScaleUses) if (uses.ContainsKey(k) && p.Default != 0) uses[k] *= Math.Pow(val / p.Default, e);
            foreach (var (k, v) in p.Uses) uses[k] = uses.GetValueOrDefault(k) + v * d;
            foreach (var (k, v) in p.Adds) adds[k] = adds.GetValueOrDefault(k) + v * d;
            cost += p.Cost * d;
            if (p.TechField != "") req[p.TechField] = Math.Clamp(req.GetValueOrDefault(p.TechField) + (int)Math.Round(d), 1, 10);
        }
        foreach (var k in adds.Keys.ToList()) adds[k] = Math.Round(Math.Clamp(adds[k], -5, 10), 2);
        foreach (var k in uses.Keys.ToList()) uses[k] = Math.Round(uses[k], 3);
        return new ComponentTemplate
        {
            Id = id, Name = name, Family = a.Family, FitsKinds = a.FitsKinds, Requires = req, Uses = uses, Adds = adds,
            Cost = Math.Round(Math.Max(0.01, cost), 2), Factory = a.Factory, Port = a.Port, Offers = a.Offers,
        };
    }
}
