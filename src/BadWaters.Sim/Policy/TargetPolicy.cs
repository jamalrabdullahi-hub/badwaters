// DESIGN INTENT: read before adding targets, weapons, missions or strike options.
//
// Bad Waters is a 2D strategy GAME for Steam. It is not a simulation of real life:
//   - every weapon, platform, component and design is a game asset with abstract game stats;
//   - stats are 1–10 style game ratings for balance and fun, not real-world performance data;
//   - designs obey plausible game limits (weight, space, power, topside load), but combat and performance
//     are rules-based game mechanics, never a realistic simulation of real-world weapon characteristics;
//   - "designing" ordnance, ships, aircraft or bases means choosing and adjusting game components;
//   - nothing here is, or should become, a real-world tool, planner or calculator.
//
// Within that fiction, two rules are fixed by design and enforced here, not left to convention:
//
// 1. POPULATION IS DEFENDED, NEVER TARGETED.
//    Cities, towns, settlements and civilian shelters exist for the player to protect. They can
//    suffer collateral damage. No player order, mission type, AI plan or mod may make them the
//    object of a strike. Legitimate targets are military forces, military sites, and the state and
//    industrial infrastructure that sustains the war effort.
//
// 2. NUCLEAR WEAPONS STAY ABSTRACT.
//    Strategic forces are deterrent posture, survivability and second-strike assurance. There is
//    no nuclear targeting gameplay; nuclear use is a campaign-ending escalation.
//
// Every target class must be listed here explicitly. Unknown classes are refused.

namespace BadWaters.Sim.Policy;

public enum TargetStatus { Legitimate, Protected }

public static class TargetPolicy
{
    private static readonly IReadOnlyDictionary<string, TargetStatus> Classes = new Dictionary<string, TargetStatus>
    {
        ["military-force"] = TargetStatus.Legitimate,     // ships, aircraft, batteries, submarines
        ["military-base"] = TargetStatus.Legitimate,      // air, naval, missile, radar/AD bases
        ["command-military"] = TargetStatus.Legitimate,   // military HQs, C2 bunkers, fusion centres
        ["comms-military"] = TargetStatus.Legitimate,     // military relays, links, ground stations
        ["intelligence-site"] = TargetStatus.Legitimate,
        ["industrial"] = TargetStatus.Legitimate,         // defence industry
        ["logistics"] = TargetStatus.Legitimate,          // ports, depots, rail and border hubs
        ["strategic-reserve"] = TargetStatus.Legitimate,  // fuel, munitions, materials stockpiles
        ["population-centre"] = TargetStatus.Protected,   // cities, towns, hamlets, settlements
        ["civil-shelter"] = TargetStatus.Protected,       // civilian bunkers, shelters, camps
    };

    public const string ProtectedMessage =
        "Population centres and civilian shelters are protected. They can't be chosen as strike targets.";

    /// <summary>True only for classes explicitly listed as legitimate. Unknown classes are refused.</summary>
    public static bool IsLegitimate(string targetClass) =>
        Classes.TryGetValue(targetClass, out var s) && s == TargetStatus.Legitimate;

    public static bool IsKnown(string targetClass) => Classes.ContainsKey(targetClass);

    public static IEnumerable<string> AllClasses => Classes.Keys;
}

public static class NuclearPolicy
{
    public const bool TargetingGameplay = false;
    public const string Outcome = "campaign-ends";
}
