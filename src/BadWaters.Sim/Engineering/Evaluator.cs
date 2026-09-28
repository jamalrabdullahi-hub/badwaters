namespace BadWaters.Sim.Engineering;

/// <summary>Catalog of engineering content (loaded from JSON by the data layer).</summary>
public sealed class Catalog
{
    public Dictionary<string, ComponentTemplate> Components { get; } = new();
    public Dictionary<string, FrameDef> Frames { get; } = new();
    public Dictionary<string, RatingRule> Ratings { get; } = new();
    public Dictionary<string, VisualPart> Parts { get; } = new();
}

/// <summary>Evaluates any design (ship, aircraft, munition, ground system) with the same generic rules.</summary>
public sealed class Evaluator
{
    private readonly Catalog _cat;
    public Evaluator(Catalog cat) => _cat = cat;

    public Evaluation Evaluate(DesignDef d, IReadOnlyDictionary<string, int>? techLevels = null)
    {
        var issues = new List<Issue>();
        if (!_cat.Frames.TryGetValue(d.Frame, out var frame))
            return new Evaluation(new Dictionary<string, (double, double)>(), new Dictionary<string, double>(),
                new[] { new Issue(IssueKind.UnknownPart, $"Unknown frame {d.Frame}.") }, 0, new Dictionary<string, double>());

        var used = new Dictionary<string, double>();
        var avail = new Dictionary<string, double>(frame.Budgets);
        var ratings = new Dictionary<string, double>(frame.Base);
        var portsUsed = new Dictionary<string, int>();
        var offered = new HashSet<string>(frame.Ports.Keys);
        var needs = new List<(string Port, string By)>();
        var work = new Dictionary<string, double>();
        var breakdown = new Dictionary<string, (double Weight, double Cost)>();
        Book(breakdown, "Hull and fittings", 0, frame.Cost);
        double cost = frame.Cost;
        if (frame.Factory != null) work[frame.Factory] = frame.WorkUnits;
        CheckTech(frame.Requires, frame.Name, techLevels, issues);

        foreach (var id in d.Components)
        {
            if (!_cat.Components.TryGetValue(id, out var c)) { issues.Add(new(IssueKind.UnknownPart, $"Unknown component {id}.")); continue; }
            if (c.FitsKinds.Length > 0 && !c.FitsKinds.Contains(frame.Kind))
                issues.Add(new(IssueKind.WrongKind, $"{c.Name} can't be fitted to a {frame.Kind}."));
            CheckTech(c.Requires, c.Name, techLevels, issues);
            foreach (var (axis, v) in c.Uses)
            {
                if (v >= 0) used[axis] = used.GetValueOrDefault(axis) + v;
                else avail[axis] = avail.GetValueOrDefault(axis) - v; // negative use = provides
            }
            foreach (var (r, v) in c.Adds) ratings[r] = Combine(r, ratings.TryGetValue(r, out var cur) ? cur : (double?)null, v);
            if (c.Port != null) portsUsed[c.Port] = portsUsed.GetValueOrDefault(c.Port) + 1;
            foreach (var o in c.Offers) offered.Add(o);
            foreach (var n in c.Needs) needs.Add((n, c.Name));
            cost += c.Cost;
            if (c.Factory != null) work[c.Factory] = work.GetValueOrDefault(c.Factory) + c.WorkUnits;
            Book(breakdown, FamilyGroup(c.Family), c.Uses.GetValueOrDefault("mass"), c.Cost);
        }

        // visual parts: their own stats, plus any component they carry (ports don't apply: the part is the mount)
        foreach (var pl in d.Parts)
        {
            if (!_cat.Parts.TryGetValue(pl.Part, out var p)) { issues.Add(new(IssueKind.UnknownPart, $"Unknown part {pl.Part}.")); continue; }
            if (p.FitsKinds.Length > 0 && !p.FitsKinds.Contains(frame.Kind)) issues.Add(new(IssueKind.WrongKind, $"{p.Name} can't be fitted to a {frame.Kind}."));
            if (frame.Hull != null && !HullGeometry.Inside(ShapeRules.TopHalf(frame, d), pl.X, pl.Y, Math.Max(0.1, frame.Hull.Width * 0.05))) issues.Add(new(IssueKind.NoPort, $"{p.Name} at ({pl.X:0}, {pl.Y:0}) m hangs off the hull."));
            var uses = new Dictionary<string, double>(p.Uses);
            double partCost = p.Cost;
            if (p.Component != null && _cat.Components.TryGetValue(p.Component, out var pc))
            {
                CheckTech(pc.Requires, pc.Name, techLevels, issues);
                foreach (var (axis, v) in pc.Uses) uses[axis] = uses.GetValueOrDefault(axis) + v;
                foreach (var (r, v) in pc.Adds) ratings[r] = Combine(r, ratings.TryGetValue(r, out var cur) ? cur : (double?)null, v);
                foreach (var o in pc.Offers) offered.Add(o);
                foreach (var n in pc.Needs) needs.Add((n, pc.Name));
                partCost += pc.Cost;
                if (pc.Factory != null) work[pc.Factory] = work.GetValueOrDefault(pc.Factory) + pc.WorkUnits;
            }
            foreach (var (axis, v) in uses)
            {
                if (v >= 0) used[axis] = used.GetValueOrDefault(axis) + v;
                else avail[axis] = avail.GetValueOrDefault(axis) - v;
            }
            if (p.Topside) used["topside"] = used.GetValueOrDefault("topside") + Math.Max(0, uses.GetValueOrDefault("mass"));
            // a weapon whose arc is mostly blocked by taller parts contributes less
            var arcFactor = 1.0;
            if (p.Arc > 0 && frame.Hull != null)
            {
                var (cov, _) = ArcGeometry.Coverage(d, d.Parts.IndexOf(pl), _cat, frame.Hull.Width * 3);
                arcFactor = 0.4 + 0.6 * cov;
                if (cov < 0.35) issues.Add(new(IssueKind.NoPort, $"{p.Name}: firing arc {cov:P0} clear, mostly blocked by taller structure."));
            }
            foreach (var (r, v) in p.Adds) ratings[r] = Combine(r, ratings.TryGetValue(r, out var cur) ? cur : (double?)null, v * (v > 0 ? arcFactor : 1));
            foreach (var o in p.Offers) offered.Add(o);
            cost += partCost;
            Book(breakdown, p.Category, Math.Max(0, uses.GetValueOrDefault("mass")), partCost);
        }

        foreach (var (port, n) in portsUsed)
        {
            var slots = frame.Ports.GetValueOrDefault(port);
            if (n > slots) issues.Add(new(IssueKind.NoPort, slots == 0 ? $"{frame.Name} has no {port} mount." : $"{n} components need {port} mounts but only {slots} exist."));
        }
        foreach (var (port, by) in needs)
            if (!offered.Contains(port)) issues.Add(new(IssueKind.MissingInterface, $"{by} needs a {port} interface that nothing on this design provides."));

        // outline shaping: simple deterministic modifiers (see ShapeRules)
        var shape = frame.Hull != null ? ShapeRules.Evaluate(frame, d, _cat) : null;
        if (shape != null)
        {
            foreach (var (r, v) in shape.Ratings) ratings[r] = ratings.GetValueOrDefault(r) + v;
            if (shape.Mass > 0) { used["mass"] = used.GetValueOrDefault("mass") + shape.Mass; Book(breakdown, "Shaping", shape.Mass, 0); }
        }
        var budgets = new Dictionary<string, (double, double)>();
        foreach (var axis in used.Keys.Union(avail.Keys))
        {
            var u = used.GetValueOrDefault(axis);
            var a = avail.GetValueOrDefault(axis);
            budgets[axis] = (u, a);
            if (u > a + 1e-9) issues.Add(new(IssueKind.OverBudget, $"{axis}: needs {u:0.#} but only {a:0.#} available."));
        }
        foreach (var (r, rule) in _cat.Ratings)
            if (ratings.TryGetValue(r, out var v)) ratings[r] = Math.Clamp(v, 0, rule.Cap);

        return new Evaluation(budgets, ratings, issues, cost, work, breakdown, shape?.Notes);
    }

