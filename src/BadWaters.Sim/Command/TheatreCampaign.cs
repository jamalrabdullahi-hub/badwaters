using BadWaters.Sim.Economy;
using BadWaters.Sim.Theatre;

namespace BadWaters.Sim.Command;

/// <summary>How a theatre at war is resolved each strategic turn.</summary>
public enum TheatreMode { Auto, Fight }

/// <summary>Compact persistent state of a theatre (what survives a save): losses, damage, stocks, ships, relocations.</summary>
public sealed class TheatreSnapshot
{
    public string Scenario { get; set; } = "";
    public double Time { get; set; }
    public List<string> DestroyedUnits { get; set; } = new();
    public Dictionary<string, string> UnitBases { get; set; } = new();
    public Dictionary<string, double> Components { get; set; } = new();      // "site/component" → integrity
    public Dictionary<string, int> LauncherStock { get; set; } = new();
    public Dictionary<string, int> BatteryStock { get; set; } = new();
    public Dictionary<string, double[]> Ships { get; set; } = new();        // id → [hp, lon, lat]
    public int DayWrittenBack { get; set; }
    /// <summary>Logistics: node stocks (loads in transit are counted as delivered), aircraft store magazines, route damage.</summary>
    public Dictionary<string, Dictionary<string, double>> Supply { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> StoreMagazines { get; set; } = new();
    public Dictionary<string, double> RouteDamage { get; set; } = new();
}

/// <summary>
/// Persistent theatres for the strategic game. A theatre at war keeps one live simulation across turns, run by its
/// two commanders. Each strategic day it is either auto-resolved headless or waits for the player to fight it, and
/// its outcome writes back into the strategic economy: aircraft losses leave the nation's fielded force, rounds fired
/// are gone, and launchers are resupplied from munitions the economy actually produced.
/// </summary>
public sealed class TheatreCampaign
{
    private readonly Func<string, ScenarioDef?> _scenarioFor;
    private readonly Func<ScenarioDef, int, InterceptTrial> _build;
    private readonly Func<string, string?> _nationOfSideName;
    public Dictionary<string, InterceptTrial> Live { get; } = new();
    public Dictionary<string, TheatreMode> Mode { get; } = new();
    /// <summary>Strategic days each theatre has been at war (the theatre clock must reach Days × 86400).</summary>
    public Dictionary<string, int> Days { get; } = new();
    public List<string> Reports { get; } = new();
    public const double Day = 86400;
    /// <summary>Coarse step for headless auto-resolve when no missile is in the air.</summary>
    public double AutoStep { get; set; } = 60;
    /// <summary>Economy regions (national fuel and spares stocks for theatre logistics).</summary>
    public EconomyGeo? Geo { get; set; }
    /// <summary>The persistent world state (World.Forces): a zone built fresh starts from it, and writes back into it.</summary>
    public Func<TheatreSnapshot>? WorldForces { get; set; }

    public TheatreCampaign(Func<string, ScenarioDef?> scenarioFor, Func<ScenarioDef, int, InterceptTrial> build, Func<string, string?> nationOfSideName)
    { _scenarioFor = scenarioFor; _build = build; _nationOfSideName = nationOfSideName; }

    /// <summary>The live theatre for a strategic theatre id, created on first use (from its scenario, or a saved snapshot).</summary>
    public InterceptTrial? Get(string theatre, TheatreSnapshot? snap = null)
    {
        if (Live.TryGetValue(theatre, out var t)) return t;
        var sc = _scenarioFor(theatre);
        if (sc == null) return null;
        t = _build(sc, 1);
        t.Adaptive = true;
        if (snap != null) Restore(t, snap);
        else if (WorldForces?.Invoke() is { } wf) Restore(t, wf, time: false); // semi-persistent: the world remembers
        Live[theatre] = t;
        Mode.TryAdd(theatre, TheatreMode.Auto);
        return t;
    }

    /// <summary>
    /// One strategic day for every theatre at war. Auto: the day is fought headless. Fight: returns the theatre id if the
    /// player still has to fight it up to today (the strategic clock should stop and offer Theatre Ops or auto-resolve).
    /// </summary>
    public string? Daily(World w, IEnumerable<string> theatresAtWar)
    {
        string? waiting = null;
        foreach (var id in theatresAtWar.OrderBy(x => x, StringComparer.Ordinal))
        {
            var t = Get(id);
            if (t == null) continue;
            Days[id] = Days.GetValueOrDefault(id) + 1;
            var target = Days[id] * Day;
            if (Mode.GetValueOrDefault(id) == TheatreMode.Fight && t.Time < target - 60) { waiting ??= id; continue; }
            Resolve(t, target);
            WriteBack(w, t);
        }
        return waiting;
    }

