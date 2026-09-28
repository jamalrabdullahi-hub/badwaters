using BadWaters.Sim.Engineering;
using Godot;

namespace BadWaters.Game;

/// <summary>Colours for the low-res part shapes (like RTW's .eqs drawings).</summary>
public static class PartPalette
{
    public static Color Fill(string f) => f switch
    {
        "dark" => new Color("2c3740"), "light" => new Color("aab8c2"), "deck" => new Color("7c8578"), "glass" => new Color("4fa8d8"),
        "black" => new Color("0d1216"), "red" => new Color("b03a2e"), "yellow" => new Color("c9a23a"), _ => new Color("6f7f8b"),
    };

    /// <summary>Draw a part's shapes onto a canvas item. toScreen maps part-local metres (already rotated) to pixels.</summary>
    public static void Draw(CanvasItem ci, VisualPart p, Func<double, double, Vector2> toScreen, float px, double rotDeg, float alpha = 1, double sx = 1, double sy = 1)
    {
        var a = rotDeg * Math.PI / 180;
        var (sin, cos) = (Math.Sin(a), Math.Cos(a));
        Vector2 T(double x, double y) { x *= sx; y *= sy; return toScreen(x * cos + y * sin, -x * sin + y * cos); } // stretch, then clockwise rotation (+y = forward)
        px *= (float)Math.Sqrt(sx * sy);
        foreach (var s in p.Shapes)
        {
            var c = Fill(s.Fill); c.A = alpha;
            switch (s.T)
            {
                case "circle":
                    ci.DrawCircle(T(s.X, s.Y), Math.Max(1, (float)s.R * px), c);
                    break;
                case "rect":
                    var r = new[] { T(s.X - s.W / 2, s.Y - s.H / 2), T(s.X + s.W / 2, s.Y - s.H / 2), T(s.X + s.W / 2, s.Y + s.H / 2), T(s.X - s.W / 2, s.Y + s.H / 2) };
                    ci.DrawColoredPolygon(r, c);
                    break;
                case "poly":
                    var pts = s.P.Chunk(2).Select(q => T(q[0], q[1])).ToArray();
                    if (pts.Length >= 3) { try { ci.DrawColoredPolygon(pts, c); } catch { ci.DrawPolyline(pts.Append(pts[0]).ToArray(), c, 1); } }
                    break;
                default:
                    var ln = s.P.Chunk(2).Select(q => T(q[0], q[1])).ToArray();
                    if (ln.Length >= 2) ci.DrawPolyline(ln, c, Math.Max(1, px * 0.25f));
                    break;
            }
        }
    }
}

/// <summary>
/// The designer's blueprint: a top-down hull (bow up) with drag-and-drop parts. Drop parts from the Parts
/// tab, drag to move, Q/E to rotate 15°, R to rotate 90°, Delete to remove, wheel to zoom. With Mirror on,
/// an off-centre drop also places its twin on the other side.
/// </summary>
public partial class Blueprint : Control
{
    public Func<DesignDef>? Design;
    /// <summary>Changed: after an edit. BeforeChange: just before an edit (for undo). SelectionChanged: a part was (de)selected.</summary>
    public Action? Changed, SelectionChanged, BeforeChange;
    public int Selected = -1;
    public bool ShowArcs = true, Mirror = true;
    /// <summary>Shape edit mode: drag handles, click an edge to add a vertex, right-click a vertex to delete it.</summary>
    public bool EditOutline;
    /// <summary>Top view (plan, parts editable) or side view (profile).</summary>
    public bool SideView;
    public float Zoom = 1;
    private int _vertex = -1;
    private bool _dragging;
    private Vector2 _grab;

    private static Catalog Cat => Session.Content.Catalog;
    private FrameDef? Frame => Design?.Invoke() is { } d && Cat.Frames.TryGetValue(d.Frame, out var f) ? f : null;
    private HullDef Hull => Frame?.Hull ?? new HullDef(20, 10, "pad");
    private bool IsAirOrMissile => Frame?.Kind is "aircraft" or "munition";

    public override void _Ready() { FocusMode = FocusModeEnum.All; ClipContents = true; }

