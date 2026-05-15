# GearGoblin plugin v0.6.5.2 dropin — "Release Hardening"

**No source changes.** Release-infrastructure patch that prevents the
v0.6.5.1 push-rejection failure from recurring.

## What's in this dropin

```
release.ps1            overwrite — fetch + pull --rebase --autostash preamble
GearGoblin.csproj      overwrite — version 0.6.5.1 → 0.6.5.2
CHANGELOG.md           overwrite — v0.6.5.2 entry on top
```

Three files. No code anywhere except the release script.

## Build & deploy

```
cd D:\GearGoblin-v0.1\GearGoblin
Move-Item $env:USERPROFILE\Downloads\GearGoblin-v0.6.5.2-dropin.zip . -Force
Expand-Archive -Path .\GearGoblin-v0.6.5.2-dropin.zip -DestinationPath . -Force
Unblock-File .\release.ps1
.\release.ps1 -DryRun
.\release.ps1
```

You should see a new "Syncing with origin/main (fetch + rebase + autostash)…"
line near the top of the release output. If the repo.json bot has pushed
since your last release (it will have — v0.6.5.1's bot commit is already
on remote and not in your local now that you're starting fresh from
yesterday's state), the rebase will pull it down silently before the build
gate runs.

## What the new step actually does

Before this release, `release.ps1` went: read csproj → check git status →
build gate → commit → tag → push. Step order in v0.6.5.2:

1. Read csproj
2. Check git status (branch only)
3. **NEW:** `git fetch origin <branch>`
4. **NEW:** `git pull --rebase --autostash origin <branch>`
5. Display "Changes to be committed"
6. Build gate
7. Commit → tag → push

`--autostash` is the key: it lets the rebase run cleanly even though
your working tree has the just-extracted dropin files modified. Git
stashes them, runs the rebase, restores them. If the bot's commits
actually conflict with anything we're shipping (extremely unlikely —
the bot only touches `repo.json` and we never bundle that file in
dropins), the rebase aborts with a clear error and we surface
recovery guidance.

## Verify after push

1. `Get-Content GearGoblin.csproj | Select-String "Version"` shows
   `0.6.5.2` everywhere.
2. In-game `/ttinfo` continues to work (no regression on the v0.6.5.1
   `/ttinfo` crash fix).
3. About-tab What's New still shows the trimmed 3-version list (v0.6.5.2,
   v0.6.5.1, v0.6.5) — wait, that's not what's in this release.

   Actually About-tab still shows v0.6.5.1, v0.6.5, v0.6.4 from the
   previous dropin — we did NOT touch UI/MainWindow.cs in v0.6.5.2.
   That's fine, the v0.6.5.2 entry is in CHANGELOG.md on GitHub
   instead. We can refresh the What's New section in v0.6.6 when
   there's actual user-visible work to highlight.

## Pairing

- **GearGoblin.Core v0.6.5.2** — same release.ps1 sync step,
  lockstep version bump.
- **TonberryTactics web v0.6.5.2** — same sync step **plus** the
  build gate that Web's release.ps1 was missing, plus the EVERCOLD
  wordmark wrapped in an external link.

## Out of scope (deferred to v0.6.6+)

- Character-panel advisor row offset (push injection down 1-2 row
  heights to clear the ILVL row's ghost overlay).
- `BrandResources.TryLoad` thread-affinity fix.
- Plan tab `GG-PLAN:v1:` paste UI + persistence + checklist.
- Lodestone integration design.
