// UI/CharacterTab.cs
//
// The new landing tab for /tt. Consolidates four pieces of identity
// information into one cohesive surface:
//   1. Hero region    — name, class, level, FC tag, iLvl pill
//   2. Stats strip    — substats with derived effects + next-tier hints
//   3. Materia advisor — top 3 recommendations (or empty-state line)
//   4. Gear table     — 12 equipped slots + Soul Crystal
//
// Long-term goal: this tab replaces the StatusPanelInjector entirely.
// The injection path stays in v0.6.5.x for backward compat but is
// deprecated in v0.6.6 and removed in v0.7.0. Until then, the Character
// tab and the native-panel injection coexist — the tab is the "new way,"
// the injector is the "fossil." We control rendering inside our own
// window, so the cloned-cell-overflow, lifecycle-event-miss, and
// CharacterPanelRefined-collision class of bugs cannot occur here.
//
// THIS IS A SKELETON. v0.6.6 alpha — visual fidelity is intentionally
// low. Each section method below is wired to its real data source and
// renders the content in plain ImGui primitives so the tab is functional
// from day one. The visual polish (typography stack, section-head rule
// lines, stat-card chrome, advisor row ranking, gear table stripes)
// comes in subsequent passes, layering Claude Design v0.2.0's design
// system onto this scaffolding section by section.
//
// Reference deliverable: character-tab/ (Claude Design v0.2.0).
//   - README.md             : 12 ImGui port flags
//   - styles.css            : design tokens (using existing TT :root vars)
//   - Character.html        : composed page, runnable in browser
//   - components/*.jsx      : per-section visual targets

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using GearGoblin.Materia;
using GearGoblin.Services;
using GearGoblin.Theme;

namespace GearGoblin.UI;

public static class CharacterTab
{
    /// <summary>
    /// Renders the Character tab. Called from <c>MainWindow.DrawBody()</c>
    /// inside the BeginTabItem("Character") block. Mirrors the
    /// <c>MateriaTab.Draw()</c> data-sourcing pattern (StatReader →
    /// JobProfiles → LevelTable) and extends it with the live
    /// <see cref="IPlayerCharacter"/> for identity rendering.
    /// </summary>
    public static void Draw(Plugin plugin, IPlayerCharacter player)
    {
        // Stats first — if we can't read them, no point continuing.
        // The hero region can still render with just player+inventory data,
        // but the stats strip and advisor depend on a real snapshot.
        var snap = StatReader.ReadCurrent();
        if (snap is null)
        {
            ImGui.TextDisabled("Player data unavailable. Log in to see your Character panel.");
            return;
        }

        var s       = snap.Value;
        var profile = JobProfiles.GetOrDefault(s.JobId);
        var mod     = LevelTable.Get(s.Level);

        var equipped = plugin.Inventory.ReadEquipped();
        var ilvl     = equipped.Count > 0 ? plugin.Inventory.CalculateAverageItemLevel(equipped) : 0;

        DrawHero(player, profile, ilvl);
        ImGui.Spacing();
        ImGui.Spacing();

        DrawStatsStrip(plugin, s, profile, mod);
        ImGui.Spacing();
        ImGui.Spacing();

        DrawMateriaAdvisor(s, profile, mod, equipped);
        ImGui.Spacing();
        ImGui.Spacing();

        DrawGearTable(equipped);
    }

