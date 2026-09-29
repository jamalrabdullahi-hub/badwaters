using BadWaters.Data;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class EmconTests
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
    public void Silent_batteries_never_detect_missiles()
    {
        var t = Setup();
        foreach (var b in t.Batteries.Where(b => b.Def.Side == "blue")) t.SetEmitting(b.Def.Id, false, b.Def.Name);
        var l = t.Launchers.First(x => x.Def.Side == "red" && !x.IsShip);
        var target = t.Scenario.Sites.First(s => s.Side == "blue" && Policy.TargetPolicy.IsLegitimate(s.Class) && Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, s.Lon, s.Lat) <= l.ReachKm);
        Assert.True(t.Launch(l.Def.Id, target.Id, 2).Ok);
        t.Advance(900);
        Assert.DoesNotContain(t.Tracks, x => x.Side == "red" && x.Detected);
    }

    [Fact]
    public void Emitting_ships_are_heard_further_than_silent_ones()
    {
        int Heard(bool silent)
        {
            var t = Setup();
            if (silent)
            {
                foreach (var u in t.Scenario.Units.Where(u => u.Side == "red" && u.Kind == "ship")) t.SetEmitting(u.Id, false, u.Name);
                foreach (var b in t.Batteries.Where(b => b.Def.Side == "red" && b.IsShip)) t.SetEmitting(b.Def.Id, false, b.Def.Name);
            }
            for (var i = 0; i < 10; i++) { t.Advance(60); t.Intel!.Tick(t.Time); }
            return t.Intel!.Pictures["blue"].Contacts.Values.Count(c => c.Kind == "ship" && c.Source == "passive (ESM)");
        }
        Assert.Equal(0, Heard(silent: true));
        Assert.True(Heard(silent: false) > 0);
    }
}
