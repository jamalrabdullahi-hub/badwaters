using BadWaters.Data;
using BadWaters.Sim.Engineering;
using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class TheatreTests
{
    private static readonly GameContent Content = GameContent.Load(Path.Combine(FindRoot(), "data", "core"));

    private static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "BadWaters.sln"))) d = d.Parent;
        return d!.FullName;
    }

    private static InterceptTrial Trial(int seed = 1, Func<ScenarioDef, ScenarioDef>? edit = null, Dictionary<string, DesignDef>? extra = null)
    {
        var sc = Content.Scenarios["intercept-trial"];
        if (edit != null) sc = edit(sc);
        return new InterceptTrial(sc, id => SystemProfile.Build(Content.Catalog, extra != null && extra.TryGetValue(id, out var d) ? d : Content.Designs[id]), seed);
    }

    [Fact]
    public void Protected_sites_cannot_be_targeted()
    {
        var t = Trial();
        t.Control["red"] = SideControl.Player;
        var r = t.Launch("cinder", "harbour-town");
        Assert.False(r.Ok);
        Assert.Equal(TargetPolicy.ProtectedMessage, r.Message);
        Assert.Empty(t.Tracks);
    }

    [Fact]
    public void Ai_defence_detects_and_engages_on_its_own()
    {
        var t = Trial();
        t.Advance(900);
        Assert.True(t.Finished);
        Assert.All(t.Tracks, tr => Assert.True(tr.Detected));
        Assert.NotEmpty(t.Shots);
        Assert.All(t.Tracks, tr => Assert.NotEqual(TrackState.Flying, tr.State));
    }

    [Fact]
    public void Player_controlled_defence_waits_for_orders()
    {
        var t = Trial();
        t.Control["blue"] = SideControl.Player;
        t.Advance(600);
        Assert.Empty(t.Shots);
        Assert.Contains(t.Tracks, tr => tr.State == TrackState.Impacted);
    }

    [Fact]
    public void Player_can_fire_and_engage_manually()
    {
        var t = Trial();
        t.Control["red"] = SideControl.Player;
        t.Control["blue"] = SideControl.Player;
        Assert.True(t.Launch("cinder", "target-alpha").Ok);
        var tr = t.Tracks.Single();
        Assert.False(t.Engage("aster", tr.Id).Ok); // not detected yet
        var fired = false;
        for (var i = 0; i < 800 && tr.State == TrackState.Flying && !fired; i++)
        {
            t.Advance(1);
            if (tr.Detected && t.PlanIntercept(t.Batteries[0], tr) != null) fired = t.Engage("aster", tr.Id, 2).Ok;
        }
        Assert.True(fired);
        Assert.Equal(2, t.Shots.Count);
    }

    [Fact]
    public void Better_interceptor_components_raise_kill_chance()
    {
        var t = Trial();
        t.Control["red"] = SideControl.Player;
        t.Launch("cinder", "target-alpha");
        var tr = t.Tracks.Single();
        var basic = t.KillChance(t.Batteries[0], tr, tr.FlightTime * 0.9).Pk;
        var hit = Trial(extra: null, edit: s => s with { Batteries = s.Batteries.Select(b => b with { Interceptor = "sam-hit" }).ToList() });
        hit.Control["red"] = SideControl.Player;
        hit.Launch("cinder", "target-alpha");
        var better = hit.KillChance(hit.Batteries[0], hit.Tracks.Single(), tr.FlightTime * 0.9);
        Assert.True(better.Pk > basic);
        Assert.Contains(better.Terms, x => x.Sources.Contains("Hit-to-kill vehicle"));
    }

    [Fact]
    public void Decoys_and_manoeuvring_lower_kill_chance()
    {
        var plain = Trial();
        plain.Control["red"] = SideControl.Player;
        plain.Launch("cinder", "target-alpha");
        var p1 = plain.KillChance(plain.Batteries[0], plain.Tracks[0], 100).Pk;
        var hard = Trial(edit: s => s with { Launchers = s.Launchers.Select(l => l with { Missile = "bm-tactical-b" }).ToList() });
        hard.Control["red"] = SideControl.Player;
        hard.Launch("cinder", "target-alpha");
        var (terms, p2) = hard.KillChance(hard.Batteries[0], hard.Tracks[0], 100);
        Assert.True(p2 < p1);
        Assert.Contains(terms, x => x.Sources.Contains("Penetration aids"));
    }

    [Fact]
    public void Same_seed_gives_the_same_battle()
    {
        var a = Trial(7); var b = Trial(7);
        a.Advance(900); b.Advance(900);
        Assert.Equal(a.Log.Select(l => l.Text), b.Log.Select(l => l.Text));
    }
}
