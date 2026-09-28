using BadWaters.Data;
using BadWaters.Sim.Economy;
using Xunit;

namespace BadWaters.Sim.Tests;

public class EconomyGraphTests
{
    private static readonly GameContent C = GameContent.Load(Path.Combine(Root(), "data", "core"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d!.FullName, "BadWaters.sln"))) d = d.Parent;
        return d.FullName;
    }
    private static BillResolver R => new(C.Economy, C.Catalog);

    [Fact]
    public void Content_has_64_resources_and_no_cycles()
    {
        Assert.Equal(64, C.Economy.Resources.Count);
        Assert.Empty(R.Cycles());
        Assert.DoesNotContain(C.Errors, e => e.Contains("economy") || e.StartsWith("good ") || e.StartsWith("process ") || e.StartsWith("facility "));
    }

    [Fact]
    public void Silicon_ore_is_not_semiconductor_grade()
    {
        // quartz -> metallurgical silicon -> polysilicon -> wafers -> chips: five stages, three different facilities
        var t = R.Tree("chips", 10);
        var facilities = t.All().Select(n => n.Facility).Where(f => f != null).ToHashSet();
        Assert.Contains("semiconductor-fab", facilities);
        Assert.Contains("wafer-plant", facilities);
        Assert.Contains("polysilicon-plant", facilities);
        Assert.Contains(t.All(), n => n.Deposit == "silicon");
        Assert.True(t.Depth >= 5);
    }

    [Fact]
    public void Rare_earth_ore_needs_separation_before_it_is_a_magnet()
    {
        var t = R.Tree("magnets", 4);
        Assert.Contains(t.All(), n => n.Facility == "re-separation");
        Assert.Contains(t.All(), n => n.Deposit == "rare-earths");
    }

    [Fact]
    public void Aircraft_design_resolves_down_to_deposits_through_every_tier()
    {
        var d = C.Designs["bomber-lo"];
        var (goods, asm) = R.DesignBill(d);
        Assert.Contains("aerospace-assembly", asm.Keys);
        var req = R.Flatten(goods, asm);
        // aluminium from bauxite, titanium from ilmenite, radar/EW parts via gallium (a by-product of alumina), magnets via rare earths
        foreach (var res in new[] { "bauxite", "titanium", "rare-earths", "crude-oil" }) Assert.True(req.Resources.ContainsKey(res), res);
        foreach (var fac in new[] { "alumina-refinery", "aluminium-smelter", "titanium-plant", "carbon-fibre-plant", "machine-works" }) Assert.True(req.FacilityBatches.ContainsKey(fac), fac);
        Assert.Contains("machine-tools", req.Tooling);
        Assert.True(req.Energy > 0);
    }

    [Fact]
    public void Every_design_has_a_bill()
    {
        foreach (var d in C.Designs.Values)
        {
            var (goods, _) = R.DesignBill(d);
            Assert.True(goods.Count > 0, d.Id);
        }
    }
}
