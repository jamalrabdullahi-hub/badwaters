using BadWaters.Sim;
using BadWaters.Sim.Time;
using BadWaters.Sim.Trade;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Strategic shell (Ops Floor · War Room colours): top bar, map viewport, bottom dock. Strategic time is
/// turn-based and for when you are NOT fighting; an interrupt (war) hands over to Theatre Ops.
/// </summary>
public partial class Shell : Control
{
    private static readonly (string Label, TurnLength Len)[] Turns =
    {
        ("1 day", TurnLength.Day), ("1 week", TurnLength.Week), ("1 month", TurnLength.Month),
        ("3 months", TurnLength.Quarter), ("6 months", TurnLength.HalfYear), ("1 year", TurnLength.Year),
    };
    private static readonly string[] Dock =
        { "Map", "Development", "Research", "Industry", "Trade & Logistics", "Forces & Bases", "Personnel", "Intelligence", "Diplomacy", "Budget", "Theatre Ops" };

    private TurnLength _turn = TurnLength.Week;
    private Label _date = null!, _info = null!, _banner = null!;
    private PanelContainer _bannerBox = null!, _placeholder = null!;
    private Label _placeholderText = null!;
    private StrategicMap _map = null!;
    private Control _stack = null!, _mapUi = null!;
    private DevelopmentScreen? _dev;
    private IndustryScreen? _industry;
    private TradeScreen? _trade;
    private TheatreOps? _theatre;

    public override void _Ready()
    {
        if (Session.World == null) Session.Start(DevScreenshot.Arg("select") ?? "JPN", int.TryParse(DevScreenshot.Arg("year"), out var y) ? y : 2020);
        DevSetup();

        // The map renders straight into the main viewport (no off-screen render target); the UI floats on a
        // CanvasLayer above it so the camera never moves the interface.
        _map = new StrategicMap();
        if (DevScreenshot.Arg("no-map") == null) AddChild(_map);
        _map.RouteHovered += ShowRoute;
        _map.ChokepointHovered += ShowChokepoint;
        _map.ChokepointClicked += ToggleChokepoint;
        var ui = new CanvasLayer { Layer = 1 };
        AddChild(ui);

        var col = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1, Theme = UiTheme.Build(), MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 0);
        ui.AddChild(col);

