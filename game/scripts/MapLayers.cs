using System.Text.Json;
using BadWaters.Sim.Environment;
using Godot;

namespace BadWaters.Game;

// Strategic Map layers. REAL EARTH (Natural Earth, public domain, built by tools/geo-build.py) is cartographic
// context only: relief, water, borders, major highways and rail, capitals and major cities, major civil ports and
// airports. No real power, energy, water, telecom, military or intelligence locations are imported. Cities are
// shown to be defended and are never targets (TargetPolicy). SIMULATED layers (trade routes, bases, theatres,
// weather) come from the game world, and fog of war applies only to them.

public sealed record MapLayerDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Group { get; init; } = "";
    /// <summary>shader | lines | points | grid | routes | military | theatres | weather | later</summary>
    public string Kind { get; init; } = "later";
    public string? File { get; init; }
    public string Color { get; init; } = "ffffff";
    public float Alpha { get; init; } = 1;
    public float MinZoom { get; init; }
    public int MaxRank { get; init; } = 9;
    public bool On { get; init; }
    public bool Sim { get; init; }

    public static List<MapLayerDef> Load() =>
        JsonSerializer.Deserialize<List<MapLayerDef>>(System.IO.File.ReadAllText(Path.Combine(Session.DataDir(), "map_layers.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
}

/// <summary>Real polylines (borders, roads, rail, rivers) as one hairline mesh per layer.</summary>
public partial class LineLayer : MeshInstance2D
{
    public float MinZoom;
    public LineLayer(MapLayerDef d)
    {
        MinZoom = d.MinZoom;
        Modulate = new Color(d.Color) { A = d.Alpha };
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(Session.DataDir(), "geo", d.File!)));
        var verts = new List<Vector2>();
        foreach (var ln in doc.RootElement.GetProperty("lines").EnumerateArray())
        {
            var v = ln.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            for (var i = 0; i + 3 < v.Length; i += 2)
            {
                if (Math.Abs(v[i] - v[i + 2]) > 180) continue; // don't draw across the date line
                verts.Add(Session.Project(v[i], v[i + 1])); verts.Add(Session.Project(v[i + 2], v[i + 3]));
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        var mesh = new ArrayMesh();
        if (verts.Count > 0) mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        Mesh = mesh;
    }
}

/// <summary>Real points (cities, ports, airports, sea names): culled to the view, labels only where they don't overlap.</summary>
public partial class PointLayer : Node2D
{
    private sealed record Pt(string Name, Vector2 P, string Kind, int Rank);
    private readonly List<Pt> _pts = new();
    private readonly MapLayerDef _d;
    private readonly Color _c;

    public PointLayer(MapLayerDef d)
    {
        _d = d; _c = new Color(d.Color);
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(Session.DataDir(), "geo", d.File!)));
        foreach (var p in doc.RootElement.GetProperty("points").EnumerateArray())
            _pts.Add(new Pt(p.GetProperty("n").GetString() ?? "", Session.Project(p.GetProperty("lon").GetDouble(), p.GetProperty("lat").GetDouble()), p.GetProperty("k").GetString() ?? "", p.GetProperty("r").GetInt32()));
        _pts.Sort((a, b) => (a.Kind == "capital" ? -1 : 0).CompareTo(b.Kind == "capital" ? -1 : 0) is var k && k != 0 ? k : a.Rank.CompareTo(b.Rank));
    }

    public override void _Draw()
    {
        var cam = GetViewport().GetCamera2D();
        var z = cam?.Zoom.X ?? 1;
        if (z < _d.MinZoom) return;
        var c = cam?.GetScreenCenterPosition() ?? Vector2.Zero;
        var half = GetViewportRect().Size / (2 * z);
        var view = new Rect2(c - half, half * 2);
        // more (smaller) places as you zoom in
        var maxRank = Math.Min(_d.MaxRank, z < 0.6f ? 1 : z < 1.2f ? 2 : z < 2.5f ? 3 : 9);
        var font = ThemeDB.FallbackFont;
        var fs = 11;
        var placed = new List<Rect2>();
        foreach (var p in _pts)
        {
            if (!view.HasPoint(p.P)) continue;
            if (p.Kind != "capital" && p.Rank > maxRank) continue;
            var s = 1 / z;
            switch (p.Kind)
            {
                case "capital": DrawRect(new Rect2(p.P - new Vector2(3.5f, 3.5f) * s, new Vector2(7, 7) * s), _c, false, 1.5f * s); DrawCircle(p.P, 1.6f * s, _c); break;
                case "city": DrawCircle(p.P, 2.4f * s, new Color(_c, 0.9f)); break;
                case "port": DrawColoredPolygon(new[] { p.P + new Vector2(0, -4) * s, p.P + new Vector2(4, 0) * s, p.P + new Vector2(0, 4) * s, p.P + new Vector2(-4, 0) * s }, new Color(_c, 0.85f)); break;
                case "airport": DrawLine(p.P - new Vector2(4, 0) * s, p.P + new Vector2(4, 0) * s, _c, 1.5f * s); DrawLine(p.P - new Vector2(0, 4) * s, p.P + new Vector2(0, 4) * s, _c, 1.5f * s); break;
            }
            // label in screen space, skipped if it would overlap one already placed
            var scr = (p.P - c) * z;
            var w = font.GetStringSize(p.Name, HorizontalAlignment.Left, -1, fs).X;
            var marine = _d.Id == "marine";
            var r = marine ? new Rect2(scr - new Vector2(w / 2, fs / 2f), new Vector2(w, fs)) : new Rect2(scr + new Vector2(6, -fs + 2), new Vector2(w, fs));
            if (placed.Any(q => q.Intersects(r))) continue;
            placed.Add(r);
            // draw text at screen scale so it stays crisp at any zoom
            DrawSetTransform(c + r.Position / z, 0, Vector2.One / z);
            DrawString(font, new Vector2(1, fs - 1), p.Name, HorizontalAlignment.Left, -1, fs, new Color(0, 0, 0, 0.7f));
            DrawString(font, new Vector2(0, fs - 2), p.Name, HorizontalAlignment.Left, -1, fs, marine ? new Color(_c, 0.8f) : _c);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }
}

/// <summary>Simulated: game bases and sites. Your side's are shown; the other side's only as unconfirmed contacts.</summary>
public partial class MilitaryLayer : Node2D
{
    public override void _Draw()
    {
        var z = GetViewport().GetCamera2D()?.Zoom.X ?? 1;
        var s = 1 / z; var font = ThemeDB.FallbackFont;
        foreach (var sc in Session.Content.Scenarios.Values.Where(x => x.Id != "intercept-trial"))
            foreach (var st in sc.Sites)
            {
                var p = Session.Project(st.Lon, st.Lat);
                var own = st.Side == "blue";
                var col = own ? new Color("4fb0e0") : new Color("ff4d5a");
                if (own) DrawRect(new Rect2(p - new Vector2(4, 4) * s, new Vector2(8, 8) * s), col, false, 1.5f * s);
                else DrawArc(p, 5 * s, 0, Mathf.Tau, 12, new Color(col, 0.8f), 1.5f * s);
                if (z < 1.2f) continue;
                // enemy sites: confidence from a fixed per-site number, never the real world
                var label = own ? st.Name : $"Unconfirmed · {20 + (Math.Abs(st.Id.GetHashCode()) % 45)}%";
                DrawString(font, p + new Vector2(7, 4) * s, label, HorizontalAlignment.Left, -1, (int)(10 / z), col);
            }
    }
}

/// <summary>Simulated: theatre centres from the scenarios, red when at war.</summary>
public partial class TheatreRings : Node2D
{
    public override void _Draw()
    {
        var z = GetViewport().GetCamera2D()?.Zoom.X ?? 1; var font = ThemeDB.FallbackFont;
        foreach (var sc in Session.Content.Scenarios.Values)
        {
            var war = Session.World.Theatres.TryGetValue(sc.Theatre, out var t) && t.AtWar;
            var col = war ? new Color("ff4d5a") : new Color("4fb0e0");
            var p = Session.Project(sc.Center[0], sc.Center[1]);
            DrawArc(p, 260, 0, Mathf.Tau, 64, new Color(col, 0.6f), 2 / z);
            DrawString(font, p + new Vector2(-60, -268), $"{sc.Name}{(war ? " · AT WAR" : "")}", HorizontalAlignment.Left, -1, (int)(12 / z), col);
        }
    }
}

/// <summary>Simulated: today's storm cells (see Weather). Redrawn when the date changes.</summary>
public partial class WeatherLayer : Node2D
{
    public override void _Draw()
    {
        foreach (var c in Weather.Cells(Session.World.Now))
        {
            var p = Session.Project(c.Lon, c.Lat);
            var r = (float)(c.RadiusDeg * Session.PxPerDeg);
            for (var k = 4; k >= 1; k--) DrawCircle(p, r * k / 4, new Color(0.85f, 0.9f, 1f, (float)(0.07 * c.Intensity)));
            if (c.Intensity > 0.7) DrawArc(p, r * 0.35f, 0, Mathf.Tau, 24, new Color(1f, 0.85f, 0.4f, 0.6f), 2);
        }
    }
}

/// <summary>Simulated: trade control points. Green = open (critical ones larger), red = shut. Click one to shut or reopen it.</summary>
public partial class ChokepointLayer : Node2D
{
    /// <summary>Minor passages (not critical) only show from this zoom.</summary>
    public const float MinorZoom = 0.8f;
    public static readonly Color OpenCol = new("5cf25c"), ShutCol = new("ff4d5a");

    public override void _Draw()
    {
        var z = GetViewport().GetCamera2D()?.Zoom.X ?? 1; var s = 1 / z; var font = ThemeDB.FallbackFont;
        foreach (var c in Session.Content.Chokepoints.Values)
        {
            if (!c.Critical && z < MinorZoom) continue;
            var p = Session.Project(c.Lon, c.Lat);
            var shut = Session.World.ClosedChokepoints.ContainsKey(c.Id);
            var r = (c.Critical ? 7f : 4.5f) * s;
            DrawCircle(p, r + 1.5f * s, new Color(0, 0, 0, 0.6f));
            DrawCircle(p, r, shut ? ShutCol : new Color(OpenCol, c.Critical ? 1 : 0.7f));
            if (shut) { DrawLine(p - new Vector2(r, r), p + new Vector2(r, r), Colors.White, 2 * s); DrawLine(p + new Vector2(-r, r), p + new Vector2(r, -r), Colors.White, 2 * s); }
            if (z < 1.1f && !shut) continue;
            DrawSetTransform(p + new Vector2(r + 3 * s, 4 * s), 0, Vector2.One / z);
            DrawString(font, new Vector2(1, 1), c.Name, HorizontalAlignment.Left, -1, 11, new Color(0, 0, 0, 0.8f));
            DrawString(font, Vector2.Zero, c.Name + (shut ? " · SHUT" : ""), HorizontalAlignment.Left, -1, 11, shut ? ShutCol : OpenCol);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }
}
