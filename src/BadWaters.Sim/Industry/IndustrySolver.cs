namespace BadWaters.Sim.Industry;

/// <summary>A factory type: turns input goods into output (a good or a component class) using work capacity.</summary>
public sealed record FactoryDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Good produced (e.g. "semiconductors"), or empty for component-assembly factories.</summary>
    public string Produces { get; init; } = "";
    /// <summary>Work units per month per factory level.</summary>
    public double CapacityPerLevel { get; init; } = 10;
    /// <summary>Input goods consumed per work unit.</summary>
    public Dictionary<string, double> InputsPerWork { get; init; } = new();
    /// <summary>Output goods per work unit (for goods factories).</summary>
    public double OutputPerWork { get; init; } = 1;
}

public sealed class FactoryInstance
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Park { get; init; }
    public int Level { get; set; } = 1;
    public double Damage { get; set; } // 0..1
}

public sealed record ProductionReport(
    IReadOnlyDictionary<string, double> WorkDone,
    IReadOnlyDictionary<string, double> WorkDemanded,
    IReadOnlyDictionary<string, double> Shortages,
    IReadOnlyList<string> Bottlenecks);

/// <summary>
/// Monthly industrial flow. Goods factories run first (their output feeds others), then component
/// and assembly factories draw on the pooled goods. Imports arrive through trade routes, scaled by
/// their delivery factor. Shortfalls in any input throttle the factory proportionally.
/// </summary>
public static class IndustrySolver
{
    public static ProductionReport RunMonth(
        IReadOnlyDictionary<string, FactoryDef> types,
        IEnumerable<FactoryInstance> factories,
        Dictionary<string, double> stock,
        IReadOnlyDictionary<string, double> imports,
        IReadOnlyDictionary<string, double> workDemand)
    {
        foreach (var (g, q) in imports) stock[g] = stock.GetValueOrDefault(g) + q;

        var capacity = new Dictionary<string, double>();
        foreach (var f in factories)
            capacity[f.Type] = capacity.GetValueOrDefault(f.Type) + types[f.Type].CapacityPerLevel * f.Level * (1 - f.Damage);

        var done = new Dictionary<string, double>();
        var shortages = new Dictionary<string, double>();
        var bottlenecks = new List<string>();

        // goods producers first, in dependency order (simple: goods with no factory inputs first)
        var order = types.Values.OrderBy(t => t.Produces == "" ? 1 : 0).ThenBy(t => t.InputsPerWork.Count).ToList();
        foreach (var t in order)
        {
            var cap = capacity.GetValueOrDefault(t.Id);
            if (cap <= 0) continue;
            var want = t.Produces != "" ? cap : Math.Min(cap, workDemand.GetValueOrDefault(t.Id));
            if (want <= 0) continue;
            var frac = 1.0;
            foreach (var (g, per) in t.InputsPerWork)
            {
                var need = want * per;
                var have = stock.GetValueOrDefault(g);
                if (have < need)
                {
                    frac = Math.Min(frac, need <= 0 ? 1 : have / need);
                    shortages[g] = shortages.GetValueOrDefault(g) + (need - have);
                }
            }
            var work = want * frac;
            foreach (var (g, per) in t.InputsPerWork) stock[g] = stock.GetValueOrDefault(g) - work * per;
            if (t.Produces != "") stock[t.Produces] = stock.GetValueOrDefault(t.Produces) + work * t.OutputPerWork;
            done[t.Id] = work;
            if (t.Produces == "" && workDemand.GetValueOrDefault(t.Id) > cap * 1.001)
                bottlenecks.Add($"{t.Name}: capacity {cap:0.#} work/month vs demand {workDemand[t.Id]:0.#}");
            if (frac < 0.999) bottlenecks.Add($"{t.Name}: input shortage, running at {frac:P0}");
        }
        return new ProductionReport(done, workDemand, shortages, bottlenecks);
    }
}
