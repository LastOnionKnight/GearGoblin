// UI/MateriaTab.cs
// Renders the Materia tab content. Milestone 1: stat sheet with percentage values
// and breakpoint distances. Milestones 2 & 3 (Plan / Audit modes) added on top.
//
// Adjust the namespace/import paths to match your project. Drop this in UI/
// and call MateriaTab.Draw() from MainWindow's BeginTabItem("Materia") block.

using Dalamud.Bindings.ImGui;
using GearGoblin.Materia;
using System;
using System.Numerics;

namespace GearGoblin.UI;

public static class MateriaTab
{
    public static void Draw()
    {
        var snap = StatReader.ReadCurrent();
        if (snap is null)
        {
            ImGui.TextDisabled("Stats unavailable. Log in to see your meld advisor.");
            return;
        }

        var s = snap.Value;
        var profile = JobProfiles.GetOrDefault(s.JobId);
        var mod = LevelTable.Get(s.Level);

        // ─── Header ───────────────────────────────────────────────────────
        ImGui.Text($"{profile.Name}  Lv {s.Level}");
        ImGui.SameLine();
        ImGui.TextDisabled($"({profile.Role})");
        ImGui.Separator();

        // ─── Stat sheet ───────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("Stat Sheet");
        ImGui.Spacing();

        if (ImGui.BeginTable("##statsheet", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Stat",       ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Value",      ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Effect",     ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Next Tier",  ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            // Crit (rate + damage on one row)
            DrawCritRow(s.Crit, mod);
            DrawRow("Direct Hit", s.DH, Formulas.DirectHit(s.DH, mod), v => $"{v * 100:0.0}% rate");
            DrawRow("Determination", s.Det, Formulas.Determination(s.Det, mod), v => $"+{v * 100:0.0}% damage");

            // Speed (pick whichever the job uses, or both if neither is dominant)
            if (UsesSks(profile))
                DrawSpeedRow("Skill Speed", s.SkS, mod, profile);
            if (UsesSps(profile))
                DrawSpeedRow("Spell Speed", s.SpS, mod, profile);

            if (profile.Role == Role.Tank)
            {
                DrawRow("Tenacity (dmg)", s.Ten, Formulas.TenacityDamage(s.Ten, mod), v => $"+{v * 100:0.0}% damage");
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

        ImGui.Spacing();
        ImGui.Separator();

        // ─── Placeholder for Plan / Audit (Milestone 2 & 3) ───────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("Meld Advisor");
        ImGui.TextDisabled("Plan and Audit modes coming next. This shows your current");
        ImGui.TextDisabled("substats translated into the percentages they actually buy you.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

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
        // Many jobs (VPR, RPR, NIN) don't actually want speed — flag it.
        if (profile.JobId is 41 or 39 or 30 or 22 or 34 or 38 or 23 or 31)
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), $"⚠ {profile.Name} usually melds away from speed");
        else
            ImGui.TextDisabled($"+{dmg.NextTier - speed} stat → next tier");
    }

    private static bool UsesSks(JobProfile p) =>
        Array.IndexOf(p.RelevantStats, Substat.SkillSpeed) >= 0;

    private static bool UsesSps(JobProfile p) =>
        Array.IndexOf(p.RelevantStats, Substat.SpellSpeed) >= 0;
}
