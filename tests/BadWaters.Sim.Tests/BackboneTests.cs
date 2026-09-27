using BadWaters.Data;
using BadWaters.Sim.Engineering;
using BadWaters.Sim.Industry;
using BadWaters.Sim.Time;
using BadWaters.Sim.Trade;
using Xunit;
using Xunit.Abstractions;

namespace BadWaters.Sim.Tests;

public class BackboneTests
{
    private readonly ITestOutputHelper _out;
    public BackboneTests(ITestOutputHelper o) => _out = o;

    private static string Core()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data", "core"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "data", "core");
    }
    private static readonly Lazy<GameContent> C = new(() => GameContent.Load(Core()));
    private static Dictionary<string, int> AllTech(int lvl) => C.Value.Tech.Keys.ToDictionary(k => k, _ => lvl);

    [Fact]
    public void Content_loads_and_cross_references_resolve()
    {
        Assert.Empty(C.Value.Errors);
        Assert.True(C.Value.Catalog.Frames.Values.Count(f => f.Kind == "ship") >= 25);
        Assert.Equal(18, C.Value.Catalog.Components.Values.Select(c => c.Family).Distinct().Count());
    }

    [Theory]
    [InlineData("ddg-1998")]
    [InlineData("ddg-2031")]
    [InlineData("bomber-lo")]
    [InlineData("cm-cruise")]
    [InlineData("sam-interceptor")]
    [InlineData("gs-radar")]
    [InlineData("gs-launcher")]
    public void Sample_designs_are_valid_with_advanced_tech(string id)
    {
        var ev = new Evaluator(C.Value.Catalog).Evaluate(C.Value.Designs[id], AllTech(10));
        foreach (var i in ev.Issues) _out.WriteLine($"{id}: {i.Text}");
        _out.WriteLine($"{id}: " + string.Join(", ", ev.Ratings.Select(r => $"{r.Key} {r.Value:0.#}")));
        Assert.True(ev.Valid);
        Assert.True(ev.Cost > 0);
    }

    [Fact]
    public void Tech_level_gates_components_not_finished_weapons()
    {
        var ev = new Evaluator(C.Value.Catalog).Evaluate(C.Value.Designs["bomber-lo"], AllTech(3));
        Assert.Contains(ev.Issues, i => i.Kind == IssueKind.TechTooLow);
    }

    [Fact]
    public void Datalink_without_a_combat_system_bus_is_flagged()
    {
        var d = C.Value.Designs["gs-launcher"] with { Id = "x", Components = C.Value.Designs["gs-launcher"].Components.Where(c => c != "cp-cms-basic").ToList() };
        var ev = new Evaluator(C.Value.Catalog).Evaluate(d);
        Assert.Contains(ev.Issues, i => i.Kind == IssueKind.MissingInterface);
    }

    [Fact]
    public void Old_hull_takes_new_radar_only_when_power_and_cooling_allow()
    {
        var planner = new RefitPlanner(C.Value.Catalog);
        var ok = planner.Plan(C.Value.Designs["ddg-1998"], C.Value.Designs["ddg-2031"], AllTech(10));
        var old = C.Value.Designs["ddg-1998"];
        var overreach = old with { Id = "overreach", Components = old.Components.Select(c => c == "sn-radar-pesa" ? "sn-radar-aesa" : c == "cp-cms-basic" ? "cp-cms-integrated" : c).ToList() };
        var bad = planner.Plan(old, overreach, AllTech(10));
        foreach (var i in bad.Issues) _out.WriteLine(i.Text);
        Assert.True(ok.Feasible);
        Assert.False(bad.Feasible);
        Assert.Contains(bad.Issues, i => i.Kind == IssueKind.OverBudget);
        Assert.True(ok.Cost > 0 && ok.Months > 0);
    }

    [Fact]
    public void Industry_bottleneck_limits_output()
    {
        var types = C.Value.Factories;
        var factories = new[]
        {
            new FactoryInstance { Id = "f1", Type = "radar-factory", Park = "p1" },
            new FactoryInstance { Id = "f2", Type = "rf-module-plant", Park = "p1" },
            new FactoryInstance { Id = "f3", Type = "semiconductor-fab", Park = "p1" },
            new FactoryInstance { Id = "f4", Type = "electronics-plant", Park = "p1" },
        };
        var stock = new Dictionary<string, double> { ["rare-materials"] = 100 };
        var rep = IndustrySolver.RunMonth(types, factories, stock, new Dictionary<string, double>(), new Dictionary<string, double> { ["radar-factory"] = 50 });
        foreach (var b in rep.Bottlenecks) _out.WriteLine(b);
        Assert.True(rep.WorkDone["radar-factory"] <= 8.0001);
        Assert.NotEmpty(rep.Bottlenecks);
    }

    [Fact]
    public void Route_disruption_raises_cost_and_cuts_delivery()
    {
        var r = new RouteState();
        Assert.Equal(RouteStatus.FullyOpen, r.Status);
        for (var i = 0; i < 10; i++) r.UpdateFromThreat(hostileThreat: 95, friendlyControl: 0, regionalWar: 100);
        Assert.Equal(RouteStatus.EffectivelyClosed, r.Status);
        Assert.True(r.CostMultiplier > 2 && r.DeliveryFactor <= 0.3);

        var contested = new RouteState();
        for (var i = 0; i < 10; i++) contested.UpdateFromThreat(40, friendlyControl: 60, regionalWar: 100);
        Assert.Equal(RouteStatus.Contested, contested.Status);

        // starved imports throttle a factory that depends on them
        var types = C.Value.Factories;
        var fab = new[] { new FactoryInstance { Id = "f", Type = "semiconductor-fab", Park = "p" } };
        var full = IndustrySolver.RunMonth(types, fab, new(), new Dictionary<string, double> { ["rare-materials"] = 3 * 1.0 }, new Dictionary<string, double>());
        var cut = IndustrySolver.RunMonth(types, fab, new(), new Dictionary<string, double> { ["rare-materials"] = 3 * r.DeliveryFactor }, new Dictionary<string, double>());
        Assert.True(cut.WorkDone.GetValueOrDefault("semiconductor-fab") < full.WorkDone["semiconductor-fab"]);
    }

    private static World NewWorld()
    {
        var w = new World { Now = new DateTime(2020, 1, 1), End = new DateTime(2050, 12, 31), Player = "JPN" };
        foreach (var t in C.Value.Theatres.Keys) w.Theatres[t] = new TheatreState { Id = t };
        foreach (var r in C.Value.Routes.Keys) w.Routes[r] = new RouteState();
        return w;
    }

    private static Scheduler NewScheduler(World w)
    {
        var s = new Scheduler(w);
        foreach (var r in C.Value.Routes.Values) s.RouteTheatres[r.Id] = r.Theatres.ToHashSet();
        return s;
    }

    [Fact]
    public void Strategic_turn_runs_full_length_in_peace()
    {
        var w = NewWorld();
        var irq = NewScheduler(w).AdvanceStrategic(TurnLength.Month);
        Assert.Null(irq);
        Assert.Equal(new DateTime(2020, 2, 1), w.Now);
    }

    [Fact]
    public void War_involving_the_player_interrupts_the_strategic_turn()
    {
        var w = NewWorld();
        var s = NewScheduler(w);
        s.DailyHook = (world, now) => { if (now == new DateTime(2020, 3, 10)) { world.Theatres["ECS"].AtWar = true; world.Theatres["ECS"].PlayerInvolved = true; } return null; };
        var irq = s.AdvanceStrategic(TurnLength.Year);
        Assert.NotNull(irq);
        Assert.Equal("war", irq!.Kind);
        Assert.Equal("ECS", irq.Theatre);
        Assert.Equal(new DateTime(2020, 3, 10), w.Now);
    }

    [Fact]
    public void World_keeps_running_during_theatre_time()
    {
        var w = NewWorld();
        var s = NewScheduler(w);
        var days = 0;
        s.DailyHook = (_, _) => { days++; return null; };
        s.AdvanceTheatreSeconds(3 * 86400 + 3600);
        Assert.Equal(3, days);
        Assert.Equal(new DateTime(2020, 1, 4, 1, 0, 0), w.Now);
    }

    [Fact]
    public void World_save_round_trips()
    {
        var w = NewWorld();
        w.Routes["gulf-asia"].Disruption = 42;
        w.Stock["fuel"] = 12.5;
        var back = World.Load(w.Save());
        Assert.Equal(42, back.Routes["gulf-asia"].Disruption);
        Assert.Equal(12.5, back.Stock["fuel"]);
        Assert.Equal(w.Now, back.Now);
    }
}
