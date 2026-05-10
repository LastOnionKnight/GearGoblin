# GearGoblin

BiS planner with acquisition tracking and materia advisor for Final Fantasy XIV. A Dalamud plugin.

## What v0.1 does

Reads your equipped gear and shows it in a window with materia melds and average item level. That's it. It's the foundation — proves the inventory plumbing works end to end.

## Roadmap

- **v0.1** ✅ Inventory reader, equipped gear display, materia inspection
- **v0.2** Paste Etro/XIVGear URL → target display + slot-by-slot diff
- **v0.3** Acquisition pathing — "next upgrade is X, costs Y books, ETA Z weeks"
- **v0.4** Materia planner — overmeld success math, breakpoint awareness, sell-vs-meld advice
- **v0.5** Casual presets — generic stat priorities per job/role for non-raiders
- **v0.6** Multi-job tracking, weekly action summary

## Build

Requires the Dalamud dev libs at `%appdata%\XIVLauncher\addon\Hooks\dev\` (the default path Dalamud installs to). Or set `DALAMUD_HOME` to override.

```
dotnet restore
dotnet build -c Release
```

The output `.dll` and `GearGoblin.json` go in a folder named `GearGoblin` under `%appdata%\XIVLauncher\devPlugins\`. Then `/xlplugins` in-game and load it as a dev plugin.

## In-game

`/goblin` toggles the window.

## Architecture

- `DalamudServices` — single static container for all `[PluginService]` injections.
- `Plugin` — entry point; constructs services, registers the command, owns the window system.
- `Configuration` — per-character `JobPlanData` (mode + BiS URL or casual preset). Persisted via `IPluginConfiguration`.
- `Services/InventoryReader` — wraps `IGameInventory` to produce typed `EquippedPiece` records with materia melds resolved against the `Materia` and `BaseParam` Lumina sheets.
- `UI/MainWindow` — tabbed: Current Gear (working), Plan (stub), Materia (stub).

## Notes for the next pass

- The materia value lookup uses `BaseParam.Name` for display. For the materia planner we'll need the actual stat ID, not just the name, to do tier-breakpoint math.
- Inventory slot index → `EquipSlot` mapping is hardcoded against the standard order. If a future Dalamud API change reorders these, it'll break visibly (slots show as Unknown) rather than silently.
- No retainer reading yet. `IGameInventory` exposes retainer slots but only when the retainer is open. v0.3+ will need a cached "last seen" approach.
