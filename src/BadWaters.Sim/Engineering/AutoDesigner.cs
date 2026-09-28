namespace BadWaters.Sim.Engineering;

/// <summary>An auto-design role (data/core/auto_roles.json): which ratings matter and what the design must carry.</summary>
public sealed record AutoRole
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public bool Sub { get; init; }
    public required string Frame { get; init; }
    /// <summary>Take the component list from this design (else <see cref="Components"/>).</summary>
    public string? BaseDesign { get; init; }
    public List<string> Components { get; init; } = new();
    public Dictionary<string, double> Weights { get; init; } = new();
    /// <summary>"cat:Category" (best part of that category) or "part:id", placed first, in order.</summary>
    public List<string> Must { get; init; } = new();
}

/// <summary>
/// Auto-design: a deterministic greedy fill. Place the role's must-haves, then keep adding the part with the best
/// weighted rating gain per weight and cost that fits the budgets and the hull, then add berths until the crew is
/// covered. Weapons go fore and aft, tall parts amidships. Same inputs and seed give the same design.
/// </summary>
public sealed class AutoDesigner
{
    private readonly Catalog _cat;
    private readonly Evaluator _eval;
    public AutoDesigner(Catalog cat) { _cat = cat; _eval = new Evaluator(cat); }

    public sealed record Result(DesignDef Design, Evaluation Evaluation, double Score);

    public const int MaxParts = 40, MaxCopies = 3;
    private const double MassReserve = 0.92; // leave room for berths

    public Result Design(AutoRole role, string id, string name, IReadOnlyDictionary<string, DesignDef> designs, string? frameId = null, int seed = 1, IReadOnlyDictionary<string, int>? tech = null)
    {
        var frame = _cat.Frames[frameId ?? role.Frame];
        var comps = role.BaseDesign != null && designs.TryGetValue(role.BaseDesign, out var bd) ? bd.Components : role.Components;
        var d = new DesignDef { Id = id, Name = name, Frame = frame.Id, Components = new List<string>(comps) };
        var rng = new Random(seed);
        var jitter = new Dictionary<string, double>();
        double J(string pid) => jitter.TryGetValue(pid, out var j) ? j : jitter[pid] = 1 + (seed == 1 ? 0 : 0.25 * rng.NextDouble());
        var rejected = new HashSet<string>();
        var ev = _eval.Evaluate(d, tech);

        var candidates = _cat.Parts.Values.Where(p => p.FitsKinds.Length == 0 || p.FitsKinds.Contains(frame.Kind)).Where(p => p.Category != "Accommodation")
            .Where(p => role.Sub == SubRules.IsSub(frame) || !IsSubOnly(p)).Where(p => !role.Sub || !p.Topside || p.Category is "Sub masts" or "Submarine fittings" or "Superstructure")
            .OrderBy(p => p.Id, StringComparer.Ordinal).ToList();

        double Gain(VisualPart p)
        {
            var res = PartResolver.Resolve(p, new Placement(p.Id, 0, 0), _cat);
            double g = 0;
            foreach (var (r, w) in role.Weights)
            {
                var a = res.Adds.GetValueOrDefault(r);
                if (a == 0) continue;
                var cur = ev.Ratings.GetValueOrDefault(r);
                var agg = _cat.Ratings.TryGetValue(r, out var rule) ? rule.Aggregate : "sum";
                g += w * (agg == "max" ? Math.Max(0, a - cur) : a * Math.Max(0, 1 - cur / 10));
            }
            var massBudget = Math.Max(1, frame.Budgets.GetValueOrDefault("mass"));
            return g * J(p.Id) / (1 + 10 * Math.Max(0, res.Uses.GetValueOrDefault("mass")) / massBudget + res.Cost / 200);
        }

        bool Fits(VisualPart p, double reserve = MassReserve)
        {
            var res = PartResolver.Resolve(p, new Placement(p.Id, 0, 0), _cat);
            foreach (var (axis, v) in res.Uses)
            {
                if (v <= 0 || axis == "crew") continue;
                var (u, a) = ev.Budgets.GetValueOrDefault(axis);
                if (u + v > a * (axis == "mass" ? reserve : 1) + 1e-9) return false;
            }
            if (p.Topside)
            {
                var (u, a) = ev.Budgets.GetValueOrDefault("topside");
                if (a > 0 && u + res.Uses.GetValueOrDefault("mass") > a) return false;
            }
            return true;
        }

        bool TryAdd(VisualPart p)
        {
            var spot = Spot(frame, d, p);
            if (spot == null) return false;
            var before = ev.Issues.Count;
            d.Parts.Add(new Placement(p.Id, spot.Value.X, spot.Value.Y));
            var next = _eval.Evaluate(d, tech);
            // keep it only if it didn't create a new problem (other than the crew, which berths fix afterwards)
            if (next.Issues.Count(i => !IsCrew(i)) > ev.Issues.Count(i => !IsCrew(i)) && !SubMissing(ev, next))
            {
                d.Parts.RemoveAt(d.Parts.Count - 1);
                return false;
            }
            ev = next;
            return true;
        }

        // 1. must-haves
        foreach (var m in role.Must)
        {
            var pool = m.StartsWith("part:") ? candidates.Where(p => p.Id == m[5..]).ToList() : candidates.Where(p => p.Category == m[4..]).ToList();
            foreach (var p in pool.OrderByDescending(Gain).ThenBy(p => p.Id, StringComparer.Ordinal))
                if ((Fits(p, 1) || m.StartsWith("part:")) && TryAdd(p)) break;
        }
        // 2. greedy fill
        while (d.Parts.Count < MaxParts)
        {
            var best = candidates.Where(p => !rejected.Contains(p.Id) && Room(frame, d, p) && Fits(p))
                .Select(p => (p, g: Gain(p))).Where(x => x.g > 0.05).OrderByDescending(x => x.g).ThenBy(x => x.p.Id, StringComparer.Ordinal).FirstOrDefault();
            if (best.p == null) break;
            if (!TryAdd(best.p)) rejected.Add(best.p.Id);
        }
        // 3. berths until the crew is covered
        var berths = _cat.Parts.Values.Where(p => p.Category == "Accommodation" && (p.FitsKinds.Length == 0 || p.FitsKinds.Contains(frame.Kind)))
            .OrderByDescending(p => -p.Uses.GetValueOrDefault("crew")).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
        for (var n = 0; n < 12 && Short(ev) > 0; n++)
        {
            var need = Short(ev);
            var pick = berths.Where(b => -b.Uses.GetValueOrDefault("crew") >= need).OrderBy(b => b.Uses.GetValueOrDefault("mass")).FirstOrDefault() ?? berths.FirstOrDefault();
            if (pick == null || !TryAdd(pick)) break;
        }
        // 4. drop parts from the end until everything fits
        while (!ev.Valid && d.Parts.Count > role.Must.Count && ev.Issues.Any(i => i.Kind == IssueKind.OverBudget))
        {
            d.Parts.RemoveAt(FindLeastValuable(d, role));
            ev = _eval.Evaluate(d, tech);
        }
        var score = role.Weights.Sum(w => w.Value * ev.Ratings.GetValueOrDefault(w.Key)) - 5 * ev.Issues.Count;
        return new Result(d, ev, Math.Round(score, 2));
    }

