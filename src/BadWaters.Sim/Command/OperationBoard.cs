using BadWaters.Sim.Theatre;

namespace BadWaters.Sim.Command;

/// <summary>
/// Runs every operation in a theatre, whoever wrote it: starts it when its time and dependencies allow, executes its
/// elements through the theatre systems (air tasking, missile fires, naval moves), evaluates its IF/THEN rules against
/// the owning side's picture, and closes it (releasing its assets to the AI pool) when it completes or aborts.
/// Assets return to the pool as they land, and when destroyed.
/// </summary>
public sealed class OperationBoard
{
    private readonly InterceptTrial _t;
    public Authority Authority { get; } = new();
    public List<Operation> Ops { get; } = new();
    private readonly Dictionary<string, (double Lon, double Lat)> _targetAtStart = new();
    private int _next = 1;
    /// <summary>Raised when a rule asks for a follow-up (e.g. BDA insufficient); the side's commander decides.</summary>
    public event Action<Operation>? FollowUpRequested;

    public OperationBoard(InterceptTrial t)
    {
        _t = t;
        if (t.Air != null) t.Air.Authority = Authority;
    }

    public string NewId(string side) => $"{side}-op-{_next++}";
    public Operation? Find(string id) => Ops.FirstOrDefault(o => o.Id == id);
    public IEnumerable<Operation> Open(string side) => Ops.Where(o => o.Side == side && o.Open);

    /// <summary>Add an operation. Assets the author picked are committed at once (all or nothing).</summary>
    public IReadOnlyList<string> Add(Operation op)
    {
        var picked = op.Elements.SelectMany(e => e.Assets).Concat(op.Elements.Where(e => e.Kind == ElementKind.Naval && e.Ship != "").Select(e => e.Ship)).ToList();
        var conflicts = Authority.Commit(op.Id, picked);
        if (conflicts.Count > 0) return conflicts;
        Ops.Add(op);
        op.Log.Add($"T+{Clock(_t.Time)} created by {(op.Author == Author.Player ? "the player" : "the AI commander")}{(op.Rationale != "" ? ": " + op.Rationale : "")}");
        return Array.Empty<string>();
    }

    /// <summary>The player (or commander) releases an operation's assets back to the AI pool and cancels what hasn't flown.</summary>
    public void Release(Operation op, string why = "released")
    {
        if (!op.Open) return;
        foreach (var f in Flights(op).Where(f => f.State is not (FlightState.Landed or FlightState.Lost))) _t.Air?.Abort(f);
        foreach (var m in FireTasks(op).Where(m => m.State == FireState.Planned)) _t.Air?.Cancel(m);
        foreach (var e in op.Elements.Where(e => e.Kind == ElementKind.Naval && e.Ship != "")) _t.Naval?.Hold(e.Ship);
        op.Status = why == "complete" ? OpStatus.Complete : OpStatus.Aborted;
        Authority.ReleaseAll(op.Id);
        op.Log.Add($"T+{Clock(_t.Time)} {why}; assets returned to the pool");
    }

    public IEnumerable<Flight> Flights(Operation op) => _t.Air?.Flights.Where(f => op.Elements.Any(e => e.Flights.Contains(f.Id))) ?? Enumerable.Empty<Flight>();
    public IEnumerable<MissileTask> FireTasks(Operation op) => _t.Air?.Fires.Where(m => op.Elements.Any(e => e.Fires.Contains(m.Id))) ?? Enumerable.Empty<MissileTask>();

    /// <summary>Rules, losses and completion are checked this often (theatre seconds); starts are exact.</summary>
    public const double CheckEvery = 5;
    private double _nextCheck;

    public void Tick(double now)
    {
        var check = now >= _nextCheck;
        if (check) _nextCheck = now + CheckEvery;
        foreach (var op in Ops.Where(o => o.Open && (check || (o.Status == OpStatus.Planned && now >= o.Start))).ToList())
        {
            if (op.Status == OpStatus.Planned)
            {
                if (op.After.Any(id => Find(id) is { Status: OpStatus.Aborted })) { Release(op, "cancelled: a prerequisite operation failed"); continue; }
                if (now < op.Start || op.After.Any(id => Find(id) is { Status: not OpStatus.Complete })) continue;
                op.Status = OpStatus.Active;
                op.Log.Add($"T+{Clock(now)} executing");
                foreach (var e in op.Elements.Where(e => !e.Held)) Launch(op, e);
            }
            if (!check) continue;
            Track(op);
            Rules(op, now);
            if (op.Status == OpStatus.Active && Done(op)) Release(op, "complete");
        }
    }

