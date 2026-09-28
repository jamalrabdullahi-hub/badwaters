using BadWaters.Sim.Engineering;

namespace BadWaters.Sim.Economy;

public sealed record ProcessLimit(string Process, string Site, double Planned, double Actual, string Reason);
public sealed record ProgrammeStatus(string Id, string Design, double Wanted, double Built, string Limit);

/// <summary>What happened in one nation in one month.</summary>
public sealed class NationReport
{
    public string Nation { get; init; } = "";
    public int Month { get; init; }
    public Dictionary<string, double> Produced { get; } = new();
    public Dictionary<string, double> Consumed { get; } = new();
    public Dictionary<string, double> Imported { get; } = new();
    /// <summary>Unmet demand per good after domestic production, stocks and imports.</summary>
    public Dictionary<string, double> Shortages { get; } = new();
    public List<ProcessLimit> Limits { get; } = new();
    public List<string> Bottlenecks { get; } = new();
    public List<ProgrammeStatus> Programmes { get; } = new();
    public double EnergySupply { get; set; }
    public double EnergyDemand { get; set; }
    public Dictionary<string, double> LabourFactor { get; } = new();
    public double BasketSatisfaction { get; set; } = 1;
    public double ValueAdded { get; set; }
    public double ImportCost { get; set; }
    public double MaintenanceMet { get; set; } = 1;
    /// <summary>Share of civilian and industrial demand that went unmet (value-weighted): lost output for GDP.</summary>
    public double ShortfallLoss { get; set; }
    public double DefenceSpent { get; set; }
    /// <summary>Planning view per good: gross requirement, stock on hand, net to make or buy.</summary>
    public Dictionary<string, (double Req, double Stock, double Net)> Plan { get; } = new();

    /// <summary>Why a good is short: the chain of limiting processes, followed upstream.</summary>
    public List<string> Explain(string good, EconomyCatalog cat, int depth = 0, HashSet<string>? seen = null)
    {
        seen ??= new(); var lines = new List<string>();
        if (depth > 8 || !seen.Add(good)) return lines;
        var pad = new string(' ', depth * 2);
        var name = cat.Goods.GetValueOrDefault(good)?.Name ?? good;
        var lim = Limits.Where(l => cat.Processes[l.Process].Outputs.ContainsKey(good) && l.Actual < l.Planned * 0.98).ToList();
        if (Shortages.TryGetValue(good, out var s)) lines.Add($"{pad}{name}: short {s:0.#}");
        else if (lim.Count > 0) lines.Add($"{pad}{name}: output reduced");
        foreach (var l in lim.GroupBy(x => x.Reason).Select(g => g.First()))
        {
            lines.Add($"{pad}  {l.Process} at {l.Site}: {l.Actual:0.#}/{l.Planned:0.#} batches · {l.Reason}");
            if (l.Reason.StartsWith("input ")) lines.AddRange(Explain(l.Reason[6..].Split(' ')[0], cat, depth + 1, seen));
        }
        if (!cat.Producers(good).Any()) lines.Add($"{pad}  (import only)");
        else if (lines.Count <= 1 && Shortages.ContainsKey(good) && !Limits.Any(l => cat.Processes[l.Process].Outputs.ContainsKey(good)))
            lines.Add($"{pad}  no domestic capacity (or technology) for {name}");
        return lines;
    }
}

/// <summary>Things outside the economy the solver needs to know this month.</summary>
public sealed class MonthContext
{
    /// <summary>Delivery factor 0..1 for imports from a supplier to a nation (trade routes, control points, distance).</summary>
    public Func<string, string, double> SupplierReach { get; init; } = (_, _) => 1;
}

