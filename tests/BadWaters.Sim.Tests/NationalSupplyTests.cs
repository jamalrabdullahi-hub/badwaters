using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Economy;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class NationalSupplyTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static string? Nat(string side) => side switch { "Japan" => "JPN", "China" => "CHN", _ => null };

    private static (EconomyState St, InterceptTrial T) Setup()
    {
        var ew = new EconomyWorld(C.Economy, C.Geo, C.Catalog, C.Designs, C.Routes);
        var st = new EconomyState();
        ew.Start(st, new[] { "JPN", "CHN" }, 2020, C.TradeRegimes);
        NationalMunitions.Seed(st, C.Stores, C.Scenarios.Values, Nat);
        var t = TheatreFactory.Create(C.Scenarios["jpn-chn"], id => SystemProfile.Build(C.Catalog, C.Designs[id]), C.PresetInfo, C.BaseTypes, _ => C.DoctrineFor(null), 1,
            new Dictionary<string, SideControl> { ["blue"] = SideControl.Player, ["red"] = SideControl.Player }, C.Stores, C.Logistics);
        return (st, t);
    }

    [Fact]
    public void Nations_only_stock_stores_they_field()
    {
        var (st, _) = Setup();
        Assert.True(NationalMunitions.Held(st.Nations["JPN"], "store:aam-4b") > 0);
        Assert.Equal(0, NationalMunitions.Held(st.Nations["JPN"], "store:pl-15"));
        Assert.True(NationalMunitions.Held(st.Nations["CHN"], "store:pl-15") > 0);
        Assert.True(C.Designs.ContainsKey("store:aim-120")); // producible through a programme
    }

    [Fact]
    public void A_theatre_holds_only_what_the_nation_has_and_takes_it()
    {
        var (st, t) = Setup();
        var ne = st.Nations["JPN"];
        var lg = t.Logistics!;
        var store = lg.Nodes.Values.Where(n => n.Side == "blue").SelectMany(lg.ItemsAt).First(i => i.StartsWith("store:"));
        ne.Fleet[store].Count = 3; // nearly none left at home
        lg.DrawFromNation("blue", ne, st, C.Geo);
        Assert.True(lg.Nodes.Values.Where(n => n.Side == "blue").Sum(n => lg.Get(n, store)) <= 3);
        Assert.Equal(0, ne.Fleet[store].Count);
    }

    [Fact]
    public void Depots_are_replenished_from_national_stocks()
    {
        var (st, t) = Setup();
        var ne = st.Nations["JPN"];
        var lg = t.Logistics!;
        var depot = lg.Nodes.Values.First(n => n.Side == "blue" && n.Reserve);
        lg.Add(depot, "avfuel", -lg.Get(depot, "avfuel"));
        var home = NationalMunitions.GoodHeld(st, C.Geo, "JPN", "jet-fuel");
        lg.Replenish("blue", ne, st, C.Geo);
        Assert.True(lg.Get(depot, "avfuel") > 0);
        Assert.True(NationalMunitions.GoodHeld(st, C.Geo, "JPN", "jet-fuel") < home);
    }
}
