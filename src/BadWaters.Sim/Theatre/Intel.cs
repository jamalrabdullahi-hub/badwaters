namespace BadWaters.Sim.Theatre;

// ISR and fog of war. Each side keeps its own picture: contacts it has detected (with identification
// level and staleness) and battle-damage beliefs about enemy base components. Sensors are radars,
// AEW, fighters, ships, patrol aircraft, satellite passes and recon flights. Detection is dice on
// sensor rating against target stealth. The UI and the AI both work from this picture, not the truth.

public sealed class Contact
{
    public required string Key { get; init; }
    public required string Kind { get; init; }   // air | ship | sub | launcher
    public double Lon { get; set; }
    public double Lat { get; set; }
    public double LastSeen { get; set; }
    /// <summary>0 = unknown, 1 = class (e.g. "hostile air ×4"), 2 = type (e.g. "4× J-16").</summary>
    public int IdLevel { get; set; }
    public string Label { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>Recognised role when identified (e.g. "AEW", "Tanker", "CAP"): what a commander can react to.</summary>
    public string Role { get; set; } = "";
}

public sealed class BdaBelief
{
    public double Integrity { get; set; } = 100;
    public double AsOf { get; set; }
    /// <summary>0–1: satellite imagery and recon are high, pilot and impact reports are low.</summary>
    public double Confidence { get; set; }
    public string Source { get; set; } = "pre-war survey";
}

public sealed class Picture
{
    public required string Side { get; init; }
    public Dictionary<string, Contact> Contacts { get; } = new();
    public Dictionary<(string Site, string Comp), BdaBelief> Bda { get; } = new();
    /// <summary>Game time of this side's next satellite pass.</summary>
    public double NextSatellite { get; set; }

    public BdaBelief Belief(string site, string comp) =>
        Bda.TryGetValue((site, comp), out var b) ? b : Bda[(site, comp)] = new BdaBelief();

    public bool Knows(string key, double now, double within = 60) => Contacts.TryGetValue(key, out var c) && now - c.LastSeen <= within;
}

public static class IntelRules
{
    public const double ScanSeconds = 10, DropAfter = 1200, StaleAfter = 300, SatellitePeriod = 5400;
    public static double DetectChance(double sensor, double stealth) => Math.Clamp(0.35 + 0.06 * (sensor - stealth), 0.04, 0.95);
    /// <summary>Range at which an emitting radar is heard by the other side's sensors (passive, bearing-quality contact).</summary>
    public const double EsmKm = 350;
    public static int IdLevel(double sensor, double stealth) => sensor - stealth >= 4 ? 2 : sensor - stealth >= 1 ? 1 : 0;
}

public sealed class IntelService
{
    private readonly InterceptTrial _t;
    private readonly Func<string, PresetInfo?> _preset;
    private readonly Random _rng;
    private double _next;
    public Dictionary<string, Picture> Pictures { get; } = new() { ["blue"] = new Picture { Side = "blue", NextSatellite = 1800 }, ["red"] = new Picture { Side = "red", NextSatellite = 2700 } };

    public IntelService(InterceptTrial t, Func<string, PresetInfo?> preset, int seed)
    {
        _t = t; _preset = preset; _rng = new Random(seed * 131 + 17);
        // both sides start with a peacetime survey of each other's fixed bases
        foreach (var b in t.Bases.Values)
            foreach (var p in Pictures.Values.Where(p => p.Side != b.Site.Side))
                foreach (var c in b.Components.Keys) p.Bda[(b.Site.Id, c)] = new BdaBelief { Integrity = 100, AsOf = 0, Confidence = 0.8 };
    }

    private record Sensor(double Lon, double Lat, double AirKm, double ShipKm, double SubKm, double Rating, string Name);

