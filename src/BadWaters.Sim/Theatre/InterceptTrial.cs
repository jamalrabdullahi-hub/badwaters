using BadWaters.Sim.Policy;

namespace BadWaters.Sim.Theatre;

// A small real-time theatre engagement: one side launches tactical ballistic missiles, the other detects
// and intercepts them. Everything is rules-based: positions move along a straight line over a fixed flight
// time (the "arc" is only drawn), detection and kills are dice rolls against 0–10 ratings. No physics.

/// <summary>A map site. Type names a base type (airbase, navalbase, garrison) whose components can be hit.</summary>
public sealed record SiteDef(string Id, string Name, string Side, string Class, double Lon, double Lat, string Type = "");
public sealed record BatteryDef(string Id, string Name, string Side, double Lon, double Lat, string Radar, string Launcher, string Interceptor, int Stock, string Platform = "ground");
public sealed record LauncherDef(string Id, string Name, string Side, double Lon, double Lat, string Missile, int Stock, string Platform = "ground");
public sealed record ScriptedLaunch(double T, string Launcher, string Target, int Count);
/// <summary>A force element shown in the theatre (aircraft, ship, submarine, AEW...). Moves on a patrol orbit.</summary>
public sealed record UnitDef(string Id, string Name, string Side, string Kind, string Preset, double Lon, double Lat, double PatrolKm = 0, string Base = "");

public sealed record ScenarioDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Brief { get; init; } = "";
    public double[] Center { get; init; } = { 0, 0 };
    public float Zoom { get; init; } = 3;
    public Dictionary<string, string> Sides { get; init; } = new();
    public List<SiteDef> Sites { get; init; } = new();
    public List<BatteryDef> Batteries { get; init; } = new();
    public List<LauncherDef> Launchers { get; init; } = new();
    /// <summary>Logistics terrain and corridors (tools/logistics-build.py).</summary>
    public ScenarioLogistics? Logistics { get; init; }
    /// <summary>Sea lanes for neutral merchant traffic: each a list of [lon, lat] points at sea.</summary>
    public List<List<double[]>> Neutral { get; init; } = new();
    public List<ScriptedLaunch> Script { get; init; } = new();
    public List<UnitDef> Units { get; init; } = new();
    /// <summary>Air tasking the AI flies for each side (player sides plan their own).</summary>
    public List<AtoEntry> Ato { get; init; } = new();
    /// <summary>Strategic theatre this scenario belongs to (for "Enter Theatre Ops" from a war).</summary>
    public string Theatre { get; init; } = "";
}

public enum SideControl { AI, Player }
public enum TrackState { Flying, Intercepted, Impacted }
public enum ShotState { Flying, Hit, Miss }

/// <summary>One line of a kill-chance breakdown: what changed the odds and which components caused it.</summary>
public sealed record PkTerm(string Label, double Delta, string Sources);

public sealed class Track
{
    public int Id { get; init; }
    public required string Side { get; init; }
    public required SystemProfile Missile { get; init; }
    public required string LauncherId { get; init; }
    public required SiteDef Target { get; init; }
    /// <summary>Base component this weapon is aimed at (runway, fuel...), if the target is a base.</summary>
    public string Aimpoint { get; init; } = "";
    /// <summary>Ship this weapon was fired at (its Target is the aimpoint where the ship was last seen).</summary>
    public string TargetShip { get; init; } = "";
    public double FromLon { get; init; }
    public double FromLat { get; init; }
    public double Launched { get; init; }
    public double FlightTime { get; init; }
    public TrackState State { get; set; }
    public bool Detected { get; set; }
    public double DetectedAt { get; set; }
    public double EndedAt { get; set; }

    public double Frac(double t) => Math.Clamp((t - Launched) / FlightTime, 0, 1);
    public (double Lon, double Lat) Pos(double t)
    {
        var f = Frac(t);
        return (FromLon + (Target.Lon - FromLon) * f, FromLat + (Target.Lat - FromLat) * f);
    }
    /// <summary>0..1 height for drawing only (a parabola over the flight).</summary>
    public double Altitude(double t) { var f = Frac(t); return 4 * f * (1 - f); }
    public string Phase(double t) => Frac(t) switch { < 0.2 => "boost", < 0.8 => "midcourse", _ => "terminal" };
}

public sealed class Shot
{
    public int Id { get; init; }
    public required string BatteryId { get; init; }
    public required Track Target { get; init; }
    public required SystemProfile Interceptor { get; init; }
    public double FromLon { get; init; }
    public double FromLat { get; init; }
    public double AimLon { get; init; }
    public double AimLat { get; init; }
    public double Launched { get; init; }
    public double FlightTime { get; init; }
    public double Pk { get; init; }
    public IReadOnlyList<PkTerm> Terms { get; init; } = Array.Empty<PkTerm>();
    public ShotState State { get; set; }

