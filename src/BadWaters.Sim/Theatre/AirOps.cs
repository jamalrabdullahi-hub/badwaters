using BadWaters.Sim.Policy;

namespace BadWaters.Sim.Theatre;

// Air tasking: packages of flights planned backwards from a time on target (TOT), flown along waypoints
// (take-off → ingress → IP → target → egress → land) and resolved with dice against 0–10 ratings.
// Movement is interpolation between planned waypoint times; there is no flight model or physics.

public enum Mission { Strike, Escort, SEAD, CAP, AEW, Tanker, Recon, Jamming, Rescue }
public enum Formation { Wedge, LineAbreast, Trail, EchelonRight, Box }
public enum FlightState { Planned, Airborne, Ingress, Attack, Egress, OnStation, Landed, Lost }

public sealed record Waypoint(string Name, double Lon, double Lat, double T);
public sealed record PresetInfo(string Id, string Name, IReadOnlyDictionary<string, double> Ratings)
{
    public double R(string k) => Ratings.TryGetValue(k, out var v) ? v : 0;
}
public sealed record FlightRequest(string Preset, int Count, Mission Mission, Formation Formation = Formation.Wedge)
{
    /// <summary>Per-aircraft stores (station → store × qty). Null: the mission's default loadout.</summary>
    public IReadOnlyDictionary<string, StoreLoad>? Loadout { get; init; }
}

/// <summary>One AI or player tasking line: a strike package (Target + Tot) or a CAP/AEW/tanker station.</summary>
public sealed record AtoEntry
{
    public required string Side { get; init; }
    public string Name { get; init; } = "";
    public string Target { get; init; } = "";
    public double Tot { get; init; }
    public bool Tanker { get; init; }
    public double IpKm { get; init; } = 60;
    public double StationLon { get; init; }
    public double StationLat { get; init; }
    public double Start { get; init; }
    public double Duration { get; init; } = 5400;
    public List<FlightRequest> Flights { get; init; } = new();
    /// <summary>Base component to aim at (runway, fuel...). Empty: doctrine picks from the side's own picture.</summary>
    public string Aimpoint { get; init; } = "";
    /// <summary>Missile fire mission: this launcher (ground or ship) fires Count rounds to arrive at Tot.</summary>
    public string Launcher { get; init; } = "";
    public int Count { get; init; } = 2;
}

public enum FireState { Planned, Launched, Failed }

/// <summary>A missile fire mission in the ATO: launch time worked back from the time on target.</summary>
public sealed class MissileTask
{
    public required string Id { get; init; }
    public required string Side { get; init; }
    public required LauncherState Launcher { get; init; }
    public required SiteDef Target { get; init; }
    public string Aimpoint { get; init; } = "";
    public int Count { get; init; }
    public double Tot { get; init; }
    public double LaunchAt { get; init; }
    public FireState State { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Game modifiers per formation (shown in the planner). Tuning values, not tactics doctrine.</summary>
public static class FormationRules
{
    public static (double AirToAir, double SamLoss, double Strike, string Text) Mod(Formation f) => f switch
    {
        Formation.Wedge => (0.03, 0, 0, "Wedge: mutual support, +3% air-to-air"),
        Formation.LineAbreast => (0.02, 0.01, 0, "Line abreast: best lookout, +2% air-to-air, slightly more exposed"),
        Formation.Trail => (-0.02, -0.02, 0.02, "Trail: small footprint, −2% SAM losses, +2% strike, weaker in a dogfight"),
        Formation.EchelonRight => (0.01, 0, 0.01, "Echelon right: +1% air-to-air, +1% strike"),
        Formation.Box => (0.02, 0.01, 0.03, "Box: +3% strike, +2% air-to-air, slightly more exposed"),
        _ => (0, 0, 0, ""),
    };

    /// <summary>Slot offsets in "aircraft spacings" (x right, y back) used for drawing the formation.</summary>
    public static (float X, float Y)[] Slots(Formation f) => f switch
    {
        Formation.Wedge => new[] { (0f, 0f), (-1f, 1f), (1f, 1f), (-2f, 2f), (2f, 2f), (-3f, 3f) },
        Formation.LineAbreast => new[] { (0f, 0f), (1.5f, 0f), (-1.5f, 0f), (3f, 0f), (-3f, 0f), (4.5f, 0f) },
        Formation.Trail => new[] { (0f, 0f), (0f, 1.5f), (0f, 3f), (0f, 4.5f), (0f, 6f), (0f, 7.5f) },
        Formation.EchelonRight => new[] { (0f, 0f), (1f, 1f), (2f, 2f), (3f, 3f), (4f, 4f), (5f, 5f) },
        _ => new[] { (0f, 0f), (1.5f, 0f), (0f, 1.5f), (1.5f, 1.5f), (0f, 3f), (1.5f, 3f) },
    };
}

/// <summary>Pacing tables for air ops (game tuning).</summary>
public static class AirPacing
{
    public static double CruiseKmPerSec(double speed) => 0.10 + 0.025 * speed;
    public static double RadiusKm(double reach) => 200 + 100 * reach;
    public const double TankerBonusKm = 300;
    /// <summary>Stand-off jamming covers enemy radars within this range (game tuning).</summary>
    public const double JamKm = 300;
    /// <summary>Rescue helicopters on alert recover downed crews within this range of their base.</summary>
    public const double RescueKm = 400;
    public const double PrepSeconds = 120;
    public const double EscortLead = 120, SeadLead = 60;
    public const double DogfightKm = 70;
}

public sealed class Flight
{
    public required string Id { get; init; }
    public required string Callsign { get; init; }
    public required string Side { get; init; }
    public required PresetInfo Type { get; init; }
    public required string BaseId { get; set; }
    public Mission Mission { get; init; }
    public Formation Formation { get; set; }
    public bool Aborted { get; set; }
    public string PackageId { get; init; } = "";
    public List<string> Aircraft { get; } = new();
    public List<string> LostAircraft { get; } = new();
    public List<Waypoint> Route { get; } = new();
    public FlightState State { get; set; } = FlightState.Planned;
    public bool Released { get; set; }
    /// <summary>Weapons held (an operation rule): the flight flies the route but does not release.</summary>
    public bool HoldFire { get; set; }
    /// <summary>Stores per aircraft (station → store × qty), drawn from the base's munition stocks when armed.</summary>
    public Dictionary<string, StoreLoad> Loadout { get; } = new();
    /// <summary>Stores fired in flight (whole flight, store → count); the rest go back to the base on landing.</summary>
    public Dictionary<string, int> Expended { get; } = new();

    public double Takeoff => Route[0].T;
    public double LandAt => Route[^1].T;
    public bool Airborne(double t) => State is not (FlightState.Planned or FlightState.Landed or FlightState.Lost) && t >= Takeoff && t < LandAt;

    /// <summary>Position at time t: straight between waypoints, a slow orbit between "Station" and "Off station".</summary>
    public (double Lon, double Lat) Pos(double t)
    {
        if (t <= Route[0].T) return (Route[0].Lon, Route[0].Lat);
        for (var i = 0; i + 1 < Route.Count; i++)
        {
            var a = Route[i]; var b = Route[i + 1];
            if (t > b.T) continue;
            if (a.Name == "Station" && b.Name == "Off station")
            {
                var ang = (t - a.T) / 600.0 * Math.PI * 2;
                var r = 25 / 111.0;
                return (a.Lon + r * Math.Cos(ang) / Math.Cos(a.Lat * Math.PI / 180), a.Lat + r * Math.Sin(ang));
            }
            var f = b.T > a.T ? (t - a.T) / (b.T - a.T) : 1;
            return (a.Lon + (b.Lon - a.Lon) * f, a.Lat + (b.Lat - a.Lat) * f);
        }
        return (Route[^1].Lon, Route[^1].Lat);
    }

    /// <summary>The next waypoint after time t (for headings and "next: IP at T+..." read-outs).</summary>
    public Waypoint? Next(double t) => Route.FirstOrDefault(w => w.T > t);
}

public sealed class Package
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Side { get; init; }
    public required SiteDef Target { get; init; }
    public double Tot { get; init; }
    public bool Tanker { get; init; }
    public double IpKm { get; init; }
    public string Aimpoint { get; init; } = "";
    public (double Lon, double Lat) Ip { get; init; }
    public List<Flight> Flights { get; } = new();
    public string Result { get; set; } = "";
}

public sealed record PlanResult(bool Ok, IReadOnlyList<string> Issues, Package? Package, IReadOnlyList<Flight> Flights);

public sealed class AirTasking
{
    private readonly InterceptTrial _t;
    private readonly Func<string, PresetInfo?> _preset;
    private readonly Random _rng;
    public List<Package> Packages { get; } = new();
    public List<Flight> Flights { get; } = new();
    public List<MissileTask> Fires { get; } = new();
    /// <summary>Aircraft (unit ids) currently assigned to a flight.</summary>
    public HashSet<string> Tasked { get; } = new();
    public HashSet<string> Destroyed { get; } = new();
    private readonly Dictionary<(string, string), double> _lastFight = new();
    private int _next = 1;
    private readonly Dictionary<string, int> _calls = new();
    /// <summary>Aircraft that landed away from their home base (unit id → base id).</summary>
    private readonly Dictionary<string, string> _divertBase = new();
    public void SetBase(string unit, string baseId) => _divertBase[unit] = baseId;
    public string BaseOf(UnitDef u) => _divertBase.TryGetValue(u.Id, out var b) ? b : u.Base;
    private static readonly string[] BlueCalls = { "EAGLE", "VIPER", "COBRA", "HAWK", "RAVEN", "LANCE", "SABRE", "FALCON" };
    private static readonly string[] RedCalls = { "DRAGON", "TIGER", "WOLF", "FLAME", "STORM", "ARROW", "THUNDER", "IRON" };

