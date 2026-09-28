using BadWaters.Sim.Engineering;

namespace BadWaters.Sim.Economy;

/// <summary>
/// Builds a nation's starting economy from data: the civilian basket (population), a general industrial basket (GDP)
/// and a baseline defence load (sector tiers × representative designs) are resolved through the goods graph; each
/// facility type is then sized to that demand × the nation's competence in its sector (tier 0 = none: it imports).
/// Extraction is limited to the nation's deposits. Sites are game entities placed inside regions (never real plants).
/// </summary>
public sealed class EconomySeeder
{
    private readonly EconomyCatalog _cat;
    private readonly EconomyGeo _geo;
    private readonly BillResolver _bills;
    private readonly IReadOnlyDictionary<string, DesignDef> _designs;
    public EconomySeeder(EconomyCatalog cat, EconomyGeo geo, Catalog eng, IReadOnlyDictionary<string, DesignDef> designs)
    { _cat = cat; _geo = geo; _bills = new BillResolver(cat, eng); _designs = designs; }

    public static Dictionary<string, int> TechFromSectors(IReadOnlyDictionary<string, int> s)
    {
        int T(string k, int mul = 2) => Math.Clamp(s.GetValueOrDefault(k) * mul, 1, 10);
        return new()
        {
            ["materials"] = Math.Max(T("specialty"), T("metals")), ["structures"] = T("heavy"), ["manufacturing"] = Math.Max(T("electronics"), T("semis")),
            ["computing"] = T("semis"), ["radar"] = Math.Max(T("electronics"), T("aero")), ["ir-systems"] = T("electronics"), ["jet-engines"] = T("aero"),
            ["flight-control"] = T("aero"), ["power-systems"] = T("heavy"), ["payloads"] = T("munitions"), ["gas-turbines"] = T("naval"),
            ["rocket-motors"] = T("munitions"), ["sonar"] = T("naval"), ["navigation"] = T("electronics"), ["datalinks"] = T("electronics"),
        };
    }

    /// <summary>Monthly civilian basket for a population (millions).</summary>
    public Dictionary<string, double> CivilianBasket(double populationM) =>
        _geo.Rules.CivilianBasket.ToDictionary(k => k.Key, k => k.Value * populationM);

    /// <summary>Everything the nation needs each month at the start (civilian + industrial + baseline defence).</summary>
    public (Dictionary<string, double> Goods, Dictionary<string, double> Assembly) BaselineDemand(NationEconomyDef n)
    {
        var goods = CivilianBasket(n.Population);
        var asm = new Dictionary<string, double>();
        void Add(Dictionary<string, double> d, string k, double v) => d[k] = d.GetValueOrDefault(k) + v;
        foreach (var (g, perGdp) in _geo.Seeding.IndustrialBasket) Add(goods, g, perGdp * n.Gdp / 12);
        foreach (var (sector, sd) in _geo.Seeding.SectorDesigns)
        {
            var (design, perTier) = (sd.Design, sd.PerTier);
            var tier = n.Sectors.GetValueOrDefault(sector);
            if (tier <= 0 || !_designs.TryGetValue(design, out var d)) continue;
            var (bg, ba) = _bills.DesignBill(d);
            var rate = perTier * tier;
            foreach (var (g, q) in bg) Add(goods, g, q * rate);
            foreach (var (f, b) in ba) Add(asm, f, b * rate);
        }
        return (goods, asm);
    }

