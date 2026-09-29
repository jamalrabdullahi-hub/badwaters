using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops · direct control: click an own unit to select it (UNIT window with its orders, incl. radar on/off),
/// map-driven mission making (strike / fire mission on a target under the cursor, CAP / AEW / recon at a point),
/// planner templates, and the MAP overlay toggles (radar coverage, air-defence reach, strike reach, routes...).
/// </summary>
public partial class TheatreOps
{
    /// <summary>What the player has clicked: kind is battery | launcher | unit | flight.</summary>
    public (string Kind, string Id)? Picked;
    /// <summary>Map overlays the player can switch off (TheatreLayer reads these).</summary>
    public readonly Dictionary<string, bool> Overlay = new()
    {
        ["radar"] = true, ["reach"] = true, ["strike"] = true, ["routes"] = true, ["tracks"] = true, ["labels"] = true, ["reports"] = true,
    };
    private static readonly (string Key, string Name)[] OverlayNames =
    {
        ("radar", "Radar coverage"), ("reach", "Air-defence reach"), ("strike", "Strike reach (launchers)"), ("routes", "Flight routes"),
        ("tracks", "Missile tracks"), ("labels", "Labels"), ("reports", "Report pins"),
    };
    private (double Lon, double Lat)? _planStation;
    private readonly List<Action> _contextActions = new();

    public bool Show(string key) => Overlay.GetValueOrDefault(key, true);

    private void BuildMapMenu(HBoxContainer bar)
    {
        var mb = new MenuButton { Text = "MAP ▾", TooltipText = "Map overlays: switch radar coverage, reach rings, routes... on or off" };
        var pop = mb.GetPopup();
        for (var i = 0; i < OverlayNames.Length; i++) { pop.AddCheckItem(OverlayNames[i].Name, i); pop.SetItemChecked(i, true); }
        pop.HideOnCheckableItemSelection = false;
        pop.IdPressed += id =>
        {
            var k = OverlayNames[id].Key;
            Overlay[k] = !Overlay[k];
            pop.SetItemChecked((int)id, Overlay[k]);
            Layer.QueueRedraw();
        };
        bar.AddChild(mb);
    }

    // ---------------- selection ----------------
    private bool TryPick(Vector2 world, float zoom)
    {
        var best = (Kind: "", Id: "", D: 18 / zoom);
        void Try(string kind, string id, double lon, double lat)
        {
            var d = Session.Project(lon, lat).DistanceTo(world);
            if (d < best.D) best = (kind, id, d);
        }
        foreach (var b in Trial.Batteries.Where(b => b.Def.Side == CommandSide)) Try("battery", b.Def.Id, b.Def.Lon, b.Def.Lat);
        foreach (var l in Trial.Launchers.Where(l => l.Def.Side == CommandSide && !l.IsShip)) Try("launcher", l.Def.Id, l.Def.Lon, l.Def.Lat);
        foreach (var u in Scenario.Units.Where(u => u.Side == CommandSide && u.Kind is "ship" or "sub" && !(Trial.Naval?.IsSunk(u.Id) ?? false)))
        { var (lon, lat) = Trial.UnitPos(u); Try("unit", u.Id, lon, lat); }
        if (Trial.Air != null)
            foreach (var f in Trial.Air.Flights.Where(f => f.Side == CommandSide && f.Airborne(Trial.Time)))
            { var (lon, lat) = f.Pos(Trial.Time); Try("flight", f.Id, lon, lat); }
        if (best.Kind == "") return false;
        Picked = (best.Kind, best.Id);
        if (best.Kind == "unit") SelectedShip = Trial.Naval?.Find(best.Id);
        if (best.Kind == "flight") SelectedFlight = Trial.Air!.Flights.First(f => f.Id == best.Id);
        Open("UNIT");
        return true;
    }