    /// <summary>Auto-resolve a theatre up to a theatre time (both commanders fight; player operations still run).</summary>
    public void Resolve(InterceptTrial t, double until)
    {
        var fine = InterceptTrial.CoarseStep;
        t.CoarseSeconds = AutoStep;
        while (t.Time < until - 1e-6) t.Advance(Math.Min(3600, until - t.Time));
        t.CoarseSeconds = fine;
    }

    /// <summary>Theatre → economy: losses, expended rounds, resupply from national production.</summary>
    public void WriteBack(World w, InterceptTrial t)
    {
        Merge(w.Forces, Snapshot(t));
        var econ = w.Economy;
        foreach (var side in new[] { "blue", "red" })
        {
            var nation = _nationOfSideName(t.Scenario.Sides.GetValueOrDefault(side, ""));
            if (nation == null || !econ.Nations.TryGetValue(nation, out var ne)) continue;
            // the fielded force: every aircraft type in the theatre, by preset; losses come off it
            foreach (var g in t.Scenario.Units.Where(u => u.Side == side && u.Kind is "air" or "aew").GroupBy(u => u.Preset))
            {
                var key = "preset:" + g.Key;
                var alive = g.Count(u => t.Air?.Destroyed.Contains(u.Id) != true);
                var fe = ne.Fleet.TryGetValue(key, out var x) ? x : ne.Fleet[key] = new FleetEntry { Count = g.Count() };
                var lost = Math.Max(0, g.Count() - alive - (int)Math.Round(fe.Lost));
                if (lost > 0) { fe.Count = Math.Max(0, fe.Count - lost); fe.Lost += lost; Reports.Add($"{w.Now:yyyy-MM-dd} {nation}: {lost} {g.Key} lost in {t.Scenario.Name}"); }
            }
            // with theatre logistics, rear depots are topped up from national stockpiles (the logistics officer moves it forward)
            if (t.Logistics != null && Geo != null)
            {
                var got = t.Logistics.Replenish(side, ne, econ, Geo);
                if (got.Count > 0) Reports.Add($"{w.Now:yyyy-MM-dd} {nation}: national stocks to {t.Scenario.Name} depots: {string.Join(", ", got.Take(4))}{(got.Count > 4 ? "…" : "")}");
                continue;
            }
            // without logistics: launchers are resupplied straight from missiles the economy built
            foreach (var l in t.Launchers.Where(l => l.Def.Side == side))
            {
                var key = l.Def.Missile;
                if (!ne.Fleet.TryGetValue(key, out var stock) || stock.Count < 1) continue;
                var want = Math.Max(0, l.Def.Stock - l.Stock);
                var give = (int)Math.Min(want, Math.Floor(stock.Count));
                if (give <= 0) continue;
                l.Stock += give; stock.Count -= give;
                Reports.Add($"{w.Now:yyyy-MM-dd} {nation}: {give} {key} delivered to {l.Def.Name}");
            }
        }
    }

    // ---------------- persistence ----------------

    /// <summary>Store every live theatre in the world (call before saving).</summary>
    public void SaveInto(World w)
    {
        foreach (var (id, t) in Live) { var s = Snapshot(t); s.DayWrittenBack = Days.GetValueOrDefault(id); w.TheatreWars[id] = s; }
    }

    /// <summary>Rebuild live theatres from a loaded world.</summary>
    public void LoadFrom(World w)
    {
        Live.Clear();
        foreach (var (id, s) in w.TheatreWars) { Get(id, s); Days[id] = s.DayWrittenBack; }
    }

