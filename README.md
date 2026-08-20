# Tonberry Tactics — GearGoblin Plugin

**Current version: 1.6.1**

GearGoblin is the internal/plugin name for the in-game half of **Tonberry Tactics**, a Final Fantasy XIV gearing and materia planning system built as a Dalamud plugin plus a Blazor WebAssembly companion app.

The plugin reads the currently logged-in character, equipped gear, melds, job, level, item level, and relevant stats; audits the current setup; compares it against an Etro or XIVGear target; and exchanges versioned gear/plan payloads with the Tonberry Tactics web companion.

The public-facing product name is **Tonberry Tactics**. The internal name remains `GearGoblin` so existing Dalamud configuration and window state continue to work.

## Current status

Version **1.6.1** is the current lockstep release of the three-project system:

- `LastOnionKnight/GearGoblin` — Dalamud plugin
- `LastOnionKnight/GearGoblin-Core` — shared optimizer/formula library
- `LastOnionKnight/TonberryTactics` — Blazor WebAssembly companion

All three are intended to ship at the same product version. Core is consumed by both front ends as the `external/GearGoblin.Core` git submodule.

GearGoblin currently targets:

- Dalamud API 15
- .NET 10 / `net10.0-windows`
- GearGoblin.Core 1.6.1

The project is under active development and is distributed through the custom plugin repository in this repo.

## Primary commands

```text
/tt          Open Tonberry Tactics
/ttexport    Export equipped gear and current stats to the clipboard
/ttimport    Import a GG-PLAN:v1 plan from the clipboard or inline text
/ttinfo      Copy diagnostics and open the Diagnostics tab
```

Older `/goblin*` and `/tactics*` aliases still exist in the codebase for compatibility, but `/tt*` is the current command family.

## What the plugin does today

### Character

The Character tab is the primary in-plugin character surface. It reads the current player and displays:

- current job and level
- average item level
- battle substats and derived context
- Crafter/Gatherer stats for DoH/DoL jobs
- current equipped gear
- materia state and recommendations

Battle jobs use the shared Core formulas and job profiles. DoH/DoL jobs are currently display-only for optimization: Craftsmanship, Control, CP, Gathering, Perception, and GP are read and surfaced, but crafting/gathering meld optimization is intentionally not enabled yet.

### Materia Advisor

The Materia tab uses `GearGoblin.Core.Materia.MeldOptimizer` against the currently equipped set.

It currently supports:

- empty-slot recommendations
- per-piece substat-cap enforcement
- overcap detection
- zero-value / wrong-stat detection
- outdated or replaceable meld auditing
- Tier XII endgame materia recommendations
- Pure Math and Balance-weight infrastructure in Core
- all 21 standard combat jobs
- DoH/DoL readback without battle-formula scoring

The current UI renders gear as per-piece cards, with meld indicators, audit status, summary counts, and recommended replacements.

### Plan / BiS comparison

The Plan tab accepts Etro and XIVGear links and compares the target set against currently equipped gear slot by slot.

Current verdicts include:

- MATCH
- REMELD
- UPGRADE
- SWAP
- TARGET LOWER
- ACQUIRE

The common target model is `Planning/BisGearset.cs`; source-specific parsing lives in `EtroParser` and `XivGearParser`.

Known limitation: item parsing is more complete than source-materia parsing. XIVGear currently creates incomplete meld metadata and Etro does not yet fully hydrate target melds. Until that is corrected, item-level/slot comparison is more trustworthy than an "item + melds identical" verdict.

### Web round trip

`/ttexport` currently emits:

```text
GG-EXPORT:v2:<base64-json>
```

The v2 export contains the character, equipped items, melds, per-piece base substats/caps, and current total stats.

The Tonberry Tactics web app accepts v1 and v2 exports, runs the shared Core optimizer, and emits:

```text
GG-PLAN:v1:<base64-json>
```

`/ttimport` consumes the plan, persists the imported recommendation data, and surfaces the active plan in the plugin.

Known limitation: imported plan persistence currently uses a compatibility fallback rather than a real per-character content ID. That must be corrected before multi-character plan storage is considered reliable.

### Native CharacterStatus injection

The plugin still contains the legacy `StatusPanelInjector` path for native Character-window derived rows, breakpoint hints, Materia Advisor output, and CharacterPanelRefined coexistence.

The standalone Tonberry Tactics Character tab is the preferred long-term surface because it avoids native AtkNode lifecycle/collision problems. The injector remains present for compatibility while that migration is completed.

### Diagnostics and feedback