/// <summary>
/// Monthly national production. Generic rules only: demand is planned backwards through the goods graph
/// (stocks, then domestic capacity, then imports by policy); production then runs forwards, each site limited by
/// capacity, condition, damage, labour, energy, tooling, deposits and the inputs that actually arrived in its
/// connected part of the country. A missing link anywhere propagates downstream by itself.
/// </summary>
public sealed class EconomySolver
{
    private readonly EconomyCatalog _cat;
    private readonly EconomyGeo _geo;
    private readonly BillResolver _bills;
    private readonly Catalog _eng;
    private readonly IReadOnlyDictionary<string, DesignDef> _designs;
    private readonly Dictionary<string, int> _depth = new();
    private readonly List<string> _upstreamFirst;
    public EconomySolver(EconomyCatalog cat, EconomyGeo geo, Catalog eng, IReadOnlyDictionary<string, DesignDef> designs)
    {
        _cat = cat; _geo = geo; _eng = eng; _bills = new BillResolver(cat, eng); _designs = designs;
        foreach (var g in cat.Goods.Keys) Depth(g, new HashSet<string>());
        _upstreamFirst = cat.Goods.Keys.OrderBy(g => _depth[g]).ThenBy(g => g, StringComparer.Ordinal).ToList();
    }

    public BillResolver Bills => _bills;
    public bool HasDesign(string id) => _designs.ContainsKey(id);
    public int GoodDepth(string g) => _depth.GetValueOrDefault(g);

    /// <summary>Topological depth over every producing process (not just the primary), so a good is always planned
    /// after everything that consumes it (e.g. electricity before the gas its stations burn).</summary>
    private int Depth(string g, HashSet<string> path)
    {
        if (_depth.TryGetValue(g, out var d)) return d;
        if (!path.Add(g)) return 0;
        var v = 0;
        foreach (var p in _cat.Producers(g))
            foreach (var i in p.Inputs.Keys)
                if (!path.Contains(i)) v = Math.Max(v, 1 + Depth(i, path));
        path.Remove(g);
        return _depth[g] = v;
    }

    // ---------------------------------------------------------------- stock pools per connected component
    private sealed class Pools
    {
        private readonly EconomyState _st;
        public readonly Dictionary<string, int> CompOf = new();
        public readonly Dictionary<int, List<string>> Regions = new();
        public Pools(EconomyState st, IEnumerable<string> regions, IEnumerable<RegionLink> links)
        {
            _st = st;
            var parent = new Dictionary<string, string>();
            string Find(string x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            foreach (var r in regions) parent[r] = r;
            foreach (var l in links)
                if (parent.ContainsKey(l.A) && parent.ContainsKey(l.B) && !st.CutLinks.Contains(l.Id) && !st.IsolatedRegions.Contains(l.A) && !st.IsolatedRegions.Contains(l.B))
                    parent[Find(l.A)] = Find(l.B);
            var ids = new Dictionary<string, int>();
            foreach (var r in parent.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var root = Find(r);
                if (!ids.TryGetValue(root, out var id)) ids[root] = id = ids.Count;
                CompOf[r] = id;
                (Regions.TryGetValue(id, out var l) ? l : Regions[id] = new()).Add(r);
            }
        }
        public double Have(int comp, string good) => Regions[comp].Sum(r => _st.Stock(r).GetValueOrDefault(good));
        public double HaveAll(string good) => Regions.Keys.Sum(c => Have(c, good));
        /// <summary>Take up to qty from the component, proportionally from its regions. Returns what was taken.</summary>
        public double Take(int comp, string good, double qty)
        {
            if (qty <= 0) return 0;
            var have = Have(comp, good);
            if (have <= 1e-9) return 0;
            var f = Math.Min(1, qty / have);
            foreach (var r in Regions[comp]) { var s = _st.Stock(r); if (s.TryGetValue(good, out var v)) s[good] = v * (1 - f); }
            return have * f;
        }
        /// <summary>Take from every component, largest first (national demand).</summary>
        public double TakeAll(string good, double qty)
        {
            var got = 0.0;
            foreach (var c in Regions.Keys.OrderByDescending(c => Have(c, good))) { if (got >= qty) break; got += Take(c, good, qty - got); }
            return got;
        }
        public void Put(string region, string good, double qty) { if (qty > 0) { var s = _st.Stock(region); s[good] = s.GetValueOrDefault(good) + qty; } }
    }

    private sealed record Demand(string Kind, string Owner, Dictionary<string, double> Goods, int Priority, int? Comp = null);

