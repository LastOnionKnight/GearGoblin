# Changelog

All notable changes to GearGoblin are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning loosely
follows [Semantic Versioning](https://semver.org/).

## [0.4.0] — 2026-05-11

### Added

- **Native Character window integration.** GearGoblin now injects directly
  into FFXIV's in-game Character window (CharacterStatus addon) when the new
  `EnableNativeStatPanel` config option is on (default true).
  - **Breakpoint hints** under Crit, Determination, and Direct Hit showing
    how many more points are needed to hit the next 0.1% tier.
  - **Real GCD derivation** under Skill Speed / Spell Speed, showing the
    speed-adjusted GCD that vanilla never exposes (vanilla only shows base
    2.50s).
  - **Materia Advisor section** below the Gear panel: top 3 ranked
    recommendations from `MeldOptimizer` (wrong-stat swaps and tier upgrades),
    sorted by `GainIfReplaced` / `ScoreGain` descending so the highest-impact
    suggestions surface first. Includes a one-line status summary
    (critical / warning / empty counts) and a clickable `▶ /goblin` footer
    that opens the full standalone window.
- **Pure Math / Balance preset disclaimers** in the Materia tab's mode
  selector — short captions explaining what each mode is for, with a note
  that Pure Math doesn't model Crit's multiplier effect on Det/DH.
- **CharacterPanelRefined attribution.** `LICENSES/CharacterPanelRefined-MIT.txt`
  bundled with the plugin. The `CloneNode<T>` primitive and `AddStatRow` helper
  are adapted from CPR (MIT, Kouzukii 2022).

### Changed

- `DalamudServices` now hosts three additional services: `IAddonLifecycle`,
  `IGameGui`, `IAddonEventManager`. Required for native injection and the
  click handler on the advisor footer.
- `Plugin.ToggleMain` is now public (was private) so the in-addon advisor
  footer's click handler can invoke it.

### Known limitations

- The Materia Advisor section is rendered as a stack of `AddStatRow` calls
  below the Gear panel rather than as a true separate section with its own
  header divider. Real section creation deferred to v0.4.1.
- Advisor candidates are now sorted by `MeldAudit.GainIfReplaced` and
  `MeldRecommendation.ScoreGain` descending so the highest-impact suggestions
  surface first, but the numeric gain value itself isn't displayed in the
  row to keep rows readable. Showing it (e.g. `+12.4` after the materia
  name) is a one-line tweak planned for v0.4.1.
- Advisor uses Pure Math weights internally. The standalone window's
  Balance-preset toggle is not yet propagated to the in-addon section.
- Tenacity and Piety breakpoint hints are not injected. Defensive Properties
  section has no injection at all. Both deferred to v0.4.1.

## [0.3.2] — 2026-05-10

### Fixed

- Removed bad `EquipSlotCategory` mappings 14–17 from `InventoryReader` that
  caused crash-on-Body-collision when multiple body-combo items were equipped.
- Defensive `foreach + TryAdd` dedup on three `ToDictionary(p => p.Slot)`
  callsites that could throw if the same slot appeared twice.

## [0.3.1] — 2026-05-10

### Fixed

- **Bug A** — Etro parser no longer drops off-hand when present.
- **Bug B** — `MateriaSlotCount` now comes from the Item sheet
  (`MateriaSlotCount` + `IsAdvancedMeldingPermitted`) rather than guessing
  five phantom slots on every piece.
- **Bug C** — grade-to-tier mapping was off by four (grade 0 was assumed to
  be Tier V; actual is Tier I). Every materia previously displayed as `?`
  with `outdated tier` audits.
- **Bug D** — slot mapping now reads `EquipSlotCategory` from the Item
  sheet instead of inferring from the inventory array index, which had
  shifted when the Waist slot was removed from `GameInventory`.

## [0.3.0] — 2026-05-10

### Added

- Full advisor: Plan / Audit / BiS-diff sub-tabs in the Materia panel.
- Pure Math / Balance preset weight toggle.
- All 21 combat jobs covered with per-job stat priorities.

## [0.2.x] — 2026-05-09

### Added

- Validated stat sheet with formula-derived breakpoints.

### Fixed

- CI build setup, API 15 bump for Dalamud v15 / FFXIV 7.5.

## [0.1.x] — 2026-05-08

### Added

- Initial inventory reader, equipped-gear inspection.
- Standalone `/goblin` window.

[0.4.0]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.4.0
[0.3.2]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.3.2
[0.3.1]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.3.1
[0.3.0]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.3.0
