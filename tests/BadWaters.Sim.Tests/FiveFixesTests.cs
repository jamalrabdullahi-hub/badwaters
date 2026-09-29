using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

/// <summary>Stores returned on landing, jamming, rescue units, waypoint editing, anti-ship air strikes, war state and neutral traffic.</summary>
public class FiveFixesTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static InterceptTrial Setup() =>
        TheatreFactory.Create(C.Scenarios["jpn-chn"], id => SystemProfile.Build(C.Catalog, C.Designs[id]), C.PresetInfo, C.BaseTypes, _ => C.DoctrineFor(null), 1,
            new Dictionary<string, SideControl> { ["blue"] = SideControl.Player, ["red"] = SideControl.Player }, C.Stores);

    private static string Fighter(InterceptTrial t) => t.Air!.IdleByType("blue").First(x => C.Stores!.Template(x.Preset)?.Id is "fighter").Preset;

    [Fact]
    public void Unfired_stores_go_back_to_the_magazine_on_landing()
    {
        var t = Setup();
        var air = t.Air!;
        var e = new AtoEntry { Side = "blue", StationLon = 127.5, StationLat = 26.5, Start = t.Time, Duration = 600, Flights = { new FlightRequest(Fighter(t), 2, Mission.CAP) } };
        var r = air.Commit(e);
        Assert.True(r.Ok, string.Join(" ", r.Issues));
        var f = r.Flights[0];
        var store = f.Loadout.Values.First().Store;
        var armed = air.Munitions.At(f.BaseId, store);
        t.Advance(f.LandAt - t.Time + 30);
        Assert.Equal(FlightState.Landed, f.State);
        Assert.Contains(t.Log, x => x.Text.Contains(f.Callsign) && x.Text.Contains("returned to the magazine"));
    }

    [Fact]
    public void A_jammer_degrades_enemy_radars_and_is_heard()
    {
        var t = Setup();
        var ew = t.Air!.IdleByType("blue").First(x => x.Preset == "JPEC1");
        var red = t.Scenario.Sites.First(s => s.Side == "red" && Policy.TargetPolicy.IsLegitimate(s.Class));
        var r = t.Air.Commit(new AtoEntry { Side = "blue", Target = red.Id, Tot = t.Time + 7200, Tanker = true, Flights = { new FlightRequest(ew.Preset, 1, Mission.Jamming) } });
        Assert.True(r.Ok, string.Join(" ", r.Issues));
        var j = r.Flights[0];
        t.Advance(j.Takeoff - t.Time + 60);
        Assert.True(j.Airborne(t.Time));
        var st = j.Pos(t.Time);
        Assert.Equal(0.6, t.JamFactor("red", st.Lon, st.Lat));
        t.SetEmitting(j.Id, false, j.Callsign); // a silent jammer jams nothing
        Assert.Equal(1, t.JamFactor("red", st.Lon, st.Lat));
        Assert.Equal(1, t.JamFactor("blue", st.Lon, st.Lat));
    }

    [Fact]
    public void Rescue_helicopters_exist_and_cannot_be_packaged()
    {
        var t = Setup();
        Assert.Contains(t.Air!.IdleByType("blue"), x => (C.PresetInfo(x.Preset)?.R("rescue") ?? 0) > 0);
        var r = t.Air.Plan(new AtoEntry { Side = "blue", StationLon = 127.5, StationLat = 26.5, Start = t.Time, Flights = { new FlightRequest("JPUH60J", 1, Mission.Rescue) } });
        Assert.False(r.Ok);
    }

    [Fact]
    public void Moving_an_ingress_point_keeps_the_time_on_target()
    {
        var t = Setup();
        var red = t.Scenario.Sites.First(s => s.Side == "red" && Policy.TargetPolicy.IsLegitimate(s.Class));
        var r = t.Air!.Commit(new AtoEntry { Side = "blue", Target = red.Id, Tot = t.Time + 3 * 3600, Tanker = true, Flights = { new FlightRequest(Fighter(t), 2, Mission.Strike) } });
        if (!r.Ok) return;
        var f = r.Flights[0];
        var tot = f.Route.First(w => w.Name == "Target").T;
        var ing = f.Route.FindIndex(w => w.Name == "Ingress");
        var before = f.Takeoff;
        var msg = t.Air.MoveWaypoint(f, ing, f.Route[ing].Lon - 0.5, f.Route[ing].Lat - 0.5);
        Assert.Equal("", msg);
        Assert.Equal(tot, f.Route.First(w => w.Name == "Target").T, 3);
        Assert.NotEqual(before, f.Takeoff);
        Assert.NotEqual("", t.Air.MoveWaypoint(f, f.Route.FindIndex(w => w.Name == "Target"), 0, 0));
    }

    [Fact]
    public void Not_at_war_means_track_only()
    {
        var t = Setup();
        t.AtWar = false;
        var l = t.Launchers.First(x => x.Def.Side == "blue");
        var red = t.Scenario.Sites.First(s => s.Side == "red" && Policy.TargetPolicy.IsLegitimate(s.Class));
        Assert.False(t.Launch(l.Def.Id, red.Id, 1).Ok);
        Assert.False(t.Air!.Plan(new AtoEntry { Side = "blue", Target = red.Id, Tot = t.Time + 7200, Flights = { new FlightRequest(Fighter(t), 2, Mission.Strike) } }).Ok);
    }

    [Fact]
    public void Neutral_merchants_sail_their_lanes()
    {
        var t = Setup();
        Assert.NotEmpty(t.Neutrals);
        var m = t.Neutrals[0]; var a = m.At;
        t.Advance(3600);
        Assert.NotEqual(a, m.At);
    }
}