    // ─── § 4.1 Hero region ─────────────────────────────────────────────────
    //
    // SKELETON: name + class line + iLvl number, no portrait frame, no
    // corner brackets, no Adventurer Plate aesthetic.
    //
    // TODO (visual polish — port from CharacterHero.jsx):
    //   - Portrait frame: 148×148px region with four 6×6 --lantern corner
    //     squares (drawList.AddRectFilled). Center contains either the
    //     player portrait (if Dalamud surfaces Adventurer Plate data in
    //     future) or the job abbreviation in Press Start 2P 32px in
    //     --gold-dim.
    //   - Name in Cinzel 22-26px, --gold-bright.
    //   - "◇ Adventurer Plate" eyebrow above the name in pixel font
    //     8px, --gold-dim.
    //   - Class line: Job name in --frost, separator dot, "Level XXX"
    //     with the level in --lantern.
    //   - FC tag line: «TAG» in --tonberry-bright, then FC name in --frost-dim.
    //   - iLvl pill right-aligned: dark background, gold border, pixel
    //     font label + serif value.
    //   - Bottom gold divider (drawList.AddLine in --gold-dim).
    //   - World/DC strip below: pixel font 8px, --frost-faint.
    private static void DrawHero(IPlayerCharacter player, JobProfile profile, int ilvl)
    {
        DrawSectionHead("Adventurer Plate", $"iLvl {ilvl}");

        var name = player.Name.ToString();
        var lvl  = player.Level;
        ImGui.Text(name);
        ImGui.SameLine();
        ImGui.TextDisabled($"— {profile.Name} Lv {lvl}");

        // World/DC display (placeholder — IClientState has world info
        // via player.CurrentWorld and player.HomeWorld but we'll wire
        // those in the polish pass).
        // FC tag also pending — Dalamud surfaces this via player object.
    }

    // ─── § 4.2 Stats strip ─────────────────────────────────────────────────
    //
    // SKELETON: one substat per line as "Label  Value". No card chrome,
    // no derived-effect math display, no next-tier hint, no warning chip.
    //
    // TODO (visual polish — port from StatsStrip.jsx):
    //   - 4-5 cards in a horizontal CSS-grid-equivalent layout
    //     (BeginTable with WidthStretch columns, or BeginChild per card
    //     with manual SameLine + width calc).
    //   - Per card: pixel 8px label / Cinzel 28px value / mono 11px
    //     derived line / mono 10px next-tier line.
    //   - Speed card: when profile.MeldsAwayFromSpeed, swap card border
    //     to --warning and append a "⚠ {warn}" warn-chip below.
    //   - For tank jobs, render a 5th card for Tenacity. For other roles,
    //     stay at 4 cards.
    //   - Derived-effect math already exists in StatReader/LevelTable;
    //     wire the existing breakpoint helpers from MateriaTab.DrawCritRow
    //     and DrawSpeedRow.
    // ─── § 4.2 Stats strip ────────────────────────────────────────────────
    //
    // v0.6.6.1: card-based layout per Claude Design v0.2.0 spec.
    // Each substat renders as a vertical card in a horizontal grid. Cards
    // surface label / value / derived effect on every job. The optional
    // "next tier" breakpoint hint is stubbed — the real math will land in
    // a v0.6.6.x polish pass once we settle the +N stat → +X% rendering
    // convention. Warn-chip on speed stat fires when the stat exceeds the
    // 420 baseline (the level-100 sub-floor); a more nuanced job-aware
    // heuristic ("BLM melds away from speed", "RDM wants 2.45 GCD") lands
    // in v0.6.6.x once we have per-job speed-meld profiles.

    private readonly record struct StatCardModel(
        string Label,
        int    Value,
        string Derived,
        string Tier,
        string? Warn);

