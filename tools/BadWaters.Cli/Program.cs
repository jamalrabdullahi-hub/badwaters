using BadWaters.Data;
using BadWaters.Sim.Policy;

// Bad Waters command-line tools.
//   validate [dataDir] [modDir...]   check that all content loads and cross-references resolve
//   trial [seed]                     run the intercept trial scenario with both sides on AI and print the log
var cmd = args.Length > 0 ? args[0] : "validate";
if (cmd == "trial")
{
    var gc = GameContent.Load(Path.Combine("data", "core"));
    var t = new BadWaters.Sim.Theatre.InterceptTrial(gc.Scenarios["intercept-trial"],
        id => BadWaters.Sim.Theatre.SystemProfile.Build(gc.Catalog, gc.Designs[id]), args.Length > 1 ? int.Parse(args[1]) : 1);
    t.Advance(1200);
    foreach (var l in t.Log) Console.WriteLine($"T+{l.T,6:0.0}  {l.Kind,-8} {l.Text}");
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
