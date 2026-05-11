// UI/MateriaTab.cs
// Renders the Materia tab. Three sub-modes: Stat Sheet / Plan / Audit.
// Plus a Pure-math vs Balance-preset toggle for Plan/Audit.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GearGoblin.Materia;
using GearGoblin.Services;

namespace GearGoblin.UI;

public static class MateriaTab
{
    private static int s_subTabIndex = 0;
    private static WeightMode s_weightMode = WeightMode.PureMath;

    public static void Draw(InventoryReader inventory)
    {
        var snap = StatReader.ReadCurrent();
        if (snap is null)
        {
            ImGui.TextDisabled("Stats unavailable. Log in to see your meld advisor.");
            return;
        }

        var s       = snap.Value;
        var profile = JobProfiles.GetOrDefault(s.JobId);
        var mod     = LevelTable.Get(s.Level);

        ImGui.Text($"{profile.Name}  Lv {s.Level}");
        ImGui.SameLine();
        ImGui.TextDisabled($"({profile.Role})");
        ImGui.Separator();

        DrawSubTabSelector();
        ImGui.Spacing();

        switch (s_subTabIndex)
        {
            case 0: DrawStatSheet(s, profile, mod); break;
            case 1: DrawPlan(s, profile, mod, inventory); break;
            case 2: DrawAudit(s, profile, mod, inventory); break;
        }
    }

    private static void DrawSubTabSelector()
    {
        if (ImGui.RadioButton("Stat Sheet", s_subTabIndex == 0)) s_subTabIndex = 0;
        ImGui.SameLine();
        if (ImGui.RadioButton("Plan", s_subTabIndex == 1)) s_subTabIndex = 1;
        ImGui.SameLine();
        if (ImGui.RadioButton("Audit", s_subTabIndex == 2)) s_subTabIndex = 2;

        if (s_subTabIndex != 0)
        {
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(40, 0));
            ImGui.SameLine();
            ImGui.TextDisabled("Mode:");
            ImGui.SameLine();
            if (ImGui.RadioButton("Pure math", s_weightMode == WeightMode.PureMath))
                s_weightMode = WeightMode.PureMath;
            ImGui.SameLine();
            if (ImGui.RadioButton("Balance preset", s_weightMode == WeightMode.BalancePreset))
                s_weightMode = WeightMode.BalancePreset;

            // v0.4.0: caption explaining the active mode. Single italic line,
            // disabled-text style so it doesn't compete with the tab content.
            DrawModeDisclaimer();
        }
    }

    private static void DrawModeDisclaimer()
    {
        // Indent a little so the caption visually attaches to the selector
        // rather than the sub-tab body that follows.
        ImGui.Indent(12);
        if (s_weightMode == WeightMode.PureMath)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
            ImGui.TextWrapped(
                "Pure math ranks melds by the formula deltas alone, with no opinion. " +
                "Doesn't model Crit's multiplier effect on Det/DH — for tighter raid recs, " +
                "switch to Balance preset.");
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
            ImGui.TextWrapped(
                "Balance preset uses per-job weights from thebalanceffxiv.com. " +
                "Community consensus circa Patch 7.5 (May 2026); weights drift over time.");
            ImGui.PopStyleColor();
        }
        ImGui.Unindent(12);
    }

    // ─── Stat Sheet ────────────────────────────────────────────────────────

    private static void DrawStatSheet(StatSnapshot s, JobProfile profile, LevelMod mod)
    {
        ImGui.TextUnformatted("Current substats and breakpoints");
        ImGui.Spacing();

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
        ImGui.TableNextColumn(); ImGui.TextUnformatted($"{rate.DisplayValue * 100:0.0}% rate, {dmg.DisplayValue:0.000}× dmg");
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

    // ─── Plan mode ─────────────────────────────────────────────────────────

    private static void DrawPlan(StatSnapshot s, JobProfile profile, LevelMod mod, InventoryReader inventory)
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
            ImGui.TextDisabled("Switch to Audit to review whether existing melds are optimal.");
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

    // ─── Audit mode ────────────────────────────────────────────────────────

    private static void DrawAudit(StatSnapshot s, JobProfile profile, LevelMod mod, InventoryReader inventory)
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
            ImGui.TextDisabled("No existing melds to audit. Switch to Plan to see fill recommendations.");
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

    private static List<MeldablePiece> BuildMeldablePieces(InventoryReader inventory)
    {
        var equipped = inventory.ReadEquipped();
        return equipped.Select(p => MeldSlotsBuilder.FromEquipped(p)).ToList();
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