    private IEnumerable<Sensor> Sensors(string side, double now)
    {
        foreach (var b in _t.Batteries.Where(b => b.Def.Side == side))
        {
            var jam = _t.JamFactor(side, b.Def.Lon, b.Def.Lat);
            if (_t.Emitting(b.Def.Id)) yield return new Sensor(b.Def.Lon, b.Def.Lat, b.RadarKm * jam, b.IsShip ? 150 : 40, b.IsShip ? 20 : 0, b.Radar.R("detection") * b.Degrade * jam, b.Def.Name + (jam < 1 ? " (jammed)" : ""));
            else if (b.IsShip) yield return new Sensor(b.Def.Lon, b.Def.Lat, 0, 40, 20, b.Radar.R("detection") * b.Degrade * 0.6, $"{b.Def.Name} (silent)"); // lookouts and sonar only
        }
        foreach (var u in _t.Scenario.Units.Where(u => u.Side == side && u.Kind == "ship"))
        {
            var p = _preset(u.Preset); var (lon, lat) = _t.UnitPos(u);
            var sub = (p?.R("anti-sub") ?? 0) >= 6 ? 30 : 10;
            yield return _t.Emitting(u.Id) ? new Sensor(lon, lat, 150, 120, sub, p?.R("detection") ?? 5, u.Name)
                : new Sensor(lon, lat, 0, 40, sub, (p?.R("detection") ?? 5) * 0.6, $"{u.Name} (silent)");
        }
        if (_t.Air == null) yield break;
        foreach (var f in _t.Air.Flights.Where(f => f.Side == side && f.Airborne(now) && f.Aircraft.Count > 0))
        {
            var (lon, lat) = f.Pos(now);
            var det = f.Type.R("detection");
            if (!_t.Emitting(f.Id)) continue; // a silent flight sees nothing beyond the pilots' eyes (not modelled)
            yield return f.Mission switch
            {
                Mission.AEW => new Sensor(lon, lat, 400, 300, 0, det, $"{f.Callsign} (AEW)"),
                Mission.Recon => new Sensor(lon, lat, 25 * det, 300, 60, det, $"{f.Callsign} (patrol)"),
                _ => new Sensor(lon, lat, 25 * det, 60, 0, det, f.Callsign),
            };
        }
    }

    public void Tick(double now)
    {
        if (now < _next) return;
        _next = now + IntelRules.ScanSeconds;
        foreach (var pic in Pictures.Values) Scan(pic, now);
    }

    private void Scan(Picture pic, double now)
    {
        var side = pic.Side;
        var sensors = Sensors(side, now).ToList();
        // enemy aircraft (per flight)
        if (_t.Air != null)
            foreach (var f in _t.Air.Flights.Where(f => f.Side != side && f.Airborne(now) && f.Aircraft.Count > 0))
            {
                var (lon, lat) = f.Pos(now);
                var sig = f.Type.R("signature");
                foreach (var s in sensors)
                {
                    if (Pacing.DistanceKm(s.Lon, s.Lat, lon, lat) > s.AirKm || _rng.NextDouble() > IntelRules.DetectChance(s.Rating, sig)) continue;
                    var id = IntelRules.IdLevel(s.Rating, sig);
                    See(pic, f.Id, "air", lon, lat, now, id, id == 2 ? $"{f.Aircraft.Count}× {f.Type.Name}" : id == 1 ? $"hostile air ×{f.Aircraft.Count}" : "unknown air", s.Name);
                    if (id >= 1 && f.Mission is Mission.AEW or Mission.Tanker) pic.Contacts[f.Id].Role = f.Mission.ToString(); // big, distinctive tracks
                    else if (id == 2) pic.Contacts[f.Id].Role = f.Mission.ToString();
                    break;
                }
            }
        // enemy ships and submarines (units and air-defence ships)
        var ships = _t.Scenario.Units.Where(u => u.Side != side && u.Kind is "ship" or "sub" && !(_t.Naval?.IsSunk(u.Id) ?? false))
            .Select(u => (Key: u.Id, Kind: u.Kind, Pos: _t.UnitPos(u), Name: _preset(u.Preset)?.Name ?? u.Name))
            .Concat(_t.Batteries.Where(b => b.Def.Side != side && b.IsShip && !(_t.Naval?.IsSunk(b.Def.Id) ?? false)).Select(b => (Key: b.Def.Id, Kind: "ship", Pos: (b.Def.Lon, b.Def.Lat), Name: b.Def.Name)));
        foreach (var sh in ships)
            foreach (var s in sensors)
            {
                var range = sh.Kind == "sub" ? s.SubKm : s.ShipKm;
                if (range <= 0 || Pacing.DistanceKm(s.Lon, s.Lat, sh.Pos.Item1, sh.Pos.Item2) > range) continue;
                if (_rng.NextDouble() > IntelRules.DetectChance(s.Rating, sh.Kind == "sub" ? 7 : 1)) continue;
                var id = IntelRules.IdLevel(s.Rating, sh.Kind == "sub" ? 7 : 1);
                See(pic, sh.Key, sh.Kind, sh.Pos.Item1, sh.Pos.Item2, now, id, id == 2 ? sh.Name : id == 1 ? (sh.Kind == "sub" ? "submarine" : "surface combatant") : "unknown surface", s.Name);
                break;
            }
        // passive listening: enemy radars that are switched on give themselves away at long range (bearing-quality, id 1)
        foreach (var e in Emitters(side == "blue" ? "red" : "blue", now))
            if (sensors.Any(s => Pacing.DistanceKm(s.Lon, s.Lat, e.Lon, e.Lat) <= IntelRules.EsmKm))
                See(pic, e.Key, e.Kind, e.Lon, e.Lat, now, 1, e.Kind == "air" ? "radar emitter (air)" : e.Kind == "ship" ? "radar emitter (ship)" : "radar emitter", "passive (ESM)");
        // satellite pass: imagery of every enemy base (BDA), ships at sea and launch areas
        if (now >= pic.NextSatellite)
        {
            pic.NextSatellite = now + IntelRules.SatellitePeriod;
            foreach (var b in _t.Bases.Values.Where(b => b.Site.Side != side))
                foreach (var (cid, c) in b.Components)
                    pic.Bda[(b.Site.Id, cid)] = new BdaBelief { Integrity = Math.Round(c.Integrity / 5) * 5, AsOf = now, Confidence = 0.9, Source = "satellite" };
            foreach (var sh in ships.Where(x => x.Kind == "ship")) See(pic, sh.Key, "ship", sh.Pos.Item1, sh.Pos.Item2, now, 2, sh.Name, "satellite");
            foreach (var l in _t.Launchers.Where(l => l.Def.Side != side && !l.IsShip)) See(pic, l.Def.Id, "launcher", l.Def.Lon, l.Def.Lat, now, 1, "launcher area", "satellite");
            _t.Report("isr", $"{_t.Scenario.Sides.GetValueOrDefault(side, side)} satellite pass: imagery of enemy bases updated.");
        }
        foreach (var k in pic.Contacts.Where(c => now - c.Value.LastSeen > IntelRules.DropAfter).Select(c => c.Key).ToList()) pic.Contacts.Remove(k);
    }

