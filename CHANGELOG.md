## [0.6.6.4] — Materia tab merge (Fork 1) + Current Gear tab purge

### Changed
- **Materia tab default landing now merges Stat Sheet + Plan** on one scroll surface. The three-radio sub-tab selector (`Stat Sheet` / `Plan` / `Audit`) is gone. The default view stacks `Current Substats` (4-col table) above `Recommended Fills` (4-col table), with the Pure-math mode disclaimer rendered as an inline italic caption above the fills.
- **Two small toggles right-aligned in the header line:**
  - `[Audit ▸]` / `[◀ Back to gear]` — swaps the body to the Audit view (severity counts + 4-col audit table) and back. Active state renders in Lantern color so the user can see the toggle is "armed."
  - `[Pure math]` / `[Balance preset]` — flips the optimizer's weighting; re-renders the Plan (or Audit) in place. Pure math is default; Balance preset persists for the session.
- **Cross-tab focus signal expanded.** CharacterTab's `See full audit in Materia tab →` link now sets BOTH `CharacterTab.WantsMateriaTabFocus` (tab focus) and `MateriaTab.WantsAuditOnNextDraw` (view selection). Single-click from the Character tab's Materia Advisor footer now lands the user directly in the Audit view. `WantsAuditOnNextDraw` is consumed on the next Draw and resets to false the same frame.

### Removed
- **`Current Gear` tab** — fully subsumed by the Character tab's `Equipped Gear` section (shipped in v0.6.6.0). Both render the same Slot / Item / iLvl / Materia table; the Current Gear tab's `Average Item Level: XXX` header line is already shown in the Character tab's right-rail pill (`iLvl 777`). Zero information loss. The `DrawCurrentGear()` method is deleted; the file-header tab-list comment in `MainWindow.cs` is updated.

### Why this version
Closes the v0.6.6.x Character-tab + Materia-tab polish arc on the TlfTheme (gold/navy) era. The four-pass character tab polish (StatsStrip v0.6.6.1, CharacterHero v0.6.6.2, Materia Advisor v0.6.6.3, gear-table polish deferred to v0.6.6.5) and now the Materia tab merge are all that's left of the Track 1 visual language. v0.6.7 begins the Track 2 (ember/frost-blue) era with the new Plan tab paste UI as the first surface in the unified design language.

### Build-gate risks
- **`MateriaTab.WantsAuditOnNextDraw` is `internal static`** — same access level pattern as `CharacterTab.WantsMateriaTabFocus`. CharacterTab can set it because they're in the same assembly (`GearGoblin.UI` namespace). If access fails, change to `public static`.
- **`ImGui.SameLine(float positionX)` for right-aligning the toggle pair** — confirmed pattern from v0.6.6.1's StatsStrip cards. Falls back gracefully to a new line via the explicit `ImGui.NewLine()` branch when the window is too narrow.

### Verify in-game (`/xlplugins` toggle off/on)
1. **Tab strip** — `Character | Quick Start | Plan | Materia | Settings | Diagnostics | Feedback | About`. No `Current Gear` tab.
2. **Materia tab default view** — opens to Stat Sheet table on top, Recommended Fills below. No radio buttons at top.
3. **Right-aligned toggles** — `[Pure math]` and `[Audit ▸]` (smaller, on the same line as `{Job} Lv {Lv} ({Role})`).
4. **Click `[Audit ▸]`** — body swaps to severity counts + audit table. Toggle label flips to `[◀ Back to gear]` and turns gold (Lantern color). Click again to return.
5. **Click `[Pure math]`** — flips to `[Balance preset]`, the mode disclaimer caption above the fills table changes to the Balance preset version, and the Plan table re-orders by the new weighting.
6. **Cross-tab path** — switch to Character tab, click `See full audit in Materia tab →` on the Materia Advisor card footer. Should land directly on the Audit view (not the default Stat Sheet + Plan view).

## [0.6.6.3] — Character tab polish pass 3: Materia Advisor ranked rows

### Added
- **Materia Advisor card** — section 4.3 rebuilt per Claude Design v0.2.0's `MateriaAdvisorCard.jsx`. Three states:
  - **Populated** — up to 3 ranked rows, each with a pixel-font rank prefix (`00`/`01`/`02` in GoldDim), slot label in Knife color, direction arrow in Lantern (`→` for audit replacements, `←` for plan additions), materia/replacement spec in Frost, and a right-aligned gain badge in pixel font + Ice color showing the calibrated marginal percentage gain (e.g. `+0.42%`).
  - **Empty** — ◆ Ship-green glyph + italic Garamond message: "All guaranteed slots filled · no upgrades suggested".
  - **Errored** — defensive fallback preserved from v0.6.6.0 (shows exception type + `/xllog` hint).
- **Cross-tab focus link** — the advisor card's footer now contains a clickable "See full audit in Materia tab →" link (Lantern text, hover-background suppressed so it reads as a hyperlink, not a button). Clicking it sets `CharacterTab.WantsMateriaTabFocus`; `MainWindow.cs`'s tab strip consumes the flag in the next frame and passes `ImGuiTabItemFlags.SetSelected` to the Materia tab's `BeginTabItem` for one frame.
- **`FormatGain(double score)` helper** — converts the optimizer's weighted marginal percentage score into a human-readable badge string. Negative or zero scores render empty (never shows "+0.00%").
- **`AdvisorRecModel` record** — strongly-typed candidate row replaces the v0.6.6.0 `List<string>` formatting. Same build logic (top-3 audits by `GainIfReplaced`, falling back to `PlanRecommendations` by `ScoreGain` to fill 3 rows), now produces structured data for the renderer.

### Changed
- `DrawMateriaAdvisor` signature now takes `Plugin plugin` as its first parameter (needed for `plugin.Fonts.Pixel` / `plugin.Fonts.GaramondItalic` access). Call site in `CharacterTab.Draw()` updated accordingly.
- Card uses `BeginChild` with calculated height (`96f` empty, `50f + recCount*30f + 50f` populated) so the card doesn't gobble vertical space when there's nothing to recommend.

### Why this version
Closes the Character tab's three large-surface polish arc:
- v0.6.6.1 — StatsStrip cards
- v0.6.6.2 — CharacterHero portrait frame + identity column
- v0.6.6.3 — Materia Advisor ranked rows ← this ship

v0.6.6.4 polishes the gear table (striped rows, gold-tier highlight, HQ ★, Soul Crystal divider) to close the Character tab's TlfTheme era. v0.6.7 begins the Track 2 (ember/frost-blue) repaint with the new Plan tab paste UI as the first surface in the v1.0 design language; the Character tab itself is repainted in v0.7.0 alongside the StatusPanelInjector removal.

### Build-gate risks
- **`ImGui.BeginTabItem(string, ImGuiTabItemFlags)`** — first use of the 2-arg overload in this codebase. If it fails to resolve, fallback is the 3-arg form `BeginTabItem(string, ref bool, ImGuiTabItemFlags)` with a `bool open = true` shim.
- **`ImGui.Selectable` returning `true` on click** — standard ImGui pattern, low risk.
- **`cardHeight` dynamic sizing of `BeginChild`** — verified pattern from v0.6.6.1 StatsStrip cards.

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
## [0.6.6.2] — 2026-05-19  (Character tab polish II: CharacterHero portrait frame + identity column)

> **Status:** Released. Second polish pass on the Character tab introduced
> in v0.6.6.0 and partially polished in v0.6.6.1. The Adventurer Plate
> aesthetic now reaches the hero region — portrait frame, corner brackets,
> jobAbbr fallback glyph, and a real identity column with name / world /
> FC tag.

### New — CharacterHero portrait region (`UI/CharacterTab.cs`)

`DrawHero` is rebuilt. The old plain-text `"{name} — {profile.Name} Lv {lvl}"`
line is gone. In its place:

- **Portrait frame.** 148px × 166px region anchored at the cursor on
  the left side of the hero block. Filled background in `InkPanel`,
  thin border in `BorderPixelLite`. Drawn via `drawList.AddRectFilled`
  + `drawList.AddRect` against the window's foreground draw list — no
  `BeginChild`, no nested layout, simple primitives.
