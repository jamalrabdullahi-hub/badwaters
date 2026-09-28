using BadWaters.Sim.Engineering;

namespace BadWaters.Sim.Economy;

/// <summary>One node of a supply-chain tree: a good, how much, and the process (and facility) that makes it.</summary>
public sealed record ChainNode(string Good, double Qty, string? Process, string? Facility, string? Deposit, IReadOnlyList<ChainNode> Inputs)
{
    public int Depth => Inputs.Count == 0 ? 1 : 1 + Inputs.Max(i => i.Depth);
    public IEnumerable<ChainNode> All() => new[] { this }.Concat(Inputs.SelectMany(i => i.All()));
}

/// <summary>Flattened requirements: raw resources, facility work, energy and labour for a quantity of goods.</summary>
public sealed record Requirements(
    IReadOnlyDictionary<string, double> Resources,
    IReadOnlyDictionary<string, double> FacilityBatches,
    IReadOnlyDictionary<string, double> Goods,
    double Energy,
    IReadOnlyDictionary<string, double> Labour,
    IReadOnlyCollection<string> Tooling);

/// <summary>
/// Recursive bill-of-materials resolution over the goods graph. A design's bill is its frame + components + parts
/// (from data templates); each good expands through its primary producing process down to deposits. Pure and
/// deterministic; the solver uses the same graph, so a missing link anywhere shows up everywhere.
/// </summary>
public sealed class BillResolver
{
    private readonly EconomyCatalog _econ;
    private readonly Catalog _eng;
    public BillResolver(EconomyCatalog econ, Catalog eng) { _econ = econ; _eng = eng; }

    /// <summary>Direct part-class goods and assembly steps (facility → batches) for one unit of a design.</summary>
    public (Dictionary<string, double> Goods, Dictionary<string, double> Assembly) DesignBill(DesignDef d)
    {
        var goods = new Dictionary<string, double>();
        var asm = new Dictionary<string, double>();
        void Add(Dictionary<string, double> into, string k, double v) { if (v > 0) into[k] = into.GetValueOrDefault(k) + v; }
        if (!_eng.Frames.TryGetValue(d.Frame, out var frame)) return (goods, asm);
        if (_econ.Bills.Frames.TryGetValue(frame.Kind, out var fb))
        {
            var scale = fb.Per == "mass" ? frame.Budgets.GetValueOrDefault("mass") / fb.Unit : 1;
            foreach (var (g, q) in fb.Goods) Add(goods, g, q * scale);
            Add(asm, fb.Facility, Math.Max(1, scale / 4));
        }
        void Component(string id)
        {
            if (!_eng.Components.TryGetValue(id, out var c) || !_econ.Bills.Families.TryGetValue(c.Family, out var t) || t.Goods.Count == 0) return;
            var scale = Math.Max(0.1, c.Cost / 10);
            foreach (var (g, q) in t.Goods) Add(goods, g, q * scale);
            if (t.Facility != "") Add(asm, t.Facility, Math.Max(0.2, scale / 5));
        }
        foreach (var id in d.Components) Component(id);
        foreach (var pl in d.Parts)
        {
            if (!_eng.Parts.TryGetValue(pl.Part, out var p)) continue;
            var res = PartResolver.Resolve(p, pl, _eng);
            var mass = Math.Max(0, res.Uses.GetValueOrDefault("mass"));
            var tpl = _econ.Bills.Parts.GetValueOrDefault(p.Category) ?? _econ.Bills.DefaultPart;
            foreach (var (g, q) in tpl) Add(goods, g, q * mass / 10);
            if (p.Component != null) Component(p.Component);
        }
        return (goods, asm);
    }

    /// <summary>Supply-chain tree for a quantity of a good down to deposits (or to goods that can only be imported).</summary>
    public ChainNode Tree(string good, double qty) => Tree(good, qty, new HashSet<string>());

    private ChainNode Tree(string good, double qty, HashSet<string> path)
    {
        var p = _econ.Producers(good).FirstOrDefault(x => !x.Inputs.Keys.Any(path.Contains));
        if (p == null || !path.Add(good)) return new ChainNode(good, qty, null, null, null, Array.Empty<ChainNode>());
        var batches = qty / p.Outputs[good];
        var kids = p.Inputs.Select(kv => Tree(kv.Key, kv.Value * batches, path)).ToList();
        path.Remove(good);
        return new ChainNode(good, qty, p.Id, p.Facility, p.Deposit, kids);
    }

    /// <summary>Total requirements for a basket of goods (e.g. a design's bill), flattened.</summary>
    public Requirements Flatten(IReadOnlyDictionary<string, double> basket, IReadOnlyDictionary<string, double>? assembly = null, bool electricityLeaf = false)
    {
        var res = new Dictionary<string, double>(); var fac = new Dictionary<string, double>(); var goods = new Dictionary<string, double>();
        var lab = new Dictionary<string, double>(); var tools = new HashSet<string>(); double energy = 0;
        void Walk(string good, double qty, HashSet<string> path)
        {
            goods[good] = goods.GetValueOrDefault(good) + qty;
            if (electricityLeaf && good == "electricity") return; // sized by the energy mix instead
            var p = _econ.Producers(good).FirstOrDefault(x => !x.Inputs.Keys.Any(path.Contains));
            if (p == null || !path.Add(good)) return;
            var b = qty / p.Outputs[good];
            fac[p.Facility] = fac.GetValueOrDefault(p.Facility) + b;
            energy += p.Energy * b;
            foreach (var (k, v) in p.Labour) lab[k] = lab.GetValueOrDefault(k) + v * b;
            if (p.Tooling != null) tools.Add(p.Tooling);
            if (p.Deposit != null) res[p.Deposit] = res.GetValueOrDefault(p.Deposit) + qty;
            foreach (var (g, q) in p.Inputs) Walk(g, q * b, path);
            if (p.Energy > 0 && !path.Contains("electricity")) Walk("electricity", p.Energy * b, path);
            path.Remove(good);
        }
        foreach (var (g, q) in basket) Walk(g, q, new HashSet<string>());
        if (assembly != null) foreach (var (f, b) in assembly) fac[f] = fac.GetValueOrDefault(f) + b;
        return new Requirements(res, fac, goods, energy, lab, tools);
    }

    /// <summary>Goods whose expansion loops back on itself through every producer (a data error).</summary>
    public IEnumerable<string> Cycles()
    {
        var ok = new HashSet<string>(); var bad = new HashSet<string>();
        bool Walk(string good, HashSet<string> path)
        {
            if (ok.Contains(good)) return true;
            if (bad.Contains(good)) return false;
            var ps = _econ.Producers(good);
            if (ps.Count == 0) { ok.Add(good); return true; }
            var p = ps.FirstOrDefault(x => !x.Inputs.Keys.Any(path.Contains));
            if (p == null) { bad.Add(good); return false; }
            path.Add(good);
            var fine = p.Inputs.Keys.All(i => Walk(i, path));
            path.Remove(good);
            (fine ? ok : bad).Add(good);
            return fine;
        }
        return _econ.Goods.Keys.Where(g => !Walk(g, new HashSet<string>())).ToList();
    }
}