    /// <summary>A side's radars that are switched on: ships, ship batteries, AEW flights (ground radar sites are already known).</summary>
    private IEnumerable<(string Key, string Kind, double Lon, double Lat)> Emitters(string side, double now)
    {
        foreach (var b in _t.Batteries.Where(b => b.Def.Side == side && b.IsShip && _t.Emitting(b.Def.Id) && !(_t.Naval?.IsSunk(b.Def.Id) ?? false)))
            yield return (b.Def.Id, "ship", b.Def.Lon, b.Def.Lat);
        foreach (var u in _t.Scenario.Units.Where(u => u.Side == side && u.Kind == "ship" && _t.Emitting(u.Id) && !(_t.Naval?.IsSunk(u.Id) ?? false)))
        {
            var (lon, lat) = _t.UnitPos(u);
            yield return (u.Id, "ship", lon, lat);
        }
        if (_t.Air == null) yield break;
        foreach (var f in _t.Air.Flights.Where(f => f.Side == side && f.Mission is Mission.AEW or Mission.Jamming && f.Airborne(now) && _t.Emitting(f.Id)))
        {
            var (lon, lat) = f.Pos(now);
            yield return (f.Id, "air", lon, lat);
        }
    }

    private static void See(Picture pic, string key, string kind, double lon, double lat, double now, int id, string label, string source)
    {
        if (!pic.Contacts.TryGetValue(key, out var c)) pic.Contacts[key] = c = new Contact { Key = key, Kind = kind };
        c.Lon = lon; c.Lat = lat; c.LastSeen = now; c.Source = source;
        if (id >= c.IdLevel) { c.IdLevel = id; c.Label = label; }
    }

    /// <summary>Something gave itself away (e.g. a submarine firing torpedoes).</summary>
    public void Reveal(string toSide, string key, string kind, double lon, double lat, double now, string source) =>
        See(Pictures[toSide], key, kind, lon, lat, now, 1, kind == "sub" ? "submarine" : "contact", source);

    /// <summary>A launch seen from space reveals the launcher (for the other side).</summary>
    public void LaunchSeen(LauncherState l, double now)
    {
        var pic = Pictures[l.Def.Side == "blue" ? "red" : "blue"];
        See(pic, l.Def.Id, "launcher", l.Def.Lon, l.Def.Lat, now, 1, l.IsShip ? "ship launching missiles" : "missile launch point", "launch detection");
    }

    /// <summary>Damage report after a strike: weak confidence unless the attacker looks (satellite or recon).</summary>
    public void Report(string attacker, BaseState b, string comp, double confidence, string source, double now)
    {
        var truth = b.Components[comp].Integrity;
        var noise = (1 - confidence) * 40 * (_rng.NextDouble() - 0.5);
        Pictures[attacker].Bda[(b.Site.Id, comp)] = new BdaBelief { Integrity = Math.Clamp(Math.Round((truth + noise) / 5) * 5, 0, 100), AsOf = now, Confidence = confidence, Source = source };
    }

    /// <summary>Recon over a base: exact damage of every component.</summary>
    public void Recon(string side, BaseState b, double now)
    {
        foreach (var (cid, c) in b.Components)
            Pictures[side].Bda[(b.Site.Id, cid)] = new BdaBelief { Integrity = Math.Round(c.Integrity), AsOf = now, Confidence = 1, Source = "recon" };
    }
}
