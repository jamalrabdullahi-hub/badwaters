using BadWaters.Sim.Theatre;

namespace BadWaters.Sim.Economy;

// National stockpiles behind theatre logistics: a theatre can only hold munitions, fuel and spares the nation has.
// Aircraft stores and missile rounds are Fleet entries ("store:<id>", design id); fuel and spares are regional goods.
// Starting inventories and the transport pool are game numbers derived from the economy, not real inventories.

public static class NationalMunitions
{
    /// <summary>Theatre supply item → economy good (fuel, spares).</summary>
    public static readonly Dictionary<string, string> Goods = new() { ["avfuel"] = "jet-fuel", ["diesel"] = "diesel", ["spares"] = "spares-general", ["propellant"] = "energetics" };

    public static string FleetKey(string item) => item.StartsWith("store:") ? item : item.StartsWith("design:") ? item[7..] : "";

    /// <summary>
    /// Starting inventories: every store a nation's aircraft can use (scaled by the economy's size), and five loads of
    /// every missile and interceptor the nation deploys in any theatre scenario.
    /// </summary>
    public static void Seed(EconomyState st, StoresCatalog? stores, IEnumerable<ScenarioDef> scenarios, Func<string, string?> nationOfSide)
    {
        foreach (var (nation, ne) in st.Nations)
        {
            var scale = Math.Clamp(ne.Gdp / 1500, 0.3, 4);
            if (stores != null)
                foreach (var s in stores.Stores.Values.Where(s => s.Nations.Contains(nation) || (s.Nations.Count == 0 && stores.Nations.ContainsValue(nation))))
                    Add(ne, "store:" + s.Id, Math.Round((s.Kind is "pod" or "tank" ? 150 : s.Cls switch { 1 => 600, 2 => 300, _ => 120 }) * scale));
        }
        foreach (var sc in scenarios)
            foreach (var (side, name) in sc.Sides)
            {
                if (nationOfSide(name) is not { } n || !st.Nations.TryGetValue(n, out var ne)) continue;
                foreach (var l in sc.Launchers.Where(l => l.Side == side)) Add(ne, l.Missile, l.Stock * 5);
                foreach (var b in sc.Batteries.Where(b => b.Side == side)) Add(ne, b.Interceptor, b.Stock * 5);
            }
    }

    private static void Add(NationEconomy ne, string key, double n)
    {
        var fe = ne.Fleet.TryGetValue(key, out var x) ? x : ne.Fleet[key] = new FleetEntry();
        fe.Count += n;
    }

    public static double Held(NationEconomy ne, string key) => ne.Fleet.TryGetValue(key, out var f) ? f.Count : 0;
    public static void Take(NationEconomy ne, string key, double n) { if (ne.Fleet.TryGetValue(key, out var f)) f.Count = Math.Max(0, f.Count - n); }

    /// <summary>Stock of a regional good across the nation's regions.</summary>
    public static double GoodHeld(EconomyState st, EconomyGeo geo, string nation, string good) =>
        geo.Regions.Values.Where(r => r.Nation == nation).Sum(r => st.Stock(r.Id).GetValueOrDefault(good));

    public static double TakeGood(EconomyState st, EconomyGeo geo, string nation, string good, double qty)
    {
        var got = 0.0;
        foreach (var r in geo.Regions.Values.Where(r => r.Nation == nation))
        {
            var s = st.Stock(r.Id);
            var t = Math.Min(qty - got, s.GetValueOrDefault(good));
            if (t <= 0) continue;
            s[good] -= t; got += t;
            if (got >= qty) break;
        }
        return got;
    }

    /// <summary>
    /// Transport a nation can put into a theatre, by mode (game numbers from the size of its economy): trucks,
    /// trains, merchant vessels, cargo aircraft, helicopters. Not designable.
    /// </summary>
    public static Dictionary<string, int> Transport(NationEconomy ne, double warShare = 0.35)
    {
        var g = Math.Max(50, ne.Gdp);
        int N(double v, int min) => Math.Max(min, (int)Math.Round(v * warShare));
        return new() { ["road"] = N(g / 4, 200), ["rail"] = N(g / 120, 6), ["sea"] = N(g / 250, 4), ["air"] = N(g / 180, 6), ["heli"] = N(g / 220, 6) };
    }
}
