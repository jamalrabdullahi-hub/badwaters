using BadWaters.Data;
using BadWaters.Sim.Trade;
using Xunit;

namespace BadWaters.Sim.Tests;

public class ChokepointTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }

    private static (World W, Scheduler S) Fresh()
    {
        var w = new World { Now = new DateTime(2020, 1, 1), End = new DateTime(2050, 1, 1) };
        foreach (var r in C.Routes.Keys) w.Routes[r] = new RouteState();
        var s = new Scheduler(w);
        foreach (var r in C.Routes.Values) s.RouteDefs[r.Id] = r;
        return (w, s);
    }

    [Fact]
    public void Routes_have_sea_and_land_modes_and_known_chokepoints()
    {
        Assert.Contains(C.Routes.Values, r => r.Mode == "land");
        Assert.Contains(C.Routes.Values, r => r.Mode == "sea");
        Assert.All(C.Routes.Values.SelectMany(r => r.Chokepoints), c => Assert.True(C.Chokepoints.ContainsKey(c), c));
        Assert.True(C.Chokepoints.Values.Count(c => c.Critical) >= 10);
    }

    [Fact]
    public void Shutting_a_chokepoint_closes_every_route_through_it_and_reopening_recovers()
    {
        var (w, s) = Fresh();
        s.SetChokepoint("hormuz", true, "test");
        s.AdvanceStrategic(BadWaters.Sim.Time.TurnLength.Week);
        Assert.Equal(RouteStatus.EffectivelyClosed, w.Routes["gulf-asia"].Status);
        Assert.Equal(RouteStatus.EffectivelyClosed, w.Routes["gulf-europe"].Status);
        Assert.Equal(RouteStatus.FullyOpen, w.Routes["north-atlantic"].Status);
        s.SetChokepoint("hormuz", false);
        s.AdvanceStrategic(BadWaters.Sim.Time.TurnLength.Month);
        Assert.Equal(RouteStatus.FullyOpen, w.Routes["gulf-asia"].Status);
    }

    [Fact]
    public void Land_corridors_ignore_naval_threat()
    {
        var (w, s) = Fresh();
        s.RouteThreat = _ => (100, 0); // naval threat everywhere, no war
        s.AdvanceStrategic(BadWaters.Sim.Time.TurnLength.Week);
        Assert.Equal(RouteStatus.FullyOpen, w.Routes["europe-east-west"].Status);
        Assert.NotEqual(RouteStatus.FullyOpen, w.Routes["east-asia-europe"].Status);
    }
}
