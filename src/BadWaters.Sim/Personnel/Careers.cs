namespace BadWaters.Sim.Personnel;

/// <summary>A scarce personnel category, loaded from data/core/personnel.json.</summary>
public sealed record PersonnelCategory(string Id, string Department, string Name, int CareerYears, int TrainingYears);

/// <summary>A cohort of people who entered service in the same year.</summary>
public sealed record Cohort(string CategoryId, int IntakeYear, int Count);

public static class Careers
{
    /// <summary>Cohorts whose career length has run out retire at the start of the given year.</summary>
    public static (List<Cohort> Remaining, int Retired) Retire(IEnumerable<Cohort> cohorts, IReadOnlyDictionary<string, PersonnelCategory> cats, int year)
    {
        var remaining = new List<Cohort>();
        var retired = 0;
        foreach (var c in cohorts)
        {
            if (!cats.TryGetValue(c.CategoryId, out var cat)) throw new KeyNotFoundException($"Unknown personnel category {c.CategoryId}.");
            if (year - c.IntakeYear >= cat.CareerYears) retired += c.Count;
            else remaining.Add(c);
        }
        return (remaining, retired);
    }
}
