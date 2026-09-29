namespace BadWaters.Sim.Theatre;

// Theatre logistics: what can be used has to exist, be stored somewhere and be moved there. Supplies sit in nodes
// (rear depots, bases, batteries, launchers, ships), are consumed by activity, and are moved over named corridors
// (road, rail, sea, air, helicopter) by a transport pool. Routes are shown, vehicles are only counted.
// Every number is game tuning (data/core/theatre/logistics.json), not real consumption or logistics data.

public sealed record SupplyItemDef
{
    public string Name { get; init; } = "";
    /// <summary>Display unit ("m3" is shown as litres).</summary>
    public string Unit { get; init; } = "";
    /// <summary>Transport mass per unit (tonnes).</summary>
    public double TonPerUnit { get; init; } = 1;
}

public sealed record TransportModeDef
{
    public string Name { get; init; } = "";
    /// <summary>Plural vehicle word for route readouts ("trucks", "trains").</summary>
    public string Vehicle { get; init; } = "";
    public double Cap { get; init; } = 25;
    public double Kmh { get; init; } = 50;
}

public sealed record UseRates
{
    /// <summary>Av fuel (m3) per aircraft sortie, by symbology class; "default" for the rest.</summary>
    public Dictionary<string, double> Sortie { get; init; } = new();
    public double ShipDieselDay { get; init; } = 40;
    public double GroundDieselDay { get; init; } = 2;
    public double BaseDieselDay { get; init; } = 6;
    public double AircraftSparesDay { get; init; } = 2;
    public double BatterySparesDay { get; init; } = 21;
    public double LauncherSparesDay { get; init; } = 8;
    public double ShipSparesDay { get; init; } = 15;
    public double PropellantPerRound { get; init; } = 1.5;
}

public sealed record EfficiencyDef
{
    /// <summary>Efficiency lost per 50 km a node sits away from any road or rail line.</summary>
    public double OffRoutePer50Km { get; init; } = 0.1;
    /// <summary>Altitude above which moving supplies gets harder, and the loss per km above it.</summary>
    public double AltStartM { get; init; } = 1500;
    public double PerKmAbove { get; init; } = 0.08;
    public double Min { get; init; } = 0.4;
}

public sealed record LogisticsDef
{
    public Dictionary<string, SupplyItemDef> Items { get; init; } = new();
    /// <summary>Transport mass per round: "store1..3" by store weight class, "design" for missiles and interceptors.</summary>
    public Dictionary<string, double> MunitionMass { get; init; } = new();
    public UseRates Use { get; init; } = new();
    /// <summary>Missile designs that burn liquid propellant (one fill per round launched).</summary>
    public List<string> Liquid { get; init; } = new();
    /// <summary>Starting stocks by node kind (depot, airbase, navalbase, garrison, battery, launcher, ship).</summary>
    public Dictionary<string, Dictionary<string, double>> Start { get; init; } = new();
    public Dictionary<string, TransportModeDef> Modes { get; init; } = new();
    /// <summary>Default transport pool per side when the strategic economy doesn't set one (vehicles per mode).</summary>
    public Dictionary<string, int> Pool { get; init; } = new();
    public EfficiencyDef Efficiency { get; init; } = new();
    /// <summary>Request resupply below this share of a node's baseline; sources never give below KeepShare.</summary>
    public double RequestShare { get; init; } = 0.4;
    public double KeepShare { get; init; } = 0.7;
}

/// <summary>Scenario-side logistics data written by tools/logistics-build.py.</summary>
public sealed record ScenarioLogistics
{
    public Dictionary<string, NodeTerrain> Nodes { get; init; } = new();
    public List<CorridorDef> Corridors { get; init; } = new();
}
public sealed record NodeTerrain(double Elev = 0, double OffRouteKm = 0, double Factor = 1);
public sealed record CorridorDef(string Id, string Name, string Mode, string Side, string A, string B, double Km);

