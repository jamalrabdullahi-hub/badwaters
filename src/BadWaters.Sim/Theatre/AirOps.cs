using BadWaters.Sim.Policy;

namespace BadWaters.Sim.Theatre;

// Air tasking: packages of flights planned backwards from a time on target (TOT), flown along waypoints
// (take-off → ingress → IP → target → egress → land) and resolved with dice against 0–10 ratings.
// Movement is interpolation between planned waypoint times; there is no flight model or physics.

public enum Mission { Strike, Escort, SEAD, CAP, AEW, Tanker, Recon }
public enum Formation { Wedge, LineAbreast, Trail, EchelonRight, Box }
public enum FlightState { Planned, Airborne, Ingress, Attack, Egress, OnStation, Landed, Lost }

public sealed record Waypoint(string Name, double Lon, double Lat, double T);
public sealed record PresetInfo(string Id, string Name, IReadOnlyDictionary<string, double> Ratings)
{
    public double R(string k) => Ratings.TryGetValue(k, out var v) ? v : 0;
}
public sealed record FlightRequest(string Preset, int Count, Mission Mission, Formation Formation = Formation.Wedge);

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
    public string BaseOf(UnitDef u) => _divertBase.TryGetValue(u.Id, out var b) ? b : u.Base;
    private static readonly string[] BlueCalls = { "EAGLE", "VIPER", "COBRA", "HAWK", "RAVEN", "LANCE", "SABRE", "FALCON" };
    private static readonly string[] RedCalls = { "DRAGON", "TIGER", "WOLF", "FLAME", "STORM", "ARROW", "THUNDER", "IRON" };

    public AirTasking(InterceptTrial trial, Func<string, PresetInfo?> preset, int seed = 1)
    {
        _t = trial;
        _preset = preset;
        _rng = new Random(seed * 7919 + 3);
        foreach (var e in trial.Scenario.Ato.Where(e => trial.Control.GetValueOrDefault(e.Side) == SideControl.AI))
            Commit(e);
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

    public PlanResult Plan(AtoEntry e, bool commit = false)
    {
        if (e.Launcher != "") return PlanFire(e, commit);
        var issues = new List<string>();
        var strike = e.Target != "";
        SiteDef? target = null;
        if (strike)
        {
            target = _t.Site(e.Target);
            if (target == null) return Fail("Unknown target.");
            if (!TargetPolicy.IsLegitimate(target.Class)) return Fail(TargetPolicy.IsKnown(target.Class) ? TargetPolicy.ProtectedMessage : "Unclassified target class: refused.");
            if (target.Side == e.Side) return Fail("That target is on your own side.");
        }
        var aim = strike ? (target!.Lon, target.Lat) : (e.StationLon, e.StationLat);
        var pkgId = $"{e.Side}-pkg-{_next}";
        var calls = e.Side == "blue" ? BlueCalls : RedCalls;
        var flights = new List<Flight>();
        var reserved = new HashSet<string>();
        (double Lon, double Lat) ip = aim;
        var side = _next % 2 == 0 ? 1 : -1; // which side of the direct line the route bends to

        var hasTanker = e.Tanker || e.Flights.Any(r => r.Mission == Mission.Tanker);
        foreach (var (req, idx) in e.Flights.Select((r, i) => (r, i)).OrderBy(x => x.r.Mission == Mission.Tanker))
        {
            var info = _preset(req.Preset);
            if (info == null) { issues.Add($"Unknown aircraft type {req.Preset}."); continue; }
            // the base with enough idle aircraft of this type that is closest to the objective
            var pool = Idle(e.Side).Where(u => u.Preset == req.Preset && !reserved.Contains(u.Id)).GroupBy(u => u.Base)
                .Select(g => (Base: _t.Site(g.Key), Units: g.ToList())).Where(x => x.Base != null && x.Units.Count >= req.Count && BaseCanLaunch(x.Base.Id, req.Mission))
                .OrderBy(x => Pacing.DistanceKm(x.Base!.Lon, x.Base.Lat, aim.Item1, aim.Item2)).FirstOrDefault();
            if (pool.Base == null) { issues.Add($"Not enough idle {info.Name} at any single working base for {req.Count} aircraft (runway closed or munitions store destroyed counts as not working)."); continue; }
            var prep = AirPacing.PrepSeconds * (_t.Bases.TryGetValue(pool.Base.Id, out var hb) ? hb.PrepFactor : 1);
            var home = pool.Base;
            var dist = Pacing.DistanceKm(home.Lon, home.Lat, aim.Item1, aim.Item2);
            var radius = AirPacing.RadiusKm(info.R("reach")) + (hasTanker && req.Mission != Mission.Tanker ? AirPacing.TankerBonusKm : 0);
            if (dist > radius && req.Mission != Mission.Tanker)
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
            foreach (var a in f.Aircraft) reserved.Add(a);

            if (req.Mission == Mission.Tanker)
            {
                // tanker orbits 45% of the way out from the package's first base, from first take-off until after TOT
                var first = flights.FirstOrDefault();
                var anchor = first != null ? _t.Site(first.BaseId)! : home;
                var st = Lerp((anchor.Lon, anchor.Lat), aim, 0.45);
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

    public void Tick(double prev, double now)
    {
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
                _t.Report("air", $"{f.Callsign} landed at {_t.Site(f.BaseId)?.Name} with {f.Aircraft.Count} aircraft.");
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

    private IEnumerable<Flight> Up(double now) => Flights.Where(f => f.Airborne(now) && f.Aircraft.Count > 0);

    private void AirCombat(double now)
    {
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
        return Math.Clamp(0.15 + 0.05 * (s.R("air-defence") - t.R("air-defence")) + 0.03 * (s.R("detection") - t.R("signature"))
            + 0.02 * (s.R("agility") - t.R("agility")) + FormationRules.Mod(shooter.Formation).AirToAir + (AewNear(shooter.Side, lon, lat, now) ? 0.05 : 0), 0.03, 0.75);
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
        var lostB = Remove(b, ka); var lostA = Remove(a, kb);
        _t.Report("air", $"Air combat: {a.Callsign} ({a.Type.Name}, {pa:P0} per shot) vs {b.Callsign} ({b.Type.Name}, {pb:P0}) · {Side(a.Side)} lost {lostA}, {Side(b.Side)} lost {lostB}.");
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
        var pLoss = Math.Clamp(0.08 * covering.Count - 0.025 * f.Type.R("signature") + form.SamLoss - (sead ? 0.08 : 0), 0.01, 0.6);
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
        var pHit = Math.Clamp(0.25 + 0.06 * f.Type.R("strike") + 0.02 * f.Type.R("payload") + form.Strike, 0.05, 0.95);
        var hits = f.Aircraft.Count(_ => _rng.NextDouble() < pHit);
        if (hits > 0) _t.Hits[tgt.Id] = _t.Hits.GetValueOrDefault(tgt.Id) + hits;
        var effects = new List<string>();
        if (_t.Bases.TryGetValue(tgt.Id, out var tb))
            for (var i = 0; i < hits; i++) effects.Add(_t.ApplyHit(f.Side, tb, pkg?.Aimpoint ?? "", 3 + 0.5 * f.Type.R("payload"), f.Type.R("strike"), 0.4, "pilot report"));
        var text = $"{f.Callsign}: {f.Aircraft.Count}× {f.Type.Name} released on {tgt.Name} at TOT · {hits} hits ({pHit:P0} each: strike {f.Type.R("strike"):0}, payload {f.Type.R("payload"):0}) · " +
                   $"{lost} lost to {covering.Count} covering SAM batteries ({pLoss:P0} each{(sead ? ", SEAD suppressing" : "")}, stealth {f.Type.R("signature"):0})" +
                   (effects.Count > 0 ? " · " + string.Join("; ", effects) : "") + ".";
        if (pkg != null) pkg.Result = text;
        _t.Report(hits > 0 ? "impact" : "air", text);
    }
}
