using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops · inspection: status, damage (BDA), armament, fuel and supplies for whatever is picked. Your own things
/// show the truth; enemy bases and contacts show only what your side believes (picture, BDA, their age and source).
/// </summary>
public partial class TheatreOps
{
    private static string Bar(double pct, int width = 10)
    {
        var n = (int)Math.Round(Math.Clamp(pct, 0, 100) / 100 * width);
        return new string('█', n) + new string('░', width - n);
    }
    private static Color Health(double pct) => pct >= 75 ? UiTheme.Good : pct >= 40 ? new Color("ff9a3c") : UiTheme.Bad;
    private string Ago(double t) { var m = (Trial.Time - t) / 60; return m < 1 ? "just now" : m < 90 ? $"{m:0} min ago" : $"{m / 60:0.0} h ago"; }

    /// <summary>Supply readout for a logistics node: each item, against its normal load, days left.</summary>
    private void SupplyLines(TacWindow w, string nodeId, bool magazines = true)
    {
        if (Trial.Logistics?.Node(nodeId) is not { } n) return;
        var lg = Trial.Logistics;
        w.Head("Supplies");
        foreach (var item in new[] { "avfuel", "diesel", "propellant", "spares" })
        {
            var have = lg.Get(n, item); var bl = lg.BaselineOf(n, item);
            if (have <= 0 && bl <= 0) continue;
            var pct = bl > 0 ? have / bl * 100 : 100;
            var use = lg.DailyUse(n, item);
            w.Line($"  {lg.ItemName(item),-18} {Bar(pct)} {lg.Amount(item, have)} / {lg.Amount(item, bl)}{(use > 0 ? $" · {lg.Amount(item, use)}/day · {DaysText(lg.DaysLeft(n, item))}" : "")}{(n.Short.Contains(item) ? " · DRY" : "")}",
                n.Short.Contains(item) ? UiTheme.Bad : Health(pct));
        }
        if (!magazines) return;
        var mun = lg.ItemsAt(n).Where(i => i.StartsWith("store:") || i.StartsWith("design:")).Select(i => (i, q: lg.Get(n, i), bl: lg.BaselineOf(n, i))).Where(x => x.q > 0 || x.bl > 0).OrderByDescending(x => x.q).ToList();
        if (mun.Count > 0)
        {
            w.Head("Munitions held");
            foreach (var (i, q, bl) in mun.Take(14)) w.Line($"  {lg.ItemName(i),-30} {q,5:0}{(bl > 0 ? $" / {bl:0}" : "")}", bl > 0 && q < bl * 0.4 ? new Color("ff9a3c") : UiTheme.TacInk);
            if (mun.Count > 14) w.Line($"  … and {mun.Count - 14} more types", UiTheme.TacDim);
        }
        var inbound = lg.Shipments.Where(s => s.To == nodeId && s.State is ShipmentState.Moving or ShipmentState.Queued).ToList();
        if (inbound.Count > 0)
        {
            w.Head("Inbound");
            foreach (var s in inbound.Take(6)) w.Line($"  {lg.Amount(s.Item, s.Qty)} {lg.ItemName(s.Item)} by {lg.Def.Modes[s.Mode].Name.ToLowerInvariant()}{(s.State == ShipmentState.Moving ? $" · {(s.Arrive - Trial.Time) / 3600:0.0} h" : " · waiting for transport")}");
        }
        if (n.Factor < 0.999) w.Line($"Delivery efficiency {n.Factor:P0} ({n.Elev:0} m, {n.OffRouteKm:0} km off route)", UiTheme.TacDim);
    }

