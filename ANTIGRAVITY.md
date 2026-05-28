# Antigravity Instructions for GearGoblin (Tonberry Tactics)

Welcome to the Gear Goblin / Tonberry Tactics repository! 
As an Antigravity agent working in this project, you must strictly follow these rules:

## 1. Project Conventions
- **Naming**: The internal name is `GearGoblin`. The public facing display name is `Tonberry Tactics`.
- **UI Architecture**: Do not use `TlfTheme`. It has been deleted. All UI tabs and visual components must be built using `TtChrome` from the Track 2 design specs. Wrap all tab contents in `Theme.TtChrome.BeginCard()` and `Theme.TtChrome.EndCard()`.
- **Slash Commands**: Use `/tt`, `/ttexport`, `/ttimport`, `/ttinfo`. Do NOT use the legacy `/goblin*` aliases.

## 2. Version Lockstep (The "Trinity")
- The Core (`GearGoblin-Core-v0.1`), Plugin (`GearGoblin-v0.1`), and Web (`TonberryTactics-workspace`) must always be kept in strict lockstep versioning.
- If you bump the version in `GearGoblin.csproj` and `repo.json`, you **must** bump it in the core and web projects as well.

## 3. Releases & About Panel
- Always document changes in `CHANGELOG.md` following the Keep-A-Changelog format.
- Run `release.ps1` to build the plugin. It automatically extracts the changelog into `Resources/about-changelog.txt` to populate the in-game About panel, preventing the release notes from going out of date.

## 4. Workspace Consolidation
- All dev work orders and design briefs live in `D:\Tonberry-Devops\_workorders\` and `_briefs\`.
- Downloads are for transit only.

Stay sharp, keep the ember and frost aesthetics clean, and remember: No Gear. No Hope. No Pants. Just Onions.