public sealed class SupplyNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Side { get; init; }
    /// <summary>depot | airbase | navalbase | garrison | battery | launcher | ship</summary>
    public required string Kind { get; init; }
    public double Lon { get; set; }
    public double Lat { get; set; }
    public Dictionary<string, double> Stock { get; } = new();
    /// <summary>What the node is meant to hold (its starting load): requests and "don't cannibalise" work against it.</summary>
    public Dictionary<string, double> Baseline { get; } = new();
    public Dictionary<string, double> UsedToday { get; } = new();
    public Dictionary<string, double> UsedYesterday { get; } = new();
    public HashSet<string> Short { get; } = new();
    public bool Reserve => Kind == "depot";
    public double Elev { get; set; }
    public double OffRouteKm { get; set; }
    /// <summary>Delivery efficiency 0.4..1 from distance to routes and altitude.</summary>
    public double Factor { get; set; } = 1;
    public string BaseId { get; set; } = "";
}

public sealed class Corridor
{
    public required CorridorDef Def { get; init; }
    /// <summary>0..1 route damage from strikes (slows traffic, can destroy loads in transit); repairs over time.</summary>
    public double Damage { get; set; }
}

public enum ShipmentState { Queued, Moving, Delivered, Lost }

public sealed class Shipment
{
    public required int Id { get; init; }
    public required string Side { get; init; }
    public required string Item { get; init; }
    public double Qty { get; set; }
    public required string From { get; init; }
    public required string To { get; init; }
    public List<string> Path { get; } = new();
    public string Mode { get; set; } = "road";
    public int Vehicles { get; set; }
    public double Depart { get; set; }
    public double Arrive { get; set; }
    public double VehiclesBack { get; set; }
    public bool Player { get; init; }
    public ShipmentState State { get; set; } = ShipmentState.Queued;
    public string Note { get; set; } = "";
}

public sealed class LogisticsService
{
    private readonly InterceptTrial _t;
    private readonly Random _rng;
    public LogisticsDef Def { get; }
    public Dictionary<string, SupplyNode> Nodes { get; } = new();
    public Dictionary<string, Corridor> Corridors { get; } = new();
    public List<Shipment> Shipments { get; } = new();
    /// <summary>Transport vehicles per side and mode, and how many are out.</summary>
    public Dictionary<(string Side, string Mode), int> Pool { get; } = new();
    public Dictionary<(string Side, string Mode), int> InUse { get; } = new();
    /// <summary>AI logistics officer per side (on unless the player switches it off).</summary>
    public Dictionary<string, bool> Auto { get; } = new() { ["blue"] = true, ["red"] = true };
    private int _next = 1;
    private double _nextHour, _nextDay;

