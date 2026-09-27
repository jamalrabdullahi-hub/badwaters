using BadWaters.Data;
using BadWaters.Sim.Policy;

// Bad Waters command-line tools.
//   validate [dataDir] [modDir...]   check that all content loads and cross-references resolve
var cmd = args.Length > 0 ? args[0] : "validate";
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

Console.WriteLine($"Nations {nations.Count} · start dates {dates.Count} · personnel categories {people.Count} · presets {presets.Count}");
Console.WriteLine($"Target classes under policy: {TargetPolicy.AllClasses.Count()}");
foreach (var e in loader.Errors) Console.Error.WriteLine($"ERROR {e}");
Console.WriteLine(loader.Errors.Count == 0 ? "Data OK." : $"{loader.Errors.Count} problem(s).");
return loader.Errors.Count == 0 ? 0 : 1;
