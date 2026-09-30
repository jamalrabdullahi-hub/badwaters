using BadWaters.Data;
using BadWaters.Sim.Policy;

// Bad Waters command-line tools.
//   validate [dataDir] [modDir...]   check that all content loads and cross-references resolve
//   trial [seed]                     run the intercept trial scenario with both sides on AI and print the log
var cmd = args.Length > 0 ? args[0] : "validate";
if (cmd == "trial")
{
    var gc = GameContent.Load(Path.Combine("data", "core"));
    var t = new BadWaters.Sim.Theatre.InterceptTrial(gc.Scenarios[args.Length > 2 ? args[2] : "intercept-trial"],
        id => BadWaters.Sim.Theatre.SystemProfile.Build(gc.Catalog, gc.Designs[id]), args.Length > 1 ? int.Parse(args[1]) : 1, gc.BaseTypes);
    t.Naval = new BadWaters.Sim.Theatre.NavalForces(t, gc.PresetInfo, 1);
    t.Intel = new BadWaters.Sim.Theatre.IntelService(t, gc.PresetInfo, 1);
    t.Air = new BadWaters.Sim.Theatre.AirTasking(t, gc.PresetInfo, args.Length > 1 ? int.Parse(args[1]) : 1);
    t.Advance(args.Length > 3 ? double.Parse(args[3]) : 1200);
    foreach (var l in t.Log) Console.WriteLine($"T+{l.T,6:0.0}  {l.Kind,-8} {l.Text}");
    return 0;
}
if (cmd == "auto")
{
    var gc = GameContent.Load(Path.Combine("data", "core"));
    foreach (var role in gc.AutoRoles.Values.Where(r => args.Length < 2 || r.Id == args[1]))
    {
        var res = new BadWaters.Sim.Engineering.AutoDesigner(gc.Catalog).Design(role, "auto-" + role.Id, role.Name, gc.Designs, seed: args.Length > 2 ? int.Parse(args[2]) : 1);
        Console.WriteLine($"{role.Name}: {res.Design.Parts.Count} parts, score {res.Score}, cost {res.Evaluation.Cost:0}");
        Console.WriteLine("  " + string.Join(", ", res.Design.Parts.GroupBy(p => p.Part).Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key)));
        Console.WriteLine("  " + string.Join("  ", res.Evaluation.Budgets.Select(b => $"{b.Key} {b.Value.Used:0.#}/{b.Value.Available:0.#}")));
        Console.WriteLine("  " + string.Join("  ", res.Evaluation.Ratings.Where(r => role.Weights.ContainsKey(r.Key)).Select(r => $"{r.Key} {r.Value:0.#}")));
        foreach (var i in res.Evaluation.Issues) Console.WriteLine("  ! " + i.Text);
    }
    return 0;
}
if (cmd == "war") return FullWar.Run(args); // same as fullwar
if (cmd == "fullwar") return FullWar.Run(args);
if (cmd == "campaign")
{
    // campaign [scenario] [hours] [seed]: both sides run by their theatre commanders, headless
    var gc = GameContent.Load(Path.Combine("data", "core"));
    var sc = gc.Scenarios[args.Length > 1 ? args[1] : "jpn-chn"];
    var hours = args.Length > 2 ? double.Parse(args[2]) : 12;
    var seed = args.Length > 3 ? int.Parse(args[3]) : 1;
    string? Nation(string side) { var n = sc.Sides.GetValueOrDefault(side, ""); return gc.Nations.Values.FirstOrDefault(x => x.Name == n || x.Id == n)?.Id; }
    var t = BadWaters.Sim.Command.TheatreFactory.Create(sc, id => BadWaters.Sim.Theatre.SystemProfile.Build(gc.Catalog, gc.Designs[id]), gc.PresetInfo, gc.BaseTypes, side => gc.DoctrineFor(Nation(side)), seed, stores: gc.Stores, logistics: gc.Logistics);
    t.Adaptive = true;
    if (Environment.GetEnvironmentVariable("PROFILE") != null) t.Profile = new();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    for (var h = 0; h < hours; h++)
    {
        t.Advance(3600);
        foreach (var (side, c) in t.Commanders)
            if (c.Last != null && (h % 3 == 0 || h == hours - 1))
                Console.WriteLine($"T+{h + 1,3}h {sc.Sides[side],-8} pool {c.Last.PoolAircraft} (F{c.Last.Fighters} S{c.Last.Strikers}) · open ops {c.Last.OpenOps} · enemy air seen {c.Last.KnownEnemyAir} · SAMs known {c.Last.KnownSams} · {string.Join("; ", c.Last.Notes)}");
        if (t.Logistics is { } lg && ((h + 1) % 24 == 0 || h == hours - 1))
            foreach (var side in new[] { "blue", "red" })
            {
                var nodes = lg.Nodes.Values.Where(n => n.Side == side).ToList();
                var stock = string.Join(" · ", new[] { "avfuel", "diesel", "propellant", "spares" }.Select(i => $"{lg.ItemName(i)} {lg.Amount(i, nodes.Sum(n => lg.Get(n, i)))}"));
                var sh = lg.Shipments.Where(x => x.Side == side).ToList();
                Console.WriteLine($"  LOGISTICS T+{h + 1}h {sc.Sides[side]}: {stock} · shipments moving {sh.Count(x => x.State == BadWaters.Sim.Theatre.ShipmentState.Moving)}, delivered {t.Log.Count(x => x.Kind == "logistics" && x.Text.Contains("delivered") && nodes.Any(n => x.Text.EndsWith(n.Name + ".")))}, lost {t.Log.Count(x => x.Kind == "logistics" && x.Text.Contains("in transit"))} · dry points {nodes.Count(n => n.Short.Count > 0)}");
            }
    }
    Console.WriteLine($"{hours} h simulated in {sw.ElapsedMilliseconds} ms · tracks {t.Tracks.Count} shots {t.Shots.Count} log {t.Log.Count}");
    if (t.Profile != null) Console.WriteLine("profile: " + string.Join(", ", t.Profile.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value:0} ms")));
    foreach (var side in new[] { "blue", "red" })
    {
        var ops = t.Ops!.Ops.Where(o => o.Side == side).ToList();
        Console.WriteLine();
        Console.WriteLine($"{sc.Sides[side]}: {ops.Count} operations ({ops.Count(o => o.Status == BadWaters.Sim.Command.OpStatus.Complete)} complete, {ops.Count(o => o.Status == BadWaters.Sim.Command.OpStatus.Aborted)} aborted, {ops.Count(o => o.Open)} open) · aircraft lost {t.Air!.Destroyed.Count(id => sc.Units.First(u => u.Id == id).Side == side)}");
        foreach (var o in ops.Where(o => o.Rationale != "opening orders").Take(30)) Console.WriteLine($"  [{o.Status,-8}] {o.Name,-34} {string.Join("/", o.Purpose),-24} {o.Rationale}");
    }
    return 0;
}
if (cmd == "bills")
{
    var gc = GameContent.Load(Path.Combine("data", "core"));
    var br = new BadWaters.Sim.Economy.BillResolver(gc.Economy, gc.Catalog);
    foreach (var d in gc.Designs.Values.Take(40))
    {
        var (goods, _) = br.DesignBill(d);
        Console.WriteLine($"{d.Id,-16} bill value {goods.Sum(k => k.Value * gc.Economy.Goods[k.Key].Price):0}");
    }
    return 0;
}
if (cmd == "world")
{
    // world [months] [year] [shut-chokepoint]
    var gc = GameContent.Load(Path.Combine("data", "core"));
    foreach (var e in gc.Errors) Console.Error.WriteLine(e);
    var months = args.Length > 1 ? int.Parse(args[1]) : 12;
    var year = args.Length > 2 ? int.Parse(args[2]) : 2020;
    var routes = gc.Routes.Keys.ToDictionary(k => k, _ => new BadWaters.Sim.Trade.RouteState());
    if (args.Length > 3) foreach (var r in gc.Routes.Values.Where(r => r.Chokepoints.Contains(args[3]))) routes[r.Id].Disruption = 100;
    var ew = new BadWaters.Sim.Economy.EconomyWorld(gc.Economy, gc.Geo, gc.Catalog, gc.Designs, gc.Routes);
    var st = new BadWaters.Sim.Economy.EconomyState();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    ew.Start(st, gc.Nations.Keys, year, gc.TradeRegimes);
    Console.WriteLine($"Seeded {st.Nations.Count} nations, {st.Sites.Count} sites in {sw.ElapsedMilliseconds} ms; sanctions {st.Sanctions.Count}");
    for (var m = 0; m < months; m++)
    {
        sw.Restart();
        var ev = ew.RunMonth(st, routes);
        var ms = sw.ElapsedMilliseconds;
        foreach (var e in ev) Console.WriteLine("  EVENT " + e);
        if ((m + 1) % Math.Max(1, months / 6) == 0 || m == months - 1)
            Console.WriteLine($"month {m + 1} ({ms} ms): " + string.Join("  ", st.Nations.Values.OrderByDescending(n => n.Gdp).Select(n =>
                $"{n.Nation} {n.Gdp:0} ({BadWaters.Sim.Economy.MacroRules.YearOnYear(n):+0%;-0%;0%}) s{n.Stability:0}")));
    }
    return 0;
}
if (cmd == "econ")
{
    // econ NATION [months] [year] [design] [rate]
    var gc = GameContent.Load(Path.Combine("data", "core"));
    foreach (var e in gc.Errors) Console.Error.WriteLine(e);
    var nat = args.Length > 1 ? args[1] : "JPN";
    var months = args.Length > 2 ? int.Parse(args[2]) : 3;
    var year = args.Length > 3 ? int.Parse(args[3]) : 2020;
    var st = new BadWaters.Sim.Economy.EconomyState();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    new BadWaters.Sim.Economy.EconomySeeder(gc.Economy, gc.Geo, gc.Catalog, gc.Designs).Seed(st, nat, year);
    Console.WriteLine($"Seeded {nat} {year}: {st.Sites.Count} sites, {st.Sites.Values.Sum(s => s.Level)} levels in {sw.ElapsedMilliseconds} ms");
    foreach (var g in st.Sites.Values.GroupBy(s => s.Type).OrderByDescending(g => g.Sum(s => s.Level)).Take(25)) Console.Write($"{g.Key}×{g.Sum(s => s.Level)}  ");
    Console.WriteLine();
    if (args.Length > 4) st.Nations[nat].Programmes.Add(new BadWaters.Sim.Economy.Programme { Id = "p1", Design = args[4], RatePerMonth = args.Length > 5 ? double.Parse(args[5]) : 1 });
    var solver = new BadWaters.Sim.Economy.EconomySolver(gc.Economy, gc.Geo, gc.Catalog, gc.Designs);
    BadWaters.Sim.Economy.NationReport? rep = null;
    for (var m = 0; m < months; m++)
    {
        sw.Restart();
        rep = solver.RunMonth(st, nat);
        st.Month++;
        Console.WriteLine($"month {m + 1}: {sw.ElapsedMilliseconds} ms · value added {rep.ValueAdded:0} · energy {rep.EnergySupply:0}/{rep.EnergyDemand:0} · basket {rep.BasketSatisfaction:P0} · maintenance {rep.MaintenanceMet:P0} · imports {rep.ImportCost:0} · shortages {rep.Shortages.Count}");
    }
    if (rep != null)
    {
        Console.WriteLine("Bottlenecks:"); foreach (var b in rep.Bottlenecks) Console.WriteLine("  " + b);
        Console.WriteLine("Top shortages:"); foreach (var (g, q) in rep.Shortages.OrderByDescending(x => x.Value * gc.Economy.Goods[x.Key].Price).Take(10)) Console.WriteLine($"  {g} {q:0.#}");
        Console.WriteLine("Imports: " + string.Join(", ", rep.Imported.OrderByDescending(x => x.Value).Take(12).Select(x => $"{x.Key} {x.Value:0}")));
        Console.WriteLine("Produced: " + string.Join(", ", new[] { "coal", "copper-ore", "crude-oil", "iron-ore", "electricity", "steel", "spares-general" }.Select(g => $"{g} {rep.Produced.GetValueOrDefault(g):0}")));
        Console.WriteLine("Labour: " + string.Join(", ", rep.LabourFactor.Select(k => $"{k.Key} {k.Value:P0}")));
        foreach (var p in rep.Programmes) { Console.WriteLine($"Programme {p.Design}: {p.Built:0.##}/{p.Wanted} · {p.Limit}"); }
        var explain = Environment.GetEnvironmentVariable("EXPLAIN")?.Split(',') ?? rep.Shortages.Keys.Take(3).ToArray();
        foreach (var g in explain) foreach (var l in rep.Explain(g, gc.Economy)) Console.WriteLine(l);
        foreach (var g in explain) Console.WriteLine($"  plan {g}: " + (rep.Plan.TryGetValue(g, out var pv) ? $"req {pv.Req:0.#} stock {pv.Stock:0.#} net {pv.Net:0.#}" : "not reached"));
        foreach (var g in explain) Console.WriteLine($"  limits for {g}: " + string.Join("; ", rep.Limits.Where(l => gc.Economy.Processes[l.Process].Outputs.ContainsKey(g)).GroupBy(l => l.Process).Select(x => $"{x.Key} {x.Sum(y => y.Actual):0.#}/{x.Sum(y => y.Planned):0.#} {x.First().Reason}")));
    }
    return 0;
}
if (cmd != "validate")
{
    Console.Error.WriteLine("Usage: BadWaters.Cli validate [dataDir] [modDir...]");
    return 2;
}

var dataDir = args.Length > 1 ? args[1] : Path.Combine("data", "core");
var mods = args.Skip(2).ToArray();
var loader = new ContentLoader(dataDir, mods);
var nations = loader.Load<NationDef>("nations.json");
var dates = loader.Load<StartDateDef>("start_dates.json");
var people = loader.Load<PersonnelDef>("personnel.json");
var presets = loader.Load<PresetDef>("presets.json");

var ids = nations.Select(n => n.Id).ToHashSet();
foreach (var p in presets.Where(p => p.Nation != "" && !ids.Contains(p.Nation)))
    loader.Errors.Add($"presets.json #{p.Id}: unknown nation {p.Nation}");
foreach (var p in people.Where(p => p.CareerYears <= p.TrainingYears))
    loader.Errors.Add($"personnel.json #{p.Id}: career must be longer than training");

var content = GameContent.Load(dataDir, mods);
loader.Errors.AddRange(content.Errors);
var presetIds = presets.Select(p => p.Id).ToHashSet();
foreach (var sc in content.Scenarios.Values)
    foreach (var u in sc.Units.Where(u => !presetIds.Contains(u.Preset))) loader.Errors.Add($"scenario {sc.Id}: unit {u.Id} uses unknown preset {u.Preset}");
foreach (var p in presets)
    foreach (var (r, v) in p.Ratings)
    {
        if (!content.Catalog.Ratings.ContainsKey(r)) loader.Errors.Add($"presets.json #{p.Id}: unknown rating {r}");
        if (v < 0 || v > 10) loader.Errors.Add($"presets.json #{p.Id}: rating {r} must be 0-10");
    }
var eval = new BadWaters.Sim.Engineering.Evaluator(content.Catalog);
var maxTech = content.Tech.Keys.ToDictionary(k => k, k => content.Tech[k].MaxLevel);
foreach (var d in content.Designs.Values)
    foreach (var i in eval.Evaluate(d, maxTech).Issues) loader.Errors.Add($"designs.json #{d.Id}: {i.Text}");
Console.WriteLine($"Components {content.Catalog.Components.Count} · frames {content.Catalog.Frames.Count} · factories {content.Factories.Count} · trade routes {content.Routes.Count} · designs {content.Designs.Count}");
Console.WriteLine($"Nations {nations.Count} · start dates {dates.Count} · personnel categories {people.Count} · presets {presets.Count}");
Console.WriteLine($"Target classes under policy: {TargetPolicy.AllClasses.Count()}");
foreach (var e in loader.Errors) Console.Error.WriteLine($"ERROR {e}");
Console.WriteLine(loader.Errors.Count == 0 ? "Data OK." : $"{loader.Errors.Count} problem(s).");
return loader.Errors.Count == 0 ? 0 : 1;
