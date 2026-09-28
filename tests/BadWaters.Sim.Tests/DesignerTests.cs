using BadWaters.Data;
using BadWaters.Sim.Engineering;
using Xunit;

namespace BadWaters.Sim.Tests;

public class DesignerTests
{
    private static readonly GameContent Content = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static Evaluation Eval(DesignDef d) => new Evaluator(Content.Catalog).Evaluate(d);

    [Fact]
    public void Library_has_about_a_hundred_parts_with_shapes()
    {
        Assert.True(Content.Catalog.Parts.Count >= 100);
        Assert.All(Content.Catalog.Parts.Values, p => Assert.NotEmpty(p.Shapes));
    }

    [Fact]
    public void A_carrier_deck_does_not_fit_a_corvette_because_of_weight()
    {
        var d = new DesignDef { Id = "t", Name = "t", Frame = "hull-corvette", Parts = { new Placement("angled-deck", 0, 0) } };
        var ev = Eval(d);
        Assert.Contains(ev.Issues, i => i.Kind == IssueKind.OverBudget && i.Text.StartsWith("mass"));
    }

    [Fact]
    public void Parts_off_the_hull_are_flagged()
    {
        var d = new DesignDef { Id = "t", Name = "t", Frame = "hull-destroyer-ad", Parts = { new Placement("gun-127", 30, 0) } };
        Assert.Contains(Eval(d).Issues, i => i.Text.Contains("hangs off the hull"));
    }

    [Fact]
    public void Topside_parts_count_against_the_topside_budget_and_bring_their_component()
    {
        var d = new DesignDef { Id = "t", Name = "t", Frame = "hull-destroyer-ad", Parts = { new Placement("rad-array-4face", 0, 10), new Placement("rhib", 5, 0) } };
        var ev = Eval(d);
        Assert.True(ev.Budgets["topside"].Used >= 60); // block + the active array radar it carries
        Assert.Equal(8, ev.Ratings["detection"]);
        Assert.True(ev.Breakdown!.ContainsKey("Sensors"));
    }

    [Fact]
    public void The_burke_blueprint_is_a_valid_design()
    {
        var ev = Eval(Content.Designs["ddg-2031"]);
        Assert.True(ev.Valid, string.Join("; ", ev.Issues.Select(i => i.Text)));
        Assert.True(Content.Designs["ddg-2031"].Parts.Count > 15);
    }

    [Fact]
    public void Taller_structure_blocks_a_gun_arc_but_lower_parts_do_not()
    {
        var clear = new DesignDef { Id = "t", Name = "t", Frame = "hull-destroyer-ad", Parts = { new Placement("gun-127", 0, 40) } };
        Assert.Equal(1, ArcGeometry.Coverage(clear, 0, Content.Catalog, 60).Coverage, 3);
        var behindBridge = new DesignDef { Id = "t", Name = "t", Frame = "hull-destroyer-ad", Parts = { new Placement("gun-127", 0, 40), new Placement("bridge-l", 0, 30) } };
        var (cov, sectors) = ArcGeometry.Coverage(behindBridge, 0, Content.Catalog, 60);
        Assert.True(cov < 1);
        Assert.Contains(sectors, x => x.Blocked);
        var besideBoat = new DesignDef { Id = "t", Name = "t", Frame = "hull-destroyer-ad", Parts = { new Placement("gun-127", 0, 40), new Placement("rhib", 0, 30) } };
        Assert.Equal(1, ArcGeometry.Coverage(besideBoat, 0, Content.Catalog, 60).Coverage, 3);
    }

    [Fact]
    public void A_boxed_in_gun_is_less_effective_and_flagged()
    {
        var open = new DesignDef { Id = "t", Name = "t", Frame = "hull-destroyer-ad", Parts = { new Placement("gun-155", 0, 0) } };
        var boxed = new DesignDef { Id = "t", Name = "t", Frame = "hull-destroyer-ad", Parts = { new Placement("gun-155", 0, 0),
            new Placement("house-l", 0, 12), new Placement("house-l", 0, -12), new Placement("house-m", 6, 0), new Placement("house-m", -6, 0) } };
        Assert.True(Eval(boxed).Ratings["strike"] < Eval(open).Ratings["strike"]);
        Assert.Contains(Eval(boxed).Issues, i => i.Text.Contains("firing arc"));
    }

