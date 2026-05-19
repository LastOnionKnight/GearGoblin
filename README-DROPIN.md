# GearGoblin plugin v0.6.6.1 dropin

**First polish pass on the Character tab.** Replaces the v0.6.6.0
StatsStrip skeleton (plain `ImGui.Text` rows) with the card-based
layout per Claude Design v0.2.0: horizontal grid, per-stat cards with
label / value / derived-effect / optional warn-chip. Bundles the
BUG-003 `Substat.None` guard in `MeldOptimizer.GenerateAudits` while
we're in the materia files. Plugin / Core / Web all lockstep at 0.6.6.1.

## What's in this dropin

```
GearGoblin.csproj                   overwrite — version 0.6.6.0 → 0.6.6.1; description rewritten for the polish-pass era
UI/CharacterTab.cs                  overwrite — DrawStatsStrip rebuilt as card grid; signature is now Draw(Plugin, IPlayerCharacter); Crafter/Gatherer path renders "Battle stats not applicable" instead of placeholder 420s
UI/MainWindow.cs                    overwrite — single one-line call-site update for the new Draw signature
Materia/MeldOptimizer.cs            overwrite — 3-line BUG-003 guard: skip audit when current.Stat == Substat.None
CHANGELOG.md                        overwrite — v0.6.6.1 entry prepended to existing history
```

Five files. No `repo.json` in the dropin — bot writes it after the
release artifact builds.

## Ship sequence

Same workflow as v0.6.6.0:

```powershell
cd D:\GearGoblin-v0.1\GearGoblin

# release.ps1's fetch+rebase+autostash handles the sync — no need for a
# separate git pull --rebase upfront.

Move-Item $env:USERPROFILE\Downloads\GearGoblin-v0.6.6.1-dropin.zip . -Force
Expand-Archive .\GearGoblin-v0.6.6.1-dropin.zip -DestinationPath . -Force

.\release.ps1 -DryRun                  # inspect first
.\release.ps1                          # ship it
```

`release.ps1` will:

1. Sync with origin/main (one or two bot commits may have landed since
   v0.6.6.0 ship — autostash handles any local CRLF/LF churn).
2. Show "Changes to be committed" with the five files above.
3. Run the build gate. Should succeed cleanly — `CharacterTab.cs`'s
   new `Plugin` parameter is in the same namespace, `TlfTheme.cs` is
   already a project file, `FontAtlasManager`'s font handles are
   already exposed via `plugin.Fonts`. No new project references.
4. Commit + tag `v0.6.6.1` + push.
5. github-actions release workflow fires, builds artifact, updates
   `repo.json`, push the bump with `[skip ci]`.

You'll get the `WARNING: No CHANGELOG entry found for version 0.6.6.1`
message again — that's the release.ps1 regex bug we noted (looks for
`## [0.6.6.1]` literal but the entry is in the file at line 14). Not
blocking; release.ps1 falls back to the generic "GearGoblin 0.6.6.1"
commit message and ships. A one-line fix to the script is on the
followup queue.

## Verify after Dalamud update

Toggle the plugin off/on in `/xlplugins` (NOT `/xlrestart`) and
verify the Character tab:

1. **Header pill** reads `v0.6.6.1`.
2. **Character tab → Substats section** — should render as four
   horizontal cards (Crit, Det, DH, SkS or SpS depending on job),
   plus a fifth (Tenacity on tanks, Piety on healers). Each card:
   pixel-font label uppercase in dim gold, large Cinzel value in
   bright gold, frost-toned derived effect line.
3. **Card backgrounds** — `InkPanelAlt` (dark navy). Borders one pixel,
   `BorderPixelLite` (slightly lighter navy).
4. **Switch to a crafter (WVR, CRP, etc.)** — Substats section
   should render the single disabled-text line "Battle stats not
   applicable for this class." instead of three 420 placeholder
   cards.
5. **Native Character panel injection still working** — Materia
   Advisor row still renders cleanly below "Average Item Level",
   `── Materia Advisor ▶ /tt` header, empty-state or candidate-row
   payload. The injector is unchanged in v0.6.6.1.

If a card displays a `⚠ ABOVE 420 BASELINE` chip with a warning-colored
border, that's the speed-stat warn behavior firing — expected if your
SkS/SpS is melded above 420. Your GNB at 420/420 should not trigger
it; if it does anyway, your speed values are above baseline and the
chip is correctly informing you of that.

## Pairing

- **GearGoblin.Core v0.6.6.1** — version-only lockstep bump, no code
  changes. Same pattern: bump csproj `<Version>`, commit, tag, push.
- **TonberryTactics web v0.6.6.1** — version-only lockstep bump.

## Out of scope (deferred)

- Next-tier breakpoint math for the StatCard tier line (v0.6.6.x).
- Per-job speed-meld profile for a smarter warn-chip (v0.6.6.x).
- JetBrains Mono font atlas registration (later, if needed).
- CharacterHero portrait + corner brackets (v0.6.6.2).
- Advisor ranked rows + gain badges (v0.6.6.3).
- Gear table stripes + gold-tier highlight (v0.6.6.4).
- StatusPanelInjector deprecation surfacing (v0.6.7).
- repo.json `Name` field correction "GearGoblin" → "Tonberry Tactics"
  (still on the followup queue; doesn't need its own version).
- release.ps1 CHANGELOG regex fix (one-line; followup queue).
