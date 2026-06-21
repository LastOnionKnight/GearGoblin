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
using GearGoblin.Core.Materia;
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

        DrawHero(plugin, player, profile, ilvl);
        ImGui.Spacing();
        ImGui.Spacing();

        DrawStatsStrip(plugin, s, profile, mod);
        ImGui.Spacing();
        ImGui.Spacing();

        DrawMateriaAdvisor(plugin, s, profile, mod, equipped);
        ImGui.Spacing();
        ImGui.Spacing();

        DrawGearTable(plugin, equipped);
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
    // ─── § 4.1 Hero region ─────────────────────────────────────────────────
    //
    // v0.6.6.2: portrait frame + identity column per Claude Design v0.2.0.
    // Left column: 148×166 framed region (InkPanel bg, BorderPixelLite
    // border, four 6×6 Lantern-gold corner brackets inset 2px from the
    // corners). When no Adventurer Plate portrait is available — which is
    // currently always, since Dalamud doesn't expose Plate textures — the
    // frame's centered glyph is the 3-letter jobAbbr in Press Start 2P
    // at 32px (the new PixelDisplay font handle, added in this version).
    // Right column: player name (Cinzel 22px, GoldBright), class line
    // (default font, FrostSoft), world (Pixel 10px uppercased, FrostDim),
    // and an optional FC tag pill (Pixel 10px, TonberryBright) when the
    // player is in a Free Company.
    //
    // All player-data accessors that reach into Lumina row references
    // (HomeWorld, ClassJob.Abbreviation, CompanyTag) go through Safe*
    // helpers below — local players are normally guaranteed to have
    // valid refs, but loading screens / between-zone moments can
    // briefly return RowId == 0, so we degrade gracefully rather than
    // throw at the first frame after a class change.

    private static void DrawHero(Plugin plugin, IPlayerCharacter player, JobProfile profile, int ilvl)
    {
        DrawSectionHead("Adventurer Plate", $"iLvl {ilvl}");

        var drawList   = ImGui.GetWindowDrawList();
        var topLeft    = ImGui.GetCursorScreenPos();
        var portraitSz = new Vector2(148f, 166f);
        var portraitBR = new Vector2(topLeft.X + portraitSz.X, topLeft.Y + portraitSz.Y);

        // Portrait frame: filled background + 1px border.
        drawList.AddRectFilled(topLeft, portraitBR, ColorToU32(Theme.TtChrome.InkDark));
        drawList.AddRect(topLeft, portraitBR, ColorToU32(Theme.TtChrome.FrostOutline));

        // Four lantern-gold corner brackets — 6×6 filled squares, inset 2px.
        var lantern = ColorToU32(Theme.TtChrome.Ember);
        DrawCornerSquare(drawList, topLeft.X + 2f,                   topLeft.Y + 2f,           lantern);
        DrawCornerSquare(drawList, portraitBR.X - 2f - 6f,           topLeft.Y + 2f,           lantern);
        DrawCornerSquare(drawList, topLeft.X + 2f,                   portraitBR.Y - 2f - 6f,   lantern);
        DrawCornerSquare(drawList, portraitBR.X - 2f - 6f,           portraitBR.Y - 2f - 6f,   lantern);

        // Centered jobAbbr fallback glyph.
        var jobAbbr = SafeJobAbbr(player);
        using (plugin.Fonts.PixelDisplay.PushOrNull())
        {
            var ts  = ImGui.CalcTextSize(jobAbbr);
            var pos = new Vector2(
                topLeft.X + (portraitSz.X - ts.X) * 0.5f,
                topLeft.Y + (portraitSz.Y - ts.Y) * 0.5f);
            drawList.AddText(pos, ColorToU32(Theme.TtChrome.EmberDeep), jobAbbr);
        }

        // Identity column to the right of the portrait, 18px gap.
        ImGui.SetCursorScreenPos(new Vector2(portraitBR.X + 18f, topLeft.Y));
        ImGui.BeginGroup();
        {
            // Player name — Cinzel Header
            using (plugin.Fonts.CinzelHeader.PushOrNull())
                ImGui.TextColored(Theme.TtChrome.EmberBright, player.Name.ToString());

            // Class line — default font, FrostSoft
            ImGui.TextColored(Theme.TtChrome.FrostMuted, $"{profile.Name} · Lv {player.Level}");

            ImGui.Spacing();
            ImGui.Spacing();

            // World — Pixel uppercased, FrostDim
            var world = SafeWorld(player);
            if (!string.IsNullOrEmpty(world))
            {
                using (plugin.Fonts.Pixel.PushOrNull())
                    ImGui.TextColored(Theme.TtChrome.FrostFaint, world.ToUpperInvariant());
            }

            // FC tag — Pixel, TonberryBright, surrounded by guillemets
            var fc = SafeFcTag(player);
            if (!string.IsNullOrEmpty(fc))
            {
                ImGui.Spacing();
                using (plugin.Fonts.Pixel.PushOrNull())
                    ImGui.TextColored(Theme.TtChrome.HpGreenBright, $"« {fc.ToUpperInvariant()} »");
            }
        }
        ImGui.EndGroup();

        // Advance cursor below the portrait (taller of the two columns wins;
        // we conservatively use the portrait height since identity column
        // varies with FC presence).
        ImGui.SetCursorScreenPos(new Vector2(topLeft.X, portraitBR.Y + 8f));
    }

    // Small 6×6 filled square — corner-bracket primitive for the portrait frame.
    private static void DrawCornerSquare(ImDrawListPtr drawList, float x, float y, uint color)
    {
        drawList.AddRectFilled(
            new Vector2(x,       y),
            new Vector2(x + 6f,  y + 6f),
            color);
    }

    // Vector4 (linear RGBA, 0..1) → packed uint (ABGR, 0..255).
    // Manual conversion avoids version-dependent ImGui.GetColorU32 /
    // ImGui.ColorConvertFloat4ToU32 ambiguity across binding generations.
    private static uint ColorToU32(Vector4 c)
    {
        static byte B(float f) => (byte)Math.Clamp(f * 255f, 0f, 255f);
        return ((uint)B(c.W) << 24)
             | ((uint)B(c.Z) << 16)
             | ((uint)B(c.Y) << 8)
             |  (uint)B(c.X);
    }

    // Lumina-row accessors — defensive against RowId == 0 transients
    // around loading screens and class swaps.
    private static string SafeJobAbbr(IPlayerCharacter player)
    {
        try { return player.ClassJob.Value.Abbreviation.ExtractText(); }
        catch { return "???"; }
    }

    private static string SafeWorld(IPlayerCharacter player)
    {
        try { return player.HomeWorld.Value.Name.ExtractText(); }
        catch { return ""; }
    }

    private static string SafeFcTag(IPlayerCharacter player)
    {
        try { return player.CompanyTag.ToString(); }
        catch { return ""; }
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

    // v0.6.6.3: cross-tab focus signal — DrawMateriaAdvisor sets this to true when
    // the user clicks the "See full audit in Materia tab" footer link. MainWindow.cs
    // reads it in the next frame's BeginTabItem call and passes ImGuiTabItemFlags.SetSelected
    // to the Materia tab, then resets the flag.
    internal static bool WantsMateriaTabFocus;

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
        var border = m.Warn != null ? Theme.TtChrome.SeverityWarning : Theme.TtChrome.FrostOutline;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.TtChrome.InkDeeper);
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
                ImGui.TextColored(Theme.TtChrome.EmberDeep, m.Label.ToUpperInvariant());
            }

            ImGui.Spacing();

            // Value — Cinzel Regular 22px, GoldBright
            using (plugin.Fonts.CinzelHeader.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.EmberBright, m.Value.ToString("N0"));
            }

            // Derived effect line — default font, FrostSoft
            if (!string.IsNullOrEmpty(m.Derived))
            {
                ImGui.TextColored(Theme.TtChrome.FrostMuted, m.Derived);
            }

            // Tier divider + tier text (only if tier line is populated)
            if (!string.IsNullOrEmpty(m.Tier))
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.TextColored(Theme.TtChrome.FrostFaint, m.Tier);
            }

            // Warn chip — Press Start 2P 10px, Warning color
            if (m.Warn != null)
            {
                ImGui.Spacing();
                using (plugin.Fonts.Pixel.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.SeverityWarning, $"⚠ {m.Warn.ToUpperInvariant()}");
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
    // ─── § 4.3 Materia advisor (ranked rows + gain badges) ────────────────
    //
    // v0.6.6.3: replaces v0.6.6.0's plain-text rank-prefix dump with a card
    // layout per Claude Design v0.2.0's MateriaAdvisorCard.jsx spec. The card
    // has three states: empty (italic ◆ message), populated (1-3 ranked rows
    // with rank prefix + slot + arrow glyph + materia/replacement + gain
    // badge), and errored (preserves v0.6.6.0's defensive try/catch path).
    // Footer is a Selectable styled to look like a link — clicking it sets
    // WantsMateriaTabFocus, which MainWindow.cs reads on the next frame to
    // pass SetSelected to the Materia tab's BeginTabItem.
    //
    // The candidate-build logic itself is unchanged from v0.6.6.0:
    // top-3 audits by gain (Critical/Warning, with a SuggestedReplacement);
    // fall back to PlanRecommendations to fill the remaining row count.
    // Mirrors StatusPanelInjector.UpdateAdvisor so users see the same
    // recommendations in both surfaces during the transition.

    private readonly record struct AdvisorRecModel(
        int    Rank,
        string Slot,
        string Direction,   // "→" for audit, "←" for plan
        string Materia,
        string GainBadge);  // pre-formatted "+0.42%" string or empty

    private static void DrawMateriaAdvisor(
        Plugin                             plugin,
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

        OptimizerResult opt;
        try
        {
            var pieces = equipped.Select(p => p.FromEquipped()).ToList();
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

        // Build the candidate list — same logic as StatusPanelInjector.UpdateAdvisor.
        var recs = new List<AdvisorRecModel>();

        var topAudits = opt.Audits
            .Where(a => a.Severity is AuditSeverity.Critical or AuditSeverity.Warning)
            .Where(a => a.SuggestedReplacement is not null)
            .OrderByDescending(a => a.GainIfReplaced)
            .Take(3)
            .ToList();
        foreach (var audit in topAudits)
        {
            recs.Add(new AdvisorRecModel(
                Rank:      recs.Count + 1,
                Slot:      $"{audit.Piece} #{audit.SlotIndex + 1}",
                Direction: "→",
                Materia:   audit.SuggestedReplacement!.Value.Display(),
                GainBadge: FormatGain(audit.GainIfReplaced)));
        }
        if (recs.Count < 3)
        {
            var topPlans = opt.PlanRecommendations
                .OrderByDescending(r => r.ScoreGain)
                .Take(3 - recs.Count);
            foreach (var rec in topPlans)
            {
                recs.Add(new AdvisorRecModel(
                    Rank:      recs.Count + 1,
                    Slot:      $"{rec.Piece} #{rec.SlotIndex + 1}",
                    Direction: "←",
                    Materia:   rec.Materia.Display(),
                    GainBadge: FormatGain(rec.ScoreGain)));
            }
        }

        var rightRail = recs.Count == 0
            ? "all slots optimal"
            : $"{recs.Count} of {opt.Audits.Count} suggested";
        DrawSectionHead("Materia Advisor", rightRail);

        // ── Card chrome ──────────────────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.TtChrome.InkDeeper);
        ImGui.PushStyleColor(ImGuiCol.Border,  Theme.TtChrome.FrostFaint);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16f, 14f));

        var cardHeight = recs.Count == 0 ? 96f : 50f + (recs.Count * 30f) + 50f;
        if (ImGui.BeginChild("##advisor_card", new Vector2(0f, cardHeight), true))
        {
            if (recs.Count == 0)
            {
                // Empty state - LanternMark or glyph in Ship green, italic Garamond message
                if (plugin.Brand.LanternMark != null)
                {
                    ImGui.Image(plugin.Brand.LanternMark.Handle, new System.Numerics.Vector2(22, 22), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, Theme.TtChrome.HpGreen);
                }
                else
                {
                    ImGui.TextColored(Theme.TtChrome.HpGreen, "◆");
                }
                ImGui.SameLine();
                using (plugin.Fonts.GaramondItalic.PushOrNull())
                    ImGui.TextColored(Theme.TtChrome.FrostMuted,
                        "All guaranteed slots filled · no upgrades suggested");
            }
            else
            {
                if (ImGui.BeginTable("##advisor_rows", 2,
                    ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn("desc", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("gain", ImGuiTableColumnFlags.WidthFixed, 78f);

                    foreach (var r in recs)
                    {
                        ImGui.TableNextRow();

                        // Left: rank + slot + arrow + materia
                        ImGui.TableNextColumn();
                        using (plugin.Fonts.Pixel.PushOrNull())
                            ImGui.TextColored(Theme.TtChrome.EmberDeep, $"{r.Rank:00}");
                        ImGui.SameLine();
                        ImGui.TextColored(Theme.TtChrome.FrostOutline, r.Slot);
                        ImGui.SameLine();
                        ImGui.TextColored(Theme.TtChrome.Ember, r.Direction);
                        ImGui.SameLine();
                        ImGui.TextColored(Theme.TtChrome.FrostText, r.Materia);

                        // Right: gain badge in pixel font, Ice color
                        ImGui.TableNextColumn();
                        if (!string.IsNullOrEmpty(r.GainBadge))
                        {
                            using (plugin.Fonts.Pixel.PushOrNull())
                                ImGui.TextColored(Theme.TtChrome.SeverityNote, r.GainBadge);
                        }
                    }
                    ImGui.EndTable();
                }
            }

            // ── Footer: separator + clickable link ───────────────────────
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Selectable styled as a link: text in Lantern color, hover/active
            // background suppressed so it doesn't read as a button.
            ImGui.PushStyleColor(ImGuiCol.Text,          Theme.TtChrome.Ember);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive,  new Vector4(0f, 0f, 0f, 0f));
            if (ImGui.Selectable("See full audit in Materia tab →"))
            {
                WantsMateriaTabFocus       = true;
                MateriaTab.WantsAuditOnNextDraw = true;
            }
            ImGui.PopStyleColor(3);
        }
        ImGui.EndChild();

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
    }

    // Format a raw optimizer score as a human-readable percentage badge.
    // The optimizer's score is "weighted marginal percentage gain" so it's
    // already roughly a fraction — multiplying by 100 gives a calibrated %.
    // Negative or zero scores render empty so we never display "+0.00%".
    private static string FormatGain(double score)
    {
        if (score <= 0) return "";
        return $"+{score * 100d:F2}%";
    }

    // ─── § 4.4 Gear table ──────────────────────────────────────────────────
    //
    // v0.6.6.5 — polish pass. The TODO list from v0.6.6.0's skeleton:
    //   ✅ Row stripes: alternate InkPanel / InkPanelAlt via per-row
    //      TableSetBgColor(RowBg0). (We don't use ImGuiTableFlags.RowBg
    //      because its default light-grey alternation clashes with TlfTheme.)
    //   ✅ HQ ★ inline with item name in GoldBright (via SameLine).
    //   ✅ Slot column in pixel font, GoldDim, uppercased.
    //   ✅ Item column in Cinzel (CinzelEmphasis 16px) + Frost.
    //   ✅ iLvl column right-aligned in default mono-feeling font + Knife.
    //   ✅ Materia column: each meld + faint "·" separator dots in FrostFaint.
    //   — (Soul Crystal divider deferred: EquippedPiece doesn't model the
    //      soul-crystal slot — the InventoryReader skips it. Revisit if/when
    //      soul-crystal data lands in the equipped list.)
    //
    // The header row is omitted — the section head supplies context and
    // the column header text was visual noise on the dark TlfTheme surface.
    private static void DrawGearTable(Plugin plugin, IReadOnlyList<EquippedPiece> equipped)
    {
        DrawSectionHead("Equipped Gear", $"{equipped.Count} slots");

        if (equipped.Count == 0)
        {
            ImGui.TextDisabled("No equipped items detected.");
            return;
        }

        // Pre-pack stripe colors once. ImGui.GetColorU32(Vector4) is the
        // canonical conversion from TlfTheme palette entries to the U32 the
        // bg-color setter wants.
        uint stripeEven = ImGui.GetColorU32(Theme.TtChrome.InkDark);
        uint stripeOdd  = ImGui.GetColorU32(Theme.TtChrome.InkDeeper);

        if (ImGui.BeginTable("##chartabgear", 4,
                ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Slot",    ImGuiTableColumnFlags.WidthFixed, 96);
            ImGui.TableSetupColumn("Item",    ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("iLvl",    ImGuiTableColumnFlags.WidthFixed, 56);
            ImGui.TableSetupColumn("Materia", ImGuiTableColumnFlags.WidthStretch);
            // No TableHeadersRow() — see comment above.

            for (int i = 0; i < equipped.Count; i++)
            {
                var piece = equipped[i];
                ImGui.TableNextRow();

                // Row stripe — even rows InkPanel, odd rows InkPanelAlt.
                ImGui.TableSetBgColor(
                    ImGuiTableBgTarget.RowBg0,
                    (i & 1) == 0 ? stripeEven : stripeOdd);

                // ── Slot col: Press Start 2P 10px, GoldDim, UPPERCASED ────
                ImGui.TableNextColumn();
                using (plugin.Fonts.Pixel.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.EmberDeep, piece.Slot.ToString().ToUpperInvariant());
                }

                // ── Item col: Cinzel SemiBold 16px in Frost, ★ in GoldBright ─
                ImGui.TableNextColumn();
                using (plugin.Fonts.CinzelEmphasis.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.FrostText, piece.Name);
                    if (piece.IsHighQuality)
                    {
                        ImGui.SameLine(0, 6);
                        ImGui.TextColored(Theme.TtChrome.EmberBright, "★");
                    }
                }

                // ── iLvl col: default font, Knife (steel), right-aligned ──
                ImGui.TableNextColumn();
                var ilvlStr   = piece.ItemLevel.ToString();
                var ilvlWidth = ImGui.CalcTextSize(ilvlStr).X;
                var availW    = ImGui.GetContentRegionAvail().X;
                if (availW > ilvlWidth)
                {
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + availW - ilvlWidth);
                }
                ImGui.TextColored(Theme.TtChrome.FrostOutline, ilvlStr);

                // ── Materia col: Frost + " · " separator in FrostFaint ────
                ImGui.TableNextColumn();
                if (piece.Materia.Count == 0)
                {
                    ImGui.TextColored(Theme.TtChrome.FrostFaint, "—");
                }
                else
                {
                    for (int m = 0; m < piece.Materia.Count; m++)
                    {
                        var meld = piece.Materia[m];
                        ImGui.TextColored(Theme.TtChrome.FrostText, $"+{meld.StatValue} {meld.StatName}");
                        if (m < piece.Materia.Count - 1)
                        {
                            ImGui.SameLine(0, 4);
                            ImGui.TextColored(Theme.TtChrome.FrostFaint, "·");
                            ImGui.SameLine(0, 4);
                        }
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

