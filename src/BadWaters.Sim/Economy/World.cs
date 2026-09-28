namespace BadWaters.Sim.Economy;

// Economic geography content (tools/gen_economy_world.py) and the saved economy state.
// Regions are public admin-level areas; deposits are regional endowments (game-scaled). Facilities are game
// entities the seeder places inside regions: never real plant locations.

public sealed record RegionDef(string Id, string Nation, string Name, double Lon, double Lat, bool Coastal, int Industry, bool Capital);
public sealed record RegionLink(string Id, string A, string B, string Mode = "land", double Capacity = 300);
public sealed record DepositDef(string Id, string Region, string Resource, int Size, double Reserve, double Grade = 1);
public sealed record SupplierDef(string Id, string Name, double Lon, double Lat, Dictionary<string, double> Exports);

public sealed record NationEconomyDef
{
    public required string Id { get; init; }
    public required string Nation { get; init; }
    public int Year { get; init; }
    /// <summary>Starting GDP in game billions (rounded public figure).</summary>
    public double Gdp { get; init; }
    /// <summary>Population in millions.</summary>
    public double Population { get; init; }
    /// <summary>Industrial competence tier 0-5 per sector.</summary>
    public Dictionary<string, int> Sectors { get; init; } = new();
    public Dictionary<string, double> EnergyMix { get; init; } = new();
    public double DefenceShare { get; init; } = 0.02;
    public double StockpileMonths { get; init; } = 3;
}

public sealed record SeedingRules
{
    public Dictionary<string, string[]> SectorFacilities { get; init; } = new();
    public string[] RequiresCoast { get; init; } = Array.Empty<string>();
    /// <summary>Basic industry sized to at least full demand whatever the sector tier.</summary>
    public string[] Basic { get; init; } = Array.Empty<string>();
    public Dictionary<string, int> MinTier { get; init; } = new();
    public Dictionary<string, string[]> TopTierExtras { get; init; } = new();
    public Dictionary<string, int> Always { get; init; } = new();
    public Dictionary<string, int> Capital { get; init; } = new();
    public Dictionary<string, int> Coastal { get; init; } = new();
    public int UniversityMinIndustry { get; init; } = 3;
    public Dictionary<string, string> EnergyMix { get; init; } = new();
    public double EnergyPerGdp { get; init; } = 0.06;
    public double JitterDeg { get; init; } = 1.2;
    /// <summary>General industrial output per GDP billion per year.</summary>
    public Dictionary<string, double> IndustrialBasket { get; init; } = new();
    /// <summary>Baseline defence output per sector: representative design and units per month per tier point.</summary>
    public Dictionary<string, SectorDesign> SectorDesigns { get; init; } = new();
}
public sealed record SectorDesign(string Design, double PerTier);

public sealed record EconomyRules
{
    public double DefenceShareDefault { get; init; } = 0.02;
    /// <summary>Nations whose sanctions can be global (every other seller must follow them).</summary>
    public string[] GlobalEnforcers { get; init; } = { "USA", "CHN" };
    public StabilityRules Stability { get; init; } = new();
    public GdpRules Gdp { get; init; } = new();
    public Dictionary<string, double> Transport { get; init; } = new();
    public MaintenanceRules Maintenance { get; init; } = new();
    /// <summary>Spares per fielded unit per month by frame kind (the kind's spares good, see bill templates).</summary>
    public Dictionary<string, double> FleetSpares { get; init; } = new();
    public double FleetDecay { get; init; } = 0.03;
    public WorldMarketRules WorldMarket { get; init; } = new();
    /// <summary>Civilian goods per million people per month.</summary>
    public Dictionary<string, double> CivilianBasket { get; init; } = new();
}
public sealed record StabilityRules { public double TrendWeight { get; init; } = 60; public double ShortageWeight { get; init; } = 25; public double UnemploymentWeight { get; init; } = 15; public double WarRally { get; init; } = 5; public double EmbargoPenalty { get; init; } = 5; public double MonthlyPull { get; init; } = 0.15; }
public sealed record GdpRules { public double CivilianShare { get; init; } = 0.85; public double TrendGrowthAnnual { get; init; } = 0.02; public double LossValueFactor { get; init; } = 1; }
public sealed record WorldMarketRules { public double DepthPerGdp { get; init; } = 0.004; public double PriceMarkup { get; init; } = 1.3; public string[] Restricted { get; init; } = Array.Empty<string>(); }
public sealed record MaintenanceRules { public double ReadinessFloor { get; init; } = 0.2; public double RestorePerMonth { get; init; } = 0.08; }

/// <summary>The geography half of the economy content.</summary>
public sealed class EconomyGeo
{
    public Dictionary<string, RegionDef> Regions { get; } = new();
    public List<RegionLink> Links { get; } = new();
    public Dictionary<string, DepositDef> Deposits { get; } = new();
    public Dictionary<string, NationEconomyDef> Nations { get; } = new();
    public Dictionary<string, SupplierDef> Suppliers { get; } = new();
    public SeedingRules Seeding { get; set; } = new();
    public EconomyRules Rules { get; set; } = new();
    public NationEconomyDef? For(string nation, int year) =>
        Nations.Values.Where(n => n.Nation == nation).OrderBy(n => Math.Abs(n.Year - year)).FirstOrDefault();
}

