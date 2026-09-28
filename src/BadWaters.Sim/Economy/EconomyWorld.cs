using BadWaters.Sim.Engineering;
using BadWaters.Sim.Trade;

namespace BadWaters.Sim.Economy;

/// <summary>Default sanctions for a start date (data/core/economy/trade.json).</summary>
public sealed record TradeRegime(string Id, int Year, List<Sanction> Sanctions);

/// <summary>
/// How well goods reach a buyer from a seller: the best open sea lane (or land corridor) that passes near both, using
/// the trade routes' live disruption (wars, shut control points). Sellers with no modelled lane still trade, slower.
/// </summary>
public sealed class TradeReach
{
    public const double NearKm = 1500, Unmodelled = 0.75;
    private readonly EconomyGeo _geo;
    private readonly IReadOnlyDictionary<string, RouteDef> _routes;
    private readonly Dictionary<(string, string), List<string>> _lanes = new();
    private readonly Dictionary<string, List<RouteDef>> _touching = new();
    public TradeReach(EconomyGeo geo, IReadOnlyDictionary<string, RouteDef> routes) { _geo = geo; _routes = routes; }

    private IEnumerable<(double Lon, double Lat)> Points(string party)
    {
        if (_geo.Suppliers.TryGetValue(party, out var sp)) return new[] { (sp.Lon, sp.Lat) };
        var regs = _geo.Regions.Values.Where(r => r.Nation == party).ToList();
        var coast = regs.Where(r => r.Coastal).ToList();
        return (coast.Count > 0 ? coast : regs).Select(r => (r.Lon, r.Lat));
    }

    /// <summary>Routes (ids) that pass near both parties.</summary>
    public IReadOnlyList<string> Lanes(string seller, string buyer)
    {
        if (_lanes.TryGetValue((seller, buyer), out var l)) return l;
        var a = Points(seller).ToList(); var b = Points(buyer).ToList();
        bool Near(RouteDef r, List<(double Lon, double Lat)> pts) => r.Path.Any(p => pts.Any(q => Km(p[0], p[1], q.Lon, q.Lat) < NearKm));
        return _lanes[(seller, buyer)] = _routes.Values.Where(r => Near(r, a) && Near(r, b)).Select(r => r.Id).ToList();
    }

    public double Reach(string seller, string buyer, IReadOnlyDictionary<string, RouteState> states)
    {
        if (seller == buyer) return 1;
        if (seller == EconomySolver.WorldMarket)
        {
            // the open market arrives over whatever lanes touch the buyer: their average condition
            if (!_touching.TryGetValue(buyer, out var touching))
                _touching[buyer] = touching = _routes.Values.Where(r => r.Path.Any(p => Points(buyer).Any(q => Km(p[0], p[1], q.Lon, q.Lat) < NearKm))).ToList();
            return touching.Count == 0 ? Unmodelled : touching.Average(r => states.TryGetValue(r.Id, out var s) ? s.DeliveryFactor : 1);
        }
        var lanes = Lanes(seller, buyer);
        if (lanes.Count == 0) return Unmodelled;
        return lanes.Max(id => states.TryGetValue(id, out var s) ? s.DeliveryFactor : 1);
    }

    public static double Km(double lon1, double lat1, double lon2, double lat2)
    {
        double R = 6371, dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}

/// <summary>Runs the whole world economy: seeds every nation, applies the start date's sanctions, and each month runs
/// every nation's solver with trade reach from the live trade routes, then the macro rules.</summary>
public sealed class EconomyWorld
{
    public EconomyCatalog Catalog { get; }
    public EconomyGeo Geo { get; }
    public EconomySolver Solver { get; }
    public EconomySeeder Seeder { get; }
    public TradeReach Reach { get; }
    public MacroRules Macro { get; }
    public Dictionary<string, NationReport> LastReports { get; } = new();

    public EconomyWorld(EconomyCatalog cat, EconomyGeo geo, Catalog eng, IReadOnlyDictionary<string, DesignDef> designs, IReadOnlyDictionary<string, RouteDef> routes)
    {
        Catalog = cat; Geo = geo;
        Solver = new EconomySolver(cat, geo, eng, designs);
        Seeder = new EconomySeeder(cat, geo, eng, designs);
        Reach = new TradeReach(geo, routes);
        Macro = new MacroRules(geo.Rules);
    }

    public void Start(EconomyState st, IEnumerable<string> nations, int year, IEnumerable<TradeRegime> regimes)
    {
        foreach (var n in nations.Where(n => Geo.Regions.Values.Any(r => r.Nation == n))) Seeder.Seed(st, n, year);
        var regime = regimes.OrderBy(r => Math.Abs(r.Year - year)).FirstOrDefault();
        if (regime != null) foreach (var x in regime.Sanctions) st.Sanction(x.By, x.Target, x.Good, x.Global && Geo.Rules.GlobalEnforcers.Contains(x.By));
        PreRoll(st, PreRollMonths);
    }

    public const int PreRollMonths = 24;

    /// <summary>
    /// Run the economy silently until stocks, yields and flows settle, then calibrate every nation's GDP to its start
    /// figure and reset the history, so play starts from a steady state rather than a seeded guess.
    /// </summary>
    public void PreRoll(EconomyState st, int months)
    {
        var open = new Dictionary<string, RouteState>();
        var vas = st.Nations.Keys.ToDictionary(k => k, _ => new List<double>());
        var ctx = new MonthContext { SupplierReach = (s, b) => Reach.Reach(s, b, open) };
        for (var m = 0; m < months; m++)
            foreach (var n in st.Nations.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var ne = st.Nations[n];
                var funds = ne.DefenceFunds;
                vas[n].Add(Solver.RunMonth(st, n, ctx).ValueAdded);
                ne.DefenceFunds = funds;
            }
        foreach (var (n, ne) in st.Nations)
        {
            var va = vas[n].TakeLast(12).DefaultIfEmpty(1).Average();
            ne.GdpScale = ne.BaseGdp / Math.Max(1e-9, va * 12);
            ne.Calibrated = true;
            ne.Gdp = ne.BaseGdp;
            ne.GdpHistory = Enumerable.Repeat(ne.BaseGdp, 13).ToList();
            ne.VaHistory.Clear();
            ne.Stability = 65; ne.GrowthIndex = 1;
            foreach (var s in st.Sites.Values.Where(s => s.Nation == n)) s.Condition = Math.Max(s.Condition, 0.9);
        }
        st.Month = 0;
    }

    /// <summary>One strategic month for every nation. Returns crisis events raised by the macro rules.</summary>
    public List<string> RunMonth(EconomyState st, IReadOnlyDictionary<string, RouteState> routes)
    {
        var ctx = new MonthContext { SupplierReach = (seller, buyer) => Reach.Reach(seller, buyer, routes) };
        var events = new List<string>();
        foreach (var n in st.Nations.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList())
        {
            var rep = Solver.RunMonth(st, n, ctx);
            LastReports[n] = rep;
            var e = Macro.Apply(st, st.Nations[n], rep);
            if (e != null) events.Add(e);
        }
        st.Month++;
        return events;
    }
}
