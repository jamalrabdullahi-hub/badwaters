# Bad Waters: C# / .NET rebuild plan

Status: draft for review. The engine, clock and map choices are pending in the UI picker (https://claude.ai/artifact/UKwFbVxpPbsRmN5XaDdYqc). This plan assumes the recommended answers (Godot 4 .NET, pausable real time, flat zoomable map) and marks where another answer would change it.

## 1. What changes from v0.1

v0.1 (TypeScript/React, this repo) proved the core: the design stat models, the battle engine, the campaign loop and the AI. It looks and feels like a browser game, and its time model (monthly turns) can't carry sortie generation, ISR timing or logistics.

The rebuild keeps the proven models, ports them to C#, and grows them into the "state as a machine" design:

> SEE → FUSE → PLAN → GENERATE → EXECUTE → ASSESS → ADAPT, sustained by PERSONNEL + EQUIPMENT + LOGISTICS + INDUSTRY.

## 2. Platform and solution layout

- **.NET 8, C# 12.** Target Windows first, with Linux and Steam Deck later.
- **Engine: Godot 4.x (.NET build), recommended.** Its scenes are text files, it has solid 2D and UI, it's free, and it exports a native .exe. Unity would work with the same solution layout, but the Game project would become Unity assemblies.
- **The simulation never references the engine.** That makes it unit-testable, headless-runnable and portable.

```
BadWaters.sln
  src/BadWaters.Sim/        pure C#: world state, offices, personnel, logistics, industry, ISR/fusion, missions, battle engine, AI
  src/BadWaters.Data/       JSON loading, schema validation, mod layering, id resolution
  src/BadWaters.Game/       Godot project: rendering, UI, input, audio, save/load, calls into Sim
  tools/BadWaters.Cli/      headless campaign runner, data validator, balance reports
  tests/BadWaters.Sim.Tests xUnit: models, determinism, 1980→2050 soak runs
  data/core/                all game content as JSON (+ JSON Schemas in data/schemas/)
  mods/                     user mods, loaded after core in priority order
```

Determinism: a seeded RNG per subsystem, fixed-step ticks, and no floating-point-order dependence in AI decisions. This allows replays, reproducible bug reports and possible multiplayer later.

## 3. Moddable data (JSON everywhere)

Every piece of content is a JSON record with a stable `id`. Mods override by id (deep merge) or add new ids. The validator CLI checks every file against its schema and cross-references before the game starts.

| Folder | Contents |
|---|---|
| `nations/` | Identity, budget curves, doctrine weights, starting relations, name lists, start-date availability |
| `scenarios/1980.json`, `2000.json`, `2020.json` | Per-nation starting orders of battle, buildings, stockpiles, personnel pools, known tech, alliances |
| `tech/` | Tech tree nodes (field, year, cost, prerequisites) |
| `components/` | Designer parts per category (missile, aircraft, ship, vehicle, drone, sensor, datalink, EW, base component, building component) |
| `designs/` | Historical starting designs, built from components |
| `offices/` | The 15 offices: inputs, outputs, formulas as named model references, UI grouping |
| `personnel/` | Categories, training pipelines, career length, fatigue rules |
| `buildings/` | Building types and their component slots |
| `missions/` | Mission types: required roles, resource costs, confidence gates |
| `events/` | Historical and random events: triggers, effects, choices |
| `ai/` | Doctrines, programme templates, force targets |
| `localisation/` | All player-facing text |

Formulas stay in C#, named and referenced from JSON (for example `"readinessModel": "air.sortieGeneration.v1"`). Mods tune coefficients, not code. A later step can add optional C# script mods.

## 4. Campaign setup

- **Start dates:** 1980, 2000 or 2020. The campaign ends in 2050.
- **Great powers:** United States, Russia (the Soviet Union in 1980), China, United Kingdom, France.
- **Regional powers:** Israel, Iran, Egypt, Türkiye, Saudi Arabia, North Korea, South Korea, Japan, Germany (West Germany in 1980), Ukraine.
- **Date rules:** Ukraine is only playable from 2000; in 1980 it's part of the Soviet Union. The 1980 Soviet Union dissolves by historical event unless the player's choices change history.
- **Setup flow:** choose the date, then the nation, then optional rules (fog strength, historical events on or off, AI aggression).

## 5. The state as a machine: offices

There are 15 offices in 6 departments. Every rating is computed from the office's inputs; there are no flat bonuses. The UI always shows why a rating is what it is.

| Department | Offices |
|---|---|
| Command | National Command Authority, Joint Operations Command (theatre commands) |
| Combat | Air Force, Navy, Strategic Forces, Air & Missile Defence |
| Intelligence | Intelligence Directorate, **Fusion Office**, Space Command, Cyber & EW Command |
| Industry | Defence Procurement, R&D, Defence Industry |
| Sustainment | Logistics Command, Maintenance, Finance/Treasury, Infrastructure |
| State | Foreign Affairs, Mobilisation, Civil Defence & Strategic Resilience |

Example: Air Force readiness 71% = f(pilot availability 82%, aircraft availability 74%, spare parts 63%, fuel 91%, maintenance capacity 68%, training 79%).

### Fusion Office (central system)

- **Physical asset.** Fusion centres are buildings you build, staff and defend. Destroying one degrades the owner's Common Operational Picture (COP) until backups take over. Redundant nodes cost money and analysts.
- **Inputs:** IMINT, SIGINT, ELINT, HUMINT and OSINT collections. Sensors feed them: radars, satellites, AEW, drones, ESM, sonar, human networks. Datalinks carry them in.
- **Process:** correlate sensor reports into tracks, with a position estimate and uncertainty, an ID (Hostile, Suspect, Unknown, Assumed friend, Friend), a confidence value and track age.
- **Throughput:** tracks per hour, capped by senior analysts, computing and network health. Overload raises latency and false correlations.
- **Deception:** decoys, emission spoofing, dummy formations and false communications inject false reports. Good multi-source fusion catches them; weak fusion doesn't.
- **Output:** the COP, which is what your command believes, not the truth. It also gives target confidence for mission planning, and the fusion queue: suggested collection tasks with their expected gain in confidence.

## 6. Truth versus belief: three information levels

- **Truth:** the simulation's ground state. The player never sees it directly.
- **Belief (COP):** a per-nation store of tracks and estimates, maintained by fusion.
- **Strategic picture:** the world map with fleets, bomber deployments, missile forces, bases, industry, logistics networks, satellite coverage, air-defence zones and readiness. Everything foreign is drawn from belief, with confidence shading.
- **COP:** the main war map. It shows air, sea and sub tracks, launches, known SAM sites, patrols, ISR and EW activity, logistics moves, damage, and uncertainty ellipses.
- **Tactical displays:** zoomed views over the COP for air defence, naval and EW. The v0.1 battle engine becomes the tactical resolution layer, with its sensors feeding fusion instead of perfect tracks.

## 7. Infrastructure you build and defend

Every building type has a map location, component slots, hardening, air-defence coverage, outputs and a damage/repair model:

1. Industrial parks: production lines, skilled labour and input stockpiles.
2. Logistics ports (sea, air, rail, border crossing): throughput and connections in the supply network.
3. Executive and government sites: the head of state, leadership and military headquarters, with bunkers, continuity-of-government backups and local air defence.
4. Strategic reserves: fuel, munitions, spare parts and rare materials.
5. Military bases (designable, see §8).
6. Civil-defence sites: public bunkers, shelters and camps.
7. Population centres: cities, towns, hamlets and settlements. They generate the economy and labour, and resilience depends on their protection.
8. Intelligence agency offices and fusion centres.
9. Also: radar sites, space ground stations, satellite launch sites and training schools (the personnel pipelines).

Design rule: population centres are defended, and they can suffer collateral damage and strikes by the AI's doctrine. The player's target list covers military, state and infrastructure targets. There is no "attack population" order.

## 8. Base designer

- **Purpose first:** Joint, Air, Naval, Missile, ISR/Fusion or Logistics. The purpose unlocks component families.
- **Components** (JSON-defined):
  - runways (count and length), hardened aircraft shelters, underground hangars, open parking
  - fuel (open or hardened, with days of storage), munitions igloos
  - piers, drydocks and submarine pens
  - sensors (radar, passive ESM), SAM batteries, point defence (CIWS, C-RAM, lasers)
  - fusion node, C2 bunker, runway repair teams, decoys, dispersal sites, housing and training areas
- **Derived stats:** sortie capacity per day, ship turnaround and repair, fusion throughput, personnel needed per category, cost, build time and upkeep.
- **Resilience test:** "40 × SRBM salvo" shows sortie capacity over the following 24 hours as runways get repaired.
- Existing bases can be rebuilt to a new design over time.

## 9. Personnel (second economy)

Scarce categories only; there's no micro-simulation of individual jobs.

| Department | Categories | Career length (years) |
|---|---|---|
| Command | senior officers, operations officers, strategic planners | 45 |
| Air Force | fighter pilots, bomber/strike pilots, transport pilots, ISR crews, aircrew specialists, maintainers | 25 |
| Navy | surface warfare, submariners, naval aviators, naval aviation crews | 12 |
| Air Defence | AD commanders, radar operators, SAM operators, AD analysts | 12 |
| Intelligence | analysts, HUMINT, SIGINT, technical specialists, officers | 35 |
| Cyber/EW | cyber operators, EW operators, network specialists | 25 |

The career lengths are as you specified; the Navy and Air Defence values of 12 years are short and may be meant as something else. All values sit in `personnel/*.json` and can be tuned.

- **Pools** are tracked by experience band (trainee, qualified, experienced, senior). Retirement happens by cohort once a career length is reached, and combat adds attrition.
- **Pipelines** are schools with a capacity per year, a cost and a lead time, and are expanded by building training infrastructure. Losing experienced crews is a strategic wound.
- **Fatigue** builds with operational tempo and raises accident rates and maintenance errors.

## 10. Readiness postures

| Force | Postures |
|---|---|
| Air Force | Peacetime, Elevated, Alert, Surge, Wartime |
| Navy | Port, Ready, Deployed, Combat |
| Strategic Forces | Normal, Elevated, High alert |
| Air Defence | Normal, Reinforced, Full combat |
| Cyber | Normal, Defensive, Elevated, Offensive |

Higher postures cost money, fuel, personnel fatigue, airframe life and diplomatic standing. Posture sets reaction time and what missions can be generated.

## 11. Missions and sortie generation

- **Unit states:** standby, alert, CAP, training, maintenance, rearming, reserve.
- **Mission catalogue** (JSON):
  - air: CAP, intercept, strike, SEAD/DEAD, reconnaissance, escort, air superiority, maritime strike, battle damage assessment
  - naval: patrol, convoy escort, ASW, surface warfare, strike, carrier ops, blockade, reconnaissance
  - missile: conventional, anti-ship, suppression, strategic
  - ISR: drone reconnaissance, ELINT, SIGINT, satellite tasking, maritime surveillance
  - cyber: reconnaissance, network disruption, EW support, communications disruption, collection
- **Operations planner:** set the objective, assemble packages (for example 8 strike aircraft, 4 escorts, 2 EW aircraft, 1 AEW&C, 2 recon drones and a tanker package), and see the Fusion Office's target confidence and the estimated threat.
- **Confidence gate:** below a mission type's minimum target confidence, strike missions are unavailable. The planner offers ISR tasks that would raise confidence. Sometimes the right move is to send another drone.
- **Sortie generation per base and air wing per day:**
  `min(aircraft mission-capable, crews available × crew ratio, maintenance capacity, fuel and munitions on hand, base capacity, planning capacity)`
  Surging above sustainable tempo accrues fatigue, maintenance backlog and accident risk.

## 12. Logistics, industry, procurement and finance

- **Logistics:** a supply network graph of ports, depots and bases. Stock types are fuel, munitions by type, spare parts, engines and consumables. Transport capacity comes from sealift, airlift and rail. Forward bases need access agreements (Foreign Affairs).
- **Industry:** production lines with capacity per year, needing inputs: skilled labour, electronics, semiconductors, engines, explosives, rare materials, supply chains and quality control. A bottleneck looks like "annual production capacity: 43". Sanctions and strikes cut inputs.
- **Procurement pipeline:** requirement → design (the designers) → prototype → testing (can fail and surface flaws) → production → deployment.
- **Finance:** allocations to procurement, personnel, maintenance, R&D, infrastructure, intelligence, strategic forces, space, navy, air force and missile defence. Acquisition and sustainment are shown separately, so you can bankrupt yourself sustaining a fleet you could afford to buy.

## 13. Space, cyber/EW, strategic forces and diplomacy

- **Space:** satellites are real orbiting assets with passes, revisit times and coverage swaths, in ISR, communications, navigation and early-warning roles. They can be lost to ASAT weapons, jamming or failure. Losing your ISR constellation should make you feel blind.
- **Cyber:** targets are networks (command, logistics, communications and infrastructure nodes). Effects are degradation, delay and false data. Defence posture matters.
- **EW:** jamming, deception, ELINT collection and anti-radiation operations, tied into the fusion and tactical layers.
- **Strategic forces:** the deterrent is managed as posture, survivability and second-strike assurance. Nuclear use stays abstract: it's a catastrophic, campaign-ending escalation with no detailed targeting gameplay. Long-range conventional strike is fully playable.
- **Foreign Affairs:** alliances, basing and overflight rights, intelligence sharing, arms sales and purchases, sanctions and embargoes, and coalition operations.

## 14. Time model (recommended: pausable real time)

- Sim tick: 1 game hour at strategic level. Speeds are paused, 1 h/s, 6 h/s and 1 day/s, with auto-pause on flagged events.
- Budgets, industry output and personnel pipelines settle monthly. Sorties, logistics and fusion run hourly.
- Tactical engagements resolve at 1-second steps when the player takes command, or at coarse steps in the background.
- If the picker chooses daily turns, the same systems run with the day as the smallest player-facing step.

## 15. Milestones

| # | Milestone | Done when |
|---|---|---|
| M0 | Toolchain: .NET 8 SDK and Godot 4 .NET installed (needs your OK to download), solution skeleton, CI tests, data validator | `dotnet test` and an empty Godot scene both run |
| M1 | Sim core port: v0.1 models ported to C#, JSON data layer and schemas, headless runner | 1980→2050 soak run passes; 1990 design ranges match v0.1 |
| M2 | Game shell: chosen UI direction, main menu, date and nation selection (15 nations × 3 dates, placeholder data for new nations), strategic map with countries, camera and zoom | You can start any nation in any start year and pan the world |
| M3 | State machine: offices and ratings, personnel pools, pipelines and retirement, finance split, readiness postures, logistics network, industry lines | Readiness numbers explain themselves in the UI |
| M4 | Information: truth/belief split, sensors, Fusion Office buildings and throughput, COP view, ISR missions, deception | A decoy can fool weak fusion and be caught by multi-source fusion |
| M5 | Operations: mission catalogue, operations planner, sortie generation, confidence gating, tactical displays (engine port) | A full strike operation from ISR to BDA |
| M6 | Infrastructure: building placement and design, base designer, strikes on infrastructure, repair, civil defence, continuity of government | The state keeps functioning after a first-strike salvo, or visibly doesn't |
| M7 | Space, cyber/EW, strategic forces, diplomacy and access | Losing satellites blinds you; losing basing rights grounds you |
| M8 | Content and AI: 15 nations × 3 start dates, events, AI using the same systems, balance passes, audio, polish, Steam build | A playable campaign from each start date |

## 16. Open questions for you

1. The engine, clock and map picks in the UI picker.
2. The Navy and Air Defence career lengths of 12 years: confirm these, or say what they should mean.
3. Multiplayer: never, or keep determinism so it's possible later? The plan keeps it possible.
4. Should v0.1 (TypeScript) stay as a prototype branch, or be deleted once M1 passes? The plan keeps it on a `prototype-ts` branch.
