using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;

namespace BadWaters.Sim.Command;

/// <summary>A nation's operational doctrine (data/core/theatre/doctrine.json): weights and limits, not scripts.</summary>
public sealed record DoctrineProfile
{
    public string Id { get; init; } = "default";
    public double Defence { get; init; } = 1.0;
    public double Strike { get; init; } = 1.0;
    public double Fires { get; init; } = 1.0;
    public double Recon { get; init; } = 0.6;
    public double Preserve { get; init; } = 1.0;
    /// <summary>0..1: losses an operation accepts before its abort rule fires.</summary>
    public double RiskTolerance { get; init; } = 0.3;
    public double CycleSeconds { get; init; } = 1800;
    public int MaxNewOpsPerCycle { get; init; } = 4;
    public int MaxConcurrentStrikes { get; init; } = 3;
    public int MaxConcurrentFires { get; init; } = 2;
    /// <summary>Role thresholds on game ratings.</summary>
    public double FighterAirDefence { get; init; } = 6;
    public double StrikerStrike { get; init; } = 6;
    public double TankerReach { get; init; } = 9;
    public double PatrolDetection { get; init; } = 8;
    /// <summary>Target value by TargetPolicy class (legitimate classes only).</summary>
    public Dictionary<string, double> TargetValue { get; init; } = new()
    {
        ["military-base"] = 10, ["command-military"] = 8, ["comms-military"] = 6, ["intelligence-site"] = 6, ["logistics"] = 6, ["industrial"] = 5, ["strategic-reserve"] = 5, ["energy"] = 4,
    };
}

/// <summary>Player guidance for a side's commander (the "strategic direction" end of the control spectrum).</summary>
public sealed class Directives
{
    /// <summary>Multipliers on objective families: defence, strike, fires, recon, preserve.</summary>
    public Dictionary<string, double> Weights { get; } = new();
    /// <summary>Areas to defend (lon, lat, weight): raises CAP priority near them.</summary>
    public List<(double Lon, double Lat, double Weight)> DefendAreas { get; } = new();
    /// <summary>Priority targets (site ids) and sites the commander must not strike.</summary>
    public HashSet<string> PriorityTargets { get; } = new();
    public HashSet<string> NoStrike { get; } = new();
    public double W(string k) => Weights.GetValueOrDefault(k, 1);
}

/// <summary>What the commander believes this cycle: derived measures, all from its own forces and its own picture.</summary>
public sealed class Situation
{
    public double Time { get; init; }
    public int PoolAircraft { get; set; }
    public int Fighters { get; set; }
    public int Strikers { get; set; }
    public int OpenOps { get; set; }
    public int KnownEnemyAir { get; set; }
    public bool EnemyAewKnown { get; set; }
    public int KnownSams { get; set; }
    public int KnownEnemyShips { get; set; }
    public int IncomingMissiles { get; set; }
    public List<string> ClosedBases { get; } = new();
    public List<string> OverloadedBases { get; } = new();
    public Dictionary<string, double> ThreatByBase { get; } = new();
    public List<string> StaleTargets { get; } = new();
    public List<string> Notes { get; } = new();
}

public sealed record ObjectiveScore(string Key, string Family, string Text, double Score);

/// <summary>
/// An autonomous theatre commander. Every cycle (and after significant events) it runs the same loop a staff would:
/// perception (own forces + its side's picture only) → assessment → objectives → forces → operation generation, using
/// the same operation system as the player. It reacts to consequences, never to scripts: lose seven airbases and the
/// measures change (closed and overloaded bases, less capacity, bigger threat), so relocation, CAP and retaliation
/// operations score differently. Deterministic for a seed.
/// </summary>
public sealed class Commander
{
    public string Side { get; }
    public DoctrineProfile Doctrine { get; }
    public Directives Directives { get; } = new();
    public Situation? Last { get; private set; }
    public List<ObjectiveScore> LastObjectives { get; } = new();
    private readonly InterceptTrial _t;
    private readonly Func<string, PresetInfo?> _preset;
    private readonly Random _rng;
    private double _next;
    private readonly Dictionary<string, string> _opFor = new();   // objective key → operation id
    private readonly HashSet<string> _followUps = new();
    private int _lostAtLastCycle;
    private double _nextLook;

