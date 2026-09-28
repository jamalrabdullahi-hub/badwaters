using BadWaters.Data;
using BadWaters.Sim.Engineering;
using Xunit;

namespace BadWaters.Sim.Tests;

public class PartOptionTests
{
    private static readonly GameContent Content = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static ResolvedPart R(string part, Placement pl) => PartResolver.Resolve(Content.Catalog.Parts[part], pl, Content.Catalog);

    [Fact]
    public void Library_now_has_over_three_hundred_parts() => Assert.True(Content.Catalog.Parts.Count >= 300);

    [Fact]
    public void Engine_type_trades_speed_for_endurance()
    {
        var baseE = R("ac-engine", new Placement("ac-engine", 0, 0));
        var hbtf = R("ac-engine", new Placement("ac-engine", 0, 0) { Options = new() { ["type"] = "hbtf" } });
        var ram = R("ac-engine", new Placement("ac-engine", 0, 0) { Options = new() { ["type"] = "scramjet" } });
        Assert.True(hbtf.Adds["endurance"] > baseE.Adds["endurance"]);
        Assert.True(hbtf.Adds["speed"] < baseE.Adds["speed"]);
        Assert.True(ram.Adds["speed"] > baseE.Adds["speed"]);
        Assert.True(ram.Cost > baseE.Cost);
    }

    [Fact]
    public void Gear_type_and_ratio_change_field_performance_and_weight()
    {
        var normal = R("ac-gear-main-twin", new Placement("ac-gear-main-twin", 0, 0));
        var stol = R("ac-gear-main-twin", new Placement("ac-gear-main-twin", 0, 0) { Options = new() { ["type"] = "stol" }, Scale = 1.3 });
        Assert.True(stol.Adds["field"] > normal.Adds["field"]);
        Assert.True(stol.Uses["mass"] > normal.Uses["mass"]);
    }

    [Fact]
    public void Bigger_parts_cost_more_and_are_easier_to_see()
    {
        var std = R("rad-array-4face", new Placement("rad-array-4face", 0, 0));
        var big = R("rad-array-4face", new Placement("rad-array-4face", 0, 0) { Scale = 1.6 });
        Assert.True(big.Uses["mass"] > std.Uses["mass"]);
        Assert.True(big.Cost > std.Cost);
        Assert.True(big.Adds.GetValueOrDefault("signature") < std.Adds.GetValueOrDefault("signature"));
    }

    [Fact]
    public void Component_builder_is_deterministic_and_parameters_matter()
    {
        var a = Content.Archetypes["ac-engine"];
        var v1 = new Dictionary<string, double> { ["size"] = 1.4, ["bypass"] = 5 };
        var c1 = ComponentBuilder.Build(a, "x", "X", v1);
        var c2 = ComponentBuilder.Build(a, "x", "X", v1);
        Assert.Equal(c1.Uses, c2.Uses);
        Assert.Equal(c1.Adds, c2.Adds);
        var small = ComponentBuilder.Build(a, "y", "Y", new Dictionary<string, double> { ["size"] = 0.7 });
        Assert.True(c1.Uses["mass"] > small.Uses["mass"]);
        Assert.True(c1.Adds["endurance"] > small.Adds["endurance"]);
        var hiTech = ComponentBuilder.Build(a, "z", "Z", new Dictionary<string, double> { ["tech"] = 9 });
        Assert.True(hiTech.Requires["jet-engines"] > a.Requires["jet-engines"]);
    }

    [Fact]
    public void A_designed_radar_array_works_in_a_design()
    {
        var c = ComponentBuilder.Build(Content.Archetypes["radar-array"], "t-radar", "Test radar", new Dictionary<string, double> { ["faces"] = 3 });
        Content.Catalog.Components[c.Id] = c;
        var d = new DesignDef { Id = "t", Name = "t", Frame = "hull-destroyer-ad", Components = { "t-radar" } };
        var ev = new Evaluator(Content.Catalog).Evaluate(d);
        Assert.True(ev.Ratings["detection"] > 0);
        Content.Catalog.Components.Remove(c.Id);
    }

    [Fact]
    public void Side_profile_affects_aircraft_performance()
    {
        var f = Content.Catalog.Frames["af-heavy-fighter"];
        var deep = new DesignDef { Id = "d", Name = "d", Frame = f.Id, Side = new List<double> { 9.5, 0, 5, 3, -5, 3, -9.5, 4, -9.5, -3, 5, -3 } };
        var r = ShapeRules.Evaluate(f, deep, Content.Catalog);
        Assert.True(r.Ratings.GetValueOrDefault("speed") < 0);
    }
}