    /// <summary>Components of a base: the truth for your own, your BDA belief (with source and age) for the enemy's.</summary>
    private void Components(TacWindow w, BaseState b, bool own)
    {
        w.Head(own ? "Condition" : "Damage assessment (your picture)");
        var pic = Trial.Intel?.Pictures[CommandSide];
        foreach (var c in b.Components.Values.OrderBy(c => c.Def.Priority))
        {
            if (own)
                w.Line($"  {c.Def.Name,-28} {Bar(c.Integrity)} {c.Integrity,3:0}%{(c.Integrity < 100 ? $" · repairing {c.Def.RepairPerHour:0}%/h" : "")}", Health(c.Integrity));
            else
            {
                var bel = pic?.Belief(b.Site.Id, c.Def.Id);
                var conf = bel == null ? "" : bel.Confidence >= 0.85 ? "high" : bel.Confidence >= 0.5 ? "medium" : "low";
                w.Line($"  {c.Def.Name,-28} {Bar(bel?.Integrity ?? 100)} ~{bel?.Integrity ?? 100,3:0}% · {bel?.Source ?? "no data"}, {(bel == null ? "" : Ago(bel.AsOf))} · {conf} confidence", Health(bel?.Integrity ?? 100));
            }
        }
        if (!own) w.Line(pic != null ? $"Next satellite pass T+{AirTasking.Clock(pic.NextSatellite)}. Recon flights give exact damage; strike and impact reports are rough." : "", UiTheme.TacDim);
    }

    /// <summary>Inspection of a base or site (your own or the enemy's).</summary>
    private void InspectSite(TacWindow w, string id)
    {
        var s = Trial.Site(id);
        if (s == null) { w.Line("Unknown site.", UiTheme.TacDim); return; }
        var own = s.Side == CommandSide;
        var prot = !TargetPolicy.IsLegitimate(s.Class);
        w.Head($"{s.Name} · {(s.Type != "" ? s.Type : s.Class)} · {(prot ? "PROTECTED" : own ? "yours" : Trial.AtWar ? "enemy" : "hostile, not at war")}");
        if (prot) { w.Line("Population centre or civil site: defended, never a target.", UiTheme.Good); return; }
        w.Line($"{Math.Abs(s.Lat):0.00}{(s.Lat >= 0 ? "N" : "S")} {Math.Abs(s.Lon):0.00}{(s.Lon >= 0 ? "E" : "W")} · hits taken {Trial.Hits.GetValueOrDefault(id)}", UiTheme.TacDim);
        if (Trial.Bases.TryGetValue(id, out var b))
        {
            if (own)
            {
                w.Line($"Runway {(b.RunwayOpen ? "OPEN" : "CLOSED")} · arming {(b.CanArm ? "possible" : "NOT POSSIBLE")} · turnaround ×{b.PrepFactor:0.0}{(b.Type.Components.Any(c => c.Effect == "launch-pads") ? $" · launch pads {(b.PadsOk ? "OK" : "DOWN")}" : "")} · shelters for {b.ShelterCapacity}",
                    b.RunwayOpen && b.CanArm ? UiTheme.TacInk : UiTheme.Bad);
            }
            Components(w, b, own);
        }
        if (own && Trial.Air != null)
        {
            var here = Scenario.Units.Where(u => u.Kind is "air" or "aew" && Trial.Air.BaseOf(u) == id).GroupBy(u => u.Preset).ToList();
            if (here.Count > 0)
            {
                w.Head("Aircraft based here");
                foreach (var g in here)
                {
                    var lost = g.Count(u => Trial.Air.Destroyed.Contains(u.Id));
                    var flying = g.Count(u => Trial.Air.Tasked.Contains(u.Id));
                    w.Line($"  {Session.Content.PresetInfo(g.Key)?.Name ?? g.Key,-26} {g.Count() - lost - flying} ready · {flying} tasked{(lost > 0 ? $" · {lost} lost" : "")}", lost > 0 ? new Color("ff9a3c") : UiTheme.TacInk);
                }
            }
            var hosted = Trial.Batteries.Where(x => x.BaseId == id).Select(x => $"{x.Def.Name} ({x.Stock} {x.Interceptor.Name})").Concat(Trial.Launchers.Where(x => x.BaseId == id).Select(x => $"{x.Def.Name} ({x.Stock} {x.Missile.Name})")).ToList();
            if (hosted.Count > 0) { w.Head("Units at this site"); foreach (var h in hosted) w.Line("  " + h); }
            SupplyLines(w, id);
        }
        if (!own)
        {
            var pic = Trial.Intel?.Pictures[CommandSide];
            var near = pic?.Contacts.Values.Where(c => Pacing.DistanceKm(c.Lon, c.Lat, s.Lon, s.Lat) < 60).ToList() ?? new();
            if (near.Count > 0) { w.Head("Contacts near it"); foreach (var c in near.Take(6)) w.Line($"  {c.Label}{(c.IdLevel < 2 ? " ?" : "")} · {c.Source}, {Ago(c.LastSeen)}"); }
            var bats = Trial.Batteries.Where(x => x.BaseId == id).ToList();
            if (bats.Count > 0) w.Line($"Known air defence here: {bats.Count} battery(ies) (from the pre-war survey; numbers left are unknown).", UiTheme.TacDim);
        }
    }

