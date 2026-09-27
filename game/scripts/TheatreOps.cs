using BadWaters.Sim.Engineering;
using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops (real time, 1×–100×). Each side is AI (passive, runs its doctrine) or player-controlled.
/// Works for any scenario: many batteries, launchers and force units. Panels show which components set
/// each detection, reach and kill chance.
/// </summary>
public partial class TheatreOps : Control
{
    private static readonly int[] Speeds = { 0, 1, 2, 5, 10, 30, 50, 100 };
    public InterceptTrial Trial { get; private set; } = null!;
    public ScenarioDef Scenario { get; private set; } = null!;
    public int Speed { get; private set; } = 10;
    public int? SelectedTrack;
    public TheatreLayer Layer { get; } = new();
    /// <summary>The side you are giving orders for (the AI keeps running unless that side is set to "You").</summary>
    public string CommandSide = "blue";
    /// <summary>Whose picture the map shows: "blue", "red" (fog of war) or "all" (umpire view).</summary>
    public string ViewSide = "blue";
    private OptionButton _aim = null!;
    private readonly List<string> _aimIds = new();

    private string _missile = "", _interceptor = "", _radar = "";
    private Label _clock = null!, _fireResult = null!, _oob = null!, _detail = null!, _title = null!, _blueName = null!, _redName = null!;
    private VBoxContainer _tracks = null!, _log = null!, _terms = null!, _pickBox = null!;
    private OptionButton _launcher = null!, _target = null!;
    private readonly List<string> _launcherIds = new(), _targetIds = new();
    private SpinBox _count = null!;
    private Control _firePanel = null!;
    private Button _blueAi = null!, _bluePlayer = null!, _redAi = null!, _redPlayer = null!;
    private double _uiTimer;
    private OptionButton _cmd = null!, _view = null!;
    private int _logSeen = -1;

    /// <summary>The single-battery trial gets design pickers; big scenarios use their own order of battle.</summary>
    private bool IsTrial => Scenario.Batteries.Count == 1 && Scenario.Launchers.Count == 1;
    public string SideName(string side) => Scenario.Sides.GetValueOrDefault(side, side);
    private static string Enemy(string side) => side == "blue" ? "red" : "blue";

