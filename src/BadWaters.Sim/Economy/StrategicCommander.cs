namespace BadWaters.Sim.Economy;

/// <summary>A nation's strategic disposition (data/core/strategic/dispositions.json): tendencies, not scripts.</summary>
public sealed record Disposition
{
    public string Id { get; init; } = "default";
    public double BaseDefenceShare { get; init; } = 0.02;
    public double WarDefenceShare { get; init; } = 0.05;
    /// <summary>Answer sanctions with sanctions on strategic goods.</summary>
    public bool Reciprocate { get; init; } = true;
    public double BuildAggression { get; init; } = 1.0;
    /// <summary>Munition / system designs this nation keeps in production (its arsenal).</summary>
    public List<string> Programmes { get; init; } = new();
}

/// <summary>
/// The strategic AI: the same loop as the theatre commanders, at national scale, acting only through the player's
/// own tools. Each month it assesses its economy report, its losses and its situation, then adjusts the defence share,
/// keeps its arsenal in production (more after losses), orders construction for persistent shortages it can build,
/// raises stockpile targets at war, and answers sanctions. Deterministic.
/// </summary>
public sealed class StrategicCommander
{
    private readonly EconomyWorld _ew;
    private readonly Func<string, Disposition> _disp;
    private readonly Dictionary<string, Dictionary<string, int>> _shortStreak = new();
    private readonly Dictionary<string, double> _lostSeen = new();
    public Dictionary<string, List<string>> Decisions { get; } = new();

    public StrategicCommander(EconomyWorld ew, Func<string, Disposition> disposition) { _ew = ew; _disp = disposition; }

    public void Act(EconomyState st, string nation, NationReport rep, bool atWar)
    {
        var ne = st.Nations[nation];
        var d = _disp(nation);
        var log = Decisions.TryGetValue(nation, out var l) ? l : Decisions[nation] = new();
        void Note(string s) { log.Add($"month {st.Month}: {s}"); if (log.Count > 40) log.RemoveAt(0); }
        ne.AtWar = atWar;

        // 1. defence share drifts toward its war or peace level; eased if civilians go short
        var target = atWar ? d.WarDefenceShare : d.BaseDefenceShare;
        if (rep.BasketSatisfaction < 0.9) target *= 0.8;
        var share = ne.DefenceShare + (target - ne.DefenceShare) * 0.25;
        if (Math.Abs(share - ne.DefenceShare) > 0.0005) { if (Math.Abs(target - ne.DefenceShare) > 0.005) Note($"defence share {ne.DefenceShare:P1} → {share:P1}{(atWar ? " (at war)" : "")}"); ne.DefenceShare = share; }

        // 2. keep the arsenal in production; losses since last month raise the rate
        var lost = ne.Fleet.Values.Sum(f => f.Lost);
        var newLosses = lost - _lostSeen.GetValueOrDefault(nation);
        _lostSeen[nation] = lost;
        foreach (var design in d.Programmes.Where(_ew.Solver.HasDesign))
        {
            var p = ne.Programmes.FirstOrDefault(x => x.Design == design);
            var rate = (atWar ? 8 : 2) * (1 + Math.Min(3, newLosses / 10));
            if (p == null) { ne.Programmes.Add(new Programme { Id = $"ai-{design}", Design = design, RatePerMonth = rate, Priority = 2 }); Note($"started production of {design} at {rate:0.#}/month"); }
            else if (Math.Abs(p.RatePerMonth - rate) > 0.5) { p.RatePerMonth = rate; Note($"{design} production set to {rate:0.#}/month"); }
        }

        // 3. construction for goods short three months running that the nation has the technology to make
        var streak = _shortStreak.TryGetValue(nation, out var ss) ? ss : _shortStreak[nation] = new();
        foreach (var g in streak.Keys.Except(rep.Shortages.Keys).ToList()) streak.Remove(g);
        foreach (var g in rep.Shortages.Keys.Where(k => _ew.Catalog.Goods.ContainsKey(k))) streak[g] = streak.GetValueOrDefault(g) + 1;
        if (ne.Construction.Count < 3)
            foreach (var (g, n) in streak.Where(k => k.Value >= 3).OrderByDescending(k => k.Value * _ew.Catalog.Goods[k.Key].Price).ThenBy(k => k.Key, StringComparer.Ordinal).Take(1))
            {
                var proc = _ew.Catalog.Producers(g).FirstOrDefault(p => p.Deposit == null && p.Tech.All(t => ne.Tech.GetValueOrDefault(t.Key) >= t.Value));
                if (proc == null) continue;
                var region = _ew.Geo.Regions.Values.Where(r => r.Nation == nation).OrderByDescending(r => r.Industry).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
                if (region == null) continue;
                _ew.OrderConstruction(st, nation, proc.Facility, region.Id, (int)Math.Max(1, Math.Round(2 * d.BuildAggression)));
                streak[g] = 0;
                Note($"ordered {_ew.Catalog.Facilities[proc.Facility].Name} in {region.Name}: {_ew.Catalog.Goods[g].Name} short for {n} months");
            }

        // 4. war reserves
        foreach (var g in new[] { "energetics", "spares-general", "diesel", "jet-fuel" })
        {
            var want = atWar ? 6 : ne.CoverMonths;
            if (ne.StockTargets.GetValueOrDefault(g, ne.CoverMonths) != want) { ne.StockTargets[g] = want; if (atWar) Note($"war reserve for {g}: {want} months"); }
        }

        // 5. answer sanctions: whoever sanctions us gets strategic export controls back
        if (d.Reciprocate)
            foreach (var by in st.Sanctions.Where(x => x.Target == nation && st.Nations.ContainsKey(x.By)).Select(x => x.By).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList())
                if (!st.Sanctions.Any(x => x.By == nation && x.Target == by)) { st.Sanction(nation, by, Sanction.Strategic); Note($"answered {by}'s sanctions with export controls"); }
    }
}
