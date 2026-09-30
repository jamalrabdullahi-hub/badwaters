using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class WorldForcesTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static string Name(string id) => C.Nations.TryGetValue(id, out var n) ? n.Name : id;
    private static ScenarioDef W => C.WorldOob!;

    [Fact]
    public void The_world_holds_every_theatre_asset_once_keyed_by_nation()
    {
        Assert.NotNull(C.WorldOob);
        foreach (var list in new[] { W.Sites.Select(s => s.Id), W.Units.Select(u => u.Id), W.Batteries.Select(b => b.Id), W.Launchers.Select(l => l.Id) })
            Assert.Equal(list.Count(), list.Distinct().Count());
        Assert.Equal(new[] { "CHN", "IRN", "ISR", "JPN" }, W.Sites.Select(s => s.Side).Distinct().OrderBy(x => x));
        foreach (var sc in C.Scenarios.Values.Where(s => s.Theatre != ""))
            Assert.All(sc.Units, u => Assert.Contains(W.Units, x => x.Id == u.Id));
    }

    [Fact]
    public void A_war_fights_where_the_two_can_reach_each_other()
    {
        var z = FightZone.Build(W, "JPN", "CHN", "JPN", Name)!;
        Assert.Equal("Japan", z.Sides["blue"]);
        Assert.Equal("China", z.Sides["red"]);
        Assert.Contains(z.Sites, s => s.Id == "naha-ab" && s.Side == "blue");
        Assert.Contains(z.Sites, s => s.Side == "red" && s.Type == "airbase");
        Assert.Equal("CHN:JPN", z.Theatre);
        Assert.NotNull(FightZone.Build(W, "ISR", "IRN", null, Name));
        Assert.Null(FightZone.Build(W, "JPN", "ISR", "JPN", Name)); // nowhere near each other: no fight
        // protected places come along, and stay protected
        Assert.All(z.Sites.Where(s => s.Class == "population-centre"), s => Assert.False(Policy.TargetPolicy.IsLegitimate(s.Class)));
    }

    [Fact]
    public void Peacetime_home_view_shows_the_nearest_neighbour_within_reach()
    {
        var h = FightZone.Home(W, "JPN", Name)!;
        Assert.Equal("China", h.Sides["red"]);
        Assert.Contains(h.Sites, s => s.Side == "blue");
    }

    [Fact]
    public void Damage_persists_in_the_world_when_a_zone_is_rebuilt()
    {
        var w = new World { Now = new DateTime(2020, 1, 1), Player = "JPN" };
        InterceptTrial Build(ScenarioDef sc, int seed) => TheatreFactory.Create(sc, id => SystemProfile.Build(C.Catalog, C.Designs[id]), C.PresetInfo, C.BaseTypes, _ => C.DoctrineFor(null), seed, null, C.Stores, C.Logistics);
        var camp = new TheatreCampaign(k => { var (a, b) = FightZone.Parse(k); return FightZone.Build(W, a, b, "JPN", Name); }, Build, _ => null) { WorldForces = () => w.Forces };
        var key = FightZone.Key("JPN", "CHN");
        var t = camp.Get(key)!;
        t.Bases["naha-ab"].Components["runway"].Integrity = 12;
        var l = t.Launchers.First(x => x.Def.Side == "blue");
        l.Stock = 0;
        camp.WriteBack(w, t);
        camp.Live.Remove(key); // the zone is dropped; the world remembers
        var again = camp.Get(key)!;
        Assert.Equal(12, again.Bases["naha-ab"].Components["runway"].Integrity);
        Assert.Equal(0, again.Launchers.First(x => x.Def.Id == l.Def.Id).Stock);
        // a different war doesn't see Japan's losses, but a new JPN-CHN zone does
        Assert.Contains("naha-ab/runway", w.Forces.Components.Keys);
    }
}