    public Commander(InterceptTrial t, string side, DoctrineProfile doctrine, Func<string, PresetInfo?> preset, int seed)
    {
        _t = t; Side = side; Doctrine = doctrine; _preset = preset; _rng = new Random(seed * 977 + side.Length);
        if (t.Ops != null) t.Ops.FollowUpRequested += op => { if (op.Side == Side && op.FollowUp) foreach (var id in op.Targets) _followUps.Add(id); };
        // the scenario's opening orders become the commander's first operations
        foreach (var (e, i) in t.Scenario.Ato.Where(e => e.Side == side).Select((e, i) => (e, i)))
        {
            var op = new Operation
            {
                Id = t.Ops!.NewId(side), Name = e.Name != "" ? e.Name : $"Opening {(e.Launcher != "" ? "fires" : "package")} {i + 1}", Side = side, Author = Author.AI,
                Purpose = { e.Launcher != "" ? "fires" : e.Target != "" ? "strike" : "area-defence" }, Rationale = "opening orders",
                Elements = { new OpElement { Id = "e1", Kind = e.Launcher != "" ? ElementKind.Fires : ElementKind.Air, Role = "opening", Spec = e } },
            };
            if (e.Target != "") op.Targets.Add(e.Target);
            t.Ops.Add(op);
        }
    }

    private OperationBoard Board => _t.Ops!;
    private string Enemy => Side == "blue" ? "red" : "blue";
    private Picture? Pic => _t.Intel?.Pictures.GetValueOrDefault(Side);
    private string Name(string side) => _t.Scenario.Sides.GetValueOrDefault(side, side);

    public void Tick(double now)
    {
        if (_t.Ops == null || _t.Air == null || now < _nextLook) return;
        _nextLook = now + 10; // look at the situation every 10 s of theatre time
        // significant events pull the next cycle forward: fresh losses, incoming missiles
        var lost = _t.Air.Destroyed.Count(id => _t.Scenario.Units.FirstOrDefault(u => u.Id == id)?.Side == Side);
        var alarm = lost > _lostAtLastCycle + 2 || _t.Tracks.Any(tr => tr.State == TrackState.Flying && tr.Detected && tr.Target.Side == Side && now - tr.Launched < 20);
        if (now < _next && !(alarm && now >= _next - Doctrine.CycleSeconds + 300)) return;
        _next = now + Doctrine.CycleSeconds;
        _lostAtLastCycle = lost;
        Cycle(now);
    }

    // ---------------------------------------------------------------- perception + assessment

    private List<UnitDef> PoolAir() =>
        _t.Air!.Idle(Side).Where(u => Board.Authority.IsFree(u.Id)).OrderBy(u => u.Id, StringComparer.Ordinal).ToList();

    private bool IsFighter(UnitDef u) => u.Kind == "air" && (_preset(u.Preset)?.R("air-defence") ?? 0) >= Doctrine.FighterAirDefence;
    private bool IsStriker(UnitDef u) => u.Kind == "air" && (_preset(u.Preset)?.R("strike") ?? 0) >= Doctrine.StrikerStrike;
    private bool IsTanker(UnitDef u) { var p = _preset(u.Preset); return u.Kind == "air" && p != null && p.R("reach") >= Doctrine.TankerReach && p.R("payload") <= 0 && p.R("air-defence") <= 0; }
    private bool IsPatrol(UnitDef u) { var p = _preset(u.Preset); return u.Kind == "air" && p != null && p.R("detection") >= Doctrine.PatrolDetection && p.R("air-defence") <= 0 && p.R("payload") > 0; }

    private IEnumerable<BaseState> OwnBases => _t.Bases.Values.Where(b => b.Site.Side == Side).OrderBy(b => b.Site.Id, StringComparer.Ordinal);
    private IEnumerable<SiteDef> EnemySites => _t.Scenario.Sites.Where(s => s.Side == Enemy && TargetPolicy.IsLegitimate(s.Class) && !Directives.NoStrike.Contains(s.Id));

