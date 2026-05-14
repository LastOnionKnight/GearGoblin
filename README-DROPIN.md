# GearGoblin v0.6.4 dropin — "Header Convergence"

**Headline:** Fixes the `/goblin → /tt` regression in the in-game
advisor header (three render paths in `StatusPanelInjector.cs`).
Picks up Core v0.6.4's Skill Speed prefix fix automatically via
ProjectReference. Adds persistent error-log transcript to
`release.ps1`. Lockstep version bump completes three-way alignment
at v0.6.4 (Core / web / plugin).

## What's in this dropin

```
Services/StatusPanelInjector.cs   overwrite — /goblin → /tt at lines 730, 752, 761
GearGoblin.csproj                 overwrite — version 0.6.3 → 0.6.4, Description refresh
CHANGELOG.md                      overwrite — v0.6.4 entry on top
release.ps1                       overwrite — Start-Transcript / Stop-Transcript added
.gitignore                        overwrite — *-dropin.zip added
```

## Why this release

The handoff flagged a brand-convergence regression: v0.4.7.1 renamed
the user-facing slash command from `/goblin` to `/tt`, but three
render paths in `StatusPanelInjector.cs` still display the legacy
name and dispatch the legacy command on click. Both `/tt` and
`/goblin` are registered in `Plugin.cs` (legacy as alias), so the
plugin still works — but the user-visible header text and the
dispatched command should agree.

The `*-dropin.zip` gitignore addition is preemptive: based on the
Core v0.6.4 ship where the dropin zip landed alongside the project
and got swept into `git add -A`. Plugin's repo root and project root
are the same directory (unlike Core, where the project sits one
level deep inside the repo), so a dropin placed at the project root
would absolutely be swept up by `git add -A` unless ignored.

## Build & deploy

```
cd D:\GearGoblin-v0.1\GearGoblin
Expand-Archive -Path .\GearGoblin-v0.6.4-dropin.zip -DestinationPath . -Force
Unblock-File .\release.ps1
git status
dotnet build -c Release
.\release.ps1 -DryRun
.\release.ps1
```

The `dotnet build` step proves Core's ProjectReference resolves
correctly (against `D:\GearGoblin-Core-v0.1\GearGoblin.Core\` v0.6.4)
and that the plugin compiles cleanly against the corrected Core.

## Verify after push

1. `https://github.com/LastOnionKnight/GearGoblin/releases` — new
   `v0.6.4` tag present.
2. `Get-Content release-error.log` in the plugin folder — a
   transcript of this run should be appended at the bottom.
3. In-game (next time you load the plugin):
   - Open the Character window. The advisor header at the bottom of
     the stat panel should read `▶ /tt` (cleared state) or
     `Nc · Mw · Xe   ▶ /tt` (with data) rather than `▶ /goblin`.
   - Click the advisor header. The plugin's main `/tt` UI should
     open. Previously this dispatched `/goblin` (still functional as
     an alias, but inconsistent with the displayed name).
4. Materia Advisor recommendations for Skill Speed should now show
   `Quickarm Materia XII` rather than `Piety Materia XII`. This is
   the Core v0.6.4 fix flowing through via ProjectReference.

## Pairing

Three-way lockstep at v0.6.4:

- **GearGoblin.Core v0.6.4** — already shipped (`5b7e627` + cleanup
  `7111d08`). Carries the Skill Speed prefix fix that this plugin
  picks up via ProjectReference.
- **TonberryTactics web v0.6.4** — already shipped (`4962952`).
  Carries vendored v0.6.3 copy of Core's `MateriaTiers.cs`; web will
  sync forward to v0.6.4 Core content on its next release.

## v0.7.0 reminder

For tracking; not in v0.6.4:

- Drop CharacterPanelRefined coexistence (plugin always injects).
- About-tab CPR-detected uninstall notice.
- Off-panel positioning rewrite (`AddAdvisorRow` for long advisor
  text instead of cloning avgIlvl row).
- Plugin-side `MeldOptimizer` consumes `Core.JobPriorities`.
- Code-namespace rename (`GearGoblin → TonberryTactics`) with config
  migration shim.
