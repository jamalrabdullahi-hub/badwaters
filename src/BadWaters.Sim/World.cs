using System.Text.Json;
using BadWaters.Sim.Industry;
using BadWaters.Sim.Time;
using BadWaters.Sim.Trade;

namespace BadWaters.Sim;

/// <summary>Why a strategic advance stopped early.</summary>
public sealed record Interrupt(DateTime At, string Kind, string Text, string? Theatre);

/// <summary>Per-theatre state. When the player is at war there, it becomes the live Theatre Ops view.</summary>
public sealed class TheatreState
{
    public required string Id { get; init; }
    public bool AtWar { get; set; }
    public bool PlayerInvolved { get; set; }
    public double HostileNavalThreat { get; set; } // 0..100
    public double FriendlySeaControl { get; set; } // 0..100
}

/// <summary>
/// The single source of truth. The Strategic Map and Theatre Ops are two views of this object;
/// neither owns separate state.
/// </summary>
public sealed class World
{
    public DateTime Now { get; set; }
    public DateTime End { get; set; }
    public string Player { get; set; } = "";
    public Dictionary<string, TheatreState> Theatres { get; set; } = new();
    public Dictionary<string, RouteState> Routes { get; set; } = new();
    public List<FactoryInstance> Factories { get; set; } = new();
    public Dictionary<string, double> Stock { get; set; } = new();
    public List<string> Log { get; set; } = new();
    public ulong Seed { get; set; } = 1;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, IncludeFields = false };
    public string Save() => JsonSerializer.Serialize(this, Json);
    public static World Load(string json) => JsonSerializer.Deserialize<World>(json, Json) ?? throw new InvalidDataException("Empty save.");
}

/// <summary>
/// Multi-rate scheduler on one clock. Strategic turns advance day by day (trade daily, industry monthly)
/// and stop early on an interrupt, e.g. when war breaks out in a theatre involving the player. Theatre
/// Ops advances the same world in seconds, and the daily processes still run as days pass.
/// </summary>
public sealed class Scheduler
{
    private readonly World _w;
    public Func<World, DateTime, Interrupt?>? DailyHook { get; set; } // events, AI, abstract ops
    public Action<World>? MonthlyHook { get; set; }                   // industry, budget, personnel

    public Scheduler(World w) => _w = w;

    public Interrupt? AdvanceStrategic(TurnLength len)
    {
        var target = GameClock.Next(_w.Now, len);
        if (target > _w.End) target = _w.End;
        while (_w.Now < target)
        {
            var irq = StepDay();
            if (irq != null) return irq;
        }
        return null;
    }

    /// <summary>Live Theatre Ops time. Crossing midnight runs the daily world processes too.</summary>
    public void AdvanceTheatreSeconds(double seconds)
    {
        var next = _w.Now.AddSeconds(seconds);
        while (next.Date > _w.Now.Date)
        {
            var midnight = _w.Now.Date.AddDays(1);
            var remaining = next - midnight;
            _w.Now = midnight.AddSeconds(-1);
            StepDay();
            next = _w.Now + remaining;
        }
        _w.Now = next;
    }

    private Interrupt? StepDay()
    {
        var before = _w.Now;
        _w.Now = _w.Now.Date.AddDays(1);
        // events first, so a war that starts today already shapes today's shipping
        if (_w.Now.Month != before.Month) MonthlyHook?.Invoke(_w);
        var irq = DailyHook?.Invoke(_w, _w.Now);
        Interrupt? routeIrq = null;
        foreach (var (id, r) in _w.Routes)
        {
            var hostile = 0.0; var friendly = 0.0; var war = 0.0;
            foreach (var t in _w.Theatres.Values.Where(t => RouteTouches(id, t.Id)))
            {
                hostile = Math.Max(hostile, t.HostileNavalThreat);
                friendly = Math.Max(friendly, t.FriendlySeaControl);
                if (t.AtWar) war = 100;
            }
            // storms along the route add a little disruption (simple weather, see Environment.Weather)
            if (RoutePaths.TryGetValue(id, out var path)) hostile += StormDisruption * BadWaters.Sim.Environment.Weather.StormAlong(path, _w.Now);
            var wasClosed = r.Status == RouteStatus.EffectivelyClosed;
            r.UpdateFromThreat(hostile, friendly, war);
            if (!wasClosed && r.Status == RouteStatus.EffectivelyClosed)
                routeIrq ??= new Interrupt(_w.Now, "route-closed", $"Trade route {id} is effectively closed.", null);
        }
        if (irq != null) return Record(irq);
        foreach (var t in _w.Theatres.Values)
            if (t.AtWar && t.PlayerInvolved)
                return Record(new Interrupt(_w.Now, "war", $"You are at war in {t.Id}. Theatre Ops is live.", t.Id));
        return routeIrq == null ? null : Record(routeIrq);
    }

    /// <summary>Route↔theatre membership is registered by the data layer.</summary>
    public Dictionary<string, HashSet<string>> RouteTheatres { get; } = new();
    /// <summary>Route waypoints [lon, lat] for weather; registered by the data layer (optional).</summary>
    public Dictionary<string, List<double[]>> RoutePaths { get; } = new();
    /// <summary>Disruption points added by a full-strength storm on the route.</summary>
    public const double StormDisruption = 12;
    private bool RouteTouches(string route, string theatre) => RouteTheatres.TryGetValue(route, out var s) && s.Contains(theatre);

    private Interrupt Record(Interrupt i) { _w.Log.Add($"{i.At:yyyy-MM-dd} {i.Text}"); return i; }
}
