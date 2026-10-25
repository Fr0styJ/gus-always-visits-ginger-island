# Gus Always Visits Ginger Island

A [SMAPI](https://smapi.io) mod for Stardew Valley 1.6+ that guarantees **Gus** is one of the Ginger Island resort visitors on every day an island visit happens.

## Why

Each day the resort is open, vanilla picks 5 visitors (`IslandSouth.SetupIslandSchedules`): 40% of the time 5 random eligible NPCs, otherwise one of 12 hardcoded friend groups (none include Gus). With big NPC mods such as Stardew Valley Expanded or Ridgeside Village the eligible pool is huge, so Gus rarely goes.

## How it works

A Harmony postfix on `IslandSouth.SetupIslandSchedules` runs after vanilla chooses visitors. If there is a visit today and Gus wasn't chosen, it re-runs the vanilla selection restricted to *(old visitors - one random visitor + Gus)*. All vanilla schedule and activity code is reused, so Gus gets his normal resort bar spot and returns to the Saloon at 6pm. The bumped NPC gets their normal schedule back.

- Does nothing on days with no visit (rain, festivals, resort closed or not restored).
- Respects eligibility: if Gus is ineligible (e.g. a mod's `CanVisitIsland` rule, hospital day, invisible), he is not forced.
- Never bumps a player's spouse. Host only (multiplayer safe).
- No content edits, so it is compatible with NPC and content mods. No config.

## Install

1. Install [SMAPI](https://smapi.io).
2. Copy the `GusIslandVisitor` folder (containing `GusIslandVisitor.dll` and `manifest.json`) into `Stardew Valley/Mods`.

## Build

Requires the .NET SDK (8+) and a Stardew Valley install; the `Pathoschild.Stardew.ModBuildConfig` package locates the game automatically.

```bash
cd GusIslandVisitor
dotnet build -c Release
```

Deployment to `Mods` is disabled in the csproj (`EnableModDeploy=false`); set it to `true` to auto-copy on build.

## Verifying

Look for `Gus is visiting the island today` in the SMAPI log (Debug level).