    /// <summary>Stores catalog (null: aircraft fly on their ratings alone, no loadouts or munition stocks).</summary>
    public StoresCatalog? Stores { get; set; }
    public MunitionStocks Munitions { get; } = new();

    /// <summary>Starting stocks: each air base holds three sorties' worth of every store its own aircraft use in their default loadouts.</summary>
    public void SeedMunitions()
    {
        if (Stores == null) return;
        foreach (var g in _t.Scenario.Units.Where(u => u.Kind == "air").GroupBy(u => u.Base))
            foreach (var u in g)
            {
                var t = Stores.Template(u.Preset);
                if (t == null) continue;
                var need = new Dictionary<string, int>();
                foreach (var name in t.Loadouts.Keys)
                    foreach (var l in Stores.Default(u.Preset, name).Values.GroupBy(x => x.Store))
                        need[l.Key] = Math.Max(need.GetValueOrDefault(l.Key), l.Sum(x => x.Qty));
                foreach (var (store, n) in need) Munitions.Add(g.Key, store, n * 3);
            }
    }

    /// <summary>Bases of a side (for theatre-wide munition totals).</summary>
    public IEnumerable<string> BasesOf(string side) => _t.Scenario.Sites.Where(s => s.Side == side).Select(s => s.Id);

    public AirTasking(InterceptTrial trial, Func<string, PresetInfo?> preset, int seed = 1)
    {
        _t = trial;
        _preset = preset;
        _rng = new Random(seed * 7919 + 3);
        // with theatre commanders the scenario's lines become their opening operations instead (see Commander)
        foreach (var e in trial.Scenario.Ato.Where(e => !trial.UseCommanders && trial.Control.GetValueOrDefault(e.Side) == SideControl.AI))
        {
            var r = Commit(e);
            if (!r.Ok) trial.Report("ato", $"{Side(e.Side)} ATO line not flown ({(e.Name != "" ? e.Name : e.Launcher)}): {string.Join(" ", r.Issues)}");
        }
    }

    // ---------------- availability ----------------

    public IEnumerable<UnitDef> Idle(string side) =>
        _t.Scenario.Units.Where(u => u.Side == side && u.Kind is "air" or "aew" && !Tasked.Contains(u.Id) && !Destroyed.Contains(u.Id))
            .Select(u => BaseOf(u) == u.Base ? u : u with { Base = BaseOf(u) });

    /// <summary>Idle aircraft by type for a side: preset → (name, count).</summary>
    public IEnumerable<(string Preset, string Name, int Count)> IdleByType(string side) =>
        Idle(side).GroupBy(u => u.Preset).Select(g => (g.Key, _preset(g.Key)?.Name ?? g.Key, g.Count()));

    // ---------------- planning ----------------

    public PlanResult Commit(AtoEntry e) => Plan(e, commit: true);

    /// <summary>Command authority: when set, planning only uses assets free in the pool or owned by the operation being planned.</summary>
    public BadWaters.Sim.Command.Authority? Authority { get; set; }
    private string _forOp = "";
    private IReadOnlyCollection<string>? _only;
    private bool Usable(string asset) =>
        (Authority == null || Authority.IsFreeFor(asset, _forOp)) && (_only == null || _only.Count == 0 || _only.Contains(asset));

    /// <summary>Plan (and optionally commit) for an operation, using its picked assets if it named any.</summary>
    public PlanResult PlanFor(AtoEntry e, string op, IReadOnlyCollection<string>? only, bool commit)
    {
        _forOp = op; _only = only;
        try { return Plan(e, commit); } finally { _forOp = ""; _only = null; }
    }