    private int FindLeastValuable(DesignDef d, AutoRole role)
    {
        var must = role.Must.Count;
        var worst = must; var worstV = double.MaxValue;
        for (var i = must; i < d.Parts.Count; i++)
        {
            var p = _cat.Parts.GetValueOrDefault(d.Parts[i].Part);
            if (p == null || p.Category == "Accommodation") continue;
            var v = role.Weights.Sum(w => w.Value * p.Adds.GetValueOrDefault(w.Key)) / (1 + p.Uses.GetValueOrDefault("mass"));
            if (v < worstV) { worstV = v; worst = i; }
        }
        return worst;
    }

    /// <summary>How many of a part a design may take: aircraft one of each (two for stores and pods), one cockpit.</summary>
    private bool Room(FrameDef f, DesignDef d, VisualPart p)
    {
        var copies = d.Parts.Count(x => x.Part == p.Id);
        if (p.Category.StartsWith("Cockpit") && d.Parts.Any(x => _cat.Parts.GetValueOrDefault(x.Part)?.Category == p.Category)) return false;
        if (f.Kind == "aircraft")
        {
            var placed = d.Parts.Select(x => _cat.Parts.GetValueOrDefault(x.Part)).Where(x => x != null).ToList();
            foreach (var one in OneOf) if (p.Id.Contains(one) && placed.Any(x => x!.Id.Contains(one))) return false;
            if (p.Category == "Engines") return placed.All(x => x!.Category != "Engines" || x.Id == p.Id) && copies < 2;
        }
        return f.Kind == "aircraft" ? copies < (p.Category is "Stores" or "Sensors & pods" ? 2 : 1) : copies < MaxCopies;
    }

