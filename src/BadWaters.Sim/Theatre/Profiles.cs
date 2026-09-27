using BadWaters.Sim.Engineering;

namespace BadWaters.Sim.Theatre;

/// <summary>One component's share of a rating, so the UI can show which parts are doing the work.</summary>
public sealed record Contribution(string Source, string Rating, double Value);

/// <summary>
/// A design as the theatre sees it: its evaluated game ratings plus where each rating came from.
/// Built from any design with the generic evaluator; no unit-specific code.
/// </summary>
public sealed class SystemProfile
{
    public required string DesignId { get; init; }
    public required string Name { get; init; }
    public IReadOnlyDictionary<string, double> Ratings { get; init; } = new Dictionary<string, double>();
    public IReadOnlyList<Contribution> Contributions { get; init; } = Array.Empty<Contribution>();

    public double R(string rating) => Ratings.TryGetValue(rating, out var v) ? v : 0;

    /// <summary>Components that add to a rating, strongest first.</summary>
    public IEnumerable<Contribution> For(string rating) =>
        Contributions.Where(c => c.Rating == rating).OrderByDescending(c => Math.Abs(c.Value));

    public string Sources(string rating)
    {
        var s = For(rating).Select(c => c.Source).Distinct().ToList();
        return s.Count == 0 ? "none fitted" : string.Join(", ", s);
    }

    public static SystemProfile Build(Catalog cat, DesignDef d)
    {
        var ev = new Evaluator(cat).Evaluate(d);
        var contrib = new List<Contribution>();
        if (cat.Frames.TryGetValue(d.Frame, out var f))
            foreach (var (r, v) in f.Base) contrib.Add(new(f.Name, r, v));
        foreach (var id in d.Components)
            if (cat.Components.TryGetValue(id, out var c))
                foreach (var (r, v) in c.Adds) contrib.Add(new(c.Name, r, v));
        return new SystemProfile { DesignId = d.Id, Name = d.Name, Ratings = ev.Ratings, Contributions = contrib };
    }
}

/// <summary>
/// Game pacing tables that turn 0–10 ratings into theatre distances and times. These are tuning
/// knobs for play, not real-world figures.
/// </summary>
public static class Pacing
{
    public static double SpeedKmPerSec(double speed) => 0.25 * Math.Max(1, speed);
    /// <summary>Strike reach: tiers 1–8 are theatre ranges; 9–10 are the long-range tiers (medium-range missiles, long-endurance drones).</summary>
    public static double StrikeReachKm(double reach) => 40 + 80 * reach + Math.Max(0, reach - 8) * 400;
    public static double InterceptReachKm(double reach) => 20 + 20 * reach;
    public static double RadarRangeKm(double detection) => 80 + 45 * detection;

    public static double DistanceKm(double lon1, double lat1, double lon2, double lat2)
    {
        var mid = (lat1 + lat2) / 2 * Math.PI / 180;
        var dx = (lon2 - lon1) * 111.0 * Math.Cos(mid);
        var dy = (lat2 - lat1) * 111.0;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