- **Four lantern-gold corner brackets.** 6×6 filled squares inset 2px
  from each corner. Per the Claude Design v0.2.0 ImGui port flag #1:
  "four 6×6 `--lantern` squares at the portrait corners ... draw four
  solid filled rectangles in `ImGui.GetWindowDrawList()` after laying
  out the portrait region." That's exactly what happens — small helper
  `DrawCornerSquare` keeps the four calls readable.
- **Centered jobAbbr fallback glyph.** Press Start 2P at 32px, drawn
  via `drawList.AddText` at the geometric center of the frame
  (`CalcTextSize` measured inside the font scope, then offset by half
  the difference from the frame size). Color: `GoldDim`. Dalamud
  doesn't expose Adventurer Plate portraits to plugins, so this is the
  portrait surface for now. A future iteration could lookup the job's
  stone icon via `ITextureProvider`'s GameIcon path and overlay it, but
  the 32px text glyph reads cleanly on its own.
- **Identity column** to the right of the portrait, 18px gap. Renders
  player name in `CinzelHeader` (22px Cinzel Regular, `GoldBright`),
  class line in default font (`"{profile.Name} · Lv {player.Level}"`,
  `FrostSoft`), uppercased world string in `Pixel` font (`FrostDim`),
  and — if the player is in a Free Company — the FC tag wrapped in
  guillemets in `Pixel` (`TonberryBright`).

### New — `PixelDisplay` font handle (`Theme/FontAtlasManager.cs`)

`PixelDisplay` (Press Start 2P @ 32px) registered alongside the
existing `Pixel` (10px) handle. Same .ttf file (`PressStart2P-Regular.ttf`
already on disk in `Assets/Fonts/`), only a new atlas size. Loaded-font
count goes 6 → 7. `Dispose` order updated (PixelDisplay disposed first,
reverse construction). Startup log line updated to v0.6.6.2.

### Defensive Lumina row access

Three `Safe*` helpers wrap the Lumina-row accessor calls that reach
into excel data via `RowRef<T>.Value`:

```csharp
SafeJobAbbr(player) → player.ClassJob.Value.Abbreviation.ExtractText()
SafeWorld(player)   → player.HomeWorld.Value.Name.ExtractText()
SafeFcTag(player)   → player.CompanyTag.ToString()
```

Each is try/catch-wrapped so a transient RowId == 0 state (loading
screens, the first frame after a class swap, brief world-server hiccups)
degrades to a fallback string instead of throwing a `NullReferenceException`
on the player's first frame back. JobAbbr falls back to `"???"`; world
and FC tag fall back to empty strings (and the corresponding lines are
suppressed entirely when empty).

### Color packing — manual `ColorToU32` helper

The `drawList.AddRectFilled` / `AddRect` / `AddText` primitives take
packed `uint` colors, but `TlfTheme`'s palette is in `Vector4` (linear
RGBA 0..1). A private `ColorToU32(Vector4) → uint` helper does the
conversion in-class. Manual rather than via `ImGui.GetColorU32` or
`ImGui.ColorConvertFloat4ToU32` so the code is portable across binding
generations — both methods exist in some ImGui.NET / Dalamud.Bindings.ImGui
flavors, but their exact signatures and overload sets vary.

### Pairing

- **GearGoblin.Core v0.6.6.2** — version-only lockstep bump.
- **TonberryTactics web v0.6.6.2** — version-only lockstep bump.

### What's still deliberately deferred

- **Adventurer Plate portrait texture access.** Would need a Dalamud
  API surface we don't currently see — `IPluginInterface` exposes
  `ITextureProvider` for GameIcons but not for Plate portraits. Could
  be revisited if FFXIVClientStructs exposes the texture pointer.
- **Job-stone icon overlay** inside the portrait frame. Would use
  `ITextureProvider.GetFromGameIcon(jobStoneIconId).GetWrapOrEmpty()`
  and then `ImGui.Image()` to render the icon above the jobAbbr
  fallback. Punted because the jobAbbr-only path looks fine on its own
  for now and adding image rendering touches a new API surface that
  deserves its own polish pass.
- **Materia Advisor ranked rows + gain badges** — v0.6.6.3.
- **Gear table stripes + gold-tier highlight + HQ star + Soul Crystal divider** — v0.6.6.4.
- **StatusPanelInjector deprecation surfacing** — v0.6.7.
- **Plan tab paste UI + persistence** — v0.6.7 Theme 3.
- **`repo.json` Name "GearGoblin" → "Tonberry Tactics" correction** — still queued, bundle with release.ps1 regex fix.

---

## [0.6.6.1] — 2026-05-19  (Character tab polish I: StatsStrip cards + BUG-003)

> **Status:** Released. First polish pass on the Character tab introduced
> in v0.6.6.0. Card-based StatsStrip lands per the Claude Design v0.2.0
> spec; BUG-003 (latent `KeyNotFoundException` in MeldOptimizer on
> materia with unrecognized stat types) is now closed.

### New — StatsStrip card layout (`UI/CharacterTab.cs`)

The plain `ImGui.Text` row dump from v0.6.6.0's skeleton is replaced
with a horizontal card grid. Each substat renders as its own vertical
card with four content layers:

- **Label** — Press Start 2P @ 10px (`Pixel` font handle from
  `FontAtlasManager`), GoldDim, uppercased. Matches the design
  deliverable's `.stat-card .lbl` styling.
- **Value** — Cinzel Regular @ 22px (`CinzelHeader` handle),
  GoldBright. Slightly smaller than the design's 28px spec — closest
  size already registered in the font atlas. JetBrains Mono / larger
  Cinzel sizes deferred to a later atlas pass if appetite emerges.
- **Derived effect line** — default Dalamud font, FrostSoft. Pulls
  from existing `DerivedStatFormatter` (`CritCompact`, `DetCompact`,
  `DhCompact`, `TenacityCompact`, `PietyMpPerTick`) so the per-stat
  effect strings match what the StatusPanelInjector renders on the
  native panel — single source of truth for the math.
- **Tier / breakpoint hint** — placeholder for now ("min substat (job
  baseline)" on speed cards, empty on others). The "next tier: +N stat
  → +X% rate (at YYYY)" breakpoint math from the design spec lands in
  a v0.6.6.x polish pass once we settle the rendering convention.

Cards are laid out via `ImGui.BeginTable("##stats_strip", N,
SizingStretchSame)` — one row, N columns, each column an equal share
of the available width. Card chrome (background, border, padding) comes
from `BeginChild` with `ChildBg = InkPanelAlt`, `Border = BorderPixelLite`,
`WindowPadding = 12px`. All color tokens pulled from existing
`Theme/TlfTheme.cs` constants — no new tokens introduced.

#### Warn-chip on speed stats

