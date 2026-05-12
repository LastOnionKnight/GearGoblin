# GearGoblin

**A CharacterPanelRefined replacement that also does BiS planning, materia recommendations, and gearset export.** A Dalamud plugin.

As of v0.4.5, GearGoblin takes over the native Character window: compact derived stats per substat, breakpoint hints inline, real GCD, role-gated Tenacity / Piety rows, and a Materia Advisor section below Gear. Plus the standalone `/goblin` window for BiS comparison and detailed planning.

Reads your equipped gear, recommends materia for empty meld slots, audits existing melds for overcap and tier issues, compares against an Etro or XIVGear BiS, and now exports your gearset to the [Tonberry Tactics](https://tonberrytactics.pages.dev) companion web app for cross-platform optimization.

> **Beta status.** This plugin is in active development and **not yet published to the public Dalamud repository.** It is currently used personally by the author for testing. A public release is targeted for v1.0.0. Install instructions below assume self-build or self-host.

## What this does (v0.4.5)

**Inside the native Character window** (v0.4.5 — full CPR replacement):

GearGoblin injects derived stat data directly into the game's Character window, so you don't need to open a second UI to see your real percentages.

Under **Offensive Properties**, each substat gets a one-line compact derived row carrying chance, damage multiplier, damage-increase contribution, AND the breakpoint hint:

```
Critical Hit                       2618
   20.8% · ×1.556 · +11.6% dmg · +13→tier
Determination                      2700
   +11.3% dmg · +13→tier
Direct Hit Rate                    1608
   23.5% · +5.9% dmg · +4→tier
```

Under **Skill Speed** / **Spell Speed** (whichever your job uses), real-GCD plus combined breakpoint and speed-damage:

```
Skill Speed                         420
   GCD (real):                      2.50s
   +0.0% dmg · +22→tier
```

Under **Role Properties** — tank-only or healer-only based on your job:

```
Tenacity                          1305   (tanks)
   +2.5% dmg · -2.5% taken
Piety                              440   (healers)
   200 MP/tick
```

Under **Gear** — Materia Advisor with status counts in the header and `▶ /goblin` click-through to open the full standalone window:

```
── Materia Advisor ──   0c · 0w · 0e   ▶ /goblin
   Earrings #1 → Savage Aim Materia XII
   Necklace #1 → Heavens' Eye Materia XII
   All guaranteed slots filled · no upgrades suggested
```

**Inside the standalone `/goblin` window:**

- Stat sheet with formula-derived breakpoints (Crit, Det, DH, Speed → real GCD, Tenacity, Piety)
- Materia planner: empty-slot recommendations + audit of existing melds (wrong stat, overcap, outdated tier)
- Pure-Math vs Balance preset weighting — strict formula scores or community consensus stat priorities
- Etro and XIVGear BiS comparison: paste a URL, see slot-by-slot diff
- All 21 combat jobs covered

**Outside the game** (v0.4.1):

- `/goblinexport` slash command serializes your gearset to a Base64-encoded JSON string prefixed `GG-EXPORT:v1:` and copies it to clipboard
- Paste into Tonberry Tactics at https://tonberrytactics.pages.dev for browser-side optimization
- `/goblinimport` (planned for v0.5.0) will round-trip the optimizer's plan back into a native checklist

## CPR coexistence

GearGoblin auto-detects CharacterPanelRefined on each Character window open. When CPR is loaded:

- **Skipped** — the v0.4.5 compact derived rows (Crit chance/damage/DI, Det DI, DH chance/DI, Tenacity, Piety). These are what CPR already shows; injecting on top would double-display the same numbers.
- **Still injected** — breakpoint hints, real GCD, and the Materia Advisor. These are GearGoblin-unique and complement CPR.

To override and inject everything regardless, set `ForceDerivationsOverCpr = true` in the plugin config (Settings tab arriving in v0.4.6; for now, edit `%appdata%\XIVLauncher\pluginConfigs\GearGoblin.json`).

**Recommendation:** pick one plugin. GG v0.4.5 is a strict superset of CPR — same derived stats, plus breakpoint hints, role-gated Tenacity/Piety, real GCD, and the Materia Advisor.

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
| **Full CPR replacement (compact derived rows, Tenacity / Piety, CPR coexistence)** | ✅ done | **v0.4.5** |
| Settings tab UI for per-section toggles | 🟡 next | v0.4.6 target |
| Tonberry Tactics real optimizer (GNB Pure Math) | ✅ done | Tonberry Tactics v0.5.1 |
| Sell-vs-meld advice | 🟡 partial | audits exist; explicit sell/keep verdict missing |
| Acquisition pathing — "next upgrade is X, costs Y books, ETA Z weeks" | ❌ planned | — |
| Overmeld success probability math | ❌ planned | — |
| Multi-job tracking, weekly action summary | ❌ planned | — |
| Tonberry Tactics multi-job profiles, stat-cap awareness, Balance preset | ❌ planned | Tonberry Tactics v0.5.2+ |
| `/goblinimport` native checklist | ❌ planned | v0.5.0 target |
| Shared `GearGoblin.Core.dll` for plugin + web | ❌ planned | retires the duplicated optimizer in TT |

Three feature areas remain from the original vision: acquisition pathing, overmeld probability math, and multi-job tracking. Two scope additions beyond the original plan have shipped: native Character window injection (v0.4.0–v0.4.5) and the Tonberry Tactics companion web app (v0.4.1 → v0.5.1).

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
- `Services/StatusPanelInjector` — unsafe AtkNode injection into FFXIV's CharacterStatus addon. Patterns adapted from [CharacterPanelRefined](https://github.com/Kouzukii/ffxiv-characterstatus-refined) (MIT). v0.4.5 rewrote this from the ground up as a CPR replacement: compact derived rows per substat (chance / damage / DI / breakpoint hint on one line), role-gated Tenacity and Piety rows, CPR coexistence via `IDalamudPluginInterface.InstalledPlugins` detection. Identifies stat rows by reading their label text rather than positional sibling order (v0.4.2). See `LICENSES/CharacterPanelRefined-MIT.txt`.
- `Services/DerivationHelpers` — small static helpers introduced in v0.4.5: `CprDetection.IsCprActive()` and `DerivedStatFormatter` (string formatting for the compact derived rows, pulling raw values from `Materia/Formulas`).
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
