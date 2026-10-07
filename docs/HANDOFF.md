# Bad Waters: session handoff

Read `CLAUDE.md` first (fixed design rules), then `docs/PLAN.md`. This file is where things stand.

## What we are building
A 2D grand-strategy / operational wargame for Steam (C:MO-style names, abstract 0–10 game stats). You run a real nation in a fictional contest:
- **Strategic Map** (turn-based, 1 day to 1 year): economy, industry, research, design (ship/missile/aircraft designers are component-picking gameplay), trade routes, diplomacy.
- **Theatre Ops** (real time 1×–100×): when at war, fight on a satellite theatre map with AI commanders on both sides. The player intervenes: missions, FRAGs, loadouts, logistics, emissions control (EMCON), inspection.
- **One persistent world:** every base, unit, depot and route lives in `data/core/world_oob.json` and keeps its state in `World.Forces`. A war between nations builds a fight zone (`FightZone`) only while the player is engaged in Theatre Ops. Unwatched wars resolve cheaply off-screen (`FightZone.AbstractDay`).
- **Hard rules:** population is never targeted, nuclear stays abstract, and nothing is usable outside the game.

## Stack and running
- **Code:** .NET 8 (`BadWaters.sln`). The engine-free sim is in `src/BadWaters.Sim`, loading in `src/BadWaters.Data`, the Godot 4.7 .NET UI in `game/`, the CLI in `tools/BadWaters.Cli`, and tests in `tests/BadWaters.Sim.Tests` (174 passing).
- **Content:** JSON in `data/core`, rebuilt by the Python tools in `tools/*.py`.
- **Tests:** `dotnet test tests/BadWaters.Sim.Tests`
- **Game:** `Godot_v4.7.2-stable_mono_win64.exe --path game res://scenes/Shell.tscn -- --select=JPN --playwar=JPN:CHN`
- **Dev args:**
  - set up the game: `--select=NAT`, `--war=A:B` (war starts on day 10), `--playwar=A:B`, `--advance=month|year`
  - open a screen or theatre view: `--screen=theatre|diplomacy|map|dev|industry|trade`, `--scenario=id`, `--center=lon,lat,zoom`
  - drive Theatre Ops: `--run=secs`, `--pick=kind:id`, `--mission=site,page`, `--genfrag=1`
  - capture the result: `--screenshot=path.png`
- **Headless war:** `dotnet run --project tools/BadWaters.Cli -- fullwar JPN:CHN 30`
- `tools/geo-src/` (raw source cartography) is gitignored. The built outputs are committed in `data/core/geo/`.

## Done recently (see git log)
- **Theatre Ops UI:**
  - map-first HUD (layout T3), satellite tiles, roads, towns, ETOPO contours
  - silhouette icons with a four-colour relation code
  - filters, overlays, EMCON
- **Mission and fire planning:**
  - Mission wizard: loadouts, munition availability, support / rescue / spare aircraft, ship strikes, multiple aimpoints on base components
  - 22 operation templates plus GENERATE DAY'S FRAG
  - right-click strike and fire missions
- **Inspection:** clicking a unit shows damage assessment, status, armament and fuel, with fog parity.
- **Logistics:** supply items, depots, transport, national stockpiles, AI logistics officer, LOGISTICS window.
- **The world:**
  - persistent world, Diplomacy screen
  - wars between nations; fight zones are built only when engaged

## Known problems (next priorities)
1. The air war stalls after about day 3 in a 30-day `fullwar`.
2. Missiles run out with no production response from the economy.
3. Operation churn: too many retried or aborted operations.
4. The AI never strikes supply routes.
5. Godot sometimes crashes at launch (GPU breadcrumb error).
6. The off-screen war results (`AbstractDay`) are untuned.

## Missing pieces
- Only JPN, CHN, ISR and IRN have forces in `world_oob.json`.
- The AI never declares war.
- Production doesn't spawn units at bases.
- There is no picker for several wars at once.
- The shipping-lane threat updates only when the date advances.
