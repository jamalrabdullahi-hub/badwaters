using BadWaters.Data;
using BadWaters.Sim.Command;
using BadWaters.Sim.Economy;
using BadWaters.Sim.Theatre;

/// <summary>
/// fullwar [A:B] [days] [seed] (e.g. JPN:CHN; ECS and GULF still work): a whole war with everything on. Strategic economy and national stockpiles, a theatre
/// at war (both commanders, logistics drawn from the nations, daily write-back), a strategic month every 30 days.
/// </summary>
static class FullWar
{
    public static int Run(string[] args)
    {
        var gc = GameContent.Load(Path.Combine("data", "core"));
        var pair = args.Length > 1 ? args[1] : "JPN:CHN";
        pair = pair switch { "ECS" => "JPN:CHN", "GULF" => "ISR:IRN", _ => pair };
        var (na, nb) = FightZone.Parse(pair);
        var theatre = FightZone.Key(na, nb);
        var days = args.Length > 2 ? int.Parse(args[2]) : 30;
        var seed = args.Length > 3 ? int.Parse(args[3]) : 1;
        string? Nat(string name) => gc.Nations.Values.FirstOrDefault(x => x.Name == name || x.Id == name)?.Id;
        string Name(string id) => gc.Nations.TryGetValue(id, out var nn) ? nn.Name : id;
        var scn = FightZone.Build(gc.WorldOob!, na, nb, na, Name) ?? throw new InvalidOperationException($"{pair}: their forces can't reach each other");
        var nations = new List<string> { na, nb };
        var ew = new EconomyWorld(gc.Economy, gc.Geo, gc.Catalog, gc.Designs, gc.Routes);
        var w = new BadWaters.Sim.World { Now = new DateTime(2020, 1, 1) };
        var sw0 = System.Diagnostics.Stopwatch.StartNew();
        ew.Start(w.Economy, nations, 2020, gc.TradeRegimes);
        NationalMunitions.Seed(w.Economy, gc.Stores, new[] { gc.WorldOob! with { Sides = gc.WorldOob.Sites.Select(s => s.Side).Distinct().ToDictionary(n => n, n => n) } }, Nat);
        w.Wars.Add(new War(na, nb, w.Now));
        Console.WriteLine($"Fight zone {scn.Name}: {scn.Sites.Count} sites, {scn.Units.Count} aircraft and ships, {scn.Batteries.Count} batteries, {scn.Launchers.Count} launchers, {scn.Logistics?.Corridors.Count ?? 0} supply routes");
        ew.Strategy = new StrategicCommander(ew, gc.DispositionFor);
        foreach (var n in nations) { ew.AiNations.Add(n); w.Economy.Nations[n].AtWar = true; }
        ew.AtWar = n => nations.Contains(n);
        var gdp0 = nations.ToDictionary(n => n, n => w.Economy.Nations[n].Gdp);
        Console.WriteLine($"Economy started in {sw0.ElapsedMilliseconds} ms: " + string.Join(" · ", nations.Select(n => $"{n} GDP {gdp0[n]:0}")));

        InterceptTrial Build(ScenarioDef sc, int sd)
        {
            var transport = new Dictionary<(string, string), int>();
            foreach (var side in new[] { "blue", "red" })
                if (Nat(sc.Sides.GetValueOrDefault(side, "")) is { } nid && w.Economy.Nations.TryGetValue(nid, out var ne))
                    foreach (var (m, c) in NationalMunitions.Transport(ne)) transport[(side, m)] = c;
            var t = TheatreFactory.Create(sc, id => SystemProfile.Build(gc.Catalog, gc.Designs[id]), gc.PresetInfo, gc.BaseTypes,
                side => gc.DoctrineFor(Nat(sc.Sides.GetValueOrDefault(side, ""))), seed, stores: gc.Stores, logistics: gc.Logistics, transport: transport);
            foreach (var side in new[] { "blue", "red" })
                if (Nat(sc.Sides.GetValueOrDefault(side, "")) is { } nid && w.Economy.Nations.TryGetValue(nid, out var ne))
                    foreach (var note in t.Logistics!.DrawFromNation(side, ne, w.Economy, gc.Geo)) Console.WriteLine($"  draw {nid}: {note}");
            return t;
        }

        var camp = new TheatreCampaign(k => k == theatre ? scn : null, Build, Nat) { Geo = gc.Geo, WorldForces = () => w.Forces };
        var routes = gc.Routes.Keys.ToDictionary(k => k, _ => new BadWaters.Sim.Trade.RouteState());
        int Lost(InterceptTrial t, string side) => t.Air!.Destroyed.Count(id => t.Scenario.Units.First(u => u.Id == id).Side == side);
        for (var d = 1; d <= days; d++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            camp.Daily(w, new[] { theatre });
            w.Now = w.Now.AddDays(1);
            var t = camp.Live[theatre];
            var lg = t.Logistics!;
            string Side(string s)
            {
                var nodes = lg.Nodes.Values.Where(n => n.Side == s).ToList();
                return $"{scn.Sides[s]}: air lost {Lost(t, s)}, ships sunk {t.Naval?.Ships.Count(x => x.Side == s && x.Sunk) ?? 0}, missiles fired {t.Tracks.Count(x => x.Side == s)}, " +
                       $"av fuel {lg.Amount("avfuel", nodes.Sum(n => lg.Get(n, "avfuel")))}, rounds in launchers {t.Launchers.Where(l => l.Def.Side == s).Sum(l => l.Stock)}, dry {nodes.Count(n => n.Short.Count > 0)}";
            }
            Console.WriteLine($"Day {d,2} ({sw.ElapsedMilliseconds / 1000.0:0.0}s) ops {t.Ops!.Ops.Count} · {Side("blue")} | {Side("red")}");
            if (d % 30 == 0)
            {
                var ev = ew.RunMonth(w.Economy, routes);
                Console.WriteLine("  STRATEGIC MONTH: " + string.Join(" · ", nations.Select(n => $"{n} GDP {w.Economy.Nations[n].Gdp:0} ({w.Economy.Nations[n].Gdp / gdp0[n] - 1:P1}), defence {w.Economy.Nations[n].DefenceShare:P1}, programmes {w.Economy.Nations[n].Programmes.Count}"))
                                  + (ev.Count > 0 ? " · events: " + string.Join("; ", ev) : ""));
                foreach (var n in nations)
                    foreach (var dec in ew.Strategy.Decisions.GetValueOrDefault(n)?.TakeLast(3) ?? Enumerable.Empty<string>()) Console.WriteLine($"    {n} AI: {dec}");
            }
        }

        var T = camp.Live[theatre];
        Console.WriteLine();
        Console.WriteLine("=== CAMPAIGN REPORT ===");
        foreach (var side in new[] { "blue", "red" })
        {
            var n = Nat(scn.Sides[side])!;
            var ne = w.Economy.Nations[n];
            var ops = T.Ops!.Ops.Where(o => o.Side == side).ToList();
            var losses = T.Air!.Destroyed.Select(id => T.Scenario.Units.First(u => u.Id == id)).Where(u => u.Side == side).GroupBy(u => gc.PresetInfo(u.Preset)?.Name ?? u.Preset).Select(g => $"{g.Count()}× {g.Key}");
            Console.WriteLine($"{scn.Sides[side]} ({n})");
            Console.WriteLine($"  operations {ops.Count}: {ops.Count(o => o.Status == OpStatus.Complete)} complete, {ops.Count(o => o.Status == OpStatus.Aborted)} aborted, {ops.Count(o => o.Open)} open");
            Console.WriteLine($"  aircraft lost: {string.Join(", ", losses.DefaultIfEmpty("none"))}");
            Console.WriteLine($"  crews recovered {T.Air.CrewsRecovered.GetValueOrDefault(side)}, crews lost {T.Air.CrewsLost.GetValueOrDefault(side)}");
            Console.WriteLine($"  ships sunk: {string.Join(", ", (T.Naval?.Ships.Where(x => x.Side == side && x.Sunk).Select(x => x.Name) ?? Enumerable.Empty<string>()).DefaultIfEmpty("none"))}");
            Console.WriteLine($"  missiles fired {T.Tracks.Count(x => x.Side == side)} (intercepted {T.Tracks.Count(x => x.Side == side && x.State == TrackState.Intercepted)}), interceptors fired {T.Shots.Count(x => T.Batteries.First(b => b.Def.Id == x.BatteryId).Def.Side == side)}");
            var hurt = T.Bases.Values.Where(b => b.Site.Side == side).Select(b => (b.Site.Name, v: b.Components.Values.Average(c => c.Integrity))).Where(x => x.v < 90).OrderBy(x => x.v).Take(6);
            Console.WriteLine($"  bases below 90%: {string.Join(", ", hurt.Select(x => $"{x.Name} {x.v:0}%").DefaultIfEmpty("none"))}");
            var lg = T.Logistics!;
            var lgn = lg.Nodes.Values.Where(x => x.Side == side).ToList();
            Console.WriteLine("  theatre stocks: " + string.Join(" · ", new[] { "avfuel", "diesel", "propellant", "spares" }.Select(i => $"{lg.ItemName(i)} {lg.Amount(i, lgn.Sum(x => lg.Get(x, i)))}")) + $" · dry points {lgn.Count(x => x.Short.Count > 0)}");
            Console.WriteLine($"  national: GDP {ne.Gdp:0} ({ne.Gdp / gdp0[n] - 1:P1}), stability {ne.Stability:0}, jet fuel {NationalMunitions.GoodHeld(w.Economy, gc.Geo, n, "jet-fuel"):0}, rounds at home: " +
                              string.Join(", ", T.Launchers.Where(l => l.Def.Side == side).Select(l => l.Def.Missile).Distinct().Select(m => $"{m} {NationalMunitions.Held(ne, m):0}")));
        }
        Console.WriteLine($"logistics: {T.Log.Count(x => x.Kind == "logistics" && x.Text.Contains("delivered"))} deliveries, {T.Log.Count(x => x.Text.Contains("in transit"))} loads lost in transit, {T.Log.Count(x => x.Text.Contains("stored supplies destroyed"))} storage hits, {T.Log.Count(x => x.Kind == "impact" && x.Text.Contains("damaged") && x.Text.Contains("Highway"))} route hits");
        Console.WriteLine("strategic reports (last): " + string.Join(" | ", camp.Reports.TakeLast(6)));
        Console.WriteLine($"total {sw0.Elapsed.TotalSeconds:0} s");
        return 0;
    }
}
