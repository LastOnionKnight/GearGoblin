# Changelog

All notable changes to Tonberry Tactics (the in-game plugin, formerly
"GearGoblin") are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning loosely
follows [Semantic Versioning](https://semver.org/).

**Versioning note (from v0.5.5 onward):** the plugin and the web app
(`tonberrytactics.pages.dev`) ship at the same version. Both halves of
the product bump together at every release going forward. Versions prior
to v0.5.5 used independent semver tracks — the plugin's v0.4.x line and
the web's v0.5.x line. v0.5.5 is the moment they re-align.

## [0.5.5] — 2026-05-13  "Version Alignment"

**Headline:** The plugin's version jumps from 0.4.7.1 to 0.5.5 to match
the web app, which shipped its v0.5.5 "Brand Convergence" release
today. Same content milestone — the brand convergence work landed in
both halves yesterday — now numbered to match. No more "plugin v0.4.7.1
pairs with web v0.5.4" cross-reference math; from this release forward,
both halves of Tonberry Tactics carry the same version number.

The web's v0.5.x track is older (v0.5.1 through v0.5.4 shipped over the
past weeks before today's brand convergence). The plugin had been on a
separate v0.4.x track from when it was originally branded as
"GearGoblin." With the rename complete and both halves now serving one
product identity, separate version trajectories were creating real
operational drag — release notes had to explain version pairings, users
had to remember which combination they had installed, every
cross-reference burned cycles.

This release does that alignment. From v0.5.5 onward:

- Plugin and web bump together at every release
- One CHANGELOG entry per release, mirrored on both repos
- "Tonberry Tactics v0.5.6" means both halves at v0.5.6
- v0.6.0 will be the next major (design port on web, native-injection
  visual overhaul on plugin)
- v1.0 will be the unified public Dalamud repository submission +
  custom domain web ship, with both halves at v1.0 together

### Functional changes

- **None.** This is purely a version-alignment release. Every feature
  and behavior is identical to v0.4.7.1. The DLL produces the same
  output, the windows render the same way, the slash commands resolve
  to the same handlers.

### Changed

- `<Version>` in `GearGoblin.csproj` bumped from `0.4.7.1` to `0.5.5`.
  AssemblyVersion and FileVersion bumped to match.
- `<Description>` rewritten to lead with the alignment story instead of
  the "Brand Convergence" framing (which is now retroactively part of
  this release's history).
- About tab displays version `0.5.5` automatically via reflection on
  the assembly version — no code change needed in `MainWindow.cs`.

### Not changed

- All v0.4.7.1 work (circle-logo in About tab, `/tt*` primary slash
  commands, `/goblin*` deprecated aliases, BrandResources, Quick Start
  reframing) ships through unchanged. v0.4.7.1's tag stays in history.
- Internal identifiers (`InternalName=GearGoblin`, code namespace,
  DLL filename, WindowSystem name, ImGui window ID `###GearGoblinMain`).
  Same reasoning as v0.4.7.1: renaming these would break existing user
  configs and saved window layouts. Full rename bundled with v0.6.0.
- Wire-format prefixes (`GG-EXPORT:v1:`, `GG-PLAN:v1:`).

### Files touched

- `GearGoblin.csproj` — version bump
- `CHANGELOG.md` — this entry

---

## [0.4.7.1] — 2026-05-13  "Brand Convergence"

**Headline:** The in-game plugin is renamed from **GearGoblin** to
**Tonberry Tactics**, matching the companion website. Two halves, one
product, one name on both. Internal identifiers (csproj `InternalName`,
WindowSystem name, code namespace, DLL filename) intentionally stay as
`GearGoblin` for this release — existing configs and saved window
layouts continue to load without migration. Full code-namespace rename
is scoped to v0.5.0 and will land bundled with the Core refactor.

This is a brand-only point release, parallel to Tonberry Tactics web
v0.5.5 which landed earlier today. No functional behavior changes; the
optimizer, audit, derivation, advisor, and round-trip surfaces all do
exactly what they did in v0.4.7. The intent is to make the rename
user-visible immediately so the brand-fragmentation question stops
recurring while the bigger v0.4.8 bug-fix and v0.5.0 architecture work
proceeds on their own timelines.

### Added

- **Brand artwork** in `Assets/` (shipped alongside the DLL):
  - `circle-logo.png` — wordmark logo, rendered in the About tab header
  - `rags-portrait.png` — full Refia hero portrait (reserved for v0.6.x
    About-tab body and future hero slots)
  - `rags-mini.png` — compact avatar (reserved for v0.6.x tab decorations)
- **`Services/BrandResources.cs`** — manages `IDalamudTextureWrap`
  loading via `ITextureProvider.GetFromFile(...).GetWrapOrEmpty()`.
  Defensive: missing-file and load-failure cases return null wraps and
  let the UI fall back to text. Logs at info level when assets load,
  warning level when they don't.
- **`/tt`** as the new primary slash command set: `/tt`, `/ttexport`,
  `/ttimport`, `/ttinfo`. Help text in `/xlhelp` matches.
- **About tab brand block** — circle-logo image (64×64) paired with the
  "Tonberry Tactics" wordmark + version, gracefully falling back to
  text-only if `BrandResources.CircleLogo` is null.

### Changed

- **Plugin display name** (`csproj.Name` → "Tonberry Tactics"). This is
  the name Dalamud's `/xlplugins` UI shows.
- **Window title bar** — `GearGoblin###GearGoblinMain` →
  `Tonberry Tactics###GearGoblinMain`. The `###GearGoblinMain` ImGui ID
  suffix is preserved, so saved window position, size, and docking
  state survive the rename without manual migration.
- **Quick Start tab opener** — dropped the
  `GearGoblin → Tonberry Tactics → GearGoblin` loop framing (no longer
  meaningful now that both halves carry the same name). Replaced with
  "The export–optimize–import loop, in plain English."
- **About tab body copy** — `"GearGoblin sits comfortably alongside CPR..."`
  → `"The in-game half of Tonberry Tactics ... Coexists comfortably with
  CharacterPanelRefined..."`.
- **Slash commands in help text** — every reference to `/goblininfo`,
  `/goblinexport`, etc. updated to the `/tt*` equivalents.

### Deprecated

- **`/goblin`**, **`/goblinexport`**, **`/goblininfo`**, **`/goblinimport`**
  still work but are marked `(deprecated alias of /tt...)` in `/xlhelp`.
  Migration policy: aliases stay through the entire v0.5.x line, removed
  at v1.0. Existing muscle memory keeps working through the public
  release without surprises.

### Not changed

- **Internal identifiers** — `csproj.InternalName`, code namespace
  `GearGoblin`, DLL filename `GearGoblin.dll`, WindowSystem name
  `"GearGoblin"`. Renaming any of these would break existing user
  configs (plugin config keys by `InternalName`) and saved window state.
  Full rename bundled with the v0.5.0 Core refactor.
- **Wire-format prefixes** — `GG-EXPORT:v1:`, `GG-PLAN:v1:`. Versioned
  identifiers, not brand names. Breaking them would break round-trip
  with v0.4.7 plugins in the wild.

### Known limitation

- `circle-logo.png` ships at 704KB — fine functionally, larger than
  necessary. Run through `pngquant -q 65-85` to drop ~70% before v1.0.

### Files touched

- `Plugin.cs` — `Name` property, slash command registration, BrandResources
  wiring, dispose order
- `UI/MainWindow.cs` — constructor window title, Quick Start opener,
  About tab brand header + body copy + slash-command references in
  changelog bullets
- `GearGoblin.csproj` — `<Version>0.4.7.1</Version>`, `<Name>Tonberry
  Tactics</Name>`, Description rewrite, `<Content Include="Assets\**\*.png">`
- `Services/BrandResources.cs` — NEW file
- `Assets/circle-logo.png`, `Assets/rags-portrait.png`,
  `Assets/rags-mini.png` — NEW files

---

## [0.4.7] — 2026-05-13  "Round Trip"

**Headline:** Closing the export–optimize–import loop. `/goblinexport`
shipped in v0.4.1; v0.4.7 adds `/goblinimport`, the consumer for the
`GG-PLAN:v1:` strings Tonberry Tactics emits from its optimizer.
Round-trip becomes real: equip gear → export → optimize on the web →
import the plan → tick boxes as you meld → done.

v0.4.7 also adds an in-window Feedback tab so beta reports become
triagable on arrival (pre-filled GitHub issue URL with `/goblininfo`
diagnostic block auto-attached, clipboard fallback for Discord/DM).

### Status: scaffold landed, full implementation in progress

The wire-format types, validator, command handler, and Plan-tab
schema additions are in place. What's still TODO before v0.4.7
ships:

- `GearsetImporter.ImportFromClipboard()` — clipboard read (currently
  returns "clipboard empty" because the read isn't wired).
- `GearsetImporter` persist step — write into
  `Configuration.JobPlans[contentId][jobId].ImportedPlanJson`, set
  `Mode = PlanMode.Imported`, reset `MeldCompletion`, call `Save()`.
- Plan tab "Active Plan" UI surface — show imported plan with
  per-meld checkboxes, source indicator, "Clear plan" button.
- Character-panel checklist injection (stretch goal) — surface meld
  steps below the Materia Advisor when an imported plan is active.

### Added (scaffold)

- **`Theme/TlfTheme.cs`** — Phase-1 visual port of the TLF Gear
  Division design language from Tonberry Tactics. Palette
  constants (ink, gold, lantern, Tonberry green, knife, frost,
  blood/ship/ice states, borders) ported verbatim from TT's
  `styles.css`. Composite helpers: `Push()`/`Pop()` style stacks
  for window-wide repaint, `Eyebrow("LABEL")` for `◇ TITLE`
  section heads in lantern-gold, `Advisor("LABEL")` for
  `▶ TITLE` workflow markers in gold-bright, `Pill(text, color)`
  for bracket-framed badges, `Credo(text)` for italic-feeling
  manifesto copy, and `StandingReadyFooter(version)` for the
  "◆ The Onion Knight stands ready ◆" tagline matching TT's
  Footer block.
- **TLF theme applied window-wide.** `MainWindow.Draw()` now pushes
  the theme stack at entry (via `try`/`finally` for safety) so
  window background, child frames, frame backgrounds, tab chrome,
  separators, and body text all read in the TLF palette.
  Interactive controls (buttons, checkboxes, sliders) deliberately
  keep default ImGui chrome so muscle memory survives.
- **Quick Start tab reskinned.** Top-of-tab TLF Manifesto opener
  with three offline/no-backend/round-trip pills, eyebrow labels
  on every section, `Advisor("1. EXPORT")` glyph headers on each
  loop step, `StandingReadyFooter` replacing the old plain tagline.
- **About tab reskinned.** `TLF GEAR DIVISION · OPERATIONS BRIEF`
  eyebrow header, `StandingReadyFooter` at bottom, version badge
  now TLF gold instead of legacy blue.
- **Bulk palette sweep.** Every `new Vector4(...)` literal across
  MainWindow.cs replaced with named TLF colors (`GoldBright`,
  `Lantern`, `ShipBright`, `Warning`). Single source of truth;
  swapping the design palette later is now one file's worth of
  edits.

### Phasing note

This is **Phase 1 ("TLF Lite")** of the visual alignment with
Tonberry Tactics. The web side's full design (Press Start 2P
pixel labels, VT323 retro UI, Cinzel serif, walking Tonberry
sprite, knife cursor, CRT scanlines) doesn't port cleanly to
ImGui without much heavier work:

- **Phase 2 (v0.5.x)** — custom font loading via Dalamud's
  `IFontAtlas`. Brings the actual pixel-font typography hierarchy
  into the plugin so headers feel like SNES menus and body feels
  like FFXIV.
- **Phase 3 (v0.6.x stretch)** — manifesto/lantern flourishes
  injected into the native FFXIV Character window itself, not
  just the standalone `/goblin` window. Heavy lift; low ROI;
  may never ship.

Phase 1 lands ~70% of the visual coherence at ~10% of the work,
which is the right ratio for a scaffold release.

- **`/goblinimport` slash command.** Registered. Validates a
  `GG-PLAN:v1:<base64>` clipboard string through the full pipeline
  (prefix check, base64 decode, JSON parse, schema version, emitter
  identity). Reports parse success and meld count to chat. Persist
  step is the next-build TODO.
- **`Services/GearsetImporter.cs`.** New service mirroring
  `GearsetExporter`. Public methods `ImportFromClipboard()` and
  `ImportFromString(wire)`. Returns a `PlanImportResult` carrying
  the typed payload plus any warnings.
- **`Planning/PlanSchema.cs`.** Typed C# records mirroring TT's
  `PlanPayloadV1` + `PlanMeldV1` + new `PlanCharacterV1` and
  `PlanImportResult` types. `JsonPropertyName` attributes pin the
  field mapping so the read side doesn't depend on serializer
  policy.
- **`Configuration.JobPlanData`** extended with `ImportedPlanJson`,
  `ImportedAt`, and `MeldCompletion` fields for the imported-plan
  storage. New `PlanMode.Imported` enum value alongside `Casual`
  and `Raider`.
- **Feedback tab in `/goblin` window.** Drafted during v0.4.6, held
  back to v0.4.7. Category radio (Bug / Idea / Confusion / Hi),
  multiline message, "Include diagnostic info" checkbox (on by
  default). Two submit buttons: "Open GitHub issue (pre-filled)"
  builds a `https://github.com/.../issues/new?title=...&body=...&labels=...`
  URL and opens the default browser; "Copy to clipboard for
  Discord / DM" puts the same markdown payload on the clipboard.
  No webhooks, no analytics, no auto-submit — explicit click only.

### Changed

- Quick Start tab — `/goblinimport` row drops its "(v0.4.7+)" caveat
  and gains a scaffold-state note; future-tense ("will read") becomes
  present-tense ("reads") in the IMPORT step description, with the
  Plan-tab persistence work explicitly flagged as the next-build TODO.
- About-tab About entry — adds Feedback tab and `/goblinimport`
  bullets to the v0.4.7 changelog block.

### Roadmap reminder

v0.5.0 will refactor shared materia/derivation logic into a
`GearGoblin.Core.dll` consumable by both the plugin and Tonberry
Tactics' web build, so wire-format changes only have to be made in
one place. After v0.5.0, the v0.5.x backlog includes Plan library
(multiple named BiS per job for healers with GCD A/B), "Open in
XIVGear" deep-link export, and (if feedback volume justifies it) a
Cloudflare Worker proxy that mirrors anonymized feedback submissions
to a Discord webhook server-side with rate-limiting.

---

## [0.4.6] — 2026-05-13

**Headline:** "Coexistence." GearGoblin runs alongside CharacterPanelRefined
out of the box. CPR provides substat derivations, GG contributes the
Materia Advisor, real GCD when CPR isn't job-aware, the Tonberry Tactics
export pipeline, and a Diagnostics surface for verifying what actually
injected.

The v0.4.5 framing was "GG replaces CPR." Field testing showed otherwise:
both plugins running cleanly, with GG detecting CPR and stepping aside on
derivations as designed — but the Materia Advisor that v0.4.5 promised
would "still inject normally" was nowhere to be seen on the Character
panel. v0.4.6 finds and fixes the bug, then makes coexistence first-class
instead of a fallback path.

### Fixed

- **Materia Advisor now visible when CPR is active.** Root cause:
  `AddStatRow` grew the parent component's height by 20px per added row,
  but never grew the *outer* addon's RootNode height. With CPR coexisting,
  CPR adds ~12 rows above us (Offensive Properties + Speed + Recast),
  pushing GG's 4 advisor rows in the gear section past the addon's clip
  boundary. The rows were being injected — they just rendered below the
  visible window. Fix: track total injected height across every
  `AddStatRow` call, then bump `characterStatusPtr->RootNode->Height` by
  that amount once `InjectAllRows` completes. Also bumps full-width child
  nodes (background, window frame) so the visible bordered area grows in
  step. ([symptom: 13:43 /xllog 2026-05-12](#))
- **Empty-advisor empty state confirmed visible.** Near-BiS gearsets like
  Refia's SAM iLvl 771 produce zero recommendations. The v0.4.2 design
  for an "All guaranteed slots filled · no upgrades suggested" row was
  intact in v0.4.5 but invisible because of the same clip-boundary bug.
  With v0.4.6's height-grow fix, the empty-state row now renders.

### Added

- **Instrumented advisor logging.** Replaces the v0.4.5 aspirational
  `"Materia Advisor will still inject normally"` line with concrete
  status output:
  ```
  StatusPanelInjector v0.4.6: Materia Advisor inject attempt. Rows OK:
    header=True rec1=True rec2=True rec3=True. Height added: 80px.
    Section present: True.
  StatusPanelInjector v0.4.6: outer addon grown 720px → 800px (+80px)
    to fit injected rows.
  StatusPanelInjector v0.4.6: Materia Advisor updated. Pieces: 13.
    Audits: crit=0 warn=0. PlanRecs: 0. Rendered: 0 candidate(s) —
    empty-state row shown.
  ```
  The verbose per-update line is gated by the new
  `EnableVerboseInjectorLogging` Configuration toggle (default true for
  v0.4.6 to make field verification easy).
- **Settings tab in `/goblin` window.** All eight derivation toggles
  from v0.4.5 surfaced as ImGui checkboxes — `EnableNativeStatPanel`,
  `EnableDerivedStatInjection`, `ShowCritDerivations`,
  `ShowDetDerivations`, `ShowDhDerivations`, `ShowSpeedDerivations`,
  `ShowTenacityRow`, `ShowPietyRow`, `ForceDerivationsOverCpr`,
  `CompactDerivationLayout`, plus the new
  `EnableVerboseInjectorLogging`. Previously config-file-only at
  `%appdata%\XIVLauncher\pluginConfigs\GearGoblin.json`. The per-stat
  toggles are greyed out with explanatory text when CPR is detected
  and the force-override is off, so users can see exactly what's
  injecting.
- **Quick Start tab in `/goblin` window.** Added as the FIRST tab so
  new users opening `/goblin` for the first time land on a workflow
  guide instead of a gear table they don't yet know how to interpret.
  Covers the export–optimize–import loop in plain English, the
  GG-EXPORT-as-portable-save-file mental model, the four slash
  commands with descriptions, what to expect in the Character window
  with and without CPR, the bug-report flow via `/goblininfo`, and
  tips pointing at the other tabs. Direct response to field testing
  where multiple users got confused by the GG-EXPORT base64 string
  and asked "what is this scary thing." Content lives in code rather
  than an external help file so it ships with the plugin and stays
  in sync with whatever version is actually loaded.
- **Diagnostics tab in `/goblin` window.** Live read of the
  `StatusPanelInjector.DiagnosticSnapshot` — panel attached, CPR
  detected, derivations enabled, advisor section present, advisor rec
  count, advisor empty-state flag, advisor errored flag, outer-addon
  height growth, last inject result string, last inject timestamp, last
  update timestamp. Plus two buttons:
  - **"Force Reinject"** — re-runs `UpdateAllValues` without requiring
    the user to close and reopen the Character window. Useful when
    debugging meld changes.
  - **"Copy /goblininfo to clipboard"** — copies the same diagnostic
    block that `/goblininfo` prints, formatted for pasting into a
    GitHub issue.
- **`/goblininfo` slash command.** Prints the diagnostic snapshot to
  chat in a copy-paste-friendly fenced block. Bug reports go from
  "send a screenshot + your /xllog" to "paste your /goblininfo."
- **`Plugin.BuildGoblinInfoString()`** public method. Single source of
  truth for the diagnostic block — used by both `/goblininfo` and the
  Diagnostics tab's clipboard button. ~15 lines covering plugin
  version, player/job/level, full DiagnosticSnapshot, and a closing
  instruction to attach `/xllog` lines for bug reports.
- **`StatusPanelInjector.ForceReinject()`** public method. Re-runs
  the value-update pass against the existing injected nodes. Does not
  re-run `AddStatRow` (that would duplicate cloned nodes).
- **`StatusPanelInjector.GetDiagnostics()`** public method returning a
  `DiagnosticSnapshot` record struct. Used by both the UI tab and the
  slash command.

### Changed

- **About tab** rewritten with v0.4.6 entry at top describing the
  bug fix, instrumentation work, and new tabs. Refia / Aisling /
  Last Onion Knight byline preserved.
- **`Configuration.EnableVerboseInjectorLogging`** — new bool, defaults
  to `true`. Existing configs upgrading from v0.4.5 will pick this up
  via the property initializer (the field is absent from their
  persisted JSON, so the default applies).
- **`StatusPanelInjector.AddStatRow`** changed from `static` to
  instance method so it can track `totalInjectedHeight` across calls.
  Behavior identical otherwise. All call sites in the injector remain
  valid since they were already inside the class.
- **`Plugin.cs`** version-bump comments and a new lineage section
  marking v0.4.6's `/goblininfo` addition.
- **Description in `GearGoblin.csproj`** rewritten to lead with the
  coexistence framing and the v0.4.5 → v0.4.6 bug-fix narrative.
  Discoverability for new users hitting Dalamud's plugin browser.

### Build / tooling

- **`release.ps1`: dotnet build gate.** Inserted between git-state
  check and commit-message generation. Runs
  `dotnet build --configuration Release --nologo` and bails on
  non-zero exit. Catches the unclosed-`</div>` class of bugs locally
  instead of letting them reach origin/main and only fail in CI or
  Cloudflare's build queue. New `-SkipBuild` flag bypasses for fast
  iteration when the user has already verified the build manually.
- **`release.ps1`: BOM fix on commit-message file.** Replaced
  `Set-Content -Path $msgFile -Value $Message -Encoding UTF8` (which
  emits a UTF-8 BOM under PowerShell 5.1) with
  `[System.IO.File]::WriteAllText($msgFile, $Message,
  [System.Text.UTF8Encoding]::new($false))`. Eliminates the
  `﻿GearGoblin 0.4.5`-style invisible-BOM prefix that was showing
  up in `git log` and on GitHub's web UI.
- **`README-DROPIN.md`: Unblock-File step.** New section 0 covers
  the NTFS Zone.Identifier ADS that Windows attaches to downloaded
  zips, which PowerShell's ExecutionPolicy treats as a script-run
  block. Running `Get-ChildItem -Recurse | Unblock-File` once after
  extracting the zip strips the mark and unblocks `release.ps1`. The
  /xllog evidence didn't surface this but the friction was real
  during the v0.4.5 release attempt.
- **`README-DROPIN.md`** also gets a fresh file list, verification
  flow, rollback instructions, and a section walking through the new
  `/goblininfo` slash command.

### Known carry-forward (not blocking)

- Stray `_redirects` and `build.sh` at the GearGoblin repo root
  (residue from a Tonberry Tactics deploy mishap weeks back).
  Cosmetic; ignore or delete in a follow-up commit.
- Bibo+ texture warnings in `/xllog`. Penumbra-side mod metadata,
  not a GearGoblin concern.

### Roadmap teaser

v0.4.7 "Round Trip" will add `/goblinimport` to consume the
`GG-PLAN:v1:` strings Tonberry Tactics emits — completing the
export-optimize-import loop with an actionable meld checklist on
the Plan tab. The Diagnostics tab in v0.4.6 lays groundwork for
surfacing import status the same way it surfaces injection status
now. v0.4.7 will also ship a **Feedback tab** in the `/goblin`
window — pre-filled GitHub issue URL plus clipboard-fallback for
Discord/DM, with the `/goblininfo` block auto-attached. (Drafted
during v0.4.6 but held back to keep this ship focused on the bug
fix.)

v0.5.0 will refactor shared logic into a `GearGoblin.Core.dll`
consumable by both the plugin and the Tonberry Tactics web build,
so wire-format changes only have to be made in one place.

---

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