    public NationReport RunMonth(EconomyState st, string nation, MonthContext? ctx = null)
    {
        ctx ??= new MonthContext();
        var ne = st.Nations[nation];
        var rep = new NationReport { Nation = nation, Month = st.Month };
        var regionIds = _geo.Regions.Values.Where(r => r.Nation == nation).Select(r => r.Id).ToList();
        var pools = new Pools(st, regionIds, _geo.Links);
        var sites = st.Sites.Values.Where(s => s.Nation == nation && s.Active && pools.CompOf.ContainsKey(s.Region)).OrderBy(s => s.Id, StringComparer.Ordinal).ToList();

        // ---- labour: pools calibrated at start (see Calibrate), demand from staffed sites
        var labourDemand = new Dictionary<string, double>();
        foreach (var s in sites) foreach (var (k, v) in _cat.Facilities[s.Type].Labour) labourDemand[k] = labourDemand.GetValueOrDefault(k) + v * s.Level;
        foreach (var (k, d) in labourDemand) rep.LabourFactor[k] = Math.Min(1, ne.Labour.GetValueOrDefault(k, d) / Math.Max(1e-9, d));
        double SiteLabour(Site s) => _cat.Facilities[s.Type].Labour.Keys.Select(k => rep.LabourFactor.GetValueOrDefault(k, 1)).DefaultIfEmpty(1).Min();

        // ---- demands, by priority
        var demands = new List<Demand>();
        foreach (var p in ne.Programmes.OrderBy(p => p.Priority))
        {
            if (!_designs.TryGetValue(p.Design, out var d)) continue;
            var (bg, _) = _bills.DesignBill(d);
            demands.Add(new Demand("programme", p.Id, bg.ToDictionary(k => k.Key, k => k.Value * p.RatePerMonth), p.Priority));
        }
        foreach (var s in sites.Where(s => _cat.Facilities[s.Type].Spares.Count > 0))
            demands.Add(new Demand("maintenance", s.Id, _cat.Facilities[s.Type].Spares.ToDictionary(k => k.Key, k => k.Value * s.Level), 2, pools.CompOf[s.Region]));
        // fielded forces: spares by frame kind, per unit
        foreach (var (design, f) in ne.Fleet)
        {
            if (f.Count <= 0 || !_designs.TryGetValue(design, out var dd) || !_eng.Frames.TryGetValue(dd.Frame, out var fr) || !_cat.Bills.Frames.TryGetValue(fr.Kind, out var fb)) continue;
            var per = _geo.Rules.FleetSpares.GetValueOrDefault(fr.Kind);
            if (per > 0) demands.Add(new Demand("fleet", design, new() { [fb.Spares] = per * f.Count }, 2));
        }
        foreach (var c in ne.Construction)
            if (c.MonthsLeft > 0) demands.Add(new Demand("construction", c.Id, c.Remaining.ToDictionary(k => k.Key, k => k.Value / Math.Max(1, c.MonthsLeft)), 2));
        var basket = _geo.Rules.CivilianBasket.ToDictionary(k => k.Key, k => k.Value * ne.Population);
        demands.Add(new Demand("civilian", nation, basket, 3));
        demands.Add(new Demand("industry", nation, _geo.Seeding.IndustrialBasket.ToDictionary(k => k.Key, k => k.Value * ne.BaseGdp / 12 * ne.GrowthIndex), 4));
        foreach (var (g, months) in ne.StockTargets)
        {
            var monthly = ne.LastConsumption.GetValueOrDefault(g);
            var gap = monthly * months - pools.HaveAll(g);
            if (gap > 0) demands.Add(new Demand("reserve", g, new() { [g] = gap }, 5));
        }

        // ---- plan: backwards through the graph (downstream first)
        var req = new Dictionary<string, double>();
        foreach (var d in demands) foreach (var (g, q) in d.Goods) req[g] = req.GetValueOrDefault(g) + q;
        var avail = _cat.Goods.Keys.ToDictionary(g => g, pools.HaveAll);
        var capLeft = sites.Where(s => _cat.Facilities[s.Type].Capacity > 0).ToDictionary(s => s.Id, s => _cat.Facilities[s.Type].Capacity * s.Level * s.Health * SiteLabour(s));
        var plan = new List<(Site Site, ProcessDef Proc, double Batches)>();
        var byType = sites.GroupBy(s => s.Type).ToDictionary(g => g.Key, g => g.ToList());
        var importPlan = new Dictionary<string, double>();
        // Several passes: a good whose demand grew after it was planned (electricity, used by nearly every process,
        // and the fuel its stations burn) gets topped up in the next pass.
        var plannedOut = new Dictionary<string, double>();   // planned domestic output + planned imports so far
        for (var pass = 0; pass < 4; pass++)
        {
            var changed = false;
            foreach (var g in _upstreamFirst.AsEnumerable().Reverse())
            {
                var r0 = req.GetValueOrDefault(g);
                // proportional control toward the working cover (months of use): close a quarter of the gap each month
                var target = _cat.Goods[g].Storable ? Math.Max(0, r0 + 0.25 * (r0 * ne.StockTargets.GetValueOrDefault(g, ne.CoverMonths) - avail[g])) : r0 - avail[g];
                var net = target - plannedOut.GetValueOrDefault(g);
                rep.Plan[g] = (r0, avail[g], target);
                if (net <= Math.Max(1e-9, target * 1e-4)) continue;
                changed = true;
                var policy = ne.SourcePolicy.GetValueOrDefault(g, "auto");
                if (policy == "import")
                {
                    var take = Math.Min(net, ImportCapacity(nation, g, st, ctx) - importPlan.GetValueOrDefault(g));
                    if (take > 0) { importPlan[g] = importPlan.GetValueOrDefault(g) + take; plannedOut[g] = plannedOut.GetValueOrDefault(g) + take; net -= take; }
                }
                // every eligible producer shares the demand in proportion to its usable capacity (keeps the energy mix, spreads load)
                var options = new List<(ProcessDef P, List<Site> Sites, double Usable)>();
                foreach (var p in _cat.Producers(g))
                {
                    if (p.Tech.Any(t => ne.Tech.GetValueOrDefault(t.Key) < t.Value)) continue;
                    var cands = (byType.TryGetValue(p.Facility, out var bt) ? bt : new()).Where(x => capLeft.GetValueOrDefault(x.Id) > 1e-9).ToList();
                    if (p.Deposit != null) cands = cands.Where(x => (x.Resource == "" || x.Resource == p.Deposit) && DepositReserve(st, x.Region, p.Deposit) > 0).ToList();
                    if (p.Tooling != null) cands = cands.Where(x => pools.Have(pools.CompOf[x.Region], p.Tooling) > 0).ToList();
                    if (cands.Count == 0) continue;
                    // rely only on what plants realistically deliver (last month's yield); never ask for more than needed
                    var usable = cands.Sum(x => capLeft[x.Id]) * Math.Max(0.05, ne.Yield.GetValueOrDefault(p.Id, 1)) * p.Outputs[g];
                    if (usable > 0) options.Add((p, cands, usable));
                }
                var totalUsable = options.Sum(o => o.Usable);
                var fromDomestic = Math.Min(net, totalUsable);
                foreach (var (p, cands, usable) in options)
                {
                    var batches = fromDomestic * usable / totalUsable / p.Outputs[g];
                    var possible = cands.Sum(x => capLeft[x.Id]);
                    foreach (var x in cands)
                    {
                        var bx = batches * capLeft[x.Id] / possible;
                        capLeft[x.Id] -= bx;
                        plan.Add((x, p, bx));
                    }
                    foreach (var (i, q) in Inputs(p)) req[i] = req.GetValueOrDefault(i) + q * batches;
                    foreach (var (o, q) in p.Outputs) if (o != g) avail[o] = avail.GetValueOrDefault(o) + q * batches; // by-products
                }
                plannedOut[g] = plannedOut.GetValueOrDefault(g) + fromDomestic;
                net -= fromDomestic;
                if (net > 1e-9 && policy != "domestic")
                {
                    var imp = Math.Min(net, ImportCapacity(nation, g, st, ctx) - importPlan.GetValueOrDefault(g));
                    if (imp > 0) { importPlan[g] = importPlan.GetValueOrDefault(g) + imp; plannedOut[g] = plannedOut.GetValueOrDefault(g) + imp; net -= imp; }
                }
                if (net > 1e-9) plannedOut[g] = plannedOut.GetValueOrDefault(g) + net; // unmet: don't retry it every pass
            }
            if (!changed) break;
        }

        // ---- imports arrive at the ports (or the capital when landlocked)
        var portRegion = sites.Where(s => s.Type == "port").Select(s => s.Region).FirstOrDefault()
                         ?? _geo.Regions.Values.First(r => r.Nation == nation && r.Capital).Id;
        foreach (var (g, want) in importPlan)
        {
            var got = 0.0;
            foreach (var sp in _geo.Suppliers.Values.Where(sp => sp.Exports.ContainsKey(g)).OrderByDescending(sp => ctx.SupplierReach(sp.Id, nation)).ThenBy(sp => sp.Id, StringComparer.Ordinal))
            {
                if (got >= want) break;
                if (Blocked(st, sp.Id, nation, g)) continue;
                var reach = ctx.SupplierReach(sp.Id, nation);
                got += Math.Min(want - got, sp.Exports[g] * reach);
            }
            // contracts with other nations draw on the seller's own stock (and only what it can spare beyond a month of its use)
            foreach (var c in ne.Contracts.Where(c => c.Active && c.Good == g && st.Nations.ContainsKey(c.From) && !Blocked(st, c.From, nation, g)))
            {
                var spare = SellerSpare(st, c.From, g);
                var q = Math.Min(Math.Min(c.PerMonth, spare), want - got) * ctx.SupplierReach(c.From, nation);
                if (q > 0) { TakeFromNation(st, c.From, g, q); got += q; Count(rep.Imported, "contract:" + c.From, q); }
            }
            // automatic spot trade with other nations: any unsanctioned nation sells what it can spare
            foreach (var other in st.Nations.Keys.Where(o => o != nation && !Blocked(st, o, nation, g)).OrderByDescending(o => ctx.SupplierReach(o, nation)).ThenBy(o => o, StringComparer.Ordinal))
            {
                if (got >= want) break;
                var q = Math.Min(SellerSpare(st, other, g), want - got) * ctx.SupplierReach(other, nation);
                if (q > 1e-6) { TakeFromNation(st, other, g, q); got += q; Count(rep.Imported, "from:" + other, q); }
            }
            var fromSuppliers = Math.Min(got, want);
            got = Math.Min(want, got + MarketDepth(nation, g, st, ctx));
            rep.ImportCost += (got - fromSuppliers) * _cat.Goods[g].Price * (_geo.Rules.WorldMarket.PriceMarkup - 1); // open-market premium
            pools.Put(portRegion, g, got);
            Count(rep.Imported, g, got);
            rep.ImportCost += got * _cat.Goods[g].Price;
        }

        // ---- execute: upstream first, each site limited by what actually reached it
        // power stations first (everything else needs their output this month), then upstream to downstream
        var yieldAcc = new Dictionary<string, (double Planned, double Actual)>();
        var byDepth = plan.OrderBy(x => _cat.Facilities[x.Proc.Facility].SuppliesEnergy > 0 ? -1 : x.Proc.Outputs.Keys.Min(GoodDepth)).ThenBy(x => x.Site.Id, StringComparer.Ordinal).ToList();
        foreach (var (s, p, planned) in byDepth)
        {
            var comp = pools.CompOf[s.Region];
            var frac = 1.0; var reason = "";
            void Limit(double f, string why) { if (f < frac - 1e-9) { frac = Math.Max(0, f); reason = why; } }
            foreach (var (i, q) in Inputs(p))
            {
                var need = q * planned; if (need <= 0) continue;
                var have = pools.Have(comp, i);
                var sub = p.Substitutes.FirstOrDefault(x => x.Good == i);
                if (sub != null && have < need) have += pools.Have(comp, sub.Alt) / sub.Ratio;
                Limit(have / need, $"input {i} short");
            }
            if (p.Deposit != null) Limit(DepositReserve(st, s.Region, p.Deposit) / (p.Outputs.Values.Sum() * planned), "deposit exhausted");
            var actual = planned * frac;
            var penalty = 0.0;
            foreach (var (i, q) in Inputs(p))
            {
                var need = q * actual;
                var got = pools.Take(comp, i, need);
                var sub = p.Substitutes.FirstOrDefault(x => x.Good == i);
                if (sub != null && got < need)
                {
                    var alt = pools.Take(comp, sub.Alt, (need - got) * sub.Ratio);
                    Count(rep.Consumed, sub.Alt, alt);
                    penalty = Math.Max(penalty, sub.Penalty * (alt / sub.Ratio) / need);
                }
                Count(rep.Consumed, i, got);
            }
            rep.EnergyDemand += p.Energy * planned;
            foreach (var (o, q) in p.Outputs) { var made = q * actual * (1 - penalty); pools.Put(s.Region, o, made); Count(rep.Produced, o, made); if (o == Electricity) rep.EnergySupply += made; }
            if (p.Deposit != null) DrawDeposit(st, s.Region, p.Deposit, p.Outputs.Values.Sum() * actual);
            rep.ValueAdded += actual * (p.Outputs.Sum(o => o.Value * _cat.Goods[o.Key].Price) * (1 - penalty) - p.Inputs.Sum(i => i.Value * _cat.Goods[i.Key].Price));
            rep.Limits.Add(new ProcessLimit(p.Id, s.Id, planned, actual, frac < 0.98 ? reason : penalty > 0 ? "substitute used" : ""));
            var (pl, ac) = yieldAcc.GetValueOrDefault(p.Id); yieldAcc[p.Id] = (pl + planned, ac + actual);
        }
        foreach (var (pid, (pl, ac)) in yieldAcc) ne.Yield[pid] = pl <= 1e-9 ? 1 : Math.Clamp(0.5 * ne.Yield.GetValueOrDefault(pid, 1) + 0.5 * ac / pl, 0, 1);
        foreach (var l in rep.Limits.Where(l => l.Reason != "" && l.Reason != "substitute used").GroupBy(l => (l.Process, l.Reason)).OrderByDescending(g => g.Sum(x => x.Planned - x.Actual)).Take(12))
            rep.Bottlenecks.Add($"{l.Key.Process}: {l.Sum(x => x.Actual):0.#}/{l.Sum(x => x.Planned):0.#} batches · {l.Key.Reason}");

        // ---- consume for the demands, by priority
        var maintenanceNeed = 0.0; var maintenanceGot = 0.0; var basketNeed = 0.0; var basketGot = 0.0; var econNeed = 0.0; var econGot = 0.0;
        foreach (var d in demands.OrderBy(d => d.Priority))
        {
            if (d.Kind == "programme")
            {
                var prog = ne.Programmes.First(p => p.Id == d.Owner);
                var dd = _designs[prog.Design];
                var (_, asm) = _bills.DesignBill(dd);
                var units = prog.RatePerMonth; var limit = "";
                foreach (var (g, q) in d.Goods) { var f = pools.HaveAll(g) / Math.Max(1e-9, q) * prog.RatePerMonth; if (f < units) { units = f; limit = $"{_cat.Goods[g].Name} short"; } }
                foreach (var (f, b) in asm)
                {
                    var cap = sites.Where(s => s.Type == f).Sum(s => _cat.Facilities[f].Capacity * s.Level * s.Health * SiteLabour(s));
                    if (cap / Math.Max(1e-9, b) < units) { units = cap / Math.Max(1e-9, b); limit = cap <= 0 ? $"no {_cat.Facilities[f].Name}" : $"{_cat.Facilities[f].Name} capacity"; }
                }
                // defence funds: a unit costs its bill at market prices plus assembly
                var unitCost = d.Goods.Sum(k => k.Value / prog.RatePerMonth * _cat.Goods[k.Key].Price) * 1.25 / 5000; // game bn: a destroyer ~2.6, a bomber ~2.1, a missile ~0.03
                if (unitCost > 0 && ne.DefenceFunds < units * unitCost) { units = Math.Max(0, ne.DefenceFunds / unitCost); limit = "defence budget"; }
                units = Math.Max(0, units);
                ne.DefenceFunds -= units * unitCost; rep.DefenceSpent += units * unitCost;
                foreach (var (g, q) in d.Goods) Count(rep.Consumed, g, pools.TakeAll(g, q / prog.RatePerMonth * units));
                prog.Built += units;
                if (units > 0)
                {
                    var fe = ne.Fleet.TryGetValue(prog.Design, out var x) ? x : ne.Fleet[prog.Design] = new FleetEntry();
                    fe.Readiness = (fe.Readiness * fe.Count + units) / (fe.Count + units); // new units arrive ready
                    fe.Count += units;
                } prog.LastMonth = units; prog.Status = units >= prog.RatePerMonth * 0.98 ? "on rate" : limit;
                rep.Programmes.Add(new ProgrammeStatus(prog.Id, prog.Design, prog.RatePerMonth, units, units >= prog.RatePerMonth * 0.98 ? "" : limit));
                continue;
            }
            var need = 0.0; var got = 0.0;
            foreach (var (g, q) in d.Goods)
            {
                var took = d.Comp is { } c ? pools.Take(c, g, q) : pools.TakeAll(g, q);
                Count(rep.Consumed, g, took);
                need += q * _cat.Goods[g].Price; got += took * _cat.Goods[g].Price;
                if (q - took > 1e-6 && d.Kind != "reserve") Count(rep.Shortages, g, q - took);
            }
            var frac = need <= 0 ? 1 : got / need;
            if (d.Kind == "maintenance")
            {
                maintenanceNeed += need; maintenanceGot += got;
                var s = st.Sites[d.Owner];
                s.Condition = Math.Clamp(s.Condition - _cat.Facilities[s.Type].Decay + _geo.Rules.Maintenance.RestorePerMonth * frac, _geo.Rules.Maintenance.ReadinessFloor, 1);
            }
            else if (d.Kind == "civilian") { basketNeed += need; basketGot += got; }
            if (d.Kind is "civilian" or "industry") { econNeed += need; econGot += got; }
            else if (d.Kind == "construction") ProgressConstruction(st, ne, d.Owner, frac);
            else if (d.Kind == "fleet" && ne.Fleet.TryGetValue(d.Owner, out var fl))
                fl.Readiness = Math.Clamp(fl.Readiness - _geo.Rules.FleetDecay + _geo.Rules.Maintenance.RestorePerMonth * frac, _geo.Rules.Maintenance.ReadinessFloor, 1);
            if (d.Kind is "civilian" or "industry") rep.ValueAdded += got * 0.3; // final consumption carries retail and service value
        }
        foreach (var s in sites.Where(s => _cat.Facilities[s.Type].Spares.Count == 0)) s.Condition = Math.Max(_geo.Rules.Maintenance.ReadinessFloor, s.Condition - _cat.Facilities[s.Type].Decay * 0.5);
        rep.MaintenanceMet = maintenanceNeed <= 0 ? 1 : maintenanceGot / maintenanceNeed;
        rep.BasketSatisfaction = basketNeed <= 0 ? 1 : basketGot / basketNeed;
        rep.ShortfallLoss = econNeed <= 0 ? 0 : 1 - econGot / econNeed;

        // non-storable goods don't carry over
        foreach (var r in regionIds) foreach (var g in _cat.Goods.Values.Where(g => !g.Storable)) st.Stock(r).Remove(g.Id);
        ne.LastConsumption = new Dictionary<string, double>(rep.Consumed);
        return rep;
    }