    /// <summary>An enemy contact: only what your sensors hold.</summary>
    private void InspectContact(TacWindow w, string key)
    {
        var c = Trial.Intel?.Pictures[CommandSide].Contacts.GetValueOrDefault(key);
        if (c == null) { w.Line("Contact lost: your sensors no longer hold it.", UiTheme.TacDim); return; }
        w.Head($"{c.Label}{(c.IdLevel < 2 ? " (unconfirmed)" : "")} · {c.Kind} contact");
        w.Line($"Identification: {(c.IdLevel == 2 ? "type identified" : c.IdLevel == 1 ? "class only" : "unknown")}{(c.Role != "" ? $" · role {c.Role}" : "")}");
        w.Line($"Last seen {Ago(c.LastSeen)} by {c.Source} at {Math.Abs(c.Lat):0.00}{(c.Lat >= 0 ? "N" : "S")} {Math.Abs(c.Lon):0.00}{(c.Lon >= 0 ? "E" : "W")}",
            Trial.Time - c.LastSeen > IntelRules.StaleAfter ? new Color("ff9a3c") : UiTheme.TacInk);
        if (Trial.Time - c.LastSeen > IntelRules.StaleAfter) w.Line("Stale: it has probably moved. Weapons fired at it may miss; get a fresh fix first.", new Color("ff9a3c"));
        w.Line("Damage, fuel and weapons of enemy units are unknown: you only see what your sensors report.", UiTheme.TacDim);
        var tracked = Trial.Log.Where(x => x.Kind is "impact" or "naval" && x.Text.Contains(c.Label.Replace(" (contact)", ""))).TakeLast(3).ToList();
        if (tracked.Count > 0) { w.Head("Reported hits"); foreach (var x in tracked) w.Line($"  T+{AirTasking.Clock(x.T)} {x.Text}"); }
    }

