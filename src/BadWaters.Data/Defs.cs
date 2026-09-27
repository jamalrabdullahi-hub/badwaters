namespace BadWaters.Data;

/// <summary>A playable (or AI-only) nation. Display names can differ by start date, e.g. the Soviet Union in 1980.</summary>
public sealed record NationDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Tier { get; init; } = "regional"; // "great" or "regional"
    public string Color { get; init; } = "#888888";
    public int[] StartYears { get; init; } = { 1980, 2000, 2020 };
    public Dictionary<string, string> NameByYear { get; init; } = new();
    public string Summary { get; init; } = "";

    public bool AvailableIn(int year) => StartYears.Contains(year);
    public string DisplayName(int year) => NameByYear.TryGetValue(year.ToString(), out var n) ? n : Name;
}

public sealed record PersonnelDef
{
    public required string Id { get; init; }
    public required string Department { get; init; }
    public required string Name { get; init; }
    public int CareerYears { get; init; }
    public int TrainingYears { get; init; }
}

public sealed record StartDateDef
{
    public required string Id { get; init; }
    public int Year { get; init; }
    public string Title { get; init; } = "";
    public string Blurb { get; init; } = "";
}

/// <summary>A ready-made design shown in a designer's preset list. Names are game designations, editable in JSON.</summary>
public sealed record PresetDef
{
    public required string Id { get; init; }
    public required string Designer { get; init; } // "ship", "aircraft", "ordnance", "base"
    public required string Name { get; init; }
    public string Archetype { get; init; } = "";
    public string Nation { get; init; } = "";
    public int FromYear { get; init; }
}
