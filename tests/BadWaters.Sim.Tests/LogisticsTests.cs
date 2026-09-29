using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Theatre;
using Xunit;

namespace BadWaters.Sim.Tests;

public class LogisticsTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static InterceptTrial Setup(string sc = "jpn-chn") =>
        TheatreFactory.Create(C.Scenarios[sc], id => SystemProfile.Build(C.Catalog, C.Designs[id]), C.PresetInfo, C.BaseTypes, _ => C.DoctrineFor(null), 1,
            new Dictionary<string, SideControl> { ["blue"] = SideControl.Player, ["red"] = SideControl.Player }, C.Stores, C.Logistics);

    [Fact]
    public void Theatre_has_depots_nodes_and_named_routes()
    {
        var t = Setup();
        var lg = t.Logistics!;
        Assert.Contains(lg.Nodes.Values, n => n.Kind == "depot" && n.Side == "blue");
        Assert.Contains(lg.Nodes.Values, n => n.Kind == "depot" && n.Side == "red");
        Assert.Contains(lg.Corridors.Values, c => c.Def.Name.StartsWith("Highway"));
        Assert.True(lg.Get(lg.Nodes.Values.First(n => n.Kind == "depot"), "avfuel") > 0);
        Assert.True(Policy.TargetPolicy.IsLegitimate(t.Scenario.Sites.First(s => s.Type == "depot").Class));
    }

    [Fact]
    public void Terrain_and_distance_cut_delivery_efficiency()
    {
        var t = Setup("isr-irn");
        var nodes = t.Logistics!.Nodes.Values.ToList();
        var high = nodes.Where(n => n.Elev > 1500).ToList();
        Assert.NotEmpty(high);
        Assert.All(high, n => Assert.True(n.Factor < 1));
        Assert.Contains(nodes, n => n.Factor >= 0.98);
    }

    [Fact]
    public void Units_consume_spares_and_a_dry_base_cannot_fly()
    {
        var t = Setup();
        var lg = t.Logistics!;
        var bat = lg.Nodes.Values.First(n => n.Kind == "battery" && n.Side == "blue");
        var before = lg.Get(bat, "spares");
        lg.Auto["blue"] = false; lg.Auto["red"] = false;
        t.Advance(3 * 3600);
        Assert.True(lg.Get(bat, "spares") < before);
        // empty an air base's av fuel: sorties are refused with a logistics reason
        var (preset, _, _) = t.Air!.IdleByType("blue").First(x => C.Stores!.Template(x.Preset) != null);
        foreach (var n in lg.Nodes.Values.Where(n => n.Side == "blue" && n.Kind == "airbase")) lg.Add(n, "avfuel", -lg.Get(n, "avfuel"));
        var r = t.Air.Plan(new AtoEntry { Side = "blue", StationLon = 128, StationLat = 27, Start = t.Time, Flights = { new FlightRequest(preset, 2, Mission.CAP) } });
        Assert.False(r.Ok);
        Assert.Contains(r.Issues, i => i.Contains("av fuel"));
    }

    [Fact]
    public void Liquid_missiles_need_propellant()
    {
        var t = Setup("isr-irn");
        var lg = t.Logistics!;
        var l = t.Launchers.FirstOrDefault(x => !x.IsShip && lg.IsLiquid(x.Def.Missile));
        if (l == null) return;
        var node = lg.Node(l.Def.Id)!;
        lg.Add(node, "propellant", -lg.Get(node, "propellant"));
        var target = t.Scenario.Sites.First(s => s.Side != l.Def.Side && Policy.TargetPolicy.IsLegitimate(s.Class) && Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, s.Lon, s.Lat) <= l.ReachKm);
        var r = t.Launch(l.Def.Id, target.Id, 1);
        Assert.False(r.Ok);
        Assert.Contains("propellant", r.Message);
    }

    [Fact]
    public void Shipments_take_time_use_vehicles_and_arrive()
    {
        var t = Setup();
        var lg = t.Logistics!;
        lg.Auto["blue"] = false;
        var depot = lg.Nodes.Values.First(n => n.Kind == "depot" && n.Side == "blue");
        var dest = lg.Nodes.Values.Where(n => n.Kind == "airbase" && n.Side == "blue").First(n => lg.Route("blue", depot.Id, n.Id) != null);
        var had = lg.Get(dest, "avfuel");
        var (ok, msg, s) = lg.Order("blue", "avfuel", 100, depot.Id, dest.Id, player: true);
        Assert.True(ok, msg);
        Assert.True(s!.Arrive > t.Time);
        Assert.True(lg.InUse.Values.Sum() > 0);
        Assert.Equal(had, lg.Get(dest, "avfuel"), 3);
        t.Advance(s.Arrive - t.Time + 10);
        Assert.Equal(ShipmentState.Delivered, s.State);
        Assert.Contains(t.Log, x => x.Kind == "logistics" && x.Text.Contains("delivered to " + dest.Name));
    }

    [Fact]
    public void A_struck_route_slows_and_can_destroy_loads()
    {
        var t = Setup();
        var lg = t.Logistics!;
        var c = lg.Corridors.Values.First(x => x.Def.Mode == "road" && x.Def.Side == "blue");
        var before = lg.Route("blue", c.Def.A, c.Def.B)!.Count;
        lg.HitCorridor(c.Def.Id, 8, "test");
        Assert.True(c.Damage > 0);
        Assert.Equal(before, lg.Route("blue", c.Def.A, c.Def.B)!.Count);
        Assert.NotNull(t.Site("corridor:" + c.Def.Id));
    }

    [Fact]
    public void Officer_resupplies_from_the_reserve_and_never_cannibalises()
    {
        var t = Setup();
        var lg = t.Logistics!;
        var bat = lg.Nodes.Values.First(n => n.Kind == "battery" && n.Side == "blue");
        lg.Add(bat, "spares", -lg.Get(bat, "spares"));
        // every other blue base sits exactly at its keep share: nothing may be taken from them
        foreach (var n in lg.Nodes.Values.Where(n => n.Side == "blue" && n.Kind is "airbase" or "navalbase" or "garrison"))
            lg.Add(n, "spares", n.Baseline["spares"] * lg.Def.KeepShare - lg.Get(n, "spares"));
        var kept = lg.Nodes.Values.Where(n => n.Side == "blue" && n.Kind is "airbase" or "navalbase" or "garrison").ToDictionary(n => n.Id, n => lg.Get(n, "spares"));
        t.Advance(3700);
        var s = lg.Shipments.FirstOrDefault(x => x.To == bat.Id && x.Item == "spares");
        Assert.NotNull(s);
        Assert.Equal("depot", lg.Nodes[s!.From].Kind);
        foreach (var (id, q) in kept) Assert.True(lg.Get(lg.Nodes[id], "spares") >= q - lg.DailyUse(lg.Nodes[id], "spares") - 1, id);
    }

    [Fact]
    public void A_depot_strike_destroys_stored_fuel()
    {
        var t = Setup();
        var lg = t.Logistics!;
        var depot = t.Bases.Values.First(b => b.Site.Type == "depot" && b.Site.Side == "red");
        var node = lg.Nodes[depot.Site.Id];
        var before = lg.Get(node, "avfuel");
        for (var i = 0; i < 6; i++) t.ApplyHit("blue", depot, "fuel", 9, 9, 0.5, "test");
        Assert.True(lg.Get(node, "avfuel") < before);
    }
}
