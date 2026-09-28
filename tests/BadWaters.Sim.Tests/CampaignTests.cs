using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Economy;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class CampaignTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static string? Nat(string name) => C.Nations.Values.FirstOrDefault(x => x.Name == name || x.Id == name)?.Id;
    private static TheatreCampaign Campaign() => new(th => C.Scenarios.Values.FirstOrDefault(s => s.Theatre == th),
        (sc, seed) => TheatreFactory.Create(sc, id => SystemProfile.Build(C.Catalog, C.Designs[id]), C.PresetInfo, C.BaseTypes, side => C.DoctrineFor(Nat(sc.Sides.GetValueOrDefault(side, ""))), seed), Nat);
    private static World WorldWith(params string[] nations)
    {
        var w = new World { Now = new DateTime(2020, 1, 1) };
        foreach (var n in nations) w.Economy.Nations[n] = new NationEconomy { Nation = n };
        return w;
    }

    [Fact]
    public void An_auto_resolved_day_writes_losses_into_the_economy()
    {
        var camp = Campaign();
        var w = WorldWith("JPN", "CHN");
        Assert.Null(camp.Daily(w, new[] { "ECS" }));
        var t = camp.Live["ECS"];
        Assert.True(t.Time >= TheatreCampaign.Day - 1);
        var lostJapan = t.Air!.Destroyed.Count(id => t.Scenario.Units.First(u => u.Id == id).Side == "blue");
        Assert.True(lostJapan > 0);
        Assert.Equal(lostJapan, (int)w.Economy.Nations["JPN"].Fleet.Values.Sum(f => f.Lost));
    }

    [Fact]
    public void Launchers_are_resupplied_from_national_stocks()
    {
        var camp = Campaign();
        var w = WorldWith("JPN", "CHN");
        var t = camp.Get("ECS")!;
        var l = t.Launchers.First(x => x.Def.Side == "blue");
        l.Stock = 0;
        w.Economy.Nations["JPN"].Fleet[l.Def.Missile] = new FleetEntry { Count = 3 };
        camp.WriteBack(w, t);
        Assert.Equal(3, l.Stock);
        Assert.Equal(0, w.Economy.Nations["JPN"].Fleet[l.Def.Missile].Count);
    }

    [Fact]
    public void A_theatre_in_fight_mode_holds_the_strategic_turn()
    {
        var camp = Campaign();
        var w = WorldWith("JPN", "CHN");
        camp.Get("ECS");
        camp.Mode["ECS"] = TheatreMode.Fight;
        Assert.Equal("ECS", camp.Daily(w, new[] { "ECS" }));
        Assert.True(camp.Live["ECS"].Time < 60);
    }

    [Fact]
    public void A_theatre_survives_save_and_load()
    {
        var camp = Campaign();
        var w = WorldWith("JPN", "CHN");
        camp.Daily(w, new[] { "ECS" });
        var t = camp.Live["ECS"];
        var lost = t.Air!.Destroyed.OrderBy(x => x).ToList();
        var comps = t.Bases.Values.SelectMany(b => b.Components.Values).Sum(c => c.Integrity);
        camp.SaveInto(w);
        var back = World.Load(w.Save());
        var camp2 = Campaign();
        camp2.LoadFrom(back);
        var t2 = camp2.Live["ECS"];
        Assert.Equal(lost, t2.Air!.Destroyed.OrderBy(x => x).ToList());
        Assert.Equal(comps, t2.Bases.Values.SelectMany(b => b.Components.Values).Sum(c => c.Integrity), 3);
        Assert.Equal(t.Time, t2.Time, 3);
    }
}