    public PlanResult Plan(AtoEntry e, bool commit = false)
    {
        if (e.Launcher != "") return PlanFire(e, commit);
        var issues = new List<string>();
        var strike = e.Target != "";
        SiteDef? target = null;
        if (strike)
        {
            string? why = null;
            target = e.Target.StartsWith("ship:") ? ShipTarget(e.Side, e.Target[5..], out why) : _t.Site(e.Target);
            if (target == null) return Fail(why ?? "Unknown target.");
            if (!TargetPolicy.IsLegitimate(target.Class)) return Fail(TargetPolicy.IsKnown(target.Class) ? TargetPolicy.ProtectedMessage : "Unclassified target class: refused.");
            if (target.Side == e.Side) return Fail("That target is on your own side.");
        }
        if (strike && !_t.AtWar) return Fail(InterceptTrial.NotAtWar);
        var aim = strike ? (target!.Lon, target.Lat) : (e.StationLon, e.StationLat);
        var pkgId = $"{e.Side}-pkg-{_next}";
        var calls = e.Side == "blue" ? BlueCalls : RedCalls;
        var flights = new List<Flight>();
        var reserved = new HashSet<string>();
        (double Lon, double Lat) ip = aim;
        var side = _next % 2 == 0 ? 1 : -1; // which side of the direct line the route bends to

        var hasTanker = e.Tanker || e.Flights.Any(r => r.Mission == Mission.Tanker);
        var drawn = new Dictionary<(string Base, string Store), int>(); // stores this plan takes, per base
        foreach (var (req, idx) in e.Flights.Select((r, i) => (r, i)).OrderBy(x => x.r.Mission is Mission.Tanker or Mission.Jamming))
        {
            if (req.Mission == Mission.Rescue) { issues.Add("Rescue helicopters are not flown in a package: on alert at their base they launch by themselves when aircraft go down."); continue; }
            var info = _preset(req.Preset);
            if (info == null) { issues.Add($"Unknown aircraft type {req.Preset}."); continue; }
            // the base with enough idle aircraft of this type that is closest to the objective
            var pool = Idle(e.Side).Where(u => u.Preset == req.Preset && !reserved.Contains(u.Id) && Usable(u.Id)).GroupBy(u => u.Base)
                .Select(g => (Base: _t.Site(g.Key), Units: g.ToList())).Where(x => x.Base != null && x.Units.Count >= req.Count && BaseCanLaunch(x.Base.Id, req.Mission))
                .OrderBy(x => Pacing.DistanceKm(x.Base!.Lon, x.Base.Lat, aim.Item1, aim.Item2)).FirstOrDefault();
            if (pool.Base == null) { issues.Add($"Not enough idle {info.Name} at any single working base for {req.Count} aircraft (runway closed or munitions store destroyed counts as not working)."); continue; }
            // arm from the base's munition stocks
            var load = new Dictionary<string, StoreLoad>();
            if (Stores != null)
            {
                load = req.Loadout != null ? req.Loadout.ToDictionary(k => k.Key, k => k.Value) : Stores.Default(req.Preset, req.Mission);
                var bad = Stores.Validate(req.Preset, load);
                if (bad.Count > 0) { issues.Add($"{info.Name} loadout: {string.Join("; ", bad)}."); continue; }
                var shortOf = load.Values.Where(l => l.Qty > 0).GroupBy(l => l.Store).Select(x => (Store: x.Key, Need: x.Sum(y => y.Qty) * req.Count))
                    .Where(x => Munitions.At(pool.Base.Id, x.Store) - drawn.GetValueOrDefault((pool.Base.Id, x.Store)) < x.Need).ToList();
                if (shortOf.Count > 0)
                {
                    issues.AddRange(shortOf.Select(x => $"Not enough {Stores.Stores.GetValueOrDefault(x.Store)?.Name ?? x.Store} at {pool.Base.Name} for {req.Count}× {info.Name}: need {x.Need}, base has {Munitions.At(pool.Base.Id, x.Store) - drawn.GetValueOrDefault((pool.Base.Id, x.Store))} (theatre {Munitions.Theatre(BasesOf(e.Side), x.Store)})."));
                    continue;
                }
                foreach (var x in load.Values.Where(l => l.Qty > 0)) drawn[(pool.Base.Id, x.Store)] = drawn.GetValueOrDefault((pool.Base.Id, x.Store)) + x.Qty * req.Count;
            }
            // av fuel for the sortie from the base's tanks (logistics)
            if (_t.Logistics is { } lg && lg.Node(pool.Base.Id) is { } fuelNode)
            {
                var fuel = lg.SortieFuel(SortieClass(req.Preset, req.Mission), req.Count);
                var left = lg.Get(fuelNode, "avfuel") - drawn.GetValueOrDefault((pool.Base.Id, "~avfuel"));
                if (left < fuel) { issues.Add($"Not enough av fuel at {pool.Base.Name} for {req.Count}× {info.Name}: need {lg.Amount("avfuel", fuel)}, base has {lg.Amount("avfuel", Math.Max(0, left))}. Resupply it (Logistics)."); continue; }
                drawn[(pool.Base.Id, "~avfuel")] = drawn.GetValueOrDefault((pool.Base.Id, "~avfuel")) + (int)Math.Ceiling(fuel);
            }
            var prep = AirPacing.PrepSeconds * (_t.Bases.TryGetValue(pool.Base.Id, out var hb) ? hb.PrepFactor : 1);
            var home = pool.Base;
            var dist = Pacing.DistanceKm(home.Lon, home.Lat, aim.Item1, aim.Item2);
            var radius = AirPacing.RadiusKm(info.R("reach")) + (hasTanker && req.Mission != Mission.Tanker ? AirPacing.TankerBonusKm : 0);
            if (dist > radius && req.Mission is not (Mission.Tanker or Mission.Jamming))
            {
                issues.Add($"{info.Name} from {home.Name}: objective is {dist:0} km away, combat radius {radius:0} km" + (hasTanker ? "." : ". Add a tanker (+300 km) or pick a closer base."));
                continue;
            }
            var v = AirPacing.CruiseKmPerSec(info.R("speed"));
            var f = new Flight
            {
                Id = $"{pkgId}-f{idx + 1}", Callsign = $"{calls[(_calls.GetValueOrDefault(e.Side) + flights.Count) % calls.Length]} {1 + (_calls.GetValueOrDefault(e.Side) + flights.Count) / calls.Length}1", Side = e.Side, Type = info,
                BaseId = home.Id, Mission = req.Mission, Formation = req.Formation, PackageId = pkgId,
            };
            f.Aircraft.AddRange(pool.Units.Take(req.Count).Select(u => u.Id));
            foreach (var (k, sl) in load) f.Loadout[k] = sl;
            foreach (var a in f.Aircraft) reserved.Add(a);

            if (req.Mission is Mission.Tanker or Mission.Jamming)
            {
                // tanker orbits 45% of the way out from the package's first base; a stand-off jammer 65%, from first take-off until after TOT
                var first = flights.FirstOrDefault(x => x.Mission is not (Mission.Tanker or Mission.Jamming));
                var anchor = first != null ? _t.Site(first.BaseId)! : home;
                var st = Lerp((anchor.Lon, anchor.Lat), aim, req.Mission == Mission.Jamming ? 0.65 : 0.45);
                var start = Math.Max(_t.Time + prep, (flights.Count > 0 ? flights.Min(x => x.Takeoff) : e.Tot - 3600) - 300);
                var tSt = start + Km((home.Lon, home.Lat), st) / v;
                var end = Math.Max(tSt + 600, e.Tot + 900);
                f.Route.Add(new("Take-off", home.Lon, home.Lat, start));
                f.Route.Add(new("Station", st.Item1, st.Item2, tSt));
                f.Route.Add(new("Off station", st.Item1, st.Item2, end));
                f.Route.Add(new("Land", home.Lon, home.Lat, end + Km(st, (home.Lon, home.Lat)) / v));
            }
            else if (strike)
            {
                // ingress point bends off the direct line; IP sits IpKm before the target on the attack axis
                var (mx, my) = Lerp((home.Lon, home.Lat), aim, 0.5);
                var (dx, dy) = (aim.Item1 - home.Lon, aim.Item2 - home.Lat);
                var ing = (mx - dy * 0.18 * side, my + dx * 0.18 * side);
                var legIpT = Pacing.DistanceKm(ing.Item1, ing.Item2, aim.Item1, aim.Item2);
                ip = legIpT <= e.IpKm ? ing : Lerp(aim, ing, e.IpKm / legIpT);
                var egr = (mx + dy * 0.18 * side, my - dx * 0.18 * side);
                var tot = e.Tot - (req.Mission == Mission.Escort ? AirPacing.EscortLead : req.Mission == Mission.SEAD ? AirPacing.SeadLead : 0);
                // times run backwards from the time on target; the IP-to-target run is 20% faster
                var tIp = tot - Km(ip, aim) / (v * 1.2);
                var tIng = tIp - Km(ing, ip) / v;
                var tTo = tIng - Km((home.Lon, home.Lat), ing) / v;
                var tEgr = tot + Km(aim, egr) / v;
                var tLand = tEgr + Km(egr, (home.Lon, home.Lat)) / v;
                f.Route.Add(new("Take-off", home.Lon, home.Lat, tTo));
                f.Route.Add(new("Ingress", ing.Item1, ing.Item2, tIng));
                f.Route.Add(new("IP", ip.Item1, ip.Item2, tIp));
                f.Route.Add(new(req.Mission is Mission.Strike or Mission.Recon ? "Target" : req.Mission == Mission.SEAD ? "SEAD" : "Sweep", aim.Item1, aim.Item2, tot));
                f.Route.Add(new("Egress", egr.Item1, egr.Item2, tEgr));
                f.Route.Add(new("Land", home.Lon, home.Lat, tLand));
                if (tTo < _t.Time + prep)
                    issues.Add($"{f.Callsign} ({info.Name}) would need to take off at T+{Clock(tTo)}; earliest TOT for this flight is T+{Clock(e.Tot + (_t.Time + prep - tTo))}{(prep > AirPacing.PrepSeconds ? " (slow turnaround: base fuel or maintenance damaged)" : "")}.");
            }
            else
            {
                var start = Math.Max(e.Start, _t.Time + prep);
                var tSt = start + dist / v;
                f.Route.Add(new("Take-off", home.Lon, home.Lat, start));
                f.Route.Add(new("Station", aim.Item1, aim.Item2, tSt));
                f.Route.Add(new("Off station", aim.Item1, aim.Item2, tSt + e.Duration));
                f.Route.Add(new("Land", home.Lon, home.Lat, tSt + e.Duration + dist / v));
            }
            flights.Add(f);
        }
        if (flights.Count == 0 && issues.Count == 0) issues.Add("Add at least one flight.");
        var pkg = strike ? new Package { Id = pkgId, Name = e.Name != "" ? e.Name : $"Package {(char)('A' + (_next - 1) % 26)}", Side = e.Side, Target = target!, Tot = e.Tot, Tanker = hasTanker, IpKm = e.IpKm, Ip = ip, Aimpoint = e.Aimpoint } : null;
        if (pkg != null) pkg.Flights.AddRange(flights);
        var ok = issues.Count == 0;
        if (ok && commit)
        {
            _next++;
            _calls[e.Side] = _calls.GetValueOrDefault(e.Side) + flights.Count;
            foreach (var f in flights) { Flights.Add(f); foreach (var a in f.Aircraft) Tasked.Add(a); }
            foreach (var ((b, s), n) in drawn)
                if (s == "~avfuel") { if (_t.Logistics?.Node(b) is { } fn) _t.Logistics.Consume(fn, "avfuel", n); }
                else Munitions.Add(b, s, -n);
            if (pkg != null) Packages.Add(pkg);
            _t.Report("ato", pkg != null
                ? $"{Side(e.Side)} ATO: {pkg.Name} → {target!.Name}, TOT T+{Clock(e.Tot)}, {flights.Count} flights ({string.Join(", ", flights.Select(f => $"{f.Callsign} {f.Aircraft.Count}× {f.Type.Name} {f.Mission}"))})."
                : $"{Side(e.Side)} ATO: {string.Join(", ", flights.Select(f => $"{f.Callsign} {f.Aircraft.Count}× {f.Type.Name} {f.Mission}"))} on station from T+{Clock(flights[0].Route[1].T)}.");
        }
        return new PlanResult(ok, issues, pkg, flights);

        PlanResult Fail(string why) => new(false, new[] { why }, null, Array.Empty<Flight>());
    }

