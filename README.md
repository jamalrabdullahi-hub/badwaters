# Bad Waters

A 2D strategy game for Steam about running a state's naval, air and strategic forces. There is no land war. It's built with C# / .NET 8 and Godot 4 (.NET).

Strategic turns (1 day to 1 year) wrap real-time Theatre Ops (1×–100×). You design ships, aircraft, ordnance and bases from game components, keep your command-and-comms network alive, and fight like an RTS. Stats are abstract game ratings; there's no physics simulation. See `CLAUDE.md` for the fixed design rules and `docs/PLAN.md` for the full plan.

## Layout

```
src/BadWaters.Sim/      pure C# game systems (no engine): policy, clock, C2 network, personnel, …
src/BadWaters.Data/     JSON content loading with mod layering (override by id, "$remove": true)
tools/BadWaters.Cli/    data validator and, later, headless campaign runs
tests/                  xUnit tests
game/                   Godot 4 .NET project (UI and rendering)
data/core/              all game content as JSON
mods/                   drop-in mod folders, loaded after core
prototype-ts/           the v0.1 TypeScript prototype (reference only)
```

## Build and test

```bash
dotnet build BadWaters.sln
```

```bash
dotnet test BadWaters.sln
```

```bash
dotnet run --project tools/BadWaters.Cli -- validate data/core
```

Open `game/project.godot` in Godot 4 (.NET edition) and press Play to see the campaign setup screen.

## Modding

Every content file in `data/core` is an array of records with an `id`. To change or add content, put a file with the same name in `mods/<your-mod>/`. Records with a matching `id` are deep-merged, new ids are added, and `{ "id": "X", "$remove": true }` deletes one. Preset names such as `USF35A` live in `data/core/presets.json` and can be renamed there.