Cards display a warn-chip footer (Press Start 2P, Warning color,
bordered) when the corresponding speed stat exceeds the 420 baseline
(level-100 sub-floor). The card's border also swaps to Warning,
matching the design's `.stat-card.has-warn` rule. Heuristic is
deliberately crude — flags ANY value above 420, regardless of job.
A more nuanced per-job behavior ("BLM melds heavily into SpS for
2.45s GCD targets; RDM does not; tank GNB melds away from SkS")
lands in v0.6.6.x once we have a job-aware speed-meld profile.

#### Crafter / Gatherer handling

The role-gated code from v0.6.6.0 (which already dropped the speed-stat
and Tenacity/Piety rows for `Role.Crafter` / `Role.Gatherer`) is
augmented with an explicit early-return path: when no relevant battle
stats apply, the StatsStrip renders a single disabled-text line —
`"Battle stats not applicable for this class."` — instead of three
near-meaningless 420 placeholder values. The Character tab as a whole
still renders the hero, advisor (empty-state), and gear table sections
for crafters, which remains useful for confirming what's equipped.

#### Signature change

`CharacterTab.Draw(InventoryReader, IPlayerCharacter)` → `Draw(Plugin,
IPlayerCharacter)`. The tab now pulls inventory from `plugin.Inventory`
internally and gains access to `plugin.Fonts` for the font-stack push.
Single call site in `UI/MainWindow.cs` updated.

### Closed — BUG-003 (MeldOptimizer KeyNotFoundException)

Three-line guard in `Materia/MeldOptimizer.cs` `GenerateAudits`:

```csharp
if (current.Stat == Substat.None) continue;
```

inserted immediately after the existing `IsEmpty || Current is null`
short-circuit. `MateriaCatalog.StatNameToSubstat()` returns
`Substat.None` for materia whose game-side stat-name strings don't map
to any of the seven substats the advisor reasons about (CriticalHit,
Determination, DirectHit, SkillSpeed, SpellSpeed, Tenacity, Piety).
Without the guard, `AuditSingleMeld`'s downstream lookup at
`totals[current.Stat]` throws on missing-key — `totals` is initialized
with the seven recognized substats only.

The defensive `try/catch` around `MeldOptimizer.Optimize` in
`CharacterTab.DrawMateriaAdvisor` (added in v0.6.6.0) was the immediate
safety net; this fix removes the root cause. The catch block stays as
belt-and-suspenders for any future optimizer crashes from unrelated
paths.

### Pairing

- **GearGoblin.Core v0.6.6.1** — version-only lockstep bump.
- **TonberryTactics web v0.6.6.1** — version-only lockstep bump.

### What's still deliberately deferred

- **Next-tier breakpoint math** in StatCard `Tier` field. The design's
  "next tier: +N stat → +X% rate (at YYYY)" rendering needs decisions
  about which breakpoint to surface (rate floor, damage floor, GCD
  step) and how to weight cross-stat trade-offs. v0.6.6.x.
- **Per-job speed-meld profile** for a smarter warn-chip. Requires
  augmenting `JobProfile` with a "speed posture" field
  (`MeldsInto` / `MeldsAwayFrom` / `TargetsGcd`). v0.6.6.x.
- **JetBrains Mono** font atlas registration for derived/tier lines.
  The design spec wants it; we're using the default font for those
  rows. Will land if the visual mismatch becomes a real complaint.
- **CharacterHero portrait + corner brackets** — v0.6.6.2.
- **Advisor ranked rows + gain badges** — v0.6.6.3.
- **Gear table stripes + gold-tier highlight** — v0.6.6.4.

---

## [0.6.6.0] — 2026-05-19  (Character tab introduction; BUG-001/002 closeouts)

> **Status:** Released. v0.6.6.0 is the first tagged release of the new
> Character-tab era and the formal closeout of the BUG-001 / BUG-002
> saga that dominated the v0.6.5.x patch stream. The Character tab
> ships in skeleton form — live data wired through, visual polish
> layered in across the v0.6.6.x line.

> **Strategic pivot:** v0.6.6 begins the migration away from the
> StatusPanelInjector approach. Months of injection-related bugs
> (BUG-001 cloned-cell text overflow, BUG-002 right-aligned-overflow on
> long advisor strings, CharacterPanelRefined incompatibility, addon
> lifecycle event misses) all share the same root cause: we do not own
> the native Character panel's node tree, so every coercion of it into
> displaying our data hits a new edge case in cell geometry, alignment
> inheritance, or initialization timing. The pivot replaces that
> approach with a dedicated Character tab inside our own ImGui window,
> where we control every pixel. The injection codepath remains shipped
> in this version for backward compatibility and side-by-side validation;
> v0.6.7 marks it deprecated in `/ttinfo`; v0.7.0 removes it entirely.

### Closed — BUG-001 (Materia Advisor header ghost text)

**Verified dead in in-game testing on 2026-05-18 (and reconfirmed
2026-05-19).** Refia Rakkiri on Gunbreaker, iLvl 780, CPR disabled.
The Materia Advisor row injected below "Average Item Level: 780"
renders cleanly: `── Materia Advisor` label on the left, `▶ /tt` pill
on the right, no ghost characters, no overflow. Two independent
screenshot captures across two sessions.

Root cause confirmation: H6 was right. The cloned label cell inherits
geometry from the original ILVL row's number cell (sized for "780",
~30px wide), then the existing `advisorHeader->SetText()` call wrote a
~17-char pill string into that narrow cell with right-alignment. The
right-aligned text overflowed *leftward* past the cell boundary, into
the label cell's render zone, producing the visible "ghost." The fix
that landed in `Services/StatusPanelInjector.cs`: both the with-audits
and empty-state branches of `advisorHeader->SetText()` now write just
`"▶ /tt"` (5 chars), which fits the cell without overflowing.

H1 (bidirectional sibling-link patch, v0.6.5.3a) was a real artifact
of cloning AtkResNode sibling pointers but did not cause the ghost.
H2 (collision-node parameter, v0.6.5.3) was the right structural
change for cell collision detection but unrelated to text overflow.
H6-A (label shortening, v0.6.5.4) confirmed the direction by shrinking
the ghost dramatically but mis-attributed the overflow source as the
label cell rather than the number cell.

Eight months. Three wrong hypotheses. One screenshot confirms the kill.

### Closed — BUG-002 (Empty-state and candidate row overflow)

A second symptom of the same architectural flaw, hidden behind
BUG-001's worse rendering. The 51-character empty-state string
`"All guaranteed slots filled · no upgrades suggested"` was being
written into `advisorRec1`'s number cell (right-aligned, ~30px wide).
After BUG-001 went away, BUG-002 became visible: the empty-state
sentence spilled leftward past the entire Character panel boundary.

The structural rule established: **descriptive payload belongs in the
left-aligned label cell, short tags belong in the right-aligned
number cell.** Codified in `Services/StatusPanelInjector.cs`:

- `SetAdvisorRow(numberNode, numberText, labelText)` — new signature
  with optional `labelText` parameter. When provided, walks the
  number cell's `PrevSiblingNode` chain (with a defensive
  `NodeType.Text` guard) and `SetText`s the label cell. Old
  signature's implicit `?? "—"` em-dash fallback removed; callers
  now explicitly pass `""` for empty cells.
- Empty state: descriptive sentence in label cell, number cells empty.
- Candidate state: each candidate string in label cell, number cells
  empty. Same rule applies whether candidates come from audits
  (`"{slot} #{idx} → {repl}"`) or plan recommendations
  (`"{slot} #{idx} ← {mat}"`).
- `ClearAdvisorRows()`: placeholder text routed to label cell.

Verified dead in the same in-game tests that closed BUG-001.

### New — Character tab (`UI/CharacterTab.cs`)

Brand new file. The first iteration of what becomes the StatusPanel
Injector's replacement. Registered in `MainWindow.cs` as the first
tab in `BeginTabBar("##goblintabs")`, positioned ahead of Quick Start.

Four sections wired to live data:

- **Hero region**: player name, class line, iLvl. Skeleton renders
  plain text. Visual polish (portrait frame, Adventurer Plate
  aesthetic, FC tag, world/DC strip) comes in subsequent passes.
