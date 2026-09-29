using BadWaters.Sim.Command;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>A note or report the player pinned to a map position (Theatre Ops, T3 HUD).</summary>
public sealed record MapReport(int Id, string Type, string Side, double Lon, double Lat, double T, string Text, string Share);

/// <summary>
/// T3 · map-first HUD: a bottom dock (F-keys) toggles every window, an alerts window, right-click context menu
/// (file a report at the cursor, move the selected ship) and the Reports window with a SITREP.
/// </summary>
public partial class TheatreOps
{
    /// <summary>Report types: key, name, colour. Kept in step with the Reports board in the design canvas.</summary>
    public static readonly (string Key, string Name, Color Color)[] ReportTypes =
    {
        ("NOTE", "Note", new Color("c9d6c3")), ("CONT", "Contact report", new Color("ff5a64")), ("THRT", "Threat warning", new Color("ff9a3c")),
        ("BDA", "Damage assessment", new Color("e8b04a")), ("RECON", "Recon request", new Color("4fbfee")), ("NAV", "Waypoint / rally", new Color("5cf2b0")),
        ("LOG", "Logistics request", new Color("b89cff")),
    };
    private static readonly string[] DockOrder = { "MISSION", "FRAG", "LOGISTICS", "OPERATION", "OOB", "INTEL", "REPORTS", "PRIORITIES", "COMMANDER", "FILTER", "LOG", "FIRES", "NAVAL", "AIR DEF", "ATO", "PICTURE", "BASES", "SYNC", "ALERTS", "UNIT" };
    /// <summary>Reports per theatre scenario (kept for the session).</summary>
    private static readonly Dictionary<string, List<MapReport>> AllReports = new();
    public List<MapReport> Reports => AllReports.TryGetValue(Scenario.Id, out var l) ? l : AllReports[Scenario.Id] = new List<MapReport>();
    private readonly Dictionary<string, Button> _dockButtons = new();
    private PopupMenu? _context;
    private (double Lon, double Lat) _contextAt;
    private string? _sitrep;

