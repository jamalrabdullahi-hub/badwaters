using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>Theatre Ops · Air tasking tab: the ATO, flight cards with timelines, and the package planner.</summary>
public partial class TheatreOps
{
    public Flight? SelectedFlight;
    /// <summary>Flights from the last "Plan" press, drawn dashed on the map until committed or replanned.</summary>
    public readonly List<Flight> Preview = new();

    private VBoxContainer _airView = null!, _atoList = null!, _timeline = null!, _planner = null!;
    private Label _flightCard = null!, _planResult = null!;
    private OptionButton _planSide = null!, _planTarget = null!;
    private SpinBox _planTot = null!, _planIp = null!;
    private readonly List<(OptionButton Type, SpinBox Count, OptionButton Mission, OptionButton Formation)> _rows = new();
    private readonly List<string> _planSides = new(), _planTargets = new();
    private readonly List<List<string>> _rowTypes = new();
    private static readonly Mission[] Missions = { Mission.Strike, Mission.Escort, Mission.SEAD, Mission.Tanker, Mission.CAP, Mission.AEW };
    private static readonly Formation[] Formations = { Formation.Wedge, Formation.LineAbreast, Formation.Trail, Formation.EchelonRight, Formation.Box };
    private int _atoSeen = -1;

    private void BuildAirView()
    {
        _planner = new VBoxContainer();
        _airView.AddChild(_planner);
        _planner.AddChild(UiTheme.Text("PLAN A PACKAGE", 13, UiTheme.Accent));
        var r1 = new HBoxContainer();
        _planSide = new OptionButton { CustomMinimumSize = new Vector2(90, 0) };
        _planSide.ItemSelected += _ => FillPlanTypes();
        _planTarget = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        r1.AddChild(_planSide); r1.AddChild(_planTarget);
        _planner.AddChild(r1);
        var r2 = new HBoxContainer();
        r2.AddChild(UiTheme.Text("TOT T+", 12, UiTheme.Dim));
        _planTot = new SpinBox { MinValue = 5, MaxValue = 240, Value = 70, Suffix = "min" };
        r2.AddChild(_planTot);
        r2.AddChild(UiTheme.Text("  IP", 12, UiTheme.Dim));
        _planIp = new SpinBox { MinValue = 20, MaxValue = 150, Value = 60, Step = 10, Suffix = "km" };
        r2.AddChild(_planIp);
        _planner.AddChild(r2);
        for (var i = 0; i < 4; i++)
        {
            var h = new HBoxContainer();
            var type = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
            var count = new SpinBox { MinValue = 1, MaxValue = 6, Value = i == 0 ? 4 : 2 };
            var mission = new OptionButton();
            foreach (var m in Missions) mission.AddItem(m.ToString());
            mission.Select(i switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 });
            var form = new OptionButton();
            foreach (var f in Formations) form.AddItem(f.ToString());
            form.Select(i == 0 ? 4 : 0);
            h.AddChild(UiTheme.Text($"{i + 1}", 12, UiTheme.Dim)); h.AddChild(type); h.AddChild(count);
            var h2 = new HBoxContainer();
            h2.AddChild(UiTheme.Text("   ", 12, UiTheme.Dim)); h2.AddChild(mission); h2.AddChild(form);
            mission.SizeFlagsHorizontal = form.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _planner.AddChild(h); _planner.AddChild(h2);
            _rows.Add((type, count, mission, form));
            _rowTypes.Add(new List<string>());
        }
        var r3 = new HBoxContainer();
        var plan = new Button { Text = "Plan (preview)", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        plan.Pressed += () => RunPlan(false);
        var commit = new Button { Text = "Commit to ATO", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        commit.Pressed += () => RunPlan(true);
        r3.AddChild(plan); r3.AddChild(commit);
        _planner.AddChild(r3);
        _planResult = UiTheme.Text("Pick a target and flights. Strike packages get take-off, ingress, IP, target, egress and landing times worked back from the TOT. With only CAP/AEW flights, the target becomes the patrol station.", 12, UiTheme.Dim, wrap: true);
        _planResult.CustomMinimumSize = new Vector2(410, 0);
        _planner.AddChild(_planResult);
        _airView.AddChild(UiTheme.Text("AIR TASKING ORDER", 13, UiTheme.Accent));
        _atoList = new VBoxContainer();
        _airView.AddChild(_atoList);
        _airView.AddChild(UiTheme.Text("SELECTED FLIGHT · CHARACTERISTICS", 13, UiTheme.Accent));
        _flightCard = UiTheme.Text("Click a flight.", 12, UiTheme.Dim, wrap: true);
        _flightCard.CustomMinimumSize = new Vector2(410, 0);
        _airView.AddChild(_flightCard);
        _timeline = new VBoxContainer();
        _airView.AddChild(_timeline);

    }

    /// <summary>Refill the planner for the sides the player controls.</summary>
    private void FillPlanner()
    {
        if (_planSide == null || Trial == null) return;
        _planSide.Clear(); _planSides.Clear();
        foreach (var s in new[] { "blue", "red" }.Where(s => Trial.Control.GetValueOrDefault(s) == SideControl.Player))
        {
            _planSides.Add(s);
            _planSide.AddItem(SideName(s));
        }
        _planner.Visible = _planSides.Count > 0;
        if (_planSides.Count > 0) { _planSide.Select(0); FillPlanTypes(); }
    }

    private void FillPlanTypes()
    {
        var side = _planSides[_planSide.Selected];
        _planTarget.Clear(); _planTargets.Clear();
        foreach (var s in Scenario.Sites.OrderBy(s => s.Side == side).ThenBy(s => s.Name))
        {
            _planTargets.Add(s.Id);
            _planTarget.AddItem(s.Side == side ? $"{s.Name} (CAP station)" : TargetPolicy.IsLegitimate(s.Class) ? s.Name : $"{s.Name} (protected)");
        }
        var idle = Trial.Air!.IdleByType(side).OrderBy(x => x.Name).ToList();
        for (var i = 0; i < _rows.Count; i++)
        {
            var o = _rows[i].Type;
            o.Clear(); _rowTypes[i].Clear();
            o.AddItem("— none —"); _rowTypes[i].Add("");
            foreach (var (preset, name, count) in idle) { o.AddItem($"{name} ({count} ready)"); _rowTypes[i].Add(preset); }
            o.Select(i < 2 && idle.Count > 0 ? Math.Min(idle.Count, i + 1) : 0);
        }
    }

    private void RunPlan(bool commit)
    {
        if (_planSides.Count == 0) return;
        var side = _planSides[_planSide.Selected];
        var target = Scenario.Sites.First(s => s.Id == _planTargets[_planTarget.Selected]);
        var reqs = _rows.Select((r, i) => (r, i)).Where(x => x.r.Type.Selected > 0)
            .Select(x => new FlightRequest(_rowTypes[x.i][x.r.Type.Selected], (int)x.r.Count.Value, Missions[x.r.Mission.Selected], Formations[x.r.Formation.Selected])).ToList();
        var strike = reqs.Any(r => r.Mission is Mission.Strike or Mission.Escort or Mission.SEAD);
        var entry = strike
            ? new AtoEntry { Side = side, Target = target.Id, Tot = _planTot.Value * 60, IpKm = _planIp.Value, Flights = reqs }
            : new AtoEntry { Side = side, StationLon = target.Lon, StationLat = target.Lat, Start = Trial.Time, Duration = 3600, Flights = reqs };
        var r = commit ? Trial.Air!.Commit(entry) : Trial.Air!.Plan(entry);
        Preview.Clear();
        if (!commit) Preview.AddRange(r.Flights);
        _planResult.Text = r.Ok
            ? (commit ? "Committed. " : "Plan OK. ") + string.Join("\n", r.Flights.Select(f => $"{f.Callsign}: {f.Aircraft.Count}× {f.Type.Name} {f.Mission} · take-off T+{AirTasking.Clock(f.Takeoff)} from {Trial.Site(f.BaseId)?.Name}" +
                  (f.Route.FirstOrDefault(w => w.Name == "IP") is { } ip ? $" · IP T+{AirTasking.Clock(ip.T)}" : "") +
                  $" · {f.Route.Skip(1).First().Name} T+{AirTasking.Clock(f.Route[1].T)}"))
            : "Can't plan this package:\n• " + string.Join("\n• ", r.Issues);
        _planResult.AddThemeColorOverride("font_color", r.Ok ? UiTheme.Good : UiTheme.Bad);
        if (commit && r.Ok) { SelectedFlight = r.Flights.FirstOrDefault(); FillPlanTypes(); }
        UpdateAir(true);
    }

    /// <summary>Dev aid for screenshots: fill the planner with a package and press Plan.</summary>
    public void DevPlan(string target, params (string Preset, int Count, Mission Mission, Formation Formation)[] rows)
    {
        FillPlanner();
        if (_planSides.Count == 0) return;
        _planTarget.Select(_planTargets.IndexOf(target));
        for (var i = 0; i < _rows.Count; i++)
        {
            if (i >= rows.Length) { _rows[i].Type.Select(0); continue; }
            _rows[i].Type.Select(Math.Max(0, _rowTypes[i].IndexOf(rows[i].Preset)));
            _rows[i].Count.Value = rows[i].Count;
            _rows[i].Mission.Select(Array.IndexOf(Missions, rows[i].Mission));
            _rows[i].Formation.Select(Array.IndexOf(Formations, rows[i].Formation));
        }
        RunPlan(false);
    }

    private void UpdateAir(bool force)
    {
        var air = Trial.Air;
        if (air == null) return;
        if (_planSide.ItemCount == 0 && Trial.Control.Values.Any(v => v == SideControl.Player)) FillPlanner();
        else if (_planSides.Count == 0) _planner.Visible = false;

        Clear(_atoList);
        foreach (var side in new[] { "blue", "red" })
        {
            _atoList.AddChild(UiTheme.Text($"{SideName(side).ToUpperInvariant()} ATO", 12, side == "blue" ? TheatreLayer.Blue : TheatreLayer.Red));
            foreach (var p in air.Packages.Where(p => p.Side == side))
            {
                var head = UiTheme.Text($"{p.Name} → {p.Target.Name} · TOT T+{AirTasking.Clock(p.Tot)}{(p.Tanker ? " · tanker" : "")}", 12, UiTheme.Ink);
                _atoList.AddChild(head);
                foreach (var f in p.Flights) _atoList.AddChild(FlightRow(f));
            }
            var stations = air.Flights.Where(f => f.Side == side && f.PackageId != "" && air.Packages.All(p => p.Id != f.PackageId)).ToList();
            if (stations.Count > 0) _atoList.AddChild(UiTheme.Text("Stations (CAP / AEW)", 12, UiTheme.Dim));
            foreach (var f in stations) _atoList.AddChild(FlightRow(f));
        }

        Clear(_timeline);
        var sel = SelectedFlight ?? Preview.FirstOrDefault();
        if (sel == null) { _flightCard.Text = "Click a flight to see its route, timing and characteristics."; return; }
        var t = sel.Type;
        var next = sel.Next(Trial.Time);
        var form = FormationRules.Mod(sel.Formation);
        var pkg = air.Packages.FirstOrDefault(p => p.Id == sel.PackageId);
        _flightCard.Text =
            $"{sel.Callsign} · {sel.Aircraft.Count}× {t.Name} · {sel.Mission} · {sel.Formation}{(sel.LostAircraft.Count > 0 ? $" · {sel.LostAircraft.Count} lost" : "")}\n" +
            $"From {Trial.Site(sel.BaseId)?.Name} · {sel.State}{(next != null ? $" · next {next.Name} at T+{AirTasking.Clock(next.T)}" : "")}\n" +
            $"Speed {t.R("speed"):0} → cruise {AirPacing.CruiseKmPerSec(t.R("speed")) * 3600:0} km/h · reach {t.R("reach"):0} → radius {AirPacing.RadiusKm(t.R("reach")):0} km{(pkg?.Tanker == true ? " (+300 with tanker)" : "")}\n" +
            $"Air-to-air {t.R("air-defence"):0} · strike {t.R("strike"):0} · payload {t.R("payload"):0} · stealth {t.R("signature"):0} · sensors {t.R("detection"):0} · agility {t.R("agility"):0}\n" +
            $"{form.Text}" + (pkg?.Result is { Length: > 0 } res ? $"\nResult: {res}" : "");
        Waypoint? prev = null;
        foreach (var w in sel.Route)
        {
            var km = prev == null ? 0 : Pacing.DistanceKm(prev.Lon, prev.Lat, w.Lon, w.Lat);
            var done = w.T <= Trial.Time;
            var c = w == next ? UiTheme.Accent : done ? UiTheme.Dim : UiTheme.Ink;
            var label = UiTheme.Text($"{(w == next ? "▸ " : "   ")}{w.Name,-12} T+{AirTasking.Clock(w.T)}   {(prev == null ? "" : $"{km:0} km")}", 12, c);
            _timeline.AddChild(label);
            prev = w;
        }
    }

    private Control FlightRow(Flight f)
    {
        var tot = f.Route.FirstOrDefault(w => w.Name is "Target" or "Sweep" or "SEAD" or "Station");
        var b = new Button
        {
            Text = $"{f.Callsign} · {f.Aircraft.Count}× {f.Type.Name} · {f.Mission} · {f.Formation} · T/O {AirTasking.Clock(f.Takeoff)}{(tot != null ? $" · {tot.Name} {AirTasking.Clock(tot.T)}" : "")} · {f.State}",
            ToggleMode = true, ButtonPressed = SelectedFlight == f, Alignment = HorizontalAlignment.Left, ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        b.AddThemeColorOverride("font_color", f.State == FlightState.Lost ? UiTheme.Bad : f.State == FlightState.Landed ? UiTheme.Dim : f.Side == "blue" ? TheatreLayer.Blue : new Color("ff8a8a"));
        b.AddThemeFontSizeOverride("font_size", 12);
        b.Pressed += () => { SelectedFlight = SelectedFlight == f ? null : f; UpdateAir(true); };
        return b;
    }
}
