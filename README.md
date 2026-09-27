# Bad Waters

A 2D grand-strategy wargame of modern naval, air and strategic missile warfare, 1990–2050. No land war: you fight at sea, in the air, and with missiles, drones and air defences.

Playable nations: **United States, Russia, China, France, United Kingdom, Israel, Iran.**

## Run it

```bash
npm install
npm run dev
```

Open http://localhost:5173. Other scripts: `npm run build`, `npm test`, `npm run typecheck`.

## How it plays

- **Monthly turns.** Each month: budgets, upkeep, research, development, production, AI nations, events, tension and wars.
- **Design everything.** Missiles, aircraft, ships, ground vehicle batteries (SAMs, TELs, drone launchers, coastal batteries), drones (one-way attack and UAVs) and bases. You build each one from components unlocked by research, and the game derives its performance from physics-flavoured models (see `src/sim/design/stats.ts`). Starting 1990 designs, such as Harpoon, Tomahawk, SM-2, Scud, Exocet, Ticonderoga, Sovremenny, F-14 and Su-27, are built from the same parts and land close to their real ranges.
- **Tech tree.** About 85 technologies in 7 fields, from 1955 legacy tech to megawatt lasers in the 2040s. Researching ahead of history is expensive; catching up on tech others already have is cheaper.
- **One battle engine for sea, air and strike.** Real-time with pause on a 2D NTDS-style plot over real coastlines. It models:
  - radar range (∝ RCS¼) and the radar horizon, so sea-skimmers are seen late
  - datalinks and CEC, fire channels and saturation
  - layered SAMs, CIWS and lasers, plus decoys and jamming
  - ballistic, hypersonic and loitering threats
  - submarines and torpedoes, and carrier air wings flying CAP, strike, AEW and maritime patrol
- **Strategic layer.** Real bases and 15 sea theatres, fleet deployments and carrier air wings. Scripted history runs from 1990 to 2024 (Gulf War, USSR collapse, 9/11, sanctions, Crimea, Ukraine, Gaza, the April 2024 Iran–Israel strikes); after that the future is procedural. Tension builds into crises and limited wars.
- **AI nations** research, modernise their designs, launch programmes (Shahed-type drones, BMD, Iron Dome, DF-21D, hypersonics, carriers), build to budget-scaled force targets, retire hulls when broke, deploy fleets and fight each other.

## Layout

```
src/data/        nations, bases, theatres, techs, designer parts, 1990 orders of battle, AI programmes
src/sim/design/  design → stats models
src/sim/campaign core, turn loop, economy, research, events, war, AI, player actions
src/sim/battle/  battle engine, battle setup from the campaign, results, geography
src/sim/worker.ts  month processing off the UI thread
src/ui/          React UI in the "Slate Ops" style (screens/, components/, store.ts)
```

## Tuning

Balance knobs live in data files: budgets and tech lag in `src/data/nations.ts`, tech costs and years in `src/data/techs.ts`, part attributes in `src/data/parts.ts`, AI programmes in `src/data/aiTemplates.ts`. The headless tests (`src/sim/__tests__`) include a full 1990–2050 AI campaign and print a decade-by-decade balance table.