    private static void Book(Dictionary<string, (double Weight, double Cost)> b, string key, double w, double c)
    {
        var (w0, c0) = b.GetValueOrDefault(key);
        b[key] = (w0 + w, c0 + c);
    }

    /// <summary>Groups component families into the designer's weight/cost lines.</summary>
    public static string FamilyGroup(string family) => family switch
    {
        "propulsion" or "power" or "thermal" => "Machinery",
        "sensors" => "Sensors",
        "payload" or "fuzing" or "interfaces" => "Weapons",
        "computing" or "comms" or "ew" or "navigation" or "guidance" => "Combat systems",
        _ => "Hull and fittings",
    };

    private double Combine(string rating, double? cur, double v)
    {
        if (cur is null) return v;
        var agg = _cat.Ratings.TryGetValue(rating, out var r) ? r.Aggregate : "sum";
        return agg switch { "max" => Math.Max(cur.Value, v), "min" => Math.Min(cur.Value, v), _ => cur.Value + v };
    }

    private static void CheckTech(Dictionary<string, int> req, string what, IReadOnlyDictionary<string, int>? tech, List<Issue> issues)
    {
        if (tech == null) return;
        foreach (var (field, lvl) in req)
            if (tech.GetValueOrDefault(field) < lvl) issues.Add(new(IssueKind.TechTooLow, $"{what} needs {field} level {lvl}."));
    }
}

/// <summary>Plans modernising an existing design: same frame, swapped components.</summary>
public sealed class RefitPlanner
{
    private readonly Catalog _cat;
    private readonly Evaluator _eval;
    public RefitPlanner(Catalog cat) { _cat = cat; _eval = new Evaluator(cat); }

    public sealed record RefitPlan(bool Feasible, IReadOnlyList<Issue> Issues, IReadOnlyList<string> Added, IReadOnlyList<string> Removed, double Cost, double Months);

    public RefitPlan Plan(DesignDef current, DesignDef proposed, IReadOnlyDictionary<string, int>? tech = null)
    {
        if (current.Frame != proposed.Frame)
            return new RefitPlan(false, new[] { new Issue(IssueKind.WrongKind, "A refit keeps the same frame. Changing it is a new design.") }, Array.Empty<string>(), Array.Empty<string>(), 0, 0);
        var ev = _eval.Evaluate(proposed, tech);
        var added = proposed.Components.Except(current.Components).ToList();
        var removed = current.Components.Except(proposed.Components).ToList();
        var cost = added.Sum(a => _cat.Components.TryGetValue(a, out var c) ? c.Cost * 1.25 : 0); // refits cost more than new-build fitting
        var months = 2 + added.Count * 1.5;
        return new RefitPlan(ev.Valid, ev.Issues, added, removed, cost, months);
    }
}
