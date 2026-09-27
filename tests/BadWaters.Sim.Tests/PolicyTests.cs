using BadWaters.Sim.Policy;
using Xunit;

namespace BadWaters.Sim.Tests;

public class PolicyTests
{
    [Theory]
    [InlineData("population-centre")]
    [InlineData("civil-shelter")]
    public void Population_is_protected(string cls) => Assert.False(TargetPolicy.IsLegitimate(cls));

    [Theory]
    [InlineData("city")]
    [InlineData("")]
    [InlineData("unclassified-site")]
    public void Unknown_classes_are_refused(string cls) => Assert.False(TargetPolicy.IsLegitimate(cls));

    [Theory]
    [InlineData("military-force")]
    [InlineData("military-base")]
    [InlineData("command-military")]
    [InlineData("industrial")]
    [InlineData("logistics")]
    public void Military_and_war_sustaining_targets_are_allowed(string cls) => Assert.True(TargetPolicy.IsLegitimate(cls));

    [Fact]
    public void Nuclear_weapons_stay_abstract()
    {
        Assert.False(NuclearPolicy.TargetingGameplay);
        Assert.Equal("campaign-ends", NuclearPolicy.Outcome);
    }
}