    public LogisticsService(InterceptTrial t, LogisticsDef def, int seed, IReadOnlyDictionary<(string, string), int>? pool = null)
    {
        _t = t; Def = def; _rng = new Random(seed * 71 + 9);
        var terrain = t.Scenario.Logistics?.Nodes ?? new();
        void Add(SupplyNode n)
        {
            if (terrain.TryGetValue(n.Id, out var tr)) { n.Elev = tr.Elev; n.OffRouteKm = tr.OffRouteKm; n.Factor = tr.Factor; }
            foreach (var (k, v) in def.Start.GetValueOrDefault(n.Kind) ?? new()) { n.Stock[k] = v; n.Baseline[k] = v; }
            Nodes[n.Id] = n;
        }
        foreach (var b in t.Bases.Values)
            Add(new SupplyNode { Id = b.Site.Id, Name = b.Site.Name, Side = b.Site.Side, Kind = b.Site.Type, Lon = b.Site.Lon, Lat = b.Site.Lat, BaseId = b.Site.Id });
        foreach (var b in t.Batteries.Where(b => !b.IsShip))
            Add(new SupplyNode { Id = b.Def.Id, Name = b.Def.Name, Side = b.Def.Side, Kind = "battery", Lon = b.Def.Lon, Lat = b.Def.Lat, BaseId = b.BaseId });
        foreach (var l in t.Launchers.Where(l => !l.IsShip))
            Add(new SupplyNode { Id = l.Def.Id, Name = l.Def.Name, Side = l.Def.Side, Kind = "launcher", Lon = l.Def.Lon, Lat = l.Def.Lat, BaseId = l.BaseId });
        foreach (var s in t.Naval?.Ships ?? new())
            Add(new SupplyNode { Id = s.Id, Name = s.Name, Side = s.Side, Kind = "ship", Lon = s.Lon, Lat = s.Lat });
        // reserve rounds: every hosted launcher and battery has one reload at its base; depots hold two per design in theatre
        foreach (var l in t.Launchers)
        {
            var key = "design:" + l.Def.Missile;
            if (l.BaseId != "" && Nodes.TryGetValue(l.BaseId, out var hb)) Seed(hb, key, l.Def.Stock);
            foreach (var d in Nodes.Values.Where(n => n.Kind == "depot" && n.Side == l.Def.Side)) Seed(d, key, 2 * l.Def.Stock);
        }
        foreach (var b in t.Batteries)
        {
            var key = "design:" + b.Def.Interceptor;
            if (b.BaseId != "" && Nodes.TryGetValue(b.BaseId, out var hb)) Seed(hb, key, b.Def.Stock);
            foreach (var d in Nodes.Values.Where(n => n.Kind == "depot" && n.Side == b.Def.Side)) Seed(d, key, 2 * b.Def.Stock);
        }
        // depots also hold aircraft stores: twice what the side's bases start with
        if (t.Air is { Stores: not null } air)
            foreach (var d in Nodes.Values.Where(n => n.Kind == "depot"))
                foreach (var (baseId, stores) in air.Munitions.ByBase.Where(x => t.Site(x.Key)?.Side == d.Side))
                    foreach (var (s, n) in stores) Seed(d, "store:" + s, n * 2.0 / Math.Max(1, Nodes.Values.Count(x => x.Kind == "depot" && x.Side == d.Side)));
        // routes: from the scenario (built from map data), else a simple road net between each node and its two nearest friends
        var defs = t.Scenario.Logistics?.Corridors ?? new();
        foreach (var c in defs) Corridors[c.Id] = new Corridor { Def = c };
        foreach (var n in Nodes.Values.Where(n => !Corridors.Values.Any(c => c.Def.A == n.Id || c.Def.B == n.Id)))
            foreach (var m in Nodes.Values.Where(m => m.Side == n.Side && m != n && m.Kind != "ship").OrderBy(m => Km(n, m)).Take(2))
            {
                var id = $"trk-{n.Id}-{m.Id}";
                Corridors[id] = new Corridor { Def = new CorridorDef(id, $"Supply track {n.Name[..Math.Min(12, n.Name.Length)]}", "road", n.Side, n.Id, m.Id, Km(n, m) * 1.3) };
            }
        foreach (var side in new[] { "blue", "red" })
            foreach (var (mode, count) in def.Pool)
                Pool[(side, mode)] = pool != null && pool.TryGetValue((side, mode), out var p) ? p : count;
        _nextHour = t.Time + 3600; _nextDay = t.Time + 86400;
    }

    private void Seed(SupplyNode n, string item, double qty) { n.Stock[item] = n.Stock.GetValueOrDefault(item) + qty; n.Baseline[item] = n.Baseline.GetValueOrDefault(item) + qty; }
    private static double Km(SupplyNode a, SupplyNode b) => Pacing.DistanceKm(a.Lon, a.Lat, b.Lon, b.Lat);

    // ---------------- stock access (launcher and battery magazines, base store magazines are the existing fields) ----------------

    public double Get(SupplyNode n, string item)
    {
        if (item.StartsWith("design:"))
        {
            if (n.Kind == "launcher" && _t.Launchers.FirstOrDefault(l => l.Def.Id == n.Id) is { } l && "design:" + l.Def.Missile == item) return l.Stock;
            if (n.Kind == "battery" && _t.Batteries.FirstOrDefault(b => b.Def.Id == n.Id) is { } b && "design:" + b.Def.Interceptor == item) return b.Stock;
        }
        if (item.StartsWith("store:") && n.Kind is "airbase" && _t.Air != null) return _t.Air.Munitions.At(n.Id, item[6..]);
        return n.Stock.GetValueOrDefault(item);
    }

    public void Add(SupplyNode n, string item, double qty)
    {
        if (item.StartsWith("design:"))
        {
            if (n.Kind == "launcher" && _t.Launchers.FirstOrDefault(l => l.Def.Id == n.Id) is { } l && "design:" + l.Def.Missile == item) { l.Stock = Math.Max(0, l.Stock + (int)Math.Round(qty)); return; }
            if (n.Kind == "battery" && _t.Batteries.FirstOrDefault(b => b.Def.Id == n.Id) is { } b && "design:" + b.Def.Interceptor == item) { b.Stock = Math.Max(0, b.Stock + (int)Math.Round(qty)); return; }
        }
        if (item.StartsWith("store:") && n.Kind is "airbase" && _t.Air != null) { _t.Air.Munitions.Add(n.Id, item[6..], (int)Math.Round(qty)); return; }
        n.Stock[item] = Math.Max(0, n.Stock.GetValueOrDefault(item) + qty);
    }

