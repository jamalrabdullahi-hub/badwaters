namespace BadWaters.Sim.Theatre;

// Naval forces: ships and submarines as moving units with hit points. An air-defence battery or strike
// launcher mounted on a ship moves and dies with it. Anti-ship missiles aim at the attacker's last
// contact position and only find the ship if it's still inside the seeker's search radius. Submarines
// ambush surface ships; ships and patrol aircraft attack submarines they hold on their picture.
// All rules are dice on 0–10 ratings; movement is straight-line at a fixed game speed.

public sealed class ShipState
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Side { get; init; }
    public required string Kind { get; init; } // ship | sub
    public double Lon { get; set; }
    public double Lat { get; set; }
    public double? DestLon { get; set; }
    public double? DestLat { get; set; }
    public double Hp { get; set; }
    public double MaxHp { get; init; }
    public bool Sunk => Hp <= 0;
    public double AntiShip { get; init; }
    public double AntiSub { get; init; }
    public double Survivability { get; init; }
    /// <summary>Submarine quieting rating: harder to find and to hit.</summary>
    public double Quieting { get; init; }
    public List<BatteryState> Batteries { get; } = new();
    public List<LauncherState> Launchers { get; } = new();
    public UnitDef? Unit { get; init; }
    public bool Moving => DestLon != null && !Sunk;
    public double SpeedKmPerSec => Kind == "sub" ? NavalRules.SubKmPerSec : NavalRules.ShipKmPerSec * (Hp < MaxHp / 2 ? 0.5 : 1);
}

public static class NavalRules
{
    public const double ShipKmPerSec = 0.015, SubKmPerSec = 0.010;   // ~54 km/h and ~36 km/h: game pacing
    public const double TorpedoKm = 25, AswKm = 30, CombatEvery = 30;
    /// <summary>Hit points: survivability plus size (endurance) — a carrier takes several hits, a frigate one or two.</summary>
    public static double MaxHp(double survivability, double endurance) => 20 + 8 * survivability + 12 * endurance;
    public static double MissileDamage(double punch) => 10 + 5 * punch;
    public const double TorpedoDamage = 60, DepthChargeDamage = 60;
    /// <summary>How far a missile's seeker searches around its aimpoint for a ship (km).</summary>
    public static double SeekerKm(double accuracy) => 10 + 4 * accuracy;
    public static bool CanHitShips(SystemProfile missile) => missile.R("anti-ship") > 0 || missile.R("accuracy") >= 6;
}

public sealed class NavalForces
{
    private readonly InterceptTrial _t;
    private readonly Random _rng;
    private double _nextCombat;
    public List<ShipState> Ships { get; } = new();

