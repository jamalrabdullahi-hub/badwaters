namespace BadWaters.Sim.Theatre;

// Bases are made of components (runway, shelters, fuel, munitions, radar...). A hit lands on a component
// ("aimpoint"), damage is dice + ratings against the component's hardness, and each component's
// integrity drives a concrete effect (sorties, arming, sensors, launches). Repairs run over time.
// All numbers are game tuning, not engineering data.

public sealed record BaseComponentDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>0–10: how much punch it takes to hurt (shelters and bunkers are hard).</summary>
    public double Hardness { get; init; }
    /// <summary>Integrity points restored per hour of game time.</summary>
    public double RepairPerHour { get; init; } = 5;
    /// <summary>What it does: sorties, shelter, apron, fuel, munitions, sensors, readiness, piers, launch-pads, command.</summary>
    public string Effect { get; init; } = "";
    /// <summary>For shelters: aircraft that fit inside.</summary>
    public int Capacity { get; init; }
    /// <summary>Doctrine priority when an attacker picks an aimpoint (lower first).</summary>
    public int Priority { get; init; } = 5;
}

public sealed record BaseTypeDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public List<BaseComponentDef> Components { get; init; } = new();
}

public sealed class ComponentState
{
    public required BaseComponentDef Def { get; init; }
    public double Integrity { get; set; } = 100;
    public bool Working => Integrity >= 50;
}

public sealed class BaseState
{
    public required SiteDef Site { get; init; }
    public required BaseTypeDef Type { get; init; }
    public Dictionary<string, ComponentState> Components { get; } = new();

    public ComponentState? ByEffect(string effect) => Components.Values.FirstOrDefault(c => c.Def.Effect == effect);
    private double Level(string effect) => ByEffect(effect)?.Integrity ?? 100;

    /// <summary>Aircraft can take off and land only with a runway at 50% or better.</summary>
    public bool RunwayOpen => Level("sorties") >= 50;
    /// <summary>Strike and SEAD flights need munitions storage at 30% or better to arm.</summary>
    public bool CanArm => Level("munitions") >= 30;
    /// <summary>Low fuel stocks and wrecked maintenance make each sortie slower to prepare.</summary>
    public double PrepFactor => (Level("fuel") < 30 ? 3 : 1) * (Level("readiness") < 50 ? 1.5 : 1);
    /// <summary>Sensor/C2 effectiveness of the base (and batteries sited at it): 0.2–1.</summary>
    public double SensorFactor => Math.Max(0.2, Level("sensors") / 100);
    /// <summary>Launchers sited here can fire only with pads at 40% or better.</summary>
    public bool PadsOk => Level("launch-pads") >= 40;
    public int ShelterCapacity => Components.Values.Where(c => c.Def.Effect == "shelter").Sum(c => (int)(c.Def.Capacity * c.Integrity / 100));

    /// <summary>The next aimpoint by doctrine: highest priority component still working.</summary>
    public string DefaultAimpoint(Func<string, double>? believed = null) =>
        Components.Values.OrderBy(c => (believed?.Invoke(c.Def.Id) ?? c.Integrity) < 50 ? 1 : 0).ThenBy(c => c.Def.Priority).First().Def.Id;

    public void Repair(double seconds)
    {
        foreach (var c in Components.Values) c.Integrity = Math.Min(100, c.Integrity + c.Def.RepairPerHour * seconds / 3600);
    }

    public string Status() => string.Join(" · ", Components.Values.Select(c => $"{c.Def.Name} {c.Integrity:0}%"));
}

/// <summary>Damage rules for one hit on a component.</summary>
public static class DamageRules
{
    /// <summary>Chance a weapon's hit lands on the aimpoint rather than nearby (accuracy 0–10).</summary>
    public static double OnAimpoint(double accuracy) => Math.Clamp(0.35 + 0.06 * accuracy, 0.2, 0.95);

    /// <summary>Integrity removed by one hit: punch against hardness; a near miss does a third.</summary>
    public static double Damage(double punch, double hardness, bool direct) =>
        Math.Max(3, 12 + 7 * punch - 6 * hardness) * (direct ? 1 : 0.33);
}
