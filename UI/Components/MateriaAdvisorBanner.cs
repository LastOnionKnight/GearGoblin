using Dalamud.Bindings.ImGui;
using GearGoblin.Theme;
using System.Numerics;

namespace GearGoblin.UI.Components;

public static class MateriaAdvisorBanner
{
    public static void Draw(Plugin plugin)
    {
        // 1px border with FrostFaint, InkDeeper background (matches advisor cards)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.TtChrome.InkDeeper);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.TtChrome.FrostFaint);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16f, 14f));

        // Use auto-resize for height to accommodate wrapping
        if (ImGui.BeginChild("##advisor_cap_banner", new Vector2(0f, 0f), true, ImGuiWindowFlags.AlwaysAutoResize))
        {
            using (plugin.Fonts.GaramondItalic.PushOrNull())
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.TtChrome.EmberDeep);
                ImGui.TextWrapped("Note: The advisor does not yet account for per-piece substat caps. " +
                                  "Check the in-game materia melding panel before melding stats that may already be at their cap on a piece. " +
                                  "A full fix is in development.");
                ImGui.PopStyleColor();
            }
        }
        ImGui.EndChild();

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
        
        ImGui.Spacing();
        ImGui.Spacing();
    }
}
