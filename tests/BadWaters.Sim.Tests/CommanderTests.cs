using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class CommanderTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }

    private static InterceptTrial Theatre(int seed = 1, bool opening = false)
    {
        var sc = C.Scenarios["jpn-chn"];
        if (!opening) sc = sc with { Ato = new() };
        var t = TheatreFactory.Create(sc, id => SystemProfile.Build(C.Catalog, C.Designs[id]), C.PresetInfo, C.BaseTypes, _ => C.DoctrineFor(null), seed);
        t.Adaptive = true;
        return t;
    }

    [Fact]
    public void Commanders_generate_their_own_operations_with_reasons()
    {
        var t = Theatre();
        t.Advance(3 * 3600);
        foreach (var side in new[] { "blue", "red" })
        {
            var ops = t.Ops!.Ops.Where(o => o.Side == side && o.Author == Author.AI).ToList();
            Assert.NotEmpty(ops);
            Assert.All(ops, o => Assert.NotEqual("", o.Rationale));
            Assert.Contains(ops, o => o.Purpose.Contains("area-defence"));
        }
        Assert.Contains(t.Ops!.Ops, o => o.Purpose.Contains("strike") || o.Purpose.Contains("fires"));
    }

    [Fact]
    public void AI_targets_are_legitimate_and_known_to_the_side()
    {
        var t = Theatre();
        t.Advance(4 * 3600);
        foreach (var o in t.Ops!.Ops.Where(o => o.Author == Author.AI))
            foreach (var id in o.Targets)
            {
                var site = t.Site(id);
                Assert.NotNull(site);                      // strikes only on sites (pre-war survey), never on unseen units
                Assert.NotEqual(o.Side, site!.Side);
                Assert.True(TargetPolicy.IsLegitimate(site.Class), $"{o.Name} → {site.Class}");
            }
    }

    [Fact]
    public void Losing_runways_makes_the_commander_disperse_the_aircraft()
    {
        var t = Theatre();
        // knock out every blue runway but one: nothing scripted reacts to this
        var blueBases = t.Bases.Values.Where(b => b.Site.Side == "blue" && b.ByEffect("sorties") != null).OrderBy(b => b.Site.Id).ToList();
        foreach (var b in blueBases.Skip(1)) b.ByEffect("sorties")!.Integrity = 0;
        t.Advance(1800);
        var blue = t.Commanders["blue"];
        Assert.NotEmpty(blue.Last!.ClosedBases);
        Assert.True(t.Ops!.Ops.Any(o => o.Side == "blue" && o.Purpose.Contains("relocate")),
            $"closed {string.Join(",", blue.Last.ClosedBases)} · objectives {string.Join(" | ", blue.LastObjectives.Select(o => $"{o.Text} {o.Score:0}"))} · pool {blue.Last.PoolAircraft}");
    }

    [Fact]
    public void Same_seed_same_war()
    {
        string Run() { var t = Theatre(7); t.Advance(2 * 3600); return string.Join("|", t.Ops!.Ops.Select(o => $"{o.Name}:{o.Status}")); }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Player_owned_assets_are_never_taken_by_the_commander()
    {
        var t = Theatre();
        var mine = t.Air!.Idle("blue").Where(u => u.Kind == "air").Take(10).Select(u => u.Id).ToList();
        var op = new Operation { Id = t.Ops!.NewId("blue"), Name = "Held back", Side = "blue", Author = Author.Player,
            Elements = { new OpElement { Id = "reserve", Kind = ElementKind.Air, Held = true, Assets = mine } } };
        Assert.Empty(t.Ops.Add(op));
        t.Advance(3 * 3600);
        foreach (var o in t.Ops.Ops.Where(o => o.Author == Author.AI && o.Side == "blue"))
            Assert.DoesNotContain(o.Elements.SelectMany(e => e.Assets), a => mine.Contains(a));
    }
}
