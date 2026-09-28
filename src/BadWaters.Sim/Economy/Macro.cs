namespace BadWaters.Sim.Economy;

/// <summary>
/// GDP, the defence budget and stability. GDP follows the value the economy actually produces, calibrated to the start
/// date's figure; defence funds accrue at the defence share (2% default). Stability is an indicator only: there is no
/// collapse mechanism. Sanctions work by removing sellers, not by triggering crises.
/// </summary>
public sealed class MacroRules
{
    public const int WarmUpMonths = 12;
    private readonly EconomyRules _r;
    public MacroRules(EconomyRules r) => _r = r;

    public static bool UnderSanctions(EconomyState st, string nation) => st.Sanctions.Any(x => x.Target == nation);

    /// <summary>12-month GDP change (e.g. -0.1 = -10%).</summary>
    public static double YearOnYear(NationEconomy ne) =>
        ne.GdpHistory.Count < 13 ? 0 : ne.GdpHistory[^1] / Math.Max(1e-9, ne.GdpHistory[^13]) - 1;

    /// <summary>Update GDP, budget, stability and crisis state. Returns an event text when a crisis starts or escalates.</summary>
    public string? Apply(EconomyState st, NationEconomy ne, NationReport rep)
    {
        // GDP: annualised value added, calibrated once after warm-up so the start date's figure is the baseline
        ne.VaHistory.Add(rep.ValueAdded);
        if (ne.VaHistory.Count > 36) ne.VaHistory.RemoveAt(0);
        if (!ne.Calibrated)
        {
            if (ne.VaHistory.Count >= WarmUpMonths)
            {
                var baseVa = ne.VaHistory.TakeLast(3).Average();
                ne.GdpScale = ne.BaseGdp / Math.Max(1e-9, baseVa * 12);
                ne.Calibrated = true;
            }
            ne.GdpHistory.Add(ne.BaseGdp);
        }
        else
        {
            var instant = rep.ValueAdded * 12 * ne.GdpScale * (1 - rep.ShortfallLoss);
            ne.Gdp = 0.85 * ne.Gdp + 0.15 * instant;
            ne.GdpHistory.Add(ne.Gdp);
            // trend growth feeds next month's demand while the economy is sound
            if (ne.Stability > 40 && rep.BasketSatisfaction > 0.9) ne.GrowthIndex *= 1 + _r.Gdp.TrendGrowthAnnual / 12;
        }
        if (ne.GdpHistory.Count > 60) ne.GdpHistory.RemoveAt(0);

        // defence budget: the defence share of GDP accrues as funds that programmes spend
        ne.DefenceFunds += ne.Gdp * ne.DefenceShare / 12;

        // stability drifts toward a target set by the growth trend, civilian shortages, war and embargo
        var yoy = YearOnYear(ne);
        var embargo = UnderSanctions(st, ne.Nation);
        var s = _r.Stability;
        var target = 65 + s.TrendWeight * Math.Clamp(yoy, -0.4, 0.2) * 2 - s.ShortageWeight * (1 - rep.BasketSatisfaction) * 2
                     + (ne.AtWar ? s.WarRally : 0) - (embargo ? s.EmbargoPenalty : 0);
        ne.Stability = Math.Clamp(ne.Stability + (target - ne.Stability) * s.MonthlyPull, 0, 100);

        return null;
    }
}