    public (double Lon, double Lat) Pos(double t)
    {
        var f = Math.Clamp((t - Launched) / FlightTime, 0, 1);
        return (FromLon + (AimLon - FromLon) * f, FromLat + (AimLat - FromLat) * f);
    }
}

public sealed class BatteryState
{
    public required BatteryDef Def { get; set; }
    /// <summary>Ship-mounted batteries move with their ship.</summary>
    public void MoveTo(double lon, double lat) => Def = Def with { Lon = lon, Lat = lat };
    public required SystemProfile Radar { get; init; }
    public required SystemProfile Launcher { get; init; }
    public required SystemProfile Interceptor { get; init; }
    public int Stock { get; set; }
    public bool IsShip => Def.Platform == "ship";
    /// <summary>Base the battery is sited at (its radar/C2 damage degrades the battery).</summary>
    public string BaseId { get; set; } = "";
    /// <summary>0.2–1 from the host base's sensors component.</summary>
    public double Degrade { get; set; } = 1;
    public double RadarKm => Pacing.RadarRangeKm(Radar.R("detection")) * Degrade;
    public double ReachKm => Pacing.InterceptReachKm(Interceptor.R("reach"));
    /// <summary>Simultaneous interceptors the battery can guide (at least one).</summary>
    public int Channels => (int)Math.Max(1, Math.Max(Radar.R("fire-channels"), Launcher.R("fire-channels")));
    public double Networking => Math.Max(Radar.R("networking"), Launcher.R("networking"));
}

public sealed class LauncherState
{
    public required LauncherDef Def { get; set; }
    public void MoveTo(double lon, double lat) => Def = Def with { Lon = lon, Lat = lat };
    public required SystemProfile Missile { get; set; }
    public int Stock { get; set; }
    public bool IsShip => Def.Platform == "ship";
    public string BaseId { get; set; } = "";
    public double ReachKm => Pacing.StrikeReachKm(Missile.R("reach"));
}

public sealed record LogEntry(double T, string Kind, string Text);

/// <summary>A neutral merchant ship moving back and forth along a scenario sea lane (game traffic, not real shipping data).</summary>
public sealed class NeutralShip
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Flag { get; init; } = "";
    public required List<double[]> Lane { get; init; }
    /// <summary>Position along the lane in segments (0..Lane.Count-1).</summary>
    public double Pos { get; set; }
    public bool Forward { get; set; } = true;
    public const double KmPerSec = 0.0075; // ~15 knots
    public (double Lon, double Lat) At
    {
        get
        {
            var i = Math.Min((int)Pos, Lane.Count - 2); var f = Pos - i;
            return (Lane[i][0] + (Lane[i + 1][0] - Lane[i][0]) * f, Lane[i][1] + (Lane[i + 1][1] - Lane[i][1]) * f);
        }
    }
    public (double Lon, double Lat) Heading
    {
        get { var i = Math.Min((int)Pos, Lane.Count - 2); var (a, b) = Forward ? (Lane[i], Lane[i + 1]) : (Lane[i + 1], Lane[i]); return (b[0] - a[0], b[1] - a[1]); }
    }
    public void Move(double dt)
    {
        var i = Math.Min((int)Pos, Lane.Count - 2);
        var seg = Math.Max(1, Pacing.DistanceKm(Lane[i][0], Lane[i][1], Lane[i + 1][0], Lane[i + 1][1]));
        Pos += (Forward ? 1 : -1) * KmPerSec * dt / seg;
        if (Pos >= Lane.Count - 1) { Pos = Lane.Count - 1 - 1e-6; Forward = false; }
        if (Pos <= 0) { Pos = 0; Forward = true; }
    }
}
public sealed record OrderResult(bool Ok, string Message);

