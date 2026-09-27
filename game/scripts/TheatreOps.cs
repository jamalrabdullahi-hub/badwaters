using BadWaters.Sim.Engineering;
using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops (real time, 1×–100×): the intercept trial. Each side is AI (passive, runs its doctrine) or
/// player-controlled. The designs used come from the Development tab, and the panels show which
/// components set each detection, reach and kill chance.
/// </summary>
public partial class TheatreOps : Control
{
    private static readonly int[] Speeds = { 0, 1, 2, 5, 10, 30, 50, 100 };
    public InterceptTrial Trial { get; private set; } = null!;
    public int Speed { get; private set; } = 10;
    public int? SelectedTrack;
    public TheatreLayer Layer { get; } = new();

    private string _missile = "", _interceptor = "", _radar = "";
    private Label _clock = null!, _fireResult = null!, _oob = null!, _detail = null!;
    private VBoxContainer _tracks = null!, _log = null!, _terms = null!;
    private OptionButton _missilePick = null!, _intPick = null!, _radarPick = null!, _target = null!;
    private SpinBox _count = null!;
    private Control _redPanel = null!;
    private Button _blueAi = null!, _bluePlayer = null!, _redAi = null!, _redPlayer = null!;
    private double _uiTimer;
    private int _logSeen = -1;

    private ScenarioDef Scenario => Session.Content.Scenarios["intercept-trial"];

    public override void _Ready()
    {
        AnchorRight = 1; AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Ignore;
        var b = Scenario.Batteries[0]; var l = Scenario.Launchers[0];
        _missile = DevScreenshot.Arg("missile") ?? l.Missile;
        _interceptor = DevScreenshot.Arg("interceptor") ?? b.Interceptor;
        _radar = b.Radar;
        Layer.Ops = this;

        // top bar: clock, speed, control modes
        var top = Panel(new Vector2(10, 10));
        var th = new HBoxContainer();
        th.AddThemeConstantOverride("separation", 8);
        top.AddChild(th);
        th.AddChild(UiTheme.Text("THEATRE OPS", 16, UiTheme.Accent));
        _clock = UiTheme.Text("", 16);
        _clock.CustomMinimumSize = new Vector2(90, 0);
        th.AddChild(_clock);
        var sg = new ButtonGroup();
        foreach (var s in Speeds)
        {
            var sb = new Button { Text = s == 0 ? "❚❚" : $"{s}×", ToggleMode = true, ButtonGroup = sg, ButtonPressed = s == Speed };
            sb.Pressed += () => Speed = s;
            th.AddChild(sb);
        }
        th.AddChild(UiTheme.Text("  Blue", 14, new Color("7fe3ff")));
        (_blueAi, _bluePlayer) = ModeButtons(th, "blue");
        th.AddChild(UiTheme.Text("  Red", 14, UiTheme.Bad));
        (_redAi, _redPlayer) = ModeButtons(th, "red");
        var reset = new Button { Text = "Reset" };
        reset.Pressed += () => Reset();
        th.AddChild(reset);

        // left: order of battle and the designs in use
        var left = Panel(new Vector2(10, 64), 340);
        var lv = new VBoxContainer();
        left.AddChild(lv);
        lv.AddChild(UiTheme.Text("ORDER OF BATTLE", 13, UiTheme.Accent));
        _missilePick = Picker(lv, "Red missile", d => d.Kind == "munition" && d.Ratings.GetValueOrDefault("reach") >= 7, () => _missile, v => _missile = v);
        _intPick = Picker(lv, "Blue interceptor", d => d.Kind == "munition" && d.Ratings.GetValueOrDefault("reach") < 7, () => _interceptor, v => _interceptor = v);
        _radarPick = Picker(lv, "Blue radar", d => d.Kind == "ground" && d.Ratings.GetValueOrDefault("detection") > 0, () => _radar, v => _radar = v);
        _oob = UiTheme.Text("", 12, UiTheme.Ink, wrap: true);
        _oob.CustomMinimumSize = new Vector2(320, 0);
        lv.AddChild(_oob);

        // red player: fire orders
        _redPanel = Panel(new Vector2(10, 470), 340);
        var rv = new VBoxContainer();
        _redPanel.AddChild(rv);
        rv.AddChild(UiTheme.Text("RED · FIRE ORDER", 13, UiTheme.Bad));
        var rh = new HBoxContainer();
        _target = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var s in Scenario.Sites.Where(s => s.Side == "blue"))
            _target.AddItem(TargetPolicy.IsLegitimate(s.Class) ? s.Name : $"{s.Name} (protected)");
        _count = new SpinBox { MinValue = 1, MaxValue = 4, Value = 1 };
        var fire = new Button { Text = "Fire ▸" };
        fire.Pressed += () =>
        {
            var site = Scenario.Sites.Where(s => s.Side == "blue").ElementAt(_target.Selected);
            var r = Trial.Launch(Scenario.Launchers[0].Id, site.Id, (int)_count.Value);
            _fireResult.Text = r.Message;
            _fireResult.AddThemeColorOverride("font_color", r.Ok ? UiTheme.Good : UiTheme.Bad);
        };
        rh.AddChild(_target); rh.AddChild(_count); rh.AddChild(fire);
        rv.AddChild(rh);
        _fireResult = UiTheme.Text("", 12, UiTheme.Dim, wrap: true);
        _fireResult.CustomMinimumSize = new Vector2(320, 0);
        rv.AddChild(_fireResult);

