# Changelog

All notable changes to GearGoblin are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning loosely
follows [Semantic Versioning](https://semver.org/).

## [0.4.1] — 2026-05-11

### Added

- **`/goblinexport` slash command.** Serializes your currently-equipped
  gearset — job, level, item IDs, item names, item levels, HQ status,
  guaranteed materia slot count, overmeld permission, and every melded
  materia (slot index, materia ID, grade, derived stat name and value) — to
  a base64-encoded JSON string prefixed `GG-EXPORT:v1:` and copies it to the
  system clipboard. Designed for the Tonberry Tactics web app
  (https://tonberrytactics.pages.dev), but the payload is plain JSON and
  can also be inspected directly by base64-decoding the segment after the
  prefix. Prints a confirmation to chat with piece count and clipboard
  length; logs job, level, and JSON byte count to `/xllog`.
- **`Services/GearsetExporter.cs`.** Export logic isolated from `Plugin.cs`
  via a dedicated service class. Wire-format DTOs (`ExportPayloadV1`,
  `ExportCharacterV1`, `ExportPieceV1`, `ExportMateriaV1`) are defined as
  private nested records inside the exporter so they're decoupled from the
  internal types (`EquippedPiece`, `MateriaMeld`). Internal refactors don't
  break the export contract; schema changes bump the version segment in the
  prefix (`v1:` → `v2:`) so consumers can refuse incompatible payloads
  cleanly without trying to decode them.

### Fixed

- **v0.4.0 build break against newer Dalamud SDKs.** `StatusPanelInjector`'s
  `OnAdvisorFooterClick` handler used the older `AddonEventHandler(AddonEventType,
  nint, nint)` signature, which has since been refactored upstream to
  `AddonEventDelegate(AddonEventType, AddonEventData)` — the three loose
  pointer parameters are now bundled into a single `AddonEventData` struct.
  Anyone cloning v0.4.0 from GitHub with a current Dalamud SDK would have
  failed to build with CS1503. The handler body never actually used the
  addon/node pointers (it just calls `plugin.ToggleMain()`), so the fix is
  purely a parameter-list update.

### Notes

- Schema version 1 is the minimum useful shape for the Tonberry Tactics
  optimizer round-trip. Future versions may add buff state, party
  composition, or food/potion context as the optimizer learns to model
  them.
- The matching `/goblinimport` command — which will consume an optimizer
  plan string from clipboard and render a native AtkNode checklist in the
  Character window — remains planned for v0.5.0 once the Tonberry Tactics
  side learns to emit plan strings.
- Player-not-logged-in and no-gear-equipped fall through to chat-error
  output rather than throwing. Any unexpected serialization failure logs
  the full exception to `/xllog` and prints a short pointer to chat.

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
  header divider. Real section creation deferred to v0.4.2.
- Advisor candidates are now sorted by `MeldAudit.GainIfReplaced` and
  `MeldRecommendation.ScoreGain` descending so the highest-impact suggestions
  surface first, but the numeric gain value itself isn't displayed in the
  row to keep rows readable. Showing it (e.g. `+12.4` after the materia
  name) is a one-line tweak planned for v0.4.2.
- Advisor uses Pure Math weights internally. The standalone window's
  Balance-preset toggle is not yet propagated to the in-addon section.
- Tenacity and Piety breakpoint hints are not injected. Defensive Properties
  section has no injection at all. Both deferred to v0.4.2.

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

[0.4.1]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.4.1
[0.4.0]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.4.0
[0.3.2]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.3.2
[0.3.1]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.3.1
[0.3.0]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.3.0
