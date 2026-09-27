using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// The ATO sync strip along the bottom of Theatre Ops: every flight and fire mission as a bar on one
/// time axis, TOT diamonds, IP ticks, a "now" line and each package's TOT line with a count of what
/// arrives together. Click a flight row to select it. Scroll to see more rows.
/// </summary>
public partial class AtoTimeline : Control
{
    public TheatreOps Ops = null!;
    private int _scroll;
    private readonly List<(Rect2 Row, Flight? Flight)> _hits = new();
    private const float LabelW = 250, RowH = 15, Top = 34;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    private string Side => Ops.ViewSide == "all" ? Ops.CommandSide : Ops.ViewSide;

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelDown) { _scroll++; QueueRedraw(); }
            else if (mb.ButtonIndex == MouseButton.WheelUp) { _scroll = Math.Max(0, _scroll - 1); QueueRedraw(); }
            else if (mb.ButtonIndex == MouseButton.Left)
                foreach (var (r, f) in _hits)
                    if (r.HasPoint(mb.Position) && f != null) { Ops.SelectedFlight = f; Ops.ShowTab("ATO"); break; }
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        _hits.Clear();
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.010f, 0.026f, 0.050f, 0.98f));
        DrawLine(Vector2.Zero, new Vector2(Size.X, 0), UiTheme.Line, 1);
        var air = Ops?.Trial?.Air;
        var font = ThemeDB.FallbackFont;
        void Text(float x, float y, string t, Color c, int size = 11) => DrawString(font, new Vector2(x, y), t, HorizontalAlignment.Left, -1, size, c);
        Text(12, 18, $"ATO SYNC · {Ops?.SideName(Side).ToUpperInvariant()}", UiTheme.Accent, 13);
        if (air == null) return;
        var now = Ops.Trial.Time;
        var last = Ops.Trial.Log.LastOrDefault();
        if (last != null) Text(LabelW + 10, 18, $"T+{AirTasking.Clock(last.T)}  {last.Text}", UiTheme.Dim);

        // rows: packages (their flights), then fire missions, then stations
        var rows = new List<(string Label, double A, double B, double? Tot, double? Ip, Color C, Flight? F, bool Thin)>();
        var sideCol = Side == "blue" ? TheatreLayer.Blue : TheatreLayer.Red;
        var amber = new Color("ffb347");
        var from = Math.Max(0, now - 600);
        foreach (var p in air.Packages.Where(p => p.Side == Side).OrderBy(p => p.Tot))
            foreach (var f in p.Flights.Where(f => air.Flights.Contains(f) && f.LandAt >= from))
                rows.Add(($"{p.Name} · {f.Callsign} · {f.Aircraft.Count}× {f.Type.Name} {f.Mission}", f.Takeoff, f.LandAt, f.Route.FirstOrDefault(w => w.Name is "Target" or "Sweep" or "SEAD")?.T, f.Route.FirstOrDefault(w => w.Name == "IP")?.T,
                    f.State == FlightState.Lost ? UiTheme.Bad : f.Mission == Mission.Tanker ? new Color(sideCol, 0.6f) : sideCol, f, false));
        foreach (var fm in air.Fires.Where(x => x.Side == Side && x.State != FireState.Failed && x.Tot >= from).OrderBy(x => x.Tot))
            rows.Add(($"Fires · {fm.Launcher.Def.Name} · {fm.Count}× {fm.Launcher.Missile.Name} → {fm.Target.Name}", fm.LaunchAt, fm.Tot, fm.Tot, null, amber, null, true));
        foreach (var f in air.Flights.Where(f => f.Side == Side && f.LandAt >= from && air.Packages.All(p => p.Id != f.PackageId)).OrderBy(f => f.Takeoff))
            rows.Add(($"{f.Mission} · {f.Callsign} · {f.Aircraft.Count}× {f.Type.Name}", f.Takeoff, f.LandAt, null, null, new Color(sideCol, 0.45f), f, false));

        // time axis: from 10 min before now, wide enough to show the next TOTs
        var t0 = Math.Max(0, now - 600);
        var t1 = Math.Max(t0 + 3600, rows.Where(r => r.Tot != null).Select(r => r.Tot!.Value + 900).DefaultIfEmpty(t0 + 5400).Max());
        t1 = Math.Min(t1, t0 + 3 * 3600);
        float X(double t) => LabelW + (float)((t - t0) / (t1 - t0)) * (Size.X - LabelW - 16);
        var step = (t1 - t0) > 7200 ? 900 : 600;
        for (var t = Math.Ceiling(t0 / step) * step; t <= t1; t += step)
        {
            DrawLine(new Vector2(X(t), Top - 6), new Vector2(X(t), Size.Y), new Color(UiTheme.Line, 0.5f), 1);
            Text(X(t) + 3, Top - 8, AirTasking.Clock(t), UiTheme.Dim, 10);
        }

        var visible = (int)((Size.Y - Top - 6) / RowH);
        _scroll = Math.Min(_scroll, Math.Max(0, rows.Count - visible));
        for (var i = 0; i < visible && i + _scroll < rows.Count; i++)
        {
            var r = rows[i + _scroll];
            var y = Top + i * RowH;
            var sel = r.F != null && r.F == Ops.SelectedFlight;
            if (sel) DrawRect(new Rect2(0, y - 1, Size.X, RowH), new Color(UiTheme.Accent, 0.12f));
            var label = r.Label.Length > 42 ? r.Label[..41] + "…" : r.Label;
            Text(12, y + 11, label, sel ? UiTheme.Accent : UiTheme.Ink);
            var a = Math.Max(X(r.A), LabelW); var b = Math.Min(X(r.B), Size.X - 16);
            if (b > a) DrawRect(new Rect2(a, y + (r.Thin ? 6 : 3), b - a, r.Thin ? 3 : 8), new Color(r.C, r.Thin ? 0.8f : 0.4f));
            if (r.Ip is { } ip && ip >= t0 && ip <= t1) DrawRect(new Rect2(X(ip) - 3, y + 4, 6, 6), r.C, false, 1.5f);
            if (r.Tot is { } tot && tot >= t0 && tot <= t1)
            {
                var c = new Vector2(X(tot), y + 7);
                DrawColoredPolygon(new[] { c + new Vector2(0, -5), c + new Vector2(5, 0), c + new Vector2(0, 5), c + new Vector2(-5, 0) }, r.C);
            }
            _hits.Add((new Rect2(0, y - 1, Size.X, RowH), r.F));
        }
        if (rows.Count > visible) Text(Size.X - 150, Size.Y - 6, $"{_scroll + 1}–{Math.Min(rows.Count, _scroll + visible)} of {rows.Count} · scroll", UiTheme.Dim, 10);

        // package TOT lines: what arrives together (flights + fire missions on the same target within 2 min)
        foreach (var p in air.Packages.Where(p => p.Side == Side && p.Tot >= t0 && p.Tot <= t1))
        {
            var fires = air.Fires.Where(f => f.Side == Side && f.Target.Id == p.Target.Id && Math.Abs(f.Tot - p.Tot) <= 120 && f.State != FireState.Failed).Sum(f => f.Count);
            var x = X(p.Tot);
            DrawDashedLine(new Vector2(x, Top - 4), new Vector2(x, Size.Y), new Color(UiTheme.Accent, 0.55f), 1.5f, 5);
            Text(x + 4, Size.Y - 6, $"{p.Name} TOT {AirTasking.Clock(p.Tot)} · {p.Flights.Count(f => f.Aircraft.Count > 0)} flights{(fires > 0 ? $" + {fires} missiles" : "")}", UiTheme.Accent, 10);
        }
        if (now >= t0 && now <= t1)
        {
            DrawLine(new Vector2(X(now), Top - 6), new Vector2(X(now), Size.Y), UiTheme.Good, 2);
            Text(X(now) + 3, Top + 2, "now", UiTheme.Good, 10);
        }
    }
}
