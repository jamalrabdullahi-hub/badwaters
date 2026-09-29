using BadWaters.Sim.Theatre;

namespace BadWaters.Sim.Command;

/// <summary>Builds a live theatre with every system and an autonomous commander on each side.</summary>
public static class TheatreFactory
{
    public static InterceptTrial Create(ScenarioDef sc, Func<string, SystemProfile> profile, Func<string, PresetInfo?> preset,
        IReadOnlyDictionary<string, BaseTypeDef> baseTypes, Func<string, DoctrineProfile> doctrineForSide, int seed,
        IReadOnlyDictionary<string, SideControl>? control = null, StoresCatalog? stores = null)
    {
        var t = new InterceptTrial(sc, profile, seed, baseTypes) { UseCommanders = true };
        if (control != null) foreach (var (k, v) in control) t.Control[k] = v;
        t.Naval = new NavalForces(t, preset, seed);
        t.Intel = new IntelService(t, preset, seed);
        t.Air = new AirTasking(t, preset, seed) { Stores = stores };
        t.Air.SeedMunitions();
        t.Ops = new OperationBoard(t);
        foreach (var side in new[] { "blue", "red" }) t.Commanders[side] = new Commander(t, side, doctrineForSide(side), preset, seed);
        return t;
    }
}