    /// <summary>Enemy SAMs this side knows about: fixed batteries (pre-war survey) and ships it currently holds on its picture.</summary>
    private IEnumerable<BatteryState> KnownSams(double now) => _t.Batteries.Where(b => b.Def.Side == Enemy && b.Stock > 0
        && (!b.IsShip || (Pic?.Contacts.TryGetValue(b.Def.Id, out var c) == true && now - c.LastSeen <= IntelRules.DropAfter)));

    private IEnumerable<Contact> Fresh(string kind, double now) =>
        Pic?.Contacts.Values.Where(c => c.Kind == kind && now - c.LastSeen <= IntelRules.StaleAfter) ?? Enumerable.Empty<Contact>();

    /// <summary>What this side believes about a site: its weakest component and the confidence of that belief.</summary>
    private (double Integrity, double Confidence, double AsOf) Belief(string site)
    {
        var b = Pic?.Bda.Where(k => k.Key.Site == site).Select(k => k.Value).ToList();
        if (b == null || b.Count == 0) return (100, 0.5, 0);
        return (b.Average(x => x.Integrity), b.Min(x => x.Confidence), b.Min(x => x.AsOf));
    }

    public Situation Assess(double now)
    {
        var pool = PoolAir();
        var sit = new Situation { Time = now, PoolAircraft = pool.Count, Fighters = pool.Count(IsFighter), Strikers = pool.Count(IsStriker), OpenOps = Board.Open(Side).Count() };
        var enemyAir = Fresh("air", now).Where(c => c.Role is not ("AEW" or "Tanker")).ToList();
        sit.KnownEnemyAir = enemyAir.Count;
        sit.EnemyAewKnown = Fresh("air", now).Any(c => c.Role == "AEW");
        sit.KnownSams = KnownSams(now).Count();
        sit.KnownEnemyShips = Fresh("ship", now).Count();
        sit.IncomingMissiles = _t.Tracks.Count(tr => tr.State == TrackState.Flying && tr.Detected && tr.Target.Side == Side);
        foreach (var b in OwnBases)
        {
            var based = _t.Scenario.Units.Count(u => u.Side == Side && u.Kind is "air" or "aew" && _t.Air!.BaseOf(u) == b.Site.Id && !_t.Air.Destroyed.Contains(u.Id));
            if (!b.RunwayOpen && based > 0) sit.ClosedBases.Add(b.Site.Id);
            var capacity = b.Components.Count > 0 ? 24 * b.Components.Values.Average(c => c.Integrity) / 100 : 24;
            if (based > capacity + 4) sit.OverloadedBases.Add(b.Site.Id);
            var threat = enemyAir.Count(c => Pacing.DistanceKm(c.Lon, c.Lat, b.Site.Lon, b.Site.Lat) < 350)
                         + 2 * _t.Tracks.Count(tr => tr.State == TrackState.Flying && tr.Detected && tr.Target.Id == b.Site.Id);
            sit.ThreatByBase[b.Site.Id] = threat;
        }
        foreach (var s in EnemySites) { var (_, conf, asOf) = Belief(s.Id); if (conf < 0.6 && now - asOf > 3 * 3600) sit.StaleTargets.Add(s.Id); }
        if (sit.ClosedBases.Count > 0) sit.Notes.Add($"{sit.ClosedBases.Count} base(s) with runways closed and aircraft on them");
        if (sit.OverloadedBases.Count > 0) sit.Notes.Add($"{sit.OverloadedBases.Count} base(s) overloaded");
        if (sit.IncomingMissiles > 0) sit.Notes.Add($"{sit.IncomingMissiles} missiles inbound");
        return sit;
    }

    // ---------------------------------------------------------------- objectives + generation

    private bool Busy(string key) => _opFor.TryGetValue(key, out var id) && Board.Find(id) is { Open: true };
    private int OpenWith(string purpose) => Board.Open(Side).Count(o => o.Author == Author.AI && o.Purpose.Contains(purpose));

