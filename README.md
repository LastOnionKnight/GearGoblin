# GearGoblin

**A Materia Advisor + BiS planner + gearset exporter for FFXIV that coexists with CharacterPanelRefined.** A Dalamud plugin.

As of **v0.4.6 ("Coexistence")**, GearGoblin runs alongside CharacterPanelRefined out of the box. CPR brings the substat derivations; GearGoblin contributes the Materia Advisor (in-panel, click-through to /goblin), real GCD when CPR doesn't supply a job-aware variant, breakpoint hints, the Tonberry Tactics export pipeline, and a Diagnostics tab + `/goblininfo` slash command for verifying what actually injected. CPR is optional — when it's not installed, GG also provides the full derivation suite itself.

**v0.4.7 ("Round Trip") is currently in development.** Closes the export–optimize–import loop with `/goblinimport` (consumes the `GG-PLAN:v1:` strings Tonberry Tactics emits, surfaces a per-meld checklist on the Plan tab) and adds an in-window Feedback tab for triagable beta reports. Scaffolding for both is in the source tree; the persistence layer and Plan-tab checklist UI are the next-build TODOs.

Reads your equipped gear, recommends materia for empty meld slots, audits existing melds for overcap and tier issues, compares against an Etro or XIVGear BiS, and exports your gearset to the [Tonberry Tactics](https://tonberrytactics.pages.dev) companion web app for cross-platform optimization.

> **Beta status.** This plugin is in active development and **not yet published to the public Dalamud repository.** It is currently used personally by the author for testing. A public release is targeted for v1.0.0. Install instructions below assume self-build or self-host.

## What this does today

> Shipped features as of v0.4.6. v0.4.7 round-trip work is in scaffold (see roadmap below).

**Inside the native Character window:**

GearGoblin injects derived data and a Materia Advisor section directly into the game's Character window, so you don't need to open a second UI to see your real percentages or upgrade suggestions.

When **CPR is also installed** (recommended setup), GG defers derivations to CPR and contributes only:

```
── Materia Advisor ──   0c · 0w · 0e   ▶ /goblin
   All guaranteed slots filled · no upgrades suggested
```

(or up to three concrete recommendations if your gear has audit issues).

When **CPR is not installed**, GG provides the full derivation suite itself. Under **Offensive Properties**, each substat gets a compact derived row carrying chance, damage multiplier, damage-increase contribution, AND the breakpoint hint:

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

**Inside the standalone `/goblin` window:**

- Stat sheet with formula-derived breakpoints (Crit, Det, DH, Speed → real GCD, Tenacity, Piety)
- Materia planner: empty-slot recommendations + audit of existing melds (wrong stat, overcap, outdated tier)
- Pure-Math vs Balance preset weighting — strict formula scores or community consensus stat priorities
- Etro and XIVGear BiS comparison: paste a URL, see slot-by-slot diff
- **Settings tab** (new in v0.4.6) — all derivation toggles as checkboxes, per-stat toggles greyed out when CPR is active
- **Diagnostics tab** (new in v0.4.6) — live injector state, "Force Reinject" button, copy-to-clipboard for bug reports
- All 21 combat jobs covered

**Outside the game** (v0.4.1):

- `/goblinexport` slash command serializes your gearset to a Base64-encoded JSON string prefixed `GG-EXPORT:v1:` and copies it to clipboard
- Paste into Tonberry Tactics at https://tonberrytactics.pages.dev for browser-side optimization
- `/goblinimport` (v0.4.7, scaffold landed) is the consumer for `GG-PLAN:v1:` strings — full persistence + checklist UI in the next-build TODO

**Diagnostics** (new in v0.4.6):

- `/goblininfo` slash command prints the current injector state to chat in a copy-paste block. Same payload as the Diagnostics tab's clipboard button. Use it when reporting bugs.

## CPR coexistence

The default and recommended setup is **both plugins installed.** GearGoblin auto-detects CharacterPanelRefined on each Character window open. When CPR is loaded:

- **Deferred to CPR** — the compact derived rows (Crit chance/damage/DI, Det DI, DH chance/DI, Tenacity, Piety). These are what CPR already shows.
- **Still injected by GG** — breakpoint hints, real GCD when CPR doesn't supply a job-aware variant, and the Materia Advisor. These are GearGoblin-unique.

To override and have GG inject derivations even when CPR is active (will double-display rows), open the new **Settings tab** in `/goblin` and check "Force GG derivations even when CPR is active." Or edit `%appdata%\XIVLauncher\pluginConfigs\GearGoblin.json` and set `"ForceDerivationsOverCpr": true`.

If you want a CPR-free setup, uninstall CPR — GG provides the full derivation suite itself in that case.

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
| Full CPR replacement (compact derived rows, Tenacity / Piety, CPR coexistence) | ✅ done | v0.4.5 |
| Materia Advisor visibility fix under CPR + Settings tab + Diagnostics tab + /goblininfo | ✅ done | v0.4.6 |
| Tonberry Tactics real optimizer (GNB Pure Math) | ✅ done | Tonberry Tactics v0.5.1 |
| **Quick Start tab — first-time-user workflow guide** | ✅ done | **v0.4.6** |
| **`/goblinimport` round-trip from `GG-PLAN:v1:` — scaffold + validator** | 🟡 scaffold | **v0.4.7 (current)** — persist + checklist UI are next-build TODOs |
| **Feedback tab — pre-filled GitHub issue + Discord clipboard fallback** | ✅ done | **v0.4.7 (current)** |
| Sell-vs-meld advice | 🟡 partial | audits exist; explicit sell/keep verdict missing |
| Acquisition pathing — "next upgrade is X, costs Y books, ETA Z weeks" | ❌ planned | — |
| Overmeld success probability math | ❌ planned | — |
| Multi-job tracking, weekly action summary | ❌ planned | — |
| Tonberry Tactics multi-job profiles, stat-cap awareness, Balance preset | ❌ planned | Tonberry Tactics v0.5.2+ |
| Shared `GearGoblin.Core.dll` for plugin + web | ❌ planned | **v0.5.0 target** — retires the duplicated optimizer in TT |
| Plan library: multiple named BiS per job (SCH 2.40/2.31/omni-healer) | ❌ planned | v0.5.x backlog — healer-shaped feature |
| "Open in XIVGear" deep-link export | ❌ planned | v0.6.x backlog — reuses existing XIVGear-fetch path |

Three feature areas remain from the original vision: acquisition pathing, overmeld probability math, and multi-job tracking. Two scope additions beyond the original plan have shipped: native Character window injection (v0.4.0–v0.4.6) and the Tonberry Tactics companion web app (v0.4.1 → v0.5.3). v0.4.7 closes the round-trip from web back to game; v0.5.0 unifies the duplicated optimizer logic between plugin and web build.

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
- `Services/StatusPanelInjector` — unsafe AtkNode injection into FFXIV's CharacterStatus addon. Patterns adapted from [CharacterPanelRefined](https://github.com/Kouzukii/ffxiv-characterstatus-refined) (MIT). v0.4.5 rewrote this from the ground up as a CPR replacement: compact derived rows per substat (chance / damage / DI / breakpoint hint on one line), role-gated Tenacity and Piety rows, CPR coexistence via `IDalamudPluginInterface.InstalledPlugins` detection. Identifies stat rows by reading their label text rather than positional sibling order (v0.4.2). v0.4.6 fixed the advisor-visibility bug by tracking total injected height (`AddStatRow` is now an instance method that accumulates a counter) and growing the outer `characterStatusPtr->RootNode->Height` after `InjectAllRows` completes. v0.4.6 also exposes a public `DiagnosticSnapshot` for the Diagnostics tab and `/goblininfo` command. See `LICENSES/CharacterPanelRefined-MIT.txt`.
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