// ------------------------------------------------------------------ saved state

/// <summary>A facility instance: a game entity placed inside a region.</summary>
public sealed class Site
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Nation { get; init; }
    public required string Region { get; init; }
    public double Lon { get; init; }
    public double Lat { get; init; }
    public int Level { get; set; } = 1;
    /// <summary>Extraction sites work one deposit resource only (a coal mine doesn't mine copper). Empty = any process of its type.</summary>
    public string Resource { get; init; } = "";
    /// <summary>Wear, 0..1: falls without maintenance, capacity scales with it.</summary>
    public double Condition { get; set; } = 1;
    /// <summary>Battle / sabotage damage, 0..1.</summary>
    public double Damage { get; set; }
    /// <summary>Months of construction left (0 = operating).</summary>
    public double BuildLeft { get; set; }
    public bool Active => BuildLeft <= 0 && Damage < 0.999;
    /// <summary>Output factor this month from condition and damage.</summary>
    public double Health => Condition * (1 - Damage);
}

public sealed class Programme
{
    public required string Id { get; init; }
    public required string Design { get; init; }
    public double RatePerMonth { get; set; } = 1;
    public int Priority { get; set; } = 1;
    public double Built { get; set; }
    public double LastMonth { get; set; }
    public string Status { get; set; } = "";
}

public sealed class Contract
{
    public required string Id { get; init; }
    /// <summary>A supplier id (world market) or a nation id.</summary>
    public required string From { get; init; }
    public required string Good { get; init; }
    public double PerMonth { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class ConstructionProject
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Region { get; init; }
    /// <summary>Site being built or upgraded.</summary>
    public required string Site { get; init; }
    public Dictionary<string, double> Remaining { get; set; } = new();
    public double MonthsLeft { get; set; }
    /// <summary>Levels added to an existing site (0 = a new site, which is inactive until finished).</summary>
    public int AddLevels { get; set; }
}

/// <summary>Units a nation fields (from its production programmes): they wear and need spares like facilities do.</summary>
public sealed class FleetEntry
{
    public double Count { get; set; }
    /// <summary>0..1: share of the force that is mission-capable.</summary>
    public double Readiness { get; set; } = 1;
    /// <summary>Units lost in theatres so far.</summary>
    public double Lost { get; set; }
}

public sealed class NationEconomy
{
    public required string Nation { get; init; }
    public double Population { get; set; }
    /// <summary>Annualised GDP in game billions.</summary>
    public double Gdp { get; set; }
    public double BaseGdp { get; set; }
    /// <summary>Value added this month → GDP via the calibration factor set at start.</summary>
    public double GdpScale { get; set; } = 1;
    public List<double> GdpHistory { get; set; } = new();
    public List<double> VaHistory { get; set; } = new();
    public bool Calibrated { get; set; }
    public double Stability { get; set; } = 65;
    public double DefenceShare { get; set; } = 0.02;
    public bool AtWar { get; set; }
    public List<Programme> Programmes { get; set; } = new();
    public List<Contract> Contracts { get; set; } = new();
    /// <summary>Good → "auto" (domestic first), "domestic" (never import), "import" (import first).</summary>
    public Dictionary<string, string> SourcePolicy { get; set; } = new();
    /// <summary>Strategic reserve targets in months of consumption per good.</summary>
    public Dictionary<string, double> StockTargets { get; set; } = new();
    public List<ConstructionProject> Construction { get; set; } = new();
    /// <summary>Fielded units by design.</summary>
    public Dictionary<string, FleetEntry> Fleet { get; set; } = new();
    public Dictionary<string, int> Tech { get; set; } = new();
    /// <summary>Workers available per skill (same units as facility staffing).</summary>
    public Dictionary<string, double> Labour { get; set; } = new();
    /// <summary>Demand growth relative to the start (1 = starting size).</summary>
    public double GrowthIndex { get; set; } = 1;
    public Dictionary<string, double> LastConsumption { get; set; } = new();
    /// <summary>Realised output / planned per process last month (smoothed): plans adapt to what plants actually deliver.</summary>
    public Dictionary<string, double> Yield { get; set; } = new();
    /// <summary>Months of consumption held as working stock before plants throttle back (per-good StockTargets override).</summary>
    public double CoverMonths { get; set; } = 3;
    /// <summary>Months of defence spending carried as money (game billions) not yet spent.</summary>
    public double DefenceFunds { get; set; }
}

/// <summary>`By` refuses to sell `Good` ("*" = everything, "strategic" = restricted strategic goods) to `Target`.
/// Global: an enforcer's sanction that every other seller must follow too (except the other enforcer).</summary>
public sealed record Sanction(string By, string Target, string Good = Sanction.All, bool Global = false)
{
    public const string All = "*", Strategic = "strategic";
    public bool Covers(string good, ICollection<string> strategic) => Good == All || Good == good || (Good == Strategic && strategic.Contains(good));
}

public sealed class EconomyState
{
    public int Month { get; set; }
    public Dictionary<string, Site> Sites { get; set; } = new();
    /// <summary>Remaining reserve per deposit id.</summary>
    public Dictionary<string, double> Reserves { get; set; } = new();
    /// <summary>Stock held in each region (at its depots and plants), good → quantity.</summary>
    public Dictionary<string, Dictionary<string, double>> RegionStock { get; set; } = new();
    public Dictionary<string, NationEconomy> Nations { get; set; } = new();
    /// <summary>Who refuses to sell what to whom. See <see cref="Sanction"/>.</summary>
    public List<Sanction> Sanctions { get; set; } = new();

