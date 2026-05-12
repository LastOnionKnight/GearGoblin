# GearGoblin v0.4.5 — Dropin Apply Instructions

**Target:** `D:\GearGoblin-v0.1\GearGoblin`

This dropin makes GearGoblin a full CharacterPanelRefined replacement. The
StatusPanelInjector is a complete rewrite (not a patch) — drop these files
over your working tree and rebuild clean, and you get guaranteed v0.4.5
state regardless of whatever partial-application history you have from
v0.4.1 / v0.4.2.

---

## 1. Apply the dropin

Extract the zip and copy these files over your working tree, overwriting:

```
GearGoblin/
├── GearGoblin.csproj                     ← version bump to 0.4.5.0
├── Configuration.cs                      ← new toggle properties
├── CHANGELOG.md                          ← v0.4.5 entry prepended
├── README.md                             ← CPR-replacement positioning
├── release.ps1                           ← carried forward unchanged
├── Services/
│   ├── DerivationHelpers.cs              ← NEW (CprDetection + DerivedStatFormatter)
│   └── StatusPanelInjector.cs            ← FULL REWRITE (replaces v0.4.2 file)
└── UI/
    └── MainWindow.cs                     ← DrawAbout rewrite for v0.4.x history
```

Files **not** in this dropin are untouched: `Plugin.cs`, `DalamudServices.cs`,
`Materia/*`, `Planning/*`, `Util/*`, `UI/MateriaTab.cs`, `UI/PlanTab.cs`,
`repo.json`, the LICENSES folder. Don't replace those.

## 2. Clean rebuild

The previous v0.4.2 deploy had a build-cache issue where the runtime didn't
match the source. To avoid that:

```powershell
cd D:\GearGoblin-v0.1\GearGoblin
dotnet build -c Release --no-incremental
```

The `--no-incremental` flag forces a from-scratch build, bypassing any
stale `obj/` or `bin/` artifacts from earlier versions.

## 3. Reload in-game

In FFXIV:

```
/xlplugins
```

Find **GearGoblin** in **Dev Tools**, click the reload icon. Or restart the
game if reload isn't doing it.

## 4. Verify v0.4.5 is actually running

Open `/xllog` and search for `v0.4.5`. You should see:

```
[INF] StatusPanelInjector v0.4.5: registered AddonLifecycle listeners for CharacterStatus.
```

When you open the Character window for the first time after reload:

```
[INF] StatusPanelInjector v0.4.5: first inject complete. CPR active: False. Derivations enabled: True.
```

If you have CharacterPanelRefined installed:

```
[INF] StatusPanelInjector v0.4.5: CharacterPanelRefined detected as active; skipping derived-stat injection (set ForceDerivationsOverCpr=true to override). Breakpoint hints, real GCD, and Materia Advisor will still inject normally.
[INF] StatusPanelInjector v0.4.5: first inject complete. CPR active: True. Derivations enabled: False.
```

If you do **not** see any of these `v0.4.5` lines, the build didn't pick up
the new source. Diagnostic check:

```powershell
cd D:\GearGoblin-v0.1\GearGoblin
Select-String -Path Services\StatusPanelInjector.cs -Pattern "v0\.4\.5: first inject"
Select-String -Path GearGoblin.csproj -Pattern "0\.4\.5\.0"
```

Both should return hits. If they do but the chat log doesn't show v0.4.5,
the issue is build/load — try a full `dotnet clean` then `dotnet build -c Release`.

## 5. What you should see in-game

**Offensive Properties** — Crit/Det/DH each get one compact derived row:

```
Critical Hit                       2618
   20.8% · ×1.556 · +11.6% dmg · +13→tier
Determination                      2700
   +11.3% dmg · +13→tier
Direct Hit Rate                    1608
   23.5% · +5.9% dmg · +4→tier
```

**Skill Speed** (or Spell Speed for casters) — GCD real + combined row:

```
Skill Speed                         420
   GCD (real):                       2.50s
   +0.0% dmg · +22→tier
```

**Role Properties** — only on tanks / healers:

```
Tenacity                          1305      (tanks)
   +2.5% dmg · -2.5% taken

Piety                              440      (healers)
   200 MP/tick
```

**Gear** — Materia Advisor section (4 rows, click header for /goblin):

```
── Materia Advisor ──   0c · 0w · 0e   ▶ /goblin
   Earrings #1 → Savage Aim Materia XII
   …
```

On VPR (your job), you should NOT see Tenacity or Piety rows — those are
role-gated.

## 6. If CPR is also installed

GG v0.4.5 detects CPR on each panel open and skips its derived stat
injection by default to avoid double-display. You'll still see GG's
breakpoint hints, real GCD, and Materia Advisor — those are GG-unique.

To force GG to inject derivations even with CPR active, edit:

```
%appdata%\XIVLauncher\pluginConfigs\GearGoblin.json
```

Set `"ForceDerivationsOverCpr": true`, save, reload the plugin. (Settings
tab UI is the v0.4.6 target.)

## 7. Release

When you're satisfied with field testing:

```powershell
cd D:\GearGoblin-v0.1\GearGoblin
.\release.ps1
```

Auto-detects version 0.4.5 from csproj, generates commit message from the
CHANGELOG v0.4.5 entry, tags v0.4.5, pushes with `--follow-tags`. The same
push-rejection pattern from yesterday could recur if origin/main has new
commits — if you get `git push --rejected`, the recovery is:

```powershell
git fetch origin
git pull --rebase origin main
git tag -f v0.4.5
git push origin main
git push origin v0.4.5 --force
```

---

## Known carry-forward issues (not blocking v0.4.5)

- **`release.ps1` BOM bug.** PS 5.1's `Set-Content -Encoding UTF8` writes a
  BOM, which is why commit subjects come through as `﻿GearGoblin 0.4.5`
  with a leading invisible char. Fix is to switch the script to
  `[System.IO.File]::WriteAllText($msgFile, $Message)`. Not in this
  dropin; tracked for v0.4.6.
- **Stray `_redirects` and `build.sh`** from a Tonberry Tactics deploy
  mishap are sitting in the GearGoblin repo at the root. Harmless cruft.
  You ran `Remove-Item` locally but didn't commit. Either commit the
  deletion or `git restore --staged` and ignore.

---

*"No gear. No hope. No pants. Just onions." — TLF*
*Stab once, stab true.*
