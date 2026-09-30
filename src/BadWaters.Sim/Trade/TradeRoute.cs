namespace BadWaters.Sim.Trade;

public enum DisruptionStep { Normal, Threatened, HighRisk, SeverelyDisrupted, Closed }

/// <summary>Headline map state, as defined by the design: open in peace, contested, or effectively closed.</summary>
public enum RouteStatus { FullyOpen, Contested, EffectivelyClosed }

public sealed record RouteDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>"sea" lane or "land" corridor (road and rail). Land corridors ignore naval threat.</summary>
    public string Mode { get; init; } = "sea";
    /// <summary>Waypoints as [lon, lat] pairs for drawing.</summary>
    public List<double[]> Path { get; init; } = new();
    /// <summary>Ids of <see cref="ChokepointDef"/>s the route passes; closing any of them closes the route.</summary>
    public string[] Chokepoints { get; init; } = Array.Empty<string>();
    public double Throughput { get; init; } = 100; // game units of goods per month at full flow
    public double TransitDays { get; init; } = 20;
}

/// <summary>A strait, canal or passage that can be shut (blockade, mining, closure). Critical ones are the world's key control points.</summary>
public sealed record ChokepointDef(string Id, string Name, double Lon, double Lat, string Kind = "strait", bool Critical = false);

public sealed class RouteState
{
    public double Disruption { get; set; } // 0..100

    public DisruptionStep Step => Disruption switch
    {
        < 15 => DisruptionStep.Normal,
        < 35 => DisruptionStep.Threatened,
        < 60 => DisruptionStep.HighRisk,
        < 85 => DisruptionStep.SeverelyDisrupted,
        _ => DisruptionStep.Closed,
    };

    /// <summary>Share of normal cargo that still gets through.</summary>
    public double DeliveryFactor => Step switch
    {
        DisruptionStep.Normal => 1.0,
        DisruptionStep.Threatened => 0.9,
        DisruptionStep.HighRisk => 0.65,
        DisruptionStep.SeverelyDisrupted => 0.3,
        _ => 0.0,
    };

    public double CostMultiplier => 1 + Disruption / 50.0;
    public double DelayMultiplier => 1 + Disruption / 40.0;

    /// <summary>
    /// Headline status. Fully open in peace; contested when fighting affects the route but it still runs
    /// (including when you are at war but dominate the sea); effectively closed when enemy naval power
    /// has shut it and cargo ships won't sail.
    /// </summary>
    public RouteStatus Status => Step switch
    {
        DisruptionStep.Normal => RouteStatus.FullyOpen,
        DisruptionStep.Closed or DisruptionStep.SeverelyDisrupted => RouteStatus.EffectivelyClosed,
        _ => RouteStatus.Contested,
    };

    /// <summary>
    /// Disruption from theatre threat: hostile naval/air threat in the route's theatres, reduced by
    /// friendly sea control (escorts, patrols, minesweeping). Inputs are 0..100 game ratings.
    /// </summary>
    public void UpdateFromThreat(double hostileThreat, double friendlyControl, double regionalWar, bool chokepointClosed = false)
    {
        // a shut chokepoint closes the route outright: ships re-route or stay in port
        if (chokepointClosed) { Disruption = 100; return; }
        var target = Math.Clamp(hostileThreat - 0.6 * friendlyControl + 0.3 * regionalWar, 0, 100);
        Disruption += (target - Disruption) * 0.5; // changes build over days, not instantly
    }
}