The plugin includes:

- Settings tab
- Diagnostics tab
- `/ttinfo` clipboard diagnostics
- Force Reinject support for the legacy CharacterStatus injector
- in-window feedback tooling

## Architecture

```text
GearGoblin/
├─ Plugin.cs                     Dalamud entry point and command registration
├─ Configuration.cs              persisted plugin/job-plan settings
├─ DalamudServices.cs            injected Dalamud service container
├─ Materia/
│  └─ StatReader.cs              live PlayerState stat reader
├─ Planning/
│  ├─ BisFetcher.cs              Etro/XIVGear network fetch
│  ├─ BisGearset.cs              neutral target-set model
│  ├─ EtroParser.cs
│  └─ XivGearParser.cs
├─ Services/
│  ├─ InventoryReader.cs         equipped gear + materia reader
│  ├─ GearsetExporter.cs         GG-EXPORT:v2 producer
│  ├─ GearsetImporter.cs         GG-PLAN:v1 consumer
│  ├─ ConfigurationService.cs
│  └─ StatusPanelInjector.cs     legacy native CharacterStatus injection
├─ UI/
│  ├─ MainWindow.cs
│  ├─ CharacterTab.cs
│  ├─ PlanTab.cs
│  └─ MateriaTab.cs
├─ Theme/                        Tonberry Tactics UI chrome/fonts
└─ external/GearGoblin.Core/    shared Core git submodule
```

## Shared Core

Optimizer and formula logic lives in `GearGoblin.Core` rather than being independently reimplemented in the plugin and web app.

Core currently owns the important shared types and logic for:

- job profiles and roles
- battle substats
- materia catalog/tier data
- stat snapshots and level modifiers
- cap math
- damage/stat formulas
- meld-slot models
- materia optimization and audit logic
- export schema types used by the current gear round trip

This split exists specifically to keep the web and plugin from producing different answers for the same character.

## Installation

Until distribution changes, install from the custom Dalamud repository:

```text
https://raw.githubusercontent.com/LastOnionKnight/GearGoblin/main/repo.json
```

Add it under:

`/xlsettings` → Experimental → Custom Plugin Repositories

Then install **Tonberry Tactics** from `/xlplugins`.

## Build

Dalamud development assemblies are expected at the standard XIVLauncher development path, or through `DALAMUD_HOME` if configured locally.

```powershell
git submodule update --init --recursive
dotnet restore
dotnet build -c Release
```

The project uses `Dalamud.NET.Sdk/15.0.0` and targets .NET 10.

## Release flow

Tagged versions trigger `.github/workflows/release.yml`, which builds the plugin, creates `latest.zip`, publishes the GitHub release, and updates `repo.json`.

The repository also contains `release.ps1` for the normal local release flow.

The plugin, Core, and web companion follow **trinity lockstep** versioning. A release should leave all three projects on the same product version unless the divergence is explicitly intentional and documented.

## Current known debt

The following are known follow-on items, not claims of completed functionality:

- make Raider mode a first-class workflow rather than only a BiS URL/config concept
- add dynamic raid food and potion recommendations
- complete Etro/XIVGear target-meld parsing
- replace the imported-plan content-ID compatibility fallback with real per-character identity
- finish migration away from native CharacterStatus injection
- continue DoH/DoL optimization beyond display-only stat support
- keep plan/export schema versions backward-compatible as new recommendation types are added

## Next planned feature: Raider consumables

The next major Raider feature is a **food and potion advisor** that recommends current raid consumables from live game data instead of hardcoded item names.

The intended direction is:

- derive the current job and real stat totals
- enumerate current food/medicine through Lumina data
- calculate actual HQ gains with percentage and cap rules
- score food against the character's real gearing needs and GCD constraints
- recommend the correct current main-stat potion for the job
- allow a loaded BiS plan to override the calculated choice when the source explicitly specifies consumables
- carry consumable recommendations through the Tonberry Tactics round trip in a future schema revision

## Companion repositories

- Web: https://github.com/LastOnionKnight/TonberryTactics
- Core: https://github.com/LastOnionKnight/GearGoblin-Core
- Live web app: https://tonberrytactics.pages.dev

## Credits

- CharacterPanelRefined — MIT-licensed native CharacterStatus injection patterns; license retained under `LICENSES/`
- FFXIV public/datamined game data used for job, item, materia, and formula work
- Akhmorning / Allagan Studies formula research
- The Balance community material used as reference for community-priority presets

## License

See the repository license files and source headers for component-specific licensing. Third-party code retains its original license notices.