        // right: tracks, selected-track detail, kill-chance breakdown
        var right = Panel(new Vector2(-440, 64), 430, anchorRight: true);
        var rsv = new VBoxContainer();
        right.AddChild(rsv);
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

        // bottom: event log
        AddChild(new MapLabels { Layer = Layer });
        var bottom = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 10, OffsetRight = 700, OffsetTop = -178, OffsetBottom = -10 };
        _log = new VBoxContainer();
        bottom.AddChild(_log);
        AddChild(bottom);

        Reset(int.TryParse(DevScreenshot.Arg("seed"), out var seed) ? seed : 1);
        if (DevScreenshot.Arg("blue") == "player") SetMode("blue", SideControl.Player);
        if (DevScreenshot.Arg("red") == "player") SetMode("red", SideControl.Player);
    }

    private PanelContainer Panel(Vector2 at, float width = 0, bool anchorRight = false)
    {
        var p = new PanelContainer { OffsetLeft = at.X, OffsetTop = at.Y };
        if (anchorRight) { p.AnchorLeft = 1; p.AnchorRight = 1; p.OffsetRight = -10; }
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
        (side == "blue" ? _bluePlayer : _redPlayer).ButtonPressed = c == SideControl.Player;
        (side == "blue" ? _blueAi : _redAi).ButtonPressed = c == SideControl.AI;
        _redPanel.Visible = Trial.Control["red"] == SideControl.Player;
    }

    private sealed record Choice(string Id, string Name, string Kind, IReadOnlyDictionary<string, double> Ratings);

    private OptionButton Picker(VBoxContainer parent, string label, Func<Choice, bool> filter, Func<string> get, Action<string> set)
    {
        var h = new HBoxContainer();
        var t = UiTheme.Text(label, 13, UiTheme.Dim); t.CustomMinimumSize = new Vector2(120, 0);
        var o = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        h.AddChild(t); h.AddChild(o);
        parent.AddChild(h);
        _pickIds[o] = new List<string>();
        o.ItemSelected += i => { set(_pickIds[o][(int)i]); Reset(); };
        _pickers.Add((o, filter, get, set));
        return o;
    }

    private readonly Dictionary<OptionButton, List<string>> _pickIds = new();
    private readonly List<(OptionButton O, Func<Choice, bool> Filter, Func<string> Get, Action<string> Set)> _pickers = new();

    /// <summary>Refill the design pickers (call after the Development tab saves a design).</summary>
    public void RefreshPickers()
    {
        var cat = Session.Content.Catalog;
        var ev = new Evaluator(cat);
        var all = Session.Content.Designs.Values.Where(d => cat.Frames.ContainsKey(d.Frame))
            .Select(d => new Choice(d.Id, d.Name, cat.Frames[d.Frame].Kind, ev.Evaluate(d).Ratings)).ToList();
        foreach (var (o, filter, get, _) in _pickers)
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
        var sc = Scenario with
        {
            Batteries = Scenario.Batteries.Select(b => b with { Interceptor = _interceptor, Radar = _radar }).ToList(),
            Launchers = Scenario.Launchers.Select(l => l with { Missile = _missile }).ToList(),
        };
        var keep = Trial?.Control.ToDictionary(k => k.Key, k => k.Value);
        Trial = new InterceptTrial(sc, Session.Profile, seed);
        if (keep != null) foreach (var (k, v) in keep) Trial.Control[k] = v;
        SelectedTrack = null;
        _logSeen = -1;
        if (_pickers.Count > 0 && _pickers[0].O.ItemCount == 0) RefreshPickers();
        _redPanel.Visible = Trial.Control["red"] == SideControl.Player;
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

    public void UpdateUi()
    {
        var t = TimeSpan.FromSeconds(Trial.Time);
        _clock.Text = $"T+{(int)t.TotalMinutes:00}:{t.Seconds:00}";
        var b = Trial.Batteries[0]; var l = Trial.Launchers[0];
        _oob.Text =
            $"{b.Def.Name} · {b.Stock} interceptors · {b.Channels} fire channels\n" +
            $"  radar {b.RadarKm:0} km ← detection {b.Radar.R("detection"):0} from {b.Radar.Sources("detection")}\n" +
            $"  reach {b.ReachKm:0} km ← reach {b.Interceptor.R("reach"):0} from {b.Interceptor.Sources("reach")}\n" +
            $"  channels ← {b.Radar.Sources("fire-channels")}\n" +
            $"{l.Def.Name} · {l.Stock} missiles\n" +
            $"  reach {l.ReachKm:0} km ← {l.Missile.Sources("reach")}\n" +
            $"  speed {l.Missile.R("speed"):0} ← {l.Missile.Sources("speed")}\n" +
            $"Impacts: {(Trial.Hits.Count == 0 ? "none" : string.Join(", ", Trial.Hits.Select(h => $"{Trial.Site(h.Key)!.Name} ×{h.Value}")))}";

        Clear(_tracks);
        var bluePlayer = Trial.Control["blue"] == SideControl.Player;
        foreach (var tr in Trial.Tracks.Where(x => x.Launched <= Trial.Time).OrderByDescending(x => x.State == TrackState.Flying).ThenBy(x => x.Id))
        {
            var h = new HBoxContainer();
            var state = tr.State switch
            {
                TrackState.Flying => tr.Detected ? $"{tr.Phase(Trial.Time)} · {(tr.Launched + tr.FlightTime - Trial.Time):0} s" : "not detected",
                TrackState.Intercepted => "intercepted", _ => "impacted",
            };
            var sel = new Button { Text = $"T{tr.Id} → {tr.Target.Name} · {state}", ToggleMode = true, ButtonPressed = SelectedTrack == tr.Id, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left, ClipText = true };
            sel.AddThemeColorOverride("font_color", tr.State == TrackState.Intercepted ? UiTheme.Good : tr.State == TrackState.Impacted ? UiTheme.Bad : UiTheme.Ink);
            var id = tr.Id;
            sel.Pressed += () => { SelectedTrack = id; UpdateUi(); };
            h.AddChild(sel);
            if (bluePlayer && tr.State == TrackState.Flying)
                foreach (var n in new[] { 1, 2 })
                {
                    var e = new Button { Text = $"Engage ×{n}", Disabled = !tr.Detected || Trial.PlanIntercept(b, tr) == null };
                    e.Pressed += () => { var r = Trial.Engage(b.Def.Id, id, n); SelectedTrack = id; if (!r.Ok) _detail.Text = r.Message; UpdateUi(); };
                    h.AddChild(e);
                }
            _tracks.AddChild(h);
        }
        if (_tracks.GetChildCount() == 0) _tracks.AddChild(UiTheme.Text("No missiles in flight.", 12, UiTheme.Dim));

        Clear(_terms);
        var trk = Trial.Tracks.FirstOrDefault(x => x.Id == SelectedTrack) ?? Trial.Tracks.LastOrDefault(x => x.Launched <= Trial.Time);
        if (trk != null)
        {
            var m = trk.Missile;
            _detail.Text =
                $"{m.Name} · track {trk.Id} · {trk.State}\n" +
                $"Speed {m.R("speed"):0} ← {m.Sources("speed")}\n" +
                $"Agility {m.R("agility"):0} ← {m.Sources("agility")}\n" +
                $"Decoys (EW) {m.R("ew"):0} ← {m.Sources("ew")}\n" +
                $"Stealth {m.R("signature"):0} ← {m.Sources("signature")}\n" +
                $"Punch {m.R("punch"):0} ← {m.Sources("punch")}\n" +
                (trk.Detected ? $"Detected at T+{trk.DetectedAt:0} s by {b.Radar.Sources("detection")}" : "Not detected yet");
            var shot = Trial.Shots.LastOrDefault(s => s.Target == trk);
            IReadOnlyList<PkTerm> terms; double pk; string head;
            if (shot != null) { terms = shot.Terms; pk = shot.Pk; head = $"Assigned shot · {shot.State}"; }
            else if (trk.State == TrackState.Flying && Trial.PlanIntercept(b, trk) is { } plan) { (terms, pk) = Trial.KillChance(b, trk, Trial.Time + plan.T); head = "Preview if engaged now"; }
            else
            {
                terms = Array.Empty<PkTerm>(); pk = 0;
                var inReach = Pacing.DistanceKm(b.Def.Lon, b.Def.Lat, trk.Target.Lon, trk.Target.Lat) <= b.ReachKm;
                head = trk.State != TrackState.Flying ? "Track ended with no shot fired."
                    : !trk.Detected ? "No firing solution: not detected yet."
                    : inReach ? "No firing solution yet: waiting for the track to enter reach."
                    : $"No firing solution: {trk.Target.Name} is outside {b.Def.Name}'s {b.ReachKm:0} km reach ({b.Interceptor.Sources("reach")}). A longer-reach interceptor would cover it.";
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
        }

        if (_logSeen != Trial.Log.Count)
        {
            _logSeen = Trial.Log.Count;
            Clear(_log);
            foreach (var e in Trial.Log.TakeLast(7))
            {
                var c = e.Kind switch { "hit" => UiTheme.Good, "impact" or "refused" => UiTheme.Bad, "miss" => new Color("ffb347"), "launch" => new Color("ff8a8a"), _ => UiTheme.Ink };
                var lbl = UiTheme.Text($"T+{e.T,5:0}  {e.Text}", 12, c);
                lbl.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                lbl.CustomMinimumSize = new Vector2(660, 0);
                _log.AddChild(lbl);
            }
        }
    }

    private static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }
}

