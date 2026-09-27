using BadWaters.Data;
using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class AirOpsTests
{
    private static readonly GameContent Content = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }

    /// <summary>The Japan vs China scenario with both sides on player control (no AI tasking), unless asked.</summary>
    private static (InterceptTrial T, AirTasking Air) Setup(bool ai = false, int seed = 1)
    {
        var t = new InterceptTrial(Content.Scenarios["jpn-chn"], id => SystemProfile.Build(Content.Catalog, Content.Designs[id]), seed);
        if (!ai) { t.Control["blue"] = SideControl.Player; t.Control["red"] = SideControl.Player; }
        t.Air = new AirTasking(t, Content.PresetInfo, seed);
        return (t, t.Air);
    }

    private static AtoEntry Strike(string target, double tot, params FlightRequest[] f) =>
        new() { Side = "blue", Target = target, Tot = tot, Flights = f.ToList() };

    [Fact]
    public void Timings_run_backwards_from_time_on_target()
    {
        var (_, air) = Setup();
        var r = air.Plan(Strike("shuimen-ab", 4000, new FlightRequest("JPF2A", 4, Mission.Strike), new FlightRequest("USF35A", 2, Mission.Escort), new FlightRequest("JPKC46A", 1, Mission.Tanker)));
        Assert.True(r.Ok, string.Join("; ", r.Issues));
        var strike = r.Flights.Single(f => f.Mission == Mission.Strike);
        var escort = r.Flights.Single(f => f.Mission == Mission.Escort);
        Assert.Equal(new[] { "Take-off", "Ingress", "IP", "Target", "Egress", "Land" }, strike.Route.Select(w => w.Name));
        Assert.Equal(4000, strike.Route.Single(w => w.Name == "Target").T, 3);
        Assert.Equal(4000 - AirPacing.EscortLead, escort.Route.Single(w => w.Name == "Sweep").T, 3);
        Assert.True(strike.Route.Zip(strike.Route.Skip(1)).All(p => p.First.T < p.Second.T));
        var ip = strike.Route.Single(w => w.Name == "IP"); var tgt = strike.Route.Single(w => w.Name == "Target");
        Assert.InRange(Pacing.DistanceKm(ip.Lon, ip.Lat, tgt.Lon, tgt.Lat), 55, 65);
    }

    [Fact]
    public void Out_of_radius_needs_a_tanker()
    {
        var (_, air) = Setup();
        var without = air.Plan(Strike("shuimen-ab", 5000, new FlightRequest("JPF2A", 4, Mission.Strike)));
        Assert.False(without.Ok);
        Assert.Contains(without.Issues, i => i.Contains("tanker"));
        Assert.True(air.Plan(Strike("shuimen-ab", 5000, new FlightRequest("JPF2A", 4, Mission.Strike), new FlightRequest("JPKC46A", 1, Mission.Tanker))).Ok);
    }

    [Fact]
    public void Too_early_tot_is_refused_with_earliest_time()
    {
        var (_, air) = Setup();
        var r = air.Plan(Strike("shuimen-ab", 600, new FlightRequest("JPF2A", 4, Mission.Strike), new FlightRequest("JPKC46A", 1, Mission.Tanker)));
        Assert.False(r.Ok);
        Assert.Contains(r.Issues, i => i.Contains("earliest TOT"));
    }

    [Fact]
    public void Protected_sites_cannot_be_tasked()
    {
        var (_, air) = Setup();
        var r = air.Plan(Strike("shanghai", 5000, new FlightRequest("JPF2A", 4, Mission.Strike), new FlightRequest("JPKC46A", 1, Mission.Tanker)));
        Assert.False(r.Ok);
        Assert.Equal(TargetPolicy.ProtectedMessage, r.Issues.Single());
    }

    [Fact]
    public void Committed_package_flies_and_releases_at_tot()
    {
        var (t, air) = Setup();
        var r = air.Commit(Strike("shuimen-ab", 4000, new FlightRequest("JPF2A", 4, Mission.Strike), new FlightRequest("JPKC46A", 1, Mission.Tanker)));
        Assert.True(r.Ok);
        Assert.Equal(5, air.Tasked.Count);
        t.Advance(4001);
        var f = air.Flights.Single(x => x.Mission == Mission.Strike);
        Assert.True(f.Released);
        Assert.Contains(t.Log, l => l.Text.Contains("released on Shuimen Air Base"));
        t.Advance(4000);
        Assert.Equal(FlightState.Landed, f.State);
        Assert.Empty(air.Tasked);
    }

    [Fact]
    public void Ai_flies_its_own_ato_with_air_combat()
    {
        var (t, air) = Setup(ai: true);
        Assert.NotEmpty(air.Packages);
        t.Advance(5000);
        Assert.Contains(t.Log, l => l.Text.StartsWith("Air combat"));
        Assert.All(air.Packages, p => Assert.True(p.Flights.Any(f => f.Released) || p.Flights.All(f => f.State == FlightState.Lost)));
    }

    [Fact]
    public void Formation_changes_the_odds()
    {
        Assert.True(FormationRules.Mod(Formation.Trail).SamLoss < FormationRules.Mod(Formation.Box).SamLoss);
        Assert.True(FormationRules.Mod(Formation.Wedge).AirToAir > FormationRules.Mod(Formation.Trail).AirToAir);
    }
}
