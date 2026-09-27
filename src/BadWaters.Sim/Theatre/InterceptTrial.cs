using BadWaters.Sim.Policy;

namespace BadWaters.Sim.Theatre;

// A small real-time theatre engagement: one side launches tactical ballistic missiles, the other detects
// and intercepts them. Everything is rules-based: positions move along a straight line over a fixed flight
// time (the "arc" is only drawn), detection and kills are dice rolls against 0–10 ratings. No physics.

public sealed record SiteDef(string Id, string Name, string Side, string Class, double Lon, double Lat);
public sealed record BatteryDef(string Id, string Name, string Side, double Lon, double Lat, string Radar, string Launcher, string Interceptor, int Stock);
public sealed record LauncherDef(string Id, string Name, string Side, double Lon, double Lat, string Missile, int Stock);
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
    public List<ScriptedLaunch> Script { get; init; } = new();
    public List<UnitDef> Units { get; init; } = new();
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
    public required BatteryDef Def { get; init; }
    public required SystemProfile Radar { get; init; }
    public required SystemProfile Launcher { get; init; }
    public required SystemProfile Interceptor { get; init; }
    public int Stock { get; set; }
    public double RadarKm => Pacing.RadarRangeKm(Radar.R("detection"));
    public double ReachKm => Pacing.InterceptReachKm(Interceptor.R("reach"));
    /// <summary>Simultaneous interceptors the battery can guide (at least one).</summary>
    public int Channels => (int)Math.Max(1, Math.Max(Radar.R("fire-channels"), Launcher.R("fire-channels")));
    public double Networking => Math.Max(Radar.R("networking"), Launcher.R("networking"));
}

public sealed class LauncherState
{
    public required LauncherDef Def { get; init; }
    public required SystemProfile Missile { get; set; }
    public int Stock { get; set; }
    public double ReachKm => Pacing.StrikeReachKm(Missile.R("reach"));
}

public sealed record LogEntry(double T, string Kind, string Text);
public sealed record OrderResult(bool Ok, string Message);

public sealed class InterceptTrial
{
    public ScenarioDef Scenario { get; }
    public double Time { get; private set; }
    public Dictionary<string, SideControl> Control { get; } = new() { ["blue"] = SideControl.AI, ["red"] = SideControl.AI };
    /// <summary>Interceptors the AI fires per engagement (doctrine). Re-engages if the salvo misses and time allows.</summary>
    public int AiSalvo { get; set; } = 2;
    public List<BatteryState> Batteries { get; } = new();
    public List<LauncherState> Launchers { get; } = new();
    public List<Track> Tracks { get; } = new();
    public List<Shot> Shots { get; } = new();
    public List<LogEntry> Log { get; } = new();
    public Dictionary<string, int> Hits { get; } = new();

    private readonly Random _rng;
    private int _nextId = 1;
    private readonly HashSet<int> _scriptDone = new();
    public const double Step = 0.5;
    private double _carry;

    public InterceptTrial(ScenarioDef s, Func<string, SystemProfile> profile, int seed = 1)
    {
        Scenario = s;
        _rng = new Random(seed);
        foreach (var b in s.Batteries)
            Batteries.Add(new BatteryState { Def = b, Radar = profile(b.Radar), Launcher = profile(b.Launcher), Interceptor = profile(b.Interceptor), Stock = b.Stock });
        foreach (var l in s.Launchers) Launchers.Add(new LauncherState { Def = l, Missile = profile(l.Missile), Stock = l.Stock });
        Note("info", s.Brief);
    }

    public SiteDef? Site(string id) => Scenario.Sites.FirstOrDefault(x => x.Id == id);
    private void Note(string kind, string text) => Log.Add(new LogEntry(Time, kind, text));

    // ---------------- orders (player or AI use the same calls) ----------------

