# GearGoblin

BiS planner, materia advisor, and gearset exporter for Final Fantasy XIV. A Dalamud plugin.

Reads your equipped gear, recommends materia for empty meld slots, audits existing melds for overcap and tier issues, compares against an Etro or XIVGear BiS, and injects derived stat data directly into the game's native Character window.

Now also exports your gearset to the [Tonberry Tactics](https://tonberrytactics.pages.dev) companion web app for cross-platform optimization — as of v0.5.1, with a real GNB Pure-Math optimizer rather than mock data.

> **Beta status.** This plugin is in active development and **not yet published to the public Dalamud repository.** It is currently used personally by the author for testing. A public release is targeted for v1.0.0. Install instructions below assume self-build or self-host.

## What this does (v0.4.2)

**Inside the game window:**

- Reads your currently-equipped gear, every melded materia, average item level
- Stat sheet with formula-derived breakpoints (Crit, Det, DH, Speed → real GCD, Tenacity, Piety)
- Materia planner: empty-slot recommendations + audit of existing melds (wrong stat, overcap, outdated tier)
- Pure-Math vs Balance preset weighting — toggle between strict formula scores and community consensus stat priorities
- Etro and XIVGear BiS comparison: paste a URL, see slot-by-slot diff
- All 21 combat jobs covered with per-job stat priorities

**Inside the native Character window** (v0.4.0, polished in v0.4.2):

- Breakpoint hints injected under Crit / Det / DH showing how many more points are needed for the next 0.1% tier
- Real speed-adjusted GCD derived under Skill Speed / Spell Speed (vanilla only shows base 2.50s)
- Materia Advisor section below the Gear panel: top 3 recommendations ranked by gain
- Clickable header opens the full standalone `/goblin` window — status counts and the clickable glyph now sit on the same row so the section fits inside the Character window's visible area on every resolution

**Outside the game** (v0.4.1):

- `/goblinexport` slash command serializes your gearset to a Base64-encoded JSON string prefixed `GG-EXPORT:v1:` and copies it to clipboard
- Paste into Tonberry Tactics at https://tonberrytactics.pages.dev for browser-side optimization (real GNB Pure-Math output, Tier XII recommendations, with a round-trip `GG-PLAN:v1:` string back)
- `/goblinimport` (planned for v0.5.0) will round-trip the optimizer's plan back into a native checklist inside the Character window

## What changed in v0.4.2

Bugfix-only release covering four issues uncovered during in-game field testing of v0.4.0+v0.4.1's native panel integration:

1. **Off-panel `▶ /goblin` footer.** The Materia Advisor was injecting six rows (header + 3 recs + status + footer), pushing the footer below the bottom of the Character window. Consolidated to four rows — the header row now carries status counts AND the clickable `▶ /goblin` glyph in its value cell.
2. **Missing Critical Hit breakpoint hint.** The positional sibling walk in `InjectBreakpointHints` could land on the wrong stat parent if anything reordered children of the Offensive Properties section. Now identifies each stat row by reading its label text ("Critical Hit", "Determination", "Direct Hit").
3. **Visual overlap on Det/DH rows.** `AddStatRow` placed the new row 4px above the previous content's bottom edge. Changed Y offset from `-24` to `-20` so the new row sits flush.
4. **Empty advisor showing three blank rows.** When the optimizer returned no recommendations, the three rec rows were set to empty strings but still consumed vertical space. Now shows `"All guaranteed slots filled · no upgrades suggested"` in rec1. Diagnostic logging at `Debug` level also records optimizer counts so silent failures get caught.

See `CHANGELOG.md` for the full entry.

## Feature status

The original roadmap was scoped as version goals. Actual development numbered chronologically, so version numbers no longer map directly to roadmap milestones. Where we are by feature area:

| Feature area | Status | Where |
|---|---|---|
| Inventory reader, equipped gear, materia inspection | ✅ done | v0.1.x |
| Etro / XIVGear URL paste + slot-by-slot diff | ✅ done | v0.3.0 — Plan tab |
| Casual presets, per-job stat priorities (21 jobs) | ✅ done | v0.3.0 — Materia tab |
| Breakpoint awareness | ✅ done | v0.4.0 — native Character window |
| Tonberry Tactics export pipeline | ✅ done | v0.4.1 — `/goblinexport` |
| Native-panel injection bugs (overlap, footer clip, missing Crit hint, empty advisor UX) | ✅ done | v0.4.2 |
| Tonberry Tactics real optimizer (GNB Pure Math) | ✅ done | Tonberry Tactics v0.5.1 |
| Sell-vs-meld advice | 🟡 partial | audits exist; explicit sell/keep verdict missing |
| Acquisition pathing — "next upgrade is X, costs Y books, ETA Z weeks" | ❌ planned | — |
| Overmeld success probability math | ❌ planned | — |
| Multi-job tracking, weekly action summary | ❌ planned | — |
| Tonberry Tactics multi-job profiles, stat-cap awareness, Balance preset | ❌ planned | Tonberry Tactics v0.5.2+ |
| `/goblinimport` native checklist | ❌ planned | v0.5.0 target |
| Shared `GearGoblin.Core.dll` for plugin + web | ❌ planned | retires the duplicated optimizer in TT |

Three feature areas remain from the original vision: acquisition pathing, overmeld probability math, and multi-job tracking. Two scope additions beyond the original plan have shipped: native Character window injection (v0.4.0–v0.4.2) and the Tonberry Tactics companion web app (v0.4.1 → v0.5.1).

## Companion: Tonberry Tactics

Tonberry Tactics is a Blazor WebAssembly web app deployed to Cloudflare Pages at https://tonberrytactics.pages.dev. It consumes `GG-EXPORT:v1:` strings produced by GearGoblin's `/goblinexport` and emits `GG-PLAN:v1:` strings for the planned `/goblinimport`.

As of TT v0.5.1, the web app runs a real GNB Pure-Math optimizer (hardcoded job priority list, Tier XII recommendations) rather than mock data. Multi-job profiles, stat-cap awareness, and the Balance preset toggle are queued for v0.5.2+. The longer-term plan is to extract `GearGoblin.Core.dll` as a shared assembly compiling for both .NET and Wasm, retiring the duplicated optimizer code in TT.

Why offload to a web app? Browser-side optimization runs on phones and tablets without launching the game, supports shareable plan links, and decouples the heavy combinatorial materia search from FFXIV's frame budget.

## Build (for developers)

Requires the Dalamud dev libs at `%appdata%\XIVLauncher\addon\Hooks\dev\` (the default path Dalamud installs to). Or set `DALAMUD_HOME` to override.

```
dotnet restore
dotnet build -c Release
```

For local testing, point Dalamud at the build output: `/xlsettings` → **Experimental** → **Dev Plugin Locations** → add the path to your `bin\Release\net10.0-windows` folder. Then `/xlplugins` → **Dev Tools** → load.

## Install (personal beta — pre v1.0.0)

Until v1.0.0, GearGoblin is not on the public Dalamud repository. To install for personal use during beta, point Dalamud at this repo's `repo.json`:

In-game, type `/xlsettings` → **Experimental** tab → **Custom Plugin Repositories** → add this URL:

```
https://raw.githubusercontent.com/LastOnionKnight/GearGoblin/main/repo.json
```

Hit save. Then `/xlplugins` → search for **GearGoblin** → install.

When v1.0.0 ships, this URL will be retired in favor of submission to the official Dalamud plugin repo.

## Architecture

- `DalamudServices` — static container for all `[PluginService]` injections. Includes `IAddonLifecycle`, `IGameGui`, and `IAddonEventManager` for native injection.
- `Plugin` — entry point; constructs services, registers `/goblin` and `/goblinexport` commands, owns the window system and the StatusPanelInjector.
- `Configuration` — per-character, per-job plan data (mode + BiS URL or casual preset), BiS response cache, native-panel toggle. Persisted via `IPluginConfiguration`.
- `Services/InventoryReader` — wraps `IGameInventory` to produce typed `EquippedPiece` records with materia melds resolved against the `Materia` and `BaseParam` Lumina sheets. Maps slots via `EquipSlotCategory` from the Item sheet (not inventory array index — that shifted when Waist was removed).
- `Services/GearsetExporter` — serializes the equipped gearset to a Base64-encoded JSON string with versioned wire format (`GG-EXPORT:v1:`). Wire-format DTOs are decoupled from internal types so schema versions bump cleanly.
- `Services/StatusPanelInjector` — unsafe AtkNode injection into FFXIV's CharacterStatus addon. Patterns adapted from [CharacterPanelRefined](https://github.com/Kouzukii/ffxiv-characterstatus-refined) (MIT). Identifies stat rows by reading their label text (v0.4.2) rather than positional sibling order. See `LICENSES/CharacterPanelRefined-MIT.txt`.
- `Materia/MeldOptimizer` — wrong-stat swaps, tier upgrades, overcap audits. Sorted by score gain descending.
- `Materia/JobProfile` — per-job stat weightings for all 21 combat jobs, Pure Math derived and Balance preset variants.
- `Materia/StatCaps`, `Materia/LevelTable`, `Materia/Formulas` — substat formula derivation from public datamining sources.
- `Planning/EtroParser`, `Planning/XivGearParser`, `Planning/BisFetcher` — paste a BiS URL, fetch the gearset, compare slot-by-slot.
- `UI/MainWindow` — tabbed: Current Gear, Plan, Materia, About.
- `UI/MateriaTab` — Stat Sheet / Plan / Audit sub-modes with Pure Math vs Balance preset toggle.
- `UI/PlanTab` — Etro/XIVGear URL paste with BiS diff rendering.

## Releases

Tagged versions trigger a GitHub Actions build that produces `latest.zip` containing the plugin DLL and manifest. Dalamud's `repo.json` points at the latest release.

Release flow (v0.4.2 onward — unified script):

1. Apply v0.X.Y dropin or commit changes directly
2. Bump `<AssemblyVersion>`, `<FileVersion>`, `<Version>` in `GearGoblin.csproj`
3. Add a CHANGELOG entry for the new version
4. Run `.\release.ps1` from the project root — auto-detects version from csproj, generates the commit message from the matching CHANGELOG entry, tags `vX.Y.Z`, pushes with `--follow-tags`

The same `release.ps1` lives in both this repo and the Tonberry Tactics repo. Use `-DryRun` to preview, `-SkipPush` to commit and tag locally without pushing.

## Credits

- **CharacterPanelRefined** (MIT, Kouzukii 2022) — `CloneNode<T>` primitive and `AddStatRow` helper, adapted for `StatusPanelInjector`. Full license at `LICENSES/CharacterPanelRefined-MIT.txt`.
- **Akhmorning Allagan Studies** and the **FFXIV datamining repo** — substat formulas, stat tier values, level table coefficients.
- **The Balance Discord** — community stat priority consensus underlying the Balance preset weightings.

## License

TBD — currently unreleased software. License to be added prior to v1.0.0.
