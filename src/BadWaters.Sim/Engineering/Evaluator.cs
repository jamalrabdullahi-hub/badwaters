namespace BadWaters.Sim.Engineering;

/// <summary>Catalog of engineering content (loaded from JSON by the data layer).</summary>
public sealed class Catalog
{
    public Dictionary<string, ComponentTemplate> Components { get; } = new();
    public Dictionary<string, FrameDef> Frames { get; } = new();
    public Dictionary<string, RatingRule> Ratings { get; } = new();
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
        }

        foreach (var (port, n) in portsUsed)
        {
            var slots = frame.Ports.GetValueOrDefault(port);
            if (n > slots) issues.Add(new(IssueKind.NoPort, slots == 0 ? $"{frame.Name} has no {port} mount." : $"{n} components need {port} mounts but only {slots} exist."));
        }
        foreach (var (port, by) in needs)
            if (!offered.Contains(port)) issues.Add(new(IssueKind.MissingInterface, $"{by} needs a {port} interface that nothing on this design provides."));

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

        return new Evaluation(budgets, ratings, issues, cost, work);
    }

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
