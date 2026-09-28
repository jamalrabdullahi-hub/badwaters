using BadWaters.Data;
using BadWaters.Sim.Economy;
using Xunit;

namespace BadWaters.Sim.Tests;

public class EconomySolverTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static EconomySolver Solver => new(C.Economy, C.Geo, C.Catalog, C.Designs);
    private static EconomyState Seed(string nation, int year = 2020)
    {
        var st = new EconomyState();
        new EconomySeeder(C.Economy, C.Geo, C.Catalog, C.Designs).Seed(st, nation, year);
        return st;
    }
    private static NationReport Run(EconomyState st, string nation, int months)
    {
        var s = Solver; NationReport r = null!;
        for (var m = 0; m < months; m++) { r = s.RunMonth(st, nation); st.Month++; }
        return r;
    }

    [Fact]
    public void Seeded_economies_run_stable_for_two_years()
    {
        foreach (var n in new[] { "JPN", "IRN", "DEU" })
        {
            var st = Seed(n);
            var r = Run(st, n, 24);
            Assert.True(r.BasketSatisfaction > 0.9, $"{n} basket {r.BasketSatisfaction:P0}");
            Assert.True(r.MaintenanceMet > 0.9, $"{n} maintenance {r.MaintenanceMet:P0}");
        }
    }

    [Fact]
    public void Destroying_the_specialty_refineries_stalls_radar_modules_downstream()
    {
        var st = Seed("JPN");
        st.Nations["JPN"].Programmes.Add(new Programme { Id = "radar", Design = "gs-radar", RatePerMonth = 2 });
        st.Embargoes.Add("world>JPN");                            // no open-market escape hatch
        foreach (var s in C.Geo.Suppliers.Keys) st.Embargoes.Add($"{s}>JPN");
        var before = Run(st, "JPN", 6).Programmes.Single();
        foreach (var s in st.Sites.Values.Where(s => s.Nation == "JPN" && s.Type == "specialty-refinery")) s.Damage = 1;
        foreach (var r in st.RegionStock.Values) { r.Remove("gallium-metal"); r.Remove("rf-modules"); r.Remove("gallium-crude"); }
        var after = Run(st, "JPN", 3);
        var prog = after.Programmes.Single();
        Assert.True(prog.Built < before.Built * 0.5, $"built {prog.Built} vs {before.Built}");
        Assert.Contains(after.Explain("rf-modules", C.Economy).Concat(after.Explain("gallium-metal", C.Economy)), l => l.Contains("gallium"));
    }

    [Fact]
    public void Stock_in_an_isolated_region_cannot_be_used()
    {
        var st = Seed("GBR");
        var nsea = "GBR-nsea";
        // move all crude into the North Sea region, then cut it off
        foreach (var r in st.RegionStock.Keys.ToList()) { var q = st.Stock(r).GetValueOrDefault("crude-oil"); st.Stock(r)["crude-oil"] = 0; st.Stock(nsea)["crude-oil"] = st.Stock(nsea).GetValueOrDefault("crude-oil") + q; }
        st.IsolatedRegions.Add(nsea);
        st.Embargoes.Add("*>GBR");
        st.Embargoes.Add("world>GBR");
        var r1 = Run(st, "GBR", 1);
        // refineries outside the cut-off region have no crude to work with
        Assert.True(r1.Limits.Where(l => l.Process == "refine-crude" && !l.Site.StartsWith(nsea)).Sum(l => l.Actual) < 1e-6);
        Assert.True(st.Stock(nsea)["crude-oil"] > 0); // still there, just unreachable
    }

    [Fact]
    public void Tungsten_ore_without_refining_is_not_tungsten_alloy()
    {
        var st = Seed("CHN");
        st.Nations["CHN"].Programmes.Add(new Programme { Id = "sam", Design = "sam-interceptor", RatePerMonth = 200 });
        foreach (var s in C.Geo.Suppliers.Keys) st.Embargoes.Add($"{s}>CHN");
        st.Embargoes.Add("world>CHN");
        var with = Run(st, "CHN", 3);
        Assert.True(with.Produced.GetValueOrDefault("tungsten-heavy-alloy") > 0);
        foreach (var s in st.Sites.Values.Where(s => s.Nation == "CHN" && s.Type == "specialty-refinery")) s.Damage = 1;
        foreach (var r in st.RegionStock.Values) { r.Remove("apt"); r.Remove("tungsten-powder"); r.Remove("tungsten-heavy-alloy"); }
        var without = Run(st, "CHN", 2);
        Assert.True(st.Reserves.Where(k => k.Key.EndsWith("-tungsten")).Sum(k => k.Value) > 0); // the ore is still in the ground
        Assert.True(without.Produced.GetValueOrDefault("tungsten-heavy-alloy") < 1e-6);
        Assert.NotEqual("", without.Programmes.Single().Limit);
    }
}
