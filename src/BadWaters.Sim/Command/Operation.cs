using BadWaters.Sim.Theatre;

namespace BadWaters.Sim.Command;

// One operation system for the AI commanders and the player alike. An operation is a programmable object: purpose,
// objectives, elements (air packages, missile fires, naval moves, sensors), doctrine, IF/THEN rules, dependencies.
// Purposes are tags, not rigid mission types. Game abstraction: ratings and dice, no real tactics or procedures.

public enum Author { AI, Player }
public enum OpStatus { Planned, Active, Complete, Aborted }

/// <summary>What an element does, executed by the existing theatre systems.</summary>
public enum ElementKind { Air, Fires, Naval, Sensor }

public sealed class OpElement
{
    public required string Id { get; init; }
    public ElementKind Kind { get; init; }
    public string Role { get; set; } = "";
    /// <summary>Air or fires: the package / fire mission spec (flights, target, TOT, launcher, count...).</summary>
    public AtoEntry? Spec { get; set; }
    /// <summary>Naval: ship id and destination.</summary>
    public string Ship { get; set; } = "";
    public double Lon { get; set; }
    public double Lat { get; set; }
    /// <summary>Specific assets the author picked (aircraft, launcher, ship ids). Empty = pick from the free pool.</summary>
    public List<string> Assets { get; set; } = new();
    /// <summary>Held elements start only when a rule activates them (e.g. an EW escort if enemy AEW shows up).</summary>
    public bool Held { get; set; }
    /// <summary>Alternative spec used when a rule switches to the contingency (e.g. a route without the tanker).</summary>
    public AtoEntry? Contingency { get; set; }
    // runtime
    public string State { get; set; } = "waiting";
    public List<string> Flights { get; } = new();
    public List<string> Fires { get; } = new();
}

public sealed record Doctrine
{
    /// <summary>free | tight | hold: when weapons may be released.</summary>
    public string Roe { get; init; } = "tight";
    /// <summary>active | limited | silent: sensor and radio emissions.</summary>
    public string Emcon { get; init; } = "limited";
    /// <summary>standard | hardened | aggressive: game ECCM posture.</summary>
    public string Eccm { get; init; } = "standard";
    /// <summary>0..1: losses the operation accepts before its abort rules bite.</summary>
    public double RiskTolerance { get; init; } = 0.3;
    /// <summary>none | rules | full: how far the operation adapts on its own.</summary>
    public string Adaptation { get; init; } = "rules";
    public bool RtbOnCompletion { get; init; } = true;
}

/// <summary>A condition checked against the owner's own picture and the operation's state (never ground truth).</summary>
public sealed record OpCondition(string Kind, string Arg = "", double Value = 0)
{
    // kinds: enemy-aew-detected, enemy-air-near (Value km, Arg count), target-confidence-below (Value 0..1),
    // tanker-unavailable, losses-above (Value fraction), target-moved, bda-insufficient (Value integrity %),
    // time-after (Value seconds), element-state (Arg element:state)
    public override string ToString() => Kind switch
    {
        "enemy-aew-detected" => "enemy AEW detected",
        "enemy-air-near" => $"≥{(Arg == "" ? "1" : Arg)} enemy air contacts within {Value:0} km of the objective",
        "target-confidence-below" => $"target confidence < {Value:P0}",
        "tanker-unavailable" => "tanker unavailable",
        "losses-above" => $"losses > {Value:P0}",
        "target-moved" => "target relocated",
        "bda-insufficient" => $"BDA shows target above {Value:0}% after the strike",
        "time-after" => $"after T+{TimeSpan.FromSeconds(Value):hh\\:mm}",
        _ => Kind,
    };
}

public sealed record OpAction(string Kind, string Arg = "", double Value = 0)
{
    // kinds: activate (Arg element id), delay (Value s), hold-weapons, contingency (Arg element id), abort, retarget, follow-up, rtb
    public override string ToString() => Kind switch
    {
        "activate" => $"activate {Arg}", "delay" => $"delay {Value / 60:0} min", "hold-weapons" => "hold weapons",
        "contingency" => $"use contingency for {Arg}", "abort" => "abort", "follow-up" => "generate follow-up", "rtb" => "RTB", "retarget" => "retarget",
        _ => Kind,
    };
}

public sealed record OpRule(OpCondition If, OpAction Then, bool Once = true)
{
    public override string ToString() => $"IF {If} THEN {Then}";
}

public sealed class Operation
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public required string Side { get; init; }
    public Author Author { get; set; } = Author.AI;
    /// <summary>Edited by the player: the AI commander leaves it alone.</summary>
    public bool Locked { get; set; }
    public List<string> Purpose { get; set; } = new();
    /// <summary>Objective target ids (sites, or contact keys from the side's picture).</summary>
    public List<string> Targets { get; set; } = new();
    public List<OpElement> Elements { get; set; } = new();
    public Doctrine Doctrine { get; set; } = new();
    public List<OpRule> Rules { get; set; } = new();
    /// <summary>Operations that must complete before this one starts.</summary>
    public List<string> After { get; set; } = new();
    /// <summary>Earliest start (theatre seconds).</summary>
    public double Start { get; set; }
    /// <summary>After a successful strike, whether the commander may generate a follow-up if BDA is insufficient.</summary>
    public bool FollowUp { get; set; } = true;
    public OpStatus Status { get; set; } = OpStatus.Planned;
    public List<string> Log { get; } = new();
    /// <summary>Why the author made it (the commander's reasoning; shown in the Commander view).</summary>
    public string Rationale { get; set; } = "";
    public double Score { get; set; }
    // runtime
    public HashSet<int> FiredRules { get; } = new();
    public bool WeaponsHeld { get; set; }
    public int AircraftCommitted { get; set; }
    public int AircraftLost { get; set; }
    public bool Open => Status is OpStatus.Planned or OpStatus.Active;
}