    public override void _Ready()
    {
        AnchorRight = 1; AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Ignore;
        Layer.Ops = this;

        // top bar: clock, speed, control modes
        var top = Panel(new Vector2(10, 10));
        var th = new HBoxContainer();
        th.AddThemeConstantOverride("separation", 8);
        top.AddChild(th);
        _title = UiTheme.Text("THEATRE OPS", 16, UiTheme.Accent);
        th.AddChild(_title);
        _clock = UiTheme.Text("", 16);
        _clock.CustomMinimumSize = new Vector2(80, 0);
        th.AddChild(_clock);
        var sg = new ButtonGroup();
        foreach (var s in Speeds)
        {
            var sb = new Button { Text = s == 0 ? "❚❚" : $"{s}×", ToggleMode = true, ButtonGroup = sg, ButtonPressed = s == Speed };
            sb.Pressed += () => Speed = s;
            th.AddChild(sb);
        }
        _blueName = UiTheme.Text("  Blue", 14, TheatreLayer.Blue);
        th.AddChild(_blueName);
        (_blueAi, _bluePlayer) = ModeButtons(th, "blue");
        _redName = UiTheme.Text("  Red", 14, TheatreLayer.Red);
        th.AddChild(_redName);
        (_redAi, _redPlayer) = ModeButtons(th, "red");
        var reset = new Button { Text = "Reset" };
        reset.Pressed += () => Reset();
        th.AddChild(reset);
        th.AddChild(UiTheme.Text("  Command", 13, UiTheme.Dim));
        _cmd = new OptionButton();
        _cmd.ItemSelected += i => { CommandSide = i == 0 ? "blue" : "red"; FillLaunchers(); FillPlanner(); UpdateUi(); };
        th.AddChild(_cmd);
        th.AddChild(UiTheme.Text("View", 13, UiTheme.Dim));
        _view = new OptionButton();
        _view.ItemSelected += i => { ViewSide = i switch { 0 => "blue", 1 => "red", _ => "all" }; UpdateUi(); };
        th.AddChild(_view);

        // left: order of battle (and design pickers for the trial)
        var left = Panel(new Vector2(10, 64), 350);
        var lv = new VBoxContainer();
        left.AddChild(lv);
        lv.AddChild(UiTheme.Text("ORDER OF BATTLE", 13, UiTheme.Accent));
        _pickBox = new VBoxContainer();
        lv.AddChild(_pickBox);
        Picker(_pickBox, "Red missile", d => d.Kind == "munition" && d.Ratings.GetValueOrDefault("reach") >= 7, () => _missile, v => _missile = v);
        Picker(_pickBox, "Blue interceptor", d => d.Kind == "munition" && d.Ratings.GetValueOrDefault("reach") < 7, () => _interceptor, v => _interceptor = v);
        Picker(_pickBox, "Blue radar", d => d.Kind is "ground" or "ship" && d.Ratings.GetValueOrDefault("detection") > 0, () => _radar, v => _radar = v);
        _oob = UiTheme.Text("", 12, UiTheme.Ink, wrap: true);
        _oob.CustomMinimumSize = new Vector2(330, 0);
        lv.AddChild(_oob);

        // fire orders for any side the player controls
        _firePanel = Panel(new Vector2(10, 440), 350);
        var rv = new VBoxContainer();
        _firePanel.AddChild(rv);
        rv.AddChild(UiTheme.Text("FIRE ORDER", 13, UiTheme.Accent));
        _launcher = new OptionButton { ClipText = true };
        _launcher.ItemSelected += _ => FillTargets();
        rv.AddChild(_launcher);
        var rh = new HBoxContainer();
        _target = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        _count = new SpinBox { MinValue = 1, MaxValue = 4, Value = 2 };
        var fire = new Button { Text = "Fire ▸" };
        fire.Pressed += () =>
        {
            if (_launcher.Selected < 0 || _target.Selected < 0) return;
            var r = Trial.Launch(_launcherIds[_launcher.Selected], _targetIds[_target.Selected], (int)_count.Value, _aim.Selected > 0 ? _aimIds[_aim.Selected] : "");
            _fireResult.Text = r.Message;
            _fireResult.AddThemeColorOverride("font_color", r.Ok ? UiTheme.Good : UiTheme.Bad);
            FillLaunchers();
        };
        _target.ItemSelected += _ => FillAim();
        rh.AddChild(_target); rh.AddChild(_count); rh.AddChild(fire);
        rv.AddChild(rh);
        _aim = new OptionButton { ClipText = true };
        rv.AddChild(_aim);
        _fireResult = UiTheme.Text("", 12, UiTheme.Dim, wrap: true);
        _fireResult.CustomMinimumSize = new Vector2(330, 0);
        rv.AddChild(_fireResult);

        // right: tracks, selected-track detail, kill-chance breakdown (scrolls, never covers the whole map)
        var right = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, AnchorBottom = 1, OffsetLeft = -520, OffsetRight = -10, OffsetTop = 64, OffsetBottom = -10, ClipContents = true, GrowHorizontal = GrowDirection.Begin };
        AddChild(right);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(scroll);
        var outer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(410, 0) };
        scroll.AddChild(outer);
        var tabs = new HBoxContainer();
        var tg = new ButtonGroup();
        var tMis = new Button { Text = "Missiles & AD", ToggleMode = true, ButtonGroup = tg, ButtonPressed = DevScreenshot.Arg("tab") is not ("air" or "bases"), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var tAir = new Button { Text = "Air tasking", ToggleMode = true, ButtonGroup = tg, ButtonPressed = DevScreenshot.Arg("tab") == "air", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var tBase = new Button { Text = "Bases & ISR", ToggleMode = true, ButtonGroup = tg, ButtonPressed = DevScreenshot.Arg("tab") == "bases", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tabs.AddChild(tMis); tabs.AddChild(tAir); tabs.AddChild(tBase);
        outer.AddChild(tabs);
        var rsv = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = tMis.ButtonPressed };
        outer.AddChild(rsv);
        _airView = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = tAir.ButtonPressed };
        outer.AddChild(_airView);
        _basesView = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = tBase.ButtonPressed };
        outer.AddChild(_basesView);
        tMis.Pressed += () => { rsv.Visible = true; _airView.Visible = false; _basesView.Visible = false; };
        tAir.Pressed += () => { rsv.Visible = false; _airView.Visible = true; _basesView.Visible = false; UpdateAir(true); };
        tBase.Pressed += () => { rsv.Visible = false; _airView.Visible = false; _basesView.Visible = true; UpdateBases(); };
        BuildAirView();
        rsv.AddChild(UiTheme.Text("TRACKS", 13, UiTheme.Accent));
        _tracks = new VBoxContainer();
        rsv.AddChild(_tracks);
        rsv.AddChild(UiTheme.Text("SELECTED TRACK · COMPONENTS IN EFFECT", 13, UiTheme.Accent));
        _detail = UiTheme.Text("Click a track.", 12, UiTheme.Dim, wrap: true);
        _detail.CustomMinimumSize = new Vector2(410, 0);
        rsv.AddChild(_detail);
        rsv.AddChild(UiTheme.Text("KILL CHANCE BREAKDOWN", 13, UiTheme.Accent));
        _terms = new VBoxContainer();
        rsv.AddChild(_terms);

        var labels = new MapLabels { Layer = Layer };
        AddChild(labels);
        MoveChild(labels, 0); // under every panel
        var bottom = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 370, OffsetRight = 940, OffsetTop = -178, OffsetBottom = -10 };
        _log = new VBoxContainer();
        bottom.AddChild(_log);
        AddChild(bottom);

        Load(DevScreenshot.Arg("scenario") ?? "intercept-trial");
        if (DevScreenshot.Arg("blue") == "player") SetMode("blue", SideControl.Player);
        if (DevScreenshot.Arg("red") == "player") SetMode("red", SideControl.Player);
    }

    /// <summary>Switch to another scenario (for example the one for a theatre that just went to war).</summary>
    public void Load(string scenarioId)
    {
        Scenario = Session.Content.Scenarios[scenarioId];
        _missile = DevScreenshot.Arg("missile") ?? Scenario.Launchers.FirstOrDefault()?.Missile ?? "";
        _interceptor = DevScreenshot.Arg("interceptor") ?? Scenario.Batteries.FirstOrDefault()?.Interceptor ?? "";
        _radar = Scenario.Batteries.FirstOrDefault()?.Radar ?? "";
        _title.Text = "THEATRE OPS";
        _blueName.Text = $"  {SideName("blue")}";
        _cmd.Clear(); _cmd.AddItem(SideName("blue")); _cmd.AddItem(SideName("red")); _cmd.Select(CommandSide == "blue" ? 0 : 1);
        _view.Clear(); _view.AddItem(SideName("blue")); _view.AddItem(SideName("red")); _view.AddItem("All (umpire)");
        _view.Select(ViewSide switch { "blue" => 0, "red" => 1, _ => 2 });
        _redName.Text = $"  {SideName("red")}";
        _pickBox.Visible = IsTrial;
        Trial = null!;
        Reset(int.TryParse(DevScreenshot.Arg("seed"), out var seed) ? seed : 1);
    }

    private PanelContainer Panel(Vector2 at, float width = 0)
    {
        var p = new PanelContainer { OffsetLeft = at.X, OffsetTop = at.Y };
        if (width > 0) p.CustomMinimumSize = new Vector2(width, 0);
        AddChild(p);
        return p;
    }

    private (Button, Button) ModeButtons(HBoxContainer h, string side)
    {
        var g = new ButtonGroup();
        var ai = new Button { Text = "AI", ToggleMode = true, ButtonGroup = g, ButtonPressed = true, TooltipText = "Passive AI runs this side's doctrine" };
        var pl = new Button { Text = "You", ToggleMode = true, ButtonGroup = g, TooltipText = "You give the orders" };
        ai.Pressed += () => SetMode(side, SideControl.AI);
        pl.Pressed += () => SetMode(side, SideControl.Player);
        h.AddChild(ai); h.AddChild(pl);
        return (ai, pl);
    }

    private void SetMode(string side, SideControl c)
    {
        Trial.Control[side] = c;
        FillPlanner();
        (side == "blue" ? _bluePlayer : _redPlayer).ButtonPressed = c == SideControl.Player;
        (side == "blue" ? _blueAi : _redAi).ButtonPressed = c == SideControl.AI;
        FillLaunchers();
    }

    private void FillLaunchers()
    {
        _launcher.Clear(); _launcherIds.Clear();
        foreach (var l in Trial.Launchers.Where(l => l.Def.Side == CommandSide))
        {
            _launcherIds.Add(l.Def.Id);
            _launcher.AddItem($"{(l.IsShip ? "⚓ " : "")}{l.Def.Name} · {l.Missile.Name} ×{l.Stock}");
        }
        _firePanel.Visible = _launcherIds.Count > 0;
        if (_launcherIds.Count > 0) { _launcher.Select(0); FillTargets(); }
    }

    private void FillTargets()
    {
        _target.Clear(); _targetIds.Clear();
        var l = Trial.Launchers.First(x => x.Def.Id == _launcherIds[_launcher.Selected]);
        foreach (var s in Scenario.Sites.Where(s => s.Side != l.Def.Side))
        {
            _targetIds.Add(s.Id);
            var d = Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, s.Lon, s.Lat);
            _target.AddItem(!TargetPolicy.IsLegitimate(s.Class) ? $"{s.Name} (protected)" : d > l.ReachKm ? $"{s.Name} (out of reach)" : $"{s.Name} · {d:0} km");
        }
        FillAim();
    }

    /// <summary>Aimpoints of the chosen target base, labelled with what your own picture believes.</summary>
    private void FillAim()
    {
        _aim.Clear(); _aimIds.Clear();
        _aim.AddItem("Aimpoint: doctrine (auto)"); _aimIds.Add("");
        if (_target.Selected < 0 || !Trial.Bases.TryGetValue(_targetIds[_target.Selected], out var b)) return;
        var pic = Trial.Intel?.Pictures[CommandSide];
        foreach (var c in b.Components.Values)
        {
            _aimIds.Add(c.Def.Id);
            var bel = pic?.Belief(b.Site.Id, c.Def.Id);
            _aim.AddItem($"{c.Def.Name} · hardness {c.Def.Hardness:0}" + (bel != null ? $" · est. {bel.Integrity:0}% ({bel.Source})" : ""));
        }
    }

    private sealed record Choice(string Id, string Name, string Kind, IReadOnlyDictionary<string, double> Ratings);
    private readonly Dictionary<OptionButton, List<string>> _pickIds = new();
    private readonly List<(OptionButton O, Func<Choice, bool> Filter, Func<string> Get)> _pickers = new();

    private void Picker(VBoxContainer parent, string label, Func<Choice, bool> filter, Func<string> get, Action<string> set)
    {
        var h = new HBoxContainer();
        var t = UiTheme.Text(label, 13, UiTheme.Dim); t.CustomMinimumSize = new Vector2(120, 0);
        var o = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        h.AddChild(t); h.AddChild(o);
        parent.AddChild(h);
        _pickIds[o] = new List<string>();
        o.ItemSelected += i => { set(_pickIds[o][(int)i]); Reset(); };
        _pickers.Add((o, filter, get));
    }

    /// <summary>Refill the design pickers (call after the Development tab saves a design).</summary>
    public void RefreshPickers()
    {
        var cat = Session.Content.Catalog;
        var ev = new Evaluator(cat);
        var all = Session.Content.Designs.Values.Where(d => cat.Frames.ContainsKey(d.Frame))
            .Select(d => new Choice(d.Id, d.Name, cat.Frames[d.Frame].Kind, ev.Evaluate(d).Ratings)).ToList();
        foreach (var (o, filter, get) in _pickers)
        {
            o.Clear();
            var ids = all.Where(filter).ToList();
            foreach (var c in ids) o.AddItem((c.Id.StartsWith("player-") ? "★ " : "") + c.Name);
            _pickIds[o] = ids.Select(c => c.Id).ToList();
            o.Select(Math.Max(0, ids.FindIndex(c => c.Id == get())));
        }
    }

    public void Reset(int seed = 1)
    {
        var sc = !IsTrial ? Scenario : Scenario with
        {
            Batteries = Scenario.Batteries.Select(b => b with { Interceptor = _interceptor, Radar = _radar }).ToList(),
            Launchers = Scenario.Launchers.Select(l => l with { Missile = _missile }).ToList(),
        };
        var keep = Trial?.Control.ToDictionary(k => k.Key, k => k.Value);
        Trial = new InterceptTrial(sc, Session.Profile, seed, Session.Content.BaseTypes);
        if (keep != null) foreach (var (k, v) in keep) Trial.Control[k] = v;
        Trial.Intel = new IntelService(Trial, Session.Content.PresetInfo, seed);
        Trial.Air = new AirTasking(Trial, Session.Content.PresetInfo, seed);
        SelectedFlight = null; Preview.Clear();
        SelectedTrack = null;
        _logSeen = -1;
        if (_pickers.Count > 0 && _pickers[0].O.ItemCount == 0) RefreshPickers();
        FillLaunchers();
        UpdateUi();
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        if (Speed > 0)
        {
            Trial.Advance(delta * Speed);
            Session.Scheduler.AdvanceTheatreSeconds(delta * Speed); // the wider world keeps running
        }
        Layer.QueueRedraw();
        _uiTimer += delta;
        if (_uiTimer > 0.25) { _uiTimer = 0; UpdateUi(); }
    }

    private string SideSummary(string side)
    {
        var bases = Scenario.Sites.Count(s => s.Side == side && TargetPolicy.IsLegitimate(s.Class));
        var bats = Trial.Batteries.Where(b => b.Def.Side == side).ToList();
        var lns = Trial.Launchers.Where(l => l.Def.Side == side).ToList();
        var units = Scenario.Units.Where(u => u.Side == side).GroupBy(u => u.Kind).ToDictionary(g => g.Key, g => g.Count());
        var taken = Trial.Hits.Where(h => Trial.Site(h.Key)?.Side == side).Sum(h => h.Value);
        var stopped = Trial.Tracks.Count(t => t.Side != side && t.State == TrackState.Intercepted);
        var total = Scenario.Units.Count(u => u.Side == side) + bats.Count + lns.Count;
        return $"{SideName(side)} · {total} units · {bases} bases\n" +
               $"  air defence: {bats.Count} batteries/ships · {bats.Sum(b => b.Stock)} interceptors left\n" +
               $"  strike: {lns.Count} launchers · {lns.Sum(l => l.Stock)} missiles left\n" +
               (units.Count > 0 ? $"  aircraft {units.GetValueOrDefault("air")} · AEW {units.GetValueOrDefault("aew")} · ships {units.GetValueOrDefault("ship")} · subs {units.GetValueOrDefault("sub")}\n" : "") +
               $"  intercepted {stopped} · impacts taken {taken}\n";
    }

    private string FoggedSummary(string enemy)
    {
        var pic = Trial.Intel?.Pictures[ViewSide];
        if (pic == null) return "";
        var c = pic.Contacts.Values.Where(x => Trial.Time - x.LastSeen < IntelRules.StaleAfter).GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.Count());
        return $"{SideName(enemy)} (your picture) · {Scenario.Sites.Count(s => s.Side == enemy && TargetPolicy.IsLegitimate(s.Class))} known bases\n" +
               $"  current contacts: air {c.GetValueOrDefault("air")} · surface {c.GetValueOrDefault("ship")} · subs {c.GetValueOrDefault("sub")} · launchers {c.GetValueOrDefault("launcher")}\n" +
               $"  next satellite pass T+{AirTasking.Clock(pic.NextSatellite)}\n";
    }

    public void UpdateUi()
    {
        var t = TimeSpan.FromSeconds(Trial.Time);
        _clock.Text = $"T+{(int)t.TotalMinutes:00}:{t.Seconds:00}";
        var text = ViewSide == "all" ? SideSummary("blue") + SideSummary("red") : SideSummary(ViewSide) + FoggedSummary(ViewSide == "blue" ? "red" : "blue");
        if (IsTrial)
        {
            var b = Trial.Batteries[0]; var l = Trial.Launchers[0];
            text += $"{b.Def.Name}: radar {b.RadarKm:0} km ← {b.Radar.Sources("detection")}\n" +
                    $"  reach {b.ReachKm:0} km ← {b.Interceptor.Sources("reach")}\n" +
                    $"{l.Def.Name}: reach {l.ReachKm:0} km ← {l.Missile.Sources("reach")}\n";
        }
        var hits = Trial.Hits.OrderByDescending(h => h.Value).Take(6).Select(h => $"{Trial.Site(h.Key)!.Name} ×{h.Value}");
        _oob.Text = text + $"Most hit: {(Trial.Hits.Count == 0 ? "none" : string.Join(", ", hits))}";

        if (_airView.Visible) UpdateAir(false);
        if (_basesView.Visible) UpdateBases();
        Clear(_tracks);
        var shown = Trial.Tracks.Where(x => x.Launched <= Trial.Time && (ViewSide == "all" || x.Side == ViewSide || x.Detected))
            .OrderByDescending(x => x.State == TrackState.Flying).ThenByDescending(x => x.EndedAt).ThenBy(x => x.Id).Take(14).ToList();
        foreach (var tr in shown)
        {
            var h = new HBoxContainer();
            var state = tr.State switch
            {
                TrackState.Flying => tr.Detected ? $"{tr.Phase(Trial.Time)} · {(tr.Launched + tr.FlightTime - Trial.Time):0} s" : "not detected",
                TrackState.Intercepted => "intercepted", _ => "impacted",
            };
            var sel = new Button { Text = $"T{tr.Id} {tr.Missile.Name} → {tr.Target.Name} · {state}", ToggleMode = true, ButtonPressed = SelectedTrack == tr.Id, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left, ClipText = true };
            sel.AddThemeColorOverride("font_color", tr.State == TrackState.Intercepted ? UiTheme.Good : tr.State == TrackState.Impacted ? UiTheme.Bad : tr.Side == "blue" ? TheatreLayer.Blue : new Color("ff8a8a"));
            var id = tr.Id;
            sel.Pressed += () => { SelectedTrack = id; UpdateUi(); };
            h.AddChild(sel);
            var defender = Enemy(tr.Side);
            if (defender == CommandSide && tr.State == TrackState.Flying)
            {
                var best = tr.Detected ? Trial.BestBattery(defender, tr) : null;
                var e = new Button { Text = "Engage ×2", Disabled = best == null, TooltipText = best == null ? "No battery has a firing solution yet" : $"Fire from {best.Def.Name}" };
                e.Pressed += () => { var r = Trial.Engage(best!.Def.Id, id, 2); SelectedTrack = id; if (!r.Ok) _detail.Text = r.Message; UpdateUi(); };
                h.AddChild(e);
            }
            _tracks.AddChild(h);
        }
        if (shown.Count == 0) _tracks.AddChild(UiTheme.Text("No missiles in flight.", 12, UiTheme.Dim));

        Clear(_terms);
        var trk = Trial.Tracks.FirstOrDefault(x => x.Id == SelectedTrack) ?? shown.FirstOrDefault();
        if (trk == null) return;
        var m = trk.Missile;
        var def = Enemy(trk.Side);
        var shot = Trial.Shots.LastOrDefault(s => s.Target == trk);
        var bat = shot != null ? Trial.Batteries.First(b => b.Def.Id == shot.BatteryId)
            : trk.State == TrackState.Flying ? Trial.BestBattery(def, trk) : null;
        var nearest = Trial.Batteries.Where(b => b.Def.Side == def)
            .OrderBy(b => Pacing.DistanceKm(b.Def.Lon, b.Def.Lat, trk.Target.Lon, trk.Target.Lat)).FirstOrDefault();
        _detail.Text =
            $"{m.Name} ({SideName(trk.Side)}) · track {trk.Id} → {trk.Target.Name} · {trk.State}\n" +
            $"Speed {m.R("speed"):0} ← {m.Sources("speed")}\n" +
            $"Agility {m.R("agility"):0} ← {m.Sources("agility")}\n" +
            $"Decoys (EW) {m.R("ew"):0} ← {m.Sources("ew")}\n" +
            $"Punch {m.R("punch"):0} ← {m.Sources("punch")}\n" +
            (trk.Detected ? $"Detected at T+{trk.DetectedAt:0} s" : "Not detected yet");
        IReadOnlyList<PkTerm> terms; double pk; string head;
        if (shot != null && bat != null) { terms = shot.Terms; pk = shot.Pk; head = $"{bat.Def.Name} · shot {shot.State}"; }
        else if (bat != null && Trial.PlanIntercept(bat, trk) is { } plan) { (terms, pk) = Trial.KillChance(bat, trk, Trial.Time + plan.T); head = $"Preview if {bat.Def.Name} engages now"; }
        else
        {
            terms = Array.Empty<PkTerm>(); pk = 0;
            var inReach = nearest != null && Pacing.DistanceKm(nearest.Def.Lon, nearest.Def.Lat, trk.Target.Lon, trk.Target.Lat) <= nearest.ReachKm;
            head = trk.State != TrackState.Flying ? "Track ended with no shot fired."
                : nearest == null ? $"{SideName(def)} has no air defence here."
                : !trk.Detected ? "No firing solution: not detected yet."
                : inReach ? "No firing solution yet: waiting for the track to enter reach."
                : $"No firing solution: {trk.Target.Name} is outside every {SideName(def)} battery's reach (nearest: {nearest.Def.Name}, {nearest.ReachKm:0} km from {nearest.Interceptor.Sources("reach")}).";
        }
        var hl = UiTheme.Text(head, 12, UiTheme.Dim, wrap: true); hl.CustomMinimumSize = new Vector2(410, 0);
        _terms.AddChild(hl);
        foreach (var x in terms)
        {
            var row = new HBoxContainer();
            var v = UiTheme.Text($"{(x.Delta >= 0 ? "+" : "")}{x.Delta * 100:0}%", 13, x.Delta >= 0 ? UiTheme.Good : UiTheme.Bad);
            v.CustomMinimumSize = new Vector2(52, 0);
            var d = UiTheme.Text($"{x.Label}\n  ← {x.Sources}", 12, UiTheme.Ink, wrap: true);
            d.CustomMinimumSize = new Vector2(350, 0);
            row.AddChild(v); row.AddChild(d);
            _terms.AddChild(row);
        }
        if (terms.Count > 0) _terms.AddChild(UiTheme.Text($"Kill chance per interceptor: {pk:P0}", 14, UiTheme.Accent));

        if (_logSeen != Trial.Log.Count)
        {
            _logSeen = Trial.Log.Count;
            Clear(_log);
            foreach (var e in Trial.Log.TakeLast(7))
            {
                var c = e.Kind switch { "hit" => UiTheme.Good, "impact" or "refused" => UiTheme.Bad, "miss" => new Color("ffb347"), "launch" => new Color("ff8a8a"), _ => UiTheme.Ink };
                var lbl = UiTheme.Text($"T+{e.T,5:0}  {e.Text}", 12, c);
                lbl.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                lbl.CustomMinimumSize = new Vector2(640, 0);
                _log.AddChild(lbl);
            }
        }
    }

    private static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }
}

