using BadWaters.Sim.Engineering;
using Godot;

namespace BadWaters.Game;

/// <summary>Colours for the low-res part shapes (like RTW's .eqs drawings).</summary>
public static class PartPalette
{
    public static Color Fill(string f) => f switch
    {
        "dark" => new Color("2c3740"), "light" => new Color("aab8c2"), "deck" => new Color("7c8578"), "glass" => new Color("4fa8d8"),
        "black" => new Color("0d1216"), "red" => new Color("b03a2e"), _ => new Color("6f7f8b"),
    };

    /// <summary>Draw a part's shapes onto a canvas item. toScreen maps part-local metres (already rotated) to pixels.</summary>
    public static void Draw(CanvasItem ci, VisualPart p, Func<double, double, Vector2> toScreen, float px, double rotDeg, float alpha = 1)
    {
        var a = rotDeg * Math.PI / 180;
        var (sin, cos) = (Math.Sin(a), Math.Cos(a));
        Vector2 T(double x, double y) => toScreen(x * cos + y * sin, -x * sin + y * cos); // clockwise rotation, +y = forward
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
    public Action? Changed, SelectionChanged;
    public int Selected = -1;
    public bool ShowArcs = true, Mirror = true;
    /// <summary>Outline edit mode: drag the white handles (mirrored automatically). Cosmetic only.</summary>
    public bool EditOutline;
    private int _vertex = -1;

    /// <summary>The outline to draw: the design's cosmetic override, or the frame's standard outline (simplified for editing).</summary>
    public List<(double X, double Y)> OutlinePoints()
    {
        var d = Design?.Invoke();
        if (d != null && d.Outline.Count >= 6) return d.Outline.Chunk(2).Select(c => (c[0], c[1])).ToList();
        var std = HullGeometry.Outline(Hull);
        if (std.Count > 20) std = std.Where((_, k) => k % 4 == 0 || k == std.Count - 1).ToList(); // editable handles for sampled hulls
        return std;
    }

    public void ResetOutline() { Design?.Invoke()?.Outline.Clear(); _vertex = -1; Changed?.Invoke(); QueueRedraw(); }
    public float Zoom = 1;
    private bool _dragging;
    private Vector2 _grab;

    private static Catalog Cat => Session.Content.Catalog;
    private FrameDef? Frame => Design?.Invoke() is { } d && Cat.Frames.TryGetValue(d.Frame, out var f) ? f : null;
    private HullDef Hull => Frame?.Hull ?? new HullDef(20, 10, "pad");

    public override void _Ready() { FocusMode = FocusModeEnum.All; ClipContents = true; }

    private float Scale => (float)Math.Min((Size.Y - 30) / Hull.Length, (Size.X - 30) / (Hull.Width * 1.15)) * Zoom;
    private Vector2 Centre => Size / 2;
    public Vector2 ToScreen(double x, double y) => Centre + new Vector2((float)x * Scale, -(float)y * Scale);
    public (double X, double Y) ToWorld(Vector2 p) => ((p.X - Centre.X) / Scale, -(p.Y - Centre.Y) / Scale);

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0a1a2a"));
        var s = Scale;
        for (var gy = -Hull.Length; gy <= Hull.Length; gy += Hull.Length > 60 ? 10 : Hull.Length > 8 ? 1 : 0.25)
            DrawLine(ToScreen(-Hull.Width, gy), ToScreen(Hull.Width, gy), new Color(0.3f, 0.6f, 0.85f, 0.06f), 1);
        // hull outline from the shared profile
        var right = OutlinePoints().Select(p => ToScreen(p.X, p.Y)).ToList();
        var left = right.Select(p => new Vector2(2 * Centre.X - p.X, p.Y)).Reverse();
        var poly = right.Concat(left).ToArray();
        try { DrawColoredPolygon(poly, new Color("56626b")); } catch { /* degenerate outline at extreme zoom */ }
        DrawPolyline(poly.Append(poly[0]).ToArray(), new Color("9fb0bc"), 1.5f);
        DrawDashedLine(ToScreen(0, -Hull.Length / 2), ToScreen(0, Hull.Length / 2), new Color(0.5f, 0.85f, 1, 0.25f), 1, 6);

        var d = Design?.Invoke();
        if (d == null) return;
        for (var i = 0; i < d.Parts.Count; i++)
        {
            var pl = d.Parts[i];
            if (!Cat.Parts.TryGetValue(pl.Part, out var p)) continue;
            PartPalette.Draw(this, p, (x, y) => ToScreen(pl.X + x, pl.Y + y), s, pl.Rot);
            if (ShowArcs && p.Arc > 0 && (i == Selected || Selected < 0))
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
            if (i == Selected) DrawArc(ToScreen(pl.X, pl.Y), (float)p.Extent * s + 3, 0, Mathf.Tau, 32, new Color("7fe3ff"), 1.5f);
            if (!HullGeometry.Inside(Hull, pl.X, pl.Y, Math.Max(0.1, Hull.Width * 0.05))) DrawArc(ToScreen(pl.X, pl.Y), (float)p.Extent * s + 5, 0, Mathf.Tau, 32, UiTheme.Bad, 2);
        }
        if (EditOutline)
        {
            var pts = OutlinePoints();
            for (var k = 0; k < pts.Count; k++)
            {
                var sp = ToScreen(pts[k].X, pts[k].Y);
                DrawRect(new Rect2(sp - new Vector2(4, 4), new Vector2(8, 8)), k == _vertex ? UiTheme.Accent : Colors.White, false, 1.5f);
                DrawRect(new Rect2(new Vector2(2 * Centre.X - sp.X, sp.Y) - new Vector2(3, 3), new Vector2(6, 6)), new Color(1, 1, 1, 0.4f), false, 1);
            }
        }
        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(8, 16), $"{Frame?.Name}  ·  {Hull.Length:0.#} × {Hull.Width:0.#} m  ·  {(Frame?.Kind is "aircraft" or "munition" ? "nose" : "bow")} ↑", HorizontalAlignment.Left, -1, 12, UiTheme.Dim);
    }

    private int HitTest(Vector2 at)
    {
        var d = Design?.Invoke();
        if (d == null) return -1;
        for (var i = d.Parts.Count - 1; i >= 0; i--) // topmost first
            if (Cat.Parts.TryGetValue(d.Parts[i].Part, out var p) && ToScreen(d.Parts[i].X, d.Parts[i].Y).DistanceTo(at) <= Math.Max(6, p.Extent * Scale * 0.8)) return i;
        return -1;
    }

    /// <summary>Snap to the centreline and to a grid that suits the size (0.5 m ships, 0.1 m aircraft, 0.05 m missiles).</summary>
    private (double, double) Snap(double x, double y)
    {
        var g = Hull.Length > 60 ? 0.5 : Hull.Length > 8 ? 0.1 : 0.05;
        return (Math.Abs(x) < g * 1.2 ? 0 : Math.Round(x / g) * g, Math.Round(y / g) * g);
    }

    public override void _GuiInput(InputEvent e)
    {
        var d = Design?.Invoke();
        if (d == null) return;
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            GrabFocus();
            if (mb.ButtonIndex == MouseButton.WheelUp) { Zoom = Math.Min(6, Zoom * 1.15f); QueueRedraw(); }
            else if (mb.ButtonIndex == MouseButton.WheelDown) { Zoom = Math.Max(0.5f, Zoom / 1.15f); QueueRedraw(); }
            else if (mb.ButtonIndex == MouseButton.Left && EditOutline)
            {
                var pts = OutlinePoints();
                _vertex = -1;
                for (var k = 0; k < pts.Count; k++) if (ToScreen(pts[k].X, pts[k].Y).DistanceTo(mb.Position) < 8) _vertex = k;
                if (_vertex >= 0 && d.Outline.Count < 6) { d.Outline.Clear(); foreach (var (x, y) in pts) { d.Outline.Add(x); d.Outline.Add(y); } }
                _dragging = _vertex >= 0;
                QueueRedraw();
            }
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                Selected = HitTest(mb.Position);
                _dragging = Selected >= 0;
                if (_dragging) _grab = ToScreen(d.Parts[Selected].X, d.Parts[Selected].Y) - mb.Position;
                SelectionChanged?.Invoke(); QueueRedraw();
            }
            else if (mb.ButtonIndex == MouseButton.Right && HitTest(mb.Position) is var h && h >= 0) { Selected = h; Rotate(90); }
            AcceptEvent();
        }
        else if (e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } && _dragging) { _dragging = false; Changed?.Invoke(); }
        else if (e is InputEventMouseMotion mv && _dragging && EditOutline && _vertex >= 0)
        {
            var (x, y) = ToWorld(mv.Position);
            d.Outline[_vertex * 2] = Math.Max(0, Math.Round(x, 2)); // right half; the left mirrors it
            d.Outline[_vertex * 2 + 1] = Math.Round(y, 2);
            QueueRedraw();
        }
        else if (e is InputEventMouseMotion mm && _dragging && Selected >= 0)
        {
            var (x, y) = ToWorld(mm.Position + _grab);
            (x, y) = Snap(x, y);
            d.Parts[Selected] = d.Parts[Selected] with { X = x, Y = y };
            QueueRedraw();
        }
        else if (e is InputEventKey { Pressed: true } k && Selected >= 0)
        {
            if (k.Keycode is Key.Delete or Key.Backspace) Remove();
            else if (k.Keycode == Key.Q) Rotate(-15);
            else if (k.Keycode == Key.E) Rotate(15);
            else if (k.Keycode == Key.R) Rotate(90);
            AcceptEvent();
        }
    }

    public void Rotate(double deg)
    {
        var d = Design?.Invoke();
        if (d == null || Selected < 0) return;
        d.Parts[Selected] = d.Parts[Selected] with { Rot = ((d.Parts[Selected].Rot + deg) % 360 + 360) % 360 };
        Changed?.Invoke(); QueueRedraw();
    }

    public void Remove()
    {
        var d = Design?.Invoke();
        if (d == null || Selected < 0) return;
        d.Parts.RemoveAt(Selected);
        Selected = -1;
        SelectionChanged?.Invoke(); Changed?.Invoke(); QueueRedraw();
    }

    public void MirrorSelected()
    {
        var d = Design?.Invoke();
        if (d == null || Selected < 0) return;
        var p = d.Parts[Selected];
        d.Parts.Add(p with { X = -p.X, Rot = (360 - p.Rot) % 360 });
        Changed?.Invoke(); QueueRedraw();
    }

    public override bool _CanDropData(Vector2 at, Variant data) => data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey("part");

    public override void _DropData(Vector2 at, Variant data)
    {
        var d = Design?.Invoke();
        if (d == null) return;
        var id = data.AsGodotDictionary()["part"].AsString();
        var (x, y) = Snap(ToWorld(at).X, ToWorld(at).Y);
        d.Parts.Add(new Placement(id, x, y));
        if (Mirror && Math.Abs(x) > 1) d.Parts.Add(new Placement(id, -x, y));
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
        CustomMinimumSize = new Vector2(96, 92);
        TooltipText = $"{Part.Name}\n{Part.Category}{(Part.Topside ? " · topside" : "")}\n" +
                      string.Join(", ", Part.Uses.Select(u => $"{u.Key} {u.Value:0.#}")) +
                      (Part.Adds.Count > 0 ? "\n" + string.Join(", ", Part.Adds.Select(a => $"+{a.Value:0.#} {a.Key}")) : "") +
                      (Part.Component != null ? $"\nincludes: {Session.Content.Catalog.Components.GetValueOrDefault(Part.Component)?.Name}" : "") +
                      "\nDrag onto the blueprint.";
    }

    public override void _Draw()
    {
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
