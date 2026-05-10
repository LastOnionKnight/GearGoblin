# GearGoblin

BiS planner with acquisition tracking and materia advisor for Final Fantasy XIV. A Dalamud plugin.

## Install (for users)

In-game, type `/xlsettings` → **Experimental** tab → **Custom Plugin Repositories** → add this URL:

```
https://raw.githubusercontent.com/LastOnionKnight/GearGoblin/main/repo.json
```

Hit save. Then `/xlplugins` → search for **GearGoblin** → install.

## What v0.1 does

Reads your equipped gear and shows it in a window with materia melds and average item level. That's it. It's the foundation — proves the inventory plumbing works end to end.

`/goblin` toggles the window.

## Roadmap

- **v0.1** ✅ Inventory reader, equipped gear display, materia inspection
- **v0.2** Paste Etro/XIVGear URL → target display + slot-by-slot diff
- **v0.3** Acquisition pathing — "next upgrade is X, costs Y books, ETA Z weeks"
- **v0.4** Materia planner — overmeld success math, breakpoint awareness, sell-vs-meld advice
- **v0.5** Casual presets — generic stat priorities per job/role for non-raiders
- **v0.6** Multi-job tracking, weekly action summary

## Build (for developers)

Requires the Dalamud dev libs at `%appdata%\XIVLauncher\addon\Hooks\dev\` (the default path Dalamud installs to). Or set `DALAMUD_HOME` to override.

```
dotnet restore
dotnet build -c Release
```

For local testing, point Dalamud at the build output: `/xlsettings` → **Experimental** → **Dev Plugin Locations** → add the path to your `bin\Release` folder. Then `/xlplugins` → **Dev Tools** → load.

## Releases

Tagged versions trigger a GitHub Actions build that produces `latest.zip` containing the plugin DLL and manifest. Dalamud's `repo.json` points at the latest release, so end users always get the current version.

To cut a release: bump `AssemblyVersion` in both `GearGoblin.json` and `repo.json`, commit, then:

```
git tag v0.1.0
git push origin v0.1.0
```

## Architecture

- `DalamudServices` — single static container for all `[PluginService]` injections.
- `Plugin` — entry point; constructs services, registers the command, owns the window system.
- `Configuration` — per-character `JobPlanData` (mode + BiS URL or casual preset). Persisted via `IPluginConfiguration`.
- `Services/InventoryReader` — wraps `IGameInventory` to produce typed `EquippedPiece` records with materia melds resolved against the `Materia` and `BaseParam` Lumina sheets.
- `UI/MainWindow` — tabbed: Current Gear (working), Plan (stub), Materia (stub).