    /// <summary>An airframe takes one of each of these (by part id).</summary>
    private static readonly string[] OneOf = { "canopy", "tail", "intake", "cargo-ramp", "winglet", "probe" };

    private static bool Structure(VisualPart p) => p.Category is "Pressure hull" or "Ballast";
    private static bool IsCrew(Issue i) => i.Kind == IssueKind.OverBudget && i.Text.StartsWith("crew");
    private static bool IsSubOnly(VisualPart p) => p.Category is "Pressure hull" or "Ballast" or "Sub propulsion" or "Sub sensors" or "Sub masts" or "Torpedo room" or "Sub control" or "Submarine fittings";
    /// <summary>While building a sub, the "missing pressure hull/ballast/propulsor" issues come and go: don't count them as new problems.</summary>
    private static bool SubMissing(Evaluation before, Evaluation after) =>
        after.Issues.Where(i => !IsCrew(i)).All(i => i.Text.StartsWith("Submarine has no") || before.Issues.Contains(i));
    private static double Short(Evaluation ev) => ev.Budgets.TryGetValue("crew", out var c) ? c.Used - c.Available : 0;

    /// <summary>Bounding box of a part's shapes (metres, part-local).</summary>
    public static (double X0, double Y0, double X1, double Y1) Bounds(VisualPart p)
    {
        double x0 = 0, y0 = 0, x1 = 0, y1 = 0; var any = false;
        void Add(double x, double y) { if (!any) { x0 = x1 = x; y0 = y1 = y; any = true; } else { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); } }
        foreach (var s in p.Shapes)
            switch (s.T)
            {
                case "rect": Add(s.X - s.W / 2, s.Y - s.H / 2); Add(s.X + s.W / 2, s.Y + s.H / 2); break;
                case "circle": Add(s.X - s.R, s.Y - s.R); Add(s.X + s.R, s.Y + s.R); break;
                default: for (var i = 0; i + 1 < s.P.Length; i += 2) Add(s.P[i], s.P[i + 1]); break;
            }
        return any ? (x0, y0, x1, y1) : (-0.5, -0.5, 0.5, 0.5);
    }

    /// <summary>A free spot on the hull: weapons toward the ends, tall parts amidships, the rest from amidships out.</summary>
    private (double X, double Y)? Spot(FrameDef f, DesignDef d, VisualPart p)
    {
        if (f.Hull == null) return (0, 0);
        var half = ShapeRules.TopHalf(f, d);
        var b = Bounds(p);
        // structure (pressure hull, ballast) is a layer under everything else: the two layers don't block each other
        var layer = Structure(p);
        var taken = d.Parts.Where(x => Structure(_cat.Parts[x.Part]) == layer).Select(x => (x, bb: Bounds(_cat.Parts[x.Part]))).Select(t => (t.x.X + t.bb.X0, t.x.Y + t.bb.Y0, t.x.X + t.bb.X1, t.x.Y + t.bb.Y1)).ToList();
        var L = f.Hull.Length; var W = f.Hull.Width;
        var step = Math.Max(0.5, L / 60);
        var ys = Enumerable.Range(0, (int)(0.9 * L / step) + 1).Select(i => 0.45 * L - i * step).ToList();
        ys = p.Arc > 0 ? ys.OrderByDescending(Math.Abs).ToList() : ys.OrderBy(Math.Abs).ToList();
        var xs = new List<double> { 0 };
        for (var k = 1; k <= 6; k++) { xs.Add(k * W / 14); xs.Add(-k * W / 14); }
        foreach (var y in ys)
            foreach (var x in xs)
            {
                double ax = x + b.X0, ay = y + b.Y0, bx = x + b.X1, by = y + b.Y1;
                if (!(HullGeometry.Inside(half, ax, ay, 0.2) && HullGeometry.Inside(half, bx, by, 0.2) && HullGeometry.Inside(half, ax, by, 0.2) && HullGeometry.Inside(half, bx, ay, 0.2) && HullGeometry.Inside(half, x, y, 0.2))) continue;
                if (taken.Any(t => ax < t.Item3 - 0.05 && bx > t.Item1 + 0.05 && ay < t.Item4 - 0.05 && by > t.Item2 + 0.05)) continue;
                return (Math.Round(x, 2), Math.Round(y, 2));
            }
        return null;
    }
}