    private bool BaseCanLaunch(string baseId, Mission m) =>
        !_t.Bases.TryGetValue(baseId, out var b) || (b.RunwayOpen && (m is not (Mission.Strike or Mission.SEAD) || b.CanArm));

    /// <summary>Missile fire mission: launch time = TOT − flight time, checked for reach, stock and pads.</summary>
    private PlanResult PlanFire(AtoEntry e, bool commit)
    {
        var l = _t.Launchers.FirstOrDefault(x => x.Def.Id == e.Launcher);
        var target = _t.Site(e.Target);
        if (l == null || target == null) return new(false, new[] { "Unknown launcher or target." }, null, Array.Empty<Flight>());
        var issues = new List<string>();
        if (l.Def.Side != e.Side) issues.Add("That launcher belongs to the other side.");
        if (!Usable(l.Def.Id)) issues.Add($"{l.Def.Name} is committed to another operation.");
        if (!TargetPolicy.IsLegitimate(target.Class)) issues.Add(TargetPolicy.IsKnown(target.Class) ? TargetPolicy.ProtectedMessage : "Unclassified target class: refused.");
        if (target.Side == e.Side) issues.Add("That target is on your own side.");
        var dist = Km((l.Def.Lon, l.Def.Lat), (target.Lon, target.Lat));
        if (dist > l.ReachKm) issues.Add($"{target.Name} is {dist:0} km from {l.Def.Name}; reach {l.ReachKm:0} km.");
        var committed = Fires.Where(f => f.Launcher == l && f.State == FireState.Planned).Sum(f => f.Count);
        if (l.Stock - committed < e.Count) issues.Add($"{l.Def.Name} has {l.Stock - committed} rounds not already tasked.");
        var flight = dist / Pacing.SpeedKmPerSec(l.Missile.R("speed"));
        var launchAt = e.Tot - flight;
        if (launchAt < _t.Time) issues.Add($"{l.Missile.Name} needs {flight / 60:0.0} min to fly {dist:0} km; earliest TOT is T+{Clock(_t.Time + flight)}.");
        var ok = issues.Count == 0;
        if (ok && commit)
        {
            var task = new MissileTask { Id = $"{e.Side}-fire-{_next++}", Side = e.Side, Launcher = l, Target = target, Aimpoint = e.Aimpoint, Count = e.Count, Tot = e.Tot, LaunchAt = launchAt };
            Fires.Add(task);
            _t.Report("ato", $"{Side(e.Side)} ATO: {l.Def.Name} fires {e.Count}× {l.Missile.Name} at {target.Name}, launch T+{Clock(launchAt)}, TOT T+{Clock(e.Tot)}.");
        }
        return new PlanResult(ok, ok ? new[] { $"{l.Def.Name}: {e.Count}× {l.Missile.Name}, {dist:0} km, flight {flight / 60:0.0} min, launch T+{Clock(launchAt)} for TOT T+{Clock(e.Tot)}." } : issues, null, Array.Empty<Flight>());
    }

