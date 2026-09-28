using BadWaters.Data;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class NavalTests
{
    private static readonly GameContent Content = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }

    private static InterceptTrial Setup()
    {
        var t = new InterceptTrial(Content.Scenarios["jpn-chn"], id => SystemProfile.Build(Content.Catalog, Content.Designs[id]), 1, Content.BaseTypes);
        t.Control["blue"] = SideControl.Player; t.Control["red"] = SideControl.Player;
        t.Naval = new NavalForces(t, Content.PresetInfo, 1);
        t.Intel = new IntelService(t, Content.PresetInfo, 1);
        t.Air = new AirTasking(t, Content.PresetInfo, 1);
        return t;
    }

    [Fact]
    public void Ships_move_and_carry_their_systems()
    {
        var t = Setup();
        var maya = t.Naval!.Find("maya-0")!;
        Assert.Contains(maya.Launchers, l => l.Def.Id == "maya-tlam");
        t.Naval.MoveTo("maya-0", maya.Lon + 1, maya.Lat);
        t.Advance(600);
        Assert.True(maya.Lon > 126.2 + 0.05);
        Assert.Equal(maya.Lon, maya.Batteries[0].Def.Lon, 6);
        Assert.Equal(maya.Lon, maya.Launchers[0].Def.Lon, 6);
    }

    [Fact]
    public void Anti_ship_fire_needs_a_fresh_contact()
    {
        var t = Setup();
        var r = t.LaunchAtShip("maya-tlam", "pla-ddg-1");
        Assert.False(r.Ok);
        Assert.Contains("contact", r.Message);
    }

    [Fact]
    public void Missiles_miss_a_ship_that_moved_out_of_the_seeker_basket()
    {
        var t = Setup();
        var target = t.Naval!.Find("pla-ddg-1")!;
        t.Intel!.Reveal("blue", target.Id, "ship", target.Lon, target.Lat, t.Time, "test");
        Assert.True(t.LaunchAtShip("maya-tlam", target.Id, 2).Ok);
        target.Lon += 2; target.Lat += 1; // ~220 km away from where it was seen
        t.Advance(2000);
        Assert.Contains(t.Log, l => l.Text.Contains("found nothing"));
        Assert.Equal(target.MaxHp, target.Hp);
    }

    [Fact]
    public void Sinking_a_ship_takes_its_air_defence_and_strike_cells_with_it()
    {
        var t = Setup();
        var maya = t.Naval!.Find("maya-0")!;
        t.Naval.Hit(maya, maya.MaxHp + 1, "test");
        Assert.True(maya.Sunk);
        Assert.All(maya.Batteries, b => Assert.Equal(0, b.Stock));
        Assert.All(maya.Launchers, l => Assert.Equal(0, l.Stock));
    }

    [Fact]
    public void Submarines_attack_ships_that_come_close()
    {
        var t = Setup();
        var sub = t.Naval!.Ships.First(s => s.Kind == "sub" && s.Side == "red");
        var prey = t.Naval.Ships.First(s => s.Kind == "ship" && s.Side == "blue");
        prey.Lon = sub.Lon + 0.05; prey.Lat = sub.Lat;
        t.Advance(1200);
        Assert.Contains(t.Log, l => l.Text.Contains("torpedo"));
    }
}