    public OrderResult Launch(string launcherId, string targetId, int count = 1)
    {
        var l = Launchers.FirstOrDefault(x => x.Def.Id == launcherId);
        var site = Site(targetId);
        if (l == null || site == null) return Refuse("Unknown launcher or target.");
        if (!TargetPolicy.IsLegitimate(site.Class)) return Refuse(TargetPolicy.IsKnown(site.Class) ? TargetPolicy.ProtectedMessage : $"Target class '{site.Class}' is not classified, so it is refused.");
        if (site.Side == l.Def.Side) return Refuse("That site is on your own side.");
        var dist = Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, site.Lon, site.Lat);
        if (dist > l.ReachKm) return Refuse($"{site.Name} is out of reach ({dist:0} km, reach {l.ReachKm:0} km from {l.Missile.Sources("reach")}).");
        if (l.Stock <= 0) return Refuse($"{l.Def.Name} has no missiles left.");
        var n = Math.Min(count, l.Stock);
        var flight = dist / Pacing.SpeedKmPerSec(l.Missile.R("speed"));
        for (var i = 0; i < n; i++)
            Tracks.Add(new Track
            {
                Id = _nextId++, Side = l.Def.Side, Missile = l.Missile, LauncherId = l.Def.Id, Target = site,
                FromLon = l.Def.Lon, FromLat = l.Def.Lat, Launched = Time + i * 4, FlightTime = flight,
            });
        l.Stock -= n;
        var msg = $"{l.Def.Name} launched {n} × {l.Missile.Name} at {site.Name} ({dist:0} km, {flight:0} s flight).";
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
        Add($"Speed gap {m.R("speed"):0} vs {i.R("speed"):0}", -0.03 * Math.Max(0, m.R("speed") - i.R("speed")), $"{m.Sources("speed")} vs {i.Sources("speed")}");
        if (tr.Phase(atTime) == "terminal") Add("Terminal-phase shot", -0.05, "late intercept");
        var pk = Math.Clamp(terms.Sum(t => t.Delta), 0.05, 0.95);
        return (terms, pk);
    }

    // ---------------- time ----------------

    public void Advance(double seconds)
    {
        _carry += seconds;
        while (_carry >= Step) { _carry -= Step; Tick(); }
    }

    private void Tick()
    {
        Time += Step;
        RunScript();

        foreach (var tr in Tracks.Where(t => t.State == TrackState.Flying && t.Launched <= Time))
        {
            if (!tr.Detected)
                foreach (var b in Batteries.Where(b => b.Def.Side != tr.Side))
                {
                    var (lon, lat) = tr.Pos(Time);
                    if (Pacing.DistanceKm(b.Def.Lon, b.Def.Lat, lon, lat) > b.RadarKm) continue;
                    var p = Math.Clamp(0.15 + 0.06 * (b.Radar.R("detection") - m(tr).R("signature")) + (tr.Phase(Time) == "boost" ? 0.1 : 0), 0.02, 0.9) * Step;
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
            Hits[tr.Target.Id] = Hits.GetValueOrDefault(tr.Target.Id) + 1;
            Note("impact", $"Track {tr.Id} hit {tr.Target.Name} (punch {tr.Missile.R("punch"):0} from {tr.Missile.Sources("punch")}).");
        }

        if (Control["blue"] == SideControl.AI) AiDefend("blue");
        if (Control["red"] == SideControl.AI) AiDefend("red");
    }

    private static SystemProfile m(Track t) => t.Missile;

    /// <summary>Where a unit is now: a steady orbit around its station (drawing and presence only).</summary>
    public (double Lon, double Lat) UnitPos(UnitDef u)
    {
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
    private void AiDefend(string side)
    {
        foreach (var b in Batteries.Where(b => b.Def.Side == side && b.Stock > 0))
            foreach (var tr in Tracks.Where(t => t.State == TrackState.Flying && t.Detected && t.Side != side))
            {
                if (Shots.Any(s => s.Target == tr && s.State == ShotState.Flying)) continue;
                if (PlanIntercept(b, tr) == null) continue;
                Engage(b.Def.Id, tr.Id, AiSalvo);
            }
    }

    public bool Finished => _scriptDone.Count >= Scenario.Script.Count && Tracks.All(t => t.State != TrackState.Flying) && Shots.All(s => s.State != ShotState.Flying);
}