    // ---------------- control ----------------

    /// <summary>Abort: the flight turns for home now (strike flights will not release).</summary>
    public string Abort(Flight f)
    {
        if (f.State is FlightState.Landed or FlightState.Lost) return "That flight is not flying.";
        var now = _t.Time;
        f.Aborted = true; f.Released = true;
        var (lon, lat) = f.Pos(now);
        var home = _t.Site(f.BaseId)!;
        var v = AirPacing.CruiseKmPerSec(f.Type.R("speed"));
        var keep = f.Route.Where(w => w.T <= now).ToList();
        f.Route.Clear();
        f.Route.AddRange(keep.Count > 0 ? keep : new List<Waypoint> { new("Take-off", home.Lon, home.Lat, now) });
        if (now < f.Takeoff) { f.Route.Clear(); f.Route.Add(new("Take-off", home.Lon, home.Lat, now)); f.Route.Add(new("Land", home.Lon, home.Lat, now + 1)); }
        else { f.Route.Add(new("Abort", lon, lat, now)); f.Route.Add(new("Land", home.Lon, home.Lat, now + Km((lon, lat), (home.Lon, home.Lat)) / v)); }
        _t.Report("air", $"{f.Callsign} aborted, returning to {home.Name}.");
        return $"{f.Callsign} is returning to base.";
    }

    /// <summary>Cancel a fire mission that hasn't launched yet.</summary>
    public void Cancel(MissileTask t)
    {
        if (t.State != FireState.Planned) return;
        t.State = FireState.Failed; t.Note = "cancelled";
        _t.Report("ato", $"{Side(t.Side)} ATO: fire mission {t.Launcher.Def.Name} → {t.Target.Name} cancelled.");
    }

    public void SetFormation(Flight f, Formation form) { f.Formation = form; _t.Report("air", $"{f.Callsign} changed formation to {form}."); }

    /// <summary>Aircraft on the ground at a hit base: apron aircraft are exposed, sheltered ones much less.</summary>
    public string GroundLosses(BaseState b, string effect)
    {
        if (effect is not ("apron" or "shelter")) return "";
        var parked = Idle(b.Site.Side).Where(u => u.Base == b.Site.Id).ToList();
        var cap = b.ShelterCapacity;
        var exposed = effect == "apron" ? parked.Skip(cap).ToList() : parked.Take(cap).ToList();
        var p = effect == "apron" ? 0.35 : 0.12;
        var lost = exposed.Where(_ => _rng.NextDouble() < p).ToList();
        foreach (var u in lost) Destroyed.Add(u.Id);
        return lost.Count == 0 ? "" : $" · {lost.Count} aircraft destroyed on the ground ({string.Join(", ", lost.GroupBy(u => _preset(u.Preset)?.Name ?? u.Preset).Select(g => $"{g.Count()}× {g.Key}"))})";
    }

    private string Side(string s) => _t.Scenario.Sides.GetValueOrDefault(s, s);
    public static string Clock(double t) { var ts = TimeSpan.FromSeconds(Math.Max(0, t)); return $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}"; }
    private static double Km((double, double) a, (double, double) b) => Pacing.DistanceKm(a.Item1, a.Item2, b.Item1, b.Item2);
    private static (double, double) Lerp((double, double) a, (double, double) b, double f) => (a.Item1 + (b.Item1 - a.Item1) * f, a.Item2 + (b.Item2 - a.Item2) * f);

    // ---------------- execution ----------------

    /// <summary>Aircraft ferrying to another base: unit id → (base, arrival time). Unavailable until they arrive.</summary>
    public Dictionary<string, (string Base, double Arrive)> Ferrying { get; } = new();

    /// <summary>Relocate aircraft to another base (dispersal). They are unavailable while ferrying, then operate from there.</summary>
    public string Relocate(IEnumerable<string> units, string baseId)
    {
        var dest = _t.Site(baseId);
        if (dest == null) return "Unknown base.";
        var n = 0;
        foreach (var id in units)
        {
            var u = _t.Scenario.Units.FirstOrDefault(x => x.Id == id);
            if (u == null || Tasked.Contains(id) || Destroyed.Contains(id)) continue;
            var from = _t.Site(BaseOf(u));
            var km = from == null ? 0 : Pacing.DistanceKm(from.Lon, from.Lat, dest.Lon, dest.Lat);
            var v = AirPacing.CruiseKmPerSec(_preset(u.Preset)?.R("speed") ?? 5);
            Ferrying[id] = (baseId, _t.Time + AirPacing.PrepSeconds + km / v);
            Tasked.Add(id); n++;
        }
        _t.Report("air", $"{Side(_t.Scenario.Units.FirstOrDefault(u => u.Id == units.FirstOrDefault())?.Side ?? "")} relocating {n} aircraft to {dest.Name}.");
        return $"{n} aircraft ferrying to {dest.Name}.";
    }

    public void Tick(double prev, double now)
    {
        foreach (var (id, (b, at)) in Ferrying.Where(k => now >= k.Value.Arrive).ToList())
        {
            Ferrying.Remove(id); Tasked.Remove(id);
            if (!Destroyed.Contains(id)) _divertBase[id] = b;
        }
        foreach (var fire in Fires.Where(x => x.State == FireState.Planned && now >= x.LaunchAt))
        {
            var r = _t.Launch(fire.Launcher.Def.Id, fire.Target.Id, fire.Count, fire.Aimpoint);
            fire.State = r.Ok ? FireState.Launched : FireState.Failed;
            fire.Note = r.Message;
        }
        foreach (var f in Flights.Where(f => f.State is not (FlightState.Landed or FlightState.Lost)))
        {
            if (now < f.Takeoff) continue;
            if (now >= f.LandAt && _t.Bases.TryGetValue(f.BaseId, out var home) && !home.RunwayOpen && f.Route[^1].Name != "Divert")
            {
                // runway cratered while airborne: divert to the nearest friendly base with an open runway
                var alt = _t.Bases.Values.Where(b => b.Site.Side == f.Side && b.RunwayOpen && b.Type.Components.Any(c => c.Effect == "sorties"))
                    .OrderBy(b => Km((home.Site.Lon, home.Site.Lat), (b.Site.Lon, b.Site.Lat))).FirstOrDefault();
                if (alt != null)
                {
                    var v = AirPacing.CruiseKmPerSec(f.Type.R("speed"));
                    f.Route.Add(new("Divert", alt.Site.Lon, alt.Site.Lat, now + Km((home.Site.Lon, home.Site.Lat), (alt.Site.Lon, alt.Site.Lat)) / v));
                    _t.Report("air", $"{f.Callsign} can't land at {home.Site.Name} (runway closed); diverting to {alt.Site.Name}.");
                    f.BaseId = alt.Site.Id;
                    foreach (var a in f.Aircraft) _divertBase[a] = alt.Site.Id;
                    continue;
                }
            }
            if (now >= f.LandAt)
            {
                f.State = FlightState.Landed;
                foreach (var a in f.Aircraft) Tasked.Remove(a);
                var back = ReturnStores(f);
                _t.Report("air", $"{f.Callsign} landed at {_t.Site(f.BaseId)?.Name} with {f.Aircraft.Count} aircraft{(back > 0 ? $"; {back} unused stores returned to the magazine" : "")}.");
                continue;
            }
            var next = f.Next(now)!;
            f.State = next.Name switch
            {
                "Ingress" => FlightState.Airborne,
                "IP" => FlightState.Ingress,
                "Target" or "SEAD" or "Sweep" => FlightState.Attack,
                "Egress" or "Land" when f.Mission is Mission.CAP or Mission.AEW or Mission.Tanker => FlightState.Egress,
                "Egress" or "Land" => FlightState.Egress,
                "Off station" => FlightState.OnStation,
                _ => FlightState.Airborne,
            };
            if (prev < f.Takeoff && now >= f.Takeoff) _t.Report("air", $"{f.Callsign} ({f.Aircraft.Count}× {f.Type.Name}, {f.Formation}) airborne from {_t.Site(f.BaseId)?.Name}.");
            var tgtWp = f.Route.FirstOrDefault(w => w.Name == "Target");
            if (tgtWp != null && !f.Released && now >= tgtWp.T)
            {
                f.Released = true;
                if (f.Mission == Mission.Recon) ResolveRecon(f); else ResolveStrike(f);
            }
        }
        AirCombat(now);
    }