- **Stats strip**: substat values displayed one-per-line. Role-gated
  (Sks vs Sps based on job's primary speed stat; Tenacity for tanks).
  Skeleton is plain `ImGui.Text` rows. Visual polish (4-5 stat cards
  in a horizontal grid, derived-effect math display, next-tier
  breakpoint hints, warning chip on speed for jobs that meld away
  from it) comes next.
- **Materia advisor inline**: top 3 recommendations or empty-state
  line. Logic mirrors `StatusPanelInjector.UpdateAdvisor`'s
  candidate-build (audits-by-gain, plan-recs fallback) so users see
  the same recommendations in both surfaces during the transition.
  Skeleton renders as plain text rows. Visual polish (ranked rows,
  gain-badge, "See full audit in Materia tab →" link) comes next.
- **Gear table**: lifted verbatim from `DrawCurrentGear()`. Same
  columns, same data source, parallel rendering. The existing
  "Current Gear" tab still works; this duplicates it for now until
  Current Gear is removed (or repurposed) in a later pass.

Defensive: `MeldOptimizer.Optimize()` is wrapped in try/catch so a
single bad meld (BUG-003 dormant — `KeyNotFoundException` on
`Substat.None` materia lookups) doesn't crash the whole tab. The
catch renders an "Advisor errored: {ExceptionType}" line with a
pointer to `/xllog`. BUG-003's actual guard fix lands in v0.6.6.1.

Design source: `character-tab/` (Claude Design deliverable v0.2.0),
which lives outside this repo but is referenced in each section's
TODO block. The deliverable's README enumerates 12 explicit ImGui
port flags — those are the spec for the polish passes.

### Pairing

- **GearGoblin.Core v0.6.6.0** — version-only lockstep bump. The
  optimizer logic the new tab calls is already in Core; no API
  changes.
- **TonberryTactics web v0.6.6.0** — version-only lockstep bump. The
  web app's existing Adventurer card design in `Pages/Index.razor`
  remains the visual reference target for the plugin port.

### What's deliberately deferred to v0.6.6.x

- BUG-003 guard (`Substat.None` lookup skip in
  `MeldOptimizer.AuditSingleMeld`). Dormant — only fires on
  unrecognized materia types, which Refia's GNB loadout doesn't
  trigger. Lands in v0.6.6.1 alongside the StatsStrip visual polish.
- CharacterPanelRefined coexistence skip in StatusPanelInjector. Not
  blocking now that the Character tab works independently of CPR.
- Crafter/Gatherer class handling in the new tab's StatsStrip and
  Materia Advisor sections. DoH/DoL classes don't have meaningful
  battle substats; the skeleton currently shows the placeholder
  values the game returns. A "Battle stats not applicable for crafter
  classes" render path lands in v0.6.6.1.
- Removal/deprecation of Quick Start tab. Stays in the tab strip
  through v0.6.6.x; absorbed into About in a later pass.
- Removal of duplicate "Current Gear" tab. The Character tab's gear
  section duplicates it; consolidation lands once Character tab
  visual polish is locked.

---

## [0.6.5.4] — 2026-05-18  (H6 candidate — BUG-001 attempt #4)

> **Status:** Released to test hypothesis H6. Whether this actually
> resolves BUG-001 is pending in-game verification. The prior three
> swings (v0.6.5.2, v0.6.5.3, v0.6.5.3a) each tested a hypothesis that
> turned out to be wrong; this is attempt #4. Awaiting Brian's thumbs
> up on the panel render before we declare done.

> **Versioning note:** Pure numeric versioning going forward. The
> letter-suffix experiment (v0.6.5.3a) is closed — caused friction with
> release.ps1's tag-from-csproj-Version flow and the github-actions
> repo.json bot's semver parsing. The `<InformationalVersion>` property
> has been removed from GearGoblin.csproj. `UI/MainWindow.cs
> ResolveVersion()` retains the InformationalVersion-preferring code
> path as dormant infrastructure for future use but it is not actively
> populated.

**Targets:** BUG-001 (Materia Advisor header ghost text).

### Hypothesis (H6)

The cloned label cell in our `AddStatRow` inherits the original ILVL
row's `TextAlignment` and `Width` properties (we clone byte-for-byte,
allocate a fresh string buffer, but never touch the geometry fields).
The original label is "Average Item Level" — fits its allotted cell.
Our injected label "── Materia Advisor ──" (with em-dashes) may be
wider than that cell, causing rightward text overflow into the number
cell's render zone. Both cells render at the same Y; their texts
collide visually. CharacterPanelRefined's `ilvlSync` injection works
fine because their text fits the inherited cell geometry. Our use
case puts longer content in a cell sized for shorter content.

### Intervention (H6-A test)

Single change:

- **`Services/StatusPanelInjector.cs InjectAdvisorSection`** —
  shortened the advisor header label argument passed to
  `AddStatRow`. Was `"── Materia Advisor ──"` (21 chars, em-dashes);
  now `"Materia Advisor"` (15 chars, no decorations).

If H6 is correct, the ghost text on the advisor header should go away
or shrink dramatically. If the ghost persists with the shorter label,
H6 is wrong and we move to the next hypothesis (likely H7: explicit
width-setting or buffer zero-initialization).

### Bonus consistency fix

- **`Plugin.cs BuildGoblinInfoString`** — `/ttinfo`'s "Plugin version"
  line now reads through `UI.MainWindow.ResolveVersion()` instead of
  duplicating the version-formatter logic locally. Surfaces the same
  display version everywhere (header pill, About tab, `/ttinfo`).
  Discovered during v0.6.5.3a testing — the header pill correctly
  showed "v0.6.5.3a" while `/ttinfo` printed "v0.6.5.4" (the underlying
  numeric AssemblyVersion) because the two read from different code
  paths. `ResolveVersion()`'s visibility changed from `private static`
  to `internal static` to enable the shared call.

### Pairing

- **GearGoblin.Core v0.6.5.4** — version-only lockstep bump.
- **TonberryTactics web v0.6.5.4** — version-only lockstep bump.

### If this doesn't work

This CHANGELOG entry gets an addendum noting H6's result, and v0.6.5.5
tries the next hypothesis. Most likely H7 (zero-init fresh buffers in
`NodeUtil.AllocateFreshTextBuffer`) or H8 (explicit Width-setting on
cloned cells — the H6-B "proper fix" path if H6-A confirms but the
short label is felt to be a design regression).

### Out of scope (still deferred)

- Mobile site work — slides to v0.6.6 from the original v0.6.5.4 slot
- Round-trip closure (Plan tab paste UI, persistence, checklist)
- README refreshes across the three GitHub repos
- Stale `/goblin*` references in StatusPanelInjector.cs code comments

---

## [0.6.5.3a] — 2026-05-17  "H1 candidate"  *(superseded by 0.6.5.4)*

> **Outcome:** Pushed for in-game verification on 2026-05-17. In-game
> test on 2026-05-18 confirmed the ghost text PERSISTS after the H1
> fix — bidirectional sibling-link patch was not the cause. Bug pattern
> changed slightly (different garbage characters in the overlap) but
> the bug remained. H1 ELIMINATED from the hypothesis ranking. The
> AddStatRow change (removing the bidirectional patch) is RETAINED
> in v0.6.5.4 — it aligns our code with CharacterPanelRefined's
> upstream pattern verbatim and didn't break anything we can identify.

**Pushed to origin/main** via manual git tag (skipped release.ps1 to
allow the letter-suffix tag). Letter-suffix versioning was deprecated
on the same day (Option 3): v0.6.5.4 is the next iteration, numeric.

> **Status:** This entry documents a candidate build staged for testing.
> It has not been released. The fix proposed below has NOT yet been
> verified to resolve BUG-001 in-game. Do not treat this as a shipped
> release until Brian gives an explicit thumbs-up and the version is
> formally tagged on origin/main. The "UNRELEASED" date placeholder
> will be replaced with the actual release date upon verification.

> **Versioning note:** The "a" suffix signals a contained patch on top
> of v0.6.5.3, distinct from a regular x-bump. v0.6.5.4 is reserved for
> the Tonberry Tactics mobile site (see scoping doc). AssemblyVersion
> ticks forward numerically to 0.6.5.4 because .NET AssemblyVersion
> cannot carry letter suffixes (CS7034); the human-facing string
> "0.6.5.3a" lives in `<InformationalVersion>` and `ResolveVersion()`
> reads from there for all user-visible display.

**Targets:** BUG-001 (Materia Advisor header ghost text) — the same
character-panel rendering bug that v0.6.5.2 ("Panel Polish") and
v0.6.5.3 ("Collision Fix") both attempted and missed. This is attempt
three. See `BUG_HUNT_AND_ROADMAP.md` for the experiment-plan context.

### Intervention

A single change, applied with single-intervention discipline (no
opportunistic items in this candidate):

