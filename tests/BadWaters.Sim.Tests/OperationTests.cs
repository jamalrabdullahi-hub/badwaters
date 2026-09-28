using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class OperationTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }

    private static InterceptTrial Trial()
    {
        var sc = C.Scenarios["jpn-chn"] with { Ato = new() }; // no scripted lines: operations only
        var t = new InterceptTrial(sc, id => SystemProfile.Build(C.Catalog, C.Designs[id]), 3, C.BaseTypes);
        t.Control["blue"] = SideControl.Player; t.Control["red"] = SideControl.Player;
        t.Naval = new NavalForces(t, C.PresetInfo, 3);
        t.Intel = new IntelService(t, C.PresetInfo, 3);
        t.Air = new AirTasking(t, C.PresetInfo, 3);
        t.Ops = new OperationBoard(t);
        return t;
    }

    private static (string Preset, List<string> Ids) Fighters(InterceptTrial t, string side, int n)
    {
        var g = t.Air!.Idle(side).GroupBy(u => (u.Preset, u.Base)).First(g => g.Count() >= n && (C.PresetInfo(g.Key.Preset)?.R("reach") ?? 0) > 0);
        return (g.Key.Preset, g.Take(n).Select(u => u.Id).ToList());
    }

    private static Operation Cap(InterceptTrial t, string side, string preset, List<string> ids, double start = 0, double lon = 128, double lat = 30) => new()
    {
        Id = t.Ops!.NewId(side), Name = "test CAP", Side = side, Author = Author.Player, Purpose = { "area-defence" }, Start = start,
        Elements = { new OpElement { Id = "cap", Kind = ElementKind.Air, Role = "CAP", Assets = ids,
            Spec = new AtoEntry { Side = side, StationLon = lon, StationLat = lat, Start = start, Duration = 1200, Flights = { new FlightRequest(preset, ids.Count, Mission.CAP) } } } },
    };

    [Fact]
    public void An_asset_belongs_to_one_operation_and_the_pool_cannot_use_it()
    {
        var t = Trial();
        var (preset, ids) = Fighters(t, "blue", 2);
        var a = Cap(t, "blue", preset, ids);
        Assert.Empty(t.Ops!.Add(a));
        var b = Cap(t, "blue", preset, ids);
        Assert.NotEmpty(t.Ops.Add(b));                               // same aircraft: refused
        Assert.All(ids, id => Assert.Equal(a.Id, t.Ops.Authority.OwnerOf(id)));
        // the AI pool (no operation) cannot plan with them
        var r = t.Air!.PlanFor(new AtoEntry { Side = "blue", StationLon = 128, StationLat = 30, Flights = { new FlightRequest(preset, 2, Mission.CAP) } }, "", ids, commit: false);
        Assert.False(r.Ok);
    }

    [Fact]
    public void Assets_return_to_the_pool_when_the_operation_completes()
    {
        var t = Trial();
        var (preset, ids) = Fighters(t, "blue", 2);
        var op = Cap(t, "blue", preset, ids);
        t.Ops!.Add(op);
        t.Advance(60);
        Assert.Equal(OpStatus.Active, op.Status);
        t.Advance(4 * 3600);
        Assert.Equal(OpStatus.Complete, op.Status);
        Assert.All(ids, id => Assert.True(t.Ops.Authority.IsFree(id)));
    }

    [Fact]
    public void A_rule_on_the_sides_own_picture_activates_a_held_element()
    {
        var t = Trial();
        var (preset, ids) = Fighters(t, "blue", 4);
        var op = Cap(t, "blue", preset, ids.Take(2).ToList());
        op.Elements.Add(new OpElement { Id = "escort", Kind = ElementKind.Air, Role = "EW escort", Held = true, Assets = ids.Skip(2).ToList(),
            Spec = new AtoEntry { Side = "blue", StationLon = 128, StationLat = 30, Duration = 1200, Flights = { new FlightRequest(preset, 2, Mission.CAP) } } });
        op.Rules.Add(new OpRule(new OpCondition("enemy-aew-detected"), new OpAction("activate", "escort")));
        t.Ops!.Add(op);
        t.Advance(120);
        Assert.Equal("waiting", op.Elements[1].State);
        // blue's picture (not ground truth) gains an identified enemy AEW
        t.Intel!.Pictures["blue"].Contacts["x"] = new Contact { Key = "x", Kind = "air", Role = "AEW", Lon = 125, Lat = 31, LastSeen = t.Time };
        t.Advance(30);
        Assert.Equal("flying", op.Elements[1].State);
        Assert.Contains(op.Log, l => l.Contains("enemy AEW detected"));
    }

    [Fact]
    public void An_abort_rule_releases_everything_and_dependencies_wait()
    {
        var t = Trial();
        var (preset, ids) = Fighters(t, "blue", 4);
        var first = Cap(t, "blue", preset, ids.Take(2).ToList());
        first.Rules.Add(new OpRule(new OpCondition("time-after", Value: 600), new OpAction("abort")));
        var second = Cap(t, "blue", preset, ids.Skip(2).ToList());
        second.After.Add(first.Id);
        t.Ops!.Add(first); t.Ops.Add(second);
        t.Advance(300);
        Assert.Equal(OpStatus.Planned, second.Status);  // waits for the first
        t.Advance(600);
        Assert.Equal(OpStatus.Aborted, first.Status);
        Assert.All(ids.Take(2), id => Assert.True(t.Ops.Authority.IsFree(id)));
        t.Advance(60);
        Assert.Equal(OpStatus.Aborted, second.Status);  // its prerequisite failed
    }
}
