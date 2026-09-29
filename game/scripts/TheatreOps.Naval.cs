using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>Theatre Ops · NAVAL tab: your ships and submarines, their state, and move orders by clicking the map.</summary>
public partial class TheatreOps
{
    private VBoxContainer _navalView = null!, _shipList = null!;
    private Label _navalHint = null!;
    public ShipState? SelectedShip;
    /// <summary>Set while waiting for a map click that gives this ship its destination.</summary>
    public ShipState? AwaitingMove;

    private void BuildNaval(VBoxContainer v)
    {
        _navalView = v;
        v.AddChild(UiTheme.Text("FLEET", 13, UiTheme.Accent));
        _navalHint = UiTheme.Text("Select a ship, then Move and click the map. Ships carry their own air defence and strike cells; submarines ambush ships within 25 km.", 12, UiTheme.Dim, wrap: true);
        _navalHint.CustomMinimumSize = new Vector2(ConsoleW - 40, 0);
        v.AddChild(_navalHint);
        _shipList = new VBoxContainer();
        v.AddChild(_shipList);
    }

    private void UpdateNaval()
    {
        var naval = Trial.Naval;
        if (naval == null) return;
        Clear(_shipList);
        var side = ViewSide == "all" ? CommandSide : ViewSide;
        foreach (var s in naval.Ships.Where(s => s.Side == side).OrderBy(s => s.Sunk).ThenBy(s => s.Kind).ThenBy(s => s.Name))
        {
            var row = new HBoxContainer();
            var name = new Button { Text = $"{(s.Kind == "sub" ? "◒" : "⚓")} {s.Name}", ToggleMode = true, ButtonPressed = SelectedShip == s, Flat = SelectedShip != s, Alignment = HorizontalAlignment.Left, ClipText = true, CustomMinimumSize = new Vector2(230, 0) };
            name.AddThemeFontSizeOverride("font_size", 12);
            name.AddThemeColorOverride("font_color", s.Sunk ? UiTheme.Bad : side == "blue" ? TheatreLayer.Blue : TheatreLayer.Red);
            var ss = s;
            name.Pressed += () => { SelectedShip = SelectedShip == ss ? null : ss; UpdateNaval(); };
            row.AddChild(name);
            var hp = new ProgressBar { MaxValue = s.MaxHp, Value = s.Hp, ShowPercentage = false, CustomMinimumSize = new Vector2(70, 8), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            var col = s.Hp / s.MaxHp >= 0.8 ? UiTheme.Good : s.Hp / s.MaxHp >= 0.5 ? new Color("ffb347") : UiTheme.Bad;
            hp.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.05f, 0.12f, 0.2f) });
            hp.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = col });
            row.AddChild(hp);
            var sys = string.Join(" · ", s.Batteries.Select(b => $"{b.Stock} SAM").Concat(s.Launchers.Select(l => $"{l.Stock} strike")));
            var status = s.Sunk ? "SUNK" : s.Moving ? $"moving · {Pacing.DistanceKm(s.Lon, s.Lat, s.DestLon!.Value, s.DestLat!.Value):0} km to go" : "holding";
            var st = UiTheme.Text($"{status}{(sys != "" ? " · " + sys : "")}", 11, s.Sunk ? UiTheme.Bad : UiTheme.Ink);
            st.ClipText = true; st.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(st);
            if (!s.Sunk && s.Side == CommandSide)
            {
                var mv = new Button { Text = AwaitingMove == s ? "click map…" : "Move" };
                mv.AddThemeFontSizeOverride("font_size", 11);
                mv.Pressed += () => { AwaitingMove = ss; SelectedShip = ss; _navalHint.Text = $"Click the map to send {ss.Name}. Right-click to cancel."; UpdateNaval(); };
                var hold = new Button { Text = "Hold", Disabled = !s.Moving };
                hold.AddThemeFontSizeOverride("font_size", 11);
                hold.Pressed += () => { naval.Hold(ss.Id); UpdateNaval(); };
                row.AddChild(mv); row.AddChild(hold);
            }
            _shipList.AddChild(row);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (AwaitingMove == null) { HudInput(e); return; }
        if (e is not InputEventMouseButton { Pressed: true } mb) return;
        if (mb.ButtonIndex == MouseButton.Right) { AwaitingMove = null; UpdateNaval(); GetViewport().SetInputAsHandled(); return; }
        if (mb.ButtonIndex != MouseButton.Left) return;
        var world = GetViewport().GetCanvasTransform().AffineInverse() * mb.Position;
        var (lon, lat) = Session.Unproject(world);
        var msg = Trial.Naval!.MoveTo(AwaitingMove.Id, lon, lat);
        _navalHint.Text = msg;
        AwaitingMove = null;
        GetViewport().SetInputAsHandled();
        UpdateNaval();
    }
}
