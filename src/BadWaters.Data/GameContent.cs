using BadWaters.Sim.Engineering;
using BadWaters.Sim.Industry;
using BadWaters.Sim.Trade;
using BadWaters.Sim.Theatre;
using BadWaters.Sim.Policy;

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
    public Dictionary<string, ScenarioDef> Scenarios { get; } = new();
    public Dictionary<string, PresetDef> Presets { get; } = new();
    public Dictionary<string, BaseTypeDef> BaseTypes { get; } = new();
    public Dictionary<string, ComponentArchetype> Archetypes { get; } = new();
    public Dictionary<string, AutoRole> AutoRoles { get; } = new();
    public Dictionary<string, ChokepointDef> Chokepoints { get; } = new();
    public BadWaters.Sim.Economy.EconomyCatalog Economy { get; } = new();
    public BadWaters.Sim.Economy.EconomyGeo Geo { get; } = new();
    public Dictionary<string, NationDef> Nations { get; } = new();

    /// <summary>A preset as the theatre sees it (name + 0–10 ratings).</summary>
    private void ValidateEconomy()
    {
        var e = Economy;
        bool Good(string g) => e.Goods.ContainsKey(g);
        foreach (var r in e.Resources.Values) if (!Good(r.RawGood)) Errors.Add($"resource {r.Id}: unknown raw good {r.RawGood}");
        foreach (var p in e.Processes.Values)
        {
            if (!e.Facilities.ContainsKey(p.Facility)) Errors.Add($"process {p.Id}: unknown facility {p.Facility}");
            foreach (var g in p.Inputs.Keys.Concat(p.Outputs.Keys)) if (!Good(g)) Errors.Add($"process {p.Id}: unknown good {g}");
            if (p.Tooling != null && !Good(p.Tooling)) Errors.Add($"process {p.Id}: unknown tooling {p.Tooling}");
            if (p.Deposit != null && !e.Resources.ContainsKey(p.Deposit)) Errors.Add($"process {p.Id}: unknown deposit resource {p.Deposit}");
            foreach (var t in p.Tech.Keys) if (!Tech.ContainsKey(t)) Errors.Add($"process {p.Id}: unknown tech field {t}");
            foreach (var sub in p.Substitutes) if (!Good(sub.Alt) || !p.Inputs.ContainsKey(sub.Good)) Errors.Add($"process {p.Id}: bad substitute {sub.Good}->{sub.Alt}");
        }
        foreach (var f in e.Facilities.Values)
        {
            foreach (var g in f.Build.Keys.Concat(f.Spares.Keys)) if (!Good(g)) Errors.Add($"facility {f.Id}: unknown good {g}");
            if (!BadWaters.Sim.Policy.TargetPolicy.IsKnown(f.TargetClass)) Errors.Add($"facility {f.Id}: unclassified target class {f.TargetClass}");
        }
        foreach (var g in e.Goods.Keys) if (e.Producers(g).Count == 0) Errors.Add($"good {g}: nothing produces it");
        foreach (var (fam, b) in e.Bills.Families)
        {
            if (b.Facility != "" && !e.Facilities.ContainsKey(b.Facility)) Errors.Add($"bill family {fam}: unknown facility {b.Facility}");
            foreach (var g in b.Goods.Keys) if (!Good(g)) Errors.Add($"bill family {fam}: unknown good {g}");
        }
        foreach (var c in Catalog.Components.Values) if (!e.Bills.Families.ContainsKey(c.Family)) Errors.Add($"component {c.Id}: no bill template for family {c.Family}");
        foreach (var c in new BadWaters.Sim.Economy.BillResolver(e, Catalog).Cycles()) Errors.Add($"good {c}: its supply chain loops back on itself");
        var geo = Geo;
        foreach (var r in geo.Regions.Values) if (!Nations.ContainsKey(r.Nation)) Errors.Add($"region {r.Id}: unknown nation {r.Nation}");
        foreach (var k in geo.Links) if (!geo.Regions.ContainsKey(k.A) || !geo.Regions.ContainsKey(k.B)) Errors.Add($"region link {k.Id}: unknown region");
        foreach (var d in geo.Deposits.Values) { if (!geo.Regions.ContainsKey(d.Region)) Errors.Add($"deposit {d.Id}: unknown region"); if (!e.Resources.ContainsKey(d.Resource)) Errors.Add($"deposit {d.Id}: unknown resource {d.Resource}"); }
        foreach (var sp in geo.Suppliers.Values) foreach (var g in sp.Exports.Keys) if (!Good(g)) Errors.Add($"supplier {sp.Id}: unknown good {g}");
        foreach (var n in geo.Nations.Values) if (!Nations.ContainsKey(n.Nation)) Errors.Add($"nation economy {n.Id}: unknown nation");
        foreach (var t in geo.Seeding.SectorFacilities.Values.SelectMany(x => x).Concat(geo.Seeding.Always.Keys).Concat(geo.Seeding.Capital.Keys).Concat(geo.Seeding.Coastal.Keys).Concat(geo.Seeding.EnergyMix.Values))
            if (!e.Facilities.ContainsKey(t)) Errors.Add($"seeding: unknown facility type {t}");
        foreach (var (sec, sd) in geo.Seeding.SectorDesigns) if (!Designs.ContainsKey(sd.Design)) Errors.Add($"seeding sector {sec}: unknown design {sd.Design}");
        foreach (var g in geo.Seeding.IndustrialBasket.Keys.Concat(geo.Rules.CivilianBasket.Keys)) if (!Good(g)) Errors.Add($"basket: unknown good {g}");
    }

    public PresetInfo? PresetInfo(string id) => Presets.TryGetValue(id, out var p) ? new PresetInfo(p.Id, p.Name, p.Ratings) : null;
    public List<string> Errors { get; } = new();

    public static GameContent Load(string coreDir, IEnumerable<string>? mods = null)
    {
        var l = new ContentLoader(coreDir, mods);
        var g = new GameContent();
        foreach (var t in l.Load<TechFieldDef>("tech_fields.json")) g.Tech[t.Id] = t;
        foreach (var x in l.Load<GoodDef>("goods.json")) g.Goods[x.Id] = x;
        foreach (var x in l.Load<FactoryDef>("factories.json")) g.Factories[x.Id] = x;
        foreach (var x in l.Load<RouteDef>("trade_routes.json")) g.Routes[x.Id] = x;
        foreach (var x in l.Load<ChokepointDef>("chokepoints.json")) g.Chokepoints[x.Id] = x;
        foreach (var x in l.Load<BadWaters.Sim.Economy.ResourceDef>("economy/resources.json")) g.Economy.Resources[x.Id] = x;
        foreach (var x in l.Load<BadWaters.Sim.Economy.GoodSpec>("economy/goods.json")) g.Economy.Goods[x.Id] = x;
        foreach (var x in l.Load<BadWaters.Sim.Economy.ProcessDef>("economy/processes.json")) g.Economy.Processes[x.Id] = x;
        foreach (var x in l.Load<BadWaters.Sim.Economy.FacilityTypeDef>("economy/facility_types.json")) g.Economy.Facilities[x.Id] = x;
        g.Economy.Bills = l.LoadObject<BadWaters.Sim.Economy.BillTemplates>("economy/bill_templates.json") ?? g.Economy.Bills;
        foreach (var x in l.Load<NationDef>("nations.json")) g.Nations[x.Id] = x;
        foreach (var x in l.Load<BadWaters.Sim.Economy.RegionDef>("economy/regions.json")) g.Geo.Regions[x.Id] = x;
        g.Geo.Links.AddRange(l.Load<BadWaters.Sim.Economy.RegionLink>("economy/region_links.json"));
        foreach (var x in l.Load<BadWaters.Sim.Economy.DepositDef>("economy/deposits.json")) g.Geo.Deposits[x.Id] = x;
        foreach (var x in l.Load<BadWaters.Sim.Economy.NationEconomyDef>("economy/nation_economies.json")) g.Geo.Nations[x.Id] = x;
        foreach (var x in l.Load<BadWaters.Sim.Economy.SupplierDef>("economy/suppliers.json")) g.Geo.Suppliers[x.Id] = x;
        g.Geo.Seeding = l.LoadObject<BadWaters.Sim.Economy.SeedingRules>("economy/seeding.json") ?? g.Geo.Seeding;
        g.Geo.Rules = l.LoadObject<BadWaters.Sim.Economy.EconomyRules>("economy/economy_rules.json") ?? g.Geo.Rules;
        foreach (var x in l.Load<TheatreDef>("theatres.json")) g.Theatres[x.Id] = x;
        foreach (var x in l.Load<RatingRule>("engineering/ratings.json")) g.Catalog.Ratings[x.Id] = x;
        foreach (var x in l.Load<ComponentTemplate>("components.json")) g.Catalog.Components[x.Id] = x;
        foreach (var f in new[] { "ships", "aircraft", "munitions", "ground" })
            foreach (var x in l.Load<FrameDef>($"frames/{f}.json")) g.Catalog.Frames[x.Id] = x;
        foreach (var x in l.Load<VisualPart>("parts.json")) g.Catalog.Parts[x.Id] = x;
        foreach (var x in l.Load<CrewRule>("engineering/crew.json")) g.Catalog.Crew[x.Id] = x;
        foreach (var x in l.Load<DesignDef>("designs.json")) g.Designs[x.Id] = x;
        foreach (var x in l.Load<ScenarioDef>("scenarios.json")) g.Scenarios[x.Id] = x;
        foreach (var x in l.Load<PresetDef>("presets.json")) g.Presets[x.Id] = x;
        foreach (var x in l.Load<BaseTypeDef>("base_types.json")) g.BaseTypes[x.Id] = x;
        foreach (var x in l.Load<ComponentArchetype>("component_archetypes.json")) g.Archetypes[x.Id] = x;
        foreach (var x in l.Load<AutoRole>("auto_roles.json")) g.AutoRoles[x.Id] = x;
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
        foreach (var sc in Scenarios.Values)
        {
            foreach (var st in sc.Sites) if (!TargetPolicy.IsKnown(st.Class)) Errors.Add($"scenario {sc.Id}: site {st.Id} has unclassified class {st.Class}");
            foreach (var b in sc.Batteries) foreach (var d in new[] { b.Radar, b.Launcher, b.Interceptor }) if (!Designs.ContainsKey(d)) Errors.Add($"scenario {sc.Id}: unknown design {d}");
            foreach (var la in sc.Launchers) if (!Designs.ContainsKey(la.Missile)) Errors.Add($"scenario {sc.Id}: unknown design {la.Missile}");
            foreach (var u in sc.Units) if (!sc.Sides.ContainsKey(u.Side)) Errors.Add($"scenario {sc.Id}: unit {u.Id} has unknown side {u.Side}");
            foreach (var u in sc.Units) if (!Presets.ContainsKey(u.Preset)) Errors.Add($"scenario {sc.Id}: unit {u.Id} uses unknown preset {u.Preset}");
            foreach (var st in sc.Sites.Where(x => x.Type != "" && !BaseTypes.ContainsKey(x.Type))) Errors.Add($"scenario {sc.Id}: site {st.Id} has unknown base type {st.Type}");
            foreach (var e in sc.Ato.Where(e => e.Launcher != "" && sc.Launchers.All(l => l.Id != e.Launcher))) Errors.Add($"scenario {sc.Id}: ATO uses unknown launcher {e.Launcher}");
            foreach (var e in sc.Ato)
            {
                foreach (var f in e.Flights) if (!Presets.ContainsKey(f.Preset)) Errors.Add($"scenario {sc.Id}: ATO uses unknown preset {f.Preset}");
                if (e.Target != "" && sc.Sites.FirstOrDefault(x => x.Id == e.Target) is { } t && !TargetPolicy.IsLegitimate(t.Class)) Errors.Add($"scenario {sc.Id}: ATO targets protected site {e.Target}");
            }
            foreach (var x in sc.Script)
            {
                if (sc.Launchers.All(la => la.Id != x.Launcher)) Errors.Add($"scenario {sc.Id}: script uses unknown launcher {x.Launcher}");
                var t = sc.Sites.FirstOrDefault(st => st.Id == x.Target);
                if (t == null) Errors.Add($"scenario {sc.Id}: script targets unknown site {x.Target}");
                else if (!TargetPolicy.IsLegitimate(t.Class)) Errors.Add($"scenario {sc.Id}: script targets protected site {x.Target}");
            }
        }
        ValidateEconomy();
        foreach (var rt in Routes.Values)
        {
            foreach (var c in rt.Chokepoints) if (!Chokepoints.ContainsKey(c)) Errors.Add($"route {rt.Id}: unknown chokepoint {c}");
            if (rt.Mode is not ("sea" or "land")) Errors.Add($"route {rt.Id}: mode must be sea or land");
        }
        foreach (var r in AutoRoles.Values)
        {
            if (!Catalog.Frames.ContainsKey(r.Frame)) Errors.Add($"auto role {r.Id}: unknown frame {r.Frame}");
            if (r.BaseDesign != null && !Designs.ContainsKey(r.BaseDesign)) Errors.Add($"auto role {r.Id}: unknown base design {r.BaseDesign}");
            foreach (var c in r.Components) if (!Catalog.Components.ContainsKey(c)) Errors.Add($"auto role {r.Id}: unknown component {c}");
            foreach (var m in r.Must.Where(m => m.StartsWith("part:"))) if (!Catalog.Parts.ContainsKey(m[5..])) Errors.Add($"auto role {r.Id}: unknown part {m[5..]}");
            foreach (var w in r.Weights.Keys) if (!Catalog.Ratings.ContainsKey(w)) Errors.Add($"auto role {r.Id}: unknown rating {w}");
        }
        foreach (var ar in Archetypes.Values)
            if (ar.Visual != "" && !Catalog.Parts.ContainsKey(ar.Visual)) Errors.Add($"archetype {ar.Id}: unknown visual part {ar.Visual}");
        foreach (var pt in Catalog.Parts.Values)
            if (pt.Component != null && !Catalog.Components.ContainsKey(pt.Component)) Errors.Add($"part {pt.Id}: unknown component {pt.Component}");
        foreach (var d in Designs.Values)
        {
            if (!Catalog.Frames.ContainsKey(d.Frame)) Errors.Add($"design {d.Id}: unknown frame {d.Frame}");
            foreach (var c in d.Components) if (!Catalog.Components.ContainsKey(c)) Errors.Add($"design {d.Id}: unknown component {c}");
        }
    }
}
