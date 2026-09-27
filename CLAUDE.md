# Bad Waters: project rules

## Design intent (fixed, not negotiable)

**This is a 2D strategy game for Steam, not a simulation of real life.**
- **Fictional content:** all weapons, platforms, components and designs are fictional game assets with fictional names. The art is 2D.
- **Game stats, not real data:** stats are abstract game ratings and tiers (for example range tier 1–10, a "hit rating", a cost in game credits), tuned for fun and balance. They are not derived from, calibrated to, or presented as real-world performance data.
- **No physics simulation:** combat and movement are rules-based game mechanics (tiers, ratings, dice/probability tables), not physical or engineering models.
- **Designers are gameplay:** "missile designer", "ship designer" and similar mean choosing and adjusting game components from a list to trade off game stats. They never do real engineering or design work.
- **Nothing usable outside the game:** don't build anything that could be interpreted or used as a real-world tool, planner, calculator or targeting aid, or anything harmful.
- Real nations appear as playable sides of a fictional strategic contest.


The targeting rules below are enforced in code by `src/sim/policy.ts` and tested in `src/sim/__tests__/policy.test.ts`. The C# rebuild must carry the same policy into `BadWaters.Sim` with the same tests. Mods can't override it.

1. **Population is defended, never targeted.** Cities, towns, settlements and civilian shelters exist for the player to protect: air defence, bunkers, warning and resilience. They can suffer collateral damage, and their losses measure what failure costs. No player order, mission type, AI plan or mod may make a population centre or civilian shelter the object of a strike. Legitimate targets are military forces, military bases, and the state and industrial infrastructure that sustains the war effort.
2. **Nuclear weapons stay abstract.** Strategic forces are modelled as deterrent posture, survivability and second-strike assurance. There is no nuclear targeting gameplay. Nuclear use is a campaign-ending escalation.

When you add a new target type, site type or mission type, classify it in `TARGET_POLICY`. Unclassified classes are refused by default.

## Working notes

- The rebuild plan is in `docs/PLAN.md` (C# / .NET, JSON-driven content).
- The TypeScript prototype is v0.1. Run the tests with `npm test`.
- v0.1 still uses real-world weapon names and stat formulas tuned to real published figures. The rebuild replaces both with fictional names and abstract JSON game ratings. Don't carry either forward.
