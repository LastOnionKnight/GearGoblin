# GearGoblin v0.6.5.1 dropin — "Quiet Info"

**Hotfix.** Fixes the `/ttinfo` hard-crash that surfaced after v0.6.5
shipped. Also trims About-tab "What's New" bloat to the latest three
releases per Brian's ask.

## What's in this dropin

```
Plugin.cs                          overwrite — OnInfoCommand crash fix
UI/MainWindow.cs                    overwrite — About-tab What's New trim (3 latest only)
GearGoblin.csproj                   overwrite — version 0.6.5 → 0.6.5.1
CHANGELOG.md                        overwrite — v0.6.5.1 entry on top
```

Note: this dropin does NOT include `Services/InventoryReader.cs` —
the HQ-offset fix from v0.6.5 is unchanged and already present.

## Build & deploy

```
cd D:\GearGoblin-v0.1\GearGoblin
Move-Item $env:USERPROFILE\Downloads\GearGoblin-v0.6.5.1-dropin.zip . -Force
Expand-Archive -Path .\GearGoblin-v0.6.5.1-dropin.zip -DestinationPath . -Force
Unblock-File .\release.ps1
git status
dotnet build -c Release
.\release.ps1 -DryRun
.\release.ps1
```

## Verify after push

1. **Reload plugin** via `/xlplugins` → Tonberry Tactics → Disable → Enable.
2. **Run `/ttinfo`.** Expected behavior:
   - Tonberry Tactics window opens automatically.
   - **One** short line in chat: "Diagnostics copied to clipboard. Opening
     the Tonberry Tactics window — see the Diagnostics tab for live state."
   - The 15-line block is now in your clipboard (paste anywhere to verify).
   - **No crash.**
3. **Click the Diagnostics tab.** Verify it has the live state you'd
   expect (PanelAttached, CPR detected, Advisor section, etc.). The "Copy
   /ttinfo block to clipboard" button on Diagnostics still works the
   same as it did before.
4. **About tab → scroll to bottom.** What's New section should show
   exactly three version blocks (v0.6.5.1, v0.6.5, v0.6.4) and a
   pointer line: "Full history: github.com/LastOnionKnight/GearGoblin/blob/main/CHANGELOG.md".

## Pairing

- **GearGoblin.Core v0.6.5.1** — lockstep version bump only.
- **TonberryTactics web v0.6.5.1** — off-by-one Tier XII display fix.

## Out of scope (v0.6.6)

- Character-panel advisor row mangle (off-panel positioning rewrite).
- Plan tab `GG-PLAN:v1:` paste UI + persistence + checklist.
- `BrandResources.TryLoad` thread-affinity bug at plugin load (three
  "Not on main thread!" warnings during startup — handled, falls back
  to text-only branding, but should be fixed).
