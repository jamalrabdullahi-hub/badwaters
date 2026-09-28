using BadWaters.Sim.Trade;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// The Strategic Map: land, terrain shading, sector grid, theatres and trade corridors, all drawn from the
/// shared World. The static base is cached in an off-screen buffer and only re-rendered when the view changes.
/// </summary>
public partial class StrategicMap : Node2D
{
    public const int Satellite = 0, Topographic = 1, Tactical = 2;
    private readonly ShaderMaterial _land = new(), _sea = new();
    private Camera2D _cam = null!;
    private MeshInstance2D _coarse = null!, _fine = null!;
    /// <summary>Zoom level above which the detailed 50m coastline replaces the 110m one.</summary>
    public const float DetailZoom = 0.6f;
    private Node2D _routes = null!, _landRoutes = null!;
    private ChokepointLayer _chokes = null!;
    public event Action<string?>? ChokepointHovered;
    public event Action<string>? ChokepointClicked;
    private Vector2 _pressAt;
    /// <summary>Land corridors when open (brown); disrupted ones take the status colour like sea lanes.</summary>
    public static readonly Color LandOpen = new("a0703f");
    private GridLayer _grid = null!;
    private bool _drag;
    // The static base (sea, land, grid, real-Earth lines and labels) renders into an off-screen buffer only when
    // the view or a layer changes; idle frames just show the cached image. Game overlays stay live on top.
    private SubViewport _baseVp = null!;
    private Camera2D _baseCam = null!;
    private Node2D _base = null!;
    public event Action<string?>? RouteHovered;

    public static readonly Color Open = new("5cf2b0"), Contested = new("ffb347"), Closed = new("ff4d5a");

    public override void _Ready()
    {
        var shader = GD.Load<Shader>("res://shaders/terrain.gdshader");
        _land.Shader = shader;
        _sea.Shader = shader;
        // real Earth: relief, ocean depth, lakes and urban areas (Natural Earth, tools/geo-build.py)
        var earth = LoadEarth("earth.png");
        var sat = LoadEarth("satellite.jpg");
        foreach (var m in new[] { _land, _sea }) { m.SetShaderParameter("earth_tex", earth); m.SetShaderParameter("sat_tex", sat); m.SetShaderParameter("px_per_deg", Session.PxPerDeg); }
        _land.SetShaderParameter("is_land", 1.0f);
        _sea.SetShaderParameter("is_land", 0.0f);

        _baseVp = new SubViewport { Disable3D = true, TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Once, Size = (Vector2I)GetViewportRect().Size };
        AddChild(_baseVp);
        _base = new Node2D();
        _baseVp.AddChild(_base);
        _baseCam = new Camera2D();
        _baseVp.AddChild(_baseCam);
        var under = new CanvasLayer { Layer = -1 };
        AddChild(under);
        under.AddChild(new TextureRect { Texture = _baseVp.GetTexture(), AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore });
        GetViewport().SizeChanged += () => { _baseVp.Size = (Vector2I)GetViewportRect().Size; Redraw(); };

        // plain deep-ocean backdrop beyond the date line (the textured sea covers -180..180)
        var bt = Session.Project(-540, 84); var bb = Session.Project(540, -80);
        _base.AddChild(new Polygon2D { Polygon = new[] { bt, new Vector2(bb.X, bt.Y), bb, new Vector2(bt.X, bb.Y) }, Color = new Color(0.006f, 0.022f, 0.055f) });
        var tl = Session.Project(-180, 84); var br = Session.Project(180, -80);
        _base.AddChild(new Polygon2D { Polygon = new[] { tl, new Vector2(br.X, tl.Y), br, new Vector2(tl.X, br.Y) }, Material = _sea });
        _coarse = new MeshInstance2D { Mesh = Session.LandMeshes["110m"], Material = _land };
        _fine = new MeshInstance2D { Mesh = Session.LandMeshes["50m"], Material = _land };
        if (DevScreenshot.Arg("no-land") == null) { _base.AddChild(_coarse); _base.AddChild(_fine); }

        _grid = new GridLayer { Visible = DevScreenshot.Arg("no-grid") == null };
        _base.AddChild(_grid);
        foreach (var d in MapLayerDef.Load())
        {
            _defs[d.Id] = d;
            Node2D? n = d.Kind switch
            {
                "lines" => new LineLayer(d),
                "points" => new PointLayer(d),
                "military" => new MilitaryLayer(),
                "theatres" => new TheatreRings(),
                "weather" => new WeatherLayer(),
                _ => null,
            };
            if (n != null) { _layers[d.Id] = n; if (n is LineLayer or PointLayer) _base.AddChild(n); else AddChild(n); }
        }
        _landRoutes = new Node2D();
        AddChild(_landRoutes);
        _routes = new Node2D();
        AddChild(_routes);
        _chokes = new ChokepointLayer();
        AddChild(_chokes);
        RefreshRoutes();
        foreach (var d in _defs.Values) SetLayer(d.Id, d.On);
        if (DevScreenshot.Arg("layers") is { } extra) foreach (var id in extra.Split(',')) SetLayer(id.TrimStart('-'), !id.StartsWith('-'));

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
        Redraw();
    }

