using System.Text.Json;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops unit symbols (Option B · silhouettes). The mapping is data (data/core/theatre/symbology.json); the icons are
/// white SVGs (data/core/theatre/symbols, built by tools/symbols-build.py) rasterised once and tinted to the side colour.
/// </summary>
public static class Symbols
{
    private sealed class Spec
    {
        public List<string> Rotate { get; set; } = new();
        public Dictionary<string, string> Presets { get; set; } = new();
        public Dictionary<string, string> Designs { get; set; } = new();
        public Dictionary<string, string> SiteTypes { get; set; } = new();
        public Dictionary<string, string> SiteClasses { get; set; } = new();
        public Dictionary<string, string> Kinds { get; set; } = new();
    }

    private const float Raster = 2f; // 56 px art rasterised at 112 px, drawn at ~22 px
    private static Spec? _spec;
    private static readonly Dictionary<string, Texture2D?> Cache = new();
    private static string Dir => Path.Combine(Session.DataDir(), "theatre");

    private static Spec S => _spec ??= Load();
    private static Spec Load()
    {
        var f = Path.Combine(Dir, "symbology.json");
        return File.Exists(f) ? JsonSerializer.Deserialize<Spec>(File.ReadAllText(f), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new() : new();
    }

    /// <summary>Icon code for a preset or design id, else the kind's fallback.</summary>
    public static string For(string? id, string kind) =>
        id != null && (S.Presets.TryGetValue(id, out var c) || S.Designs.TryGetValue(id, out c)) ? c : S.Kinds.GetValueOrDefault(kind, "FFG");

    public static string ForSite(string type, string cls) =>
        cls == "population-centre" ? "CITY" : S.SiteTypes.TryGetValue(type, out var c) || S.SiteClasses.TryGetValue(cls, out c) ? c : "C2";

    public static bool Rotates(string code) => S.Rotate.Contains(code);

    private static Texture2D? Tex(string code, bool hostile)
    {
        var key = hostile && code != "CITY" ? code + "_h" : code;
        if (Cache.TryGetValue(key, out var t)) return t;
        var f = Path.Combine(Dir, "symbols", key + ".svg");
        Texture2D? tex = null;
        if (File.Exists(f))
        {
            var img = new Image();
            if (img.LoadSvgFromString(File.ReadAllText(f), Raster) == Error.Ok) { img.GenerateMipmaps(); tex = ImageTexture.CreateFromImage(img); }
        }
        return Cache[key] = tex;
    }

    /// <summary>Draws a symbol centred on p, <paramref name="px"/> screen pixels across, nose along <paramref name="head"/> (map space).</summary>
    public static bool Draw(CanvasItem ci, string code, Vector2 p, Color c, bool hostile, float zoom, Vector2 head = default, float px = 22)
    {
        var tex = Tex(code, hostile);
        if (tex == null) return false;
        var rot = Rotates(code) && head != Vector2.Zero ? head.Angle() + Mathf.Pi / 2 : 0;
        var s = px / zoom / tex.GetWidth();
        ci.DrawSetTransform(p, rot, new Vector2(s, s));
        ci.DrawTexture(tex, -tex.GetSize() / 2, c);
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        return true;
    }
}