/// <summary>Draws the theatre on the map: sites, units, rings, missile arcs, interceptors and bursts.</summary>
public partial class TheatreLayer : Node2D
{
    public TheatreOps Ops = null!;
    /// <summary>Label anchors in map space; drawn crisp in screen space by <see cref="MapLabels"/>.</summary>
    public readonly List<(Vector2 At, string Text, Color Color)> Labels = new();
    public static readonly Color Blue = new("7fe3ff"), Red = new("ff4d5a"), Green = new("5cf2b0");
    private static Color SideColor(string side) => side == "blue" ? Blue : Red;
    /// <summary>True when this side's things are enemy to the viewer and must come from the viewer's picture.</summary>
    private bool Fogged(string side) => Ops.ViewSide != "all" && side != Ops.ViewSide;
    private Picture? Pic => Ops.ViewSide == "all" ? null : Ops.Trial.Intel?.Pictures[Ops.ViewSide];
    /// <summary>The viewer's contact for a key, or null if the viewer doesn't hold it.</summary>
    private Contact? Seen(string key) => Pic != null && Pic.Contacts.TryGetValue(key, out var c) ? c : null;
    private float Fade(Contact c) => (float)Math.Clamp(1 - (Ops.Trial.Time - c.LastSeen) / IntelRules.DropAfter, 0.25, 1);