    public static TheatreSnapshot Snapshot(InterceptTrial t) => new()
    {
        Scenario = t.Scenario.Id, Time = t.Time,
        DestroyedUnits = t.Air?.Destroyed.ToList() ?? new(),
        UnitBases = t.Scenario.Units.Where(u => t.Air != null && t.Air.BaseOf(u) != u.Base).ToDictionary(u => u.Id, u => t.Air!.BaseOf(u)),
        Components = t.Bases.Values.SelectMany(b => b.Components.Select(c => (k: $"{b.Site.Id}/{c.Key}", v: c.Value.Integrity))).ToDictionary(x => x.k, x => x.v),
        LauncherStock = t.Launchers.ToDictionary(l => l.Def.Id, l => l.Stock),
        BatteryStock = t.Batteries.ToDictionary(b => b.Def.Id, b => b.Stock),
        Ships = t.Naval?.Ships.ToDictionary(s => s.Id, s => new[] { s.Hp, s.Lon, s.Lat }) ?? new(),
        Supply = t.Logistics?.Nodes.Values.ToDictionary(n => n.Id, n =>
        {
            var d = new Dictionary<string, double>(n.Stock);
            foreach (var sh in t.Logistics.Shipments.Where(x => x.To == n.Id && x.State is ShipmentState.Moving or ShipmentState.Queued && !x.Item.StartsWith("store:")))
                if (!sh.Item.StartsWith("design:") || n.Kind is not ("launcher" or "battery")) d[sh.Item] = d.GetValueOrDefault(sh.Item) + sh.Qty;
            return d;
        }) ?? new(),
        StoreMagazines = t.Air?.Munitions.ByBase.ToDictionary(k => k.Key, k => new Dictionary<string, int>(k.Value)) ?? new(),
        RouteDamage = t.Logistics?.Corridors.Values.Where(c => c.Damage > 0).ToDictionary(c => c.Def.Id, c => c.Damage) ?? new(),
    };

    /// <summary>Fold a zone's state into the world's (its assets overwrite; losses accumulate).</summary>
    public static void Merge(TheatreSnapshot into, TheatreSnapshot s)
    {
        foreach (var id in s.DestroyedUnits) if (!into.DestroyedUnits.Contains(id)) into.DestroyedUnits.Add(id);
        foreach (var (k, v) in s.UnitBases) into.UnitBases[k] = v;
        foreach (var (k, v) in s.Components) into.Components[k] = v;
        foreach (var (k, v) in s.LauncherStock) into.LauncherStock[k] = v;
        foreach (var (k, v) in s.BatteryStock) into.BatteryStock[k] = v;
        foreach (var (k, v) in s.Ships) into.Ships[k] = v;
        foreach (var (k, v) in s.Supply) into.Supply[k] = v;
        foreach (var (k, v) in s.StoreMagazines) into.StoreMagazines[k] = v;
        foreach (var (k, v) in s.RouteDamage) into.RouteDamage[k] = v;
    }

    public static void Restore(InterceptTrial t, TheatreSnapshot s, bool time = true)
    {
        if (time) t.SetTime(s.Time);
        if (t.Air != null)
        {
            foreach (var id in s.DestroyedUnits) t.Air.Destroyed.Add(id);
            foreach (var (u, b) in s.UnitBases) t.Air.SetBase(u, b);
        }
        foreach (var (k, v) in s.Components)
        {
            var parts = k.Split('/');
            if (t.Bases.TryGetValue(parts[0], out var b) && b.Components.TryGetValue(parts[1], out var c)) c.Integrity = v;
        }
        foreach (var l in t.Launchers) if (s.LauncherStock.TryGetValue(l.Def.Id, out var n)) l.Stock = n;
        foreach (var b in t.Batteries) if (s.BatteryStock.TryGetValue(b.Def.Id, out var n)) b.Stock = n;
        if (t.Naval != null)
            foreach (var sh in t.Naval.Ships) if (s.Ships.TryGetValue(sh.Id, out var v)) { sh.Hp = v[0]; sh.Lon = v[1]; sh.Lat = v[2]; }
        if (t.Logistics != null)
        {
            foreach (var (id, stock) in s.Supply)
                if (t.Logistics.Nodes.TryGetValue(id, out var n)) { n.Stock.Clear(); foreach (var (k, q) in stock) n.Stock[k] = q; }
            foreach (var (id, dmg) in s.RouteDamage) if (t.Logistics.Corridors.TryGetValue(id, out var c)) c.Damage = dmg;
        }
        if (t.Air != null && s.StoreMagazines.Count > 0)
            foreach (var (b, m) in s.StoreMagazines.Where(x => t.Bases.ContainsKey(x.Key)))
            {
                t.Air.Munitions.ByBase.Remove(b);
                foreach (var (k, q) in m) t.Air.Munitions.Add(b, k, q);
            }
    }
}