- **`Services/StatusPanelInjector.cs` AddStatRow** — removed the
  two-line bidirectional sibling-link patch:
  ```csharp
  if (prevSiblingBeforeLabel != null)
      prevSiblingBeforeLabel->NextSiblingNode = (AtkResNode*)newLabelNode;
  ```
  This patch had been in place since v0.4.0 to keep the new node's
  `NextSibling` chain consistent with its `PrevSibling` chain after
  insertion. It is not present in CharacterPanelRefined, the upstream
  reference implementation we adapted `AddStatRow` from.

### Plumbing changes (related but separate from the bug fix)

- **`GearGoblin.csproj`** — added `<InformationalVersion>0.6.5.3a</InformationalVersion>`
  and `<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>`.
  AssemblyVersion / FileVersion / Version remain numeric at 0.6.5.4.
- **`UI/MainWindow.cs ResolveVersion()`** — now prefers
  `AssemblyInformationalVersionAttribute.InformationalVersion` when set,
  falls back to AssemblyVersion-based formatting otherwise. Strips any
  `+SourceRevisionId` suffix the SDK might append (belt-and-suspenders
  alongside the csproj setting above).

### Hypothesis (H1 from CPR_DEEP_DIVE.md)

`UldManager.UpdateDrawNodeList()` rebuilds the component's draw-order
NodeList by walking sibling chains. If it walks PrevSibling and
NextSibling separately and enumerates nodes from both directions, our
bidirectional patch makes cloned label nodes reachable through two
paths. Each clone enters NodeList twice, gets rendered twice per frame
at slightly different draw priorities. The visible ghost-text on the
Materia Advisor header (visible since the section was added; misdiagnosed
twice as collision-node / pre-pad issues) matches this signature.

CPR's `AddStatRow` only updates PrevSibling. Aligning our code with
CPR's pattern verbatim should resolve the bug — if H1 is correct.

### Verification gate (definition of done)

This candidate ships as v0.6.5.3a only when all six criteria from
BUG-001's definition of done are met:

1. Character-panel advisor header renders cleanly on VPR, no ghost text
2. The fix is a single intervention (this is — only AddStatRow touched)
3. Root cause is articulable (yes — H1 documented in code comment + CPR_DEEP_DIVE.md)
4. Tested on combat job (VPR/PLD) AND crafter job (CRP)
5. Tested with CharacterPanelRefined both enabled and disabled
6. `/ttinfo` reports clean inject state with no warnings or errors

Additional v0.6.5.3a-specific verification:

7. In-game header version pill renders as `v0.6.5.3a` (not `v0.6.5.4`)
   — validates the InformationalVersion plumbing
8. About-tab footer reads `in-game plugin · v0.6.5.3a`
9. `/ttinfo` output's version line reads `v0.6.5.3a`

### If this doesn't work

This CHANGELOG entry gets revised with a "didn't work" note, the patch
is restored, and we move to H2 (Gear section repositioning). The
experiment plan in BUG_HUNT_AND_ROADMAP.md drives next steps.

### Pairing

- **GearGoblin.Core** stays at v0.6.5.3 until this fix is verified.
  Lockstep ship happens only on confirmed-working v0.6.5.3a.
- **TonberryTactics web** stays at v0.6.5.3 for the same reason.

### Explicitly NOT in this candidate

- Stale `/goblin*` references in `StatusPanelInjector.cs` code comments
  (deferred — comment cleanup is risk-free but expands the diff, which
  muddies the experiment signal)
- README refreshes (v0.6.6)
- Any non-AddStatRow code path changes
- Mobile site work (scoped for v0.6.5.4 — see TT_MOBILE_SCOPE.md)

---

## [0.6.5.3] — 2026-05-16  "Collision Fix"

v0.6.5.2 "Panel Polish" misdiagnosed the character-panel ghost-text bug
and shipped the wrong fix. v0.6.5.3 ships the right one. Plugin = Core =
Web = v0.6.5.3 (Core and Web are version-only lockstep bumps; no source
changes on those two).

### What was wrong

The Materia Advisor section, injected into the "Average Item Level"
component below the Gear header, rendered with ghost-text artifacts on
its "Materia Advisor" header line. Symptom looked like the title text
was being drawn twice at slightly offset Y positions. Visible across
every job's Character window (combat: VPR, PLD; crafter: CRP — tested
in-game).