    /// <summary>Baseline (what the node is meant to hold) including magazines.</summary>
    public double BaselineOf(SupplyNode n, string item)
    {
        if (item.StartsWith("design:"))
        {
            if (n.Kind == "launcher" && _t.Launchers.FirstOrDefault(l => l.Def.Id == n.Id) is { } l && "design:" + l.Def.Missile == item) return l.Def.Stock;
            if (n.Kind == "battery" && _t.Batteries.FirstOrDefault(b => b.Def.Id == n.Id) is { } b && "design:" + b.Def.Interceptor == item) return b.Def.Stock;
        }
        return n.Baseline.GetValueOrDefault(item);
    }

    public IEnumerable<string> ItemsAt(SupplyNode n)
    {
        var items = new HashSet<string>(n.Stock.Keys.Concat(n.Baseline.Keys));
        if (n.Kind == "launcher" && _t.Launchers.FirstOrDefault(l => l.Def.Id == n.Id) is { } l) items.Add("design:" + l.Def.Missile);
        if (n.Kind == "battery" && _t.Batteries.FirstOrDefault(b => b.Def.Id == n.Id) is { } b) items.Add("design:" + b.Def.Interceptor);
        if (n.Kind == "airbase" && _t.Air?.Munitions.ByBase.GetValueOrDefault(n.Id) is { } st) foreach (var k in st.Keys) items.Add("store:" + k);
        return items;
    }

    public double Consume(SupplyNode n, string item, double qty)
    {
        var have = Get(n, item);
        var take = Math.Min(have, qty);
        Add(n, item, -take);
        n.UsedToday[item] = n.UsedToday.GetValueOrDefault(item) + qty;
        if (take < qty - 1e-9) n.Short.Add(item); else n.Short.Remove(item);
        return take;
    }

    public SupplyNode? Node(string id) => Nodes.GetValueOrDefault(id);
    /// <summary>Daily use estimate (yesterday's, or today's so far scaled).</summary>
    public double DailyUse(SupplyNode n, string item) => Math.Max(n.UsedYesterday.GetValueOrDefault(item), n.UsedToday.GetValueOrDefault(item));
    public double DaysLeft(SupplyNode n, string item) { var u = DailyUse(n, item); return u <= 0 ? double.PositiveInfinity : Get(n, item) / u; }

    public string ItemName(string item) =>
        Def.Items.TryGetValue(item, out var d) ? d.Name
        : item.StartsWith("store:") ? _t.Air?.Stores?.Stores.GetValueOrDefault(item[6..])?.Name ?? item[6..]
        : item.StartsWith("design:") ? (_t.Launchers.FirstOrDefault(l => "design:" + l.Def.Missile == item)?.Missile.Name ?? _t.Batteries.FirstOrDefault(b => "design:" + b.Def.Interceptor == item)?.Interceptor.Name ?? item[7..])
        : item;

    public string Amount(string item, double q) =>
        Def.Items.TryGetValue(item, out var d) ? (d.Unit == "m3" ? (q * 1000 >= 1e6 ? $"{q / 1000:0.##} M L" : $"{q * 1000:0,0} L") : $"{q:0,0} {d.Unit}") : $"{q:0}";

    public double Tons(string item, double q)
    {
        if (Def.Items.TryGetValue(item, out var d)) return q * d.TonPerUnit;
        if (item.StartsWith("store:") && _t.Air?.Stores?.Stores.GetValueOrDefault(item[6..]) is { } s) return q * Def.MunitionMass.GetValueOrDefault("store" + s.Cls, 0.5);
        return q * Def.MunitionMass.GetValueOrDefault("design", 3);
    }

    // ---------------- consumption hooks (called by the theatre systems) ----------------

    /// <summary>Av fuel a sortie of n aircraft of this class needs (m3).</summary>
    public double SortieFuel(string cls, int n) => n * Def.Use.Sortie.GetValueOrDefault(cls, Def.Use.Sortie.GetValueOrDefault("default", 10));
    public bool IsLiquid(string design) => Def.Liquid.Contains(design);

