using System.Text.Json;
using BadWaters.Sim.Environment;
using Xunit;

namespace BadWaters.Sim.Tests;

public class MapTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }

    [Fact]
    public void Every_map_layer_file_exists()
    {
        var core = Path.Combine(Root(), "data", "core");
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(core, "map_layers.json")));
        foreach (var l in doc.RootElement.EnumerateArray())
            if (l.TryGetProperty("file", out var f)) Assert.True(File.Exists(Path.Combine(core, "geo", f.GetString()!)), f.GetString());
        Assert.True(File.Exists(Path.Combine(core, "geo", "earth.png")));
    }

    [Fact]
    public void No_military_airports_in_the_real_map()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "data", "core", "geo", "airports.json"));
        Assert.DoesNotContain("military", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Weather_is_deterministic_and_bounded()
    {
        var t = new DateTime(2020, 8, 20);
        Assert.Equal(Weather.Cells(t).Count(), Weather.Cells(t).Count());
        var s = Weather.StormAt(135, 18, t);
        Assert.InRange(s, 0, 1);
        Assert.Equal(s, Weather.StormAt(135, 18, t));
        Assert.Contains(Weather.Cells(t), c => c.Intensity > 0.3);
    }
}
