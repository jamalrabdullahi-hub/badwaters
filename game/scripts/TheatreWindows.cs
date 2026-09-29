using BadWaters.Sim.Command;
using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>A draggable instrument window for Theatre Ops (title strip, close box, scrolling body).</summary>
public partial class TacWindow : PanelContainer
{
    public VBoxContainer Body { get; } = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _title;
    private bool _drag;
    public Action? Refresh;
    public TacWindow(string title, Vector2 at, Vector2 size)
    {
        Position = at; CustomMinimumSize = size; Size = size;
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 2);
        AddChild(v);
        var bar = new HBoxContainer();
        var strip = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Stop };
        var sb = new StyleBoxFlat { BgColor = new Color(UiTheme.TacAccent, 0.18f) }; sb.ContentMarginLeft = 6; sb.ContentMarginTop = 2; sb.ContentMarginBottom = 2;
        strip.AddThemeStyleboxOverride("panel", sb);
        _title = new Label { Text = title.ToUpperInvariant() };
        _title.AddThemeColorOverride("font_color", UiTheme.TacAccent);
        strip.AddChild(_title);
        strip.GuiInput += e =>
        {
            if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left) { _drag = mb.Pressed; if (mb.Pressed) GetParent().MoveChild(this, -1); }
            else if (e is InputEventMouseMotion mm && _drag) Position += mm.Relative;
        };
        bar.AddChild(strip);
        var close = new Button { Text = "✕" }; close.Pressed += () => Visible = false;
        bar.AddChild(close);
        v.AddChild(bar);
        var sc = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, size.Y - 30) };
        sc.AddChild(Body);
        v.AddChild(sc);
    }
    public void Clear() { foreach (var c in Body.GetChildren()) { Body.RemoveChild(c); c.QueueFree(); } }
    public Label Line(string text, Color? color = null)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeColorOverride("font_color", color ?? UiTheme.TacInk);
        l.AddThemeFontSizeOverride("font_size", 11);
        Body.AddChild(l);
        return l;
    }
    public Label Head(string text) { var l = Line(text.ToUpperInvariant(), UiTheme.TacAccent); return l; }
}

/// <summary>Theatre Ops windows: FRAG list, Operation Editor, OOB, Priorities, Intel (with replay) and Commander.</summary>
public partial class TheatreOps
{
    private readonly Dictionary<string, TacWindow> _windows = new();
    private Operation? _editing;
    private string _edTab = "OVERVIEW";
    private bool _showClosed;
    private readonly List<(double T, List<(double Lon, double Lat, string Kind)> Pts)> _replay = new();
    private double _nextReplay;
    private int _replayIndex = -1;

    private OperationBoard Board => Trial.Ops!;
    private Commander? Cmdr => Trial.Commanders.GetValueOrDefault(CommandSide);

