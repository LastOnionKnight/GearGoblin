// UI/MateriaTab.cs
// v0.6.6.4: Stat Sheet + Plan merged into one default landing per Fork 1
// (toggle treatment). The 3-radio sub-tab selector is gone. The default
// view shows current substats and recommended fills stacked on a single
// scroll surface; an [Audit ▸] toggle in the top-right swaps the body to
// the audit view, and a [Balance preset] toggle re-runs the optimizer
// with the alternate weighting.
//
// State:
//   s_showingAudit  — false (default) shows Stat Sheet + Plan; true shows Audit
//   s_weightMode    — Pure math (default) vs Balance preset
//
// Cross-tab signal:
//   WantsAuditOnNextDraw — set by CharacterTab's "See full audit in
//   Materia tab →" link to land the user directly on the Audit view
//   when MateriaTab.Draw next runs.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GearGoblin.Materia;
using GearGoblin.Core.Materia;
using GearGoblin.Services;
using GearGoblin.Theme;

namespace GearGoblin.UI;

public static class MateriaTab
{
    private static bool       s_showingAudit;
    private static WeightMode s_weightMode = WeightMode.PureMath;

    // v0.6.6.4: cross-tab signal from CharacterTab's audit-link footer.
    // When true on next Draw, lands the user on the Audit view directly.
    internal static bool WantsAuditOnNextDraw;

    public static void Draw(Plugin plugin)
    {
        Theme.TtChrome.Push();
        try
        {
            var inventory = plugin.Inventory;
        // Consume cross-tab signal if pending
        if (WantsAuditOnNextDraw)
        {
            s_showingAudit       = true;
            WantsAuditOnNextDraw = false;
        }

        var snap = StatReader.ReadCurrent();
        if (snap is null)
        {
            ImGui.TextDisabled("Stats unavailable. Log in to see your meld advisor.");
            return;
        }

        var s       = snap.Value;
        var profile = JobProfiles.GetOrDefault(s.JobId);
        var mod     = LevelTable.Get(s.Level);

        // Header line — job/level/role on the left, toggles right-aligned
        DrawHeader(profile, s.Level);
        ImGui.Separator();
        ImGui.Spacing();

        if (s_showingAudit)
        {
            DrawAuditView(plugin, s, profile, mod, inventory);
        }
        else
        {
            DrawDefaultView(plugin, s, profile, mod, inventory);
        }
        }
        finally
        {
            Theme.TtChrome.Pop();
        }
    }

    // ─── Header + toolbar ─────────────────────────────────────────────────

