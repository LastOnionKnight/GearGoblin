# Changelog

All notable changes to GearGoblin are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning loosely
follows [Semantic Versioning](https://semver.org/).

## [0.4.5] — 2026-05-12

**Headline:** GearGoblin replaces CharacterPanelRefined. Same data CPR
surfaces, plus breakpoint hints, real GCD, role-gated Tenacity/Piety rows,
and the Materia Advisor — all in one plugin, with a compact layout that
fits inside the default Character window without scrolling.

If you have CPR installed, GG auto-detects it on each panel open and steps
aside (skips derived stats so you don't see them twice). You can uninstall
CPR after upgrading; GG covers everything it did. Or set
`ForceDerivationsOverCpr = true` in the config file if you want both.

### Added

- **Compact derived stat row per substat** under Offensive Properties. One
  row each for Crit, Det, DH carrying chance / damage multiplier / damage
  increase contribution AND the breakpoint hint on a single line. Example:
  `20.8% · ×1.556 · +11.6% dmg · +13→tier`. Replaces the v0.4.2 standalone
  "next tier:" rows.
- **Tenacity row** in Role Properties for tank jobs. Format:
  `+2.5% dmg · -2.5% taken`. Suppressed entirely on non-tank jobs.
- **Piety row** in Role Properties for healer jobs. Format:
  `200 MP/tick`. Suppressed entirely on non-healer jobs.
- **CPR detection** via `IDalamudPluginInterface.InstalledPlugins`.
  When `CharacterPanelRefined` is loaded, GearGoblin defaults its
  derived-stat injection OFF so the two plugins don't double-display the
  same percentages. Breakpoint hints, real GCD, and Materia Advisor still
  inject normally — those are GG-unique and worth showing alongside CPR.
- **Per-section configuration toggles**: `EnableDerivedStatInjection`
  (master), `ShowCritDerivations`, `ShowDetDerivations`,
  `ShowDhDerivations`, `ShowSpeedDerivations`, `ShowTenacityRow`,
  `ShowPietyRow`, `ForceDerivationsOverCpr`, `CompactDerivationLayout`.
  All default to sensible values (on, compact, defer to CPR).
- **First-inject chat-log signature**: on the first time the Character
  window opens in a Dalamud session, GG logs
  `StatusPanelInjector v0.4.5: first inject complete. CPR active: {bool}. Derivations enabled: {bool}.`
  You can confirm which version of the plugin is actually loaded by
  running `/xllog` and searching for `v0.4.5`. Added because v0.4.2 had
  a build-cache issue where the runtime didn't match the source.

### Changed

- **Offensive section row count is the same as v0.4.2.** v0.4.5 doesn't
  add extra rows under Crit/Det/DH; the v0.4.2 "next tier:" row is
  replaced by the new compact derived row. Three injected rows in
  Offensive Properties, just like v0.4.2.
- **Speed section consolidated.** v0.4.2 had two injected rows under
  Skill/Spell Speed (GCD real, next GCD tier). v0.4.5 keeps GCD real
  but folds the breakpoint hint and speed-damage contribution into a
  single row: `+0.1% dmg · +22→tier`. Net: one fewer row in the speed
  section.
- **StatusPanelInjector rewritten from the ground up.** v0.4.2 was a
  patch on top of v0.4.1; v0.4.5 ships a clean rewrite of
  `Services/StatusPanelInjector.cs` so partial-build state from earlier
  versions can't bleed through. All v0.4.2 bug fixes (label-walk,
  Y-position, height bump, advisor consolidation) are preserved verbatim.
- **`GearGoblin.csproj` Punchline and Description** rewritten to reflect
  CPR-replacement positioning. Tag list adds `cpr`, `character-panel`.
- **About tab** (inside the plugin's `/goblin` window) now covers v0.4.0
  through v0.4.5 properly. It was stuck on v0.3.x. Includes the new
  Refia / Aisling byline.

### Notes

- **CPR coexistence.** If both plugins are installed and loaded:
  - GearGoblin still injects breakpoint hints, real GCD, and the Materia
    Advisor section — those are GG-unique and don't conflict with CPR.
  - GearGoblin skips the new v0.4.5 compact derived rows by default to
    avoid double-display of the chance/damage/DI numbers CPR already
    shows.
  - You can override with `ForceDerivationsOverCpr = true` in the
    plugin config (will be on the Settings tab in v0.4.6).
  - Recommendation: pick one. GG is now a strict superset of CPR plus
    breakpoints, Tenacity/Piety, real GCD, and the Advisor.
- **Bug 2 status carried forward.** v0.4.2's label-walk identification
  of Crit / Det / DH components is preserved unchanged in v0.4.5.
  If you still see a missing derivation row, check `/xllog` for the
  warning `could not identify all three offensive substat rows by label`
  — that means SE changed the addon's internal node layout in a patch
  and the StartsWith() matchers need updating.

[0.4.5]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.4.5

## [0.4.2] — 2026-05-11

Bugfix release. Four issues uncovered during in-game field testing of v0.4.0
+ v0.4.1: the `▶ /goblin` advisor footer was rendering off the bottom of the
Character window, the Critical Hit breakpoint hint wasn't appearing, the
injected rows visually overlapped the vanilla content above them, and the
advisor rendered as three blank rows when no recommendations were available.
All four are fixed in `Services/StatusPanelInjector.cs`.

### Fixed

- **Off-panel `▶ /goblin` footer (bug 1).** The Materia Advisor section was
  injecting six rows (header + 3 recs + status + footer), adding 120px of
  vertical content to the Gear panel. Combined with the breakpoint-hint
  rows (+60px) and speed-derivation rows (+40px), the total +220px pushed
  the footer below the Character window's visible area. Now consolidated
  to four rows: the header row carries the status counts AND the clickable
  `▶ /goblin` glyph in its value cell, and the dedicated status and footer
  rows are retired. Saves 40px; footer no longer clips.
- **Missing Critical Hit breakpoint hint (bug 2).** `InjectBreakpointHints`
  used a positional sibling walk (`offensive->ChildNode →
  ->PrevSiblingNode → ->PrevSiblingNode`) and assumed DH-then-Det-then-Crit
  linked-list order. When that assumption broke — possibly due to extra
  nodes inserted by the addon itself or by interaction with other plugins —
  the Crit pointer landed on the wrong component, producing an orphan
  injected row at the bottom of Offensive Properties instead of a hint
  under Critical Hit. The walk now iterates all children of the offensive
  node and identifies each stat component by reading its label TextNode
  contents ("Critical Hit", "Determination", "Direct Hit"). Robust against
  reorder, extra children, and other plugins injecting into the same
  section. English client only for v0.4.2; localized matching via the
  Lumina `Addon` sheet is a follow-up.
- **Visual overlap on injected rows (bug 3).** `AddStatRow` computed the new
  row's Y coordinate as `parentNode.Height - 24` *after* bumping the
  parent's height by 20px. The result was Y = old_height - 4, which placed
  the new row's top edge 4px inside the original content's bottom edge —
  a visible 4px overlap on the first injected row under each parent.
  Changed to `parentNode.Height - 20`, which places the new row's top
  exactly at the old bottom edge. The overlap on Det/DH/etc. is gone.
- **Empty advisor showing three blank rows (bug 4).** When `MeldOptimizer`
  returned no critical/warning audits and no plan recommendations, the
  three rec rows each got set to empty strings — invisible text but still
  consuming 60px of vertical real estate. Now when there are zero
  candidates, rec1 displays "All guaranteed slots filled · no upgrades
  suggested" and rec2/rec3 stay empty. Diagnostic logging at Debug level
  also records the optimizer's result counts (`audits`, `planRecs`,
  `candidates`) on every update tick, so we can distinguish "genuinely
  optimal melds" from "silent optimizer failure" without guessing.

### Added

- `GetComponentLabelText` helper in `StatusPanelInjector` — reads a stat
  row component's label TextNode contents. Used by the new label-based
  breakpoint-hint identification (bug 2 fix). Returns null defensively if
  the component's internal node layout doesn't match expectations.

### Removed

- `advisorStatus` and `advisorFooter` field declarations in
  `StatusPanelInjector` — retired by the bug 1 consolidation. Status counts
  now live in the header row's value cell; the click handler is registered
  on the header instead of a separate footer.

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

[0.4.2]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.4.2
[0.4.1]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.4.1
[0.4.0]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.4.0
[0.3.2]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.3.2
[0.3.1]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.3.1
[0.3.0]: https://github.com/LastOnionKnight/GearGoblin/releases/tag/v0.3.0
