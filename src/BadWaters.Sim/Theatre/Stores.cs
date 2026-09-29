namespace BadWaters.Sim.Theatre;

// Aircraft stores (weapons, pods, tanks) on stations, and munition stocks at bases. Real names for identification
// only (data/core/theatre/stores.json); every number is an abstract 0-10 game rating, not a real characteristic.

public sealed record StoreDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>aam | bomb | agm | arm | asm | pod | tank</summary>
    public string Kind { get; init; } = "";
    /// <summary>Weight class 1 light, 2 medium, 3 heavy: a station takes stores up to its class.</summary>
    public int Cls { get; init; } = 1;
    public double Punch { get; init; }
    public double Accuracy { get; init; }
    public double Air { get; init; }
    /// <summary>Nations that field it (empty: everyone).</summary>
    public List<string> Nations { get; init; } = new();
    public bool AirToGround => Kind is "bomb" or "agm" or "arm" or "asm";
}

public sealed record StationDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public int MaxCls { get; init; } = 1;
    public List<string> Kinds { get; init; } = new();
    /// <summary>Stores per station (racks and bomb bays carry several).</summary>
    public int MaxQty { get; init; } = 1;
}

public sealed record LoadoutSlot(string Station, string Store, int Qty);

public sealed record AirframeTemplate
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public List<StationDef> Stations { get; init; } = new();
    /// <summary>Named default loadouts; a slot's Store may list alternatives "a|b|c" (the first the nation fields wins).</summary>
    public Dictionary<string, List<LoadoutSlot>> Loadouts { get; init; } = new();
}

public readonly record struct StoreLoad(string Store, int Qty);

public sealed class StoresCatalog
{
    public Dictionary<string, StoreDef> Stores { get; init; } = new();
    public Dictionary<string, AirframeTemplate> Templates { get; init; } = new();
    /// <summary>Aircraft preset → airframe template id.</summary>
    public Dictionary<string, string> Presets { get; init; } = new();
    /// <summary>Aircraft preset → nation (resolves store alternatives).</summary>
    public Dictionary<string, string> Nations { get; init; } = new();

    public AirframeTemplate? Template(string preset) => Presets.TryGetValue(preset, out var t) && Templates.TryGetValue(t, out var a) ? a : null;

    public static string LoadoutFor(Mission m) => m switch
    {
        Mission.Strike => "Strike", Mission.SEAD => "SEAD", Mission.Recon => "Recon", Mission.Escort or Mission.CAP => "Air-to-air", _ => "",
    };

    /// <summary>Stores this aircraft's nation can use, by id (alternatives resolved).</summary>
    public string? Resolve(string preset, string alternatives)
    {
        var nation = Nations.GetValueOrDefault(preset, "");
        return alternatives.Split('|').FirstOrDefault(o => Stores.TryGetValue(o, out var s) && (s.Nations.Count == 0 || s.Nations.Contains(nation)));
    }

    public bool Usable(string preset, StoreDef s) => s.Nations.Count == 0 || s.Nations.Contains(Nations.GetValueOrDefault(preset, ""));

    /// <summary>A named default loadout (or the mission's) for one aircraft: station → store × qty.</summary>
    public Dictionary<string, StoreLoad> Default(string preset, string loadout)
    {
        var res = new Dictionary<string, StoreLoad>();
        if (Template(preset) is not { } t || !t.Loadouts.TryGetValue(loadout, out var slots)) return res;
        foreach (var s in slots) if (Resolve(preset, s.Store) is { } store) res[s.Station] = new StoreLoad(store, s.Qty); // the nation fields none: empty station
        return res;
    }
    public Dictionary<string, StoreLoad> Default(string preset, Mission m) => Default(preset, LoadoutFor(m));

    /// <summary>Stores that fit a station (kind and weight class) and the aircraft's nation fields.</summary>
    public IEnumerable<StoreDef> Fits(string preset, StationDef st) =>
        Stores.Values.Where(s => st.Kinds.Contains(s.Kind) && s.Cls <= st.MaxCls && Usable(preset, s)).OrderBy(s => s.Kind).ThenBy(s => s.Name);

    public List<string> Validate(string preset, IReadOnlyDictionary<string, StoreLoad> load)
    {
        var issues = new List<string>();
        var t = Template(preset);
        foreach (var (st, l) in load)
        {
            var sd = t?.Stations.FirstOrDefault(x => x.Id == st);
            if (sd == null) { issues.Add($"no station {st}"); continue; }
            if (!Stores.TryGetValue(l.Store, out var s)) { issues.Add($"unknown store {l.Store}"); continue; }
            if (!sd.Kinds.Contains(s.Kind) || s.Cls > sd.MaxCls) issues.Add($"{s.Name} does not fit {sd.Name}");
            if (l.Qty > sd.MaxQty) issues.Add($"{sd.Name} carries at most {sd.MaxQty}");
        }
        return issues;
    }

    public string Describe(IReadOnlyDictionary<string, StoreLoad> load) =>
        load.Count == 0 ? "clean" : string.Join(", ", load.Values.Where(l => l.Qty > 0).GroupBy(l => l.Store).Select(g => $"{g.Sum(x => x.Qty)}× {Stores.GetValueOrDefault(g.Key)?.Name ?? g.Key}"));
}

/// <summary>Munitions held at each base (per store id). Arming a flight draws from its base.</summary>
public sealed class MunitionStocks
{
    public Dictionary<string, Dictionary<string, int>> ByBase { get; } = new();
    public int At(string baseId, string store) => ByBase.TryGetValue(baseId, out var d) ? d.GetValueOrDefault(store) : 0;
    public int Theatre(IEnumerable<string> bases, string store) => bases.Sum(b => At(b, store));
    public void Add(string baseId, string store, int n)
    {
        if (!ByBase.TryGetValue(baseId, out var d)) ByBase[baseId] = d = new();
        d[store] = Math.Max(0, d.GetValueOrDefault(store) + n);
    }
}