    /// <summary>Propellant for a launch; false (with a reason) when a liquid-fuelled launcher's node is dry.</summary>
    public bool Fuel(LauncherState l, int rounds, out string why)
    {
        why = "";
        if (!IsLiquid(l.Def.Missile)) return true;
        var node = l.IsShip ? null : Nodes.GetValueOrDefault(l.Def.Id);
        if (node == null) return true;
        var need = rounds * Def.Use.PropellantPerRound;
        if (Get(node, "propellant") < need) { node.Short.Add("propellant"); why = $"{l.Def.Name} has no liquid propellant for {rounds} round(s) (needs {Amount("propellant", need)}, holds {Amount("propellant", Get(node, "propellant"))})."; return false; }
        Consume(node, "propellant", need);
        return true;
    }

    /// <summary>0.3..1: readiness from spares and fuel at a node (feeds battery radar/channels and base turnaround).</summary>
    public double Readiness(string nodeId) =>
        Nodes.TryGetValue(nodeId, out var n) ? (n.Short.Contains("spares") ? 0.7 : 1) * (n.Short.Contains("diesel") ? 0.6 : 1) : 1;

    // ---------------- transport ----------------

    /// <summary>Cheapest route (hours) over this side's corridors, or null.</summary>
    public List<Corridor>? Route(string side, string from, string to, string? mode = null)
    {
        var dist = new Dictionary<string, double> { [from] = 0 };
        var prev = new Dictionary<string, Corridor>();
        var open = new HashSet<string> { from };
        while (open.Count > 0)
        {
            var u = open.OrderBy(x => dist[x]).First();
            open.Remove(u);
            if (u == to) break;
            foreach (var c in Corridors.Values.Where(c => c.Def.Side == side && (c.Def.A == u || c.Def.B == u) && (mode == null || c.Def.Mode == mode || mode == "air")))
            {
                var v = c.Def.A == u ? c.Def.B : c.Def.A;
                var h = Hours(c, mode == "air" ? "air" : c.Def.Mode);
                if (dist.GetValueOrDefault(v, double.MaxValue) <= dist[u] + h) continue;
                dist[v] = dist[u] + h; prev[v] = c; open.Add(v);
            }
        }
        if (!prev.ContainsKey(to) && from != to) return null;
        var path = new List<Corridor>();
        for (var n = to; n != from; n = prev[n].Def.A == n ? prev[n].Def.B : prev[n].Def.A) path.Insert(0, prev[n]);
        return path;
    }

    private double Hours(Corridor c, string mode)
    {
        var m = Def.Modes.GetValueOrDefault(mode) ?? new TransportModeDef();
        var km = mode == "air" ? Pacing.DistanceKm(Nodes[c.Def.A].Lon, Nodes[c.Def.A].Lat, Nodes[c.Def.B].Lon, Nodes[c.Def.B].Lat) : c.Def.Km;
        return km / m.Kmh / Math.Max(0.1, 1 - c.Damage);
    }

    /// <summary>Order a shipment (the AI officer and the player use the same call).</summary>
    public (bool Ok, string Message, Shipment? S) Order(string side, string item, double qty, string from, string to, string? mode = null, bool player = false)
    {
        var a = Node(from); var b = Node(to);
        if (a == null || b == null || a.Side != side || b.Side != side) return (false, "Pick two of your own supply points.", null);
        if (from == to) return (false, "Source and destination are the same.", null);
        qty = Math.Min(qty, Get(a, item));
        if (qty <= 0) return (false, $"{a.Name} holds no {ItemName(item)}.", null);
        mode ??= ChooseMode(side, a, b, item, qty);
        var path = Route(side, from, to, mode == "air" || mode == "heli" ? "air" : null);
        if (path == null) return (false, $"No {mode} route from {a.Name} to {b.Name}.", null);
        var m = Def.Modes[mode];
        var tons = Tons(item, qty);
        var need = Math.Max(1, (int)Math.Ceiling(tons / m.Cap));
        var free = Pool.GetValueOrDefault((side, mode)) - InUse.GetValueOrDefault((side, mode));
        var s = new Shipment { Id = _next++, Side = side, Item = item, Qty = qty, From = from, To = to, Mode = mode, Vehicles = need, Player = player };
        s.Path.AddRange(path.Select(c => c.Def.Id));
        Add(a, item, -qty); // loaded and leaving
        Shipments.Add(s);
        if (free >= need) Dispatch(s); else s.Note = $"waiting for {need} {m.Vehicle} ({free} free)";
        var eta = s.State == ShipmentState.Moving ? $"arrives T+{AirTasking.Clock(s.Arrive)}" : s.Note;
        var msg = $"{Amount(item, qty)} {ItemName(item)} {a.Name} → {b.Name} by {m.Name.ToLowerInvariant()} ({need} {m.Vehicle}, {tons:0} t), {eta}.";
        _t.Report("logistics", msg);
        return (true, msg, s);
    }

