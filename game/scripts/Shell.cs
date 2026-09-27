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

    public override void _Ready()
    {
        if (Session.World == null) Session.Start(DevScreenshot.Arg("select") ?? "JPN", int.TryParse(DevScreenshot.Arg("year"), out var y) ? y : 2020);
        DevSetup();

        // The map renders straight into the main viewport (no off-screen render target); the UI floats on a
        // CanvasLayer above it so the camera never moves the interface.
        _map = new StrategicMap();
        if (DevScreenshot.Arg("no-map") == null) AddChild(_map);
        _map.RouteHovered += ShowRoute;
        var ui = new CanvasLayer { Layer = 1 };
        AddChild(ui);

        var col = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1, Theme = UiTheme.Build(), MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 0);
        ui.AddChild(col);
        col.AddChild(TopBar());

        var middle = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        foreach (var s in new[] { "left", "right", "top", "bottom" }) middle.AddThemeConstantOverride($"margin_{s}", 8);
        col.AddChild(middle);
        var bezel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        bezel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0, 0, 0, 0), new Color(UiTheme.Accent, 0.25f), 6, 2)); // border only
        middle.AddChild(bezel);
        var stack = new Control { MouseFilter = MouseFilterEnum.Ignore };
        bezel.AddChild(stack);

        stack.AddChild(MapModes());
        _info = UiTheme.Text("Hover a trade corridor for details.", 13, UiTheme.Dim, wrap: true);
        _info.CustomMinimumSize = new Vector2(330, 0); // fixed width: autowrap labels without one re-layout every frame
        var infoBox = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -360, OffsetRight = -10, OffsetTop = 10 };
        infoBox.AddChild(_info);
        stack.AddChild(infoBox);
        stack.AddChild(Legend());

        _bannerBox = new PanelContainer { AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -330, OffsetRight = 330, OffsetTop = 10, Visible = false };
        _bannerBox.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.25f, 0.03f, 0.05f, 0.95f), UiTheme.Bad, 6, 2));
        var bh = new HBoxContainer();
        _banner = UiTheme.Text("", 15);
        bh.AddChild(_banner);
        var enter = new Button { Text = "Enter Theatre Ops ▸" };
        enter.Pressed += () => ShowPlaceholder("Theatre Ops");
        bh.AddChild(enter);
        _bannerBox.AddChild(bh);
        stack.AddChild(_bannerBox);

        _placeholder = new PanelContainer { AnchorRight = 1, AnchorBottom = 1, Visible = false };
        _placeholderText = UiTheme.Text("", 18, UiTheme.Dim, wrap: true);
        _placeholderText.CustomMinimumSize = new Vector2(900, 0);
        _placeholder.AddChild(_placeholderText);
        stack.AddChild(_placeholder);

        col.AddChild(DockBar());
        UpdateDate();
        DevAfterBuild();
    }

    private Control TopBar()
    {
        var bar = new PanelContainer { CustomMinimumSize = new Vector2(0, 54) };
        bar.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.015f, 0.045f, 0.08f, 0.96f), new Color(UiTheme.Accent, 0.18f), 0));
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 16);
        bar.AddChild(h);
        h.AddChild(UiTheme.Text("BAD WATERS", 22, UiTheme.Accent));
        h.AddChild(UiTheme.Text("STRATEGIC", 13, UiTheme.Dim));
        _date = UiTheme.Text("", 18);
        h.AddChild(_date);
        var group = new ButtonGroup();
        foreach (var (label, len) in Turns)
        {
            var b = new Button { Text = label, ToggleMode = true, ButtonGroup = group, ButtonPressed = len == _turn };
            b.Pressed += () => _turn = len;
            h.AddChild(b);
        }
        h.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        h.AddChild(UiTheme.Text($"{Session.Content.Theatres.Count} theatres · {Session.Content.Routes.Count} corridors · player {Session.World.Player}", 13, UiTheme.Dim));
        var adv = new Button { Text = "Advance ▸", CustomMinimumSize = new Vector2(150, 0) };
        adv.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTheme.Accent, UiTheme.Accent));
        adv.AddThemeColorOverride("font_color", UiTheme.AccentInk);
        adv.Pressed += () => Advance(_turn);
        h.AddChild(adv);
        return bar;
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
        return h;
    }

    private Control Legend()
    {
        var box = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, OffsetTop = -52, OffsetLeft = 10, OffsetBottom = -10 };
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 14);
        box.AddChild(h);
        foreach (var (t, c) in new[] { ("Fully open", StrategicMap.Open), ("Contested", StrategicMap.Contested), ("Effectively closed", StrategicMap.Closed) })
        {
            h.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(22, 6), SizeFlagsVertical = SizeFlags.ShrinkCenter });
            h.AddChild(UiTheme.Text(t, 13));
        }
        return box;
    }

    private Control DockBar()
    {
        var bar = new PanelContainer { CustomMinimumSize = new Vector2(0, 50) };
        bar.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.015f, 0.045f, 0.08f, 0.96f), new Color(UiTheme.Accent, 0.18f), 0));
        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        h.AddThemeConstantOverride("separation", 6);
        bar.AddChild(h);
        var group = new ButtonGroup();
        foreach (var name in Dock)
        {
            var b = new Button { Text = name, ToggleMode = true, ButtonGroup = group, ButtonPressed = name == "Map" };
            b.Pressed += () => { if (name == "Map") ShowMap(); else ShowPlaceholder(name); };
            h.AddChild(b);
        }
        return bar;
    }

    private void ShowMap() { _placeholder.Visible = false; }
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
        _map.RefreshRoutes();
        if (irq != null)
        {
            _bannerBox.Visible = true;
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
                     $"Chokepoints: {string.Join(", ", def.Chokepoints)}";
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
            Session.Scheduler.DailyHook = (w, now) =>
            {
                if ((now - new DateTime(Session.StartYear, 1, 1)).TotalDays >= 10) { w.Theatres[theatre].AtWar = true; w.Theatres[theatre].PlayerInvolved = true; w.Theatres[theatre].HostileNavalThreat = 80; }
                return null;
            };
    }

    private void DevAfterBuild()
    {
        if (DevScreenshot.Arg("mode") is { } m) _map.SetMode(m == "topographic" ? 1 : m == "tactical" ? 2 : 0);
        if (DevScreenshot.Arg("view") == "world") _map.CenterOn(60, 20, 0.32f);
        if (DevScreenshot.Arg("hover") is { } r) ShowRoute(r);
        if (DevScreenshot.Arg("advance") is { } a) Advance(a == "year" ? TurnLength.Year : TurnLength.Month);
        DevScreenshot.MaybeCapture(this);
    }
}
