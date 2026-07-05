using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GearGoblin.Core;
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
        var allEquipped = inventory.ReadEquipped();
        var pieces = allEquipped
            .Where(p => p.Slot != EquipSlot.Unknown)
            .Select(p => p.FromEquipped())
            .ToList();

        if (pieces.Count == 0)
        {
            ImGui.TextDisabled("No equipped gear detected.");
            return;
        }

        var result = MeldOptimizer.Optimize(pieces, s, mod, profile, WeightMode.PureMath);

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(11, 11));
        if (ImGui.BeginTable("materia_grid", 2, ImGuiTableFlags.None))
        {
            foreach (var piece in pieces)
            {
                ImGui.TableNextColumn();
                DrawGearCard(plugin, piece, result.Audits.Where(a => a.Piece == piece.Slot).ToList(), profile);
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();
        DrawLegend(plugin);
    }

    private static void DrawGearCard(Plugin plugin, MeldablePiece piece, List<MeldAudit> audits, JobProfile profile)
    {
        bool isEmpty = piece.EmptySlotCount == piece.Slots.Count && !audits.Any(a => a.Current != null);
        
        if (isEmpty)
        {
            ImGui.PushStyleColor(ImGuiCol.Border, Theme.TtChrome.LineSoft);
        }

        Theme.TtChrome.BeginPanel("card_" + piece.Slot, 84f);

        var draw = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;

        // --- gc-top ---
        ImGui.BeginGroup();
        
        // HQ Star and Name
        using (plugin.Fonts.GaramondBody.PushOrNull())
        {
            if (piece.IsHighQuality)
            {
                ImGui.TextColored(Theme.TtChrome.Gold, "★ ");
                ImGui.SameLine(0, 0);
            }
            ImGui.TextColored(Theme.TtChrome.Fg, piece.Name);
        }

        // Ilvl Pill
        var ilvlText = $"i{piece.ItemLevel}";
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            var ilvlSize = ImGui.CalcTextSize(ilvlText);
            var ilvlPos = new Vector2(p.X + w - ilvlSize.X, p.Y);
            ImGui.SetCursorScreenPos(ilvlPos);
            ImGui.TextColored(Theme.TtChrome.GoldDim, ilvlText);
        }

        // Aggregate Badge
        DrawAuditBadge(plugin, audits, p.X + w, p.Y + 24f, profile);

        ImGui.EndGroup();

        ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + 44f));
        
        // --- gc-bot ---
        ImGui.BeginGroup();
        using (plugin.Fonts.Pixel.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.CobaltBright, piece.Slot.ToString().ToUpperInvariant());
        }

        if (isEmpty)
        {
            ImGui.SameLine();
            using (plugin.Fonts.GaramondItalic.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.FgFaint, "no melds");
            }
        }
        else
        {
            // Dots
            float dotX = p.X + w; // Start from right
            foreach (var audit in audits.AsEnumerable().Reverse())
            {
                if (audit.Current == null) continue;
                dotX -= 16f; // spacing
                DrawDot(draw, new Vector2(dotX, p.Y + 48f), GetMateriaColor(audit.Current.Value.Stat));
            }
        }
        ImGui.EndGroup();

        Theme.TtChrome.EndPanel();

        if (isEmpty)
        {
            ImGui.PopStyleColor(); // Pop border override
        }

        if (ImGui.IsItemHovered())
        {
            DrawHoverTooltip(plugin, piece, audits, profile);
        }
    }

    private static void DrawAuditBadge(Plugin plugin, List<MeldAudit> audits, float rightX, float y, JobProfile profile)
    {
        if (audits.Count == 0 || !audits.Any(a => a.Current != null)) return;

        bool hasWaste = false;
        bool hasOver = false;
        string wasteText = "";
        int totalOvercap = 0;

        foreach (var a in audits)
        {
            if (a.Severity == AuditSeverity.Critical) 
            {
                hasWaste = true;
                if (a.Current != null)
                {
                    wasteText = $"{a.Current.Value.Stat.ToString().Substring(0, 3)} · 0 dmg";
                }
            }
            else if (a.Severity == AuditSeverity.Warning)
            {
                hasOver = true;
                if (a.Current != null && a.Headline.Contains("(-"))
                {
                    var start = a.Headline.IndexOf("(-") + 2;
                    var end = a.Headline.IndexOf(")", start);
                    if (end > start && int.TryParse(a.Headline.Substring(start, end - start), out int parsed))
                    {
                        totalOvercap += parsed;
                    }
                }
            }
        }

        string text = "clean";
        Vector4 color = Theme.TtChrome.Ok;

        if (hasWaste)
        {
            text = wasteText;
            color = Theme.TtChrome.Over;
        }
        else if (hasOver)
        {
            text = $"+{Math.Max(totalOvercap, 1)} overcap";
            color = Theme.TtChrome.Warn;
        }

        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            var size = ImGui.CalcTextSize(text);
            var draw = ImGui.GetWindowDrawList();
            var padding = new Vector2(8, 2);
            var rectMin = new Vector2(rightX - size.X - padding.X * 2, y);
            var rectMax = new Vector2(rightX, y + size.Y + padding.Y * 2);

            draw.AddRectFilled(rectMin, rectMax, ImGui.GetColorU32(new Vector4(color.X, color.Y, color.Z, 0.12f)), 999f);
            draw.AddRect(rectMin, rectMax, ImGui.GetColorU32(color), 999f);

            ImGui.SetCursorScreenPos(new Vector2(rectMin.X + padding.X, rectMin.Y + padding.Y));
            ImGui.TextColored(color, text);
        }
    }

    private static void DrawDot(ImDrawListPtr draw, Vector2 center, Vector4 color)
    {
        draw.AddCircleFilled(center, 5f, ImGui.GetColorU32(color));
        draw.AddCircle(center, 5f, ImGui.GetColorU32(Theme.TtChrome.Bg2), 12, 1.5f);
    }

    private static Vector4 GetMateriaColor(Substat stat) => stat switch
    {
        Substat.CriticalHit => Theme.TtChrome.MatCrit,
        Substat.DirectHit   => Theme.TtChrome.MatDh,
        Substat.Determination => Theme.TtChrome.MatDet,
        Substat.SkillSpeed  => Theme.TtChrome.MatSks,
        Substat.SpellSpeed  => Theme.TtChrome.MatSps,
        Substat.Tenacity    => Theme.TtChrome.MatTen,
        Substat.Piety       => Theme.TtChrome.MatPie,
        _ => Theme.TtChrome.FgMuted
    };

    private static void DrawHoverTooltip(Plugin plugin, MeldablePiece piece, List<MeldAudit> audits, JobProfile profile)
    {
        ImGui.BeginTooltip();
        
        int filled = audits.Count(a => a.Current != null);
        int total = piece.Slots.Count;
        
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.FgMuted, $"MELDS · {filled} of {total}");
        }
        ImGui.Separator();

        foreach (var audit in audits)
        {
            if (audit.Current == null) continue;
            
            var c = audit.Current.Value;
            ImGui.TextColored(GetMateriaColor(c.Stat), "●");
            ImGui.SameLine();
            using (plugin.Fonts.GaramondBody.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.Fg, c.Display());
            }
            ImGui.SameLine(200f);
            using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.Fg2, $"+{c.Value} {c.Stat.Short()}");
            }
        }

        ImGui.Separator();
        // Footer verdict logic similar to badge
        DrawAuditBadge(plugin, audits, ImGui.GetCursorScreenPos().X + 300f, ImGui.GetCursorScreenPos().Y, profile);
        ImGui.EndTooltip();
    }

    private static void DrawLegend(Plugin plugin)
    {
        var draw = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        using (plugin.Fonts.Pixel.PushOrNull())
        {
            float xOffset = 0;
            var legends = new[] { 
                ("MATERIA", Theme.TtChrome.FgMuted),
                ("CRITICAL HIT", Theme.TtChrome.MatCrit),
                ("DIRECT HIT", Theme.TtChrome.MatDh),
                ("DETERMINATION", Theme.TtChrome.MatDet),
                ("SKILL SPEED", Theme.TtChrome.MatSks),
                ("SPELL SPEED", Theme.TtChrome.MatSps),
                ("TENACITY", Theme.TtChrome.MatTen),
                ("PIETY", Theme.TtChrome.MatPie)
            };

            foreach (var (label, color) in legends)
            {
                DrawDot(draw, new Vector2(p.X + xOffset + 6f, p.Y + 6f), color);
                ImGui.SetCursorScreenPos(new Vector2(p.X + xOffset + 16f, p.Y));
                ImGui.TextColored(Theme.TtChrome.FgFaint, label);
                xOffset += ImGui.CalcTextSize(label).X + 32f;
            }
        }
    }
}