    public const string Electricity = "electricity";
    private readonly Dictionary<string, Dictionary<string, double>> _inputs = new();
    /// <summary>A process's inputs with its energy as an electricity input (one rule for every process).</summary>
    public Dictionary<string, double> Inputs(ProcessDef p)
    {
        if (_inputs.TryGetValue(p.Id, out var d)) return d;
        d = new Dictionary<string, double>(p.Inputs);
        if (p.Energy > 0 && !p.Outputs.ContainsKey(Electricity)) d[Electricity] = d.GetValueOrDefault(Electricity) + p.Energy;
        return _inputs[p.Id] = d;
    }

    private void ProgressConstruction(EconomyState st, NationEconomy ne, string id, double frac)
    {
        var c = ne.Construction.FirstOrDefault(x => x.Id == id);
        if (c == null) return;
        foreach (var k in c.Remaining.Keys.ToList()) c.Remaining[k] = Math.Max(0, c.Remaining[k] - c.Remaining[k] / Math.Max(1, c.MonthsLeft) * frac);
        c.MonthsLeft -= frac;
        if (!st.Sites.TryGetValue(c.Site, out var s)) { ne.Construction.Remove(c); return; }
        if (c.AddLevels == 0) s.BuildLeft = Math.Max(0, c.MonthsLeft);
        if (c.MonthsLeft <= 1e-6)
        {
            if (c.AddLevels > 0) s.Level += c.AddLevels;
            ne.Construction.Remove(c);
        }
    }

