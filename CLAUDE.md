# Bad Waters: project rules

## Design intent (fixed, not negotiable)

**This is a 2D strategy game for Steam, not a simulation of real life.**
- **Fictional content:** all weapons, platforms, components and designs are fictional game assets with fictional names. The art is 2D.
- **Game stats, not real data:** stats are abstract game ratings and tiers (for example range tier 1–10, a "hit rating", a cost in game credits), tuned for fun and balance. They are not derived from, calibrated to, or presented as real-world performance data.
- **No physics simulation:** combat and movement are rules-based game mechanics (tiers, ratings, dice/probability tables), not physical or engineering models.
- **Designers are gameplay:** "missile designer", "ship designer" and similar mean choosing and adjusting game components from a list to trade off game stats. They never do real engineering or design work.
- **Nothing usable outside the game:** don't build anything that could be interpreted or used as a real-world tool, planner, calculator or targeting aid, or anything harmful.
- Real nations appear as playable sides of a fictional strategic contest.


The targeting rules below are enforced in code by `src/BadWaters.Sim/Policy/TargetPolicy.cs` and tested in `tests/BadWaters.Sim.Tests/PolicyTests.cs`. The v0.1 prototype has the same rules in `prototype-ts/src/sim/policy.ts`. Mods can't override them.

1. **Population is defended, never targeted.** Cities, towns, settlements and civilian shelters exist for the player to protect: air defence, bunkers, warning and resilience. They can suffer collateral damage, and their losses measure what failure costs. No player order, mission type, AI plan or mod may make a population centre or civilian shelter the object of a strike. Legitimate targets are military forces, military bases, and the state and industrial infrastructure that sustains the war effort.
2. **Nuclear weapons stay abstract.** Strategic forces are modelled as deterrent posture, survivability and second-strike assurance. There is no nuclear targeting gameplay. Nuclear use is a campaign-ending escalation.

When you add a new target type, site type or mission type, classify it in `TargetPolicy`. Unclassified classes are refused by default.

## Working notes

- UI direction (chosen by the user): **Ops Floor · War Room colours**. It's a monitor-wall layout with slim glass bezels in deep navy and ice-cyan, with no CRT or scanline effects. `game/scripts/UiTheme.cs` is the single palette source.
- One simulation, two views. The **Strategic Map** is turn-based (1 day to 1 year) and is for running the country when you are NOT fighting: economy, industry, research, development, trade, deployments. **Theatre Ops** is where you fight when you ARE at war: you engage on the theatre map in real time at 1×–100×, with tasking and the C2 & comms network inside it. The rest of the world keeps running while a theatre is live.
- Hardcode rules, not units. Components, frames, techs, factories and routes are JSON; the C# code only evaluates them.

- C# / .NET 8 solution: `BadWaters.sln`. The engine-free game systems are in `src/BadWaters.Sim`, JSON loading in `src/BadWaters.Data`, and the Godot 4 .NET UI in `game/`.
- Content is JSON in `data/core`; mods in `mods/` override by `id`. Keep content and names out of code.
- Preset names use compact designations (for example `USF35A`) and live only in `data/core/presets.json`, so they can be renamed in one place.
- Personnel career lengths (from the user) are in `data/core/personnel.json`: command 45, air 25, naval 12, intelligence 35, air defence 12, cyber 25.
- The plan is in `docs/PLAN.md`. The TypeScript v0.1 prototype in `prototype-ts/` is reference only; don't port its real-world names or figure-calibrated formulas.
