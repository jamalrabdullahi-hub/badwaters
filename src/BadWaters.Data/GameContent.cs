using BadWaters.Sim.Engineering;
using BadWaters.Sim.Industry;
using BadWaters.Sim.Trade;

namespace BadWaters.Data;

public sealed record TechFieldDef(string Id, string Name, int MaxLevel);
public sealed record GoodDef(string Id, string Name);
public sealed record TheatreDef(string Id, string Name);

/// <summary>All loaded content, with cross-reference validation. Everything comes from JSON; nothing is hardcoded.</summary>
public sealed class GameContent
{
    public Catalog Catalog { get; } = new();
    public Dictionary<string, TechFieldDef> Tech { get; } = new();
    public Dictionary<string, GoodDef> Goods { get; } = new();
    public Dictionary<string, FactoryDef> Factories { get; } = new();
    public Dictionary<string, RouteDef> Routes { get; } = new();
    public Dictionary<string, TheatreDef> Theatres { get; } = new();
    public Dictionary<string, DesignDef> Designs { get; } = new();
    public List<string> Errors { get; } = new();

    public static GameContent Load(string coreDir, IEnumerable<string>? mods = null)
    {
        var l = new ContentLoader(coreDir, mods);
        var g = new GameContent();
        foreach (var t in l.Load<TechFieldDef>("tech_fields.json")) g.Tech[t.Id] = t;
        foreach (var x in l.Load<GoodDef>("goods.json")) g.Goods[x.Id] = x;
        foreach (var x in l.Load<FactoryDef>("factories.json")) g.Factories[x.Id] = x;
        foreach (var x in l.Load<RouteDef>("trade_routes.json")) g.Routes[x.Id] = x;
        foreach (var x in l.Load<TheatreDef>("theatres.json")) g.Theatres[x.Id] = x;
        foreach (var x in l.Load<RatingRule>("engineering/ratings.json")) g.Catalog.Ratings[x.Id] = x;
        foreach (var x in l.Load<ComponentTemplate>("components.json")) g.Catalog.Components[x.Id] = x;
        foreach (var f in new[] { "ships", "aircraft", "munitions", "ground" })
            foreach (var x in l.Load<FrameDef>($"frames/{f}.json")) g.Catalog.Frames[x.Id] = x;
        foreach (var x in l.Load<DesignDef>("designs.json")) g.Designs[x.Id] = x;
        g.Errors.AddRange(l.Errors);
        g.Validate();
        return g;
    }

    private void Validate()
    {
        void CheckTech(string where, Dictionary<string, int> req)
        {
            foreach (var (f, lvl) in req)
            {
                if (!Tech.TryGetValue(f, out var t)) Errors.Add($"{where}: unknown tech field {f}");
                else if (lvl > t.MaxLevel) Errors.Add($"{where}: {f} level {lvl} exceeds max {t.MaxLevel}");
            }
        }
        foreach (var c in Catalog.Components.Values)
        {
            if (!Families.All.Contains(c.Family)) Errors.Add($"component {c.Id}: unknown family {c.Family}");
            if (c.Factory != null && !Factories.ContainsKey(c.Factory)) Errors.Add($"component {c.Id}: unknown factory {c.Factory}");
            foreach (var r in c.Adds.Keys) if (!Catalog.Ratings.ContainsKey(r)) Errors.Add($"component {c.Id}: unknown rating {r}");
            CheckTech($"component {c.Id}", c.Requires);
        }
        var ports = Catalog.Frames.Values.SelectMany(f => f.Ports.Keys).Concat(Catalog.Components.Values.SelectMany(c => c.Offers)).ToHashSet();
        foreach (var c in Catalog.Components.Values)
        {
            if (c.Port != null && !Catalog.Frames.Values.Any(f => f.Ports.ContainsKey(c.Port))) Errors.Add($"component {c.Id}: no frame has port {c.Port}");
            foreach (var n in c.Needs) if (!ports.Contains(n)) Errors.Add($"component {c.Id}: nothing provides {n}");
        }
        foreach (var f in Catalog.Frames.Values)
        {
            if (f.Factory != null && !Factories.ContainsKey(f.Factory)) Errors.Add($"frame {f.Id}: unknown factory {f.Factory}");
            CheckTech($"frame {f.Id}", f.Requires);
        }
        foreach (var fac in Factories.Values)
        {
            if (fac.Produces != "" && !Goods.ContainsKey(fac.Produces)) Errors.Add($"factory {fac.Id}: produces unknown good {fac.Produces}");
            foreach (var g in fac.InputsPerWork.Keys) if (!Goods.ContainsKey(g)) Errors.Add($"factory {fac.Id}: unknown input {g}");
        }
        foreach (var r in Routes.Values) foreach (var t in r.Theatres) if (!Theatres.ContainsKey(t)) Errors.Add($"route {r.Id}: unknown theatre {t}");
        foreach (var d in Designs.Values)
        {
            if (!Catalog.Frames.ContainsKey(d.Frame)) Errors.Add($"design {d.Id}: unknown frame {d.Frame}");
            foreach (var c in d.Components) if (!Catalog.Components.ContainsKey(c)) Errors.Add($"design {d.Id}: unknown component {c}");
        }
    }
}
