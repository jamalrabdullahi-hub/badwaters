using BadWaters.Data;
using BadWaters.Sim.C2;
using BadWaters.Sim.Personnel;
using BadWaters.Sim.Time;
using Xunit;

namespace BadWaters.Sim.Tests;

public class ClockTests
{
    [Theory]
    [InlineData(TurnLength.Day, 2031, 8, 12)]
    [InlineData(TurnLength.Week, 2031, 8, 18)]
    [InlineData(TurnLength.Month, 2031, 9, 11)]
    [InlineData(TurnLength.Quarter, 2031, 11, 11)]
    [InlineData(TurnLength.HalfYear, 2032, 2, 11)]
    [InlineData(TurnLength.Year, 2032, 8, 11)]
    public void Strategic_turns_advance_by_the_chosen_length(TurnLength len, int y, int m, int d)
    {
        var clock = new GameClock(new DateTime(2031, 8, 11), new DateTime(2050, 12, 31));
        clock.Advance(len);
        Assert.Equal(new DateTime(y, m, d), clock.Now);
    }

    [Fact]
    public void Turns_stop_at_the_campaign_end()
    {
        var clock = new GameClock(new DateTime(2050, 10, 1), new DateTime(2050, 12, 31));
        clock.Advance(TurnLength.Year);
        Assert.True(clock.Finished);
    }

    [Fact]
    public void Theatre_speeds_match_the_design() => Assert.Equal(new[] { 0, 1, 2, 5, 10, 30, 50, 100 }, TheatreSpeeds.Allowed);
}

public class CommandNetworkTests
{
    private static CommandNetwork Sample()
    {
        var n = new CommandNetwork();
        n.Add(new CommandNode { Id = "NCA", Name = "National command", Kind = NodeKind.NationalCommand });
        n.Add(new CommandNode { Id = "JOC", Name = "Joint ops", Kind = NodeKind.Command });
        n.Add(new CommandNode { Id = "R1", Name = "Relay", Kind = NodeKind.Relay });
        n.Add(new CommandNode { Id = "FUS", Name = "Fusion centre", Kind = NodeKind.Fusion });
        n.Connect(new CommsLink { A = "NCA", B = "JOC", Kind = LinkKind.Fibre });
        n.Connect(new CommsLink { A = "JOC", B = "R1", Kind = LinkKind.Microwave });
        n.Connect(new CommsLink { A = "R1", B = "FUS", Kind = LinkKind.Microwave });
        return n;
    }

    [Fact]
    public void Healthy_network_is_not_defeated()
    {
        var n = Sample();
        Assert.Equal(3, n.EffectiveCommandNodes());
        Assert.False(n.IsDefeated);
    }

    [Fact]
    public void Cutting_a_link_isolates_the_nodes_behind_it()
    {
        var n = Sample();
        n.Links[1].State = LinkState.Cut;
        Assert.False(n.IsConnected("FUS"));
        Assert.Equal(2, n.EffectiveCommandNodes());
    }

    [Fact]
    public void Jammed_links_still_carry_traffic()
    {
        var n = Sample();
        n.Links[2].State = LinkState.Jammed;
        Assert.True(n.IsConnected("FUS"));
    }

    [Fact]
    public void Losing_national_command_loses_the_game()
    {
        var n = Sample();
        n.Node("NCA").Destroyed = true;
        Assert.True(n.IsDefeated);
    }
}

public class CareerTests
{
    [Fact]
    public void Cohorts_retire_when_their_career_length_runs_out()
    {
        var cats = new Dictionary<string, PersonnelCategory>
        {
            ["surface-warfare"] = new("surface-warfare", "naval", "Surface warfare", 12, 2),
            ["senior-officers"] = new("senior-officers", "command", "Senior officers", 45, 12),
        };
        var cohorts = new[] { new Cohort("surface-warfare", 2008, 100), new Cohort("surface-warfare", 2012, 80), new Cohort("senior-officers", 1990, 10) };
        var (remaining, retired) = Careers.Retire(cohorts, cats, 2020);
        Assert.Equal(100, retired);
        Assert.Equal(2, remaining.Count);
    }
}

public class DataTests
{
    private static string DataRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data", "core"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("data/core not found"), "data", "core");
    }

    [Fact]
    public void Core_data_loads_without_errors()
    {
        var loader = new ContentLoader(DataRoot());
        var nations = loader.Load<NationDef>("nations.json");
        var people = loader.Load<PersonnelDef>("personnel.json");
        var presets = loader.Load<PresetDef>("presets.json");
        Assert.Empty(loader.Errors);
        Assert.Equal(15, nations.Count);
        Assert.Equal(5, nations.Count(n => n.Tier == "great"));
        Assert.Equal("Soviet Union", nations.Single(n => n.Id == "RUS").DisplayName(1980));
        Assert.False(nations.Single(n => n.Id == "UKR").AvailableIn(1980));
        Assert.Equal(12, people.Single(p => p.Id == "surface-warfare").CareerYears);
        Assert.Equal(12, people.Single(p => p.Id == "sam-operators").CareerYears);
        Assert.Equal(45, people.Single(p => p.Id == "senior-officers").CareerYears);
        Assert.Contains(presets, p => p.Id == "USF35A" && p.Name == "F-35A Lightning II" && p.Ratings["signature"] > p.Ratings["speed"]);
    }

    [Fact]
    public void Mods_override_by_id_and_can_remove_records()
    {
        var mod = Directory.CreateTempSubdirectory("bw-mod-");
        File.WriteAllText(Path.Combine(mod.FullName, "presets.json"),
            """[ { "id": "USF35A", "name": "Custom name" }, { "id": "USF4E", "$remove": true } ]""");
        var loader = new ContentLoader(DataRoot(), new[] { mod.FullName });
        var presets = loader.Load<PresetDef>("presets.json");
        Assert.Empty(loader.Errors);
        Assert.Equal("Custom name", presets.Single(p => p.Id == "USF35A").Name);
        Assert.Equal("aircraft", presets.Single(p => p.Id == "USF35A").Designer);
        Assert.DoesNotContain(presets, p => p.Id == "USF4E");
    }
}