    public const string WorldMarket = "world";
    private bool Blocked(EconomyState st, string seller, string buyer, string good) =>
        st.Sanctioned(seller, buyer, good, _geo.Rules.WorldMarket.Restricted, _geo.Rules.GlobalEnforcers);
    /// <summary>What the open world market will sell a nation of one good this month (0 for restricted goods or under a general embargo).</summary>
    private double MarketDepth(string nation, string good, EconomyState st, MonthContext ctx)
    {
        var wm = _geo.Rules.WorldMarket;
        if (wm.Restricted.Contains(good) || Blocked(st, WorldMarket, nation, good) || !_cat.Goods[good].Storable) return 0;
        return wm.DepthPerGdp * st.Nations[nation].BaseGdp * 1000 / Math.Max(0.1, _cat.Goods[good].Price) / 12 * ctx.SupplierReach(WorldMarket, nation);
    }

    private double ImportCapacity(string nation, string good, EconomyState st, MonthContext ctx) =>
        MarketDepth(nation, good, st, ctx) + _geo.Suppliers.Values.Where(sp => sp.Exports.ContainsKey(good) && !Blocked(st, sp.Id, nation, good)).Sum(sp => sp.Exports[good] * ctx.SupplierReach(sp.Id, nation))
        + st.Nations[nation].Contracts.Where(c => c.Active && c.Good == good && st.Nations.ContainsKey(c.From) && !Blocked(st, c.From, nation, good))
            .Sum(c => Math.Min(c.PerMonth, SellerSpare(st, c.From, good)) * ctx.SupplierReach(c.From, nation))
        + st.Nations.Keys.Where(o => o != nation && !Blocked(st, o, nation, good)).Sum(o => SellerSpare(st, o, good) * ctx.SupplierReach(o, nation));