    /// <summary>Extra inspection for your own battery, launcher, ship or flight (after its orders).</summary>
    private void InspectOwn(TacWindow w, string kind, string id)
    {
        switch (kind)
        {
            case "battery":
            {
                var b = Trial.Batteries.First(x => x.Def.Id == id);
                w.Head("Status");
                w.Line($"  Effectiveness {Bar(b.Degrade * 100)} {b.Degrade:P0} (host base sensors, spares and diesel)", Health(b.Degrade * 100));
                w.Line($"  Magazine {Bar(b.Def.Stock > 0 ? 100.0 * b.Stock / b.Def.Stock : 0)} {b.Stock}/{b.Def.Stock} {b.Interceptor.Name}");
                w.Line($"  Engagements so far {Trial.Shots.Count(s => s.BatteryId == id)} · in flight {Trial.Shots.Count(s => s.BatteryId == id && s.State == ShotState.Flying)} · networking {b.Networking:0}");
                w.Line($"  Ratings: radar detection {b.Radar.R("detection"):0} · interceptor speed {b.Interceptor.R("speed"):0}, accuracy {b.Interceptor.R("accuracy"):0}, reach {b.Interceptor.R("reach"):0} (game ratings)", UiTheme.TacDim);
                SupplyLines(w, id, magazines: false);
                break;
            }
            case "launcher":
            {
                var l = Trial.Launchers.First(x => x.Def.Id == id);
                w.Head("Status");
                w.Line($"  Magazine {Bar(l.Def.Stock > 0 ? 100.0 * l.Stock / l.Def.Stock : 0)} {l.Stock}/{l.Def.Stock} {l.Missile.Name}");
                w.Line($"  Fired so far {Trial.Tracks.Count(t => t.LauncherId == id)} · {(Trial.Logistics?.IsLiquid(l.Def.Missile) == true ? "liquid-fuelled (needs propellant per round)" : "solid-fuelled")}");
                if (l.BaseId != "" && Trial.Bases.TryGetValue(l.BaseId, out var hb)) w.Line($"  Host {hb.Site.Name}: launch pads {(hb.PadsOk ? "OK" : "DOWN")}", hb.PadsOk ? UiTheme.TacInk : UiTheme.Bad);
                w.Line($"  Ratings: speed {l.Missile.R("speed"):0} · accuracy {l.Missile.R("accuracy"):0} · punch {l.Missile.R("punch"):0} · reach {l.Missile.R("reach"):0} (game ratings)", UiTheme.TacDim);
                SupplyLines(w, id, magazines: false);
                break;
            }
            case "unit":
            {
                var s = Trial.Naval?.Find(id);
                if (s == null) break;
                w.Head("Status");
                w.Line($"  Hull {Bar(s.Hp / s.MaxHp * 100)} {s.Hp / s.MaxHp:P0} · speed {s.SpeedKmPerSec * 3600:0} km/h{(s.OutOfFuel ? " (OUT OF FUEL)" : "")}{(s.Hp < s.MaxHp / 2 ? " · radar degraded" : "")}", Health(s.Hp / s.MaxHp * 100));
                w.Line($"  Radar {(Trial.Emitting(id) ? "emitting (heard at 350 km)" : "silent")}");
                SupplyLines(w, id, magazines: false);
                break;
            }
            case "flight":
            {
                var f = Trial.Air?.Flights.FirstOrDefault(x => x.Id == id);
                if (f == null) break;
                w.Head("Status");
                w.Line($"  Aircraft {f.Aircraft.Count} flying{(f.LostAircraft.Count > 0 ? $" · {f.LostAircraft.Count} lost" : "")} · from {Trial.Site(f.BaseId)?.Name}");
                if (Trial.Logistics != null) w.Line($"  Av fuel for this sortie {Trial.Logistics.Amount("avfuel", Trial.Logistics.SortieFuel(Trial.Air!.SortieClass(f.Type.Id, f.Mission), f.Aircraft.Count + f.LostAircraft.Count))} (drawn at take-off)");
                if (f.Loadout.Count > 0 && Trial.Air?.Stores is { } cat)
                {
                    w.Head("Armament (whole flight)");
                    foreach (var g in f.Loadout.Values.GroupBy(x => x.Store))
                    {
                        var carried = g.Sum(x => x.Qty) * f.Aircraft.Count;
                        var fired = f.Expended.GetValueOrDefault(g.Key) + (f.Released && cat.Stores.GetValueOrDefault(g.Key)?.AirToGround == true ? carried : 0);
                        var left = Math.Max(0, carried - Math.Min(carried, fired));
                        w.Line($"  {cat.Stores.GetValueOrDefault(g.Key)?.Name ?? g.Key,-28} {left,3}/{carried} left");
                    }
                }
                w.Head("Route");
                foreach (var wp in f.Route) w.Line($"  {(wp.T <= Trial.Time ? "✓" : " ")} {wp.Name,-12} T+{AirTasking.Clock(wp.T)}", wp.T <= Trial.Time ? UiTheme.TacDim : UiTheme.TacInk);
                w.Line($"  Ratings: speed {f.Type.R("speed"):0} · air-defence {f.Type.R("air-defence"):0} · strike {f.Type.R("strike"):0} · stealth {f.Type.R("signature"):0} · reach {f.Type.R("reach"):0} (game ratings)", UiTheme.TacDim);
                break;
            }
        }
    }
}
