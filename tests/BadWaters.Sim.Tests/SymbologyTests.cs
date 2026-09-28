using System.Text.Json;
using Xunit;

namespace BadWaters.Sim.Tests;

public class SymbologyTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static readonly string Dir = Path.Combine(Root(), "data", "core", "theatre");
    private static JsonElement Spec => JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "symbology.json"))).RootElement;

    private static IEnumerable<string> Codes(string section) => Spec.GetProperty(section).EnumerateObject().Select(p => p.Value.GetString()!);

    [Fact]
    public void Every_mapped_code_has_own_and_hostile_icons()
    {
        foreach (var code in new[] { "presets", "designs", "siteTypes", "siteClasses", "kinds" }.SelectMany(Codes).Distinct())
        {
            Assert.True(File.Exists(Path.Combine(Dir, "symbols", code + ".svg")), code);
            Assert.True(File.Exists(Path.Combine(Dir, "symbols", code + "_h.svg")), code + "_h");
        }
    }

    [Fact]
    public void Every_preset_has_a_symbol()
    {
        var mapped = Spec.GetProperty("presets").EnumerateObject().Select(p => p.Name).ToHashSet();
        var presets = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "data", "core", "presets.json"))).RootElement.EnumerateArray().Select(p => p.GetProperty("id").GetString()!);
        Assert.Empty(presets.Where(p => !mapped.Contains(p)));
    }

    [Fact]
    public void Population_centres_only_get_the_protected_symbol()
    {
        Assert.Equal("CITY", Spec.GetProperty("siteClasses").GetProperty("population-centre").GetString());
        Assert.DoesNotContain("CITY", new[] { "presets", "designs", "siteTypes", "kinds" }.SelectMany(Codes));
    }
}