        var middle = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        foreach (var s in new[] { "left", "right", "top", "bottom" }) middle.AddThemeConstantOverride($"margin_{s}", 8);
        col.AddChild(middle);
        var bezel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        bezel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0, 0, 0, 0), new Color(UiTheme.Accent, 0.25f), 6, 2)); // border only
        middle.AddChild(bezel);
        var stack = new Control { MouseFilter = MouseFilterEnum.Ignore };
        bezel.AddChild(stack);
        _stack = stack;
        _mapUi = new Control { AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore };
        stack.AddChild(_mapUi);

        _mapModes = MapModes();
        stack.AddChild(_mapModes);
        _info = UiTheme.Text("Hover a trade corridor for details.", 13, UiTheme.Dim, wrap: true);
        _info.CustomMinimumSize = new Vector2(330, 0); // fixed width: autowrap labels without one re-layout every frame
        var infoBox = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -360, OffsetRight = -10, OffsetTop = 10 };
        infoBox.AddChild(_info);
        _mapUi.AddChild(infoBox);
        _mapUi.AddChild(Legend());

        _bannerBox = new PanelContainer { AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -330, OffsetRight = 330, OffsetTop = 10, Visible = false };
        _bannerBox.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.25f, 0.03f, 0.05f, 0.95f), UiTheme.Bad, 6, 2));
        var bh = new HBoxContainer();
        _banner = UiTheme.Text("", 15);
        bh.AddChild(_banner);
        var enter = new Button { Text = "Fight in Theatre Ops ▸", TooltipText = "This theatre waits for you each turn until you have fought it up to date." };
        enter.Pressed += () =>
        {
            _bannerBox.Visible = false;
            if (_bannerTheatre != null && Session.Campaign != null) Session.Campaign.Mode[_bannerTheatre] = BadWaters.Sim.Command.TheatreMode.Fight;
            Open("Theatre Ops");
        };
        bh.AddChild(enter);
        var auto = new Button { Text = "Auto-resolve", TooltipText = "The commanders fight it each day; results carry into the next turn." };
        auto.Pressed += () =>
        {
            _bannerBox.Visible = false;
            if (_bannerTheatre != null && Session.Campaign != null) Session.Campaign.Mode[_bannerTheatre] = BadWaters.Sim.Command.TheatreMode.Auto;
        };
        bh.AddChild(auto);
        _bannerBox.AddChild(bh);
        stack.AddChild(_bannerBox);

        _placeholder = new PanelContainer { AnchorRight = 1, AnchorBottom = 1, Visible = false };
        _placeholderText = UiTheme.Text("", 18, UiTheme.Dim, wrap: true);
        _placeholderText.CustomMinimumSize = new Vector2(900, 0);
        _placeholder.AddChild(_placeholderText);
        stack.AddChild(_placeholder);

        _dock = DockBar();
        col.AddChild(_dock);
        UpdateDate();
        DevAfterBuild();
    }

    private void TurnControls(HBoxContainer h)
    {
        _date = UiTheme.Text("", 18);
        h.AddChild(_date);
        var group = new ButtonGroup();
        foreach (var (label, len) in Turns)
        {
            var b = new Button { Text = label, ToggleMode = true, ButtonGroup = group, ButtonPressed = len == _turn };
            b.Pressed += () => _turn = len;
            h.AddChild(b);
        }
        var adv = new Button { Text = "Advance ▸", CustomMinimumSize = new Vector2(150, 0) };
        adv.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTheme.Accent, UiTheme.Accent));
        adv.AddThemeColorOverride("font_color", UiTheme.AccentInk);
        adv.Pressed += () => Advance(_turn);
        h.AddChild(adv);
    }

    private Control MapModes()
    {
        var h = new HBoxContainer { OffsetLeft = 10, OffsetTop = 10 };
        var group = new ButtonGroup();
        foreach (var (name, mode) in new[] { ("Satellite", StrategicMap.Satellite), ("Topographic", StrategicMap.Topographic), ("Tactical", StrategicMap.Tactical) })
        {
            var start = DevScreenshot.Arg("mode") switch { "topographic" => StrategicMap.Topographic, "tactical" => StrategicMap.Tactical, _ => StrategicMap.Satellite };
            var b = new Button { Text = name, ToggleMode = true, ButtonGroup = group, ButtonPressed = mode == start };
            b.Pressed += () => _map.SetMode(mode);
            h.AddChild(b);
        }
        var layersBtn = new Button { Text = "Layers ▾", ToggleMode = true };
        h.AddChild(layersBtn);
        var panel = LayerPanel();
        panel.Visible = DevScreenshot.Arg("layers-open") != null;
        layersBtn.ButtonPressed = panel.Visible;
        layersBtn.Toggled += on => panel.Visible = on;
        var col = new VBoxContainer { OffsetLeft = 10, OffsetTop = 10 };
        col.AddChild(h);
        col.AddChild(panel);
        return col;
    }

    /// <summary>Map layers by group. Real Earth layers are facts; (sim) layers come from the game and its fog of war.</summary>
    private Control LayerPanel()
    {
        var box = new PanelContainer { CustomMinimumSize = new Vector2(250, 0) };
        var sc = new ScrollContainer { CustomMinimumSize = new Vector2(250, 520), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 0);
        sc.AddChild(v); box.AddChild(sc);
        foreach (var g in _map.LayerDefs.GroupBy(d => d.Group))
        {
            v.AddChild(UiTheme.Text(g.Key, 12, UiTheme.Accent));
            foreach (var d in g)
            {
                var later = d.Kind == "later";
                var cb = new CheckBox { Text = d.Name + (d.Sim ? "  (sim)" : "") + (later ? "  · later" : ""), ButtonPressed = _map.LayerOn(d.Id), Disabled = later };
                var id = d.Id;
                cb.Toggled += on => _map.SetLayer(id, on);
                v.AddChild(cb);
            }
        }
        v.AddChild(UiTheme.Text("Map data: Natural Earth, NASA Blue Marble (public domain). Civil geography only.", 10, UiTheme.Dim, wrap: true));
        return box;
    }

    private Control Legend()
    {
        var box = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, OffsetTop = -52, OffsetLeft = 10, OffsetBottom = -10 };
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 14);
        box.AddChild(h);
        foreach (var (t, c) in new[] { ("Sea lane open", StrategicMap.Open), ("Land corridor open", StrategicMap.LandOpen), ("Contested", StrategicMap.Contested), ("Closed", StrategicMap.Closed), ("Control point", ChokepointLayer.OpenCol) })
        {
            h.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(22, 6), SizeFlagsVertical = SizeFlags.ShrinkCenter });
            h.AddChild(UiTheme.Text(t, 13));
        }
        return box;
    }

    private Control DockBar()
    {
        var bar = new PanelContainer { CustomMinimumSize = new Vector2(0, 50) };
        bar.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.006f, 0.018f, 0.038f, 0.97f), new Color(UiTheme.Accent, 0.18f), 0));
        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        h.AddThemeConstantOverride("separation", 6);
        bar.AddChild(h);
        var group = new ButtonGroup();
        foreach (var name in Dock)
        {
            var b = new Button { Text = name, ToggleMode = true, ButtonGroup = group, ButtonPressed = name == "Map" };
            b.Pressed += () => Open(name);
            _dockButtons[name] = b;
            h.AddChild(b);
        }
        h.AddChild(new Control { CustomMinimumSize = new Vector2(24, 0) });
        TurnControls(h);
        return bar;
    }

    private readonly Dictionary<string, Button> _dockButtons = new();
    private string? _bannerTheatre;
    private Control _dock = null!;

    private Control _mapModes = null!;

    private void Open(string name)
    {
        if (_dockButtons.TryGetValue(name, out var btn)) btn.ButtonPressed = true;
        _placeholder.Visible = false;
        if (_dev != null) _dev.Visible = name == "Development";
        if (_industry != null) _industry.Visible = name == "Industry";
        if (_trade != null) _trade.Visible = name == "Trade & Logistics";
        var theatre = name == "Theatre Ops";
        if (_theatre != null) { _theatre.Visible = theatre; _theatre.Layer.Visible = theatre; }
        _mapUi.Visible = name == "Map";
        // Theatre Ops is its own game: no strategic dock, no strategy/design/economy screens while you're in it
        _dock.Visible = !theatre;
        _mapModes.Visible = !theatre; // strategic map modes and layers belong to the strategic game
        _map.ShowRoutes(!theatre);
        if (name == "Map") return;
        if (name == "Development")
        {
            if (_dev == null) { _dev = new DevelopmentScreen(); _stack.AddChild(_dev); _dev.DesignsChanged += () => { if (_theatre != null) { _theatre.RefreshPickers(); _theatre.Reset(); } }; }
            _dev.Visible = true;
            return;
        }
        if (name == "Industry")
        {
            if (_industry == null) { _industry = new IndustryScreen(); _stack.AddChild(_industry); } else _industry.Refresh();
            _industry.Visible = true;
            return;
        }
        if (name == "Trade & Logistics")
        {
            if (_trade == null) { _trade = new TradeScreen(); _stack.AddChild(_trade); } else _trade.Refresh();
            _trade.Visible = true;
            return;
        }
        if (theatre)
        {
            if (_theatre == null)
            {
                _theatre = new TheatreOps(); _stack.AddChild(_theatre); _map.AddOverlay(_theatre.Layer);
                _theatre.ReturnRequested += () => Open("Map");
                _theatre.AutoResolveRequested += () =>
                {
                    var th = _theatre.Scenario.Theatre;
                    if (Session.Campaign != null && th != "" && Session.Campaign.Live.TryGetValue(th, out var live))
                    {
                        Session.Campaign.Mode[th] = BadWaters.Sim.Command.TheatreMode.Auto;
                        Session.Campaign.Resolve(live, Session.Campaign.Days.GetValueOrDefault(th) * BadWaters.Sim.Command.TheatreCampaign.Day);
                        Session.Campaign.WriteBack(Session.World, live);
                    }
                    Open("Map");
                };
            }
            _theatre.Visible = true; _theatre.Layer.Visible = true;
            // a theatre at war with the player opens its own scenario; before a war, your own nation's theatre in
            // "not at war" mode (track only); the proving-ground trial only for nations with no theatre
            var war = Session.World.Theatres.Values.Where(t => t.AtWar && t.PlayerInvolved).Select(t => t.Id).ToHashSet();
            var mine = Session.Content.Scenarios.Values.FirstOrDefault(x => x.Theatre != "" && x.Sides.Values.Any(n => Session.NationId(n) == Session.World.Player));
            var id = DevScreenshot.Arg("scenario") ?? Session.Content.Scenarios.Values.FirstOrDefault(x => war.Contains(x.Theatre))?.Id ?? mine?.Id ?? "intercept-trial";
            var nowAtWar = Session.Content.Scenarios.TryGetValue(id, out var target) && war.Contains(target.Theatre);
            if (_theatre.Scenario.Id != id || (target?.Theatre is { Length: > 0 } && _theatre.Trial.AtWar != nowAtWar)) _theatre.Load(id); // the war started: switch to the live campaign theatre
            var sc = _theatre.Scenario;
            _map.CenterOn(sc.Center[0], sc.Center[1], sc.Zoom);
            _map.PanScreen(new Vector2(0, -TheatreOps.TopH / 2f)); // map-first: only the command bar covers it
            return;
        }
        ShowPlaceholder(name);
    }

    private void ShowMap() => Open("Map");
    private void ShowPlaceholder(string name)
    {
        _placeholder.Visible = true;
        _placeholderText.Text = name == "Theatre Ops"
            ? "THEATRE OPS\n\nThe live war layer arrives in milestone M5: pick a theatre, fight in real time at 1×–100×, with tasking and the C2 & comms network. It runs on the same world as this map."
            : $"{name.ToUpperInvariant()}\n\nThis screen arrives in a later milestone. It will read and change the same shared world state as the map.";
    }

    private void Advance(TurnLength len)
    {
        var irq = Session.Scheduler.AdvanceStrategic(len);
        UpdateDate();
        _map.RefreshRoutes(); _map.Redraw();
        if (_industry?.Visible == true) _industry.Refresh();
        if (_trade?.Visible == true) _trade.Refresh();
        if (irq != null)
        {
            _bannerBox.Visible = true;
            _bannerTheatre = irq.Theatre;
            _banner.Text = irq.Kind == "war" ? $"WAR · {irq.Text}  " : $"{irq.Text}  ";
        }
    }

    private void UpdateDate() => _date.Text = Session.World.Now.ToString("d MMM yyyy");

    private void ShowRoute(string? id)
    {
        if (id == null) { _info.Text = "Hover a trade corridor for details."; return; }
        var def = Session.Content.Routes[id];
        var st = Session.World.Routes[id];
        var status = st.Status switch { RouteStatus.FullyOpen => "Fully open", RouteStatus.Contested => "Contested", _ => "Effectively closed" };
        _info.Text = $"{def.Name}\n{status} · {st.Step} (disruption {st.Disruption:0})\n" +
                     $"Throughput {def.Throughput * st.DeliveryFactor:0}/{def.Throughput:0} per month\n" +
                     $"Cost ×{st.CostMultiplier:0.00} · transit {def.TransitDays * st.DelayMultiplier:0} days\n" +
                     $"{(def.Mode == "land" ? "Land corridor" : "Sea lane")} · control points: " +
                     (def.Chokepoints.Length == 0 ? "none" : string.Join(", ", def.Chokepoints.Select(c => Session.Content.Chokepoints[c].Name + (Session.World.ClosedChokepoints.ContainsKey(c) ? " (SHUT)" : ""))));
    }

    private void ShowChokepoint(string id)
    {
        var c = Session.Content.Chokepoints[id];
        var shut = Session.World.ClosedChokepoints.TryGetValue(id, out var by);
        var routes = Session.Content.Routes.Values.Where(r => r.Chokepoints.Contains(id)).ToList();
        _info.Text = $"{c.Name}{(c.Critical ? " · critical control point" : "")}\n" +
                     (shut ? $"SHUT{(by != "" ? $" by {by}" : "")}. Routes through it are closed.\n" : "Open.\n") +
                     $"Routes through it ({routes.Count}): {string.Join(", ", routes.Select(r => r.Name))}\n" +
                     $"Throughput at stake: {routes.Sum(r => r.Throughput):0} per month\n" +
                     $"Click to {(shut ? "reopen" : "shut")} it.";
    }

    /// <summary>Shut or reopen a control point (a blockade or closure as a game action).</summary>
    private void ToggleChokepoint(string id)
    {
        Session.Scheduler.SetChokepoint(id, !Session.World.ClosedChokepoints.ContainsKey(id), Session.World.Player);
        _map.RefreshRoutes();
        _map.Redraw();
        ShowChokepoint(id);
    }

    // ---- dev helpers for automated screenshots
    private void DevSetup()
    {
        if (DevScreenshot.Arg("demo-routes") != null)
        {
            Session.World.Routes["gulf-asia"].Disruption = 90;
            Session.World.Routes["gulf-europe"].Disruption = 70;
            Session.World.Routes["east-asia-europe"].Disruption = 40;
            Session.World.Routes["east-asia-australia"].Disruption = 25;
        }
        if (DevScreenshot.Arg("war") is { } theatre)
            Session.ExtraDaily = (w, now) =>
            {
                if ((now - new DateTime(Session.StartYear, 1, 1)).TotalDays >= 10) { w.Theatres[theatre].AtWar = true; w.Theatres[theatre].PlayerInvolved = true; w.Theatres[theatre].HostileNavalThreat = 80; }
                return null;
            };
    }

    private void DevAfterBuild()
    {
        if (DevScreenshot.Arg("mode") is { } m) _map.SetMode(m == "topographic" ? 1 : m == "tactical" ? 2 : 0);
        if (DevScreenshot.Arg("shut") is { } shutIds) foreach (var cid in shutIds.Split(',')) ToggleChokepoint(cid);
        if (DevScreenshot.Arg("view") == "world") _map.CenterOn(60, 20, 0.32f);
        if (DevScreenshot.Arg("center")?.Split(',') is { Length: 3 } cz) _map.CenterOn(double.Parse(cz[0]), double.Parse(cz[1]), float.Parse(cz[2]));
        if (DevScreenshot.Arg("hover") is { } r) ShowRoute(r);
        if (DevScreenshot.Arg("programme")?.Split(':') is { Length: 2 } pg)
            Session.World.Economy.Nations[Session.World.Player].Programmes.Add(new BadWaters.Sim.Economy.Programme { Id = "dev", Design = pg[0], RatePerMonth = double.Parse(pg[1]) });
        if (DevScreenshot.Arg("sanction")?.Split(':') is { Length: >= 3 } sc)
            Session.World.Economy.Sanction(sc[0], sc[1], sc[2], sc.Length > 3 && sc[3] == "global");
        if (DevScreenshot.Arg("advance") is { } a) Advance(a == "year" ? TurnLength.Year : TurnLength.Month);
        if (DevScreenshot.Arg("screen") is { } screen) Open(screen switch { "dev" => "Development", "theatre" => "Theatre Ops", "industry" => "Industry", "trade" => "Trade & Logistics", _ => "Map" });
        if (_theatre != null && DevScreenshot.Arg("run") is { } run && double.TryParse(run, out var secs))
        {
            if (DevScreenshot.Arg("fire") is { } target) _theatre.Trial.Launch(_theatre.Trial.Launchers.First(l => l.Def.Side == "red").Def.Id, target, 2);
            _theatre.Trial.Advance(secs);
            _theatre.RefreshWindows(force: true);
            if (DevScreenshot.Arg("track") is { } tk) _theatre.SelectedTrack = int.Parse(tk);
            if (DevScreenshot.Arg("flight") is { } fl) _theatre.SelectedFlight = _theatre.Trial.Air?.Flights.FirstOrDefault(f => f.Callsign.StartsWith(fl));
            if (DevScreenshot.Arg("view") is { } vw && vw != "world") { _theatre.ViewSide = vw; _theatre.UpdateUi(); }
            if (DevScreenshot.Arg("plan") is { } pt)
                _theatre.DevPlan(pt, ("JPF15J", 4, BadWaters.Sim.Theatre.Mission.Strike, BadWaters.Sim.Theatre.Formation.Box),
                    ("USF35A", 2, BadWaters.Sim.Theatre.Mission.Escort, BadWaters.Sim.Theatre.Formation.Wedge),
                    ("JPKC46A", 1, BadWaters.Sim.Theatre.Mission.Tanker, BadWaters.Sim.Theatre.Formation.Trail));
            _theatre.UpdateUi();
        }
        DevScreenshot.MaybeCapture(this);
    }
}
