using System.Text.Json;
using BadWaters.Data;
using BadWaters.Sim;
using BadWaters.Sim.Trade;
using BadWaters.Sim.Engineering;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>The running campaign: loaded content plus the one shared World that every screen views.</summary>
public static class Session
{
    public static GameContent Content { get; private set; } = null!;
    public static World World { get; private set; } = null!;
    public static Scheduler Scheduler { get; private set; } = null!;
    public static int StartYear { get; private set; }
    /// <summary>The national production economy (every nation), run on each strategic month.</summary>
    public static BadWaters.Sim.Economy.EconomyWorld Econ { get; private set; } = null!;
    public static List<string> EconomyEvents { get; } = new();
    /// <summary>Persistent theatre wars: auto-resolved or fought each strategic day, written back into the economy.</summary>
    public static BadWaters.Sim.Command.TheatreCampaign? Campaign { get; private set; }
    /// <summary>Extra daily events (dev scenarios); the campaign runs first.</summary>
    public static Func<World, DateTime, BadWaters.Sim.Interrupt?>? ExtraDaily { get; set; }

    /// <summary>A live theatre with both commanders; its strikes write back into the strategic economy.</summary>
    public static BadWaters.Sim.Theatre.InterceptTrial BuildTheatre(BadWaters.Sim.Theatre.ScenarioDef sc, int seed)
    {
        var t = BadWaters.Sim.Command.TheatreFactory.Create(sc, Profile, Content.PresetInfo, Content.BaseTypes,
            side => Content.DoctrineFor(NationId(sc.Sides.GetValueOrDefault(side, ""))), seed, stores: Content.Stores);
        // a campaign theatre that isn't at war shows the other side as "hostile, not at war": track only
        if (sc.Theatre != "" && World.Theatres.TryGetValue(sc.Theatre, out var ts) && !ts.AtWar && DevScreenshot.Arg("war") == null) t.AtWar = false;
        t.SiteDamaged += (site, frac) =>
        {
            var nation = NationId(t.Scenario.Sides.GetValueOrDefault(site.Side, ""));
            if (nation == null) return;
            var hit = World.Economy.DamageNear(nation, site.Lon, site.Lat, site.Class, frac, Content.Economy);
            if (hit != null) t.Report("industry", $"Strategic effect: {hit} damaged {frac:P0}.");
        };
        return t;
    }
    /// <summary>Nation id from a scenario side name ("China" → "CHN").</summary>
    public static string? NationId(string sideName) =>
        Content.Nations.ContainsKey(sideName) ? sideName : Content.Nations.Values.FirstOrDefault(n => n.Name == sideName)?.Id;

    public static string DataDir()
    {
        var project = ProjectSettings.GlobalizePath("res://");
        var dir = Path.GetFullPath(Path.Combine(project, "..", "data", "core"));
        return Directory.Exists(dir) ? dir : Path.Combine(project, "data", "core");
    }

    public static IEnumerable<string>? ModDirs()
    {
        var mods = Path.GetFullPath(Path.Combine(DataDir(), "..", "..", "mods"));
        return Directory.Exists(mods) ? Directory.GetDirectories(mods) : null;
    }

    public static void Start(string nation, int year)
    {
        Content = GameContent.Load(DataDir(), ModDirs());
        foreach (var e in Content.Errors) GD.PushError(e);
        StartYear = year;
        World = new World { Now = new DateTime(year, 1, 1), End = new DateTime(2050, 12, 31), Player = nation };
        foreach (var t in Content.Theatres.Keys) World.Theatres[t] = new TheatreState { Id = t };
        foreach (var r in Content.Routes.Keys) World.Routes[r] = new RouteState();
        Scheduler = new Scheduler(World);
        foreach (var r in Content.Routes.Values) { Scheduler.RouteTheatres[r.Id] = r.Theatres.ToHashSet(); Scheduler.RoutePaths[r.Id] = r.Path; Scheduler.RouteDefs[r.Id] = r; }
        LoadLand();
        Econ = new BadWaters.Sim.Economy.EconomyWorld(Content.Economy, Content.Geo, Content.Catalog, Content.Designs, Content.Routes);
        Econ.Start(World.Economy, Content.Nations.Keys, year, Content.TradeRegimes);
        // every other nation is run by its strategic AI; the player can delegate their own too
        Econ.Strategy = new BadWaters.Sim.Economy.StrategicCommander(Econ, Content.DispositionFor);
        foreach (var n in World.Economy.Nations.Keys.Where(n => n != nation)) Econ.AiNations.Add(n);
        Econ.AtWar = n => World.Theatres.Values.Any(t => t.AtWar && Content.Scenarios.Values.Any(sc => sc.Theatre == t.Id && sc.Sides.Values.Any(v => NationId(v) == n)));
        Campaign = new BadWaters.Sim.Command.TheatreCampaign(th => Content.Scenarios.Values.FirstOrDefault(x => x.Theatre == th), BuildTheatre, NationId);
        Scheduler.DailyHook = (w, now) =>
        {
            var extra = ExtraDaily?.Invoke(w, now);
            var wars = w.Theatres.Values.Where(t => t.AtWar).Select(t => t.Id).ToList();
            var waiting = Campaign.Daily(w, wars);
            foreach (var r in Campaign.Reports) w.Log.Add(r);
            Campaign.Reports.Clear();
            if (waiting != null) return new BadWaters.Sim.Interrupt(now, "theatre-fight", $"The war in {waiting} is waiting for you: fight it in Theatre Ops or auto-resolve.", waiting);
            return extra;
        };
        Scheduler.MonthlyHook = w =>
        {
            foreach (var e in Econ.RunMonth(w.Economy, w.Routes)) { EconomyEvents.Add(e); w.Log.Add($"{w.Now:yyyy-MM-dd} {e}"); }
        };
    }

