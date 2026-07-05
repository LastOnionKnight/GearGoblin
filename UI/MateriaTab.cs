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
            Theme.TtChrome.Quip(plugin.Fonts, "One card per equipped piece. Dots are melded substats, colored by type — hover any card for the full meld breakdown and cap audit.");
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

        // --- Overcap summary bar ---
        int overCount = 0;
        int wasteCount = 0;
        int cleanCount = 0;
        int overTotal = 0;
        int totalPieces = pieces.Count;
        List<string> overPieces = new();
        List<string> wasteStats = new();
        
        foreach (var p in pieces)
        {
            var pAudits = result.Audits.Where(a => a.Piece == p.Slot).ToList();
            bool hasWaste = pAudits.Any(a => a.Severity == AuditSeverity.Critical);
            bool hasOver = !hasWaste && pAudits.Any(a => a.Severity == AuditSeverity.Warning);
            
            if (hasWaste) 
            {
                wasteCount++;
                foreach (var w in pAudits.Where(a => a.Severity == AuditSeverity.Critical))
                    wasteStats.Add(w.Headline); // Simplification: collect headlines
            }
            else if (hasOver) 
            {
                overCount++;
                overPieces.Add(p.Slot.ToString());
                foreach (var w in pAudits.Where(a => a.Severity == AuditSeverity.Warning))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(w.Headline, @"\(-\d+\)");
                    if (match.Success)
                    {
                        var valStr = match.Value.Trim('(', ')', '-');
                        if (int.TryParse(valStr, out int val)) overTotal += val;
                    }
                }
            }
            else cleanCount++;
        }

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.TtChrome.Sink);
        ImGui.BeginChild("mat-summary", new Vector2(0, 80), true, ImGuiWindowFlags.NoScrollbar);
        var sumW = ImGui.GetContentRegionAvail().X / 3f;
        
        // Card 1: Overcap
        ImGui.BeginGroup();
        using (plugin.Fonts.Pixel.PushOrNull()) ImGui.TextColored(Theme.TtChrome.Warn, "OVERCAP");
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.SetWindowFontScale(2.0f);
            ImGui.TextUnformatted($"+{overTotal}");
            ImGui.SetWindowFontScale(1.0f);
        }
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull()) 
        {
            string overSlots = overCount > 0 ? string.Join(", ", overPieces) : "None";
            ImGui.TextColored(Theme.TtChrome.FgFaint, $"{overCount} piece(s) · {overSlots}");
        }
        ImGui.EndGroup();
        
        // Card 2: Waste
        ImGui.SameLine(sumW);
        ImGui.BeginGroup();
        using (plugin.Fonts.Pixel.PushOrNull()) ImGui.TextColored(Theme.TtChrome.Over, "ZERO-VALUE");
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.SetWindowFontScale(2.0f);
            ImGui.TextUnformatted($"{wasteCount}");
            ImGui.SetWindowFontScale(1.0f);
        }
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull()) 
        {
            string wasteStr = wasteCount > 0 ? "melds" : "None";
            ImGui.TextColored(Theme.TtChrome.FgFaint, $"{wasteCount} piece(s) · {wasteStr}");
        }
        ImGui.EndGroup();
        
        // Card 3: Clean
        ImGui.SameLine(sumW * 2);
        ImGui.BeginGroup();
        using (plugin.Fonts.Pixel.PushOrNull()) ImGui.TextColored(Theme.TtChrome.Ok, "CLEAN");
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.SetWindowFontScale(2.0f);
            ImGui.TextUnformatted($"{cleanCount} / {totalPieces}");
            ImGui.SetWindowFontScale(1.0f);
        }
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull()) ImGui.TextColored(Theme.TtChrome.FgFaint, $"melded pieces");
        ImGui.EndGroup();
        
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.Spacing();

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

        Theme.TtChrome.BeginPanel("card_" + piece.Slot, 96f);

        var draw = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;

        // --- gc-top ---
        ImGui.BeginGroup();
        
        // Measure the iLvl pill first so the name can reserve space for it
        // and never overlap it (the card is fixed-height; names vary).
        var ilvlText = $"i{piece.ItemLevel}";
        float ilvlPillW;
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
            ilvlPillW = ImGui.CalcTextSize(ilvlText).X + 16f;

        // Icon
        var pieceIcon = DalamudServices.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(piece.IconId)).GetWrapOrEmpty();
        float iconAdvance = 0f;
        if (pieceIcon != null)
        {
            ImGui.Image(pieceIcon.Handle, new Vector2(20, 20));
            ImGui.SameLine(0, 6);
            iconAdvance = 26f;
        }

        // Name (truncated to the space left of the pill), HQ star AFTER the name
        using (plugin.Fonts.GaramondBody.PushOrNull())
        {
            float nameMaxW = w - ilvlPillW - iconAdvance - (piece.IsHighQuality ? 16f : 0f) - 12f;
            ImGui.TextColored(Theme.TtChrome.Fg, Truncate(piece.Name, nameMaxW));
            if (piece.IsHighQuality)
            {
                ImGui.SameLine(0, 4);
                ImGui.TextColored(Theme.TtChrome.Gold, "★");
            }
        }

        // iLvl pill (right-aligned, mono)
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.SetCursorScreenPos(new Vector2(p.X + w - ilvlPillW, p.Y));
            Theme.TtChrome.PillBox(ilvlText, Theme.TtChrome.Gold);
        }

        // Aggregate Badge
        DrawAuditBadge(plugin, audits, p.X + w, p.Y + 28f, profile);

        ImGui.EndGroup();

        ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y + 52f));
        
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
                dotX -= 18f; // spacing
                DrawDot(draw, new Vector2(dotX, p.Y + 58f), GetMateriaColor(audit.Current.Value.Stat));
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
        draw.AddCircleFilled(center, 6f, ImGui.GetColorU32(color));
        draw.AddCircle(center, 6f, ImGui.GetColorU32(Theme.TtChrome.Bg2), 12, 1.5f);
    }

    // Ellipsis-truncate to a pixel width, measured in the currently pushed font.
    private static string Truncate(string text, float maxW)
    {
        if (maxW <= 0f || string.IsNullOrEmpty(text)) return text;
        if (ImGui.CalcTextSize(text).X <= maxW) return text;
        while (text.Length > 1 && ImGui.CalcTextSize(text + "…").X > maxW)
            text = text.Substring(0, text.Length - 1);
        return text + "…";
    }

    private static void DrawTooltipVerdict(Plugin plugin, List<MeldAudit> audits)
    {
        int overcap = 0;
        bool waste = false;
        foreach (var a in audits)
        {
            if (a.Severity == AuditSeverity.Critical) waste = true;
            else if (a.Severity == AuditSeverity.Warning && a.Headline.Contains("(-"))
            {
                var start = a.Headline.IndexOf("(-") + 2;
                var end = a.Headline.IndexOf(")", start);
                if (end > start && int.TryParse(a.Headline.Substring(start, end - start), out int parsed))
                    overcap += parsed;
            }
        }

        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            if (waste) Theme.TtChrome.PillBox("ZERO-VALUE MELD", Theme.TtChrome.Over);
            else if (overcap > 0) Theme.TtChrome.PillBox($"+{overcap} OVERCAP", Theme.TtChrome.Warn);
            else Theme.TtChrome.PillBox("CLEAN", Theme.TtChrome.Ok);
        }
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
            // Left: dot + full stat name. Right: tier + value, once — no dupes.
            ImGui.TextColored(GetMateriaColor(c.Stat), "●");
            ImGui.SameLine();
            using (plugin.Fonts.GaramondBody.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.Fg, c.Stat.Display());
            }
            ImGui.SameLine(200f);
            using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.Fg2, $"{c.Tier.Roman()} · +{c.Value}");
            }
        }

        ImGui.Separator();
        DrawTooltipVerdict(plugin, audits);
        ImGui.EndTooltip();
    }

    private static void DrawLegend(Plugin plugin)
    {
        var draw = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        using (plugin.Fonts.Pixel.PushOrNull())
        {
            // Plain leading label — no dot (it names the legend, it is not a stat).
            ImGui.SetCursorScreenPos(new Vector2(p.X, p.Y));
            ImGui.TextColored(Theme.TtChrome.FgMuted, "MATERIA");
            float xOffset = ImGui.CalcTextSize("MATERIA").X + 20f;

            var legends = new[] {
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

