using BadWaters.Data;
using BadWaters.Sim.Economy;
using Xunit;

namespace BadWaters.Sim.Tests;

public class StrategicAiTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }

    private static (EconomyWorld Ew, EconomyState St, StrategicCommander Ai) Setup(string nation)
    {
        var ew = new EconomyWorld(C.Economy, C.Geo, C.Catalog, C.Designs, C.Routes);
        var st = new EconomyState();
        ew.Seeder.Seed(st, nation, 2020);
        return (ew, st, new StrategicCommander(ew, C.DispositionFor));
    }

    [Fact]
    public void At_war_it_raises_defence_spending_keeps_its_arsenal_in_production_and_builds_reserves()
    {
        var (ew, st, ai) = Setup("IRN");
        var ne = st.Nations["IRN"];
        var before = ne.DefenceShare;
        for (var m = 0; m < 6; m++) { var rep = ew.Solver.RunMonth(st, "IRN"); ai.Act(st, "IRN", rep, atWar: true); st.Month++; }
        Assert.True(ne.DefenceShare > before);
        Assert.Contains(ne.Programmes, p => C.DispositionFor("IRN").Programmes.Contains(p.Design));
        Assert.Equal(6, ne.StockTargets["energetics"]);
        Assert.NotEmpty(ai.Decisions["IRN"]);
    }

    [Fact]
    public void It_answers_sanctions()
    {
        var (ew, st, ai) = Setup("CHN");
        ew.Seeder.Seed(st, "USA", 2020);
        st.Sanction("USA", "CHN", "chips");
        ai.Act(st, "CHN", ew.Solver.RunMonth(st, "CHN"), atWar: false);
        Assert.Contains(st.Sanctions, x => x.By == "CHN" && x.Target == "USA");
    }

    [Fact]
    public void Persistent_shortages_lead_to_construction()
    {
        var (ew, st, ai) = Setup("DEU");
        var rep = new NationReport { Nation = "DEU" };
        rep.Shortages["structural-steel"] = 500;
        for (var m = 0; m < 3; m++) ai.Act(st, "DEU", rep, atWar: false);
        Assert.Contains(st.Nations["DEU"].Construction, c => C.Economy.Processes.Values.Any(p => p.Facility == c.Type && p.Outputs.ContainsKey("structural-steel")));
    }

    [Fact]
    public void Same_state_same_decisions()
    {
        string Run() { var (ew, st, ai) = Setup("IRN"); for (var m = 0; m < 3; m++) { ai.Act(st, "IRN", ew.Solver.RunMonth(st, "IRN"), true); st.Month++; } return string.Join("|", ai.Decisions["IRN"]); }
        Assert.Equal(Run(), Run());
    }
}