    // ---- coordinates. Top view: bow up (x = starboard, y = forward). Side view: bow right (y along length, z up).
    private float Scale => SideView
        ? (float)Math.Min((Size.X - 30) / Hull.Length, (Size.Y - 30) / Math.Max(1, SideHeight() * 2.4)) * Zoom
        : (float)Math.Min((Size.Y - 30) / Hull.Length, (Size.X - 30) / (Hull.Width * 1.15)) * Zoom;
    private Vector2 Centre => Size / 2;
    public Vector2 ToScreen(double x, double y) => SideView ? Centre + new Vector2((float)x * Scale, -(float)y * Scale) : Centre + new Vector2((float)x * Scale, -(float)y * Scale);
    public (double X, double Y) ToWorld(Vector2 p) => ((p.X - Centre.X) / Scale, -(p.Y - Centre.Y) / Scale);
    private double SideHeight() => SidePoints().Select(p => Math.Abs(p.Y)).DefaultIfEmpty(1).Max();

    /// <summary>Top outline (right half) to draw and edit: the design's own, or the frame's standard (simplified for editing).</summary>
    public List<(double X, double Y)> OutlinePoints()
    {
        var d = Design?.Invoke();
        if (d != null && d.Outline.Count >= 6) return d.Outline.Chunk(2).Select(c => (c[0], c[1])).ToList();
        var std = HullGeometry.Outline(Hull);
        if (std.Count > 20) std = std.Where((_, k) => k % 4 == 0 || k == std.Count - 1).ToList();
        return std;
    }

    /// <summary>Side profile (full polygon) in view coordinates: x = along the length (bow right), y = height.</summary>
    public List<(double X, double Y)> SidePoints()
    {
        var d = Design?.Invoke();
        if (d != null && d.Side.Count >= 6) return d.Side.Chunk(2).Select(c => (c[0], c[1])).ToList();
        return Frame?.Hull != null ? SideGeometry.Profile(Hull) : new();
    }

    private List<double> Store(DesignDef d) => SideView ? d.Side : d.Outline;
    private List<(double X, double Y)> EditPoints() => SideView ? SidePoints() : OutlinePoints();

    public void ResetOutline() { if (Design?.Invoke() is { } d) { BeforeChange?.Invoke(); Store(d).Clear(); _vertex = -1; Changed?.Invoke(); QueueRedraw(); } }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0a1a2a"));
        var d = Design?.Invoke();
        var font = ThemeDB.FallbackFont;
        if (SideView) { DrawSide(d); DrawHandles(); DrawString(font, new Vector2(8, 16), $"{Frame?.Name} · side profile · {(IsAirOrMissile ? "nose" : "bow")} →", HorizontalAlignment.Left, -1, 12, UiTheme.Dim); return; }