    public NavalForces(InterceptTrial t, Func<string, PresetInfo?> preset, int seed = 1)
    {
        _t = t; _rng = new Random(seed * 577 + 11);
        foreach (var u in t.Scenario.Units.Where(u => u.Kind is "ship" or "sub"))
        {
            var p = preset(u.Preset);
            var surv = p?.R("survivability") ?? 4;
            var end = p?.R("endurance") ?? 5;
            Ships.Add(new ShipState
            {
                Id = u.Id, Name = u.Name, Side = u.Side, Kind = u.Kind, Lon = u.Lon, Lat = u.Lat, Unit = u,
                MaxHp = NavalRules.MaxHp(surv, end), Hp = NavalRules.MaxHp(surv, end), Survivability = surv,
                AntiShip = p?.R("anti-ship") ?? 0, AntiSub = p?.R("anti-sub") ?? 0, Quieting = p?.R("quieting") ?? 0,
            });
        }
        // air-defence ships (batteries) and their strike cells (launchers at the same spot) are one ship
        foreach (var b in t.Batteries.Where(b => b.IsShip))
        {
            var surv = b.Radar.R("survivability");
            var end = b.Radar.R("endurance");
            var s = new ShipState
            {
                Id = b.Def.Id, Name = b.Def.Name, Side = b.Def.Side, Kind = "ship", Lon = b.Def.Lon, Lat = b.Def.Lat,
                MaxHp = NavalRules.MaxHp(surv, end), Hp = NavalRules.MaxHp(surv, end), Survivability = surv, AntiShip = 5, AntiSub = b.Radar.R("anti-sub"),
            };
            s.Batteries.Add(b);
            s.Launchers.AddRange(t.Launchers.Where(l => l.IsShip && Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, b.Def.Lon, b.Def.Lat) < 2));
            Ships.Add(s);
        }
        foreach (var l in t.Launchers.Where(l => l.IsShip && Ships.All(s => !s.Launchers.Contains(l))))
            Ships.Add(new ShipState { Id = l.Def.Id, Name = l.Def.Name, Side = l.Def.Side, Kind = "ship", Lon = l.Def.Lon, Lat = l.Def.Lat, MaxHp = 60, Hp = 60, Survivability = 5, Launchers = { l } });
    }

    public ShipState? Find(string id) => Ships.FirstOrDefault(s => s.Id == id);
    public bool IsSunk(string id) => Find(id)?.Sunk ?? false;

    public string MoveTo(string id, double lon, double lat)
    {
        var s = Find(id);
        if (s == null || s.Sunk) return "That ship can't take orders.";
        s.DestLon = lon; s.DestLat = lat;
        var km = Pacing.DistanceKm(s.Lon, s.Lat, lon, lat);
        _t.Report("naval", $"{s.Name} ordered to move {km:0} km (ETA {km / s.SpeedKmPerSec / 60:0} min).");
        return $"{s.Name} under way, {km:0} km.";
    }

    public void Hold(string id) { if (Find(id) is { } s) { s.DestLon = null; s.DestLat = null; } }

    /// <summary>Apply damage; a sunk ship loses its air defence and strike cells.</summary>
    public string Hit(ShipState s, double damage, string by)
    {
        if (s.Sunk) return "";
        s.Hp = Math.Max(0, s.Hp - damage);
        foreach (var b in s.Batteries) b.Degrade = s.Sunk ? 0 : s.Hp < s.MaxHp / 2 ? 0.5 : 1;
        if (s.Sunk)
        {
            foreach (var b in s.Batteries) b.Stock = 0;
            foreach (var l in s.Launchers) l.Stock = 0;
            s.DestLon = null;
            _t.Report("naval", $"{s.Name} SUNK by {by}.");
            return $"{s.Name} sunk";
        }
        var text = $"{s.Name} hit by {by}: −{damage:0} → {s.Hp / s.MaxHp:P0} strength{(s.Hp < s.MaxHp / 2 ? " (half speed, radar degraded)" : "")}";
        _t.Report("naval", text + ".");
        return text;
    }

    public void Tick(double dt, double now)
    {
        foreach (var s in Ships.Where(s => s.Moving))
        {
            var d = Pacing.DistanceKm(s.Lon, s.Lat, s.DestLon!.Value, s.DestLat!.Value);
            var step = s.SpeedKmPerSec * dt;
            if (d <= step) { s.Lon = s.DestLon.Value; s.Lat = s.DestLat.Value; s.DestLon = s.DestLat = null; _t.Report("naval", $"{s.Name} on station."); }
            else { var f = step / d; s.Lon += (s.DestLon.Value - s.Lon) * f; s.Lat += (s.DestLat.Value - s.Lat) * f; }
            foreach (var b in s.Batteries) b.MoveTo(s.Lon, s.Lat);
            foreach (var l in s.Launchers) l.MoveTo(s.Lon, s.Lat);
        }
        if (now < _nextCombat) return;
        _nextCombat = now + NavalRules.CombatEvery;
        Submarines(now);
        Asw(now);
    }

    /// <summary>Where a naval unit is (units that have never been ordered keep their patrol orbit).</summary>
    public (double Lon, double Lat)? Pos(string unitId)
    {
        var s = Find(unitId);
        if (s == null) return null;
        return (s.Lon, s.Lat);
    }

    private void Submarines(double now)
    {
        foreach (var sub in Ships.Where(s => s.Kind == "sub" && !s.Sunk))
        {
            var prey = Ships.Where(t => t.Side != sub.Side && t.Kind == "ship" && !t.Sunk && Pacing.DistanceKm(sub.Lon, sub.Lat, t.Lon, t.Lat) <= NavalRules.TorpedoKm)
                .OrderBy(t => Pacing.DistanceKm(sub.Lon, sub.Lat, t.Lon, t.Lat)).FirstOrDefault();
            if (prey == null || _rng.NextDouble() > 0.5 + 0.02 * sub.Quieting) continue; // the sub has to find and set up the shot
            var p = Math.Clamp(0.3 + 0.05 * sub.AntiShip - 0.03 * prey.AntiSub, 0.1, 0.8);
            _t.Intel?.Reveal(prey.Side, sub.Id, "sub", sub.Lon, sub.Lat, now, "torpedo attack");
            if (_rng.NextDouble() < p) Hit(prey, NavalRules.TorpedoDamage, $"{sub.Name} (torpedo, {p:P0})");
            else _t.Report("naval", $"{sub.Name} torpedo attack on {prey.Name} missed ({p:P0}).");
        }
    }

    private void Asw(double now)
    {
        if (_t.Intel == null) return;
        foreach (var side in new[] { "blue", "red" })
        {
            var pic = _t.Intel.Pictures[side];
            foreach (var sub in Ships.Where(s => s.Kind == "sub" && s.Side != side && !s.Sunk && pic.Knows(s.Id, now, 90)))
            {
                // hunters: own surface ships with sonar, or patrol aircraft overhead
                var ship = Ships.Where(h => h.Side == side && h.Kind == "ship" && !h.Sunk && h.AntiSub >= 5 && Pacing.DistanceKm(h.Lon, h.Lat, sub.Lon, sub.Lat) <= NavalRules.AswKm)
                    .OrderByDescending(h => h.AntiSub).FirstOrDefault();
                var air = _t.Air?.Flights.FirstOrDefault(f => f.Side == side && f.Mission == Mission.Recon && f.Airborne(now) && f.Aircraft.Count > 0 &&
                    Pacing.DistanceKm(f.Pos(now).Lon, f.Pos(now).Lat, sub.Lon, sub.Lat) <= NavalRules.AswKm);
                if (ship == null && air == null) continue;
                var asw = Math.Max(ship?.AntiSub ?? 0, air != null ? 6 : 0);
                var p = Math.Clamp(0.15 + 0.05 * asw - 0.03 * sub.Survivability - 0.03 * sub.Quieting, 0.05, 0.6);
                var who = ship?.Name ?? air!.Callsign;
                if (_rng.NextDouble() < p) Hit(sub, NavalRules.DepthChargeDamage, $"{who} (anti-submarine attack, {p:P0})");
                else _t.Report("naval", $"{who} attacked a submarine contact and missed ({p:P0}).");
            }
        }
    }
}