    private static Vector2 P(double lon, double lat) => Session.Project(lon, lat);
    private static float PxPerKm(double lat) => (float)(Session.PxPerDeg / (111.0 * Math.Cos(lat * Math.PI / 180)));

    /// <summary>A missile's drawn position: its ground point lifted by an arc (drawing only, not physics).</summary>
    private static Vector2 Arc(Track t, double time)
    {
        var (lon, lat) = t.Pos(time);
        var g = P(lon, lat);
        var len = P(t.FromLon, t.FromLat).DistanceTo(P(t.Target.Lon, t.Target.Lat));
        var lift = t.Missile.R("speed") >= 8 ? 0.22f : 0.04f; // cruise missiles fly low
        return g - new Vector2(0, (float)t.Altitude(time) * len * lift);
    }

    public override void _Draw()
    {
        var tr = Ops?.Trial;
        if (tr == null) return;
        var zoom = GetViewport().GetCamera2D()?.Zoom.X ?? 1;
        var w = 1.5f / zoom;
        var many = tr.Batteries.Count > 2;
        Labels.Clear();
        void Label(Vector2 p, string s, Color c) => Labels.Add((p, s, c));

        foreach (var b in tr.Batteries)
        {
            if (Fogged(b.Def.Side) && b.IsShip && Seen(b.Def.Id) == null) continue; // enemy ships only when detected
            var p = P(b.Def.Lon, b.Def.Lat);
            var k = PxPerKm(b.Def.Lat);
            var c = SideColor(b.Def.Side);
            DrawArc(p, (float)b.RadarKm * k, 0, Mathf.Tau, 96, new Color(c, many ? 0.10f : 0.35f), w);
            DrawCircle(p, (float)b.ReachKm * k, new Color(c, 0.06f));
            DrawArc(p, (float)b.ReachKm * k, 0, Mathf.Tau, 64, new Color(c, many ? 0.45f : 0.7f), w);
            var ship = Session.Content.Designs.TryGetValue(b.Def.Radar, out var rd) && Session.Content.Catalog.Frames.TryGetValue(rd.Frame, out var fr) && fr.Kind == "ship";
            if (ship) Ship(p, c, zoom, 1.3f);
            else DrawColoredPolygon(new[] { p + new Vector2(0, -9) / zoom, p + new Vector2(8, 6) / zoom, p + new Vector2(-8, 6) / zoom }, c);
            if (!many || zoom > 3.5f) Label(p, many ? $"{b.Def.Name} · {b.Stock}" : $"{b.Def.Name} · radar {b.RadarKm:0} km · reach {b.ReachKm:0} km", c);
        }
        foreach (var l in tr.Launchers)
        {
            if (Fogged(l.Def.Side) && Seen(l.Def.Id) == null) continue; // mobile launchers hide until seen
            var p = P(l.Def.Lon, l.Def.Lat);
            var c = SideColor(l.Def.Side);
            DrawColoredPolygon(new[] { p + new Vector2(0, -8) / zoom, p + new Vector2(8, 0) / zoom, p + new Vector2(0, 8) / zoom, p + new Vector2(-8, 0) / zoom }, c);
            DrawArc(p, (float)l.ReachKm * PxPerKm(l.Def.Lat), 0, Mathf.Tau, 96, new Color(c, many ? 0.12f : 0.25f), w);
            if (!many || zoom > 3.5f) Label(p, $"{l.Def.Name} · {l.Stock} left", c);
        }
        foreach (var u in tr.Scenario.Units.Where(u => u.Kind is not ("air" or "aew")))
        {
            var (lon, lat) = tr.UnitPos(u);
            var c = SideColor(u.Side);
            if (Fogged(u.Side))
            {
                var seen = Seen(u.Id);
                if (seen == null) continue;
                (lon, lat) = (seen.Lon, seen.Lat); // where we last saw it, not where it is
                c = new Color(c, Fade(seen));
                if (zoom > 2.5f || seen.IdLevel == 2) Label(P(lon, lat), seen.Label + (tr.Time - seen.LastSeen > IntelRules.StaleAfter ? $" · {(tr.Time - seen.LastSeen) / 60:0} min old" : ""), c);
            }
            var p = P(lon, lat);
            switch (u.Kind)
            {
                case "air":
                    DrawColoredPolygon(new[] { p + new Vector2(0, -6) / zoom, p + new Vector2(5, 5) / zoom, p + new Vector2(0, 2) / zoom, p + new Vector2(-5, 5) / zoom }, c);
                    break;
                case "aew":
                    DrawArc(p, 7 / zoom, 0, Mathf.Tau, 16, c, w * 1.4f);
                    DrawLine(p - new Vector2(7, 0) / zoom, p + new Vector2(7, 0) / zoom, c, w * 1.4f);
                    break;
                case "sub":
                    DrawArc(p, 6 / zoom, Mathf.Pi, Mathf.Tau, 12, c, w * 1.6f);
                    DrawLine(p - new Vector2(6, 0) / zoom, p + new Vector2(6, 0) / zoom, c, w * 1.6f);
                    break;
                default:
                    Ship(p, c, zoom, u.Preset.Contains("CV") || u.Preset.Contains("DDH") ? 1.6f : 1f);
                    break;
            }
            if (zoom > 3.5f && (u.Preset.Contains("CV") || u.Preset.Contains("DDH"))) Label(p, u.Name.Split(" #")[0], c);
        }
        foreach (var s in tr.Scenario.Sites)
        {
            var p = P(s.Lon, s.Lat);
            var prot = !TargetPolicy.IsLegitimate(s.Class);
            var c = prot ? Green : SideColor(s.Side);
            if (prot) { DrawArc(p, 8 / zoom, 0, Mathf.Tau, 24, c, w); DrawCircle(p, 2.5f / zoom, c); }
            else DrawRect(new Rect2(p - new Vector2(6, 6) / zoom, new Vector2(12, 12) / zoom), c, false, w * 1.4f);
            var status = "";
            if (tr.Bases.TryGetValue(s.Id, out var bs))
            {
                if (Fogged(s.Side) && Pic != null)
                {
                    var down = bs.Components.Keys.Select(k => (k, b: Pic.Belief(s.Id, k))).Where(x => x.b.Integrity < 60).Select(x => $"{bs.Components[x.k].Def.Name} ~{x.b.Integrity:0}%");
                    status = string.Join(", ", down);
                }
                else
                {
                    var ready = tr.Air?.Idle(s.Side).Count(u => u.Base == s.Id) ?? 0;
                    status = (ready > 0 ? $"{ready} ready" : "") + (bs.Type.Components.Any(x => x.Effect == "sorties") && !bs.RunwayOpen ? " · RUNWAY CLOSED" : "") + (bs.Type.Components.Any(x => x.Effect == "launch-pads") && !bs.PadsOk ? " · PADS DOWN" : "");
                }
            }
            if (!prot || !many || zoom > 3.5f) Label(p, prot ? $"{s.Name} · protected" : s.Name + (status.Trim(' ', '·') != "" ? $" · {status.Trim(' ', '·')}" : ""), c);
        }

        foreach (var t in tr.Tracks.Where(t => t.Launched <= tr.Time && (!Fogged(t.Side) || t.Detected)))
        {
            var end = t.State == TrackState.Flying ? tr.Time : t.EndedAt;
            var c = SideColor(t.Side);
            var a = P(t.FromLon, t.FromLat); var z = P(t.Target.Lon, t.Target.Lat);
            if (t.State == TrackState.Flying) DrawDashedLine(a, z, new Color(c, 0.2f), w, 8 / zoom);
            var pts = new List<Vector2>();
            for (var s = t.Launched; s <= end; s += Math.Max(0.5, t.FlightTime / 60)) pts.Add(Arc(t, s));
            pts.Add(Arc(t, end));
            var sel = Ops.SelectedTrack == t.Id;
            var col = t.State != TrackState.Flying ? new Color(c, 0.25f) : t.Detected ? c : new Color(c, 0.55f);
            if (pts.Count > 1) DrawPolyline(pts.ToArray(), col, sel ? w * 2.2f : w * 1.2f);
            var head = Arc(t, end);
            if (t.State == TrackState.Flying)
            {
                DrawCircle(head, 4 / zoom, c);
                if (sel) { DrawArc(head, 11 / zoom, 0, Mathf.Tau, 24, Colors.White, w); Label(head, $"T{t.Id}", c); }
            }
            Burst(t.State == TrackState.Intercepted ? head : z, t.EndedAt, t.State == TrackState.Intercepted ? Green : t.State == TrackState.Impacted ? Red : default, zoom);
        }

        DrawAir(tr, zoom, w);
        DrawAirContacts(zoom, w);
        foreach (var s in tr.Shots.Where(s => s.Launched <= tr.Time && s.State == ShotState.Flying))
        {
            var c = SideColor(Enemy(s.Target.Side));
            var from = P(s.FromLon, s.FromLat);
            var aim = Arc(s.Target, s.Launched + s.FlightTime);
            var f = (float)Math.Clamp((tr.Time - s.Launched) / s.FlightTime, 0, 1);
            var pos = from.Lerp(aim, f);
            DrawLine(from, pos, c, w * 1.2f);
            DrawCircle(pos, 3 / zoom, Colors.White);
        }
    }

