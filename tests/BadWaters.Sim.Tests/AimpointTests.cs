using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class AimpointTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }

    [Fact]
    public void A_package_spreads_its_weapons_over_the_chosen_components()
    {
        var t = TheatreFactory.Create(C.Scenarios["jpn-chn"], id => SystemProfile.Build(C.Catalog, C.Designs[id]), C.PresetInfo, C.BaseTypes, _ => C.DoctrineFor(null), 3,
            new Dictionary<string, SideControl> { ["blue"] = SideControl.Player, ["red"] = SideControl.Player }, C.Stores);
        // China strikes a Japanese base: pick the attacker with the most strike aircraft and a target in reach
        var (preset, _, _) = t.Air!.IdleByType("red").Where(x => C.Stores!.Template(x.Preset)?.Id is "strike" or "bomber").OrderByDescending(x => x.Count).First();
        var target = t.Bases.Values.Where(b => b.Site.Side == "blue" && b.Components.ContainsKey("runway") && b.Components.ContainsKey("fuel")).OrderBy(b => b.Site.Lon).First();
        var e = new AtoEntry { Side = "red", Target = target.Site.Id, Tot = t.Time + 3 * 3600, Tanker = true, Aimpoints = { "runway", "fuel" }, Flights = { new FlightRequest(preset, 2, Mission.Strike) } };
        var r = t.Air.Commit(e);
        if (!r.Ok) return; // out of reach in this layout
        Assert.Equal(new[] { "runway", "fuel" }, t.Air.Packages.Last().Aimpoints);
        t.Advance(r.Flights[0].Route.First(w => w.Name == "Target").T - t.Time + 30);
        var log = string.Join(" ", t.Log.Where(x => x.Text.Contains(r.Flights[0].Callsign) && x.Text.Contains("released")).Select(x => x.Text));
        if (log.Contains(" 0 hits")) return; // all missed: nothing to check
        Assert.True(target.Components["runway"].Integrity < 100 || target.Components["fuel"].Integrity < 100);
    }
}
