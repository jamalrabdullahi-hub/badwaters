using BadWaters.Sim.Trade;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// The Strategic Map: land, terrain shading, sector grid, theatres and trade corridors, all drawn from the
/// shared World. It lives inside a SubViewport so the shell UI stays independent of camera zoom.
/// </summary>
public partial class StrategicMap : Node2D
{
    public const int Satellite = 0, Topographic = 1, Tactical = 2;
    private readonly ShaderMaterial _land = new(), _sea = new();
    private Camera2D _cam = null!;
    private MeshInstance2D _coarse = null!, _fine = null!;
    /// <summary>Zoom level above which the detailed 50m coastline replaces the 110m one.</summary>
    public const float DetailZoom = 0.6f;
    private Node2D _routes = null!;
    private GridLayer _grid = null!;
    private bool _drag;
    public event Action<string?>? RouteHovered;

    public static readonly Color Open = new("5cf2b0"), Contested = new("ffb347"), Closed = new("ff4d5a");

    public override void _Ready()
    {
        var shader = GD.Load<Shader>("res://shaders/terrain.gdshader");
        _land.Shader = shader;
        _sea.Shader = shader;
        var noise = new NoiseTexture2D
        {
            Width = 512, Height = 512, Seamless = true, GenerateMipmaps = true, Normalize = true,
            Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, Frequency = 0.012f, FractalOctaves = 5, Seed = 7 },
        };
        _land.SetShaderParameter("noise_tex", noise);
        _sea.SetShaderParameter("noise_tex", noise);
        _land.SetShaderParameter("is_land", 1.0f);
        _sea.SetShaderParameter("is_land", 0.0f);

        var tl = Session.Project(-180, 84); var br = Session.Project(180, -80);
        AddChild(new Polygon2D { Polygon = new[] { tl, new Vector2(br.X, tl.Y), br, new Vector2(tl.X, br.Y) }, Material = _sea });
        _coarse = new MeshInstance2D { Mesh = Session.LandMeshes["110m"], Material = _land };
        _fine = new MeshInstance2D { Mesh = Session.LandMeshes["50m"], Material = _land };
        if (DevScreenshot.Arg("no-land") == null) { AddChild(_coarse); AddChild(_fine); }

        _grid = new GridLayer { Visible = DevScreenshot.Arg("no-grid") == null };
        AddChild(_grid);
        _routes = new Node2D();
        AddChild(_routes);
        RefreshRoutes();

        _cam = new Camera2D { Position = Session.Project(125, 28), Zoom = new Vector2(0.9f, 0.9f) };
        AddChild(_cam);
        _cam.MakeCurrent();
        SetMode(Satellite);
        UpdateLod();
    }

    private void UpdateLod()
    {
        var fine = _cam.Zoom.X >= DetailZoom;
        _fine.Visible = fine;
        _coarse.Visible = !fine;
        _grid.QueueRedraw();
    }

    public void SetMode(int mode)
    {
        _land.SetShaderParameter("mode", mode);
        _sea.SetShaderParameter("mode", mode);
        _grid.Dark = mode != Topographic;
        _grid.QueueRedraw();
    }

    public static Color StatusColor(RouteStatus s) => s switch { RouteStatus.FullyOpen => Open, RouteStatus.Contested => Contested, _ => Closed };

    public void RefreshRoutes()
    {
        foreach (var c in _routes.GetChildren()) c.QueueFree();
        foreach (var r in Session.Content.Routes.Values)
        {
            var st = Session.World.Routes[r.Id];
            var pts = new List<Vector2>();
            double prevLon = double.NaN;
            foreach (var p in r.Path)
            {
                var lon = p[0];
                if (!double.IsNaN(prevLon)) { while (lon - prevLon > 180) lon -= 360; while (prevLon - lon > 180) lon += 360; }
                prevLon = lon;
                pts.Add(Session.Project(lon, p[1]));
            }
            var line = new Line2D { Points = pts.ToArray(), Width = 5, DefaultColor = StatusColor(st.Status), JointMode = Line2D.LineJointMode.Round, Name = r.Id };
            if (st.Status != RouteStatus.FullyOpen) line.Width = 7;
            line.SetMeta("route", r.Id);
            _routes.AddChild(line);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown)
            {
                var before = GetGlobalMousePosition();
                var f = mb.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1 / 1.15f;
                _cam.Zoom = (_cam.Zoom * f).Clamp(new Vector2(0.15f, 0.15f), new Vector2(12, 12));
                _cam.Position += before - GetGlobalMousePosition();
                UpdateLod();
            }
            else if (mb.ButtonIndex == MouseButton.Left) _drag = mb.Pressed;
        }
        else if (e is InputEventMouseMotion mm)
        {
            if (_drag) { _cam.Position -= mm.Relative / _cam.Zoom; _grid.QueueRedraw(); }
            if (_routes.Visible) RouteHovered?.Invoke(HitRoute(GetGlobalMousePosition()));
        }
    }

    private string? HitRoute(Vector2 p)
    {
        var tol = 8 / _cam.Zoom.X;
        foreach (var n in _routes.GetChildren())
            if (n is Line2D l)
                for (var i = 0; i + 1 < l.Points.Length; i++)
                    if (Geometry2D.GetClosestPointToSegment(p, l.Points[i], l.Points[i + 1]).DistanceTo(p) < tol) return (string)l.GetMeta("route");
        return null;
    }

    public void AddOverlay(Node2D n) { if (n.GetParent() == null) AddChild(n); }
    public void ShowRoutes(bool on) { _routes.Visible = on; if (!on) RouteHovered?.Invoke(null); }

    /// <summary>Move the view by screen pixels (e.g. so a point sits in the middle of the uncovered map area).</summary>
    public void PanScreen(Vector2 px) { _cam.Position += px / _cam.Zoom; _grid.QueueRedraw(); }

    public void CenterOn(double lon, double lat, float zoom) { _cam.Position = Session.Project(lon, lat); _cam.Zoom = new Vector2(zoom, zoom); UpdateLod(); }
}

