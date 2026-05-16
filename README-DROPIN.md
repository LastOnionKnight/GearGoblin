# GearGoblin plugin v0.6.5.2 polish dropin

**This is a re-tag of v0.6.5.2, not a new version.** Eight polish fixes
that should have been part of the original v0.6.5.2 build. Plugin csproj
stays at `0.6.5.2`. Lockstep preserved (Core = Web = v0.6.5.2 already).

## What's in this dropin

```
Plugin.cs                          overwrite — /ttinfo branding sweep + version formatter symmetry
UI/MainWindow.cs                    overwrite — ResolveVersion fix, Refresh button, About-tab v0.6.5.2 entry
UI/PlanTab.cs                       overwrite — CS4014 warning fix (line 96)
Services/StatusPanelInjector.cs     overwrite — advisor row 20px pre-pad to clear ILVL overlap
Services/BrandResources.cs          overwrite — defer asset loads to framework thread
GearGoblin.csproj                   overwrite — Description refresh (version unchanged at 0.6.5.2)
CHANGELOG.md                        overwrite — v0.6.5.2 entry replaces v0.6.5.1 folded-in block
```

Six source files + csproj + CHANGELOG. No version bump.

## Ship sequence (you've already done the tag-deletion step)

The remote v0.6.5.2 tag is already deleted (from your earlier session).
Just extract and ship normally:

```
cd D:\GearGoblin-v0.1\GearGoblin
Move-Item $env:USERPROFILE\Downloads\GearGoblin-v0.6.5.2-dropin.zip . -Force
Expand-Archive -Path .\GearGoblin-v0.6.5.2-dropin.zip -DestinationPath . -Force
Unblock-File .\release.ps1
.\release.ps1 -DryRun
.\release.ps1
```

release.ps1 will:
1. Sync with remote (no bot commits since last ship, should be a no-op)
2. Show "Changes to be committed" with the six source files
3. Run the build gate — should succeed with NO warnings now (PlanTab CS4014 fix)
4. Commit, tag v0.6.5.2 fresh, push

## Verify after Dalamud reload

`/xlrestart` or disable/enable plugin in `/xlplugins`, then:

1. **Header version pill** — top right of the plugin window should now
   read **v0.6.5.2** (not v0.6.5).
2. **Refresh button** — click it. A "✓ refreshed" label should appear
   next to the button in ice-cyan and fade out over 2 seconds.
3. **About tab → Plugin info subtitle** — bottom of About body should
   read "in-game plugin · v0.6.5.2".
4. **About tab → What's New** — top entry is now "v0.6.5.2 — 'Panel
   Polish'" with the union list. v0.6.5 and v0.6.4 below it.
5. **Open character window**, equip any job, view PLD/VPR/CRP gear panel.
   Advisor section header should now sit BELOW the "Average Item Level"
   row with breathing room — no more ghost text overlap.
6. **Run `/ttinfo`** with character window open. Diagnostic block in
   your clipboard should header `───── Tonberry Tactics /ttinfo ─────`
   and show `Plugin version : v0.6.5.2`.
7. **Plugin startup logs** (`/xllog`) — search for "Not on main thread".
   Should be ZERO hits now (was 3 per startup before).

## Pairing (unchanged)

- **GearGoblin.Core v0.6.5.2** — same csproj as yesterday, no re-ship.
- **TonberryTactics web v0.6.5.2** — same Cloudflare deploy as
  yesterday, no re-ship.

## Out of scope (v0.6.6)

- Plan tab paste UI + persistence + checklist.
- Lodestone integration design.
- README refreshes across the three GitHub repos.