    private static void DrawHeader(JobProfile profile, int level)
    {
        ImGui.Text($"{profile.Name}  Lv {level}");
        ImGui.SameLine();
        ImGui.TextDisabled($"({profile.Role})");

        // Right-align the toggle pair. Compute reserved width based on the
        // longest button label so the buttons stay anchored to the right edge
        // regardless of which mode is active.
        var auditLabel  = s_showingAudit ? "◀ Back to gear"   : "Audit ▸";
        var modeLabel   = s_weightMode == WeightMode.PureMath ? "Pure math" : "Balance preset";
        var auditWidth  = ImGui.CalcTextSize(auditLabel).X  + 16f;
        var modeWidth   = ImGui.CalcTextSize(modeLabel).X   + 16f;
        var reservedW   = auditWidth + modeWidth + 8f;
        var availableW  = ImGui.GetContentRegionAvail().X;
        if (availableW > reservedW + 20f)
        {
            ImGui.SameLine(ImGui.GetCursorPosX() + availableW - reservedW);
        }
        else
        {
            // Narrow window — drop the toggles to a new line rather than truncate
            ImGui.NewLine();
        }

        // Balance preset toggle (only meaningful when viewing Plan; still
        // shown in Audit since the optimizer feeds both surfaces)
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TtChrome.FrostMuted);
        if (ImGui.SmallButton(modeLabel))
        {
            s_weightMode = s_weightMode == WeightMode.PureMath
                ? WeightMode.BalancePreset
                : WeightMode.PureMath;
        }
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, s_showingAudit ? Theme.TtChrome.Ember : Theme.TtChrome.FrostMuted);
        if (ImGui.SmallButton(auditLabel))
        {
            s_showingAudit = !s_showingAudit;
        }
        ImGui.PopStyleColor();
    }

    // ─── Default view: Stat Sheet + Recommended Fills stacked ─────────────

    private static void DrawDefaultView(Plugin plugin, StatSnapshot s, JobProfile profile, LevelMod mod, IInventoryReader inventory)
    {
        Theme.TtChrome.BeginCard();
        DrawSectionHead(plugin, "Current Substats", null);
        DrawStatSheet(plugin, s, profile, mod);
        Theme.TtChrome.EndCard();

        ImGui.Spacing();
        ImGui.Spacing();

        Theme.TtChrome.BeginCard();
        DrawSectionHead(plugin, "Recommended Fills", null);
        DrawModeDisclaimer();
        ImGui.Spacing();
        DrawPlan(s, profile, mod, inventory);
        Theme.TtChrome.EndCard();
    }

    // ─── Audit view ───────────────────────────────────────────────────────

    private static void DrawAuditView(Plugin plugin, StatSnapshot s, JobProfile profile, LevelMod mod, IInventoryReader inventory)
    {
        Theme.TtChrome.BeginCard();
        DrawSectionHead(plugin, "Materia Audit", null);
        DrawAudit(s, profile, mod, inventory);
        Theme.TtChrome.EndCard();
    }

    // Section head mirrors CharacterTab's visual treatment (gold-bright title,
    // hairline separator). Local copy to avoid cross-class coupling.
    private static void DrawSectionHead(Plugin plugin, string title, string? rightRail)
    {
        Theme.TtChrome.Eyebrow(plugin.Fonts, title);
        if (!string.IsNullOrEmpty(rightRail))
        {
            ImGui.SameLine();
            var w   = ImGui.GetContentRegionAvail().X;
            var rw  = ImGui.CalcTextSize(rightRail).X;
            if (w > rw + 8f)
                ImGui.SameLine(ImGui.GetCursorPosX() + w - rw - 4f);
            ImGui.TextDisabled(rightRail);
        }
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void DrawModeDisclaimer()
    {
        ImGui.Indent(12);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
        if (s_weightMode == WeightMode.PureMath)
        {
            ImGui.TextWrapped(
                "Pure math ranks melds by the formula deltas alone, with no opinion. " +
                "Doesn't model Crit's multiplier effect on Det/DH — for tighter raid recs, " +
                "switch to Balance preset.");
        }
        else
        {
            ImGui.TextWrapped(
                "Balance preset uses per-job weights from thebalanceffxiv.com. " +
                "Community consensus circa Patch 7.5 (May 2026); weights drift over time.");
        }
        ImGui.PopStyleColor();
        ImGui.Unindent(12);
    }

    // ─── Stat Sheet ────────────────────────────────────────────────────────

    private static void DrawStatSheet(Plugin plugin, StatSnapshot s, JobProfile profile, LevelMod mod)
    {
        if (ImGui.BeginTable("##statsheet", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Stat",      ImGuiTableColumnFlags.WidthFixed,  120);
            ImGui.TableSetupColumn("Value",     ImGuiTableColumnFlags.WidthFixed,  80);
            ImGui.TableSetupColumn("Effect",    ImGuiTableColumnFlags.WidthFixed,  220);
            ImGui.TableSetupColumn("Next Tier", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            DrawCritRow(s.Crit, mod);
            DrawRow("Direct Hit",    s.DH,  Formulas.DirectHit(s.DH, mod),     v => $"{v * 100:0.0}% rate");
            DrawRow("Determination", s.Det, Formulas.Determination(s.Det, mod),v => $"+{v * 100:0.0}% damage");

            if (UsesSks(profile)) DrawSpeedRow("Skill Speed", s.SkS, mod, profile);
            if (UsesSps(profile)) DrawSpeedRow("Spell Speed", s.SpS, mod, profile);

            if (profile.Role == Role.Tank)
            {
                DrawRow("Tenacity (dmg)", s.Ten, Formulas.TenacityDamage(s.Ten, mod),     v => $"+{v * 100:0.0}% damage");
                DrawRow("Tenacity (mit)", s.Ten, Formulas.TenacityMitigation(s.Ten, mod), v => $"-{v * 100:0.0}% damage taken");
            }
            if (profile.Role == Role.Healer)
            {
                var pie = Formulas.Piety(s.Pie, mod);
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted("Piety");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(s.Pie.ToString());
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{pie.DisplayValue:0} MP/tick");
                ImGui.TableNextColumn(); ImGui.TextDisabled("(fight-dependent)");
            }

            ImGui.EndTable();
        }
    }

    private static void DrawCritRow(int crit, in LevelMod mod)
    {
        var rate = Formulas.CritRate(crit, mod);
        var dmg  = Formulas.CritDmg(crit, mod);
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.TextUnformatted("Critical Hit");
        ImGui.TableNextColumn(); ImGui.TextUnformatted(crit.ToString());
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{rate.DisplayValue * 100:0.0}% rate, {dmg.DisplayValue:0.000}× dmg");
        ImGui.TableNextColumn();
        var deltaToNext = rate.NextTier - crit;
        if (deltaToNext > 0)
            ImGui.TextDisabled($"+{deltaToNext} stat → +0.1% rate (at {rate.NextTier})");
        else
            ImGui.TextDisabled("at maximum tier within rounding");
    }

    private static void DrawRow(string name, int value, StatBreakdown breakdown, Func<double, string> fmt)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.TextUnformatted(name);
        ImGui.TableNextColumn(); ImGui.TextUnformatted(value.ToString());
        ImGui.TableNextColumn(); ImGui.TextUnformatted(fmt(breakdown.DisplayValue));
        ImGui.TableNextColumn();
        var delta = breakdown.NextTier - value;
        if (delta > 0)
            ImGui.TextDisabled($"+{delta} stat → next 0.1% (at {breakdown.NextTier})");
        else
            ImGui.TextDisabled("at maximum tier within rounding");
    }

    private static void DrawSpeedRow(string label, int speed, in LevelMod mod, JobProfile profile)
    {
        var dmg = Formulas.SpeedDamage(speed, mod);
        var gcd = Formulas.GcdFromSpeed(speed, mod);
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.TextUnformatted(label);
        ImGui.TableNextColumn(); ImGui.TextUnformatted(speed.ToString());
        ImGui.TableNextColumn(); ImGui.TextUnformatted($"{gcd:0.00}s GCD, +{dmg.DisplayValue * 100:0.0}% dmg");
        ImGui.TableNextColumn();
        if (profile.JobId is 41 or 39 or 30 or 22 or 34 or 38 or 23 or 31)
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), $"⚠ {profile.Name} usually melds away from speed");
        else
            ImGui.TextDisabled($"+{dmg.NextTier - speed} stat → next tier");
    }

    // ─── Plan body ────────────────────────────────────────────────────────

    private static void DrawPlan(StatSnapshot s, JobProfile profile, LevelMod mod, IInventoryReader inventory)
    {
        var pieces = BuildMeldablePieces(inventory);
        if (pieces.Count == 0)
        {
            ImGui.TextDisabled("No equipped gear detected.");
            return;
        }

        var result = MeldOptimizer.Optimize(pieces, s, mod, profile, s_weightMode);

        if (result.PlanRecommendations.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.5f, 1f),
                "No empty meld slots — your gear is fully melded.");
            ImGui.TextDisabled("Toggle Audit ▸ to review whether existing melds are optimal.");
            return;
        }

        ImGui.TextUnformatted($"{result.PlanRecommendations.Count} empty slot(s) found. Recommended fills:");
        ImGui.Spacing();

        if (ImGui.BeginTable("##plan", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Piece",     ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Slot",      ImGuiTableColumnFlags.WidthFixed,  90);
            ImGui.TableSetupColumn("Recommend", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Why",       ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var rec in result.PlanRecommendations)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{rec.Piece} — {rec.PieceName}");

                ImGui.TableNextColumn();
                if (rec.IsGuaranteedSlot)
                    ImGui.TextUnformatted($"#{rec.SlotIndex + 1}");
                else
                    ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), $"#{rec.SlotIndex + 1} (overmeld)");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(rec.Materia.Display());

                ImGui.TableNextColumn();
                ImGui.TextWrapped(rec.Reasoning);
            }
            ImGui.EndTable();
        }
    }

    // ─── Audit body ───────────────────────────────────────────────────────

    private static void DrawAudit(StatSnapshot s, JobProfile profile, LevelMod mod, IInventoryReader inventory)
    {
        var pieces = BuildMeldablePieces(inventory);
        if (pieces.Count == 0)
        {
            ImGui.TextDisabled("No equipped gear detected.");
            return;
        }

        var result = MeldOptimizer.Optimize(pieces, s, mod, profile, s_weightMode);

        if (result.Audits.Count == 0)
        {
            ImGui.TextDisabled("No existing melds to audit. Use ◀ Back to gear to see fill recommendations.");
            return;
        }

        var bySev = result.Audits.GroupBy(a => a.Severity).ToDictionary(g => g.Key, g => g.Count());
        var critCount  = bySev.GetValueOrDefault(AuditSeverity.Critical);
        var warnCount  = bySev.GetValueOrDefault(AuditSeverity.Warning);
        var minorCount = bySev.GetValueOrDefault(AuditSeverity.Minor);
        var goodCount  = bySev.GetValueOrDefault(AuditSeverity.Good);

        if (critCount > 0)
            ImGui.TextColored(SeverityColor(AuditSeverity.Critical),
                $"⛔ {critCount} critical issue{(critCount == 1 ? "" : "s")}");
        if (warnCount > 0)
            ImGui.TextColored(SeverityColor(AuditSeverity.Warning),
                $"⚠ {warnCount} warning{(warnCount == 1 ? "" : "s")}");
        if (minorCount > 0)
            ImGui.TextColored(SeverityColor(AuditSeverity.Minor),
                $"ℹ {minorCount} minor issue{(minorCount == 1 ? "" : "s")}");
        if (goodCount > 0)
            ImGui.TextColored(SeverityColor(AuditSeverity.Good),
                $"✓ {goodCount} solid meld{(goodCount == 1 ? "" : "s")}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.BeginTable("##audit", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Piece",   ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Slot",    ImGuiTableColumnFlags.WidthFixed,  60);
            ImGui.TableSetupColumn("Current", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Status",  ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            var sorted = result.Audits
                .OrderBy(a => SeverityOrder(a.Severity))
                .ThenBy(a => a.PieceName);

            foreach (var audit in sorted)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{audit.Piece} — {audit.PieceName}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"#{audit.SlotIndex + 1}");
                ImGui.TableNextColumn();
                if (audit.Current is not null)
                    ImGui.TextUnformatted(audit.Current.Value.Display());
                else
                    ImGui.TextDisabled("(empty)");

                ImGui.TableNextColumn();
                var color = SeverityColor(audit.Severity);
                ImGui.TextColored(color, audit.Headline);
                if (!string.IsNullOrWhiteSpace(audit.Detail))
                    ImGui.TextDisabled(audit.Detail);
                if (audit.SuggestedReplacement is not null && audit.Severity != AuditSeverity.Good)
                    ImGui.TextDisabled($"→ Replace with {audit.SuggestedReplacement.Value.Display()}");
            }
            ImGui.EndTable();
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static List<MeldablePiece> BuildMeldablePieces(IInventoryReader inventory)
    {
        var equipped = inventory.ReadEquipped();
        return equipped.Select(p => p.FromEquipped()).ToList();
    }

    private static Vector4 SeverityColor(AuditSeverity sev) => sev switch
    {
        AuditSeverity.Good     => new Vector4(0.5f,  0.9f,  0.5f, 1f),
        AuditSeverity.Minor    => new Vector4(0.7f,  0.85f, 1.0f, 1f),
        AuditSeverity.Warning  => new Vector4(1.0f,  0.75f, 0.3f, 1f),
        AuditSeverity.Critical => new Vector4(1.0f,  0.4f,  0.4f, 1f),
        _                      => new Vector4(0.7f,  0.7f,  0.7f, 1f),
    };

    private static int SeverityOrder(AuditSeverity sev) => sev switch
    {
        AuditSeverity.Critical => 0,
        AuditSeverity.Warning  => 1,
        AuditSeverity.Minor    => 2,
        AuditSeverity.Good     => 3,
        _                      => 4,
    };

    private static bool UsesSks(JobProfile p) =>
        Array.IndexOf(p.RelevantStats, Substat.SkillSpeed) >= 0;

    private static bool UsesSps(JobProfile p) =>
        Array.IndexOf(p.RelevantStats, Substat.SpellSpeed) >= 0;
}