    private static string Enemy(string side) => side == "blue" ? "red" : "blue";

    /// <summary>Flight routes (take-off → ingress → IP → target → egress → land), CAP orbits and formations.</summary>
    private void DrawAir(InterceptTrial tr, float zoom, float w)
    {
        if (tr.Air == null) return;
        var labelled = new HashSet<string>();
        foreach (var f in tr.Air.Flights.Concat(Ops.Preview))
        {
            var preview = Ops.Preview.Contains(f);
            if (!preview && f.State is FlightState.Landed || f.State == FlightState.Lost && !preview) continue;
            if (Fogged(f.Side)) continue; // enemy flights are drawn from contacts below
            var c = preview ? Colors.White : SideColor(f.Side);
            var sel = Ops.SelectedFlight == f || preview;
            var pts = f.Route.Select(x => P(x.Lon, x.Lat)).ToArray();
            var routeCol = new Color(c, sel ? 0.9f : 0.28f);
            for (var i = 0; i + 1 < pts.Length; i++)
            {
                if (pts[i] == pts[i + 1]) continue;
                if (preview || sel) DrawDashedLine(pts[i], pts[i + 1], routeCol, w * (sel ? 1.4f : 1f), 7 / zoom);
                else DrawLine(pts[i], pts[i + 1], routeCol, w);
            }
            foreach (var x in f.Route.Where(x => x.Name == "Station"))
                DrawArc(P(x.Lon, x.Lat), 25 * PxPerKm(x.Lat), 0, Mathf.Tau, 32, routeCol, w);
            foreach (var x in f.Route)
            {
                var p = P(x.Lon, x.Lat);
                if (x.Name is "IP")
                {
                    DrawRect(new Rect2(p - new Vector2(4, 4) / zoom, new Vector2(8, 8) / zoom), c, false, w);
                    var pk = tr.Air.Packages.FirstOrDefault(k => k.Id == f.PackageId);
                    if (labelled.Add("ip" + f.PackageId)) Tag(p, $"IP · {pk?.Name ?? f.Callsign}", c);
                }
                if (x.Name is "Target" && labelled.Add("tot" + f.PackageId)) Tag(p + new Vector2(0, 16 / zoom), $"TOT T+{AirTasking.Clock(x.T)}", c);
                if (sel && x.Name is not ("IP" or "Target")) { DrawCircle(p, 2.5f / zoom, c); Tag(p, $"{x.Name} T+{AirTasking.Clock(x.T)}", c); }
            }
            if (!f.Airborne(tr.Time) && !preview) continue;
            if (preview) continue;
            var (lon, lat) = f.Pos(tr.Time);
            var at = P(lon, lat);
            var nx = f.Next(tr.Time);
            var (hl, ht) = nx != null && (nx.Name != "Off station") ? (nx.Lon, nx.Lat) : f.Pos(tr.Time + 5);
            var head = (P(hl, ht) - at).Normalized();
            if (head == Vector2.Zero) head = Vector2.Up;
            var right = new Vector2(-head.Y, head.X);
            var slots = FormationRules.Slots(f.Formation);
            var sp = 7 / zoom;
            for (var i = 0; i < f.Aircraft.Count && i < slots.Length; i++)
            {
                var q = at + right * slots[i].X * sp - head * slots[i].Y * sp;
                Chevron(q, head, right, c, zoom);
            }
            if (sel) DrawArc(at, 14 / zoom, 0, Mathf.Tau, 24, Colors.White, w);
            if (sel || f.Mission is Mission.Strike or Mission.Escort or Mission.SEAD || zoom > 3f) Tag(at, f.Callsign, c);
        }
    }