    [Fact]
    public void Shape_modifiers_are_deterministic()
    {
        var d = Content.Designs["ddg-2031"] with { Outline = new List<double> { 0, 77, 10, 40, 10, -70, 8, -77, 0, -77 } };
        var a = ShapeRules.Evaluate(Content.Catalog.Frames[d.Frame], d, Content.Catalog);
        var b = ShapeRules.Evaluate(Content.Catalog.Frames[d.Frame], d, Content.Catalog);
        Assert.Equal(a.Notes, b.Notes);
        Assert.Equal(a.Ratings, b.Ratings);
    }

    [Fact]
    public void Giant_drone_body_is_less_stealthy_than_a_clean_flying_wing()
    {
        var wing = new DesignDef { Id = "w", Name = "w", Frame = "af-ucav-wing", Outline = new List<double> { 0, 5.8, 9.45, -1.8, 8.5, -5.8, 0, -3.5 } };
        var giant = new DesignDef { Id = "g", Name = "g", Frame = "af-ucav-wing", Outline = new List<double> { 0, 5.8, 3, 5, 14, 2, 18, -1, 16, -4, 12, -5.8, 6, -7, 3, -6, 1, -8, 0, -8 } };
        var fw = Content.Catalog.Frames["af-ucav-wing"];
        var sw = ShapeRules.Evaluate(fw, wing, Content.Catalog).Ratings.GetValueOrDefault("signature");
        var sg = ShapeRules.Evaluate(fw, giant, Content.Catalog).Ratings.GetValueOrDefault("signature");
        Assert.True(sw > sg);
        Assert.True(ShapeRules.Evaluate(fw, giant, Content.Catalog).Mass > 0);
    }

    [Fact]
    public void A_bare_four_point_flying_wing_pays_in_handling()
    {
        var wing = new DesignDef { Id = "w", Name = "w", Frame = "af-ucav-wing", Outline = new List<double> { 0, 5.8, 9.45, -1.8, 8.5, -5.8, 0, -3.5 } };
        var r = ShapeRules.Evaluate(Content.Catalog.Frames["af-ucav-wing"], wing, Content.Catalog);
        Assert.True(r.Ratings.GetValueOrDefault("agility") <= -2); // tailless and crude
        var withFins = wing with { Parts = new List<Placement> { new("ac-tail-canted", 0, -4) } };
        Assert.True(ShapeRules.Evaluate(Content.Catalog.Frames["af-ucav-wing"], withFins, Content.Catalog).Ratings.GetValueOrDefault("agility") > r.Ratings["agility"]);
    }

    [Fact]
    public void Slender_hulls_are_faster()
    {
        var f = Content.Catalog.Frames["hull-destroyer-ad"];
        var slim = new DesignDef { Id = "s", Name = "s", Frame = f.Id, Outline = new List<double> { 0, 77, 8, 30, 8, -70, 6, -77, 0, -77 } };
        var fat = new DesignDef { Id = "f", Name = "f", Frame = f.Id, Outline = new List<double> { 0, 77, 20, 30, 20, -70, 18, -77, 0, -77 } };
        Assert.True(ShapeRules.Evaluate(f, slim, Content.Catalog).Ratings.GetValueOrDefault("speed") > ShapeRules.Evaluate(f, fat, Content.Catalog).Ratings.GetValueOrDefault("speed"));
    }

    [Fact]
    public void New_hull_and_airframe_types_exist()
    {
        foreach (var id in new[] { "hull-dinghy", "hull-midget-sub", "hull-trimaran", "hull-catamaran", "hull-destroyer-stealth", "hull-frigate-stealth", "af-hale", "af-male", "af-ucav-wing", "af-delta-heavy", "af-canard-delta", "af-lo-heavy" })
            Assert.NotNull(Content.Catalog.Frames[id].Hull);
    }
}