    private static void DrawStatsStrip(Plugin plugin, StatSnapshot s, JobProfile profile, LevelMod mod)
    {
        DrawSectionHead("Substats", $"{profile.Name} · Lv {s.Level}");

        var cards = BuildStatCards(s, profile, mod);
        if (cards.Count == 0)
        {
            // Crafter/Gatherer with no relevant battle substats. Quiet placeholder
            // rather than the misleading 420/420 default values the game returns.
            ImGui.TextDisabled("Battle stats not applicable for this class.");
            return;
        }

        // Render as a horizontal grid via a table. SizingStretchSame gives each
        // card an equal share of the available width.
        if (ImGui.BeginTable("##stats_strip", cards.Count, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextRow();
            foreach (var card in cards)
            {
                ImGui.TableNextColumn();
                DrawStatCard(plugin, card);
            }
            ImGui.EndTable();
        }
    }

    private static List<StatCardModel> BuildStatCards(StatSnapshot s, JobProfile profile, LevelMod mod)
    {
        // Crafter/Gatherer: skip the strip entirely. Their "battle" stats are
        // always 420/420 placeholder values that confuse rather than inform.
        if (profile.Role == Role.Crafter || profile.Role == Role.Gatherer)
            return new List<StatCardModel>();

        var cards = new List<StatCardModel>
        {
            new StatCardModel(
                Label:   "Critical Hit",
                Value:   s.Crit,
                Derived: DerivedStatFormatter.CritCompact(s.Crit, mod),
                Tier:    "",
                Warn:    null),
            new StatCardModel(
                Label:   "Determination",
                Value:   s.Det,
                Derived: DerivedStatFormatter.DetCompact(s.Det, mod),
                Tier:    "",
                Warn:    null),
            new StatCardModel(
                Label:   "Direct Hit",
                Value:   s.DH,
                Derived: DerivedStatFormatter.DhCompact(s.DH, mod),
                Tier:    "",
                Warn:    null),
        };

        if (UsesSks(profile))
        {
            var gcd  = Formulas.GcdFromSpeed(s.SkS, mod);
            var warn = s.SkS > 420 ? "Above 420 baseline" : null;
            cards.Add(new StatCardModel(
                Label:   "Skill Speed",
                Value:   s.SkS,
                Derived: $"{gcd:F2}s GCD",
                Tier:    "min substat (job baseline)",
                Warn:    warn));
        }
        else if (UsesSps(profile))
        {
            var gcd  = Formulas.GcdFromSpeed(s.SpS, mod);
            var warn = s.SpS > 420 ? "Above 420 baseline" : null;
            cards.Add(new StatCardModel(
                Label:   "Spell Speed",
                Value:   s.SpS,
                Derived: $"{gcd:F2}s GCD",
                Tier:    "min substat (job baseline)",
                Warn:    warn));
        }

        if (profile.Role == Role.Tank)
            cards.Add(new StatCardModel(
                Label:   "Tenacity",
                Value:   s.Ten,
                Derived: DerivedStatFormatter.TenacityCompact(s.Ten, mod),
                Tier:    "",
                Warn:    null));
        else if (profile.Role == Role.Healer)
            cards.Add(new StatCardModel(
                Label:   "Piety",
                Value:   s.Pie,
                Derived: DerivedStatFormatter.PietyMpPerTick(s.Pie, mod),
                Tier:    "",
                Warn:    null));

        return cards;
    }

    private static void DrawStatCard(Plugin plugin, StatCardModel m)
    {
        // Border color swaps to Warning if the card is warn-flagged. Background
        // is always InkPanelAlt. Inner padding 12px via WindowPadding style var.
        var border = m.Warn != null ? TlfTheme.Warning : TlfTheme.BorderPixelLite;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, TlfTheme.InkPanelAlt);
        ImGui.PushStyleColor(ImGuiCol.Border,  border);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 12f));

        // Card height — fixed so all cards align even when contents differ in
        // line count. 112px covers label + value + derived + tier line. Cards
        // with warn chips get an additional 24px.
        var cardHeight = m.Warn != null ? 138f : 114f;

        if (ImGui.BeginChild($"##card_{m.Label}", new Vector2(0f, cardHeight), true))
        {
            // Label — Press Start 2P 10px, GoldDim, uppercased
            using (plugin.Fonts.Pixel.PushOrNull())
            {
                ImGui.TextColored(TlfTheme.GoldDim, m.Label.ToUpperInvariant());
            }

            ImGui.Spacing();

            // Value — Cinzel Regular 22px, GoldBright
            using (plugin.Fonts.CinzelHeader.PushOrNull())
            {
                ImGui.TextColored(TlfTheme.GoldBright, m.Value.ToString("N0"));
            }

            // Derived effect line — default font, FrostSoft
            if (!string.IsNullOrEmpty(m.Derived))
            {
                ImGui.TextColored(TlfTheme.FrostSoft, m.Derived);
            }

            // Tier divider + tier text (only if tier line is populated)
            if (!string.IsNullOrEmpty(m.Tier))
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.TextColored(TlfTheme.FrostDim, m.Tier);
            }

            // Warn chip — Press Start 2P 10px, Warning color
            if (m.Warn != null)
            {
                ImGui.Spacing();
                using (plugin.Fonts.Pixel.PushOrNull())
                {
                    ImGui.TextColored(TlfTheme.Warning, $"⚠ {m.Warn.ToUpperInvariant()}");
                }
            }
        }
        ImGui.EndChild();

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
    }

    // ─── § 4.3 Materia advisor inline ──────────────────────────────────────
    //
    // SKELETON: top-3 recommendations as plain text lines, or a single
    // empty-state line. Logic mirrors StatusPanelInjector.UpdateAdvisor
    // candidate-building so users see the same recommendations whether
    // they look at the injected native panel or this tab.
    //
    // TODO (visual polish — port from MateriaAdvisorCard.jsx):
    //   - Card container with --ink-panel-alt background, 1px --frost-faint
    //     border, 14×16px padding.
    //   - Each row: rank prefix in pixel font 9px ("01"/"02"/"03"), slot
    //     name in --knife, "←" in --lantern, materia name in --frost,
    //     delta in --ship green, optional gain-badge on the right.
    //   - Row separators: 1px --frost-faint between rows, none on first/last.
    //   - Empty state: serif italic 15px in --frost-soft, with "◆" prefix
    //     in --ship green, centered, padded.
    //   - Footer: right-aligned "See full audit in Materia tab →" link in
    //     --gold-dim with --lantern hover state. Clicking it should select
    //     the Materia tab — wire via setting an ImGui tab focus flag or a
    //     static "wantsMateriaTabFocus" hint that MainWindow picks up next
    //     frame.
    private static void DrawMateriaAdvisor(
        StatSnapshot                       s,
        JobProfile                         profile,
        LevelMod                           mod,
        IReadOnlyList<EquippedPiece>       equipped)
    {
        if (equipped.Count == 0)
        {
            DrawSectionHead("Materia Advisor", "no gear");
            ImGui.TextDisabled("No equipped items detected.");
            return;
        }

        // Use PureMath as the skeleton default. The Mode toggle (PureMath
        // vs BalancePreset) lives in MateriaTab today and we'll add a
        // mirrored selector here in a polish pass, or accept that the
        // Character tab uses one default and the Materia tab is the place
        // to A/B between modes.
        OptimizerResult opt;
        try
        {
            // Convert EquippedPiece (inventory layer) → MeldablePiece (optimizer layer).
            // Same pattern StatusPanelInjector.UpdateAdvisor uses (Services/StatusPanelInjector.cs ~line 678).
            var pieces = equipped.Select(MeldSlotsBuilder.FromEquipped).ToList();
            opt = MeldOptimizer.Optimize(pieces, s, mod, profile, WeightMode.PureMath);
        }
        catch (Exception ex)
        {
            DrawSectionHead("Materia Advisor", "errored");
            ImGui.TextColored(new Vector4(0.85f, 0.45f, 0.45f, 1f),
                $"Advisor errored: {ex.GetType().Name}");
            ImGui.TextDisabled("See /xllog (search 'MeldOptimizer') for details.");
            return;
        }

        // Mirror StatusPanelInjector.UpdateAdvisor's candidate-list build —
        // top 3 audits by gain, fall back to PlanRecommendations to fill 3.
        var candidates = new List<string>();

        var topAudits = opt.Audits
            .Where(a => a.Severity is AuditSeverity.Critical or AuditSeverity.Warning)
            .Where(a => a.SuggestedReplacement is not null)
            .OrderByDescending(a => a.GainIfReplaced)
            .Take(3);
        foreach (var audit in topAudits)
        {
            candidates.Add($"{audit.Piece} #{audit.SlotIndex + 1} → {audit.SuggestedReplacement!.Value.Display()}");
        }
        if (candidates.Count < 3)
        {
            var topPlans = opt.PlanRecommendations
                .OrderByDescending(r => r.ScoreGain)
                .Take(3 - candidates.Count);
            foreach (var rec in topPlans)
            {
                candidates.Add($"{rec.Piece} #{rec.SlotIndex + 1} ← {rec.Materia.Display()}");
            }
        }

        var rightRail = candidates.Count == 0
            ? "all slots optimal"
            : $"{candidates.Count} of {opt.Audits.Count} suggested";

        DrawSectionHead("Materia Advisor", rightRail);

        if (candidates.Count == 0)
        {
            ImGui.TextDisabled("◆ All guaranteed slots filled · no upgrades suggested");
        }
        else
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                ImGui.Text($"{i + 1:00}  {candidates[i]}");
            }
        }
    }

    // ─── § 4.4 Gear table ──────────────────────────────────────────────────
    //
    // SKELETON: identical to MainWindow.DrawCurrentGear() — same columns,
    // same row construction, same data source. Lifted verbatim so the
    // Character tab is functionally complete from day one. The existing
    // "Current Gear" tab still works; this is a parallel rendering, not
    // a replacement of DrawCurrentGear yet.
    //
    // TODO (visual polish — port from GearTable.jsx):
    //   - Row stripes: alternate --ink-panel / --ink-panel-alt via
    //     ImGuiTableFlags.RowBg + TableSetBgColor per row.
    //   - HQ ★ inline with item name in --gold-bright.
    //   - Soul Crystal row gets a darker background and a 1px top border
    //     (drawList.AddLine after the table renders).
    //   - Slot column in pixel font 9px, --gold-dim.
    //   - Item column in Cinzel.
    //   - iLvl column right-aligned in mono.
    //   - Materia column: each meld in mono with "·" separator dots in
    //     --frost-faint.
    private static void DrawGearTable(IReadOnlyList<EquippedPiece> equipped)
    {
        DrawSectionHead("Equipped Gear", $"{equipped.Count} slots");

        if (equipped.Count == 0)
        {
            ImGui.TextDisabled("No equipped items detected.");
            return;
        }

        if (ImGui.BeginTable("##chartabgear", 4,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Slot",    ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Item",    ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("iLvl",    ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Materia", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var piece in equipped)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(piece.Slot.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(piece.IsHighQuality ? $"{piece.Name} ★" : piece.Name);
                ImGui.TableNextColumn(); ImGui.Text(piece.ItemLevel.ToString());
                ImGui.TableNextColumn();
                if (piece.Materia.Count == 0)
                {
                    ImGui.TextDisabled("—");
                }
                else
                {
                    foreach (var m in piece.Materia)
                    {
                        ImGui.TextUnformatted($"+{m.StatValue} {m.StatName}");
                        if (m.SlotIndex < piece.Materia.Count - 1) ImGui.SameLine();
                    }
                }
            }

            ImGui.EndTable();
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Skeleton section-head: simple separator + "── label" left, right-rail
    /// text right-aligned. Polish pass replaces this with the Press Start
    /// 2P 9px label in --gold-dim, flex-grow 1px rule line, and right-rail
    /// in pixel font matching the Claude Design v0.2.0 spec.
    /// </summary>
    private static void DrawSectionHead(string label, string rightRail)
    {
        ImGui.Separator();
        ImGui.TextDisabled($"── {label}");

        if (!string.IsNullOrEmpty(rightRail))
        {
            ImGui.SameLine();
            var avail = ImGui.GetContentRegionAvail();
            var sz    = ImGui.CalcTextSize(rightRail);
            ImGui.SameLine(ImGui.GetCursorPosX() + Math.Max(0, avail.X - sz.X - 8));
            ImGui.TextDisabled(rightRail);
        }
    }

    private static bool UsesSks(JobProfile p) =>
        p.Role == Role.Tank || p.Role == Role.MeleeDps || p.Role == Role.PhysicalRangedDps;

    private static bool UsesSps(JobProfile p) =>
        p.Role == Role.MagicalRangedDps || p.Role == Role.Healer;
}