v0.6.5.2 attempted to fix this by pre-padding the Gear component's
parent + collision node by 20px before the first advisor row injection.
That made the bug worse: the operation that causes the ghost text IS
growing the collision node, and the pre-pad added 20 more pixels of
collision growth on top of the 80px that the 4 advisor rows were
already adding (each row's existing `collisionNode->Height += 20`).
Stack of 100px of collision growth stretched the ILVL row's text node
across the whole advisor section.

### How we found it

Cloned `Kouzukii/ffxiv-characterstatus-refined` (CharacterPanelRefined,
MIT — the upstream we adapted `AddStatRow` from) and compared their
implementation against ours. CPR's `AddStatRow` signature carries a
fourth parameter, `expandCollisionNode = true`, that ours dropped during
the adaptation. CPR explicitly passes `expandCollisionNode: false` for
every row injected into the Average Item Level component and the
crafter-stats components (CP, GP). Without that flag our adaptation
always grew the collision node, which the Gear / ILVL parent doesn't
tolerate.

### Fixed

- **`Services/StatusPanelInjector.cs AddStatRow`** — added
  `expandCollisionNode = true` as a fourth parameter. Body gates the
  `collisionNode->Height += 20` line behind the flag. Default `true`
  preserves behavior at every existing call site (Crit, Det, DH, Speed,
  Tenacity, Piety — none change). `totalInjectedHeight` accumulator
  unchanged: outer addon RootNode still needs to grow to accommodate
  visible content regardless of the immediate parent's collision
  behavior.
- **`Services/StatusPanelInjector.cs InjectAdvisorSection`** — removed
  the v0.6.5.2 pre-pad block (parent + collision +20px before the first
  AddStatRow). All 4 advisor rows now pass `expandCollisionNode: false`
  to suppress collision growth while still extending the parent
  component's visible height. This matches the CPR pattern for the
  Gear / Average Item Level component.

### Changed

- **`Services/StatusPanelInjector.cs`** — advisor pill text and click
  handler reference `/tt` instead of `/goblin`. Four call sites:
  has-audits branch pill (line 739), empty/error branch pill (line
  761), `ProcessCommand("/tt")` on click (line 770), error log message
  (line 774). This is a brand-convergence fix the v0.5.x sweep missed
  in this file. `/goblin` remains registered as a legacy command alias
  in `Plugin.cs` — clicking the pill now invokes the primary `/tt`
  rather than the legacy form.

### Pairing

- **GearGoblin.Core v0.6.5.3** — version-only bump. No source changes.
- **TonberryTactics web v0.6.5.3** — version-only bump. In-page version
  strings updated in `Pages/Index.razor`. No functional changes.

### Out of scope (deferred to v0.6.6)

- Plan tab `GG-PLAN:v1:` paste UI + `Configuration.JobPlans` persistence.
- README refreshes across the three GitHub repos.
- Stale `/goblin*` references in `StatusPanelInjector.cs` code comments
  (functional references all fixed; comments are historical record).

---

## [0.6.5.2] — 2026-05-15  "Panel Polish" *(re-tagged)*

**Important:** This release re-tags v0.6.5.2 with eight polish fixes that
should have shipped in the original v0.6.5.2 build. The previous v0.6.5.2
tag (`fc42885`) was deleted from origin and recreated on the polished
commit. Lockstep preserved: Plugin = Core = Web = v0.6.5.2.

### Why the re-tag

The original v0.6.5.2 ship was release-infra only and didn't touch any
user-visible code path. Loading the v0.6.5.2 DLL in-game produced a
plugin that still LOOKED like v0.6.5 in several ways:

- Header version pill displayed `v0.6.5` (formatter truncated the
  Revision component).
- Refresh button in the header was a placeholder stub that did nothing.
- About-tab "What's New" section had no v0.6.5.2 entry.
- `/ttinfo` diagnostic block still headered itself as
  `GearGoblin /goblininfo` (legacy branding the v0.6.5 sweep missed).
- Character-panel advisor row still rendered with the ILVL ghost
  overlay (visible in Refia's VPR and CRP panels).
- `BrandResources.TryLoad` warnings still logged at every plugin
  startup ("Not on main thread!"), with the icons silently failing
  to load.

Bumping to v0.6.5.3 would have broken the lockstep convention against
Core and Web (both at v0.6.5.2). Re-tagging v0.6.5.2 keeps all three
in sync while landing the polish work.

### Fixed

- **Version badge formatter** (`UI/MainWindow.cs ResolveVersion()`):
  was `$"{v.Major}.{v.Minor}.{v.Build}"`, silently dropping the
  Revision component for patch-level releases. Now emits four
  components when Revision is non-zero, three when not. v0.6.5 still
  reads "0.6.5"; v0.6.5.2 now correctly reads "0.6.5.2". Propagates
  to every site that reads `s_versionString`: header badge,
  standing-ready footer, Feedback tab title prefix, "in-game plugin"
  subtitle.
- **Refresh button** (`UI/MainWindow.cs`): was a placeholder stub
  with empty body since v0.6.0. Now forces an explicit
  `InventoryReader.ReadEquipped()` call, captures a timestamp, and
  renders a 2-second "✓ refreshed" confirmation label inline with
  the button. Label fades from full ice-cyan opacity to transparent
  across the 2-second window so the click registers visually.
  Read exception (rare; player object weird state during loading
  screens) caught defensively so the UI never breaks on a refresh.
- **Character-panel advisor row offset**
  (`Services/StatusPanelInjector.cs InjectAdvisorSection()`): the
  first advisor row injected at a Y that overlapped the "Average
  Item Level" text above it, producing ghost-text artifacts in both
  combat (VPR/PLD) and crafter (CRP) panels. Subsequent rows
  rendered cleanly because each AddStatRow grows the parent height
  by 20px before placing its row. The fix pre-pads the parent
  component by 20px once before the first AddStatRow call, creating
  the same buffer space the subsequent rows already enjoyed.
- **BrandResources thread affinity**
  (`Services/BrandResources.cs`): constructor synchronously called
  `TextureProvider.GetFromFile(path).GetWrapOrEmpty()`, which
  requires the render-thread ImGui/texture context. When the
  constructor ran off the framework thread (typical during plugin
  startup), Dalamud logged three "Not on main thread!" warnings
  (one per asset) and all three loads returned null — plugin silently
  fell back to text-only branding for the whole session. Now the
  three loads are queued onto the next framework tick via
  `Framework.RunOnFrameworkThread`; assets populate within one
  frame of plugin load. Existing callers already null-check.
- **PlanTab.cs:96 CS4014 build warning** (`UI/PlanTab.cs`): explicit
  `_ =` discard added to the `Framework.RunOnFrameworkThread(...)`
  call inside the BiS-fetcher fire-and-forget Task. The pattern was
  intentional — we don't want to await the framework dispatch from
  inside a Task.Run that's already off the main thread — but the
  compiler couldn't infer that without the discard.

### Changed

- **`/ttinfo` diagnostic block** (`Plugin.cs BuildGoblinInfoString()`):
  header reads `───── Tonberry Tactics /ttinfo ─────` instead of the
  stale `───── GearGoblin /goblininfo ─────`. Footer drops the
  hardcoded `v0.4.6` reference in favor of a generic search term
  ('StatusPanelInjector' or 'BrandResources') that works across
  versions. Version line now respects the same 4-component
  formatting as the header badge.
- **About-tab "What's New"** (`UI/MainWindow.cs DrawAbout()`):
  v0.6.5.2 entry replaces both the previous v0.6.5.1 ("Quiet Info")
  and the original v0.6.5.2 ("Release Hardening") blocks, since
  this re-tag folds everything into a single comprehensive v0.6.5.2
  release. Kept entries below: v0.6.5 ("Crafted Visible") and v0.6.4
  ("Header Convergence"). Pointer to CHANGELOG.md preserved for
  older versions.

### Pairing

- **GearGoblin.Core v0.6.5.2** — unchanged from yesterday's ship.
  No re-tag needed; Core has no source changes that map to this
  polish work.
- **TonberryTactics web v0.6.5.2** — unchanged from yesterday's
  ship. The web's v0.6.5.2 (EVERCOLD link + release.ps1 build gate)
  is already live on Cloudflare; no re-tag needed.

### Out of scope (deferred to v0.6.6)

- Plan tab `GG-PLAN:v1:` paste UI + `Configuration.JobPlans`
  persistence + in-game meld checklist (ridden 7 releases).
- Lodestone integration design (Cloudflare Worker proxy vs
  third-party API vs plugin-only path).
- README refreshes across the three GitHub repos.

---


## [0.6.5] — 2026-05-14  "Crafted Visible"

**Headline:** Fixes the critical HQ-offset bug in `InventoryReader`
that silently dropped every high-quality crafted gear piece from the
gearset export. Pre-v0.6.5 users wearing realistic raid gear saw
"3 of 13 pieces" or "7 of 13 pieces" exports — the missing pieces
were the HQ-crafted body / legs / etc. that the Item sheet lookup
returned `null` for because Dalamud's `GameInventoryItem.ItemId`
carries a `+1,000,000` offset for HQ items. The fix strips the
offset before the sheet lookup; `IsHighQuality` continues to carry
the quality flag separately on the wire format.

Also cleans up the legacy `/goblin*` references that survived the
v0.4.7.1 brand convergence in user-facing About-tab text, chat
messages, and Settings/Diagnostics/Feedback button labels — these
were missed because the convergence-era sweep focused on slash-
command registration and didn't scan static UI strings.

This release does NOT include the `/ttimport` persistence /
checklist UI work. That ships in v0.6.6 ("Round-trip closed").
The scaffold-era "lands in the next build" message in chat and the
About tab has been honestly updated to reference v0.6.6 instead.

### Fixed

- **🔴 `Services/InventoryReader.cs` — HQ-offset filter** — sheet
  lookup now strips the `+1,000,000` offset before
  `GetExcelSheet<Item>().GetRowOrDefault(...)`. Crafted gear
  (almost always HQ) is no longer silently filtered out. The
  piece's `ItemId` field on the wire is now the base ID;
  `IsHighQuality` flags the HQ state. Existing GG-EXPORT:v1:
  schema unchanged.
- **`Plugin.cs` `OnImportCommand`** — chat-message branding:
  `[GearGoblin]` → `[Tonberry Tactics]`, `/goblinimport` references
  → `/ttimport`, "v0.4.7 scaffold" / "next build" notice rewritten
  to honestly say in-game checklist ships in v0.6.6.
- **`Plugin.cs` `OnInfoCommand`** — error path branding updated
  similarly.
- **`UI/MainWindow.cs` About tab — Quick Start steps** — Step 1
  references `/ttexport`, Step 3 references `/ttimport`, scaffold
  warning rewritten for v0.6.5 / v0.6.6 reality.
- **`UI/MainWindow.cs` About tab — Slash commands cheatsheet** —
  primary list shows `/tt`, `/ttexport`, `/ttimport`, `/ttinfo`;
  legacy `/goblin*` aliases listed as a single deprecation row
  (removed at v1.0).
- **`UI/MainWindow.cs` About tab — "What you'll see in the
  Character window"** — example header text `▶ /goblin` → `▶ /tt`
  (matching the v0.6.4 `StatusPanelInjector` fix); narration
  rebranded "GearGoblin injects" → "Tonberry Tactics injects".
- **`UI/MainWindow.cs` About tab — bug-report flow** —
  `Run /goblininfo` → `Run /ttinfo`.
- **`UI/MainWindow.cs` Settings tab** — disabled-state caption
  "Off = /goblin window..." → "Off = /tt window...".
- **`UI/MainWindow.cs` Diagnostics tab** — clipboard-copy button
  label `Copy /goblininfo block` → `Copy /ttinfo block`.
- **`UI/MainWindow.cs` Feedback tab** — diagnostic-attachment
  caption "/goblininfo prints" → "/ttinfo prints".

### Changed

- **`GearGoblin.csproj`** — version `0.6.4 → 0.6.5`, Description
  refreshed for "Crafted Visible".

### Pairing

- **GearGoblin.Core v0.6.5** — no source changes; lockstep version
  bump only.
- **TonberryTactics web v0.6.5** — Materia Tier vendored copy
  synced to Core v0.6.4 content (Skill Speed prefix fix), Meld
  Audit panel rows for Wrong stat / Under-tier / Overcap wired to
  real logic, sell-vs-meld verdict row added. Output format shaped
  to match plugin's `MeldAudit` records so v0.7.x consolidation
  doesn't churn the web UI.

### What still doesn't work (v0.6.6 scope)

- **In-game Plan tab has no `GG-PLAN:v1:` paste box.** Plan tab
  currently accepts Etro/XIVGear URLs only. Round-trip from the
  web has no in-game UI surface.
- **`/ttimport` doesn't persist or apply.** Parses + validates,
  prints success message, but writes nothing to
  `Configuration.JobPlans` and there's no checklist UI to walk
  the imported melds.
- **Plugin still shows `/goblin` in advisor header for users on
  v0.6.4 and earlier.** This release's About-tab fixes don't
  affect already-installed running instances until users reload
  the plugin via `/xlplugins`.

---


## [0.6.3] — 2026-05-14  "Lockstep"

**Headline:** Plugin joins the new shared library **GearGoblin.Core**.
The plugin's existing job-aware `MeldOptimizer` (driven by `JobProfile`)
continues to function unchanged — the web is where today's actual
per-job priority fix lands for users. The plugin's contribution to
this release is structural: a `<ProjectReference>` to Core in
`GearGoblin.csproj`, restoring the v0.5.5 lockstep convention that
v0.6.2 broke when the web shipped a hotfix without a matching plugin
release.

### Why this release exists

When the web's v0.6.2 fix went out, the plugin stayed at v0.6.1 because
the actual bug (Stat Profile hardcoding to Skill Speed for healers) was
display-only on the web side. That was technically honest but broke the
"same version on both halves" convention. The new ground rule: when one
half ships, all halves bump together. v0.6.3 catches the plugin up and
brings Core into the same lockstep so the version-skip in v0.6.2 doesn't
happen again.

### Added

- **ProjectReference to `GearGoblin.Core`** in `GearGoblin.csproj`.
  Path: `..\..\GearGoblin-Core-v0.1\GearGoblin.Core\GearGoblin.Core.csproj`.
  Adjust if your local Core clone uses a different layout. See Core's
  README for the canonical directory structure (Core lives as a
  sibling-of-grandparent next to both the plugin repo and the web repo).

### Changed

- **`GearGoblin.csproj`** — version 0.6.1 → 0.6.3 (skipping 0.6.2 since
  no v0.6.2 plugin release shipped). Description refresh reflecting Core
  integration. New ItemGroup with the ProjectReference, sitting alongside
  the existing brand-artwork + IFontAtlas-fonts content globs.

### Not changed (carries through unchanged)

- **`Services/StatusPanelInjector.cs`** — all v0.6.0 IFontAtlas Phase 2
  palette work + v0.4.6 outer-addon-grow logic unchanged. The off-panel
  positioning bug surfaced in screenshots earlier today is still
  present and is the v0.7.0 CPR-replacement workstream.
- **`Services/MeldOptimizer.cs`** (and its `JobProfile` consumer) —
  the plugin's existing job-aware logic stays. v0.7.x will migrate it
  to consume `Core.JobPriorities` so the plugin and web optimizers
  share their priority tables explicitly rather than implicitly.
- **`Services/GearsetImporter.cs`** — v0.6.1's `ImGui.GetClipboardText()`
  wiring carries through unchanged.
- **All v0.6.0 IFontAtlas typography** (Cinzel / EB Garamond /
  Press Start 2P) — unchanged. The plugin's `/tt` window keeps the
  custom fonts.
- **Wire format** — `GG-EXPORT:v1:` and `GG-PLAN:v1:` schemas
  unchanged. The wire still emits `"plugin": "GearGoblin"` (the
  InternalName); the brand display name "Tonberry Tactics" is a UI
  concern. The plugin namespace and DLL filename also stay
  `GearGoblin` — full code-namespace rename ships with v0.7.x Core
  refactor alongside config migration.

### Deferred (still tracked for v0.7.0)

- **CPR replacement** — drop the coexistence layer; plugin's
  derivations always inject; About-tab notice when CPR is detected
  suggesting uninstall. Signed off on earlier today.
- **Off-panel positioning fix** — `StatusPanelInjector.AddStatRow`
  clones the avgIlvl row's right-aligned number cell, which is sized
  for short stat values; long advisor recs overflow the panel's left
  edge. Fix: separate full-width advisor row method.
- **`/goblin` → `/tt` regression** in the advisor header (line 730 of
  StatusPanelInjector). The brand convergence missed this one
  rendering path; v0.7.0 picks it up.
- **Core-consuming `MeldOptimizer`** — incremental migration to
  `GearGoblin.Core.JobPriorities` so plugin and web optimizers share
  their priority tables explicitly rather than implicitly converging.

### Pairing

Ships in lockstep with:

- **GearGoblin.Core v0.6.3** — new repo, must be cloned to the
  expected path before the plugin can build.
- **TonberryTactics web v0.6.3** — adds the same ProjectReference,
  also refactors `PureMathOptimizer` to actually consume Core's
  `JobPriorities`. This is where today's user-visible improvement
  (Materia Advisor now produces real per-job recs for all 21 jobs)
  shows up.

All three (Core, web, plugin) ship same date, same version, same
release night.

---

## [0.6.1] — 2026-05-13  "Gear Division, hotfix"

**Headline:** Wires the `/ttimport` clipboard read. The v0.4.7 scaffold
had stubbed `ImportFromClipboard()` to a hardcoded empty string with a
"TODO(v0.4.7 build): read clipboard via Dalamud's IClipboardProvider"
comment that survived through v0.5.x and v0.6.0 unchanged. Result:
`/ttimport` always reported "Clipboard is empty" regardless of what
was actually on the clipboard, even immediately after a successful
Ctrl+C of a `GG-PLAN:v1:` string from the website.

### The bug

`Services/GearsetImporter.cs::ImportFromClipboard` opened with:

```csharp
// TODO(v0.4.7 build): read clipboard via Dalamud's
// IClipboardProvider or System.Windows.Forms.Clipboard...
var clipboard = string.Empty;
```

The local was literally `string.Empty`. Every call hit the next-line
`IsNullOrWhiteSpace` gate and bailed out with the user-facing message
"Clipboard is empty. Copy a GG-PLAN:v1: string from Tonberry Tactics
and try again." The error suggested the system clipboard was the
problem; the actual problem was that the method never *looked* at
the system clipboard.

The slash command's inline-arg path (`/ttimport <paste-string>`)
worked fine all along — `Plugin.OnImportCommand` routes non-empty
args through `ImportFromString()` directly, bypassing this method.
Affected v0.4.7–v0.6.0 users with this issue can keep using the
inline form until they update.

### The fix

```csharp
clipboard = ImGui.GetClipboardText() ?? string.Empty;
```

Dalamud's `Dalamud.Bindings.ImGui` clipboard backend proxies the
Windows clipboard, so a Ctrl+C from anywhere (the website's COPY PLAN
button, Notepad, in-game chat) populates this read. The null-coalesce
handles the case where the clipboard contains non-text data (image,
file paths) — falls through to the same "Clipboard is empty" message
which is now actually accurate.

Wrapped in `try`/`catch` so a transient Windows clipboard lock (some
other app holding it) gives the user the inline-arg workaround in
the error message instead of an unhandled exception.

### Pairing

Pairs with **web v0.6.1** which separately fixes the Stat Profile
"+0 materia" / blank-materia-dot bug — the v0.6.0 web only matched
stat-name abbreviations (`CRT`, `DH`, etc.) but the plugin's
`GearsetExporter` writes BaseParam display names verbatim
(`Critical Hit`, `Direct Hit Rate`, etc.). Same release night, same
version number on both halves, two complementary hotfixes on opposite
sides of the round-trip.

### Files touched

- `Services/GearsetImporter.cs` — `ImportFromClipboard()` wires
  `ImGui.GetClipboardText()` + try/catch + workaround hint in the
  error path. `using Dalamud.Bindings.ImGui;` added to the file imports.
- `GearGoblin.csproj` — version 0.6.0 → 0.6.1, hotfix description

### Not changed

- All v0.6.0 IFontAtlas Phase 2 typography (Cinzel / EB Garamond /
  Press Start 2P) carries through unchanged.
- Native CharacterStatus injection palette refresh (LanternHot
  advisor accent, FrostSoft derived rows) carries through unchanged.
- Wire format (`GG-EXPORT:v1:`, `GG-PLAN:v1:`) untouched.

---

## [0.6.0] — 2026-05-13  "Gear Division"

**Headline:** The plugin's half of the v0.6.0 design port. Web's v0.6.0
"Gear Division" shipped earlier today with the full TLF Gear Division
landing page; this is the in-game side. Three workstreams in one ship:
version sync, IFontAtlas Phase 2 (custom Google Fonts in the /tt
window), and a palette refresh on the native CharacterStatus injection.

The native injection's typography stayed where it was — FFXIV's bundled
SE font system. AtkTextNodes in the game's UI don't accept plugin font
atlases; that limitation isn't going away. What v0.6.0 brings in-game
is colour: the derived stat rows now render in TLF FrostSoft (close to
the web's body-text tone), and the Materia Advisor section accent
shifts from TLF Gold to LanternHot, which reads brighter against the
native panel's blue background.

The custom typography landed where it could — in the plugin's own
ImGui surfaces. Open `/tt` after the v0.6.0 install and the player name
header renders in Cinzel @ 22px, the About-tab description in EB
Garamond @ 15px, version pills in Press Start 2P @ 10px. Same fonts
the web uses, same fallback hierarchy if a `.ttf` fails to load.

### Added

- **`Theme/FontAtlasManager.cs`** (new) — IFontAtlas Phase 2 loader.
  Exposes six `IFontHandle` properties (CinzelDisplay/Header/Emphasis,
  GaramondBody/Italic, Pixel) each backed by a `.ttf` in
  `Assets/Fonts/`. Defensive load: missing or malformed font files
  yield a null handle plus a warning in `/xllog`, and consuming code
  falls back to the default ImGui font. The plugin loads either way.

- **`Theme/FontPushExtensions.cs`** (new) — `PushOrNull()` extension
  method on `IFontHandle?` that returns a no-op `IDisposable` for null
  handles. Lets call sites wrap a section in a custom font with a
  plain `using` block, no null-check ceremony:

  ```csharp
  using (plugin.Fonts.CinzelDisplay.PushOrNull())
      ImGui.TextColored(TlfTheme.GoldBright, "TONBERRY TACTICS");
  ```

- **Five `.ttf` files in `Assets/Fonts/`:**
  - `Cinzel-Regular.ttf` (32 KB) — display serif, used at 22px and 32px
  - `Cinzel-SemiBold.ttf` (32 KB) — display serif emphasis weight
  - `EBGaramond-Regular.ttf` (49 KB) — body serif
  - `EBGaramond-Italic.ttf` (47 KB) — italic emphasis inside Garamond
  - `PressStart2P-Regular.ttf` (41 KB) — pixel display

  All sourced from Google Fonts via `@fontsource/*` v4 npm packages,
  converted from `.woff` to `.ttf` with `fontTools.ttLib`. csproj's
  `<Content Include="Assets\Fonts\*.ttf">` glob copies them to the
  deployed plugin directory next to the DLL with
  `CopyToOutputDirectory=PreserveNewest`.

### Changed

- **`Plugin.cs`** — instantiates `FontAtlasManager` between `Brand` and
  `MainWindow` so `MainWindow.Draw()` can reference the font handles
  immediately. Disposes between `StatusPanel` and `Brand` in teardown
  to keep the order Dalamud-services-still-alive when font handles
  release their atlas refcounts.

- **`UI/MainWindow.cs`** — `using GearGoblin.Theme;` added for the
  extension method. `DrawBody()` player-name line wrapped in
  `CinzelHeader.PushOrNull()`; right-aligned version badge wrapped in
  `Pixel.PushOrNull()`. `DrawAbout()` brand-header wordmark wrapped in
  `CinzelDisplay.PushOrNull()`, version line in `Pixel`, eyebrow label
  in `Pixel`, description prose in `GaramondBody`, the "Refia Rakkiri
  — the Last Onion Knight" credit line in `GaramondItalic`.

- **`Theme/TlfTheme.cs`** — `StandingReadyFooter()` fixes a leftover
  `GEARGOBLIN · v…` brand string to `TONBERRY TACTICS · v…`. The
  rename had landed in v0.4.7.1 but missed this one rendering path.
  Caught while reviewing fonts wiring.

- **`Services/StatusPanelInjector.cs`** — `InjectedRowColor` shifts
  from `#A0A0A0` neutral gray to `#C2C5D8` (TlfTheme.FrostSoft) so
  derived stat rows pick up the body-text tone used on the web.
  `AdvisorAccentColor` shifts from `#C9B27E` (TlfTheme.Gold) to
  `#FFCE5E` (TlfTheme.LanternHot) — same gold-family but brighter,
  reads better against FFXIV's native blue panel background. No
  behavioural change; both colours are pre-existing static fields.

- **`GearGoblin.csproj`** — version 0.5.5 → 0.6.0, Description refresh
  reflecting the IFontAtlas Phase 2 + visual overhaul work, new
  `<Content Include="Assets\Fonts\*.ttf">` glob.

### Deliberately deferred

- **Fonts on the native CharacterStatus injection.** The game's
  AtkTextNode system uses FFXIV's bundled SE font and can't accept a
  `.ttf` from a plugin atlas. This is a hard engine constraint, not a
  todo. The native injection picks up colour parity (above) but its
  typography continues to render in the game's own font. Users see
  consistent typography in `/tt`; the native panel reads as native.

- **Per-job optimizer priorities on derivation rows.** The injection
  still uses the GNB priority order for "next breakpoint" hints when
  the active job isn't GNB — same fallback as the web. Per-job tables
  land with the v0.5.0 Core refactor (`GearGoblin.Core` netstandard
  library that both halves project-reference).

- **Plan-tab "Active Plan" UI.** Still the v0.4.7 scaffold —
  `/ttimport` validates and parses but doesn't yet wire to a per-job
  persisted plan with meld-completion checkboxes. Queued for v0.6.1.

- **Tab icons.** Polish item from the design handoff. Queued.

### Bootstrap recipe (for repro / future font additions)

The five `.ttf` files were derived from Google Fonts as follows. Capture
here so we can repeat the recipe if Google Fonts changes upstream:

```
# 1. Pull fontsource v4 tarballs from npm (they ship .woff, not .ttf):
npm pack @fontsource/press-start-2p@4.5.11
npm pack @fontsource/cinzel@4.5.10
npm pack @fontsource/eb-garamond@4.5.11

# 2. Extract just the latin-400 .woff files:
tar -xzf fontsource-press-start-2p-4.5.11.tgz \
    package/files/press-start-2p-latin-400-normal.woff
# (and similar for cinzel 400 + 600, eb-garamond 400 + italic)

# 3. Convert .woff → .ttf via fontTools.ttLib:
python3 -c "
from fontTools.ttLib import TTFont
f = TTFont('press-start-2p-latin-400-normal.woff')
f.flavor = None
f.save('PressStart2P-Regular.ttf')"
```

`raw.githubusercontent.com` is blocked from the dev environment, so
pulling .ttf directly from `github.com/google/fonts` doesn't work —
fontsource via npm + fontTools conversion is the path that survives the
allowlist constraints.

---

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