    private static void LoadLand()
    {
        if (LandMeshes.Count > 0) return;
        foreach (var res in new[] { "110m", "50m" })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(DataDir(), "geo", $"land-{res}.json")));
            // pre-triangulated land (earcut, tools/geo-convert.mjs) as one mesh in map pixels
            var verts = new List<Vector2>();
            var idx = new List<int>();
            foreach (var poly in doc.RootElement.GetProperty("polys").EnumerateArray())
            {
                var baseIndex = verts.Count;
                var v = poly.GetProperty("v").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                for (var i = 0; i < v.Length; i += 2) verts.Add(Project(v[i], v[i + 1])); // date-line polygons are pre-shifted by geo-convert.mjs
                foreach (var t in poly.GetProperty("t").EnumerateArray()) idx.Add(baseIndex + t.GetInt32());
            }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            LandMeshes[res] = mesh;
        }
    }

    public static SystemProfile Profile(string designId) => SystemProfile.Build(Content.Catalog, Content.Designs[designId]);

    /// <summary>Player designs live in a mod folder, so they layer over core content like any other mod.</summary>
    public static string PlayerModDir() => Path.GetFullPath(Path.Combine(DataDir(), "..", "..", "mods", "player"));

    public static void SaveDesign(DesignDef d)
    {
        Content.Designs[d.Id] = d;
        Directory.CreateDirectory(PlayerModDir());
        var mine = Content.Designs.Values.Where(x => x.Id.StartsWith("player-")).ToList();
        File.WriteAllText(Path.Combine(PlayerModDir(), "designs.json"),
            JsonSerializer.Serialize(mine, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    /// <summary>A component from the component designer, plus its drawn part; both go to mods/player.</summary>
    public static void SaveComponent(ComponentTemplate c, VisualPart part)
    {
        Content.Catalog.Components[c.Id] = c;
        Content.Catalog.Parts[part.Id] = part;
        Directory.CreateDirectory(PlayerModDir());
        var opts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(Path.Combine(PlayerModDir(), "components.json"), JsonSerializer.Serialize(Content.Catalog.Components.Values.Where(x => x.Id.StartsWith("player-")).ToList(), opts));
        File.WriteAllText(Path.Combine(PlayerModDir(), "parts.json"), JsonSerializer.Serialize(Content.Catalog.Parts.Values.Where(x => x.Id.StartsWith("part-player-")).ToList(), opts));
    }

    /// <summary>Land meshes by resolution: "110m" for zoomed-out views, "50m" for detail.</summary>
    public static Dictionary<string, ArrayMesh> LandMeshes { get; } = new();

    // ---- Mercator projection in map pixels
    public const float PxPerDeg = 20f;
    /// <summary>Map pixels back to longitude/latitude (inverse of Project).</summary>
    public static (double Lon, double Lat) Unproject(Vector2 p)
    {
        var y = -p.Y / PxPerDeg * Math.PI / 180;
        return (p.X / PxPerDeg, (2 * Math.Atan(Math.Exp(y)) - Math.PI / 2) * 180 / Math.PI);
    }

    public static Vector2 Project(double lon, double lat)
    {
        lat = Math.Clamp(lat, -80, 84);
        var y = Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360)) * 180 / Math.PI;
        return new Vector2((float)(lon * PxPerDeg), (float)(-y * PxPerDeg));
    }
}