    /// <summary>
    /// Can `seller` (a nation, a world-market supplier, or "world") sell `good` to `buyer`? A normal sanction stops only
    /// the sanctioning nation selling. A global sanction by an enforcer (the USA or China, see economy_rules.json) stops
    /// every seller except the other enforcers: enforcers ignore each other's global sanctions.
    /// </summary>
    public bool Sanctioned(string seller, string buyer, string good, ICollection<string> strategic, ICollection<string> enforcers)
    {
        foreach (var x in Sanctions)
        {
            if (x.Target != buyer || !x.Covers(good, strategic)) continue;
            if (x.By == seller) return true;
            if (x.Global && enforcers.Contains(x.By) && !enforcers.Contains(seller)) return true;
        }
        return false;
    }

    /// <summary>Impose a sanction (idempotent). Global only has force for an enforcer.</summary>
    public void Sanction(string by, string target, string good = Economy.Sanction.All, bool global = false)
    {
        if (!Sanctions.Any(x => x.By == by && x.Target == target && x.Good == good && x.Global == global)) Sanctions.Add(new Sanction(by, target, good, global));
    }
    public void Lift(string by, string target, string good = Economy.Sanction.All) => Sanctions.RemoveAll(x => x.By == by && x.Target == target && x.Good == good);

    /// <summary>A trade war: both sides sanction each other on the listed goods (or everything).</summary>
    public void TradeWar(string a, string b, IEnumerable<string>? goods = null, bool global = false)
    {
        foreach (var g in goods ?? new[] { Economy.Sanction.All }) { Sanction(a, b, g, global); Sanction(b, a, g, global); }
    }

    /// <summary>Regions cut off (occupied, blockaded, links destroyed).</summary>
    public HashSet<string> IsolatedRegions { get; set; } = new();
    public HashSet<string> CutLinks { get; set; } = new();

    public Dictionary<string, double> Stock(string region) => RegionStock.TryGetValue(region, out var d) ? d : RegionStock[region] = new();


    /// <summary>
    /// A theatre strike at (lon, lat) on a site of a target class: damage the nearest facility of that nation within
    /// range whose own target class matches (else the nearest legitimate one). Protected facilities are never hit.
    /// Returns the economy site id damaged, or null.
    /// </summary>
    public string? DamageNear(string nation, double lon, double lat, string targetClass, double fraction, EconomyCatalog cat, double rangeKm = 250)
    {
        var cands = Sites.Values.Where(s => s.Nation == nation && cat.Facilities.TryGetValue(s.Type, out var f) && Policy.TargetPolicy.IsLegitimate(f.TargetClass))
            .Select(s => (s, d: Trade.TradeReachKm(lon, lat, s.Lon, s.Lat))).Where(x => x.d <= rangeKm).ToList();
        var pick = cands.Where(x => cat.Facilities[x.s.Type].TargetClass == targetClass).OrderBy(x => x.d).Select(x => x.s).FirstOrDefault()
                   ?? cands.OrderBy(x => x.d).Select(x => x.s).FirstOrDefault();
        if (pick == null) return null;
        ApplyDamage(pick.Id, fraction, cat);
        return pick.Id;
    }

    /// <summary>Damage a site (theatre strike, sabotage). Stock in its region is destroyed in proportion to the site's share of storage.</summary>
    public void ApplyDamage(string siteId, double fraction, EconomyCatalog cat)
    {
        if (!Sites.TryGetValue(siteId, out var s)) return;
        s.Damage = Math.Clamp(s.Damage + fraction, 0, 1);
        var storage = cat.Facilities.GetValueOrDefault(s.Type)?.Storage ?? 0;
        var regionStorage = Sites.Values.Where(x => x.Region == s.Region).Sum(x => (cat.Facilities.GetValueOrDefault(x.Type)?.Storage ?? 0) * x.Level);
        if (storage <= 0 || regionStorage <= 0) return;
        var lost = fraction * storage * s.Level / regionStorage;
        foreach (var k in Stock(s.Region).Keys.ToList()) Stock(s.Region)[k] *= 1 - lost;
    }
}