    private void BuildDock()
    {
        var dock = new PanelContainer { AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1, AnchorBottom = 1, GrowHorizontal = GrowDirection.Both, GrowVertical = GrowDirection.Begin, OffsetBottom = -10 };
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 4);
        dock.AddChild(h);
        AddChild(dock);
        for (var i = 0; i < DockOrder.Length; i++)
        {
            var key = DockOrder[i];
            if (!_windows.ContainsKey(key)) continue;
            var b = new Button { Text = key, ToggleMode = true, TooltipText = i < 12 ? $"F{i + 1}" : "" };
            b.AddThemeFontSizeOverride("font_size", 11);
            b.Pressed += () => Toggle(key);
            h.AddChild(b);
            _dockButtons[key] = b;
        }
        _context = new PopupMenu();
        AddChild(_context);
        _context.IdPressed += ContextPicked;
    }

    private void Toggle(string key)
    {
        if (!_windows.TryGetValue(key, out var w)) return;
        if (w.Visible) w.Visible = false; else Open(key);
        UpdateUi();
    }

    /// <summary>Keep the dock lit for open windows (windows also close from their own ✕).</summary>
    private void SyncDock() { foreach (var (k, b) in _dockButtons) b.SetPressedNoSignal(_windows[k].Visible); }

    /// <summary>Hotkeys and the right-click menu (called from _UnhandledInput when no move order is pending).</summary>
    private void HudInput(InputEvent e)
    {
        if (RouteInput(e)) return;
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode >= Key.F1 && k.Keycode <= Key.F12)
            {
                var i = (int)(k.Keycode - Key.F1);
                var keys = DockOrder.Where(_windows.ContainsKey).ToList();
                if (i < keys.Count) { Toggle(keys[i]); GetViewport().SetInputAsHandled(); }
            }
            else if (k.Keycode == Key.Space) { SetSpeed(Speed == 0 ? _lastSpeed : 0); GetViewport().SetInputAsHandled(); }
            else if (k.Keycode >= Key.Key1 && k.Keycode <= Key.Key7) { SetSpeed(Speeds[(int)(k.Keycode - Key.Key1) + 1]); GetViewport().SetInputAsHandled(); }
            else if (k.Keycode == Key.R) { _contextAt = Session.Unproject(GetViewport().GetCanvasTransform().AffineInverse() * GetViewport().GetMousePosition()); NewReport("NOTE"); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } lc)
        {
            var wz = GetViewport().GetCamera2D()?.Zoom.X ?? 1;
            if (TryPick(GetViewport().GetCanvasTransform().AffineInverse() * lc.Position, wz)) { Layer.QueueRedraw(); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } mb || _context == null) return;
        var world = GetViewport().GetCanvasTransform().AffineInverse() * mb.Position;
        var zoom = GetViewport().GetCamera2D()?.Zoom.X ?? 1;
        _contextAt = Session.Unproject(world);
        _context.Clear();
        _context.AddSeparator($"{Fmt(_contextAt.Lat, "N", "S")} {Fmt(_contextAt.Lon, "E", "W")}");
        for (var i = 0; i < ReportTypes.Length; i++) _context.AddItem($"{ReportTypes[i].Name}   {i + 1}", i);
        if (SelectedShip is { Sunk: false } s && s.Side == CommandSide) { _context.AddSeparator(); _context.AddItem($"Move {s.Name} here", 100); }
        if (Reports.FirstOrDefault(r => Near(r, world)) is { } hit) { _context.AddSeparator(); _context.AddItem($"Delete report R-{hit.Id:000}", 200 + hit.Id); }
        AddMissionItems(_context, world, zoom);
        _context.Position = (Vector2I)(GetViewport().GetScreenTransform() * mb.Position);
        _context.Popup();
        GetViewport().SetInputAsHandled();
    }

    private static string Fmt(double v, string pos, string neg) => $"{Math.Abs(v):0.00}{(v >= 0 ? pos : neg)}";
    private static bool Near(MapReport r, Vector2 world) => Session.Project(r.Lon, r.Lat).DistanceTo(world) < 14;

    private int _lastSpeed = 10;
    private readonly List<(int Speed, Button B)> _speedButtons = new();
    private void SetSpeed(int s)
    {
        if (Speed > 0) _lastSpeed = Speed;
        Speed = s;
        foreach (var (v, b) in _speedButtons) b.SetPressedNoSignal(v == s);
    }

    private void ContextPicked(long id)
    {
        if (id < ReportTypes.Length) NewReport(ReportTypes[id].Key);
        else if (id == 100 && SelectedShip != null) { Trial.Naval!.MoveTo(SelectedShip.Id, _contextAt.Lon, _contextAt.Lat); UpdateNaval(); }
        else if (id >= 1000) { _contextActions[(int)id - 1000](); UpdateUi(); }
        else if (id == 999) { }
        else if (id >= 200) { Reports.RemoveAll(r => r.Id == id - 200); _windows["REPORTS"].Refresh?.Invoke(); }
    }

    /// <summary>A small form window: note text and who it is shared with, then "File report".</summary>
    private void NewReport(string type)
    {
        var (key, name, col) = ReportTypes.First(r => r.Key == type);
        var at = _contextAt;
        var w = new TacWindow($"New {name} · {Fmt(at.Lat, "N", "S")} {Fmt(at.Lon, "E", "W")}", GetViewport().GetMousePosition() + new Vector2(16, 8), new Vector2(360, 190));
        var kind = new OptionButton();
        foreach (var r in ReportTypes) kind.AddItem(r.Name);
        kind.Select(Array.FindIndex(ReportTypes, r => r.Key == key));
        w.Body.AddChild(kind);
        var text = new TextEdit { CustomMinimumSize = new Vector2(330, 60), PlaceholderText = "What, where, when, how sure…", WrapMode = TextEdit.LineWrappingMode.Boundary };
        w.Body.AddChild(text);
        var share = new OptionButton();
        foreach (var s in new[] { "Just me", "My commander", "Side picture" }) share.AddItem(s);
        share.Select(key is "RECON" or "THRT" or "LOG" ? 1 : 0);
        var row = new HBoxContainer();
        row.AddChild(UiTheme.Text("Share with", 12, UiTheme.Dim));
        row.AddChild(share);
        var file = new Button { Text = "FILE REPORT" };
        row.AddChild(file);
        w.Body.AddChild(row);
        AddChild(w);
        text.GrabFocus();
        file.Pressed += () =>
        {
            var t = ReportTypes[kind.Selected];
            var r = new MapReport(Reports.Count == 0 ? 1 : Reports.Max(x => x.Id) + 1, t.Key, CommandSide, at.Lon, at.Lat, Trial.Time, text.Text.Trim(), share.GetItemText(share.Selected));
            Reports.Add(r);
            Trial.Report("report", $"R-{r.Id:000} {t.Name} filed ({r.Share.ToLowerInvariant()}){(r.Text != "" ? ": " + Trunc(r.Text, 60) : "")}");
            w.QueueFree();
            if (_windows["REPORTS"].Visible) _windows["REPORTS"].Refresh?.Invoke();
        };
    }

    private void FillReports(TacWindow w)
    {
        var h = new HBoxContainer();
        var sit = new Button { Text = "GENERATE SITREP", TooltipText = "Summary of this theatre from your side's picture and your reports" };
        sit.Pressed += () => { _sitrep = Sitrep(); w.Refresh?.Invoke(); };
        h.AddChild(sit);
        h.AddChild(UiTheme.Text("Right-click the map (or press R) to file a report", 11, UiTheme.Dim));
        w.Body.AddChild(h);
        if (_sitrep != null) { w.Head("SITREP"); w.Line(_sitrep); }
        w.Head($"REPORTS · {Reports.Count(r => r.Side == CommandSide)}");
        foreach (var r in Reports.Where(r => r.Side == CommandSide).OrderByDescending(r => r.T))
        {
            var t = ReportTypes.First(x => x.Key == r.Type);
            var b = new Button { Text = $"R-{r.Id:000}  T+{T(r.T)}  {t.Name,-18} {Fmt(r.Lat, "N", "S")} {Fmt(r.Lon, "E", "W")}  {Trunc(r.Text, 40)}", Flat = true, Alignment = HorizontalAlignment.Left, TooltipText = $"{r.Text}\nShared: {r.Share}\nClick to centre the map" };
            b.AddThemeColorOverride("font_color", t.Color);
            b.AddThemeFontSizeOverride("font_size", 11);
            var rr = r;
            b.Pressed += () => GetViewport().GetCamera2D()?.SetPosition(Session.Project(rr.Lon, rr.Lat));
            w.Body.AddChild(b);
        }
    }

    /// <summary>A readable summary from what the viewing side knows (fog-safe: its own picture, its own log).</summary>
    private string Sitrep()
    {
        var side = CommandSide;
        var pic = Trial.Intel?.Pictures[side];
        var contacts = pic?.Contacts.Values.Where(c => Trial.Time - c.LastSeen < IntelRules.StaleAfter).GroupBy(c => c.Kind).ToDictionary(g => g.Key, g => g.Count()) ?? new();
        var ops = Trial.Ops?.Ops.Where(o => o.Side == side).ToList() ?? new();
        var since = Math.Max(0, Trial.Time - 3600);
        var mine = Reports.Where(r => r.Side == side && r.T >= since).GroupBy(r => r.Type).Select(g => $"{g.Count()} {ReportTypes.First(x => x.Key == g.Key).Name.ToLowerInvariant()}");
        return $"SITREP {SideName(side)} · T+{T(Trial.Time)}\n" +
               $"Operations: {ops.Count(o => o.Open)} open, {ops.Count(o => o.Status == OpStatus.Complete)} complete, {ops.Count(o => o.Status == OpStatus.Aborted)} aborted; aircraft lost {ops.Sum(o => o.AircraftLost)}.\n" +
               $"Missiles: {Trial.Tracks.Count(t => t.Side != side && t.State == TrackState.Intercepted)} enemy intercepted, {Trial.Hits.Where(x => Trial.Site(x.Key)?.Side == side).Sum(x => x.Value)} impacts on our sites.\n" +
               $"Picture: air {contacts.GetValueOrDefault("air")}, surface {contacts.GetValueOrDefault("ship")}, subs {contacts.GetValueOrDefault("sub")}, launchers {contacts.GetValueOrDefault("launcher")} (current contacts).\n" +
               $"Reports last hour: {(mine.Any() ? string.Join(", ", mine) : "none")}.";
    }

    private void FillAlerts(TacWindow w)
    {
        var shown = Trial.Log.Where(e => e.Kind is "impact" or "launch" or "refused" or "hit" or "miss" or "report" or "naval").TakeLast(12).Reverse().ToList();
        if (shown.Count == 0) { w.Line("Quiet.", UiTheme.TacDim); return; }
        foreach (var e in shown)
        {
            var c = e.Kind switch { "impact" or "refused" => UiTheme.Bad, "launch" => new Color("ff9a3c"), "hit" => UiTheme.Good, "report" => UiTheme.TacAccent, _ => UiTheme.TacInk };
            w.Line($"T+{T(e.T)}  {e.Text}", c);
        }
    }

    /// <summary>Report pins, drawn by the map layer.</summary>
    public void DrawReports(TheatreLayer layer, float zoom)
    {
        foreach (var r in Reports.Where(r => r.Side == CommandSide || ViewSide == "all"))
        {
            var c = ReportTypes.First(x => x.Key == r.Type).Color;
            var p = Session.Project(r.Lon, r.Lat);
            layer.DrawCircle(p, 8 / zoom, new Color(UiTheme.TacBg, 0.9f));
            layer.DrawArc(p, 8 / zoom, 0, Mathf.Tau, 20, c, 1.6f / zoom);
            layer.DrawCircle(p, 2.5f / zoom, c);
            layer.Labels.Add((p + new Vector2(0, 10 / zoom), $"R-{r.Id:000}", c));
        }
    }
}