    private string Name(string kind, string id) => kind switch
    {
        "battery" => Trial.Batteries.First(b => b.Def.Id == id).Def.Name,
        "launcher" => Trial.Launchers.First(l => l.Def.Id == id).Def.Name,
        "unit" => Scenario.Units.First(u => u.Id == id).Name,
        _ => Trial.Air?.Flights.FirstOrDefault(f => f.Id == id)?.Callsign ?? id,
    };

    // ---------------- UNIT window: status and orders for the picked entity ----------------
    private void FillUnit(TacWindow w)
    {
        if (Picked is not { } p) { w.Line("Left-click one of your units on the map.", UiTheme.TacDim); return; }
        var (kind, id) = p;
        var row = new HBoxContainer();
        Button Btn(string text, Action a, string tip = "", bool on = false)
        {
            var b = new Button { Text = text, TooltipText = tip, ToggleMode = on, ButtonPressed = on };
            b.AddThemeFontSizeOverride("font_size", 11);
            b.Pressed += () => { a(); w.Refresh?.Invoke(); Layer.QueueRedraw(); };
            row.AddChild(b);
            return b;
        }
        void Emcon(string name)
        {
            var on = Trial.Emitting(id);
            Btn(on ? "RADAR: ON" : "RADAR: OFF", () => Trial.SetEmitting(id, !on, name),
                on ? "Emitting: detects with radar, but enemy passive sensors can hear it at long range. Click to go silent."
                   : "Silent: no radar detection, and the enemy can't hear it. Click to switch the radar on.", on);
        }
        switch (kind)
        {
            case "battery":
            {
                var b = Trial.Batteries.First(x => x.Def.Id == id);
                w.Head($"{b.Def.Name} · air defence{(b.IsShip ? " (ship)" : "")}");
                w.Line($"Radar {b.Radar.Name} · {b.RadarKm:0} km{(Trial.Emitting(id) ? "" : " · SILENT")}\nInterceptor {b.Interceptor.Name} · reach {b.ReachKm:0} km · {b.Stock} left · {b.Channels} channels");
                Emcon(b.Def.Name);
                break;
            }
            case "launcher":
            {
                var l = Trial.Launchers.First(x => x.Def.Id == id);
                w.Head($"{l.Def.Name} · strike launcher");
                w.Line($"{l.Missile.Name} · reach {l.ReachKm:0} km · {l.Stock} rounds left");
                w.Line("Right-click an enemy base on the map for a fire mission, or open FIRES to schedule one for a time on target.", UiTheme.TacDim);
                Btn("FIRES…", () => { var i = _launcherIds.IndexOf(id); if (i >= 0) { _launcher.Select(i); FillTargets(); } Open("FIRES"); });
                break;
            }
            case "unit":
            {
                var u = Scenario.Units.First(x => x.Id == id);
                var s = Trial.Naval?.Find(id);
                w.Head($"{u.Name} · {(u.Kind == "sub" ? "submarine" : "ship")}");
                if (s != null) w.Line($"Hull {s.Hp / s.MaxHp:P0}{(s.Moving ? $" · under way to {s.DestLat:0.00}, {s.DestLon:0.00}" : " · on station")}");
                foreach (var l in s?.Launchers ?? new()) w.Line($"  {l.Missile.Name} ×{l.Stock} · reach {l.ReachKm:0} km");
                foreach (var b in s?.Batteries ?? new()) w.Line($"  {b.Interceptor.Name} ×{b.Stock} · reach {b.ReachKm:0} km");
                if (u.Kind == "ship") Emcon(u.Name);
                if (s != null)
                {
                    Btn("MOVE…", () => { AwaitingMove = s; SelectedShip = s; }, "Then left-click the map (or right-click → Move here)");
                    Btn("HOLD", () => Trial.Naval!.Hold(id));
                    // ship batteries share the ship's radar switch
                    foreach (var b in s.Batteries) if (Trial.Emitting(id) != Trial.Emitting(b.Def.Id)) Trial.SetEmitting(b.Def.Id, Trial.Emitting(id), b.Def.Name);
                }
                break;
            }
            case "flight":
            {
                var f = Trial.Air?.Flights.FirstOrDefault(x => x.Id == id);
                if (f == null) { Picked = null; return; }
                w.Head($"{f.Callsign} · {f.Aircraft.Count}× {f.Type.Name} · {f.Mission}");
                w.Line($"{f.State} · formation {f.Formation}{(f.HoldFire ? " · WEAPONS HOLD" : "")}\nNext: {f.Next(Trial.Time)?.Name ?? "—"} T+{AirTasking.Clock(f.Next(Trial.Time)?.T ?? Trial.Time)} · lands T+{AirTasking.Clock(f.LandAt)}");
                if (f.Mission == Mission.AEW) Emcon(f.Callsign);
                Btn(f.HoldFire ? "WEAPONS FREE" : "HOLD FIRE", () => f.HoldFire = !f.HoldFire);
                Btn("FORMATION ▸", () => Trial.Air!.SetFormation(f, Formations[(Array.IndexOf(Formations, f.Formation) + 1) % Formations.Length]));
                Btn("ABORT / RTB", () => Trial.Air!.Abort(f));
                Btn("ATO…", () => { SelectedFlight = f; Open("ATO"); });
                break;
            }
        }
        w.Body.AddChild(row);
    }

