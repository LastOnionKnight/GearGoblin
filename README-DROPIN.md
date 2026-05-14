# GearGoblin v0.6.5 dropin — "Crafted Visible"

**Headline:** Fixes the critical HQ-offset bug that silently dropped
all crafted gear from gearset exports. Also sweeps `/goblin*` legacy
references out of user-facing About-tab text, chat messages, and
button labels.

## What's in this dropin

```
Services/InventoryReader.cs       overwrite — HQ offset strip + base-ID storage
Plugin.cs                          overwrite — chat-message branding + scaffold notice
UI/MainWindow.cs                   overwrite — About tab + Settings/Diagnostics/Feedback strings
GearGoblin.csproj                  overwrite — version 0.6.4 → 0.6.5
CHANGELOG.md                       overwrite — v0.6.5 entry on top
```

## Build & deploy

```
cd D:\GearGoblin-v0.1\GearGoblin
Move-Item $env:USERPROFILE\Downloads\GearGoblin-v0.6.5-dropin.zip . -Force
Expand-Archive -Path .\GearGoblin-v0.6.5-dropin.zip -DestinationPath . -Force
Unblock-File .\release.ps1
git status
dotnet build -c Release
.\release.ps1 -DryRun
.\release.ps1
```

## Verify after push

1. Reload the plugin in-game: `/xlplugins` → Tonberry Tactics →
   Disable then Enable.
2. Open the About tab. Confirm slash command list reads
   `/tt`, `/ttexport`, `/ttimport`, `/ttinfo`, with a deprecated
   `/goblin*` row below.
3. `/ttexport` and paste into tonberrytactics.pages.dev. **The
   gear count should now show 13/13 (or 12/13 if you have a
   two-handed weapon).** Pre-v0.6.5 your AST/PCT/MNK characters
   were showing 3–7 pieces because the crafted gear was being
   silently dropped.
4. Stat Profile on the web should now show real materia totals
   instead of `+0 materia` everywhere — pieces with melded
   materia are finally in the payload.

## Pairing

- **GearGoblin.Core v0.6.5** — lockstep version bump, no source
  changes. Ships separately.
- **TonberryTactics web v0.6.5** — Skill Speed materia prefix fix
  (vendored Core sync), real Meld Audit logic, sell-vs-meld verdict
  row. Ships separately.

## Out of scope (v0.6.6)

- In-game Plan tab paste box for `GG-PLAN:v1:` strings.
- `/ttimport` persistence into `Configuration.JobPlans`.
- Plan-tab meld-checklist UI.