    private string ChooseMode(string side, SupplyNode a, SupplyNode b, string item, double qty)
    {
        var tons = Tons(item, qty);
        var urgent = b.Short.Contains(item) || DaysLeft(b, item) < 0.5;
        bool Free(string mode, int need) => Pool.GetValueOrDefault((side, mode)) - InUse.GetValueOrDefault((side, mode)) >= need;
        if (b.Kind == "ship" || Route(side, a.Id, b.Id, "sea") is { Count: > 0 } && tons > 500) return "sea";
        if (tons > 300 && Route(side, a.Id, b.Id, "rail") is { Count: > 0 } && Free("rail", 1)) return "rail";
        if (urgent && tons <= 200 && Free("air", (int)Math.Ceiling(tons / Def.Modes["air"].Cap))) return "air";
        if (b.Factor < 0.6 && tons <= 30 && Free("heli", (int)Math.Ceiling(tons / Def.Modes["heli"].Cap))) return "heli";
        return "road";
    }

    private void Dispatch(Shipment s)
    {
        var m = Def.Modes[s.Mode];
        var hours = s.Path.Sum(id => Hours(Corridors[id], s.Mode is "air" or "heli" ? "air" : Corridors[id].Def.Mode) * (s.Mode == "heli" ? Def.Modes["air"].Kmh / m.Kmh : 1));
        hours /= Math.Max(Def.Efficiency.Min, Nodes[s.To].Factor); // last leg: off-route and altitude penalties
        s.Depart = _t.Time; s.Arrive = _t.Time + hours * 3600; s.VehiclesBack = s.Arrive + hours * 3600;
        s.State = ShipmentState.Moving; s.Note = "";
        InUse[(s.Side, s.Mode)] = InUse.GetValueOrDefault((s.Side, s.Mode)) + s.Vehicles;
    }

    /// <summary>Active vehicles per corridor (outbound loads only), for route readouts.</summary>
    public IEnumerable<(Corridor C, string Mode, int Vehicles, double Tons)> ActiveRoutes() =>
        Shipments.Where(s => s.State == ShipmentState.Moving).SelectMany(s => s.Path.Select(p => (C: Corridors[p], s.Mode, s.Vehicles, T: Tons(s.Item, s.Qty))))
            .GroupBy(x => (x.C.Def.Id, x.Mode)).Select(g => (g.First().C, g.Key.Mode, g.Sum(x => x.Vehicles), g.Sum(x => x.T)));

    // ---------------- interception and damage ----------------

    /// <summary>A strike on a route: throughput falls and loads on it may be destroyed.</summary>
    public string HitCorridor(string id, double punch, string by)
    {
        if (!Corridors.TryGetValue(id, out var c)) return "";
        c.Damage = Math.Min(0.9, c.Damage + 0.1 + 0.03 * punch);
        var lost = 0;
        foreach (var s in Shipments.Where(s => s.State == ShipmentState.Moving && s.Path.Contains(id)))
            if (_rng.NextDouble() < c.Damage * 0.5) { Lose(s, $"destroyed on {c.Def.Name} by {by}"); lost++; }
        var msg = $"{c.Def.Name} hit by {by}: {c.Damage:P0} damaged{(lost > 0 ? $", {lost} load(s) destroyed" : "")}.";
        _t.Report("impact", msg);
        return msg;
    }

