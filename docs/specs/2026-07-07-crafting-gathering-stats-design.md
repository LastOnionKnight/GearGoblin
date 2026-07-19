# Crafting & Gathering Stats — Read & Show (v1.5.8)

**Date:** 2026-07-07 · **Author:** Cody (Claude Code) · **Status:** DRAFT for operator review
**Ship lane:** Antigravity (tag/push). **Version:** FEATURE → **v1.5.8**, trinity together (VERSIONING.md).

## Goal

Make GearGoblin **read and show** Disciple of the Hand (DoH / crafter) and Disciple of the
Land (DoL / gatherer) stats, in both the in-game plugin and the companion web app. Today the
plugin renders *"Battle stats not applicable for this class"* for those roles; after this
slice a crafter or gatherer sees their real stats, gear, and melds.

**Explicitly NOT in this slice** (deferred to the P6 optimizer follow-on):
- No crafting/gathering effect formulas (Craftsmanship→progress/quality, etc.).
- No meld optimization / BiS / thresholds.
- No web-optimizer changes (the client-side meld solver stays battle-only).

## The 6 stats

| Stat | Discipline | BaseParam ID* | Kind |
|---|---|---|---|
| Craftsmanship | DoH | 70 | main gear stat (linear) |
| Control | DoH | 71 | main gear stat (linear) |
| CP | DoH | 11 | pool (like MP) |
| Gathering | DoL | 72 | main gear stat (linear) |
| Perception | DoL | 73 | main gear stat (linear) |
| GP | DoL | 10 | pool |

\* BaseParam IDs and the 11 ClassJob RowIDs (CRP 8 → CUL 15, MIN 16 / BTN 17 / FSH 18) are
**verified against ffxiv-datamining CSVs during implementation** before use (per the
"verify source before building" rule).

## Approach

**Extend the existing types** (chosen over parallel `CrafterStatSet` types or a full
`StatSnapshot` generalization). Rationale: least invasive, reuses the already-generic V2
export path, delivers read-only value fast. The full generalization of `StatSnapshot` to a
stat map is the right move when the optimizer lands — deferred, not done here.

## Design

### Core (`GearGoblin.Core`) — shared foundation
- `Substat` enum: add the 6 values with their BaseParam IDs.
- `SubstatExt.Display()/Short()` + `StatNames`: names for the 6.
- `JobProfiles.All`: add 11 DoH/DoL profiles — `Role.Crafter`/`Gatherer`, `RelevantStats` =
  that job's 3 stats, empty `BalanceWeights` (no optimizer).
- `StatSnapshot`: add 6 int fields (`Craftsmanship`, `Control`, `CP`, `Gathering`,
  `Perception`, `GP`), default 0 — battle jobs leave them 0, DoH/DoL leave battle stats 0.
- `Caps.HasNoCap`: add all 6 (main/pool stats have no battle-style diminishing cap) so
  nothing renders a misleading "OVER".
- **`CraftGatherReference`** (new, small): approximate current-tier fully-melded value per
  DoH/DoL stat — the "soft reference max" the Character-tab fill bar fills against. Sourced
  from current crafting/gathering BiS; **labeled "approx" in the UI**. Same spirit as the
  battle gauges' rough caps. Lives in Core so plugin + web can share it.
- Materia catalog (`MateriaTiers`/`Materias`): add DoH/DoL materia (Craftsman's
  Command/Cunning/Competence + Gatherer's equivalents) so melds read & label correctly.

### Plugin
- `StatReader`: read the 6 totals from the character sheet into the extended snapshot.
- `CharacterTab.DrawGauges`: replace the `Role.Crafter || Role.Gatherer` "not applicable"
  branch with real gauges for that job's 3 stats — **fill bar vs the `CraftGatherReference`
  soft max** (operator's chosen treatment), no OK/OVER cap state, with an "approx" note. CP/GP
  shown as pool values.
- `MateriaTab`: add dot colors for the 6 stats in `GetMateriaColor` (the card grid is already
  generic, so crafter/gatherer melds render once colored). Melds are **display-only** this
  slice; the per-slot materia soft-cap audit is deferred with the optimizer.
- Export: include the 6 stats in `ExportCharacterV2.TotalStats` when the job is DoH/DoL
  (Cap = the reference max or null).
- Settings: no new toggles (DoH/DoL have no derivation rows); the existing per-stat toggles
  are battle-only and simply don't apply.

### Web (`TonberryTactics`) — parity is nearly free
- Stat panel (`Index.razor:196-200`) already renders `TotalStats` generically
  (`@foreach → <CapGauge>`). DoH/DoL stats appear automatically once the plugin exports them.
- Shown as no-cap (or reference-cap) values via `Caps`. No web UI change required for display.
- Web meld/optimizer sections stay battle-only (out of scope).

## Data flow

`Lumina character sheet → StatReader → StatSnapshot(+DoH/DoL) → CharacterTab gauges (plugin)`
and `→ ExportCharacterV2.TotalStats → GG-EXPORT string → web parse → generic CapGauge`.

## Isolation / units

- `Substat` + `StatNames` + `CraftGatherReference` + `JobProfiles`: pure Core data, no I/O,
  independently testable.
- `StatReader` DoH/DoL read: isolated to the snapshot-build path.
- `CharacterTab` crafter/gatherer branch: self-contained render path, gated on `Role`.
- Export addition: one list-populate step, gated on `Role`.

## Testing / Definition of Done

- Core: builds green; unit check that the 6 stats resolve names/IDs and the 11 job profiles
  return `Crafter`/`Gatherer` with the right `RelevantStats`.
- Plugin: `dotnet build -c Release` green; headless boot clean.
- Web: `dotnet build -c Release` green.
- In-game smoke (operator): log in as one crafter + one gatherer → Character tab shows the 3
  stats with fill bars + "approx", Materia tab shows their melds, export string carries the
  stats, and the web renders them from that export.
- Version lockstep: v1.5.8 across Core/web/plugin (numeric `1.5.8.0`, `<InformationalVersion>`
  = `1.5.8` — no letter this time; per VERSIONING.md).

## Risks / open items

- **Reference-max accuracy:** the soft max is approximate and drifts per patch; the "approx"
  label is the mitigation. Numbers sourced + sanity-checked at impl time.
- **BaseParam / ClassJob IDs:** verified against datamining CSVs before wiring.
- **CP/GP as pools** don't fit a "fill vs max" as cleanly as Craftsmanship/Control — shown as
  value + pool max (max CP/GP is itself gear-derived); acceptable for read-only.
