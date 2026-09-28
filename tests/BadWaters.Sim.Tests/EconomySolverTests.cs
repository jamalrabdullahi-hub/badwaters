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
        st.Nations["JPN"].DefenceFunds = 1e6; // solver-only test: fund the programme directly
        st.Sanction("CHN", "JPN", global: true); st.Sanction("USA", "JPN", global: true); // nobody left to sell to Japan
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
        st.Sanction("USA", "GBR", global: true); st.Sanction("CHN", "GBR", global: true);
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
        st.Sanction("USA", "CHN", global: true); // everyone but China itself stops selling to China
        var with = Run(st, "CHN", 3);
        Assert.True(with.Produced.GetValueOrDefault("tungsten-heavy-alloy") > 0);
        foreach (var s in st.Sites.Values.Where(s => s.Nation == "CHN" && s.Type == "specialty-refinery")) s.Damage = 1;
        foreach (var r in st.RegionStock.Values) { r.Remove("apt"); r.Remove("tungsten-powder"); r.Remove("tungsten-heavy-alloy"); }
        var without = Run(st, "CHN", 2);
        Assert.True(st.Reserves.Where(k => k.Key.EndsWith("-tungsten")).Sum(k => k.Value) > 0); // the ore is still in the ground
        Assert.True(without.Produced.GetValueOrDefault("tungsten-heavy-alloy") < 1e-6);
        Assert.NotEqual("", without.Programmes.Single().Limit);
    }

    [Fact]
    public void Construction_consumes_its_bill_and_brings_the_plant_online()
    {
        var st = Seed("DEU");
        var ew = new EconomyWorld(C.Economy, C.Geo, C.Catalog, C.Designs, C.Routes);
        var id = ew.OrderConstruction(st, "DEU", "steelworks", "DEU-bav", 2);
        var site = st.Sites.Values.Single(s => s.Type == "steelworks" && s.Id.Contains("new"));
        Assert.False(site.Active);
        var months = (int)C.Economy.Facilities["steelworks"].BuildMonths;
        var rep = Run(st, "DEU", 1);
        Assert.True(rep.Consumed.GetValueOrDefault("cement") > 0);
        Run(st, "DEU", months + 6);
        Assert.True(site.Active);
        Assert.DoesNotContain(st.Nations["DEU"].Construction, c => c.Id == id);
    }

    [Fact]
    public void Fleet_readiness_falls_without_spares_and_holds_with_them()
    {
        var st = Seed("JPN");
        st.Nations["JPN"].Fleet["ddg-2031"] = new FleetEntry { Count = 20 };
        Run(st, "JPN", 6);
        var held = st.Nations["JPN"].Fleet["ddg-2031"].Readiness;
        foreach (var r in st.RegionStock.Values) r.Remove("spares-naval");
        foreach (var x in st.Sites.Values.Where(x => x.Nation == "JPN" && x.Type == "heavy-engineering")) x.Damage = 1;
        st.Sanction("USA", "JPN", "spares-naval", true); st.Sanction("CHN", "JPN", "spares-naval", true);
        Run(st, "JPN", 8);
        Assert.True(held > 0.9, $"held {held}");
        Assert.True(st.Nations["JPN"].Fleet["ddg-2031"].Readiness < held - 0.1);
    }

    [Fact]
    public void A_theatre_strike_damages_the_facility_there_in_the_strategic_economy()
    {
        var st = Seed("CHN");
        var t = new BadWaters.Sim.Theatre.InterceptTrial(C.Scenarios["jpn-chn"], id => BadWaters.Sim.Theatre.SystemProfile.Build(C.Catalog, C.Designs[id]), 1, C.BaseTypes);
        string? hit = null;
        t.SiteDamaged += (site, frac) => hit = st.DamageNear("CHN", site.Lon, site.Lat, site.Class, frac, C.Economy, 600);
        var b = t.Bases.Values.First(x => x.Site.Side == "red");
        for (var i = 0; i < 5; i++) t.ApplyHit("blue", b, "", 9, 9, 0.5, "test");
        Assert.NotNull(hit);
        Assert.True(st.Sites[hit!].Damage > 0);
        Assert.True(BadWaters.Sim.Policy.TargetPolicy.IsLegitimate(C.Economy.Facilities[st.Sites[hit!].Type].TargetClass));
    }
}