    public void Seed(EconomyState st, string nation, int year)
    {
        var n = _geo.For(nation, year) ?? throw new ArgumentException($"No economy for {nation}");
        var regions = _geo.Regions.Values.Where(r => r.Nation == nation).OrderByDescending(r => r.Industry).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        if (regions.Count == 0) return;
        var capital = regions.FirstOrDefault(r => r.Capital) ?? regions[0];
        var tech = TechFromSectors(n.Sectors);
        st.Nations[nation] = new NationEconomy
        {
            Nation = nation, Population = n.Population, Gdp = n.Gdp, BaseGdp = n.Gdp, DefenceShare = n.DefenceShare, Tech = tech, CoverMonths = n.StockpileMonths,
            GdpHistory = Enumerable.Repeat(n.Gdp, 12).ToList(),
        };
        foreach (var d in _geo.Deposits.Values.Where(d => regions.Any(r => r.Id == d.Region))) st.Reserves[d.Id] = d.Reserve;

        var (goods, asm) = BaselineDemand(n);
        var req = _bills.Flatten(goods, asm, true);
        // later passes add the spares the previous pass's sites will need, then size again from scratch (converges in ~3)
        var baseGoods = goods;
        for (var pass = 0; pass < 3; pass++)
        {
            if (pass > 0)
            {
                var spares = new Dictionary<string, double>(baseGoods);
                foreach (var s0 in st.Sites.Values.Where(x => x.Nation == nation).ToList())
                    foreach (var (g, q) in _cat.Facilities[s0.Type].Spares) spares[g] = spares.GetValueOrDefault(g) + q * s0.Level;
                foreach (var id in st.Sites.Values.Where(x => x.Nation == nation).Select(x => x.Id).ToList()) st.Sites.Remove(id);
                // power stations' fuel by the energy mix, so the fuel chains are sized to what the stations burn
                foreach (var (g, q) in PowerFuel(n, req.Goods.GetValueOrDefault("electricity") * 1.35)) spares[g] = spares.GetValueOrDefault(g) + q;
                goods = spares;
                req = _bills.Flatten(goods, asm, true);
            }
            SeedSites(st, nation, n, regions, capital, req, goods);
        }
        SeedStocks(st, nation, n, regions, capital, req, goods);
    }

    /// <summary>Fuel the nation's power stations burn per month for an electricity output, split by its energy mix.</summary>
    public Dictionary<string, double> PowerFuel(NationEconomyDef n, double electricity)
    {
        var fuel = new Dictionary<string, double>();
        foreach (var (mix, share) in n.EnergyMix)
        {
            if (!_geo.Seeding.EnergyMix.TryGetValue(mix, out var type)) continue;
            var gen = _cat.Processes.Values.FirstOrDefault(p => p.Facility == type);
            if (gen == null) continue;
            var batches = electricity * share / gen.Outputs.GetValueOrDefault("electricity", 10);
            foreach (var (g, q) in gen.Inputs) fuel[g] = fuel.GetValueOrDefault(g) + q * batches;
        }
        return fuel;
    }