        var s = Scale;
        for (var gy = -Hull.Length; gy <= Hull.Length; gy += Hull.Length > 60 ? 10 : Hull.Length > 8 ? 1 : 0.25)
            DrawLine(ToScreen(-Hull.Width, gy), ToScreen(Hull.Width, gy), new Color(0.3f, 0.6f, 0.85f, 0.06f), 1);
        var right = OutlinePoints().Select(p => ToScreen(p.X, p.Y)).ToList();
        var left = right.Select(p => new Vector2(2 * Centre.X - p.X, p.Y)).Reverse();
        var poly = right.Concat(left).ToArray();
        try { DrawColoredPolygon(poly, new Color("56626b")); } catch { /* self-crossing outline while editing */ }
        DrawPolyline(poly.Append(poly[0]).ToArray(), new Color("9fb0bc"), 1.5f);
        DrawDashedLine(ToScreen(0, -Hull.Length / 2), ToScreen(0, Hull.Length / 2), new Color(0.5f, 0.85f, 1, 0.25f), 1, 6);
        if (d == null) return;
        var half = ShapeRules.TopHalf(Frame!, d);
        for (var i = 0; i < d.Parts.Count; i++)
        {
            var pl = d.Parts[i];
            if (!Cat.Parts.TryGetValue(pl.Part, out var p)) continue;
            var rs = PartResolver.Resolve(p, pl, Cat);
            PartPalette.Draw(this, p, (x, y) => ToScreen(pl.X + x, pl.Y + y), s, pl.Rot, EditOutline ? 0.5f : 1, rs.ShapeX, rs.ShapeY);
            if (ShowArcs && !EditOutline && p.Arc > 0 && (i == Selected || Selected < 0))
            {
                var c = ToScreen(pl.X, pl.Y);
                var reach = (float)Math.Max(p.Extent * 4, Hull.Width * 0.8) * s;
                var (_, sectors) = ArcGeometry.Coverage(d, i, Cat, Hull.Width * 3);
                var alpha = i == Selected ? 0.75f : 0.22f;
                foreach (var sec in sectors)
                {
                    var a0 = (float)((sec.From - 90) * Math.PI / 180); var a1 = (float)((sec.To - 90) * Math.PI / 180);
                    var col = sec.Blocked ? new Color(1, 0.3f, 0.3f, alpha) : new Color(1, 0.85f, 0.3f, alpha);
                    var pts = new List<Vector2> { c };
                    for (var k = 0; k <= 6; k++) { var a = a0 + (a1 - a0) * k / 6; pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach); }
                    DrawColoredPolygon(pts.ToArray(), new Color(col, col.A * 0.25f));
                    DrawArc(c, reach, a0, a1, 8, col, 1);
                }
            }
            if (i == Selected) DrawArc(ToScreen(pl.X, pl.Y), (float)(p.Extent * pl.Scale) * s + 3, 0, Mathf.Tau, 32, new Color("7fe3ff"), 1.5f);
            if (!HullGeometry.Inside(half, pl.X, pl.Y, Math.Max(0.1, Hull.Width * 0.05))) DrawArc(ToScreen(pl.X, pl.Y), (float)p.Extent * s + 5, 0, Mathf.Tau, 32, UiTheme.Bad, 2);
        }
        DrawHandles();
        DrawString(font, new Vector2(8, 16), $"{Frame?.Name}  ·  {Hull.Length:0.#} × {Hull.Width:0.#} m  ·  {(IsAirOrMissile ? "nose" : "bow")} ↑", HorizontalAlignment.Left, -1, 12, UiTheme.Dim);
    }

    /// <summary>Side view: the profile polygon, a waterline for ships, and parts as blocks sized by their height class.</summary>
    private void DrawSide(DesignDef? d)
    {
        var pts = SidePoints().Select(p => ToScreen(p.X, p.Y)).ToArray();
        if (pts.Length >= 3)
        {
            try { DrawColoredPolygon(pts, new Color("56626b")); } catch { }
            DrawPolyline(pts.Append(pts[0]).ToArray(), new Color("9fb0bc"), 1.5f);
        }
        if (Frame?.Kind == "ship") DrawLine(ToScreen(-Hull.Length * 0.6, 0), ToScreen(Hull.Length * 0.6, 0), new Color(0.3f, 0.6f, 0.9f, 0.6f), 1);
        if (d == null || Frame?.Kind != "ship" && Frame?.Kind != "ground") return;
        var deck = SidePoints().Where(p => p.Y > 0).Select(p => p.Y).DefaultIfEmpty(0).Min() is var m && m > 0 ? SidePoints().Max(p => p.Y) * 0.8 : 0;
        var unit = Frame.Kind == "ship" ? Math.Max(1.2, Hull.Beam * 0.12) : 0.5;
        foreach (var pl in d.Parts.OrderBy(q => Cat.Parts.TryGetValue(q.Part, out var pp) ? ArcGeometry.Height(pp) : 0))
        {
            if (!Cat.Parts.TryGetValue(pl.Part, out var p)) continue;
            var h = Math.Max(0.4, ArcGeometry.Height(p)) * unit * pl.Scale;
            var w = p.Extent * 1.4 * pl.Scale;
            var a = ToScreen(pl.Y - w / 2, deck + h); var b = ToScreen(pl.Y + w / 2, deck);
            DrawRect(new Rect2(a, b - a), PartPalette.Fill(p.Shapes.FirstOrDefault()?.Fill ?? "steel"));
            DrawRect(new Rect2(a, b - a), new Color("2c3740"), false, 1);
        }
    }

    private void DrawHandles()
    {
        if (!EditOutline) return;
        var pts = EditPoints();
        for (var k = 0; k < pts.Count; k++)
        {
            var sp = ToScreen(pts[k].X, pts[k].Y);
            DrawRect(new Rect2(sp - new Vector2(4, 4), new Vector2(8, 8)), k == _vertex ? UiTheme.Accent : Colors.White, false, 1.5f);
            if (!SideView) DrawRect(new Rect2(new Vector2(2 * Centre.X - sp.X, sp.Y) - new Vector2(3, 3), new Vector2(6, 6)), new Color(1, 1, 1, 0.35f), false, 1);
        }
    }

    private int HitTest(Vector2 at)
    {
        var d = Design?.Invoke();
        if (d == null || SideView) return -1;
        for (var i = d.Parts.Count - 1; i >= 0; i--)
            if (Cat.Parts.TryGetValue(d.Parts[i].Part, out var p) && ToScreen(d.Parts[i].X, d.Parts[i].Y).DistanceTo(at) <= Math.Max(6, p.Extent * d.Parts[i].Scale * Scale * 0.8)) return i;
        return -1;
    }

    private (double, double) Snap(double x, double y)
    {
        var g = Hull.Length > 60 ? 0.5 : Hull.Length > 8 ? 0.1 : 0.05;
        return (Math.Abs(x) < g * 1.2 ? 0 : Math.Round(x / g) * g, Math.Round(y / g) * g);
    }

    /// <summary>Copy the shown outline into the design so it can be edited.</summary>
    private void Materialise(DesignDef d, List<(double X, double Y)> pts)
    {
        var store = Store(d);
        if (store.Count >= 6) return;
        store.Clear();
        foreach (var (x, y) in pts) { store.Add(x); store.Add(y); }
    }

    public override void _GuiInput(InputEvent e)
    {
        var d = Design?.Invoke();
        if (d == null) return;
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            GrabFocus();
            if (mb.ButtonIndex == MouseButton.WheelUp) { Zoom = Math.Min(8, Zoom * 1.15f); QueueRedraw(); }
            else if (mb.ButtonIndex == MouseButton.WheelDown) { Zoom = Math.Max(0.5f, Zoom / 1.15f); QueueRedraw(); }
            else if (EditOutline) ShapeClick(d, mb);
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                Selected = HitTest(mb.Position);
                _dragging = Selected >= 0;
                if (_dragging) { BeforeChange?.Invoke(); _grab = ToScreen(d.Parts[Selected].X, d.Parts[Selected].Y) - mb.Position; }
                SelectionChanged?.Invoke(); QueueRedraw();
            }
            else if (mb.ButtonIndex == MouseButton.Right && HitTest(mb.Position) is var h && h >= 0) { Selected = h; Rotate(90); }
            AcceptEvent();
        }
        else if (e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } && _dragging) { _dragging = false; Changed?.Invoke(); }
        else if (e is InputEventMouseMotion mv && _dragging && EditOutline && _vertex >= 0)
        {
            var (x, y) = ToWorld(mv.Position);
            var store = Store(d);
            store[_vertex * 2] = SideView ? Math.Round(x, 2) : Math.Max(0, Math.Round(x, 2)); // top: right half only, the left mirrors it
            store[_vertex * 2 + 1] = Math.Round(y, 2);
            QueueRedraw();
        }
        else if (e is InputEventMouseMotion mm && _dragging && Selected >= 0)
        {
            var (x, y) = Snap(ToWorld(mm.Position + _grab).X, ToWorld(mm.Position + _grab).Y);
            d.Parts[Selected] = d.Parts[Selected] with { X = x, Y = y };
            QueueRedraw();
        }
        else if (e is InputEventKey { Pressed: true } k && Selected >= 0 && !EditOutline)
        {
            if (k.Keycode is Key.Delete or Key.Backspace) Remove();
            else if (k.Keycode == Key.Q) Rotate(-15);
            else if (k.Keycode == Key.E) Rotate(15);
            else if (k.Keycode == Key.R) Rotate(90);
            AcceptEvent();
        }
    }

    /// <summary>Shape editing: grab a vertex, click an edge to insert one, right-click a vertex to delete it.</summary>
    private void ShapeClick(DesignDef d, InputEventMouseButton mb)
    {
        var pts = EditPoints();
        var near = -1;
        for (var k = 0; k < pts.Count; k++) if (ToScreen(pts[k].X, pts[k].Y).DistanceTo(mb.Position) < 8) near = k;
        if (mb.ButtonIndex == MouseButton.Right)
        {
            if (near < 0 || pts.Count <= 3) return;
            BeforeChange?.Invoke();
            Materialise(d, pts);
            Store(d).RemoveRange(near * 2, 2);
            _vertex = -1; Changed?.Invoke(); QueueRedraw();
            return;
        }
        if (mb.ButtonIndex != MouseButton.Left) return;
        if (near >= 0)
        {
            BeforeChange?.Invoke();
            Materialise(d, pts);
            _vertex = near; _dragging = true; QueueRedraw();
            return;
        }
        // insert on the nearest edge (top view edges run along the right half; the side profile is closed)
        var edges = SideView ? pts.Count : pts.Count - 1;
        var best = -1; var bestD = 7f; var at = Vector2.Zero;
        for (var k = 0; k < edges; k++)
        {
            var a = ToScreen(pts[k].X, pts[k].Y); var b = ToScreen(pts[(k + 1) % pts.Count].X, pts[(k + 1) % pts.Count].Y);
            var q = Geometry2D.GetClosestPointToSegment(mb.Position, a, b);
            var dist = q.DistanceTo(mb.Position);
            if (dist < bestD) { bestD = dist; best = k; at = q; }
        }
        if (best < 0) return;
        BeforeChange?.Invoke();
        Materialise(d, pts);
        var (wx, wy) = ToWorld(at);
        Store(d).InsertRange((best + 1) * 2, new[] { Math.Round(wx, 2), Math.Round(wy, 2) });
        _vertex = best + 1; _dragging = true;
        Changed?.Invoke(); QueueRedraw();
    }

    public void Rotate(double deg)
    {
        var d = Design?.Invoke();
        if (d == null || Selected < 0) return;
        BeforeChange?.Invoke();
        d.Parts[Selected] = d.Parts[Selected] with { Rot = ((d.Parts[Selected].Rot + deg) % 360 + 360) % 360 };
        Changed?.Invoke(); QueueRedraw();
    }

    public void Remove()
    {
        var d = Design?.Invoke();
        if (d == null || Selected < 0) return;
        BeforeChange?.Invoke();
        d.Parts.RemoveAt(Selected);
        Selected = -1;
        SelectionChanged?.Invoke(); Changed?.Invoke(); QueueRedraw();
    }

    public void MirrorSelected()
    {
        var d = Design?.Invoke();
        if (d == null || Selected < 0) return;
        BeforeChange?.Invoke();
        var p = d.Parts[Selected];
        d.Parts.Add(p with { X = -p.X, Rot = (360 - p.Rot) % 360 });
        Changed?.Invoke(); QueueRedraw();
    }

    public override bool _CanDropData(Vector2 at, Variant data) => !SideView && data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey("part");

    public override void _DropData(Vector2 at, Variant data)
    {
        var d = Design?.Invoke();
        if (d == null) return;
        BeforeChange?.Invoke();
        var id = data.AsGodotDictionary()["part"].AsString();
        var (x, y) = Snap(ToWorld(at).X, ToWorld(at).Y);
        d.Parts.Add(new Placement(id, x, y));
        if (Mirror && Math.Abs(x) > Hull.Width * 0.03) d.Parts.Add(new Placement(id, -x, y));
        Selected = d.Parts.Count - 1;
        SelectionChanged?.Invoke(); Changed?.Invoke(); QueueRedraw();
    }
}

