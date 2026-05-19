# GearGoblin plugin v0.6.6.0 dropin

**First tagged release of the Character-tab era.** Closes BUG-001 and
BUG-002 (both already dead in working-tree code, verified in-game across
two sessions on 2026-05-18 and 2026-05-19); introduces the new
`Character` tab in `/tt` as the strategic replacement for the
StatusPanelInjector's native-panel injection. The injection codepath
keeps shipping in parallel for backward compat — deprecated in v0.6.7,
removed in v0.7.0. Plugin / Core / Web all version-lockstep at 0.6.6.0.

## What's in this dropin

```
GearGoblin.csproj                   overwrite — version triplet bumped 0.6.5.4 → 0.6.6.0; description rewritten to describe Character tab introduction + BUG-001/002 closeout; v0.6.5.x hypothesis commentary stripped
UI/CharacterTab.cs                  new       — 4-section character surface: hero (name+class+iLvl), substats strip (role-gated, Sks vs Sps + Tenacity for tanks), inline materia advisor (mirrors StatusPanelInjector.UpdateAdvisor candidate-build), gear table (lifted from DrawCurrentGear). Defensive try/catch around MeldOptimizer.Optimize for BUG-003. Each section has TODO comments pointing at the Claude Design v0.2.0 deliverable for polish-pass spec.
UI/MainWindow.cs                    overwrite — Character tab registered as FIRST tab in ##goblintabs strip, before Quick Start
Services/StatusPanelInjector.cs     overwrite — already has BUG-001 (advisor header → "▶ /tt") and BUG-002 (SetAdvisorRow signature change, descriptive payload routes to label cell) fixes from your working tree, shipped verbatim so the released artifact matches what you already verified in-game
Plugin.cs                           overwrite — no logic changes; included so the dropin pattern stays uniform with v0.6.5.x precedent
CHANGELOG.md                        overwrite — v0.6.6.0 entry prepended; existing 75 kB history preserved verbatim
```

Six files total. `repo.json` is **not** in the dropin — the
github-actions release bot updates it automatically after building the
v0.6.6.0 GitHub release artifact, and including a pre-bumped repo.json
would open a 2-3 minute window between Brian's push and the bot's build
where Dalamud sees "v0.6.6.0 available" but the download URL 404s.

## Ship sequence

```powershell
cd D:\GearGoblin-v0.1\GearGoblin

# Pull first — the bot's repo.json revert commit (ccaf0fa) from
# yesterday's "decommissioned" recovery is on origin, your working
# tree may be behind. Without this, release.ps1's push will be
# rejected as non-fast-forward.
git pull --rebase origin main

# Drop in the v0.6.6.0 files
Move-Item $env:USERPROFILE\Downloads\GearGoblin-v0.6.6.0-dropin.zip . -Force
Expand-Archive -Path .\GearGoblin-v0.6.6.0-dropin.zip -DestinationPath . -Force

# Dry-run first to inspect what release.ps1 will commit
Unblock-File .\release.ps1
.\release.ps1 -DryRun

# If dry-run looks clean, ship it
.\release.ps1
```

`release.ps1` will:

1. Sync with remote (should be a no-op after your `git pull --rebase`).
2. Show "Changes to be committed" with the six files above.
3. Run the build gate (`dotnet build -c Release`). Should succeed with
   no warnings. If CharacterTab.cs has any compile errors (it references
   `plugin.Inventory` and `player` from MainWindow's scope — the new
   tab block in MainWindow.cs passes both through), this is where they'd
   surface. Fix before proceeding.
4. Commit the staged files. **Commit message will mention "v0.6.6"** so
   you can spot it in the log later.
5. Tag `v0.6.6` and push both commit and tag.
6. The github-actions release workflow fires on the tag push, builds
   the release artifact `latest.zip`, creates the GitHub release at
   `v0.6.6`, and updates `repo.json` to point at the v0.6.6 download
   URLs. Bot's commit is tagged `[skip ci]` so it doesn't re-fire.

## Verify after Dalamud reload

**Do not use `/xlrestart`** — that reboots the entire FFXIV game.
Instead, in `/xlplugins`, toggle GearGoblin off then back on (cheapest,
reloads just the DLL), or `/xlreload` (reloads all of Dalamud but
doesn't restart the game).

After reload:

1. **Header version pill** — top right of `/tt` window should read
   `v0.6.6.0` (not `v0.6.5.4`).
2. **Tab strip** — first tab in the bar should be **Character**, not
   Quick Start. Click it. Should render four sections: hero line, stats
   strip, materia advisor block, gear table. Visual is skeleton-grade
   plain text; polish lands in v0.6.6.x.
3. **`/ttinfo`** — `Plugin version` line should read `v0.6.6.0`.
4. **Character panel injection still works** — open the game's
   Character window. The `── Materia Advisor    ▶ /tt` header should
   still appear cleanly below "Average Item Level", confirming the
   StatusPanelInjector path is still functional in parallel with the
   new tab.
5. **Class swap test** — switch Refia to Weaver. The Character tab in
   `/tt` should render without crashing (it currently shows the
   placeholder battle-stat values the game returns for crafters; a
   proper "Battle stats not applicable" render path lands in v0.6.6.1).
6. **Dalamud manifest health** — after ~2-3 minutes (let the bot
   finish), reopen `/xlsettings` → Experimental → Plugin Repositories
   → custom GearGoblin repo. The "decommissioned" banner should not
   reappear. If it does, the bot's repo.json bump failed; investigate
   `.github/workflows/release.yml` runs in the GitHub UI and fall back
   to the manual `repo.json` recovery pattern from `ccaf0fa`.

## Pairing

- **GearGoblin.Core v0.6.6.0** — version-only lockstep bump. Update the
  Core csproj's `<Version>` to `0.6.6.0`, commit, push, tag `v0.6.6`,
  push tag. No code changes.
- **TonberryTactics web v0.6.6.0** — version-only lockstep bump. Same
  pattern on the web side. Cloudflare Pages will redeploy automatically
  on the next push to `main`.

## Out of scope (deferred to v0.6.6.x or later)

- BUG-003 guard (`Substat.None` skip in `MeldOptimizer.AuditSingleMeld`)
  — defensive try/catch in CharacterTab handles it for now; proper fix
  in v0.6.6.1.
- Crafter/Gatherer class handling in CharacterTab StatsStrip + Advisor
  sections — "Battle stats not applicable" render path in v0.6.6.1.
- Character tab visual polish (Adventurer Plate aesthetic, stat cards,
  ranked advisor rows, striped gear table) — incremental across v0.6.6.x
  per the Claude Design v0.2.0 deliverable's per-component spec.
- CharacterPanelRefined coexistence skip in StatusPanelInjector —
  unnecessary now that the new tab works independently.
- Removal of Quick Start tab (absorb into About) — later in v0.6.6.x.
- Removal of duplicate "Current Gear" tab — once Character tab gear
  section polish is locked.
- Line-ending normalization across the repo (`.gitattributes`) — the
  CRLF/LF churn in your working tree is real but cosmetic; defer.
- Mobile site responsive CSS deploy — web-only, untouched in this
  dropin, files still sitting in your Downloads from yesterday's work.
