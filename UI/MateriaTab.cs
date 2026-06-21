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
    internal static bool WantsAuditOnNextDraw;

    public static void Draw(Plugin plugin)
    {
        Theme.TtChrome.Push();
        try
        {
            var inventory = plugin.Inventory;
            // WantsAuditOnNextDraw doesn't do anything structural anymore because there's only one view, 
            // but we consume it just in case.
            if (WantsAuditOnNextDraw)
            {
                WantsAuditOnNextDraw = false;
            }

            var snap = StatReader.ReadCurrent();
            if (snap is null)
            {
                ImGui.TextDisabled("Stats unavailable. Log in to see your meld advisor.");
                return;
            }

            var s = snap.Value;
            var profile = JobProfiles.GetOrDefault(s.JobId);
            var mod = LevelTable.Get(s.Level);

            Theme.TtChrome.Eyebrow(plugin.Fonts, "Materia · Current Melds");
            Theme.TtChrome.Quip(plugin.Fonts, "Every melded substat with overcap and tier audits. Red rows are wasting budget.");
            ImGui.Spacing();
            ImGui.Spacing();

            DrawAudit(plugin, s, profile, mod, inventory);
        }
        finally
        {
            Theme.TtChrome.Pop();
        }
    }

    private static void DrawAudit(Plugin plugin, StatSnapshot s, JobProfile profile, LevelMod mod, IInventoryReader inventory)
    {
        var pieces = BuildMeldablePieces(inventory);
        if (pieces.Count == 0)
        {
            ImGui.TextDisabled("No equipped gear detected.");
            return;
        }

        var result = MeldOptimizer.Optimize(pieces, s, mod, profile, WeightMode.PureMath);

        if (result.Audits.Count == 0)
        {
            ImGui.TextDisabled("No existing melds to audit.");
            return;
        }

        foreach (var audit in result.Audits)
        {
            Theme.TtChrome.BeginPanel("audit_" + audit.Piece + "_" + audit.SlotIndex);
            
            // Layout matching mockup:
            // Orb image
            // slot name
            // Materia name & stat value
            // Verdict

            ImGui.BeginGroup();
            
            if (audit.Current is not null)
            {
                var statName = audit.Current.Value.Stat.ToString().ToLowerInvariant();
                var orbPath = "crit";
                if (statName.Contains("crit")) orbPath = "crit";
                else if (statName.Contains("direct")) orbPath = "dh";
                else if (statName.Contains("deter")) orbPath = "det";
                else if (statName.Contains("skill")) orbPath = "sks";
                else if (statName.Contains("spell")) orbPath = "sps";
                else if (statName.Contains("tenac")) orbPath = "ten";
                else if (statName.Contains("piet")) orbPath = "pie";

                // We don't have orb textures loaded in BrandResources yet, so we'll draw a colored circle or just text
                // Let's use a colored bullet for now
                var color = Theme.TtChrome.Fg;
                if (orbPath == "crit") color = Theme.TtChrome.Over;
                else if (orbPath == "dh") color = Theme.TtChrome.Warn;
                else if (orbPath == "det") color = Theme.TtChrome.FgMuted;
                else if (orbPath == "sks" || orbPath == "sps") color = Theme.TtChrome.Ok;

                ImGui.TextColored(color, "●");
            }
            ImGui.EndGroup();

            ImGui.SameLine(0, 16);

            ImGui.BeginGroup();
            using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.FgMuted, $"{audit.Piece} · {audit.SlotIndex + 1}");
            }
            ImGui.EndGroup();

            ImGui.SameLine(150);

            ImGui.BeginGroup();
            if (audit.Current is not null)
            {
                using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.Fg, audit.Current.Value.Display());
                }
                using (plugin.Fonts.Pixel.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.FgFaint, $"+{audit.Current.Value.Value} {audit.Current.Value.Stat}");
                }
            }
            else
            {
                using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.FgFaint, "Empty Slot");
                }
            }
            ImGui.EndGroup();

            var avail = ImGui.GetContentRegionAvail();
            ImGui.SameLine(ImGui.GetWindowWidth() - 250f);

            ImGui.BeginGroup();
            var sevColor = SeverityColor(audit.Severity);
            using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
            {
                ImGui.TextColored(sevColor, audit.Headline);
                if (audit.SuggestedReplacement is not null && audit.Severity != AuditSeverity.Good)
                {
                    ImGui.TextColored(sevColor, $"→ Replace with {audit.SuggestedReplacement.Value.Display()}");
                }
            }
            ImGui.EndGroup();

            Theme.TtChrome.EndPanel();
            ImGui.Spacing();
        }
    }

    private static List<MeldablePiece> BuildMeldablePieces(IInventoryReader inventory)
    {
        var equipped = inventory.ReadEquipped();
        return equipped.Select(p => p.FromEquipped()).ToList();
    }

    private static Vector4 SeverityColor(AuditSeverity sev) => sev switch
    {
        AuditSeverity.Good     => Theme.TtChrome.Ok,
        AuditSeverity.Minor    => Theme.TtChrome.FgMuted,
        AuditSeverity.Warning  => Theme.TtChrome.Warn,
        AuditSeverity.Critical => Theme.TtChrome.Over,
        _                      => Theme.TtChrome.FgMuted,
    };
}