public sealed class InterceptTrial
{
    public ScenarioDef Scenario { get; }
    public double Time { get; private set; }
    public Dictionary<string, SideControl> Control { get; } = new() { ["blue"] = SideControl.AI, ["red"] = SideControl.AI };
    /// <summary>Interceptors the AI fires per engagement (doctrine). Re-engages if the salvo misses and time allows.</summary>
    public int AiSalvo { get; set; } = 2;
    public List<BatteryState> Batteries { get; } = new();
    /// <summary>At war: weapons may be used. Not at war the other side is "hostile, not at war": tracked, never attacked.</summary>
    public bool AtWar { get; set; } = true;
    public const string NotAtWar = "Not at war: hostile forces can be tracked but not attacked.";
    /// <summary>Neutral merchant traffic on the scenario's sea lanes: visible to everyone, never a target.</summary>
    public List<NeutralShip> Neutrals { get; } = new();
    /// <summary>Radar effectiveness at a point for the victim side: 0.6 under an enemy stand-off jammer, else 1.</summary>
    public double JamFactor(string victimSide, double lon, double lat) => Air?.Jammer(victimSide == "blue" ? "red" : "blue", lon, lat) != null ? 0.6 : 1;
    /// <summary>EMCON: ids of batteries, ships and AEW flights whose radar is switched off. Silent emitters don't
    /// detect with radar, but can't be found by the enemy's passive listening either (game rule, see IntelRules.EsmKm).</summary>
    public HashSet<string> Silent { get; } = new();
    public bool Emitting(string id) => !Silent.Contains(id);
    public string SetEmitting(string id, bool on, string name)
    {
        if (on) Silent.Remove(id); else Silent.Add(id);
        var msg = on ? $"{name}: radar ON (emitting; can be heard by enemy passive sensors)." : $"{name}: radar OFF (silent; no radar detection).";
        Note("emcon", msg);
        return msg;
    }
    public List<LauncherState> Launchers { get; } = new();
    public List<Track> Tracks { get; } = new();
    public List<Shot> Shots { get; } = new();
    public List<LogEntry> Log { get; } = new();
    public Dictionary<string, int> Hits { get; } = new();
    /// <summary>Bases with components (sites whose Type is a known base type).</summary>
    public Dictionary<string, BaseState> Bases { get; } = new();
    /// <summary>The operation system (both sides, AI and player). Set up after Air/Intel/Naval.</summary>
    public BadWaters.Sim.Command.OperationBoard? Ops { get; set; }
    /// <summary>Set before creating Air: the sides are run by theatre commanders (scenario ATO lines become opening operations).</summary>
    public bool UseCommanders { get; set; }
    /// <summary>One autonomous commander per side (both always exist; a side under direct player control pauses its own).</summary>
    public Dictionary<string, BadWaters.Sim.Command.Commander> Commanders { get; } = new();
    public IntelService? Intel { get; set; }
    /// <summary>Theatre logistics (supplies in nodes, transport, the AI logistics officer). Optional.</summary>
    public LogisticsService? Logistics { get; set; }
    public NavalForces? Naval { get; set; }
    private double _nextAiNaval = 300;

    private readonly Random _rng;
    private int _nextId = 1;
    private readonly HashSet<int> _scriptDone = new();
    public const double Step = 0.5;
    private double _carry;

    public InterceptTrial(ScenarioDef s, Func<string, SystemProfile> profile, int seed = 1, IReadOnlyDictionary<string, BaseTypeDef>? baseTypes = null)
    {
        Scenario = s;
        _rng = new Random(seed);
        if (baseTypes != null)
            foreach (var site in s.Sites.Where(x => baseTypes.ContainsKey(x.Type)))
            {
                var bs = new BaseState { Site = site, Type = baseTypes[site.Type] };
                foreach (var c in bs.Type.Components) bs.Components[c.Id] = new ComponentState { Def = c };
                Bases[site.Id] = bs;
            }
        foreach (var b in s.Batteries)
            Batteries.Add(new BatteryState { Def = b, Radar = profile(b.Radar), Launcher = profile(b.Launcher), Interceptor = profile(b.Interceptor), Stock = b.Stock });
        foreach (var l in s.Launchers) Launchers.Add(new LauncherState { Def = l, Missile = profile(l.Missile), Stock = l.Stock });
        // batteries and land launchers belong to the nearest friendly base within 20 km
        string Host(string side, double lon, double lat) => Bases.Values.Where(b => b.Site.Side == side)
            .Select(b => (b, d: Pacing.DistanceKm(b.Site.Lon, b.Site.Lat, lon, lat))).Where(x => x.d <= 20).OrderBy(x => x.d).Select(x => x.b.Site.Id).FirstOrDefault() ?? "";
        foreach (var b in Batteries.Where(b => !b.IsShip)) b.BaseId = Host(b.Def.Side, b.Def.Lon, b.Def.Lat);
        foreach (var l in Launchers.Where(l => !l.IsShip)) l.BaseId = Host(l.Def.Side, l.Def.Lon, l.Def.Lat);
        Note("info", s.Brief);
        var nr = new Random(seed * 17 + 5);
        var flags = new[] { "Panama", "Liberia", "Marshall Is.", "Singapore", "Malta", "Greece", "Hong Kong", "Bahamas" };
        var k = 0;
        foreach (var lane in s.Neutral.Where(l => l.Count >= 2))
            for (var i = 0; i < 3; i++)
                Neutrals.Add(new NeutralShip { Id = $"mer-{++k}", Name = $"MV {(char)('A' + nr.Next(26))}{(char)('a' + nr.Next(26))}{(char)('a' + nr.Next(26))}ia", Flag = flags[nr.Next(flags.Length)],
                    Lane = lane, Pos = nr.NextDouble() * (lane.Count - 1), Forward = nr.Next(2) == 0 });
    }

    public SiteDef? Site(string id) => id.StartsWith("corridor:") ? CorridorSite(id[9..]) : Scenario.Sites.FirstOrDefault(x => x.Id == id);