    /// <summary>Execute one element through the theatre systems and commit whatever it uses.</summary>
    public void Launch(Operation op, OpElement e)
    {
        if (e.State != "waiting") return;
        switch (e.Kind)
        {
            case ElementKind.Air or ElementKind.Sensor when e.Spec != null && _t.Air != null:
            {
                var before = _t.Air.Flights.Count;
                var r = _t.Air.PlanFor(e.Spec with { Side = op.Side }, op.Id, e.Assets, commit: true);
                if (!r.Ok) { e.State = "failed"; op.Log.Add($"{e.Id}: not flown ({string.Join(" ", r.Issues)})"); return; }
                foreach (var f in _t.Air.Flights.Skip(before)) { e.Flights.Add(f.Id); Authority.Commit(op.Id, f.Aircraft); op.AircraftCommitted += f.Aircraft.Count; f.HoldFire = op.WeaponsHeld; if ((e.Emcon == "" ? op.Doctrine.Emcon : e.Emcon) == "silent") _t.Silent.Add(f.Id); }
                e.State = "flying";
                break;
            }
            case ElementKind.Fires when e.Spec != null && _t.Air != null:
            {
                var before = _t.Air.Fires.Count;
                var r = _t.Air.PlanFor(e.Spec with { Side = op.Side }, op.Id, e.Assets, commit: true);
                if (!r.Ok) { e.State = "failed"; op.Log.Add($"{e.Id}: not fired ({string.Join(" ", r.Issues)})"); return; }
                foreach (var m in _t.Air.Fires.Skip(before)) { e.Fires.Add(m.Id); Authority.Commit(op.Id, new[] { m.Launcher.Def.Id }); }
                e.State = "firing";
                break;
            }
            case ElementKind.Relocate when e.Base != "" && _t.Air != null:
                Authority.Commit(op.Id, e.Assets);
                op.Log.Add($"{e.Id}: {_t.Air.Relocate(e.Assets, e.Base)}");
                e.State = "ferrying";
                break;
            case ElementKind.Naval when e.Ship != "" && _t.Naval != null:
                op.Log.Add($"{e.Id}: {_t.Naval.MoveTo(e.Ship, e.Lon, e.Lat)}");
                e.State = "sailing";
                break;
            default:
                e.State = "failed";
                break;
        }
        if (e.State != "failed") op.Log.Add($"T+{Clock(_t.Time)} {e.Id} ({e.Role}) {e.State}");
    }

    /// <summary>Losses, landings (assets back to the pool), fires launched.</summary>
    private void Track(Operation op)
    {
        foreach (var f in Flights(op))
        {
            if (f.LostAircraft.Count > 0) Authority.Release(f.LostAircraft);
            if (f.State is FlightState.Landed or FlightState.Lost) Authority.Release(f.Aircraft);
        }
        op.AircraftLost = Flights(op).Sum(f => f.LostAircraft.Count);
        foreach (var e in op.Elements.Where(e => e.Kind == ElementKind.Fires && e.State == "firing"))
            if (FireTasks(op).Where(m => e.Fires.Contains(m.Id)).All(m => m.State != FireState.Planned)) { e.State = "done"; Authority.Release(FireTasks(op).Where(m => e.Fires.Contains(m.Id)).Select(m => m.Launcher.Def.Id)); }
        foreach (var e in op.Elements.Where(e => e.Kind == ElementKind.Relocate && e.State == "ferrying"))
            if (_t.Air != null && e.Assets.All(a => !_t.Air.Ferrying.ContainsKey(a))) { e.State = "done"; Authority.Release(e.Assets); }
        foreach (var e in op.Elements.Where(e => e.Kind == ElementKind.Naval && e.State == "sailing"))
            if (_t.Naval?.Find(e.Ship) is { } s && (!s.Moving || s.Sunk)) { e.State = s.Sunk ? "lost" : "on station"; if (s.Sunk) Authority.Release(new[] { e.Ship }); }
    }

    private bool Done(Operation op) =>
        op.Elements.Any(e => !e.Held) && op.Elements.Where(e => !e.Held).All(e => e.State is "failed" or "done" or "on station" or "lost"
            || (e.State == "flying" && Flights(op).Where(f => e.Flights.Contains(f.Id)).All(f => f.State is FlightState.Landed or FlightState.Lost)));

    // ---------------- IF/THEN rules (the owner's picture only) ----------------

    private void Rules(Operation op, double now)
    {
        if (op.Doctrine.Adaptation == "none") return;
        for (var i = 0; i < op.Rules.Count; i++)
        {
            var rule = op.Rules[i];
            if (rule.Once && op.FiredRules.Contains(i)) continue;
            if (!Holds(op, rule.If, now)) continue;
            op.FiredRules.Add(i);
            op.Log.Add($"T+{Clock(now)} {rule}");
            Do(op, rule.Then, now);
            if (!op.Open) return;
        }
    }

    private (double Lon, double Lat)? TargetPos(Operation op)
    {
        var id = op.Targets.FirstOrDefault();
        if (id == null) return op.Elements.Where(e => e.Spec is { StationLon: not 0 }).Select(e => ((double, double)?)(e.Spec!.StationLon, e.Spec.StationLat)).FirstOrDefault();
        if (_t.Site(id) is { } s) return (s.Lon, s.Lat);
        var pic = _t.Intel?.Pictures.GetValueOrDefault(op.Side);
        return pic != null && pic.Contacts.TryGetValue(id, out var c) ? (c.Lon, c.Lat) : null;
    }

