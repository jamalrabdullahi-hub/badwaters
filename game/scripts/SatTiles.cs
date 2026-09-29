using System.Text.Json;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Detail satellite imagery: full-resolution Blue Marble (public domain) tiles in data/core/geo/sat, streamed in for the
/// visible area only when zoomed in (the whole-Earth satellite.jpg covers the zoomed-out view). A small cache and a few
/// tile loads per frame keep memory and stutter low.
/// </summary>
public partial class SatTiles : Node2D
{
    public const float FromZoom = 1.1f;
    private const int MaxCached = 48, LoadsPerFrame = 2;
    public Action? OnLoaded;
    private readonly Dictionary<(int, int), ImageTexture> _cache = new();
    private readonly LinkedList<(int, int)> _lru = new();
    private readonly string _dir = Path.Combine(Session.DataDir(), "geo", "sat");
    private int _cols, _rows, _tile, _w, _h;
    private float _yMax, _yMin;
    private bool _ok;

    public override void _Ready()
    {
        var f = Path.Combine(_dir, "tiles.json");
        if (!File.Exists(f)) return;
        using var d = JsonDocument.Parse(File.ReadAllText(f));
        var r = d.RootElement;
        (_cols, _rows, _tile, _w, _h) = (r.GetProperty("cols").GetInt32(), r.GetProperty("rows").GetInt32(), r.GetProperty("tile").GetInt32(), r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32());
        (_yMax, _yMin) = ((float)r.GetProperty("yMax").GetDouble(), (float)r.GetProperty("yMin").GetDouble());
        _ok = true;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
    }

    /// <summary>World rectangle (map pixels) of a tile: linear in longitude and in Mercator y, like the texture.</summary>
    private Rect2 TileRect(int c, int r)
    {
        var ppd = Session.PxPerDeg;
        float X(float px) => (px / _w * 360f - 180f) * ppd;
        float Y(float py) => -(_yMax - py / _h * (_yMax - _yMin)) * ppd;
        var x0 = X(c * _tile); var x1 = X(Math.Min(_w, (c + 1) * _tile));
        var y0 = Y(r * _tile); var y1 = Y(Math.Min(_h, (r + 1) * _tile));
        return new Rect2(x0, y0, x1 - x0, y1 - y0);
    }

    private IEnumerable<(int C, int R)> Wanted()
    {
        var vp = GetViewport();
        var cam = vp.GetCamera2D();
        if (cam == null) yield break;
        var half = vp.GetVisibleRect().Size / (2 * cam.Zoom.X);
        var view = new Rect2(cam.GetScreenCenterPosition() - half, half * 2);
        var ppd = Session.PxPerDeg;
        int Col(float x) => (int)Math.Floor(((x / ppd + 180f) / 360f) * _w / _tile);
        int Row(float y) => (int)Math.Floor(((_yMax + y / ppd) / (_yMax - _yMin)) * _h / _tile);
        for (var r = Math.Max(0, Row(view.Position.Y)); r <= Math.Min(_rows - 1, Row(view.End.Y)); r++)
            for (var c = Col(view.Position.X); c <= Col(view.End.X); c++)
                if (c >= 0 && c < _cols) yield return (c, r);
    }

    public override void _Process(double delta)
    {
        if (!_ok || !Visible) return;
        var loaded = 0;
        foreach (var k in Wanted())
        {
            if (_cache.ContainsKey(k)) continue;
            if (loaded++ >= LoadsPerFrame) break;
            var f = Path.Combine(_dir, $"{k.Item1}_{k.Item2}.jpg");
            if (!File.Exists(f)) { _cache[k] = null!; continue; }
            using var img = Image.LoadFromFile(f);
            img.GenerateMipmaps();
            _cache[k] = ImageTexture.CreateFromImage(img);
            _lru.AddFirst(k);
            while (_lru.Count > MaxCached) { var old = _lru.Last!.Value; _lru.RemoveLast(); _cache.Remove(old); }
        }
        if (loaded > 0) OnLoaded?.Invoke();
    }

    public override void _Draw()
    {
        if (!_ok) return;
        foreach (var k in Wanted())
            if (_cache.TryGetValue(k, out var t) && t != null)
                DrawTextureRect(t, TileRect(k.C, k.R), false, new Color(0.9f, 0.9f, 0.9f)); // same toning as the shader's sat_brightness
    }
}
