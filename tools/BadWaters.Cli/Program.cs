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
