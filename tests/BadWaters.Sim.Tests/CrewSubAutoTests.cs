using BadWaters.Data;
using BadWaters.Sim.Engineering;
using Xunit;

namespace BadWaters.Sim.Tests;

public class CrewSubAutoTests
{
    private static readonly GameContent Content = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static Catalog Cat => Content.Catalog;
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static Evaluation Eval(DesignDef d) => new Evaluator(Cat).Evaluate(d);
    private static DesignDef D(string frame, params Placement[] parts) => new() { Id = "t", Name = "t", Frame = frame, Parts = parts.ToList() };

    // ---- crew
    [Fact]
    public void Bigger_hulls_need_more_crew() =>
        Assert.True(CrewRules.HullCrew(Cat.Frames["hull-destroyer-ad"], Cat) > CrewRules.HullCrew(Cat.Frames["hull-corvette"], Cat));

    [Fact]
    public void Uncrewed_frames_need_no_crew()
    {
        Assert.Equal(0, CrewRules.HullCrew(Cat.Frames["hull-usv"], Cat));
        Assert.Equal(0, Eval(D("hull-usv", new Placement("gun-30rws", 0, 0))).Budgets.GetValueOrDefault("crew").Used);
    }

    [Fact]
    public void Bigger_gun_needs_more_crew()
    {
        var p = Cat.Parts.Values.First(x => x.Category == "Guns" && x.Scaling != null);
        var small = PartResolver.Resolve(p, new Placement(p.Id, 0, 0), Cat).Uses["crew"];
        var big = PartResolver.Resolve(p, new Placement(p.Id, 0, 0) { Scale = p.Scaling!.Max }, Cat).Uses["crew"];
        Assert.True(big > small);
    }

    [Fact]
    public void Too_few_berths_is_an_issue_and_berths_fix_it()
    {
        var d = D("hull-corvette", Enumerable.Range(0, 12).Select(i => new Placement("gun-twin-76", 0, 20 - i * 4)).ToArray());
        Assert.Contains(Eval(d).Issues, i => i.Text.StartsWith("crew"));
        d.Parts.Add(new Placement("berth-large", 0, -25));
        Assert.DoesNotContain(Eval(d).Issues, i => i.Text.StartsWith("crew"));
    }

    // ---- submarines
    [Fact]
    public void Submarine_without_pressure_hull_is_flagged() =>
        Assert.Contains(Eval(D("hull-ssk", new Placement("sub-sail", 0, 0))).Issues, i => i.Text.Contains("pressure hull"));

    [Fact]
    public void Titanium_goes_deeper()
    {
        var p = Cat.Parts["ph-mid"];
        var steel = PartResolver.Resolve(p, new Placement("ph-mid", 0, 0) { Options = new() { ["grade"] = "std" } }, Cat).Adds["depth"];
        var ti = PartResolver.Resolve(p, new Placement("ph-mid", 0, 0) { Options = new() { ["grade"] = "ti" } }, Cat).Adds["depth"];
        Assert.True(ti > steel);
    }

    [Fact]
    public void Battery_and_aip_add_submerged_endurance()
    {
        var a = Eval(D("hull-ssk", new Placement("sub-battery", 0, 0))).Ratings.GetValueOrDefault("submerged-endurance");
        var b = Eval(D("hull-ssk", new Placement("sub-battery", 0, 0), new Placement("sub-aip", 0, 10))).Ratings.GetValueOrDefault("submerged-endurance");
        Assert.True(b > a && a > 0);
    }

    // ---- auto-design
    public static IEnumerable<object[]> Roles() => Content.AutoRoles.Keys.Select(k => new object[] { k });

    [Theory, MemberData(nameof(Roles))]
    public void Auto_design_fills_every_role_inside_the_hull(string id)
    {
        var role = Content.AutoRoles[id];
        var res = new AutoDesigner(Cat).Design(role, "auto", role.Name, Content.Designs);
        Assert.NotEmpty(res.Design.Parts);
        Assert.DoesNotContain(res.Evaluation.Issues, i => i.Kind == IssueKind.OverBudget || i.Text.Contains("hangs off"));
        Assert.DoesNotContain(res.Evaluation.Issues, i => i.Text.StartsWith("Submarine has no"));
    }

    [Fact]
    public void Auto_design_is_deterministic_per_seed()
    {
        var role = Content.AutoRoles["ffg-asw"];
        var a = new AutoDesigner(Cat).Design(role, "a", "a", Content.Designs, seed: 3).Design.Parts;
        var b = new AutoDesigner(Cat).Design(role, "a", "a", Content.Designs, seed: 3).Design.Parts;
        Assert.Equal(a.Select(p => (p.Part, p.X, p.Y)), b.Select(p => (p.Part, p.X, p.Y)));
    }
}