/// <summary>Draws the trial on the map: sites, radar and reach rings, missile arcs, interceptors and bursts.</summary>
public partial class TheatreLayer : Node2D
{
    public TheatreOps Ops = null!;
    /// <summary>Label anchors in map space; drawn crisp in screen space by <see cref="MapLabels"/>.</summary>
    public readonly List<(Vector2 At, string Text, Color Color)> Labels = new();
    private static readonly Color Blue = new("7fe3ff"), Red = new("ff4d5a"), Green = new("5cf2b0");

    private static Vector2 P(double lon, double lat) => Session.Project(lon, lat);
    private static float PxPerKm(double lat) => (float)(Session.PxPerDeg / (111.0 * Math.Cos(lat * Math.PI / 180)));

    /// <summary>A missile's drawn position: its ground point lifted by an arc (drawing only, not physics).</summary>
    private static Vector2 Arc(Track t, double time)
    {
        var (lon, lat) = t.Pos(time);
        var g = P(lon, lat);
        var len = P(t.FromLon, t.FromLat).DistanceTo(P(t.Target.Lon, t.Target.Lat));
        return g - new Vector2(0, (float)t.Altitude(time) * len * 0.22f);
    }

    public override void _Draw()
    {
        var tr = Ops?.Trial;
        if (tr == null) return;
        var zoom = GetViewport().GetCamera2D()?.Zoom.X ?? 1;
        var w = 1.5f / zoom;
        Labels.Clear();
        void Label(Vector2 p, string s, Color c) => Labels.Add((p, s, c));

        foreach (var s in tr.Scenario.Sites)
        {
            var p = P(s.Lon, s.Lat);
            var prot = !TargetPolicy.IsLegitimate(s.Class);
            var c = prot ? Green : s.Side == "blue" ? Blue : Red;
            if (prot) { DrawArc(p, 9 / zoom, 0, Mathf.Tau, 24, c, w); DrawCircle(p, 3 / zoom, c); }
            else DrawRect(new Rect2(p - new Vector2(6, 6) / zoom, new Vector2(12, 12) / zoom), c, false, w * 1.4f);
            Label(p, prot ? $"{s.Name} · protected" : s.Name + (tr.Hits.TryGetValue(s.Id, out var h) ? $" · hit ×{h}" : ""), c);
        }
        foreach (var b in tr.Batteries)
        {
            var p = P(b.Def.Lon, b.Def.Lat);
            var k = PxPerKm(b.Def.Lat);
            DrawArc(p, (float)b.RadarKm * k, 0, Mathf.Tau, 96, new Color(Blue, 0.35f), w);
            DrawCircle(p, (float)b.ReachKm * k, new Color(Blue, 0.07f));
            DrawArc(p, (float)b.ReachKm * k, 0, Mathf.Tau, 64, new Color(Blue, 0.7f), w);
            DrawColoredPolygon(new[] { p + new Vector2(0, -9) / zoom, p + new Vector2(8, 6) / zoom, p + new Vector2(-8, 6) / zoom }, Blue);
            Label(p, $"{b.Def.Name} · radar {b.RadarKm:0} km · reach {b.ReachKm:0} km", Blue);
        }
        foreach (var l in tr.Launchers)
        {
            var p = P(l.Def.Lon, l.Def.Lat);
            DrawColoredPolygon(new[] { p + new Vector2(0, -8) / zoom, p + new Vector2(8, 0) / zoom, p + new Vector2(0, 8) / zoom, p + new Vector2(-8, 0) / zoom }, Red);
            DrawArc(p, (float)l.ReachKm * PxPerKm(l.Def.Lat), 0, Mathf.Tau, 96, new Color(Red, 0.25f), w);
            Label(p, $"{l.Def.Name} · {l.Stock} left", Red);
        }

        foreach (var t in tr.Tracks.Where(t => t.Launched <= tr.Time))
        {
            var end = t.State == TrackState.Flying ? tr.Time : t.EndedAt;
            var a = P(t.FromLon, t.FromLat); var z = P(t.Target.Lon, t.Target.Lat);
            DrawDashedLine(a, z, new Color(Red, 0.25f), w, 8 / zoom);
            var pts = new List<Vector2>();
            for (var s = t.Launched; s <= end; s += Math.Max(0.5, t.FlightTime / 60)) pts.Add(Arc(t, s));
            pts.Add(Arc(t, end));
            var sel = Ops.SelectedTrack == t.Id;
            var col = t.State == TrackState.Intercepted ? new Color(Red, 0.35f) : t.Detected ? Red : new Color(1, 0.6f, 0.6f, 0.5f);
            if (pts.Count > 1) DrawPolyline(pts.ToArray(), col, sel ? w * 2.2f : w * 1.3f);
            var head = Arc(t, end);
            if (t.State == TrackState.Flying)
            {
                DrawCircle(head, 5 / zoom, Red);
                if (sel) DrawArc(head, 11 / zoom, 0, Mathf.Tau, 24, Color.Color8(255, 255, 255), w);
                Label(head, $"T{t.Id}{(t.Detected ? "" : " ?")}", Red);
            }
            Burst(t.State == TrackState.Intercepted ? head : z, t.EndedAt, t.State == TrackState.Intercepted ? Green : t.State == TrackState.Impacted ? Red : default, zoom);
        }

        foreach (var s in tr.Shots.Where(s => s.Launched <= tr.Time && s.State == ShotState.Flying))
        {
            var from = P(s.FromLon, s.FromLat);
            var aimGround = P(s.AimLon, s.AimLat);
            var aim = Arc(s.Target, s.Launched + s.FlightTime) ;
            var f = (float)Math.Clamp((tr.Time - s.Launched) / s.FlightTime, 0, 1);
            var pos = from.Lerp(aim, f);
            DrawLine(from, pos, Blue, w * 1.3f);
            DrawCircle(pos, 3.5f / zoom, Blue);
            DrawDashedLine(pos, aim, new Color(Blue, 0.3f), w, 5 / zoom);
            _ = aimGround;
        }
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
            var p = toLocal * (toScreen * at) + new Vector2(12, -8);
            DrawString(font, p + new Vector2(1, 1), text, HorizontalAlignment.Left, -1, 13, new Color(0, 0, 0, 0.8f));
            DrawString(font, p, text, HorizontalAlignment.Left, -1, 13, color);
        }
    }
}