    public bool Holds(Operation op, OpCondition c, double now)
    {
        var pic = _t.Intel?.Pictures.GetValueOrDefault(op.Side);
        switch (c.Kind)
        {
            case "enemy-aew-detected":
                return pic != null && pic.Contacts.Values.Any(x => x.Kind == "air" && x.Role == "AEW" && now - x.LastSeen <= IntelRules.StaleAfter);
            case "enemy-air-near":
            {
                var at = TargetPos(op);
                if (pic == null || at == null) return false;
                var n = pic.Contacts.Values.Count(x => x.Kind == "air" && x.Role is not ("AEW" or "Tanker") && now - x.LastSeen <= IntelRules.StaleAfter && Pacing.DistanceKm(x.Lon, x.Lat, at.Value.Lon, at.Value.Lat) <= c.Value);
                return n >= (int.TryParse(c.Arg, out var k) ? k : 1);
            }
            case "target-confidence-below":
            {
                var id = op.Targets.FirstOrDefault();
                if (id == null || _t.Site(id) != null) return false; // fixed sites are known
                if (pic == null || !pic.Contacts.TryGetValue(id, out var ct)) return true;
                var conf = (ct.IdLevel + 1) / 3.0 * Math.Clamp(1 - (now - ct.LastSeen) / IntelRules.DropAfter, 0, 1);
                return conf < c.Value;
            }
            case "tanker-unavailable":
                return op.Elements.Any(e => e.Spec?.Flights.Any(r => r.Mission == Mission.Tanker) == true && (e.State == "failed" || Flights(op).Any(f => e.Flights.Contains(f.Id) && f.Mission == Mission.Tanker && f.Aircraft.Count == 0)));
            case "losses-above":
                return op.AircraftCommitted > 0 && (double)op.AircraftLost / op.AircraftCommitted > c.Value;
            case "target-moved":
            {
                var id = op.Targets.FirstOrDefault();
                if (id == null || pic == null || !pic.Contacts.TryGetValue(id, out var ct)) return false;
                if (!_targetAtStart.TryGetValue(op.Id, out var p0)) { _targetAtStart[op.Id] = (ct.Lon, ct.Lat); return false; }
                return Pacing.DistanceKm(p0.Lon, p0.Lat, ct.Lon, ct.Lat) > 20;
            }
            case "bda-insufficient":
            {
                var id = op.Targets.FirstOrDefault();
                var tot = op.Elements.Where(e => e.Spec != null).Select(e => e.Spec!.Tot).DefaultIfEmpty(double.MaxValue).Max();
                if (id == null || pic == null || now < tot + 60 || _t.Site(id) == null) return false;
                var beliefs = pic.Bda.Where(k => k.Key.Site == id).Select(k => k.Value).ToList();
                return beliefs.Count > 0 && beliefs.Min(b => b.Integrity) > c.Value;
            }
            case "time-after":
                return now >= c.Value;
            case "element-state":
            {
                var parts = c.Arg.Split(':');
                return parts.Length == 2 && op.Elements.Any(e => e.Id == parts[0] && e.State == parts[1]);
            }
        }
        return false;
    }

    private void Do(Operation op, OpAction a, double now)
    {
        switch (a.Kind)
        {
            case "activate":
                if (op.Elements.FirstOrDefault(e => e.Id == a.Arg) is { } held) { held.Held = false; if (op.Status == OpStatus.Active) Launch(op, held); }
                break;
            case "delay":
                if (op.Status == OpStatus.Planned) op.Start = Math.Max(op.Start, now) + a.Value;
                break;
            case "hold-weapons":
                op.WeaponsHeld = true;
                foreach (var f in Flights(op)) f.HoldFire = true;
                foreach (var m in FireTasks(op).Where(m => m.State == FireState.Planned)) _t.Air?.Cancel(m);
                break;
            case "contingency":
                if (op.Elements.FirstOrDefault(e => e.Id == a.Arg) is { Contingency: not null } el)
                {
                    foreach (var f in Flights(op).Where(f => el.Flights.Contains(f.Id) && f.State is not (FlightState.Landed or FlightState.Lost))) _t.Air?.Abort(f);
                    Authority.Release(Flights(op).Where(f => el.Flights.Contains(f.Id)).SelectMany(f => f.Aircraft));
                    el.Spec = el.Contingency; el.Contingency = null; el.Flights.Clear(); el.State = "waiting";
                    if (op.Status == OpStatus.Active) Launch(op, el);
                }
                break;
            case "abort":
                Release(op, "aborted by rule");
                break;
            case "rtb":
                foreach (var f in Flights(op).Where(f => f.Airborne(now))) _t.Air?.Abort(f);
                break;
            case "follow-up":
                FollowUpRequested?.Invoke(op);
                break;
        }
    }

    private static string Clock(double t) => AirTasking.Clock(t);
}