    private void Tag(Vector2 p, string text, Color c) => Labels.Add((p, text, c));

    /// <summary>Enemy air as the viewer's sensors hold it: a symbol at the last-seen spot, labelled by ID level.</summary>
    private void DrawAirContacts(float zoom, float w)
    {
        if (Pic == null) return;
        foreach (var c in Pic.Contacts.Values.Where(c => c.Kind == "air"))
        {
            var col = new Color(SideColor(Ops.ViewSide == "blue" ? "red" : "blue"), Fade(c));
            var p = P(c.Lon, c.Lat);
            Chevron(p, Vector2.Up, Vector2.Right, col, zoom);
            DrawArc(p, 9 / zoom, 0, Mathf.Tau, 16, col, w);
            var age = Ops.Trial.Time - c.LastSeen;
            Tag(p, c.Label + (c.IdLevel < 2 ? " ?" : "") + (age > 60 ? $" · {age / 60:0} min" : ""), col);
        }
    }

    private void Chevron(Vector2 p, Vector2 head, Vector2 right, Color c, float zoom)
    {
        var s = 1 / zoom;
        DrawColoredPolygon(new[] { p + head * 6 * s, p - head * 5 * s + right * 5 * s, p - head * 2 * s, p - head * 5 * s - right * 5 * s }, c);
    }