    /// <summary>
    /// Move one waypoint of a flight that hasn't taken off (ingress, IP, egress, station...). The anchor time (time on
    /// target, or station arrival) stays fixed; the legs before it are worked back and the ones after it forward.
    /// </summary>
    public string MoveWaypoint(Flight f, int index, double lon, double lat)
    {
        if (f.State != FlightState.Planned || _t.Time >= f.Takeoff) return $"{f.Callsign} is already flying: its route can't be edited.";
        if (index <= 0 || index >= f.Route.Count - 1) return "Take-off and landing stay at the base.";
        var w = f.Route[index];
        if (w.Name is "Target" or "SEAD" or "Sweep" or "Off station") return "The objective point is set by the mission: move the ingress, IP or egress instead.";
        var old = f.Route.ToList();
        var anchor = f.Route.FindIndex(x => x.Name is "Target" or "SEAD" or "Sweep" or "Station");
        if (anchor < 0) return "Nothing to anchor the timing to.";
        f.Route[index] = w with { Lon = lon, Lat = lat };
        if (w.Name == "Station" && index + 1 < f.Route.Count && f.Route[index + 1].Name == "Off station") f.Route[index + 1] = f.Route[index + 1] with { Lon = lon, Lat = lat };
        var v = AirPacing.CruiseKmPerSec(f.Type.R("speed"));
        double Leg(int i, int j) => Km((f.Route[i].Lon, f.Route[i].Lat), (f.Route[j].Lon, f.Route[j].Lat)) / (v * (f.Route[j].Name is "Target" or "SEAD" or "Sweep" ? 1.2 : 1));
        if (w.Name == "Station" || f.Route[anchor].Name == "Station")
            f.Route[anchor] = f.Route[anchor] with { T = f.Route[0].T + Leg(0, anchor) };
        for (var i = anchor - 1; i >= 0; i--) f.Route[i] = f.Route[i] with { T = f.Route[i + 1].T - Leg(i, i + 1) };
        for (var i = anchor + 1; i < f.Route.Count; i++)
            f.Route[i] = f.Route[i] with { T = f.Route[i].Name == "Off station" ? f.Route[i - 1].T + (old[i].T - old[i - 1].T) : f.Route[i - 1].T + Leg(i - 1, i) };
        var prep = AirPacing.PrepSeconds * (_t.Bases.TryGetValue(f.BaseId, out var hb) ? hb.PrepFactor : 1);
        if (f.Route[0].T < _t.Time + prep)
        {
            f.Route.Clear(); f.Route.AddRange(old);
            return $"That route is too long: {f.Callsign} would have to take off before it can be ready. Waypoint not moved.";
        }
        var radius = AirPacing.RadiusKm(f.Type.R("reach")) + (f.Mission == Mission.Tanker || Packages.FirstOrDefault(p => p.Id == f.PackageId)?.Tanker == true ? AirPacing.TankerBonusKm : 0);
        var total = Enumerable.Range(0, f.Route.Count - 1).Sum(i => Km((f.Route[i].Lon, f.Route[i].Lat), (f.Route[i + 1].Lon, f.Route[i + 1].Lat)));
        if (total > radius * 2.6)
        {
            f.Route.Clear(); f.Route.AddRange(old);
            return $"Out of fuel: that route is {total:0} km for a {radius:0} km radius. Waypoint not moved.";
        }
        _t.Report("air", $"{f.Callsign}: {w.Name} moved; take-off T+{Clock(f.Takeoff)}, lands T+{Clock(f.LandAt)}.");
        return "";
    }

    /// <summary>Airframe class for av-fuel use per sortie (logistics data keys).</summary>
    public string SortieClass(string preset, Mission m) =>
        m switch { Mission.Tanker => "tanker", Mission.AEW => "aew", Mission.Jamming => "jamming", _ => Stores?.Template(preset)?.Id ?? "default" };

    /// <summary>A ship contact from the side's own picture as a strike aimpoint (fresh contacts only).</summary>
    private SiteDef? ShipTarget(string side, string key, out string? why)
    {
        why = null;
        var c = _t.Intel?.Pictures[side].Contacts.GetValueOrDefault(key);
        var ship = _t.Naval?.Find(key);
        if (c == null || ship == null || _t.Time - c.LastSeen > IntelRules.StaleAfter) { why = "No fresh contact on that ship: find it first."; return null; }
        return new SiteDef("ship:" + key, $"{c.Label} (contact)", ship.Side, "military-force", c.Lon, c.Lat, "ship");
    }

    /// <summary>An airborne, emitting stand-off jammer of this side covering the point (or null).</summary>
    public Flight? Jammer(string side, double lon, double lat) =>
        Flights.FirstOrDefault(j => j.Side == side && j.Mission == Mission.Jamming && j.Airborne(_t.Time) && j.Aircraft.Count > 0 && _t.Emitting(j.Id)
            && Pacing.DistanceKm(j.Pos(_t.Time).Lon, j.Pos(_t.Time).Lat, lon, lat) <= AirPacing.JamKm);

