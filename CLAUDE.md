# Bad Waters: project rules

## Design intent (fixed, not negotiable)

**This is a 2D strategy game for Steam, not a simulation of real life.**
- **Real names, game stats:** real-world system names are used for identification (see `DISCLAIMER.md`), in the C:MO style. The art is 2D. Performance stays abstract 0–10 game ratings for relative feel; never real specifications.
- **Game stats, not real data:** stats are abstract game ratings and tiers (for example range tier 1–10, a "hit rating", a cost in game credits), tuned for fun and balance. They are not derived from, calibrated to, or presented as real-world performance data.
- **Plausible, not real:** designs obey believable limits (weight, space, power, cooling, topside load, hull size), so a carrier deck won't fit on a corvette because it is too heavy, not because a rule forbids it. What we do NOT do is reproduce or calibrate to real weapon characteristics: combat and performance stay abstract game ratings, tiers and dice, never a realistic simulation of real-world systems.
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
- **Real Earth map:** the Strategic Map base is Natural Earth civil cartography built by `tools/geo-build.py` from an allow-list (relief, water, borders, major roads/rail, capitals and major cities, major civil ports/airports). Never import real power, energy, pipeline, water, telecom, military or intelligence locations; those layers stay game-simulated abstractions. Cities are protected, never targets. Layers are data in `data/core/map_layers.json`.
- **The war fights itself.** Theatre commanders (`src/BadWaters.Sim/Command/Commander.cs`, one per side) and the strategic AI (`Economy/StrategicCommander.cs`, per nation) run everything; the player intervenes. There is ONE operation system (`Command/Operation.cs` + `OperationBoard`) used by AI and player alike, and command authority (`Authority`) gives each asset exactly one owner. Commanders see only their own forces and their side's `Picture` (fog parity) and react to the resulting situation, never to scripts. There are no fixed theatres: **one persistent world, local fights**. Every base, unit, depot and route lives in the world order of battle (`data/core/world_oob.json`, keyed by nation, built by `tools/oob-build.py`) and its state in `World.Forces`. Wars are between nations (`World.Wars`); a war fights in a zone generated from the forces within reach of each other (`Command/FightZone.cs`), built and simulated only while the player is engaged in it in Theatre Ops (to save resources; unwatched wars resolve cheaply off-screen via `FightZone.AbstractDay`, and the strategic map draws no zones), and written back into `World.Forces` and the economy, so a rebuilt zone starts where it left off. Doctrine and dispositions are data (`data/core/theatre/doctrine.json`, `data/core/strategic/dispositions.json`).
- Theatre Ops is its own mode with its own visual language (tactical palette in `UiTheme`, windows in `game/scripts/TheatreWindows.cs`); no strategic screens are reachable inside it.
- Hardcode rules, not units. Components, frames, techs, factories and routes are JSON; the C# code only evaluates them.

- C# / .NET 8 solution: `BadWaters.sln`. The engine-free game systems are in `src/BadWaters.Sim`, JSON loading in `src/BadWaters.Data`, and the Godot 4 .NET UI in `game/`.
- Content is JSON in `data/core`; mods in `mods/` override by `id`. Keep content and names out of code.
- Names (real systems) live only in JSON (`designs.json`, `presets.json`), so they can be renamed in one place. `DISCLAIMER.md` covers them.
- Personnel career lengths (from the user) are in `data/core/personnel.json`: command 45, air 25, naval 12, intelligence 35, air defence 12, cyber 25.
- The plan is in `docs/PLAN.md`. The TypeScript v0.1 prototype in `prototype-ts/` is reference only; don't port its real-world names or figure-calibrated formulas.