    private IEnumerable<string> RegionsOf(string nation) => _geo.Regions.Values.Where(r => r.Nation == nation).Select(r => r.Id);
    /// <summary>What a seller nation can spare of a good: stock beyond one month of its own last consumption.</summary>
    private double SellerSpare(EconomyState st, string seller, string good) =>
        Math.Max(0, RegionsOf(seller).Sum(r => st.Stock(r).GetValueOrDefault(good)) - st.Nations[seller].LastConsumption.GetValueOrDefault(good));
    private void TakeFromNation(EconomyState st, string seller, string good, double qty)
    {
        var regs = RegionsOf(seller).ToList();
        var have = regs.Sum(r => st.Stock(r).GetValueOrDefault(good));
        if (have <= 0) return;
        var f = Math.Min(1, qty / have);
        foreach (var r in regs) if (st.Stock(r).TryGetValue(good, out var v)) st.Stock(r)[good] = v * (1 - f);
    }

    private Dictionary<(string, string), List<DepositDef>>? _depIndex;
    private List<DepositDef> Deps(string region, string resource)
    {
        _depIndex ??= _geo.Deposits.Values.GroupBy(d => (d.Region, d.Resource)).ToDictionary(g => g.Key, g => g.ToList());
        return _depIndex.TryGetValue((region, resource), out var l) ? l : new();
    }
    private double DepositReserve(EconomyState st, string region, string resource) => Deps(region, resource).Sum(d => st.Reserves.GetValueOrDefault(d.Id));

    private void DrawDeposit(EconomyState st, string region, string resource, double qty)
    {
        foreach (var d in Deps(region, resource))
        {
            var r = st.Reserves.GetValueOrDefault(d.Id);
            var take = Math.Min(r, qty); st.Reserves[d.Id] = r - take; qty -= take;
            if (qty <= 0) break;
        }
    }

    private static void Count(Dictionary<string, double> d, string k, double v) { if (v > 0) d[k] = d.GetValueOrDefault(k) + v; }
}