    private void Cycle(double now)
    {
        var sit = Assess(now);
        Last = sit;
        LastObjectives.Clear();
        var candidates = new List<(ObjectiveScore Score, Func<Operation?> Build)>();
        var pool = PoolAir();
        var used = new HashSet<string>();
        List<UnitDef> Take(Func<UnitDef, bool> ok, int n, (double Lon, double Lat)? near = null)
        {
            // a group from one working base, closest first
            var g = pool.Where(u => !used.Contains(u.Id) && ok(u) && (!_t.Bases.TryGetValue(u.Base, out var b) || b.RunwayOpen))
                .GroupBy(u => (u.Base, u.Preset)).Where(x => x.Count() >= Math.Min(n, 2))
                .OrderBy(x => near == null || _t.Site(x.Key.Base) is not { } s ? 0 : Pacing.DistanceKm(s.Lon, s.Lat, near.Value.Lon, near.Value.Lat))
                .ThenBy(x => x.Key.Base, StringComparer.Ordinal).FirstOrDefault();
            var picked = g?.Take(n).ToList() ?? new List<UnitDef>();
            foreach (var u in picked) used.Add(u.Id);
            return picked;
        }
        var enemyCentroid = EnemySites.Any() ? (EnemySites.Average(s => s.Lon), EnemySites.Average(s => s.Lat)) : (0.0, 0.0);

        // 1. relocate aircraft off closed or overloaded bases (preserve the force)
        foreach (var b in sit.ClosedBases.Concat(sit.OverloadedBases).Distinct())
        {
            var key = "relocate:" + b;
            if (Busy(key)) continue;
            var stranded = pool.Where(u => u.Base == b && !used.Contains(u.Id)).ToList();
            var dest = OwnBases.Where(x => x.Site.Id != b && x.RunwayOpen && !sit.OverloadedBases.Contains(x.Site.Id))
                .OrderBy(x => sit.ThreatByBase.GetValueOrDefault(x.Site.Id)).ThenBy(x => Pacing.DistanceKm(x.Site.Lon, x.Site.Lat, _t.Site(b)!.Lon, _t.Site(b)!.Lat)).FirstOrDefault();
            if (stranded.Count == 0 || dest == null) continue;
            var move = sit.OverloadedBases.Contains(b) ? stranded.Take(stranded.Count / 2).ToList() : stranded;
            foreach (var u in move) used.Add(u.Id);
            var score = (100 + 5 * move.Count) * Doctrine.Preserve * Directives.W("preserve"); // stranded aircraft: urgent
            candidates.Add((new ObjectiveScore(key, "preserve", $"Disperse {move.Count} aircraft from {_t.Site(b)!.Name} to {dest.Site.Name}", score), () => new Operation
            {
                Id = Board.NewId(Side), Name = $"Disperse {_t.Site(b)!.Name}", Side = Side, Purpose = { "relocate" },
                Rationale = $"{_t.Site(b)!.Name} {(sit.ClosedBases.Contains(b) ? "has its runway closed" : "is overloaded")}; moving aircraft to {dest.Site.Name}",
                Elements = { new OpElement { Id = "ferry", Kind = ElementKind.Relocate, Role = "relocation", Base = dest.Site.Id, Assets = move.Select(u => u.Id).ToList() } },
            }));
        }

        // 2. defend: CAP over the most threatened bases (plus a standing CAP over the most exposed one)
        var exposed = OwnBases.Where(b => b.RunwayOpen).OrderByDescending(b => sit.ThreatByBase.GetValueOrDefault(b.Site.Id))
            .ThenBy(b => Pacing.DistanceKm(b.Site.Lon, b.Site.Lat, enemyCentroid.Item1, enemyCentroid.Item2)).Take(3).ToList();
        foreach (var (b, rank) in exposed.Select((b, i) => (b, i)))
        {
            var threat = sit.ThreatByBase.GetValueOrDefault(b.Site.Id);
            if (rank > 0 && threat <= 0) continue;
            var key = "cap:" + b.Site.Id;
            if (Busy(key)) continue;
            var area = Directives.DefendAreas.Sum(a => Pacing.DistanceKm(a.Lon, a.Lat, b.Site.Lon, b.Site.Lat) < 300 ? a.Weight : 0);
            var score = (10 + 12 * threat + 10 * area) * Doctrine.Defence * Directives.W("defence");
            var n = (int)Math.Clamp(2 + threat, 2, 8);
            var st = (b.Site.Lon + (enemyCentroid.Item1 - b.Site.Lon) * 0.25, b.Site.Lat + (enemyCentroid.Item2 - b.Site.Lat) * 0.25);
            var f = Take(IsFighter, n, st);
            if (f.Count < 2) continue;
            candidates.Add((new ObjectiveScore(key, "defence", $"CAP near {b.Site.Name} (threat {threat:0})", score), () => new Operation
            {
                Id = Board.NewId(Side), Name = $"CAP {b.Site.Name}", Side = Side, Purpose = { "area-defence", "counter-air" },
                Rationale = threat > 0 ? $"{threat:0} known threats near {b.Site.Name}" : $"standing CAP over the most exposed base, {b.Site.Name}",
                Doctrine = new Doctrine { Roe = "free", RiskTolerance = Doctrine.RiskTolerance },
                Elements = { AirElement("cap", "CAP", f, new AtoEntry { Side = Side, StationLon = st.Item1, StationLat = st.Item2, Start = now, Duration = 5400, Flights = { new FlightRequest(f[0].Preset, f.Count, Mission.CAP) } }) },
                Rules = { new OpRule(new OpCondition("losses-above", Value: 0.5), new OpAction("rtb")) },
            }));
        }

        // 3. keep an AEW up
        if (!Busy("aew"))
        {
            var aew = pool.Where(u => u.Kind == "aew" && !used.Contains(u.Id)).Take(1).ToList();
            var home = OwnBases.FirstOrDefault(b => b.RunwayOpen);
            if (aew.Count == 1 && home != null)
            {
                used.Add(aew[0].Id);
                var st = (home.Site.Lon + (enemyCentroid.Item1 - home.Site.Lon) * 0.3, home.Site.Lat + (enemyCentroid.Item2 - home.Site.Lat) * 0.3);
                candidates.Add((new ObjectiveScore("aew", "defence", "AEW coverage", 40 * Doctrine.Defence), () => new Operation
                {
                    Id = Board.NewId(Side), Name = "AEW orbit", Side = Side, Purpose = { "ISR", "area-defence" }, Rationale = "no airborne early warning up",
                    Elements = { AirElement("aew", "AEW", aew, new AtoEntry { Side = Side, StationLon = st.Item1, StationLat = st.Item2, Start = now, Duration = 7200, Flights = { new FlightRequest(aew[0].Preset, 1, Mission.AEW) } }) },
                }));
            }
        }

        // 4. strikes on the most valuable believed-intact targets (TargetPolicy-filtered), composed for the known threat
        if (OpenWith("strike") < Doctrine.MaxConcurrentStrikes)
        {
            var targets = EnemySites.Select(s => (s, b: Belief(s.Id))).Where(x => x.b.Integrity > 30 && !Board.Open(Side).Any(o => o.Targets.Contains(x.s.Id)))
                .Select(x => (x.s, Value: Doctrine.TargetValue.GetValueOrDefault(x.s.Class, 3) * x.b.Integrity / 100 * (Directives.PriorityTargets.Contains(x.s.Id) ? 2 : 1) * (_followUps.Contains(x.s.Id) ? 1.5 : 1)))
                .OrderByDescending(x => x.Value).ThenBy(x => x.s.Id, StringComparer.Ordinal).Take(4).ToList();
            foreach (var (tgt, value) in targets)
            {
                var key = "strike:" + tgt.Id;
                if (Busy(key)) continue;
                var sams = KnownSams(now).Count(b => Pacing.DistanceKm(b.Def.Lon, b.Def.Lat, tgt.Lon, tgt.Lat) <= b.ReachKm * 1.2);
                var strikers = Take(IsStriker, 4, (tgt.Lon, tgt.Lat));
                if (strikers.Count < 2) continue;
                var info = _preset(strikers[0].Preset)!;
                var from = _t.Site(strikers[0].Base)!;
                var dist = Pacing.DistanceKm(from.Lon, from.Lat, tgt.Lon, tgt.Lat);
                var radius = AirPacing.RadiusKm(info.R("reach"));
                var tanker = dist > radius ? Take(IsTanker, 1) : new List<UnitDef>();
                if (dist > radius + (tanker.Count > 0 ? AirPacing.TankerBonusKm : 0)) { foreach (var u in strikers.Concat(tanker)) used.Remove(u.Id); continue; }
                var sead = sams > 0 ? Take(u => IsStriker(u) && (_preset(u.Preset)?.R("signature") ?? 0) >= 5, 2, (tgt.Lon, tgt.Lat)) : new List<UnitDef>();
                var escort = sit.KnownEnemyAir > 0 || sit.EnemyAewKnown ? Take(IsFighter, 2, (tgt.Lon, tgt.Lat)) : new List<UnitDef>();
                var pHit = Math.Clamp(0.25 + 0.06 * info.R("strike"), 0.05, 0.95);
                var expLoss = 0.08 * Math.Max(0, sams - (sead.Count > 0 ? 1 : 0)) * strikers.Count;
                var score = value * 6 * pHit * Doctrine.Strike * Directives.W("strike") - 6 * expLoss * Doctrine.Preserve * Directives.W("preserve");
                if (score <= 2) { foreach (var u in strikers.Concat(tanker).Concat(sead).Concat(escort)) used.Remove(u.Id); continue; }
                var v = AirPacing.CruiseKmPerSec(info.R("speed"));
                var tot = now + AirPacing.PrepSeconds * 2 + dist * 1.4 / v + 900;
                var reason = $"{tgt.Name}: value {value:0.#}, {sams} known SAM{(sams == 1 ? "" : "s")} covering, {(sit.KnownEnemyAir > 0 ? $"{sit.KnownEnemyAir} enemy air contacts" : "no enemy air seen")}";
                candidates.Add((new ObjectiveScore(key, "strike", $"Strike {tgt.Name}", score), () =>
                {
                    var spec = new AtoEntry { Side = Side, Name = $"Strike {tgt.Name}", Target = tgt.Id, Tot = tot, Tanker = tanker.Count > 0, Flights = { new FlightRequest(strikers[0].Preset, strikers.Count, Mission.Strike) } };
                    if (tanker.Count > 0) spec.Flights.Add(new FlightRequest(tanker[0].Preset, 1, Mission.Tanker));
                    if (sead.Count > 0) spec.Flights.Add(new FlightRequest(sead[0].Preset, sead.Count, Mission.SEAD));
                    var op = new Operation
                    {
                        Id = Board.NewId(Side), Name = $"Strike {tgt.Name}", Side = Side, Purpose = { "strike" }, Targets = { tgt.Id }, Rationale = reason, Score = score,
                        Doctrine = new Doctrine { Roe = "free", RiskTolerance = Doctrine.RiskTolerance },
                        Elements = { AirElement("package", "strike package", strikers.Concat(tanker).Concat(sead).ToList(), spec) },
                        Rules =
                        {
                            new OpRule(new OpCondition("enemy-air-near", "4", 150), new OpAction("delay", Value: 1800)),
                            new OpRule(new OpCondition("losses-above", Value: Doctrine.RiskTolerance), new OpAction("abort")),
                            new OpRule(new OpCondition("bda-insufficient", Value: 60), new OpAction("follow-up")),
                        },
                    };
                    if (escort.Count > 0)
                    {
                        op.Elements.Add(AirElement("escort", "escort", escort, new AtoEntry { Side = Side, Target = tgt.Id, Tot = tot, Flights = { new FlightRequest(escort[0].Preset, escort.Count, Mission.Escort) } }, held: !sit.EnemyAewKnown && sit.KnownEnemyAir == 0));
                        op.Rules.Add(new OpRule(new OpCondition("enemy-aew-detected"), new OpAction("activate", "escort")));
                    }
                    return op;
                }));
            }
        }

        // 5. missile fires from free launchers at the best target in reach
        if (OpenWith("fires") < Doctrine.MaxConcurrentFires)
            foreach (var l in _t.Launchers.Where(l => l.Def.Side == Side && l.Stock >= 2 && Board.Authority.IsFree(l.Def.Id)).OrderBy(l => l.Def.Id, StringComparer.Ordinal))
            {
                var key = "fires:" + l.Def.Id;
                if (Busy(key)) continue;
                var best = EnemySites.Where(s => Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, s.Lon, s.Lat) <= l.ReachKm)
                    .Select(s => (s, v: Doctrine.TargetValue.GetValueOrDefault(s.Class, 3) * Belief(s.Id).Integrity / 100)).Where(x => x.v > 3)
                    .OrderByDescending(x => x.v).ThenBy(x => x.s.Id, StringComparer.Ordinal).FirstOrDefault();
                if (best.s == null) continue;
                var n = Math.Min(4, l.Stock / 2);
                var flight = Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, best.s.Lon, best.s.Lat) / Pacing.SpeedKmPerSec(l.Missile.R("speed"));
                var score = best.v * 3 * Doctrine.Fires * Directives.W("fires");
                candidates.Add((new ObjectiveScore(key, "fires", $"{l.Def.Name} fires {n}× at {best.s.Name}", score), () => new Operation
                {
                    Id = Board.NewId(Side), Name = $"Fires {l.Def.Name} → {best.s.Name}", Side = Side, Purpose = { "fires", "strike" }, Targets = { best.s.Id },
                    Rationale = $"{best.s.Name} in reach of {l.Def.Name}, believed {Belief(best.s.Id).Integrity:0}% intact", Score = score,
                    Elements = { new OpElement { Id = "salvo", Kind = ElementKind.Fires, Role = "missile fires", Assets = { l.Def.Id },
                        Spec = new AtoEntry { Side = Side, Launcher = l.Def.Id, Target = best.s.Id, Count = n, Tot = now + flight + 300 } } },
                }));
            }

        // 6. recon where our picture is stale, or after a strike that needs BDA
        foreach (var site in sit.StaleTargets.Concat(_followUps).Distinct().OrderBy(x => x, StringComparer.Ordinal).Take(2))
        {
            var key = "recon:" + site;
            if (Busy(key) || _t.Site(site) is not { } s) continue;
            var ac = Take(u => IsPatrol(u) || (_preset(u.Preset)?.R("signature") ?? 0) >= 7, 1, (s.Lon, s.Lat));
            if (ac.Count == 0) continue;
            var v = AirPacing.CruiseKmPerSec(_preset(ac[0].Preset)!.R("speed"));
            var dist = Pacing.DistanceKm(_t.Site(ac[0].Base)!.Lon, _t.Site(ac[0].Base)!.Lat, s.Lon, s.Lat);
            candidates.Add((new ObjectiveScore(key, "recon", $"Recon {s.Name}", 20 * Doctrine.Recon * Directives.W("recon")), () => new Operation
            {
                Id = Board.NewId(Side), Name = $"Recon {s.Name}", Side = Side, Purpose = { "recon", "ISR" }, Targets = { s.Id },
                Rationale = _followUps.Contains(site) ? "strike BDA insufficient" : "our picture of it is stale",
                Doctrine = new Doctrine { Roe = "hold", Emcon = "silent" },
                Elements = { AirElement("recon", "recon", ac, new AtoEntry { Side = Side, Target = s.Id, Tot = now + AirPacing.PrepSeconds * 2 + dist * 1.3 / v + 600, Flights = { new FlightRequest(ac[0].Preset, 1, Mission.Recon) } }) },
            }));
        }

        // choose: best scores within the per-cycle budget; each objective key at most once
        foreach (var (score, build) in candidates.OrderByDescending(c => c.Score.Score).ThenBy(c => c.Score.Key, StringComparer.Ordinal))
        {
            LastObjectives.Add(score);
            if (LastObjectives.Count(o => _opFor.ContainsKey(o.Key) && Busy(o.Key)) >= Doctrine.MaxNewOpsPerCycle) continue;
        }
        var made = 0;
        foreach (var (score, build) in candidates.OrderByDescending(c => c.Score.Score).ThenBy(c => c.Score.Key, StringComparer.Ordinal))
        {
            if (made >= Doctrine.MaxNewOpsPerCycle) break;
            var op = build();
            if (op == null) continue;
            op.Author = Author.AI;
            op.Score = score.Score;
            op.Start = Math.Max(op.Start, now);
            if (Board.Add(op).Count > 0) continue;
            _opFor[score.Key] = op.Id;
            if (score.Family is "strike" or "recon") foreach (var t in op.Targets) _followUps.Remove(t); // one look per request
            made++;
        }
    }

    private static OpElement AirElement(string id, string role, List<UnitDef> ac, AtoEntry spec, bool held = false) =>
        new() { Id = id, Kind = ElementKind.Air, Role = role, Assets = ac.Select(u => u.Id).ToList(), Spec = spec, Held = held };
}
