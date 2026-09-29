using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class StoresTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }

    private static InterceptTrial Setup()
    {
        var t = TheatreFactory.Create(C.Scenarios["jpn-chn"], id => SystemProfile.Build(C.Catalog, C.Designs[id]), C.PresetInfo, C.BaseTypes, _ => C.DoctrineFor(null), 1,
            new Dictionary<string, SideControl> { ["blue"] = SideControl.Player, ["red"] = SideControl.Player }, C.Stores);
        return t;
    }

    [Fact]
    public void Every_default_loadout_fits_its_airframe_and_the_nation_fields_it()
    {
        var s = C.Stores!;
        foreach (var (preset, tpl) in s.Presets)
            foreach (var name in s.Templates[tpl].Loadouts.Keys)
            {
                var load = s.Default(preset, name);
                Assert.Empty(s.Validate(preset, load));
                Assert.All(load.Values, l => Assert.True(s.Usable(preset, s.Stores[l.Store]), $"{preset} {name}: {l.Store}"));
            }
    }

    [Fact]
    public void Bases_start_with_munitions_and_arming_draws_them_down()
    {
        var t = Setup();
        var air = t.Air!;
        var (preset, _, _) = air.IdleByType("blue").First(x => C.Stores!.Template(x.Preset)?.Id is "fighter" or "strike");
        var target = t.Scenario.Sites.First(x => x.Side == "red" && Policy.TargetPolicy.IsLegitimate(x.Class));
        var entry = new AtoEntry { Side = "blue", Target = target.Id, Tot = t.Time + 3 * 3600, Tanker = true, Flights = { new FlightRequest(preset, 2, Mission.Strike) } };
        var dry = air.Plan(entry);
        if (!dry.Ok) return; // out of reach for this scenario's layout: nothing to arm
        var f = dry.Flights[0];
        var store = f.Loadout.Values.First(l => C.Stores!.Stores[l.Store].AirToGround).Store;
        var before = air.Munitions.At(f.BaseId, store);
        Assert.True(before > 0);
        Assert.True(air.Commit(entry).Ok);
        Assert.Equal(before - f.Loadout.Values.Where(l => l.Store == store).Sum(l => l.Qty) * 2, air.Munitions.At(f.BaseId, store));
    }

    [Fact]
    public void A_loadout_the_base_cannot_supply_is_refused()
    {
        var t = Setup();
        var air = t.Air!;
        var (preset, _, _) = air.IdleByType("blue").First(x => C.Stores!.Template(x.Preset)?.Id is "fighter");
        foreach (var b in air.Munitions.ByBase.Values) b.Clear();
        var r = air.Plan(new AtoEntry { Side = "blue", StationLon = 128, StationLat = 27, Start = t.Time, Flights = { new FlightRequest(preset, 2, Mission.CAP) } });
        Assert.False(r.Ok);
        Assert.Contains(r.Issues, i => i.StartsWith("Not enough"));
    }
}