    /// <summary>Air-launched anti-ship attack: weapons fly to where the picture saw the ship; a ship that moved beyond the seeker basket is missed.</summary>
    private void ResolveShipStrike(Flight f, SiteDef tgt, int lost, int covering)
    {
        var ship = _t.Naval?.Find(tgt.Id[5..]);
        var weapons = Stores == null ? new List<StoreDef>() : f.Loadout.Values.Where(l => Stores.Stores.TryGetValue(l.Store, out var s) && s.Kind is "asm" or "agm")
            .SelectMany(l => Enumerable.Repeat(Stores.Stores[l.Store], l.Qty * f.Aircraft.Count)).ToList();
        if (ship == null || ship.Sunk) { _t.Report("air", $"{f.Callsign}: no ship at {tgt.Name}; weapons kept."); return; }
        if (weapons.Count == 0) { _t.Report("air", $"{f.Callsign} over {tgt.Name}: no anti-ship weapons carried."); return; }
        foreach (var w in weapons) f.Expended[w.Id] = f.Expended.GetValueOrDefault(w.Id) + 1;
        var moved = Pacing.DistanceKm(ship.Lon, ship.Lat, tgt.Lon, tgt.Lat);
        var hits = 0; var effects = new List<string>();
        foreach (var w in weapons)
        {
            if (moved > NavalRules.SeekerKm(w.Accuracy)) continue;
            if (_rng.NextDouble() >= Math.Clamp(0.2 + 0.06 * w.Accuracy - 0.02 * ship.Batteries.Count(b => b.Stock > 0), 0.05, 0.9)) continue;
            hits++;
            var e = _t.Naval!.Hit(ship, NavalRules.MissileDamage(w.Punch), $"{f.Callsign} ({w.Name})");
            if (e != "") effects.Add(e);
            if (ship.Sunk) break;
        }
        _t.Report(hits > 0 ? "impact" : "air", $"{f.Callsign}: {weapons.Count} anti-ship weapons at {tgt.Name} · {hits} hits" + (moved > 10 ? $" (ship had moved {moved:0} km since last seen)" : "")
            + $" · {lost} lost to {covering} covering batteries" + (effects.Count > 0 ? " · " + effects.Last() : "") + ".");
    }

    /// <summary>On landing, stores not fired go back to the base's magazine. Released air-to-ground weapons are gone.</summary>
    private int ReturnStores(Flight f)
    {
        if (Stores == null || f.Loadout.Count == 0 || f.Aircraft.Count == 0) return 0;
        var released = f.Released && !f.HoldFire && !f.Aborted;
        var n = 0;
        foreach (var g in f.Loadout.Values.GroupBy(l => l.Store))
        {
            var s = Stores.Stores.GetValueOrDefault(g.Key);
            if (s == null || (released && s.AirToGround)) continue;
            var left = Math.Max(0, g.Sum(x => x.Qty) * f.Aircraft.Count - f.Expended.GetValueOrDefault(g.Key));
            if (left > 0) { Munitions.Add(f.BaseId, g.Key, left); n += left; }
        }
        return n;
    }

    /// <summary>Downed crews: a rescue helicopter on alert at a friendly base within range may recover them.</summary>
    public Dictionary<string, int> CrewsRecovered { get; } = new();
    public Dictionary<string, int> CrewsLost { get; } = new();
    private void Rescue(Flight f, int lost)
    {
        if (lost <= 0) return;
        var (lon, lat) = f.Pos(_t.Time);
        var heli = Idle(f.Side).Where(u => (_preset(u.Preset)?.R("rescue") ?? 0) > 0)
            .Select(u => (u, b: _t.Site(u.Base))).Where(x => x.b != null && Pacing.DistanceKm(x.b.Lon, x.b.Lat, lon, lat) <= AirPacing.RescueKm)
            .OrderBy(x => Pacing.DistanceKm(x.b!.Lon, x.b.Lat, lon, lat)).FirstOrDefault();
        var saved = 0;
        if (heli.u != null)
        {
            var p = Math.Clamp(0.35 + 0.05 * (_preset(heli.u.Preset)?.R("rescue") ?? 0) - (_t.Intel?.Pictures[f.Side == "blue" ? "red" : "blue"].Contacts.Values.Count(c => c.Kind == "air" && Pacing.DistanceKm(c.Lon, c.Lat, lon, lat) < 100) ?? 0) * 0.05, 0.1, 0.9);
            for (var i = 0; i < lost; i++) if (_rng.NextDouble() < p) saved++;
        }
        CrewsRecovered[f.Side] = CrewsRecovered.GetValueOrDefault(f.Side) + saved;
        CrewsLost[f.Side] = CrewsLost.GetValueOrDefault(f.Side) + lost - saved;
        _t.Report("air", heli.u == null ? $"{f.Callsign}: {lost} crew(s) down, no rescue helicopter within {AirPacing.RescueKm:0} km."
            : $"{f.Callsign}: {saved} of {lost} downed crew(s) recovered by {heli.u.Name} from {heli.b!.Name}.");
    }

    private IEnumerable<Flight> Up(double now) => Flights.Where(f => f.Airborne(now) && f.Aircraft.Count > 0);

    private void AirCombat(double now)
    {
        if (!_t.AtWar) return; // hostile, not at war: intercepts shadow each other but don't fire
        var up = Up(now).ToList();
        foreach (var a in up.Where(f => f.Side == "blue"))
            foreach (var b in up.Where(f => f.Side == "red"))
            {
                if (a.Aircraft.Count == 0 || b.Aircraft.Count == 0) continue;
                if (a.Mission is Mission.Tanker || b.Mission is Mission.Tanker) continue;
                var (alon, alat) = a.Pos(now); var (blon, blat) = b.Pos(now);
                if (Pacing.DistanceKm(alon, alat, blon, blat) > AirPacing.DogfightKm) continue;
                if (_lastFight.TryGetValue((a.Id, b.Id), out var last) && now - last < 120) continue;
                _lastFight[(a.Id, b.Id)] = now;
                // escorts step in for their strikers
                var x = Defender(a, now); var y = Defender(b, now);
                Fight(x, y, now);
            }
    }

    private Flight Defender(Flight f, double now)
    {
        if (f.Mission != Mission.Strike && f.Mission != Mission.AEW) return f;
        var esc = Up(now).FirstOrDefault(e => e.PackageId == f.PackageId && e.Mission == Mission.Escort && e.PackageId != "");
        return esc ?? f;
    }

    private bool AewNear(string side, double lon, double lat, double now) =>
        Up(now).Any(f => f.Side == side && f.Mission == Mission.AEW && Pacing.DistanceKm(f.Pos(now).Lon, f.Pos(now).Lat, lon, lat) < 400);

    /// <summary>Kill chance for one shooter against a flight: ratings, formation and AEW support.</summary>
    public double AirToAirChance(Flight shooter, Flight target, double now)
    {
        var s = shooter.Type; var t = target.Type;
        var (lon, lat) = shooter.Pos(now);
        var p = 0.15 + 0.05 * (s.R("air-defence") - t.R("air-defence")) + 0.03 * (s.R("detection") - t.R("signature"))
            + 0.02 * (s.R("agility") - t.R("agility")) + FormationRules.Mod(shooter.Formation).AirToAir + (AewNear(shooter.Side, lon, lat, now) ? 0.05 : 0);
        // loadout: the best air-to-air missile carried adds to the chance; none carried leaves guns only
        if (Stores != null && shooter.Loadout.Count > 0)
        {
            var aam = shooter.Loadout.Values.Select(l => Stores.Stores.GetValueOrDefault(l.Store)).Where(x => x is { Kind: "aam" }).Select(x => x!.Air).DefaultIfEmpty(-1).Max();
            p = aam < 0 ? p * 0.3 : p + 0.02 * (aam - 5);
        }
        return Math.Clamp(p, 0.03, 0.75);
    }

