// DESIGN INTENT — read before adding targets, weapons, or strike options.
//
// Bad Waters is a 2D strategy GAME for Steam. It is not a simulation of real life:
//   - every weapon, platform, component and design is a fictional game asset;
//   - stats are abstract game ratings for balance and fun, not real-world performance data;
//   - there is no physics simulation, only rules-based game mechanics;
//   - "designing" a missile or ship means choosing and adjusting game components from a list;
//   - nothing here is, or should become, a real-world tool, planner or calculator.
//
// Within that fiction, two rules are fixed by design and are enforced here, not left to convention:
//
// 1. POPULATION IS DEFENDED, NEVER TARGETED.
//    Cities, towns, settlements and civil-defence shelters exist so the player can protect
//    them (air defence, bunkers, warning, resilience). They can suffer collateral damage and
//    they are a measure of what failure costs. No player order, mission type, AI plan, or mod
//    may make a population centre or civilian shelter the object of a strike. Legitimate
//    targets are military forces, military bases, and the state and industrial infrastructure
//    that sustains the war effort.
//
// 2. NUCLEAR WEAPONS STAY ABSTRACT.
//    Strategic forces are modelled as deterrent posture, survivability and second-strike
//    assurance. There is no nuclear targeting gameplay: nuclear use is a catastrophic,
//    campaign-ending escalation, not a tool the player aims.
//
// Any new target class must be added to TARGET_POLICY explicitly. Unknown classes are refused.

export type TargetClass =
  // legitimate
  | "military-force" // ships, aircraft, vehicle batteries, submarines
  | "military-base" // air, naval, missile, radar/air-defence bases
  | "command-military" // military headquarters, C2 bunkers, fusion centres
  | "intelligence-site" // intelligence agency facilities, ground stations
  | "industrial" // defence industrial parks, production lines
  | "logistics" // ports, depots, rail and border logistics hubs
  | "strategic-reserve" // fuel, munitions and materials stockpiles
  // protected
  | "population-centre" // cities, towns, hamlets, settlements
  | "civil-shelter"; // civilian bunkers, shelters, camps

export const TARGET_POLICY: Readonly<Record<TargetClass, "legitimate" | "protected">> = Object.freeze({
  "military-force": "legitimate",
  "military-base": "legitimate",
  "command-military": "legitimate",
  "intelligence-site": "legitimate",
  industrial: "legitimate",
  logistics: "legitimate",
  "strategic-reserve": "legitimate",
  "population-centre": "protected",
  "civil-shelter": "protected",
});

/** True only for classes explicitly listed as legitimate. Unknown classes are refused. */
export function isLegitimateTarget(cls: string): boolean {
  return (TARGET_POLICY as Record<string, string>)[cls] === "legitimate";
}

/** Map a campaign base type to its target class. */
export function targetClassOfBase(type: string): TargetClass | null {
  switch (type) {
    case "air": case "naval": case "missile": case "radar": return "military-base";
    default: return null; // unknown site types are not targetable until classified
  }
}

export const PROTECTED_TARGET_MESSAGE = "Population centres and civilian shelters are protected. They can't be chosen as strike targets.";

export const NUCLEAR_POLICY = Object.freeze({
  targetingGameplay: false,
  /** Nuclear use is modelled only as a campaign-ending escalation. */
  outcome: "campaign-ends" as const,
});