    private void BuildWindows()
    {
        void Win(string key, string title, Vector2 at, Vector2 size, Action<TacWindow> fill)
        {
            var w = new TacWindow(title, at, size) { Visible = false };
            w.Refresh = () => { w.Clear(); fill(w); };
            AddChild(w);
            _windows[key] = w;
        }
        Win("FRAG", "Frag order · operations", new Vector2(14, TopH + 12), new Vector2(620, 340), FillFrag);
        Win("OPERATION", "Operation editor", new Vector2(14, TopH + 364), new Vector2(620, 420), FillEditor);
        Win("OOB", "Order of battle", new Vector2(650, TopH + 12), new Vector2(420, 460), FillOob);
        Win("PRIORITIES", "Priorities · directives to the commander", new Vector2(650, TopH + 480), new Vector2(420, 320), FillPriorities);
        Win("INTEL", "Intelligence picture", new Vector2(40, TopH + 40), new Vector2(520, 460), FillIntel);
        Win("COMMANDER", "Commander · why is my general doing this", new Vector2(580, TopH + 40), new Vector2(480, 420), FillCommander);
        Win("REPORTS", "Reports · map notes and SITREP", new Vector2(80, TopH + 60), new Vector2(560, 420), FillReports);
        Win("ALERTS", "Alerts", new Vector2(0, TopH + 12), new Vector2(380, 230), FillAlerts);
        Win("UNIT", "Inspect · status, BDA, armament, fuel · orders", new Vector2(0, TopH + 260), new Vector2(470, 560), FillUnit);
        Win("LOGISTICS", "Logistics · supplies, routes, orders", new Vector2(360, TopH + 40), new Vector2(900, 640), FillLogistics);
        Win("MISSION", "Mission creation · air, ground, naval and support to one ToT", new Vector2(360, TopH + 12), new Vector2(940, 700), FillMission);
        var filter = new TacWindow("Map filter · sides, classes, overlays", new Vector2(14, TopH + 12), new Vector2(330, 720)) { Visible = false };
        AddChild(filter);
        _windows["FILTER"] = filter;
        FillFilter(filter);
        BuildDock();
        Open("ALERTS");
        _windows["ALERTS"].Position = new Vector2(GetViewportRect().Size.X - 394, TopH + 12);
        _windows["UNIT"].Position = new Vector2(GetViewportRect().Size.X - 484, TopH + 256);
        if (DevScreenshot.Arg("lgtab") is { } lt) _lgTab = lt.ToUpperInvariant();
        // dev aids for screenshots: --silent=id,id  --pick=kind:id  --planat=cap:lon,lat
        foreach (var id in (DevScreenshot.Arg("silent") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)) Trial.SetEmitting(id, false, id);
        if (DevScreenshot.Arg("pick")?.Split(':') is [var pk, var pid]) { Picked = (pk, pid); Open("UNIT"); }
        if (DevScreenshot.Arg("mission")?.Split(',') is [var ms, var mp]) { NewMission(ms, "strike"); _mPage = int.Parse(mp); _md.EditRow = 0; RefreshMission(); }
        if (DevScreenshot.Arg("planat")?.Split(':') is [var tpl, var ll] && ll.Split(',') is [var lo, var la]) PlanAt(null, tpl, (double.Parse(lo), double.Parse(la)));
        foreach (var k in (DevScreenshot.Arg("windows") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (_windows.TryGetValue(k.ToUpperInvariant(), out var w)) { w.Visible = true; w.Refresh?.Invoke(); }
    }

    public void RefreshWindows(bool force = false)
    {
        SyncDock();
        if (_editing == null && DevScreenshot.Arg("editop") is { } want)
            _editing = Board.Ops.FirstOrDefault(o => o.Side == CommandSide && o.Author == Author.AI && o.Purpose.Contains(want) && o.Rationale != "opening orders");
        // record the side's picture every 5 theatre-minutes for the replay
        if (Trial.Time >= _nextReplay && Trial.Intel != null)
        {
            _nextReplay = Trial.Time + 300;
            _replay.Add((Trial.Time, Trial.Intel.Pictures[CommandSide].Contacts.Values.Select(c => (c.Lon, c.Lat, c.Kind)).ToList()));
            if (_replay.Count > 600) _replay.RemoveAt(0);
        }
        foreach (var (k, w) in _windows.Where(x => x.Value.Visible && x.Key is not "MISSION" && (force || !x.Value.GetGlobalRect().HasPoint(GetGlobalMousePosition())))) w.Refresh?.Invoke();
    }

    private static string T(double s) => AirTasking.Clock(s);

    // ---------------- FRAG ----------------
    private void FillFrag(TacWindow w)
    {
        var h = new HBoxContainer();
        var add = new Button { Text = "+ NEW OPERATION" };
        add.Pressed += () => { _editing = NewDraft(); _edTab = "OVERVIEW"; Open("OPERATION"); };
        var closed = new CheckBox { Text = "show closed", ButtonPressed = _showClosed };
        closed.Toggled += on => { _showClosed = on; w.Refresh?.Invoke(); };
        var gen = new Button { Text = "GENERATE DAY'S FRAG", TooltipText = "Commit a full coordinated set of operations from the template library: AEW, CAPs, shipping protection, SEAD, airfield and naval strikes, depot and route interdiction, anti-ship, recon, missile barrage, tanker and jamming support." };
        gen.Pressed += () => { var m = GenerateDayFrag(); w.Refresh?.Invoke(); Trial.Report("ato", m); };
        h.AddChild(add); h.AddChild(gen); h.AddChild(closed);
        w.Body.AddChild(h);
        w.Line($"{"START",-8}{"BY",-4}{"",-2}{"PURPOSE",-18}{"OPERATION",-30}{"STATUS",-9}AC", UiTheme.TacDim);
        foreach (var op in Board.Ops.Where(o => o.Side == CommandSide && (_showClosed || o.Open)).OrderBy(o => o.Status is OpStatus.Complete or OpStatus.Aborted).ThenBy(o => o.Start).Take(120))
        {
            var ac = Board.Flights(op).Sum(f => f.Aircraft.Count + f.LostAircraft.Count);
            var b = new Button
            {
                Text = $"{T(op.Start),-8}{(op.Author == Author.AI ? "AI" : "PLR"),-4}{(op.Locked ? "L" : " "),-2}{Trunc(string.Join("/", op.Purpose), 17),-18}{Trunc(op.Name, 29),-30}{op.Status,-9}{ac}{(op.AircraftLost > 0 ? $"(-{op.AircraftLost})" : "")}",
                Alignment = HorizontalAlignment.Left, Flat = true,
            };
            b.AddThemeColorOverride("font_color", op.Status == OpStatus.Aborted ? UiTheme.Bad : op.Author == Author.Player ? UiTheme.TacAccent : op.Open ? UiTheme.TacInk : UiTheme.TacDim);
            b.AddThemeFontSizeOverride("font_size", 11);
            var o = op;
            b.Pressed += () => { _editing = o; _edTab = "OVERVIEW"; Open("OPERATION"); };
            w.Body.AddChild(b);
        }
    }

    private void Open(string key) { if (_windows.TryGetValue(key, out var w)) { w.Visible = true; MoveChild(w, -1); w.Refresh?.Invoke(); } }
    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    private Operation NewDraft() => new()
    {
        Id = Board.NewId(CommandSide), Name = "Operation " + (Board.Ops.Count(o => o.Author == Author.Player) + 1), Side = CommandSide, Author = Author.Player,
        Start = Trial.Time + 60, Status = OpStatus.Planned,
    };
    private bool IsDraft(Operation op) => !Board.Ops.Contains(op);

    // ---------------- OPERATION EDITOR ----------------
    private void FillEditor(TacWindow w)
    {
        var op = _editing;
        if (op == null) { w.Line("Pick an operation in the FRAG list, or create one.", UiTheme.TacDim); return; }
        var tabs = new HBoxContainer();
        foreach (var t in new[] { "OVERVIEW", "FORCES", "ELEMENTS", "DOCTRINE", "RULES", "DEPENDENCIES", "LOG" })
        {
            var b = new Button { Text = t, ToggleMode = true, ButtonPressed = t == _edTab };
            b.AddThemeFontSizeOverride("font_size", 10);
            var tt = t; b.Pressed += () => { _edTab = tt; w.Refresh?.Invoke(); };
            tabs.AddChild(b);
        }
        w.Body.AddChild(tabs);
        w.Line($"{op.Name} · {(IsDraft(op) ? "DRAFT" : op.Status.ToString().ToUpperInvariant())} · by {(op.Author == Author.AI ? "AI commander" : "you")}{(op.Locked ? " · LOCKED (edited, the AI leaves it alone)" : "")}", UiTheme.TacAccent);
        void Edited() { if (op.Author == Author.AI && !op.Locked) { op.Locked = true; op.Log.Add($"T+{T(Trial.Time)} edited by the player"); } }
        switch (_edTab)
        {
            case "OVERVIEW":
            {
                var name = new LineEdit { Text = op.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                name.TextSubmitted += t => { op.Name = t; Edited(); };
                w.Body.AddChild(name);
                var purposes = new HBoxContainer();
                foreach (var p in new[] { "counter-air", "strike", "SEAD", "recon", "fires", "sea-denial", "area-defence", "relocate" })
                {
                    var cb = new CheckBox { Text = p, ButtonPressed = op.Purpose.Contains(p) };
                    cb.AddThemeFontSizeOverride("font_size", 10);
                    var pp = p; cb.Toggled += on => { if (on) op.Purpose.Add(pp); else op.Purpose.Remove(pp); Edited(); };
                    purposes.AddChild(cb);
                }
                w.Body.AddChild(purposes);
                w.Line($"Start T+{T(op.Start)} · objectives: {(op.Targets.Count == 0 ? "—" : string.Join(", ", op.Targets.Select(t => Trial.Site(t)?.Name ?? t)))}");
                if (op.Rationale != "") w.Line("Why: " + op.Rationale, UiTheme.TacDim);
                if (op.Score != 0) w.Line($"Commander score {op.Score:0.#}", UiTheme.TacDim);
                w.Line($"Assets committed: {Board.Authority.AssetsOf(op.Id).Count()} · aircraft flown {op.AircraftCommitted} · lost {op.AircraftLost}");
                var row = new HBoxContainer();
                if (IsDraft(op))
                {
                    var go = new Button { Text = "EXECUTE" };
                    go.Pressed += () =>
                    {
                        var issues = Board.Add(op);
                        if (issues.Count > 0) op.Log.Add("Refused: " + string.Join("; ", issues));
                        w.Refresh?.Invoke(); _windows["FRAG"].Refresh?.Invoke();
                    };
                    row.AddChild(go);
                }
                else if (op.Open)
                {
                    if (op.Author == Author.AI)
                    {
                        var take = new Button { Text = op.Locked ? "LOCKED" : "TAKE COMMAND", Disabled = op.Locked, TooltipText = "Lock it: you own it now, the AI leaves it alone." };
                        take.Pressed += () => { Edited(); w.Refresh?.Invoke(); };
                        row.AddChild(take);
                    }
                    var rel = new Button { Text = "RELEASE / CANCEL", TooltipText = "Cancel what hasn't flown and hand every asset back to the AI pool." };
                    rel.Pressed += () => { Board.Release(op, "released by the player"); w.Refresh?.Invoke(); };
                    row.AddChild(rel);
                }
                w.Body.AddChild(row);
                break;
            }
            case "FORCES":
            {
                w.Head("Committed");
                foreach (var a in Board.Authority.AssetsOf(op.Id).Concat(op.Elements.SelectMany(e => e.Assets)).Distinct().OrderBy(x => x))
                    w.Line($"  {AssetName(a)}");
                if (!IsDraft(op) && op.Author == Author.AI && !op.Locked) { w.Line("Take command (OVERVIEW) to change its forces.", UiTheme.TacDim); break; }
                w.Head("Free pool · pick aircraft by base and type");
                foreach (var g in Trial.Air!.Idle(CommandSide).Where(u => Board.Authority.IsFreeFor(u.Id, op.Id)).GroupBy(u => (u.Base, u.Preset)).OrderBy(g => g.Key.Base))
                {
                    var h = new HBoxContainer();
                    var picked = op.Elements.SelectMany(e => e.Assets).Count(a => g.Any(u => u.Id == a));
                    var l = new Label { Text = $"{Trial.Site(g.Key.Base)?.Name ?? g.Key.Base,-26} {Session.Content.PresetInfo(g.Key.Preset)?.Name ?? g.Key.Preset,-24} {g.Count()} free" };
                    l.AddThemeFontSizeOverride("font_size", 11); l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    var n = new SpinBox { MinValue = 0, MaxValue = g.Count(), Value = picked, CustomMinimumSize = new Vector2(70, 0) };
                    var mission = new OptionButton();
                    foreach (var m in Enum.GetNames<Mission>()) mission.AddItem(m);
                    var add = new Button { Text = "ADD FLIGHT" };
                    var key = g.Key; var units = g.Select(u => u.Id).ToList();
                    add.Pressed += () =>
                    {
                        var count = (int)n.Value; if (count <= 0) return;
                        var ids = units.Where(u => !op.Elements.SelectMany(e => e.Assets).Contains(u)).Take(count).ToList();
                        var m = Enum.Parse<Mission>(mission.GetItemText(mission.Selected));
                        var tgt = op.Targets.FirstOrDefault();
                        var site = tgt != null ? Trial.Site(tgt) : null;
                        var home = Trial.Site(key.Base)!;
                        var spec = new AtoEntry
                        {
                            Side = CommandSide, Name = op.Name, Target = m is Mission.Strike or Mission.SEAD or Mission.Escort or Mission.Recon && site != null ? site.Id : "",
                            Tot = Trial.Time + 5400, StationLon = site?.Lon ?? home.Lon, StationLat = site?.Lat ?? home.Lat, Start = Trial.Time + 300, Duration = 5400,
                            Flights = { new FlightRequest(key.Preset, ids.Count, m) },
                        };
                        op.Elements.Add(new OpElement { Id = $"e{op.Elements.Count + 1}", Kind = m == Mission.Recon ? ElementKind.Sensor : ElementKind.Air, Role = m.ToString(), Assets = ids, Spec = spec });
                        if (!IsDraft(op)) { Board.Authority.Commit(op.Id, ids); if (op.Status == OpStatus.Active) Board.Launch(op, op.Elements[^1]); }
                        Edited(); w.Refresh?.Invoke();
                    };
                    h.AddChild(l); h.AddChild(n); h.AddChild(mission); h.AddChild(add);
                    w.Body.AddChild(h);
                }
                w.Head("Launchers");
                foreach (var ln in Trial.Launchers.Where(x => x.Def.Side == CommandSide && x.Stock > 0 && Board.Authority.IsFreeFor(x.Def.Id, op.Id)))
                {
                    var h = new HBoxContainer();
                    var l = new Label { Text = $"{ln.Def.Name} · {ln.Missile.Name} · {ln.Stock} rounds" }; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; l.AddThemeFontSizeOverride("font_size", 11);
                    var n = new SpinBox { MinValue = 1, MaxValue = ln.Stock, Value = Math.Min(2, ln.Stock) };
                    var add = new Button { Text = "ADD FIRES" };
                    var lid = ln.Def.Id;
                    add.Pressed += () =>
                    {
                        var tgt = op.Targets.FirstOrDefault(); if (tgt == null) { op.Log.Add("Set an objective (ELEMENTS → objective) before adding fires."); w.Refresh?.Invoke(); return; }
                        op.Elements.Add(new OpElement { Id = $"e{op.Elements.Count + 1}", Kind = ElementKind.Fires, Role = "fires", Assets = { lid },
                            Spec = new AtoEntry { Side = CommandSide, Launcher = lid, Target = tgt, Count = (int)n.Value, Tot = Trial.Time + 3600 } });
                        Edited(); w.Refresh?.Invoke();
                    };
                    h.AddChild(l); h.AddChild(n); h.AddChild(add);
                    w.Body.AddChild(h);
                }
                break;
            }
            case "ELEMENTS":
            {
                w.Head("Objective");
                var tp = new OptionButton { ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                var sites = Scenario.Sites.Where(s => s.Side != CommandSide && TargetPolicy.IsLegitimate(s.Class)).OrderBy(s => s.Name).ToList();
                tp.AddItem("— none (area / station) —");
                foreach (var s in sites) tp.AddItem($"{s.Name} ({s.Class})");
                tp.Select(op.Targets.Count > 0 ? Math.Max(0, sites.FindIndex(s => s.Id == op.Targets[0]) + 1) : 0);
                tp.ItemSelected += i => { op.Targets.Clear(); if (i > 0) op.Targets.Add(sites[(int)i - 1].Id); Edited(); };
                w.Body.AddChild(tp);
                w.Line("Population centres and civilian shelters are never selectable targets.", UiTheme.TacDim);
                w.Head("Elements");
                if (op.Elements.Count == 0) w.Line("None yet: add flights or fires in FORCES.", UiTheme.TacDim);
                foreach (var e in op.Elements)
                {
                    var sp = e.Spec;
                    var what = e.Kind switch
                    {
                        ElementKind.Fires => $"{sp?.Count}× from {AssetName(sp?.Launcher ?? "")} → {Trial.Site(sp?.Target ?? "")?.Name}, TOT T+{T(sp?.Tot ?? 0)}",
                        ElementKind.Relocate => $"{e.Assets.Count} aircraft → {Trial.Site(e.Base)?.Name}",
                        _ => sp == null ? "" : $"{string.Join(", ", sp.Flights.Select(f => $"{f.Count}× {Session.Content.PresetInfo(f.Preset)?.Name ?? f.Preset} {f.Mission}"))}{(sp.Target != "" ? $" → {Trial.Site(sp.Target)?.Name}, TOT T+{T(sp.Tot)}" : $" station ({sp.StationLon:0.0}, {sp.StationLat:0.0})")}",
                    };
                    w.Line($"[{e.Id}] {e.Role} · {e.Kind} · {(e.Held ? "HELD · " : "")}{e.State} · {what}", e.State == "failed" ? UiTheme.Bad : UiTheme.TacInk);
                    if (sp != null && e.State == "waiting" && e.Kind != ElementKind.Relocate)
                    {
                        var h = new HBoxContainer();
                        h.AddChild(new Label { Text = "   TOT in min" });
                        var tot = new SpinBox { MinValue = 10, MaxValue = 720, Step = 5, Value = Math.Max(10, (sp.Tot - Trial.Time) / 60) };
                        var ee = e;
                        tot.ValueChanged += v => { ee.Spec = ee.Spec! with { Tot = Trial.Time + v * 60, Target = op.Targets.FirstOrDefault() ?? ee.Spec.Target }; Edited(); };
                        var held = new CheckBox { Text = "held (a rule activates it)", ButtonPressed = e.Held };
                        held.Toggled += on => { ee.Held = on; Edited(); };
                        var rm = new Button { Text = "✕" };
                        rm.Pressed += () => { op.Elements.Remove(ee); Board.Authority.Release(ee.Assets); Edited(); w.Refresh?.Invoke(); };
                        h.AddChild(tot); h.AddChild(held); h.AddChild(rm);
                        w.Body.AddChild(h);
                    }
                }
                break;
            }
            case "DOCTRINE":
            {
                var d = op.Doctrine;
                void Choice(string label, string[] opts, string cur, Action<string> set)
                {
                    var h = new HBoxContainer(); h.AddChild(new Label { Text = $"{label,-12}", CustomMinimumSize = new Vector2(120, 0) });
                    var ob = new OptionButton(); foreach (var o in opts) ob.AddItem(o); ob.Select(Math.Max(0, Array.IndexOf(opts, cur)));
                    ob.ItemSelected += i => { set(opts[i]); Edited(); };
                    h.AddChild(ob); w.Body.AddChild(h);
                }
                Choice("ROE", new[] { "free", "tight", "hold" }, d.Roe, v => op.Doctrine = op.Doctrine with { Roe = v });
                Choice("EMCON", new[] { "active", "limited", "silent" }, d.Emcon, v => op.Doctrine = op.Doctrine with { Emcon = v });
                Choice("ECCM", new[] { "standard", "hardened", "aggressive" }, d.Eccm, v => op.Doctrine = op.Doctrine with { Eccm = v });
                Choice("Adaptation", new[] { "none", "rules", "full" }, d.Adaptation, v => op.Doctrine = op.Doctrine with { Adaptation = v });
                var rh = new HBoxContainer(); rh.AddChild(new Label { Text = "Risk tolerance", CustomMinimumSize = new Vector2(120, 0) });
                var risk = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = d.RiskTolerance, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                risk.ValueChanged += v => { op.Doctrine = op.Doctrine with { RiskTolerance = v }; Edited(); };
                rh.AddChild(risk); w.Body.AddChild(rh);
                var rtb = new CheckBox { Text = "RTB on completion", ButtonPressed = d.RtbOnCompletion };
                rtb.Toggled += on => { op.Doctrine = op.Doctrine with { RtbOnCompletion = on }; Edited(); };
                w.Body.AddChild(rtb);
                var fu = new CheckBox { Text = "Generate follow-up if BDA insufficient", ButtonPressed = op.FollowUp };
                fu.Toggled += on => { op.FollowUp = on; Edited(); };
                w.Body.AddChild(fu);
                break;
            }
            case "RULES":
            {
                w.Head("IF / THEN (checked against our own picture, never ground truth)");
                foreach (var (r, i) in op.Rules.Select((r, i) => (r, i)))
                {
                    var h = new HBoxContainer();
                    var l = new Label { Text = $"{(op.FiredRules.Contains(i) ? "✓ " : "  ")}{r}" }; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; l.AddThemeFontSizeOverride("font_size", 11);
                    var rm = new Button { Text = "✕" }; var rr = r; rm.Pressed += () => { op.Rules.Remove(rr); Edited(); w.Refresh?.Invoke(); };
                    h.AddChild(l); h.AddChild(rm); w.Body.AddChild(h);
                }
                var conds = new[] { "enemy-aew-detected", "enemy-air-near", "target-confidence-below", "tanker-unavailable", "losses-above", "target-moved", "bda-insufficient", "time-after" };
                var acts = new[] { "activate", "delay", "hold-weapons", "contingency", "abort", "follow-up", "rtb" };
                var f = new HBoxContainer();
                var c = new OptionButton(); foreach (var x in conds) c.AddItem(x);
                var cv = new SpinBox { MinValue = 0, MaxValue = 10000, Step = 0.05, Value = 0.3, CustomMinimumSize = new Vector2(80, 0), TooltipText = "value: km, fraction, % or seconds" };
                var a = new OptionButton(); foreach (var x in acts) a.AddItem(x);
                var arg = new LineEdit { PlaceholderText = "element / min", CustomMinimumSize = new Vector2(90, 0) };
                var add = new Button { Text = "ADD RULE" };
                add.Pressed += () =>
                {
                    var act = acts[a.Selected];
                    var ov = act == "delay" && double.TryParse(arg.Text, out var mins) ? mins * 60 : 0;
                    op.Rules.Add(new OpRule(new OpCondition(conds[c.Selected], conds[c.Selected] == "enemy-air-near" ? "3" : "", cv.Value), new OpAction(act, act == "delay" ? "" : arg.Text, ov)));
                    Edited(); w.Refresh?.Invoke();
                };
                f.AddChild(new Label { Text = "IF" }); f.AddChild(c); f.AddChild(cv); f.AddChild(new Label { Text = "THEN" }); f.AddChild(a); f.AddChild(arg); f.AddChild(add);
                w.Body.AddChild(f);
                break;
            }
            case "DEPENDENCIES":
            {
                w.Head("Start only after these complete");
                foreach (var other in Board.Ops.Where(o => o.Side == CommandSide && o != op && (o.Open || op.After.Contains(o.Id))).Take(40))
                {
                    var cb = new CheckBox { Text = $"{other.Name} ({other.Status})", ButtonPressed = op.After.Contains(other.Id) };
                    cb.AddThemeFontSizeOverride("font_size", 11);
                    var oid = other.Id; cb.Toggled += on => { if (on) op.After.Add(oid); else op.After.Remove(oid); Edited(); };
                    w.Body.AddChild(cb);
                }
                break;
            }
            case "LOG":
                foreach (var l in op.Log.AsEnumerable().Reverse().Take(60)) w.Line(l, UiTheme.TacDim);
                break;
        }
    }

    private string AssetName(string id) =>
        Scenario.Units.FirstOrDefault(u => u.Id == id) is { } u ? $"{u.Name} ({Session.Content.PresetInfo(u.Preset)?.Name ?? u.Preset})"
        : Trial.Launchers.FirstOrDefault(l => l.Def.Id == id)?.Def.Name ?? Trial.Batteries.FirstOrDefault(b => b.Def.Id == id)?.Def.Name ?? id;

    // ---------------- OOB ----------------
    private void FillOob(TacWindow w)
    {
        foreach (var side in new[] { CommandSide, Enemy(CommandSide) })
        {
            w.Head($"{SideName(side)}{(side != CommandSide ? " · as our intelligence sees it" : "")}");
            foreach (var b in Trial.Bases.Values.Where(b => b.Site.Side == side).OrderBy(b => b.Site.Name))
            {
                var pic = Trial.Intel?.Pictures[CommandSide];
                var integ = side == CommandSide ? b.Components.Values.Average(c => c.Integrity)
                    : pic?.Bda.Where(k => k.Key.Site == b.Site.Id).Select(k => k.Value.Integrity).DefaultIfEmpty(100).Average() ?? 100;
                w.Line($"{b.Site.Name,-30} {integ,4:0}% operational{(side == CommandSide && !b.RunwayOpen ? " · RUNWAY CLOSED" : "")}", integ < 50 ? UiTheme.Bad : UiTheme.TacInk);
                if (side != CommandSide) continue;
                foreach (var g in Scenario.Units.Where(u => u.Side == side && u.Kind is "air" or "aew" && Trial.Air!.BaseOf(u) == b.Site.Id).GroupBy(u => u.Preset))
                {
                    var lost = g.Count(u => Trial.Air!.Destroyed.Contains(u.Id));
                    var tasked = g.Count(u => Trial.Air!.Tasked.Contains(u.Id));
                    var committed = g.Count(u => !Board.Authority.IsFree(u.Id));
                    w.Line($"    {g.Count() - lost,3} {Session.Content.PresetInfo(g.Key)?.Name ?? g.Key,-26} flying {tasked} · committed {committed}{(lost > 0 ? $" · lost {lost}" : "")}", UiTheme.TacDim);
                }
            }
            if (side == CommandSide)
            {
                foreach (var l in Trial.Launchers.Where(l => l.Def.Side == side)) w.Line($"  launcher {l.Def.Name}: {l.Stock} × {l.Missile.Name}", UiTheme.TacDim);
                foreach (var b in Trial.Batteries.Where(b => b.Def.Side == side)) w.Line($"  air defence {b.Def.Name}: {b.Stock} interceptors", UiTheme.TacDim);
                foreach (var s in Trial.Naval?.Ships.Where(s => s.Side == side) ?? Enumerable.Empty<ShipState>()) w.Line($"  {(s.Kind == "sub" ? "sub" : "ship")} {s.Name}: {(s.Sunk ? "SUNK" : $"{s.Hp / s.MaxHp:P0}")}", s.Sunk ? UiTheme.Bad : UiTheme.TacDim);
            }
        }
    }

    // ---------------- PRIORITIES ----------------
    private void FillPriorities(TacWindow w)
    {
        var c = Cmdr;
        if (c == null) return;
        w.Head("Weights (1 = doctrine)");
        foreach (var k in new[] { "defence", "strike", "fires", "recon", "preserve" })
        {
            var h = new HBoxContainer();
            h.AddChild(new Label { Text = $"{k,-10}", CustomMinimumSize = new Vector2(90, 0) });
            var val = new Label { Text = $"{c.Directives.W(k):0.0}", CustomMinimumSize = new Vector2(34, 0) };
            var sl = new HSlider { MinValue = 0, MaxValue = 3, Step = 0.1, Value = c.Directives.W(k), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var kk = k; sl.ValueChanged += v => { c.Directives.Weights[kk] = v; val.Text = $"{v:0.0}"; };
            h.AddChild(sl); h.AddChild(val); w.Body.AddChild(h);
        }
        w.Head("Defend (our bases)");
        foreach (var b in Trial.Bases.Values.Where(b => b.Site.Side == CommandSide).OrderBy(b => b.Site.Name))
        {
            var on = c.Directives.DefendAreas.Any(a => Math.Abs(a.Lon - b.Site.Lon) < 1e-6 && Math.Abs(a.Lat - b.Site.Lat) < 1e-6);
            var cb = new CheckBox { Text = b.Site.Name, ButtonPressed = on };
            var s = b.Site;
            cb.Toggled += v => { c.Directives.DefendAreas.RemoveAll(a => Math.Abs(a.Lon - s.Lon) < 1e-6 && Math.Abs(a.Lat - s.Lat) < 1e-6); if (v) c.Directives.DefendAreas.Add((s.Lon, s.Lat, 2)); };
            w.Body.AddChild(cb);
        }
        w.Head("Enemy targets: priority / do not strike");
        foreach (var s in Scenario.Sites.Where(s => s.Side != CommandSide && TargetPolicy.IsLegitimate(s.Class)).OrderBy(s => s.Name))
        {
            var h = new HBoxContainer();
            var l = new Label { Text = s.Name }; l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var pr = new CheckBox { Text = "priority", ButtonPressed = c.Directives.PriorityTargets.Contains(s.Id) };
            var ns = new CheckBox { Text = "no strike", ButtonPressed = c.Directives.NoStrike.Contains(s.Id) };
            var id = s.Id;
            pr.Toggled += v => { if (v) c.Directives.PriorityTargets.Add(id); else c.Directives.PriorityTargets.Remove(id); };
            ns.Toggled += v => { if (v) c.Directives.NoStrike.Add(id); else c.Directives.NoStrike.Remove(id); };
            h.AddChild(l); h.AddChild(pr); h.AddChild(ns); w.Body.AddChild(h);
        }
    }

    // ---------------- INTEL ----------------
    private void FillIntel(TacWindow w)
    {
        var pic = Trial.Intel?.Pictures[CommandSide];
        if (pic == null) return;
        w.Head($"Replay · {_replay.Count} snapshots (every 5 min)");
        var h = new HBoxContainer();
        var sl = new HSlider { MinValue = -1, MaxValue = Math.Max(0, _replay.Count - 1), Step = 1, Value = _replayIndex, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var lab = new Label { Text = _replayIndex < 0 ? "live" : $"T+{T(_replay[Math.Min(_replayIndex, _replay.Count - 1)].T)}", CustomMinimumSize = new Vector2(70, 0) };
        sl.ValueChanged += v => { _replayIndex = (int)v; lab.Text = _replayIndex < 0 ? "live" : $"T+{T(_replay[_replayIndex].T)}"; Layer.Replay = _replayIndex < 0 ? null : _replay[_replayIndex].Pts; Layer.QueueRedraw(); };
        h.AddChild(sl); h.AddChild(lab); w.Body.AddChild(h);
        w.Head($"Contacts held · {pic.Contacts.Count}");
        w.Line($"{"KIND",-9}{"ID",-7}{"LABEL",-28}{"AGE",-8}SOURCE", UiTheme.TacDim);
        foreach (var c in pic.Contacts.Values.OrderBy(c => Trial.Time - c.LastSeen).Take(60))
        {
            var age = Trial.Time - c.LastSeen;
            w.Line($"{c.Kind,-9}{new[] { "unk", "class", "type" }[Math.Clamp(c.IdLevel, 0, 2)],-7}{Trunc(c.Label + (c.Role != "" ? $" [{c.Role}]" : ""), 27),-28}{T(age),-8}{c.Source}",
                age > IntelRules.StaleAfter ? UiTheme.TacDim : UiTheme.TacInk);
        }
        w.Head("Damage beliefs (BDA)");
        foreach (var g in pic.Bda.GroupBy(k => k.Key.Site).OrderBy(g => g.Key))
        {
            var avg = g.Average(k => k.Value.Integrity); var conf = g.Min(k => k.Value.Confidence); var asOf = g.Min(k => k.Value.AsOf);
            w.Line($"{Trunc(Trial.Site(g.Key)?.Name ?? g.Key, 28),-29}{avg,4:0}%  conf {conf:P0}  as of T+{T(asOf)}", avg < 60 ? UiTheme.TacAccent : UiTheme.TacDim);
        }
    }

    // ---------------- COMMANDER ----------------
    private void FillCommander(TacWindow w)
    {
        var c = Cmdr;
        if (c == null) return;
        var s = c.Last;
        w.Head($"{SideName(CommandSide)} commander · doctrine {c.Doctrine.Id} · {(Trial.Control.GetValueOrDefault(CommandSide) == SideControl.AI ? "running (hands-off)" : "paused: you have direct command")}");
        if (s == null) { w.Line("No planning cycle yet.", UiTheme.TacDim); return; }
        w.Line($"Situation at T+{T(s.Time)}: pool {s.PoolAircraft} aircraft (fighters {s.Fighters}, strikers {s.Strikers}) · open operations {s.OpenOps}");
        w.Line($"Enemy air held {s.KnownEnemyAir}{(s.EnemyAewKnown ? " + AEW" : "")} · SAMs known {s.KnownSams} · ships {s.KnownEnemyShips} · missiles inbound {s.IncomingMissiles}");
        foreach (var n in s.Notes) w.Line("• " + n, UiTheme.TacAccent);
        w.Head("Threat by base");
        foreach (var (b, v) in s.ThreatByBase.Where(k => k.Value > 0).OrderByDescending(k => k.Value)) w.Line($"  {Trial.Site(b)?.Name,-30} {v:0}");
        w.Head("Objectives this cycle (scored)");
        foreach (var o in c.LastObjectives.Take(14)) w.Line($"  {o.Score,6:0.0}  {o.Family,-9} {o.Text}", Board.Ops.Any(op => op.Side == CommandSide && op.Open && op.Name.Contains(o.Text.Split(' ').Last())) ? UiTheme.TacInk : UiTheme.TacDim);
        w.Head("Latest decisions");
        foreach (var op in Board.Ops.Where(o => o.Side == CommandSide && o.Author == Author.AI).AsEnumerable().Reverse().Take(10))
            w.Line($"  T+{T(op.Start)} {op.Name}: {op.Rationale}", UiTheme.TacDim);
    }
}