    private void Fight(Flight a, Flight b, double now)
    {
        // you can only shoot what your own sensors hold: an undetected (stealthy) flight gets free shots
        var seeA = _t.Intel?.Pictures[a.Side].Knows(b.Id, now) ?? true;
        var seeB = _t.Intel?.Pictures[b.Side].Knows(a.Id, now) ?? true;
        if (!seeA && !seeB) return;
        var pa = seeA ? AirToAirChance(a, b, now) : 0; var pb = seeB ? AirToAirChance(b, a, now) : 0;
        var ka = a.Aircraft.Count(_ => _rng.NextDouble() < pa);
        var kb = b.Aircraft.Count(_ => _rng.NextDouble() < pb);
        if (seeA) Fire(a); if (seeB) Fire(b);
        var lostB = Remove(b, ka); var lostA = Remove(a, kb);
        Rescue(a, lostA); Rescue(b, lostB);
        _t.Report("air", $"Air combat: {a.Callsign} ({a.Type.Name}, {pa:P0} per shot) vs {b.Callsign} ({b.Type.Name}, {pb:P0}) · {Side(a.Side)} lost {lostA}, {Side(b.Side)} lost {lostB}.");
    }

    /// <summary>Each aircraft in an engagement fires its best air-to-air missile (if any left).</summary>
    private void Fire(Flight f)
    {
        if (Stores == null) return;
        var aam = f.Loadout.Values.Where(l => Stores.Stores.GetValueOrDefault(l.Store)?.Kind == "aam").GroupBy(l => l.Store)
            .Select(g => (Store: g.Key, Left: g.Sum(x => x.Qty) * f.Aircraft.Count - f.Expended.GetValueOrDefault(g.Key), Air: Stores.Stores[g.Key].Air))
            .Where(x => x.Left > 0).OrderByDescending(x => x.Air).FirstOrDefault();
        if (aam.Store != null) f.Expended[aam.Store] = f.Expended.GetValueOrDefault(aam.Store) + Math.Min(aam.Left, f.Aircraft.Count);
    }

    private int Remove(Flight f, int n)
    {
        n = Math.Min(n, f.Aircraft.Count);
        for (var i = 0; i < n; i++)
        {
            var id = f.Aircraft[^1];
            f.Aircraft.RemoveAt(f.Aircraft.Count - 1);
            f.LostAircraft.Add(id);
            Tasked.Remove(id);
            Destroyed.Add(id);
        }
        if (f.Aircraft.Count == 0) f.State = FlightState.Lost;
        return n;
    }

    private void ResolveRecon(Flight f)
    {
        var tgt = Packages.FirstOrDefault(p => p.Id == f.PackageId)?.Target;
        if (tgt != null && _t.Bases.TryGetValue(tgt.Id, out var b) && _t.Intel != null)
        {
            _t.Intel.Recon(f.Side, b, _t.Time);
            _t.Report("isr", $"{f.Callsign} recon over {tgt.Name}: {b.Status()}.");
        }
    }

    private void ResolveStrike(Flight f)
    {
        var pkg = Packages.FirstOrDefault(p => p.Id == f.PackageId);
        var tgt = pkg?.Target ?? _t.Site(f.Route.First(w => w.Name == "Target").Name)!;
        var covering = _t.Batteries.Where(b => b.Def.Side != f.Side && b.Stock > 0 && Pacing.DistanceKm(b.Def.Lon, b.Def.Lat, tgt.Lon, tgt.Lat) <= b.ReachKm * 1.2).ToList();
        var sead = pkg != null && pkg.Flights.Any(x => x.Mission == Mission.SEAD && x.Aircraft.Count > 0);
        var form = FormationRules.Mod(f.Formation);
        var jammed = Jammer(f.Side, tgt.Lon, tgt.Lat) != null;
        var pLoss = Math.Clamp(0.08 * covering.Count - 0.025 * f.Type.R("signature") + form.SamLoss - (sead ? 0.08 : 0) - (jammed ? 0.06 : 0), 0.01, 0.6);
        var lost = 0;
        if (covering.Count > 0)
            foreach (var _ in f.Aircraft.ToList())
            {
                var bat = covering[_rng.Next(covering.Count)];
                if (bat.Stock <= 0) continue;
                bat.Stock--;
                if (_rng.NextDouble() < pLoss) lost++;
            }
        Remove(f, lost);
        Rescue(f, lost);
        if (f.HoldFire)
        {
            _t.Report("air", $"{f.Callsign}: weapons held over {tgt.Name} (operation rule); {lost} lost to covering SAM batteries.");
            return;
        }
        if (tgt.Id.StartsWith("ship:")) { ResolveShipStrike(f, tgt, lost, covering.Count); return; }
        var pHit = Math.Clamp(0.25 + 0.06 * f.Type.R("strike") + 0.02 * f.Type.R("payload") + form.Strike, 0.05, 0.95);
        var effects = new List<string>();
        _t.Bases.TryGetValue(tgt.Id, out var tb);
        int hits;
        // with a loadout, every air-to-ground store is a weapon: its own accuracy and punch (game ratings)
        var weapons = Stores == null ? new List<StoreDef>() : f.Loadout.Values.Where(l => Stores.Stores.TryGetValue(l.Store, out var s) && s.AirToGround)
            .SelectMany(l => Enumerable.Repeat(Stores.Stores[l.Store], l.Qty * f.Aircraft.Count)).ToList();
        string how;
        if (weapons.Count > 0)
        {
            hits = 0;
            foreach (var w in weapons)
            {
                var p = Math.Clamp(0.2 + 0.06 * w.Accuracy + 0.02 * f.Type.R("strike") + form.Strike, 0.05, 0.95);
                if (_rng.NextDouble() >= p) continue;
                hits++;
                if (tb != null) effects.Add(_t.ApplyHit(f.Side, tb, pkg?.Aimpoint ?? "", w.Punch, w.Accuracy, 0.4, "pilot report"));
            }
            how = $"{weapons.Count} weapons ({string.Join(", ", weapons.GroupBy(w => w.Name).Select(g => $"{g.Count()}× {g.Key}"))})";
        }
        else
        {
            hits = f.Aircraft.Count(_ => _rng.NextDouble() < pHit);
            if (tb != null) for (var i = 0; i < hits; i++) effects.Add(_t.ApplyHit(f.Side, tb, pkg?.Aimpoint ?? "", 3 + 0.5 * f.Type.R("payload"), f.Type.R("strike"), 0.4, "pilot report"));
            how = $"{pHit:P0} each: strike {f.Type.R("strike"):0}, payload {f.Type.R("payload"):0}";
        }
        if (hits > 0) _t.Hits[tgt.Id] = _t.Hits.GetValueOrDefault(tgt.Id) + hits;
        effects = effects.Distinct().Take(6).ToList();
        var text = $"{f.Callsign}: {f.Aircraft.Count}× {f.Type.Name} released on {tgt.Name} at TOT · {hits} hits ({how}) · " +
                   $"{lost} lost to {covering.Count} covering SAM batteries ({pLoss:P0} each{(sead ? ", SEAD suppressing" : "")}, stealth {f.Type.R("signature"):0})" +
                   (effects.Count > 0 ? " · " + string.Join("; ", effects) : "") + ".";
        if (pkg != null) pkg.Result = text;
        _t.Report(hits > 0 ? "impact" : "air", text);
    }
}
