# GearGoblin v0.4.7 dropin

**Status: scaffolding commit.** This dropin lays down the v0.4.7
source structure on top of v0.4.6 — bumped version, new
`/goblinimport` command (registered + validating, persist body
TODO), new `Services/GearsetImporter.cs`, new `Planning/` namespace
with the typed `GG-PLAN:v1:` schema, extended
`Configuration.JobPlanData` for imported plans, and the previously
held-back Feedback tab now live.

Safe to deploy: every TODO is a wiring stub, not a crash hazard.
`/goblinimport` is reachable; it just reports honestly that the
persistence step lands in the next build. Nothing else regresses
from v0.4.6.

If you want the full round-trip working in-game, wait for the
v0.4.7 build commit that fills in the TODOs in `GearsetImporter`.
If you want to test the scaffold (validate plan strings, see the
import chat output, exercise the Feedback tab), this is the
dropin.

---

## What ships in this scaffold

### Source additions

- `Planning/PlanSchema.cs` — typed C# records mirroring TT's
  `PlanPayloadV1` / `PlanMeldV1`. `JsonPropertyName` attributes
  pin field mapping so we don't depend on serializer policy.
- `Services/GearsetImporter.cs` — public `ImportFromClipboard()`
  and `ImportFromString(wire)` methods with the full validation
  flow:
    1. Prefix check (must start with `GG-PLAN:v1:`)
    2. Base64 decode
    3. JSON parse to `PlanPayloadV1`
    4. Schema version validation
    5. Emitter identity check
    6. Character context warnings (job/ContentId mismatch — TODO)
    7. Configuration persistence (TODO)
  Returns a `PlanImportResult` carrying the typed payload + any
  warnings.

### Configuration additions

- `PlanMode.Imported` — new enum value alongside `Casual` and
  `Raider`.
- `JobPlanData.ImportedPlanJson` — decoded JSON body of the last
  imported plan, stored so it's inspectable in the config file.
- `JobPlanData.ImportedAt` — timestamp the plan was applied;
  surfaced in the future Plan tab "Active Plan" section.
- `JobPlanData.MeldCompletion` — per-meld tick state for the
  checklist UI. Persisted across plugin reloads.

### Plugin.cs additions

- `/goblinimport` slash command registered, with inline `args`
  variant: `/goblinimport <GG-PLAN:v1:...>` routes to
  `ImportFromString`; bare `/goblinimport` will (when wired) read
  clipboard.
- `OnImportCommand` handler invokes the importer, reports parse
  success / failure / meld count to chat, and surfaces warnings.
- New `Importer` property of type `GearsetImporter` constructed
  alongside `Exporter`.

### MainWindow.cs additions

- **Feedback tab** between Diagnostics and About. Category radio
  (Bug / Idea / Confusion / Hi), multiline message input, "Include
  diagnostic info" checkbox (on by default). Two buttons:
  "Open GitHub issue (pre-filled)" constructs a
  `https://github.com/LastOnionKnight/GearGoblin/issues/new?title=...&body=...&labels=...`
  URL and opens the default browser; "Copy to clipboard for
  Discord / DM" puts the same markdown payload on the clipboard.
  Both paths share a single `BuildFeedbackPayload()` helper.
  No webhooks, no analytics, no auto-submit.
- **Quick Start tab** — `/goblinimport` row drops the "(v0.4.7+)"
  caveat; IMPORT step description goes from future-tense to
  present-tense with a clear scaffold-state note.

---

## What's still TODO before v0.4.7 ships proper

These are the unwired pieces. The scaffold names every one of
them explicitly with `TODO(v0.4.7 build):` comments.

1. **Clipboard read in `ImportFromClipboard`.** Currently returns
   "clipboard empty." Needs Dalamud's clipboard provider (or
   `System.Windows.Forms.Clipboard` fallback) wired up.
2. **Configuration persist step.** After validation succeeds,
   write the JSON body into
   `Configuration.JobPlans[contentId][jobId].ImportedPlanJson`,
   set `Mode = PlanMode.Imported`, set `ImportedAt = UtcNow`,
   reset `MeldCompletion` to a list of falses sized to the meld
   count, call `Configuration.Save()`.
3. **Character context warnings.** Compare
   `payload.SourceCharacter.Job` against current job and
   ContentId against current character; add warnings (not errors)
   on mismatch so the Plan tab can ask the user to confirm before
   applying.
4. **Plan tab "Active Plan" UI surface.** Show imported plan with
   per-meld checkboxes correlating against equipped gear,
   source indicator ("Plan source: Tonberry Tactics import,
   2026-05-13 14:32 UTC"), "Clear plan" button.
5. **(Stretch goal)** Character-panel checklist injection. Surface
   the meld steps below the Materia Advisor when an imported plan
   is active, so progress is visible without opening `/goblin`.

---

## Build & verify

```powershell
cd D:\GearGoblin-v0.1\GearGoblin
Unblock-File -Path .\GearGoblin-v0.4.7-dropin.zip
Expand-Archive -Path .\GearGoblin-v0.4.7-dropin.zip -DestinationPath . -Force
dotnet build
```

Expected: clean build, no warnings beyond v0.4.6 baseline.

In-game:

```
/xlreload GearGoblin
```

Verify in `/xllog`:

- `GearGoblin v0.4.7.0 loaded.`
- No exceptions on startup.

Smoke test:

1. `/goblin` opens the window. Tab order: Quick Start | Current
   Gear | Plan | Materia | Settings | Diagnostics | **Feedback** |
   About.
2. Open the **Feedback** tab. Type "scaffold smoke test." Click
   **Copy to clipboard for Discord / DM**. Paste somewhere —
   confirm the payload has `### Category`, `### Message`, and (if
   the checkbox is on) a fenced `### Diagnostic info` block.
3. From Tonberry Tactics, copy a `GG-PLAN:v1:...` string. In-game
   type `/goblinimport`. Expect chat output:
   `[GearGoblin] Plan parsed successfully. (N meld(s) recommended.)
   Persisting to active plan: not yet wired in v0.4.7 scaffold.`
4. Type `/goblinimport notavalidstring`. Expect:
   `[GearGoblin] Import failed: Doesn't look like a GG-PLAN:v1: string. ...`

Everything else (Quick Start, Current Gear, Plan, Materia,
Settings, Diagnostics, About, `/goblinexport`, `/goblininfo`,
Materia Advisor injection, CPR coexistence) behaves identically
to v0.4.6.

---

## Roadmap reminder

- **v0.4.7 (this commit, scaffold)** — `/goblinimport` validator,
  Feedback tab, schema types, Configuration extensions.
- **v0.4.7 build (next session)** — fill in the five TODOs above.
- **v0.5.0** — `GearGoblin.Core.dll` refactor. Shared materia
  formulas, derivation math, and wire-format types extracted into
  a netstandard library consumed by both the plugin and Tonberry
  Tactics' Blazor build. Retires the duplicated optimizer code in
  TT.
- **v0.5.x backlog** — Plan library (multiple named BiS per job
  for SCH GCD A/B and similar healer-shaped flexibility).
- **v0.6.x backlog** — "Open in XIVGear" deep-link export (reuses
  existing XIVGear-fetch path, so it's small surface for big
  perceived value).