    private void SeedSites(EconomyState st, string nation, NationEconomyDef n, List<RegionDef> regions, RegionDef capital, Requirements req, Dictionary<string, double> goods)
    {
        var sectorOf = _geo.Seeding.SectorFacilities.SelectMany(kv => kv.Value.Select(f => (f, kv.Key))).GroupBy(x => x.f).ToDictionary(g => g.Key, g => g.First().Key);
        var rng = new Random(StableHash(nation) ^ n.Year);
        var count = st.Sites.Count;
        void Place(string type, RegionDef r, int level, string resource = "")
        {
            while (level > 0)
            {
                var l = Math.Min(level, 8);
                var j = _geo.Seeding.JitterDeg;
                st.Sites[$"{r.Id}-{type}-{++count}"] = new Site
                {
                    Id = $"{r.Id}-{type}-{count}", Type = type, Nation = nation, Region = r.Id, Level = l, Resource = resource,
                    Lon = Math.Round(r.Lon + (rng.NextDouble() - 0.5) * 2 * j, 3), Lat = Math.Round(r.Lat + (rng.NextDouble() - 0.5) * 2 * j * 0.7, 3),
                };
                level -= l;
            }
        }
        int TierFor(string type) => sectorOf.TryGetValue(type, out var sec) ? n.Sectors.GetValueOrDefault(sec) : n.Sectors.GetValueOrDefault("heavy");

        // extraction: by resource, split across the regions that hold it, sized to demand and capped by deposit size
        foreach (var proc in _cat.Processes.Values.Where(p => p.Deposit != null))
        {
            var fac = _cat.Facilities[proc.Facility];
            var need = req.Resources.GetValueOrDefault(proc.Deposit!) / proc.Outputs.Values.Sum();
            var deps = _geo.Deposits.Values.Where(d => d.Resource == proc.Deposit && regions.Any(r => r.Id == d.Region)).ToList();
            var total = deps.Sum(d => d.Size);
            foreach (var d in deps)
            {
                var share = need * d.Size / Math.Max(1, total);
                var level = (int)Math.Ceiling(Math.Min(share / fac.Capacity, d.Size * 8.0));
                if (level > 0) Place(proc.Facility, _geo.Regions[d.Region], level, proc.Deposit!);
            }
        }
        // processing and assembly: demand × competence
        var coastal = regions.Where(r => r.Coastal).ToList();
        foreach (var (type, batches) in req.FacilityBatches.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (!_cat.Facilities.TryGetValue(type, out var fac) || fac.Capacity <= 0 || fac.SuppliesEnergy > 0) continue;
            if (_cat.Processes.Values.Any(p => p.Facility == type && p.Deposit != null) && !_cat.Processes.Values.Any(p => p.Facility == type && p.Deposit == null)) continue; // pure extraction
            var tier = TierFor(type);
            var basic = _geo.Seeding.Basic.Contains(type);
            if (!basic && (tier <= 0 || tier < _geo.Seeding.MinTier.GetValueOrDefault(type))) continue;
            var factor = Math.Min(1.5, tier / 3.0);
            if (basic) factor = Math.Max(1.1, factor);
            var levels = (int)Math.Ceiling(batches / fac.Capacity * factor);
            var pool = _geo.Seeding.RequiresCoast.Contains(type) ? coastal : regions;
            if (levels <= 0 || pool.Count == 0) continue;
            var spread = Math.Clamp(levels / 6 + 1, 1, pool.Count);
            var start = StableHash(type) % pool.Count;
            for (var k = 0; k < spread; k++) Place(type, pool[(start + k) % pool.Count], levels / spread + (k < levels % spread ? 1 : 0));
        }
        foreach (var (sector, extras) in _geo.Seeding.TopTierExtras)
            if (n.Sectors.GetValueOrDefault(sector) >= 5) foreach (var t in extras) Place(t, capital, 1);
        // fixed infrastructure
        foreach (var r in regions)
        {
            foreach (var (t, l) in _geo.Seeding.Always) Place(t, r, l);
            if (r.Coastal) foreach (var (t, l) in _geo.Seeding.Coastal) Place(t, r, l);
            if (r.Industry >= _geo.Seeding.UniversityMinIndustry) Place("university", r, Math.Max(1, r.Industry - 2));
        }
        foreach (var (t, l) in _geo.Seeding.Capital) Place(t, capital, l);
        // energy: plant levels by mix to cover industrial and civilian electricity
        var power = req.Goods.GetValueOrDefault("electricity");
        foreach (var (mix, share) in n.EnergyMix)
        {
            if (!_geo.Seeding.EnergyMix.TryGetValue(mix, out var type) || !_cat.Facilities.TryGetValue(type, out var fac) || fac.SuppliesEnergy <= 0) continue;
            var levels = (int)Math.Ceiling(power * share * 1.35 / fac.SuppliesEnergy);
            var spread = Math.Clamp(levels / 8 + 1, 1, regions.Count);
            for (var k = 0; k < spread; k++) Place(type, regions[k % regions.Count], levels / spread + (k < levels % spread ? 1 : 0));
        }
    }

    private void SeedStocks(EconomyState st, string nation, NationEconomyDef n, List<RegionDef> regions, RegionDef capital, Requirements req, Dictionary<string, double> goods)
    {
        // starting stocks: months of every good the nation consumes, held in the capital and industrial regions
        var months = n.StockpileMonths;
        foreach (var (g, q) in req.Goods)
        {
            if (!_cat.Goods.TryGetValue(g, out var gs) || !gs.Storable) continue;
            var per = q * months / Math.Min(3, regions.Count);
            for (var k = 0; k < Math.Min(3, regions.Count); k++) st.Stock(regions[k].Id)[g] = st.Stock(regions[k].Id).GetValueOrDefault(g) + per;
        }
        foreach (var (g, q) in goods) if (_cat.Goods.TryGetValue(g, out var gs) && gs.Storable) st.Stock(capital.Id)[g] = st.Stock(capital.Id).GetValueOrDefault(g) + q * months;
        // labour: pools with a working margin over the seeded staffing (universities and academies grow the skilled pools later)
        var ne = st.Nations[nation];
        foreach (var s in st.Sites.Values.Where(s => s.Nation == nation))
            foreach (var (k, v) in _cat.Facilities[s.Type].Labour) ne.Labour[k] = ne.Labour.GetValueOrDefault(k) + v * s.Level * 1.15;
        // tooling on hand where it is used
        foreach (var t in req.Tooling) foreach (var r in regions.Take(3)) st.Stock(r.Id)[t] = st.Stock(r.Id).GetValueOrDefault(t) + 5;
    }

    public static int StableHash(string s) { unchecked { var h = 23; foreach (var c in s) h = h * 31 + c; return Math.Abs(h); } }
}