    /// <summary>A supply route as a strike aimpoint (its midpoint); logistics routes are legitimate military targets.</summary>
    public SiteDef? CorridorSite(string id)
    {
        if (Logistics?.Corridors.GetValueOrDefault(id) is not { } c) return null;
        var a = Logistics.Nodes[c.Def.A]; var b = Logistics.Nodes[c.Def.B];
        return new SiteDef("corridor:" + id, c.Def.Name, c.Def.Side, "logistics", (a.Lon + b.Lon) / 2, (a.Lat + b.Lat) / 2, "corridor");
    }
    private void Note(string kind, string text) => Log.Add(new LogEntry(Time, kind, text));
    public void Report(string kind, string text) => Note(kind, text);
    /// <summary>Air tasking layer (packages, flights). Optional; set by the host after construction.</summary>
    public AirTasking? Air { get; set; }

    // ---------------- orders (player or AI use the same calls) ----------------

    public OrderResult Launch(string launcherId, string targetId, int count = 1, string aimpoint = "")
    {
        if (!AtWar) return new(false, NotAtWar);
        var l = Launchers.FirstOrDefault(x => x.Def.Id == launcherId);
        var site = Site(targetId);
        if (l == null || site == null) return Refuse("Unknown launcher or target.");
        if (!TargetPolicy.IsLegitimate(site.Class)) return Refuse(TargetPolicy.IsKnown(site.Class) ? TargetPolicy.ProtectedMessage : $"Target class '{site.Class}' is not classified, so it is refused.");
        if (site.Side == l.Def.Side) return Refuse("That site is on your own side.");
        var dist = Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, site.Lon, site.Lat);
        if (dist > l.ReachKm) return Refuse($"{site.Name} is out of reach ({dist:0} km, reach {l.ReachKm:0} km from {l.Missile.Sources("reach")}).");
        if (l.Stock <= 0) return Refuse($"{l.Def.Name} has no missiles left.");
        if (l.BaseId != "" && Bases.TryGetValue(l.BaseId, out var hostBase) && !hostBase.PadsOk) return Refuse($"{l.Def.Name} can't fire: launch pads at {hostBase.Site.Name} are damaged.");
        if (Logistics != null && !Logistics.Fuel(l, Math.Min(count, l.Stock), out var dry)) return Refuse(dry);
        if (Bases.TryGetValue(site.Id, out var tb))
        {
            if (aimpoint == "" || !tb.Components.ContainsKey(aimpoint)) aimpoint = AimFor(l.Def.Side, tb);
        }
        else aimpoint = "";
        var n = Math.Min(count, l.Stock);
        var flight = dist / Pacing.SpeedKmPerSec(l.Missile.R("speed"));
        for (var i = 0; i < n; i++)
            Tracks.Add(new Track
            {
                Id = _nextId++, Side = l.Def.Side, Missile = l.Missile, LauncherId = l.Def.Id, Target = site,
                FromLon = l.Def.Lon, FromLat = l.Def.Lat, Launched = Time + i * 4, FlightTime = flight, Aimpoint = aimpoint,
            });
        l.Stock -= n;
        Intel?.LaunchSeen(l, Time);
        var msg = $"{l.Def.Name} launched {n} × {l.Missile.Name} at {site.Name}{(aimpoint != "" ? $" ({Bases[site.Id].Components[aimpoint].Def.Name})" : "")} ({dist:0} km, {flight:0} s flight).";
        Note("launch", msg);
        return new OrderResult(true, msg);