    // ---------------- map-driven mission making (added to the right-click menu) ----------------
    private void AddMissionItems(PopupMenu m, Vector2 world, float zoom)
    {
        _contextActions.Clear();
        void Item(string text, Action a) { m.AddItem(text, 1000 + _contextActions.Count); _contextActions.Add(a); }
        var site = Scenario.Sites.Where(s => Session.Project(s.Lon, s.Lat).DistanceTo(world) < 18 / zoom).OrderBy(s => Session.Project(s.Lon, s.Lat).DistanceTo(world)).FirstOrDefault();
        m.AddSeparator("MISSION");
        if (site != null && site.Side != CommandSide)
        {
            if (!TargetPolicy.IsLegitimate(site.Class)) m.AddItem($"{site.Name}: protected, never a target", 999);
            else
            {
                Item($"Plan strike package → {site.Name}", () => PlanAt(site.Id, "strike"));
                Item($"Plan SEAD strike → {site.Name}", () => PlanAt(site.Id, "sead"));
                foreach (var l in Trial.Launchers.Where(l => l.Def.Side == CommandSide && l.Stock > 0 && Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, site.Lon, site.Lat) <= l.ReachKm).Take(8))
                {
                    var ll = l;
                    Item($"Fire mission: {l.Def.Name} · 2× {l.Missile.Name} → {site.Name}", () => { var r = Trial.Launch(ll.Def.Id, site.Id, 2); Trial.Report("fires", r.Message); });
                }
            }
        }
        var pic = Trial.Intel?.Pictures[CommandSide];
        var ship = pic?.Contacts.Values.Where(c => c.Kind == "ship" && Session.Project(c.Lon, c.Lat).DistanceTo(world) < 18 / zoom).FirstOrDefault();
        if (ship != null)
            foreach (var l in Trial.Launchers.Where(l => l.Def.Side == CommandSide && l.Stock > 0 && NavalRules.CanHitShips(l.Missile) && Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, ship.Lon, ship.Lat) <= l.ReachKm).Take(6))
            {
                var ll = l; var key = ship.Key;
                Item($"Anti-ship fire: {l.Def.Name} · 2× {l.Missile.Name} → {ship.Label}", () => { var r = Trial.LaunchAtShip(ll.Def.Id, key, 2); Trial.Report("fires", r.Message); });
            }
        var at = Session.Unproject(world);
        Item("CAP station here…", () => PlanAt(null, "cap", at));
        Item("AEW orbit here…", () => PlanAt(null, "aew", at));
        Item("Maritime patrol / recon here…", () => PlanAt(null, "recon", at));
    }

    /// <summary>Open the planner filled for a mission: target (site id) or a station point, plus a flight template.</summary>
    private void PlanAt(string? siteId, string template, (double Lon, double Lat)? station = null)
    {
        _planStation = station;
        FillPlanner();
        _planTarget.Select(Math.Max(0, _planTargets.IndexOf(siteId ?? "@pt")));
        FillPlanAim();
        ApplyTemplate(template);
        Open("ATO");
        RunPlan(false);
    }

    /// <summary>Planner templates: fill the flight rows from idle aircraft, best-rated first. Game ratings only.</summary>
    private void ApplyTemplate(string t)
    {
        var side = CommandSide;
        var idle = Trial.Air!.IdleByType(side).Select(x => (x.Preset, x.Count, P: Session.Content.PresetInfo(x.Preset))).Where(x => x.P != null).ToList();
        string? Best(string rating, Func<PresetInfo, bool>? keep = null) => idle.Where(x => keep?.Invoke(x.P!) ?? true).OrderByDescending(x => x.P!.R(rating)).ThenByDescending(x => x.Count).FirstOrDefault().Preset;
        bool Aew(PresetInfo p) => Symbols.For(p.Id, "air") == "AEW";
        bool Tkr(PresetInfo p) => Symbols.For(p.Id, "air") == "TKR";
        bool Mpa(PresetInfo p) => Symbols.For(p.Id, "air") == "MPA";
        bool Combat(PresetInfo p) => !Aew(p) && !Tkr(p) && !Mpa(p);
        var rows = t switch
        {
            "strike" => new[] { (Best("strike", Combat), 4, Mission.Strike, Formation.Box), (Best("air-defence", Combat), 2, Mission.Escort, Formation.Wedge), (Best("reach", Tkr), 1, Mission.Tanker, Formation.Trail) },
            "sead" => new[] { (Best("strike", Combat), 4, Mission.Strike, Formation.Box), (Best("strike", Combat), 2, Mission.SEAD, Formation.LineAbreast), (Best("air-defence", Combat), 2, Mission.Escort, Formation.Wedge), (Best("reach", Tkr), 1, Mission.Tanker, Formation.Trail) },
            "cap" => new[] { (Best("air-defence", Combat), 2, Mission.CAP, Formation.LineAbreast) },
            "aew" => new[] { (Best("detection", Aew), 1, Mission.AEW, Formation.Trail), (Best("air-defence", Combat), 2, Mission.CAP, Formation.LineAbreast) },
            _ => new[] { (Best("detection", Mpa) ?? Best("detection", Combat), 1, Mission.Recon, Formation.Trail) },
        };
        for (var i = 0; i < _rows.Count; i++)
        {
            var (type, count, mission, form) = _rows[i];
            if (i >= rows.Length || rows[i].Item1 == null) { type.Select(0); continue; }
            type.Select(Math.Max(0, _rowTypes[i].IndexOf(rows[i].Item1!)));
            count.Value = rows[i].Item2;
            mission.Select(Array.IndexOf(Missions, rows[i].Item3));
            form.Select(Array.IndexOf(Formations, rows[i].Item4));
        }
    }

    private HBoxContainer TemplateBar()
    {
        var h = new HBoxContainer();
        h.AddChild(UiTheme.Text("TEMPLATE", 12, UiTheme.Dim));
        foreach (var (k, n) in new[] { ("strike", "Strike"), ("sead", "SEAD strike"), ("cap", "CAP"), ("aew", "AEW + CAP"), ("recon", "Recon") })
        {
            var b = new Button { Text = n };
            b.AddThemeFontSizeOverride("font_size", 11);
            b.Pressed += () => { ApplyTemplate(k); RunPlan(false); };
            h.AddChild(b);
        }
        return h;
    }
}
