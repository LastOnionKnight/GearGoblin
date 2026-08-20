# Tonberry Tactics — GearGoblin Plugin

**Current released version: 1.6.1**  
**Current `main`: unreleased 1.6.2 stabilization work**

GearGoblin is the internal/plugin name for the in-game half of **Tonberry Tactics**, a Final Fantasy XIV character optimization platform built as a Dalamud plugin, a shared Core library, and a Blazor WebAssembly companion app.

The long-term product target is an **Ask Mr. Robot-style optimizer for FFXIV**: read the character and owned gear, evaluate target sets, recommend upgrades/melds/food/potions, and eventually answer “what should I do next?” under job, encounter, currency, weekly-lockout, and player constraints.

The public-facing product name is **Tonberry Tactics**. The internal name remains `GearGoblin` so existing Dalamud configuration and window state continue to work.

## Ecosystem

```text
LastOnionKnight/GearGoblin       — Dalamud plugin / live game-state reader
LastOnionKnight/GearGoblin-Core  — shared formulas, optimizer, schemas
LastOnionKnight/TonberryTactics  — browser planning companion
```

The three projects use trinity lockstep versioning for releases.

Current runtime target:

- Dalamud API 15
- .NET 10 / `net10.0-windows`
- shared `GearGoblin.Core` submodule at `external/GearGoblin.Core`

## Primary commands

```text
/tt          Open Tonberry Tactics
/ttexport    Export equipped gear and current stats
/ttimport    Import a GG-PLAN:v1 plan
/ttinfo      Copy diagnostics and open the Diagnostics tab
```

`/tactics*` and `/goblin*` remain compatibility aliases. All registered command families are removed during plugin disposal.

## Character and gear ingestion

The plugin reads live character state through Dalamud and local game data through Lumina, including:

- current job and level
- stable character Content ID through API 15 `IPlayerState`
- equipped items and item levels
- HQ state
- materia and grades
- per-piece base substats/caps
- current battle stats
- current DoH/DoL stats

Imported plans are persisted per:

```text
ContentId
└── JobId
    └── JobPlanData
```

The old synthetic `contentId = 1` compatibility fallback is retired.

## Materia Advisor

The plugin uses `GearGoblin.Core.Materia.MeldOptimizer` for shared recommendation and audit behavior.

Current capabilities include:

- empty-slot recommendations
- per-piece cap enforcement
- overcap detection
- wrong/zero-value stat detection
- outdated/replacement auditing
- Pure Math and Balance-weight modes
- Tier XII combat materia projection from the shared Core tier table
- all 21 standard combat jobs
- DoH/DoL identification/display with battle optimization intentionally disabled

Current Tier XII combat projection is **+54**. Core owns the authoritative projection table so the plugin and web cannot drift independently.

## Plan / BiS comparison

The Plan tab accepts Etro and XIVGear targets and compares them against equipped gear.

Current target ingestion includes:

- Etro gearset fetch/parse
- current XIVGear URL-based data fetch path
- Lumina hydration of target item name and item level
- source materia resolution where the payload provides usable IDs
- XIVGear selected food Item ID capture for future Raider Solver work

Verdicts include:

- `MATCH` — target item and known target melds match
- `ITEM MATCH` — correct item; source meld details are unresolved/incomplete
- `REMELD`
- `UPGRADE`
- `SWAP`
- `TARGET LOWER`
- `ACQUIRE`

Unknown item level is not treated as zero for upgrade/downgrade decisions.

## Web round trip

Current plugin export:

```text
GG-EXPORT:v2:<base64-json>
```

The v2 payload carries character, gear, materia, total stats, and per-piece cap/base-stat context.

Current web-to-plugin plan:

```text
GG-PLAN:v1:<base64-json>
```

The web remains backward-compatible with `GG-EXPORT:v1`.

## Architecture

```text
GearGoblin/
├─ Plugin.cs
├─ Configuration.cs
├─ DalamudServices.cs
├─ Materia/
│  └─ StatReader.cs
├─ Planning/
│  ├─ BisFetcher.cs
│  ├─ BisGearset.cs
│  ├─ BisItemResolver.cs
│  ├─ EtroParser.cs
│  └─ XivGearParser.cs
├─ Services/
│  ├─ InventoryReader.cs
│  ├─ GearsetExporter.cs
│  ├─ GearsetImporter.cs
│  ├─ ConfigurationService.cs
│  └─ StatusPanelInjector.cs
├─ UI/
│  ├─ MainWindow.cs
│  ├─ CharacterTab.cs
│  ├─ PlanTab.cs
│  └─ MateriaTab.cs
└─ external/GearGoblin.Core/
```

## Build

```powershell
git submodule update --init --recursive
dotnet restore
dotnet build -c Release
```

## Release flow

Tagged releases run `.github/workflows/release.yml`. Tag-triggered builds now compile the tagged commit rather than forcibly checking out `main`, then publish `latest.zip` and update `repo.json`.

The plugin, Core, and web companion should be released in trinity lockstep unless divergence is explicitly documented.

## Current known debt

The major remaining work is solver capability rather than UI plumbing:

- replace generic cross-stat ranking with a normalized expected-output objective
- add job/GCD-aware gearset solving
- add Raider food + potion optimization
- evolve the plan schema for consumables and full-set recommendations
- add Best-in-Bags and full candidate-gear solving
- add acquisition/currency/weekly-lockout planning
- continue DoH/DoL optimization beyond display-only support
- finish migration away from the legacy native CharacterStatus injector where practical
- expand/validate external target parsing as Etro/XIVGear schemas evolve

## Next milestone

The next major development branch is the **v1.7 Solver Foundation**. Its first visible feature is Raider Consumables, but food/potions are intended to be solved as part of the character/gear objective rather than maintained as a hardcoded per-job lookup table.

## Installation

Custom Dalamud repository:

```text
https://raw.githubusercontent.com/LastOnionKnight/GearGoblin/main/repo.json
```

Add it under `/xlsettings` → Experimental → Custom Plugin Repositories, then install **Tonberry Tactics** from `/xlplugins`.

## Companion repositories

- Web: https://github.com/LastOnionKnight/TonberryTactics
- Core: https://github.com/LastOnionKnight/GearGoblin-Core
- Live web app: https://tonberrytactics.pages.dev

## License / credits

See repository license files and source headers for component-specific terms. Third-party code and assets retain their original licenses. Formula/reference work uses public FFXIV game data and community-verified research as documented in source.