        OrderResult Refuse(string why) { Note("refused", why); return new OrderResult(false, why); }
    }

    public OrderResult Engage(string batteryId, int trackId, int salvo = 1)
    {
        var b = Batteries.FirstOrDefault(x => x.Def.Id == batteryId);
        var tr = Tracks.FirstOrDefault(x => x.Id == trackId);
        if (b == null || tr == null) return new(false, "Unknown battery or track.");
        if (tr.State != TrackState.Flying) return new(false, "That track is no longer flying.");
        if (!tr.Detected) return new(false, "Track not detected yet: the radar has no firing solution.");
        if (b.Stock <= 0) return new(false, $"{b.Def.Name} has no interceptors left.");
        var busy = Shots.Count(s => s.BatteryId == b.Def.Id && s.State == ShotState.Flying);
        if (busy >= b.Channels) return new(false, $"All {b.Channels} fire channels are busy.");
        var plan = PlanIntercept(b, tr);
        if (plan == null) return new(false, $"No intercept possible yet: the track must come within {b.ReachKm:0} km ({b.Interceptor.Sources("reach")}).");
        var n = Math.Min(Math.Min(salvo, b.Stock), b.Channels - busy);
        var (terms, pk) = KillChance(b, tr, Time + plan.Value.T);
        for (var i = 0; i < n; i++)
            Shots.Add(new Shot
            {
                Id = _nextId++, BatteryId = b.Def.Id, Target = tr, Interceptor = b.Interceptor,
                FromLon = b.Def.Lon, FromLat = b.Def.Lat, AimLon = plan.Value.Lon, AimLat = plan.Value.Lat,
                // held in the canister until the right moment, then flies its own flight time
                Launched = Time + plan.Value.T - plan.Value.Fly + i * 0.5, FlightTime = plan.Value.Fly, Pk = pk, Terms = terms,
            });
        b.Stock -= n;
        var msg = $"{b.Def.Name} assigned {n} × {b.Interceptor.Name} to track {tr.Id} · kill chance {pk:P0} each · launch in {plan.Value.T - plan.Value.Fly:0} s, intercept in {plan.Value.T:0} s.";
        Note("engage", msg);
        return new(true, msg);
    }

    /// <summary>The first moment the interceptor can meet the track inside its reach, before impact.</summary>
    public (double T, double Lon, double Lat, double Fly)? PlanIntercept(BatteryState b, Track tr)
    {
        var v = Pacing.SpeedKmPerSec(b.Interceptor.R("speed"));
        for (double dt = 1; Time + dt < tr.Launched + tr.FlightTime * 0.98; dt += 1)
        {
            var (lon, lat) = tr.Pos(Time + dt);
            if (tr.Frac(Time + dt) <= 0) continue;
            var d = Pacing.DistanceKm(b.Def.Lon, b.Def.Lat, lon, lat);
            if (d <= b.ReachKm && d / v <= dt) return (dt, lon, lat, d / v);
        }
        return null;
    }

    /// <summary>Kill chance for one interceptor, broken down by the ratings (and components) that set it.</summary>
    public (IReadOnlyList<PkTerm> Terms, double Pk) KillChance(BatteryState b, Track tr, double atTime)
    {
        var i = b.Interceptor;
        var m = tr.Missile;
        var terms = new List<PkTerm> { new("Base engagement", 0.30, "doctrine") };
        void Add(string label, double d, string src) { if (Math.Abs(d) > 1e-9) terms.Add(new(label, d, src)); }
        Add($"Interceptor accuracy {i.R("accuracy"):0}", 0.05 * i.R("accuracy"), i.Sources("accuracy"));
        Add($"Interceptor punch {i.R("punch"):0}", 0.02 * i.R("punch"), i.Sources("punch"));
        Add($"Battery networking {b.Networking:0}", 0.02 * b.Networking, $"{b.Radar.Sources("networking")}");
        Add($"Target agility {m.R("agility"):0}", -0.04 * Math.Max(0, m.R("agility") - i.R("agility") / 2), m.Sources("agility") + (i.R("agility") > 0 ? $" vs {i.Sources("agility")}" : ""));
        var ew = Math.Max(0, m.R("ew") - i.R("jam-resistance"));
        Add($"Target decoys {m.R("ew"):0} vs jam resistance {i.R("jam-resistance"):0}", -0.05 * ew, $"{m.Sources("ew")} vs {i.Sources("jam-resistance")}");
        Add($"Speed gap {m.R("speed"):0} vs {i.R("speed"):0}", -0.08 * Math.Max(0, m.R("speed") - i.R("speed")), $"{m.Sources("speed")} vs {i.Sources("speed")}");
        if (tr.Phase(atTime) == "terminal") Add("Terminal-phase shot", -0.08, "late intercept");
        var pk = Math.Clamp(terms.Sum(t => t.Delta), 0.05, 0.95);
        return (terms, pk);
    }

    // ---------------- time ----------------

    /// <summary>Coarse steps when nothing fast is in the air (headless auto-resolve and high time compression).</summary>
    public bool Adaptive { get; set; }
    public const double CoarseStep = 10;
    /// <summary>Coarse step used by Adaptive (auto-resolve raises it).</summary>
    public double CoarseSeconds { get; set; } = CoarseStep;
    /// <summary>Restore the clock from a saved theatre.</summary>
    public void SetTime(double t) { Time = t; _carry = 0; }
    private double _dt = Step;
    /// <summary>No missiles in flight: nothing needs half-second resolution.</summary>
    private bool Quiet => !Tracks.Any(t => t.State == TrackState.Flying);

    public void Advance(double seconds)
    {
        _carry += seconds;
        while (_carry >= Step)
        {
            var dt = Adaptive && Quiet && _carry >= CoarseSeconds ? CoarseSeconds : Step;
            _carry -= dt; _dt = dt; Tick();
        }
    }

    /// <summary>Developer profiling: milliseconds spent per subsystem (null = off).</summary>
    public Dictionary<string, double>? Profile { get; set; }
    private readonly System.Diagnostics.Stopwatch _sw = new();
    private void Lap(string k) { if (Profile == null) return; Profile[k] = Profile.GetValueOrDefault(k) + _sw.Elapsed.TotalMilliseconds; _sw.Restart(); }

    private void Tick()
    {
        if (Profile != null) _sw.Restart();
        Time += _dt;
        foreach (var b in Bases.Values) b.Repair(_dt);
        foreach (var b in Batteries.Where(b => b.BaseId != "")) b.Degrade = Bases[b.BaseId].SensorFactor * (Logistics?.Readiness(b.Def.Id) ?? 1);
        if (Logistics != null) { foreach (var b in Bases.Values) b.Supply = Logistics.Readiness(b.Site.Id); Logistics.Tick(Time); }
        Lap("bases");
        Naval?.Tick(_dt, Time);
        foreach (var n in Neutrals) n.Move(_dt);
        Lap("naval");
        Intel?.Tick(Time);
        Lap("intel");
        if (Naval != null && Time >= _nextAiNaval)
        {
            _nextAiNaval = Time + 600;
            foreach (var side in new[] { "blue", "red" }.Where(x => Control.GetValueOrDefault(x) == SideControl.AI)) AiAntiShip(side);
        }
        RunScript();
        Lap("ai-naval+script");

        foreach (var tr in Tracks.Where(t => t.State == TrackState.Flying && t.Launched <= Time))
        {
            if (!tr.Detected)
                foreach (var b in Batteries.Where(b => b.Def.Side != tr.Side && Emitting(b.Def.Id)))
                {
                    var (lon, lat) = tr.Pos(Time);
                    var jam = JamFactor(b.Def.Side, b.Def.Lon, b.Def.Lat);
                    if (Pacing.DistanceKm(b.Def.Lon, b.Def.Lat, lon, lat) > b.RadarKm * jam) continue;
                    var p = jam * Math.Clamp(0.15 + 0.06 * (b.Radar.R("detection") - m(tr).R("signature")) + (tr.Phase(Time) == "boost" ? 0.1 : 0), 0.02, 0.9) * _dt;
                    if (_rng.NextDouble() < p)
                    {
                        tr.Detected = true; tr.DetectedAt = Time;
                        Note("detect", $"{b.Def.Name} detected track {tr.Id} ({tr.Phase(Time)}) with {b.Radar.Sources("detection")}.");
                        break;
                    }
                }
        }

        foreach (var s in Shots.Where(s => s.State == ShotState.Flying && Time >= s.Launched + s.FlightTime))
        {
            if (s.Target.State != TrackState.Flying) { s.State = ShotState.Miss; continue; }
            if (_rng.NextDouble() < s.Pk)
            {
                s.State = ShotState.Hit;
                s.Target.State = TrackState.Intercepted; s.Target.EndedAt = Time;
                Note("hit", $"Track {s.Target.Id} intercepted by {s.Interceptor.Name} (chance was {s.Pk:P0}).");
            }
            else { s.State = ShotState.Miss; Note("miss", $"Interceptor missed track {s.Target.Id} (chance was {s.Pk:P0})."); }
        }

        foreach (var tr in Tracks.Where(t => t.State == TrackState.Flying && Time >= t.Launched + t.FlightTime))
        {
            tr.State = TrackState.Impacted; tr.EndedAt = Time;
            if (tr.TargetShip != "") { Note("impact", ShipImpact(tr)); continue; }
            Hits[tr.Target.Id] = Hits.GetValueOrDefault(tr.Target.Id) + 1;
            if (tr.Target.Id.StartsWith("corridor:")) { Logistics?.HitCorridor(tr.Target.Id[9..], tr.Missile.R("punch"), $"track {tr.Id}"); continue; }
            var effect = Bases.TryGetValue(tr.Target.Id, out var hb) ? " · " + ApplyHit(tr.Side, hb, tr.Aimpoint, tr.Missile.R("punch"), tr.Missile.R("accuracy"), 0.2, "impact report") : "";
            Note("impact", $"Track {tr.Id} hit {tr.Target.Name} (punch {tr.Missile.R("punch"):0} from {tr.Missile.Sources("punch")}){effect}.");
        }

        Lap("tracks+shots");
        Air?.Tick(Time - _dt, Time);
        Lap("air");
        Ops?.Tick(Time);
        Lap("ops");
        foreach (var c in Commanders.Values) if (Control.GetValueOrDefault(c.Side) == SideControl.AI) c.Tick(Time);
        Lap("commanders");
        if (Control["blue"] == SideControl.AI) AiDefend("blue");
        if (Control["red"] == SideControl.AI) AiDefend("red");
        Lap("ai-defend");
    }

    private static SystemProfile m(Track t) => t.Missile;

    /// <summary>
    /// Fire anti-ship missiles at a ship contact. The missiles fly to where your picture last saw it;
    /// they need a fresh contact, and only find the ship if it's still inside the seeker's search radius.
    /// </summary>
    public OrderResult LaunchAtShip(string launcherId, string shipId, int count = 2)
    {
        var l = Launchers.FirstOrDefault(x => x.Def.Id == launcherId);
        var ship = Naval?.Find(shipId);
        if (l == null || ship == null) return new(false, "Unknown launcher or ship.");
        if (ship.Side == l.Def.Side) return new(false, "That ship is on your own side.");
        if (!AtWar) return new(false, NotAtWar);
        if (!NavalRules.CanHitShips(l.Missile)) return new(false, $"{l.Missile.Name} has no seeker that can find a ship (needs accuracy 6+ or an anti-ship rating).");
        var c = Intel?.Pictures[l.Def.Side].Contacts.GetValueOrDefault(shipId);
        if (c == null || Time - c.LastSeen > IntelRules.StaleAfter) return new(false, "No fresh contact on that ship: find it first (AEW, patrol aircraft, ships or a satellite pass).");
        if (l.Stock <= 0) return new(false, $"{l.Def.Name} has no missiles left.");
        if (l.BaseId != "" && Bases.TryGetValue(l.BaseId, out var hb) && !hb.PadsOk) return new(false, $"{l.Def.Name} can't fire: launch pads damaged.");
        var dist = Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, c.Lon, c.Lat);
        if (dist > l.ReachKm) return new(false, $"Contact is {dist:0} km away; reach {l.ReachKm:0} km.");
        var n = Math.Min(count, l.Stock);
        var flight = dist / Pacing.SpeedKmPerSec(l.Missile.R("speed"));
        var aim = new SiteDef(shipId, $"{c.Label} (contact)", ship.Side, "military-force", c.Lon, c.Lat);
        for (var i = 0; i < n; i++)
            Tracks.Add(new Track { Id = _nextId++, Side = l.Def.Side, Missile = l.Missile, LauncherId = l.Def.Id, Target = aim, TargetShip = shipId,
                FromLon = l.Def.Lon, FromLat = l.Def.Lat, Launched = Time + i * 4, FlightTime = flight });
        l.Stock -= n;
        Intel?.LaunchSeen(l, Time);
        var msg = $"{l.Def.Name} fired {n} × {l.Missile.Name} at {c.Label} ({dist:0} km, {flight / 60:0.0} min flight, contact {(Time - c.LastSeen) / 60:0} min old).";
        Note("launch", msg);
        return new(true, msg);
    }

    private string ShipImpact(Track tr)
    {
        var ship = Naval?.Find(tr.TargetShip);
        if (ship == null || ship.Sunk) return $"Track {tr.Id} found nothing: the target is gone.";
        var off = Pacing.DistanceKm(ship.Lon, ship.Lat, tr.Target.Lon, tr.Target.Lat);
        var search = NavalRules.SeekerKm(tr.Missile.R("accuracy"));
        if (off > search) return $"Track {tr.Id} ({tr.Missile.Name}) found nothing: {ship.Name} had moved {off:0} km from the aimpoint (seeker searches {search:0} km).";
        var hit = _rng.NextDouble() < DamageRules.OnAimpoint(tr.Missile.R("accuracy"));
        if (!hit) return $"Track {tr.Id} ({tr.Missile.Name}) missed {ship.Name}.";
        return $"Track {tr.Id}: " + Naval!.Hit(ship, NavalRules.MissileDamage(tr.Missile.R("punch")), tr.Missile.Name);
    }

    /// <summary>AI: anti-ship-capable launchers with spare rounds fire at the freshest enemy ship contact in reach.</summary>
    private void AiAntiShip(string side)
    {
        var pic = Intel?.Pictures[side];
        if (pic == null || Naval == null) return;
        foreach (var l in Launchers.Where(l => l.Def.Side == side && l.Stock > 0 && NavalRules.CanHitShips(l.Missile)))
        {
            var tasked = Air?.Fires.Where(f => f.Launcher == l && f.State == FireState.Planned).Sum(f => f.Count) ?? 0;
            if (l.Stock - tasked < 2) continue;
            var target = pic.Contacts.Values.Where(c => c.Kind == "ship" && Time - c.LastSeen <= 120 && !Naval.IsSunk(c.Key) && Naval.Find(c.Key)?.Side != side
                    && Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, c.Lon, c.Lat) <= l.ReachKm)
                .OrderBy(c => Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, c.Lon, c.Lat)).FirstOrDefault();
            if (target != null) LaunchAtShip(l.Def.Id, target.Key, 2);
        }
    }

    /// <summary>Aimpoint an attacker picks at a base: doctrine order, skipping what its own picture believes is already down.</summary>
    public string AimFor(string attacker, BaseState b)
    {
        var pic = Intel?.Pictures.GetValueOrDefault(attacker);
        return b.DefaultAimpoint(pic == null ? null : c => pic.Belief(b.Site.Id, c).Integrity);
    }

    /// <summary>
    /// One weapon hit on a base component: accuracy decides a direct hit on the aimpoint or a near miss
    /// on another component; punch against hardness sets the damage. Parked aircraft can be destroyed.
    /// The attacker gets a damage report whose quality depends on the source.
    /// </summary>
    /// <summary>Raised when a strike damages a site: (site, fraction of the whole site destroyed). The strategic economy
    /// maps it onto its own facility there, so the loss shows up in production next month.</summary>
    public event Action<SiteDef, double>? SiteDamaged;

    public string ApplyHit(string attacker, BaseState b, string aim, double punch, double accuracy, double reportConfidence, string reportSource)
    {
        if (aim == "" || !b.Components.ContainsKey(aim)) aim = AimFor(attacker, b);
        var direct = _rng.NextDouble() < DamageRules.OnAimpoint(accuracy);
        var others = b.Components.Keys.Where(k => k != aim).ToList();
        var hitId = direct || others.Count == 0 ? aim : others[_rng.Next(others.Count)];
        var comp = b.Components[hitId];
        var dmg = DamageRules.Damage(punch, comp.Def.Hardness, direct);
        comp.Integrity = Math.Max(0, comp.Integrity - dmg);
        Logistics?.OnComponentHit(b, comp, dmg);
        SiteDamaged?.Invoke(b.Site, dmg / 100.0 / Math.Max(1, b.Components.Count));
        var extra = Air?.GroundLosses(b, comp.Def.Effect) ?? "";
        Intel?.Report(attacker, b, hitId, reportConfidence, reportSource, Time);
        return $"{(direct ? "direct hit" : "near miss")} on {comp.Def.Name} −{dmg:0}% → {comp.Integrity:0}%{extra}";
    }

    /// <summary>Where a unit is now: a steady orbit around its station (drawing and presence only).</summary>
    public (double Lon, double Lat) UnitPos(UnitDef u)
    {
        if (u.Kind is "air" or "aew" && Air != null)
        {
            var f = Air.Flights.FirstOrDefault(x => x.Aircraft.Contains(u.Id) && x.Airborne(Time));
            if (f != null) return f.Pos(Time);
            var home = Site(u.Base);
            if (home != null) return (home.Lon, home.Lat); // on the ground at its base
        }
        if (u.Kind is "ship" or "sub" && Naval?.Pos(u.Id) is { } np) return np;
        if (u.PatrolKm <= 0) return (u.Lon, u.Lat);
        var period = u.Kind is "air" or "aew" ? 900.0 : 14400.0;
        var phase = u.Id.Aggregate(17, (h, ch) => h * 31 + ch) % 360 * Math.PI / 180;
        var a = Time / period * Math.PI * 2 + phase;
        var r = u.PatrolKm / 111.0;
        return (u.Lon + r * Math.Cos(a) / Math.Cos(u.Lat * Math.PI / 180), u.Lat + r * Math.Sin(a));
    }

    /// <summary>The best battery of a side to engage a track now (has stock, a free channel and a solution).</summary>
    public BatteryState? BestBattery(string side, Track tr) =>
        Batteries.Where(b => b.Def.Side == side && b.Stock > 0 && Shots.Count(s => s.BatteryId == b.Def.Id && s.State == ShotState.Flying) < b.Channels)
            .Select(b => (b, plan: PlanIntercept(b, tr))).Where(x => x.plan != null).OrderBy(x => x.plan!.Value.T).Select(x => x.b).FirstOrDefault();

    private void RunScript()
    {
        for (var i = 0; i < Scenario.Script.Count; i++)
        {
            var s = Scenario.Script[i];
            if (s.T > Time || _scriptDone.Contains(i)) continue;
            _scriptDone.Add(i);
            var side = Launchers.FirstOrDefault(l => l.Def.Id == s.Launcher)?.Def.Side;
            if (side != null && Control.GetValueOrDefault(side) == SideControl.AI) Launch(s.Launcher, s.Target, s.Count); // player sides give their own orders
        }
    }

    /// <summary>Passive AI doctrine: engage every detected threat with a salvo; re-engage after a miss.</summary>
    private readonly Dictionary<string, double> _nextDefend = new();

    private void AiDefend(string side)
    {
        if (Time < _nextDefend.GetValueOrDefault(side)) return;
        _nextDefend[side] = Time + 1; // once a second is plenty for engagement decisions
        var threats = Tracks.Where(t => t.State == TrackState.Flying && t.Detected && t.Side != side).ToList();
        if (threats.Count == 0) return;
        var engaged = Shots.Where(s => s.State == ShotState.Flying).Select(s => s.Target).ToHashSet();
        foreach (var b in Batteries.Where(b => b.Def.Side == side && b.Stock > 0))
            foreach (var tr in threats)
            {
                if (engaged.Contains(tr)) continue;
                // cheap geometry first: a battery that can't reach the missile now or its target can't intercept it
                var (lon, lat) = tr.Pos(Time);
                var reach = b.ReachKm * 1.3;
                if (Pacing.DistanceKm(b.Def.Lon, b.Def.Lat, lon, lat) > reach && Pacing.DistanceKm(b.Def.Lon, b.Def.Lat, tr.Target.Lon, tr.Target.Lat) > reach) continue;
                if (PlanIntercept(b, tr) == null) continue;
                Engage(b.Def.Id, tr.Id, AiSalvo);
                engaged.Add(tr);
            }
    }

    public bool Finished => _scriptDone.Count >= Scenario.Script.Count && Tracks.All(t => t.State != TrackState.Flying) && Shots.All(s => s.State != ShotState.Flying);
}