    /// <summary>A hit on a base's fuel or munitions storage destroys a share of what's held there.</summary>
    public void OnComponentHit(BaseState b, ComponentState comp, double damage)
    {
        if (!Nodes.TryGetValue(b.Site.Id, out var n)) return;
        var share = Math.Clamp(damage / 100, 0, 1);
        var keys = comp.Def.Effect switch
        {
            "fuel" => new[] { "avfuel", "diesel", "propellant" },
            "munitions" => ItemsAt(n).Where(k => k.StartsWith("store:") || k.StartsWith("design:")).ToArray(),
            "stores" => new[] { "spares" },
            _ => Array.Empty<string>(),
        };
        var gone = new List<string>();
        foreach (var k in keys)
        {
            var q = Math.Floor(Get(n, k) * share);
            if (q <= 0) continue;
            Add(n, k, -q);
            gone.Add($"{Amount(k, q)} {ItemName(k)}");
        }
        if (gone.Count > 0) _t.Report("impact", $"{b.Site.Name}: stored supplies destroyed ({string.Join(", ", gone.Take(4))}{(gone.Count > 4 ? "…" : "")}).");
    }

    private void Lose(Shipment s, string why)
    {
        s.State = ShipmentState.Lost; s.Note = why;
        InUse[(s.Side, s.Mode)] = Math.Max(0, InUse.GetValueOrDefault((s.Side, s.Mode)) - s.Vehicles);
        Pool[(s.Side, s.Mode)] = Math.Max(0, Pool.GetValueOrDefault((s.Side, s.Mode)) - (int)Math.Ceiling(s.Vehicles * 0.5)); // some vehicles don't come back
        _t.Report("logistics", $"{Amount(s.Item, s.Qty)} {ItemName(s.Item)} for {Nodes[s.To].Name} {why}.");
    }

    // ---------------- tick ----------------

    public void Tick(double now)
    {
        foreach (var s in Shipments.Where(s => s.State == ShipmentState.Moving && now >= s.Arrive).ToList())
        {
            s.State = ShipmentState.Delivered;
            Add(Nodes[s.To], s.Item, s.Qty);
            Nodes[s.To].Short.Remove(s.Item);
            _t.Report("logistics", $"{Amount(s.Item, s.Qty)} {ItemName(s.Item)} delivered to {Nodes[s.To].Name}.");
        }
        foreach (var s in Shipments.Where(s => s.State == ShipmentState.Delivered && s.VehiclesBack > 0 && now >= s.VehiclesBack))
        {
            InUse[(s.Side, s.Mode)] = Math.Max(0, InUse.GetValueOrDefault((s.Side, s.Mode)) - s.Vehicles);
            s.VehiclesBack = 0;
        }
        foreach (var s in Shipments.Where(s => s.State == ShipmentState.Queued).ToList())
            if (Pool.GetValueOrDefault((s.Side, s.Mode)) - InUse.GetValueOrDefault((s.Side, s.Mode)) >= s.Vehicles) Dispatch(s);
        if (now < _nextHour) return;
        var hours = Math.Max(1, (now - (_nextHour - 3600)) / 3600);
        _nextHour = now + 3600;
        Hourly(hours);
        if (now >= _nextDay)
        {
            _nextDay = now + 86400;
            foreach (var n in Nodes.Values) { n.UsedYesterday.Clear(); foreach (var (k, v) in n.UsedToday) n.UsedYesterday[k] = v; n.UsedToday.Clear(); }
        }
        foreach (var side in new[] { "blue", "red" }) if (Auto.GetValueOrDefault(side)) Officer(side);
        Shipments.RemoveAll(s => s.State is ShipmentState.Delivered or ShipmentState.Lost && s.VehiclesBack == 0 && now - s.Arrive > 6 * 3600);
    }