/// <summary>A part in the Parts tab: a small drawing plus its name. Drag it onto the blueprint.</summary>
public partial class PartTile : Button
{
    public VisualPart Part = null!;

    public override void _Ready()
    {
        if (CustomMinimumSize == Vector2.Zero) CustomMinimumSize = new Vector2(96, 92);
        if (Part == null) return; // assigned later (component designer preview)
        TooltipText = $"{Part.Name}\n{Part.Category}{(Part.Topside ? " · topside" : "")}\n" +
                      string.Join(", ", Part.Uses.Select(u => $"{u.Key} {u.Value:0.#}")) +
                      (Part.Adds.Count > 0 ? "\n" + string.Join(", ", Part.Adds.Select(a => $"+{a.Value:0.#} {a.Key}")) : "") +
                      (Part.Component != null ? $"\nincludes: {Session.Content.Catalog.Components.GetValueOrDefault(Part.Component)?.Name}" : "") +
                      "\nDrag onto the blueprint.";
    }

    public override void _Draw()
    {
        if (Part == null) return;
        var box = new Rect2(4, 4, Size.X - 8, Size.Y - 26);
        DrawRect(box, new Color("0a1a2a"));
        var ext = (float)Math.Max(1, Part.Extent);
        var px = Math.Min(box.Size.X, box.Size.Y) / (ext * 2.2f);
        var c = box.GetCenter();
        PartPalette.Draw(this, Part, (x, y) => c + new Vector2((float)x * px, -(float)y * px), px, 0);
        DrawString(ThemeDB.FallbackFont, new Vector2(4, Size.Y - 8), Part.Name.Length > 16 ? Part.Name[..15] + "…" : Part.Name, HorizontalAlignment.Left, Size.X - 8, 11, UiTheme.Ink);
    }

    public override Variant _GetDragData(Vector2 at)
    {
        var preview = new PartTile { Part = Part, Modulate = new Color(1, 1, 1, 0.8f) };
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary { { "part", Part.Id } };
    }
}
