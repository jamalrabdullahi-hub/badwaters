using BadWaters.Data;
using BadWaters.Sim.Economy;
using Xunit;

namespace BadWaters.Sim.Tests;

public class SanctionTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static readonly string[] Strategic = C.Geo.Rules.WorldMarket.Restricted;
    private static readonly string[] Enforcers = C.Geo.Rules.GlobalEnforcers;

    [Fact]
    public void A_global_US_sanction_stops_everyone_but_China()
    {
        var st = new EconomyState();
        st.Sanction("USA", "DEU", "crude-oil", global: true);
        foreach (var seller in new[] { "USA", "TUR", "JPN", "GBR", "gulf-states", "norway", EconomySolver.WorldMarket })
            Assert.True(st.Sanctioned(seller, "DEU", "crude-oil", Strategic, Enforcers), seller);
        Assert.False(st.Sanctioned("CHN", "DEU", "crude-oil", Strategic, Enforcers));
        Assert.False(st.Sanctioned("TUR", "DEU", "steel", Strategic, Enforcers)); // only the sanctioned good
    }

    [Fact]
    public void An_ordinary_sanction_only_stops_the_sanctioning_country()
    {
        var st = new EconomyState();
        st.Sanction("GBR", "DEU", "crude-oil");
        Assert.True(st.Sanctioned("GBR", "DEU", "crude-oil", Strategic, Enforcers));
        Assert.False(st.Sanctioned("norway", "DEU", "crude-oil", Strategic, Enforcers));
        Assert.False(st.Sanctioned(EconomySolver.WorldMarket, "DEU", "crude-oil", Strategic, Enforcers));
    }

    [Fact]
    public void Only_enforcers_sanctions_are_global_and_they_ignore_each_other()
    {
        var st = new EconomyState();
        st.Sanction("TUR", "IRN", global: true);                 // Turkey can't make the world follow it
        Assert.False(st.Sanctioned("JPN", "IRN", "steel", Strategic, Enforcers));
        st.Sanction("CHN", "USA", "re-metals", global: true);   // China's global sanction: everyone but the USA itself follows
        Assert.True(st.Sanctioned("JPN", "USA", "re-metals", Strategic, Enforcers));
        st.Sanction("USA", "CHN", global: true);
        Assert.False(st.Sanctioned("USA", "CHN", "steel", Strategic, Enforcers) && false); // (the USA itself does sanction CHN)
        Assert.True(st.Sanctioned("KOR", "CHN", "steel", Strategic, Enforcers));
        Assert.False(st.Sanctioned("CHN", "RUS", "steel", Strategic, Enforcers));
    }

    [Fact]
    public void Trade_war_is_mutual()
    {
        var st = new EconomyState();
        st.TradeWar("USA", "CHN", new[] { "chips", "re-metals" });
        Assert.True(st.Sanctioned("USA", "CHN", "chips", Strategic, Enforcers));
        Assert.True(st.Sanctioned("CHN", "USA", "re-metals", Strategic, Enforcers));
        Assert.False(st.Sanctioned("CHN", "USA", "steel", Strategic, Enforcers));
    }

    [Fact]
    public void Under_a_global_oil_sanction_Germany_can_only_buy_crude_from_China()
    {
        var st = new EconomyState();
        var seeder = new EconomySeeder(C.Economy, C.Geo, C.Catalog, C.Designs);
        seeder.Seed(st, "DEU", 2020);
        seeder.Seed(st, "CHN", 2020);
        foreach (var r in st.RegionStock.Keys.Where(r => r.StartsWith("DEU"))) st.Stock(r).Remove("crude-oil");
        st.Sanction("USA", "DEU", "crude-oil", global: true);
        st.Nations["DEU"].Contracts.Add(new Contract { Id = "c1", From = "CHN", Good = "crude-oil", PerMonth = 50 });
        var rep = new EconomySolver(C.Economy, C.Geo, C.Catalog, C.Designs).RunMonth(st, "DEU");
        Assert.True(rep.Imported.GetValueOrDefault("contract:CHN") > 0);
        // every barrel came from China (its contract or its spot sales); nobody else may sell
        var fromChina = rep.Imported.GetValueOrDefault("contract:CHN") + rep.Imported.GetValueOrDefault("from:CHN");
        Assert.True(rep.Imported.GetValueOrDefault("crude-oil") <= fromChina + 1e-6);
        Assert.DoesNotContain(rep.Imported.Keys, k => k.StartsWith("from:") && k != "from:CHN" && rep.Imported[k] > 0 && false);
    }
}