    private void Hourly(double h)
    {
        var u = Def.Use;
        foreach (var c in Corridors.Values) c.Damage = Math.Max(0, c.Damage - 0.03 * h); // repair crews
        foreach (var n in Nodes.Values)
        {
            switch (n.Kind)
            {
                case "battery": Consume(n, "spares", u.BatterySparesDay / 24 * h); Consume(n, "diesel", u.GroundDieselDay / 24 * h); break;
                case "launcher": Consume(n, "spares", u.LauncherSparesDay / 24 * h); Consume(n, "diesel", u.GroundDieselDay / 24 * h); break;
                case "ship":
                    if (_t.Naval?.Find(n.Id) is { Sunk: false } sh) { n.Lon = sh.Lon; n.Lat = sh.Lat; Consume(n, "diesel", u.ShipDieselDay / 24 * h * (sh.Moving ? 1 : 0.4)); Consume(n, "spares", u.ShipSparesDay / 24 * h); sh.OutOfFuel = n.Short.Contains("diesel"); }
                    break;
                case "depot": break;
                default:
                    var aircraft = _t.Scenario.Units.Count(x => x.Kind is "air" or "aew" && (_t.Air?.BaseOf(x) ?? x.Base) == n.Id && !(_t.Air?.Destroyed.Contains(x.Id) ?? false));
                    Consume(n, "spares", u.AircraftSparesDay / 24 * h * aircraft);
                    Consume(n, "diesel", u.BaseDieselDay / 24 * h);
                    break;
            }
        }
        // interdiction: loads at sea or in the air near enemy forces can be lost
        foreach (var s in Shipments.Where(s => s.State == ShipmentState.Moving && s.Mode is "sea" or "air").ToList())
        {
            var f = Math.Clamp((_t.Time - s.Depart) / Math.Max(1, s.Arrive - s.Depart), 0, 1);
            var a = Nodes[s.From]; var b = Nodes[s.To];
            var (lon, lat) = (a.Lon + (b.Lon - a.Lon) * f, a.Lat + (b.Lat - a.Lat) * f);
            var threat = s.Mode == "air"
                ? _t.Air?.Flights.Count(x => x.Side != s.Side && x.Airborne(_t.Time) && x.Mission is Mission.CAP or Mission.Escort && Pacing.DistanceKm(x.Pos(_t.Time).Lon, x.Pos(_t.Time).Lat, lon, lat) < 150) ?? 0
                : _t.Naval?.Ships.Count(x => x.Side != s.Side && !x.Sunk && Pacing.DistanceKm(x.Lon, x.Lat, lon, lat) < 80) ?? 0;
            if (threat > 0 && _rng.NextDouble() < 0.12 * threat * h) Lose(s, s.Mode == "air" ? "shot down in transit" : "sunk in transit");
        }
    }

    /// <summary>
    /// AI logistics officer: nodes below their request share get topped back up to their baseline, from the nearest
    /// reserve depot first, then from bases with surplus (never taking a source below its keep share).
    /// </summary>
    private void Officer(string side)
    {
        var inbound = Shipments.Where(s => s.Side == side && s.State is ShipmentState.Queued or ShipmentState.Moving).ToList();
        foreach (var n in Nodes.Values.Where(n => n.Side == side && n.Kind != "depot").OrderBy(n => n.Kind == "airbase" ? 0 : 1))
        {
            if (n.Kind == "ship" && _t.Naval?.Find(n.Id) is { } ship && ship.Sunk) continue;
            foreach (var item in ItemsAt(n).ToList())
            {
                var baseQty = BaselineOf(n, item);
                if (baseQty <= 0) continue;
                var have = Get(n, item) + inbound.Where(s => s.To == n.Id && s.Item == item).Sum(s => s.Qty);
                if (have >= baseQty * Def.RequestShare && !n.Short.Contains(item)) continue;
                var want = baseQty - have;
                if (want <= 0) continue;
                // launchers and batteries reload from their host base first (same site, no transport)
                if (n.Kind is "launcher" or "battery" && n.BaseId != "" && Nodes.TryGetValue(n.BaseId, out var host) && item.StartsWith("design:"))
                {
                    var give = Math.Min(want, Get(host, item));
                    if (give >= 1) { Add(host, item, -give); Add(n, item, give); _t.Report("logistics", $"{n.Name} reloaded {give:0} {ItemName(item)} from {host.Name}."); want -= give; }
                    if (want <= 0) continue;
                }
                var source = Nodes.Values.Where(s => s.Side == side && s.Reserve && Get(s, item) >= 1).OrderBy(s => Route(side, s.Id, n.Id) is { } r ? r.Sum(c => Hours(c, c.Def.Mode)) : double.MaxValue).FirstOrDefault()
                    ?? Nodes.Values.Where(s => s.Side == side && !s.Reserve && s != n && s.Kind != "ship" && Get(s, item) > BaselineOf(s, item) * Def.KeepShare + 1)
                        .OrderBy(s => Km(s, n)).FirstOrDefault();
                if (source == null) continue;
                var avail = source.Reserve ? Get(source, item) : Get(source, item) - BaselineOf(source, item) * Def.KeepShare;
                var qty = Math.Floor(Math.Min(want, avail));
                if (qty < 1) continue;
                Order(side, item, qty, source.Id, n.Id);
            }
        }
    }
}
