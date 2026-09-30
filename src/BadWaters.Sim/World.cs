using System.Text.Json;
using BadWaters.Sim.Industry;
using BadWaters.Sim.Time;
using BadWaters.Sim.Trade;

namespace BadWaters.Sim;

/// <summary>Why a strategic advance stopped early.</summary>
public sealed record Interrupt(DateTime At, string Kind, string Text, string? Theatre);

/// <summary>
/// The single source of truth. The Strategic Map and Theatre Ops are two views of this object;
/// neither owns separate state.
/// </summary>
public sealed class World
{
    public DateTime Now { get; set; }
    public DateTime End { get; set; }
    public string Player { get; set; } = "";
    /// <summary>Wars between nations. There are no fixed theatres: each war fights where the two can reach each other.</summary>
    public List<BadWaters.Sim.Command.War> Wars { get; set; } = new();
    /// <summary>The persistent state of every military asset in the world (damage, losses, stocks, positions): fight
    /// zones are built from it and write their results back into it.</summary>
    public BadWaters.Sim.Command.TheatreSnapshot Forces { get; set; } = new();
    public bool AtWar(string a, string b) => Wars.Any(w => w.Involves(a) && w.Involves(b));
    public Dictionary<string, RouteState> Routes { get; set; } = new();
    /// <summary>Chokepoints currently shut (id → who shut it).</summary>
    public Dictionary<string, string> ClosedChokepoints { get; set; } = new();
    /// <summary>The national production economy (sites, stocks, deposits, nations).</summary>
    public BadWaters.Sim.Economy.EconomyState Economy { get; set; } = new();
    /// <summary>Saved theatre wars (live theatres are snapshotted here on save).</summary>
    public Dictionary<string, BadWaters.Sim.Command.TheatreSnapshot> TheatreWars { get; set; } = new();
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
            // threat from active fight zones the route passes through (set by the game from its wars)
            var (hostile, war) = RouteThreat?.Invoke(id) ?? (0, 0);
            var friendly = 0.0;
            var def = RouteDefs.GetValueOrDefault(id);
            var shut = def != null && def.Chokepoints.Any(c => _w.ClosedChokepoints.ContainsKey(c));
            if (def?.Mode == "land") hostile = 0; // land corridors: only a regional war disrupts them
            // storms along the route add a little disruption (simple weather, see Environment.Weather)
            if (def?.Mode != "land" && RoutePaths.TryGetValue(id, out var path)) hostile += StormDisruption * BadWaters.Sim.Environment.Weather.StormAlong(path, _w.Now);
            var wasClosed = r.Status == RouteStatus.EffectivelyClosed;
            r.UpdateFromThreat(hostile, friendly, war, shut);
            if (!wasClosed && r.Status == RouteStatus.EffectivelyClosed)
                routeIrq ??= new Interrupt(_w.Now, "route-closed", $"Trade route {id} is effectively closed.", null);
        }
        if (irq != null) return Record(irq);
        return routeIrq == null ? null : Record(routeIrq);
    }

    /// <summary>
    /// Shut or reopen a chokepoint (blockade, mining, political closure). Routes through it close at once;
    /// after reopening they recover over the following days like any other disruption.
    /// </summary>
    public void SetChokepoint(string id, bool closed, string by = "")
    {
        if (closed) _w.ClosedChokepoints[id] = by; else _w.ClosedChokepoints.Remove(id);
        foreach (var (rid, def) in RouteDefs)
            if (def.Chokepoints.Contains(id) && _w.Routes.TryGetValue(rid, out var r))
                r.Disruption = closed ? 100 : Math.Min(r.Disruption, 60);
        _w.Log.Add($"{_w.Now:yyyy-MM-dd} {id} {(closed ? "closed" : "reopened")}{(by != "" ? $" by {by}" : "")}.");
    }

    /// <summary>Hostile threat and war level (0..100) along a route, from the fight zones it crosses.</summary>
    public Func<string, (double Hostile, double War)>? RouteThreat { get; set; }
    /// <summary>Route waypoints [lon, lat] for weather; registered by the data layer (optional).</summary>
    public Dictionary<string, List<double[]>> RoutePaths { get; } = new();
    /// <summary>Route definitions (mode and chokepoints); registered by the data layer (optional).</summary>
    public Dictionary<string, RouteDef> RouteDefs { get; } = new();
    /// <summary>Disruption points added by a full-strength storm on the route.</summary>
    public const double StormDisruption = 12;

    private Interrupt Record(Interrupt i) { _w.Log.Add($"{i.At:yyyy-MM-dd} {i.Text}"); return i; }
}