/// <summary>BMS-style sector grid: thin 1° lines when zoomed in, bold 5° lines, sector labels.</summary>
public partial class GridLayer : Node2D
{
    public bool Dark = true;
    public override void _Draw()
    {
        var cam = GetViewport().GetCamera2D();
        var zoom = cam?.Zoom.X ?? 1;
        var line = Dark ? new Color(0.5f, 0.85f, 1f, 0.10f) : new Color(0.15f, 0.15f, 0.45f, 0.45f);
        var bold = Dark ? new Color(0.5f, 0.85f, 1f, 0.22f) : new Color(0.15f, 0.15f, 0.45f, 0.75f);
        var label = Dark ? new Color(0.5f, 0.85f, 1f, 0.5f) : new Color(0.15f, 0.15f, 0.55f, 0.85f);
        var step = zoom > 1.2f ? 1 : 5;
        // only lines inside the visible area
        var half = GetViewportRect().Size / (2 * zoom);
        var c = cam?.GetScreenCenterPosition() ?? Vector2.Zero;
        float lonMin = (c.X - half.X) / Session.PxPerDeg - step, lonMax = (c.X + half.X) / Session.PxPerDeg + step;
        var top = c.Y - half.Y; var bottom = c.Y + half.Y;
        for (var lon = -180; lon <= 180; lon += step)
        {
            if (lon < lonMin || lon > lonMax) continue;
            DrawLine(new Vector2(lon * Session.PxPerDeg, top), new Vector2(lon * Session.PxPerDeg, bottom), lon % 5 == 0 ? bold : line, (lon % 5 == 0 ? 1.4f : 0.8f) / zoom);
        }
        for (var lat = -80; lat <= 80; lat += step)
        {
            var y = Session.Project(0, lat).Y;
            if (y < top - 200 || y > bottom + 200) continue;
            DrawLine(new Vector2(c.X - half.X, y), new Vector2(c.X + half.X, y), lat % 5 == 0 ? bold : line, (lat % 5 == 0 ? 1.4f : 0.8f) / zoom);
        }
        if (zoom < 0.8f) return; // sector labels only when zoomed in (keeps draw calls low)
        var font = ThemeDB.FallbackFont;
        var size = (int)(14 / zoom);
        for (var lon = -180; lon < 180; lon += 5)
            for (var lat = -75; lat < 80; lat += 5)
            {
                var p = Session.Project(lon + 2.5, lat + 2.5);
                if (Mathf.Abs(p.X - c.X) > half.X + 100 || Mathf.Abs(p.Y - c.Y) > half.Y + 100) continue;
                var code = $"{(char)('A' + ((lon + 180) / 5) % 26)}{(lat + 90) / 5}";
                DrawString(font, p - new Vector2(size, -size / 3f), code, HorizontalAlignment.Left, -1, size, label);
            }
    }
}