    private void Ship(Vector2 p, Color c, float zoom, float scale)
    {
        var s = scale / zoom;
        DrawColoredPolygon(new[] { p + new Vector2(-9, -3) * s, p + new Vector2(6, -3) * s, p + new Vector2(10, 0) * s, p + new Vector2(6, 3) * s, p + new Vector2(-9, 3) * s }, c);
    }

    private void Burst(Vector2 p, double endedAt, Color c, float zoom)
    {
        if (c == default) return;
        var age = Ops.Trial.Time - endedAt;
        var window = Math.Max(3, Ops.Speed * 1.2);
        if (age < 0 || age > window) return;
        var k = (float)(age / window);
        DrawArc(p, (8 + 30 * k) / zoom, 0, Mathf.Tau, 32, new Color(c, 1 - k), 3 / zoom);
        DrawCircle(p, (6 * (1 - k) + 1) / zoom, new Color(c, 1 - k));
    }
}

/// <summary>Draws the theatre layer's labels in screen space so text stays sharp at any zoom.</summary>
public partial class MapLabels : Control
{
    public TheatreLayer Layer = null!;
    public override void _Ready() { AnchorRight = 1; AnchorBottom = 1; MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (!Layer.Visible) return;
        var toScreen = GetViewport().GetCanvasTransform();
        var toLocal = GetGlobalTransformWithCanvas().AffineInverse();
        var font = ThemeDB.FallbackFont;
        foreach (var (at, text, color) in Layer.Labels)
        {
            var p = toLocal * (toScreen * at) + new Vector2(10, -6);
            DrawString(font, p + new Vector2(1, 1), text, HorizontalAlignment.Left, -1, 12, new Color(0, 0, 0, 0.85f));
            DrawString(font, p, text, HorizontalAlignment.Left, -1, 12, color);
        }
    }
}