    private readonly Dictionary<string, MapLayerDef> _defs = new();
    private readonly Dictionary<string, Node2D> _layers = new();
    private readonly HashSet<string> _on = new();
    public IReadOnlyCollection<MapLayerDef> LayerDefs => _defs.Values;
    public bool LayerOn(string id) => _on.Contains(id);

    /// <summary>Turn a map layer on or off (see data/core/map_layers.json).</summary>
    public void SetLayer(string id, bool on)
    {
        if (!_defs.TryGetValue(id, out var d) || d.Kind == "later") return;
        if (on) _on.Add(id); else _on.Remove(id);
        if (d.Kind == "shader") foreach (var m in new[] { _land, _sea }) m.SetShaderParameter("show_" + (id == "lakes" ? "lakes" : id), on);
        if (id == "lakes") foreach (var m in new[] { _land, _sea }) m.SetShaderParameter("show_lakes", on);
        if (d.Kind == "grid") _grid.Visible = on;
        if (d.Kind == "routes") _routes.Visible = on;
        if (d.Kind == "landroutes") _landRoutes.Visible = on;
        if (d.Kind == "chokepoints") _chokes.Visible = on;
        Redraw();
    }

    /// <summary>Redraw the view-dependent layers (after a pan, zoom or date change).</summary>
    public void Redraw()
    {
        _grid.QueueRedraw();
        var z = _cam?.Zoom.X ?? 1;
        foreach (var (id, n) in _layers)
        {
            n.Visible = _on.Contains(id) && (n is not LineLayer l || z >= l.MinZoom);
            if (n.Visible && n is not LineLayer) n.QueueRedraw();
        }
        ScaleRouteWidths();
        _chokes?.QueueRedraw();
        if (_cam == null || _baseVp == null) return;
        _baseCam.Position = _cam.Position;
        _baseCam.Zoom = _cam.Zoom;
        _baseVp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    /// <summary>Load a map image to the GPU (mipmapped, S3TC-compressed) and free the CPU copy straight away.</summary>
    private static ImageTexture LoadEarth(string file)
    {
        using var img = Image.LoadFromFile(Path.Combine(Session.DataDir(), "geo", file));
        img.GenerateMipmaps();
        // block-compressed in VRAM: a quarter of the memory traffic per lookup (matters on integrated GPUs)
        img.Compress(Image.CompressMode.S3Tc, Image.CompressSource.Generic);
        return ImageTexture.CreateFromImage(img);
    }

    public void SetMode(int mode)
    {
        _land.SetShaderParameter("mode", mode);
        _sea.SetShaderParameter("mode", mode);
        _grid.Dark = mode != Topographic;
        Redraw();
    }

    public static Color StatusColor(RouteStatus s) => s switch { RouteStatus.FullyOpen => Open, RouteStatus.Contested => Contested, _ => Closed };

    public void RefreshRoutes()
    {
        foreach (var c in _routes.GetChildren().Concat(_landRoutes.GetChildren())) c.QueueFree();
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
            var land = r.Mode == "land";
            var open = st.Status == RouteStatus.FullyOpen;
            var line = new Line2D { Points = pts.ToArray(), DefaultColor = land && open ? LandOpen : StatusColor(st.Status), JointMode = Line2D.LineJointMode.Round, Name = r.Id };
            line.SetMeta("route", r.Id);
            line.SetMeta("px", (land ? 2.5f : 3f) + (open ? 0 : 1.5f)); // screen pixels at any zoom (see Redraw)
            (land ? _landRoutes : _routes).AddChild(line);
        }
        _chokes?.QueueRedraw();
        ScaleRouteWidths();
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
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                _drag = mb.Pressed;
                if (mb.Pressed) _pressAt = mb.Position;
                // a click (not a drag) on a control point selects it
                else if (mb.Position.DistanceTo(_pressAt) < 4 && _chokes.Visible && HitChokepoint(GetGlobalMousePosition()) is { } cp) ChokepointClicked?.Invoke(cp);
            }
        }
        else if (e is InputEventMouseMotion mm)
        {
            if (_drag) { _cam.Position -= mm.Relative / _cam.Zoom; Redraw(); }
            var at = GetGlobalMousePosition();
            var cp = _chokes.Visible ? HitChokepoint(at) : null;
            if (cp != null) ChokepointHovered?.Invoke(cp);
            else if (_routes.Visible || _landRoutes.Visible) RouteHovered?.Invoke(HitRoute(at));
        }
    }

    private string? HitRoute(Vector2 p)
    {
        var tol = 8 / _cam.Zoom.X;
        foreach (var n in (_routes.Visible ? _routes.GetChildren() : new()).Concat(_landRoutes.Visible ? _landRoutes.GetChildren() : new()))
            if (n is Line2D l)
                for (var i = 0; i + 1 < l.Points.Length; i++)
                    if (Geometry2D.GetClosestPointToSegment(p, l.Points[i], l.Points[i + 1]).DistanceTo(p) < tol) return (string)l.GetMeta("route");
        return null;
    }

    /// <summary>Route lines keep a constant on-screen width, so they read at world zoom and don't bloat close up.</summary>
    private void ScaleRouteWidths()
    {
        if (_routes == null) return;
        var z = _cam?.Zoom.X ?? 1;
        foreach (var n in _routes.GetChildren().Concat(_landRoutes.GetChildren()))
            if (n is Line2D l && l.HasMeta("px")) l.Width = (float)l.GetMeta("px") / z;
    }

    private string? HitChokepoint(Vector2 p)
    {
        var tol = 10 / _cam.Zoom.X;
        return Session.Content.Chokepoints.Values.Where(c => c.Critical || _cam.Zoom.X >= ChokepointLayer.MinorZoom)
            .Select(c => (c.Id, d: Session.Project(c.Lon, c.Lat).DistanceTo(p))).Where(x => x.d < tol).OrderBy(x => x.d).Select(x => x.Id).FirstOrDefault();
    }

    public void AddOverlay(Node2D n) { if (n.GetParent() == null) AddChild(n); }
    public void ShowRoutes(bool on)
    {
        _routes.Visible = on && _on.Contains("routes");
        _landRoutes.Visible = on && _on.Contains("landroutes");
        _chokes.Visible = on && _on.Contains("chokepoints");
        if (!on) RouteHovered?.Invoke(null);
    }

    /// <summary>Move the view by screen pixels (e.g. so a point sits in the middle of the uncovered map area).</summary>
    public void PanScreen(Vector2 px) { _cam.Position += px / _cam.Zoom; Redraw(); }

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
