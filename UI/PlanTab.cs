using Dalamud.Interface;
using GearGoblin.Core;
// UI/PlanTab.cs
//
// Plan tab: paste an Etro/XIVGear URL, fetch BiS, see a slot-by-slot diff
// against currently equipped gear.
//
// v0.6.7 — First Track 2 surface in the ember/frost-blue visual language.
// The data flow (StartFetch, BisFetcher, slot-by-slot diff) is preserved
// verbatim from v0.6.x. Only the chrome changes:
//
//   - Wrapped in TtChrome.BeginCard/EndCard for the signature doubled
//     inner frame (outer 2px frost-outline, inner 1px hairline at 6px
//     inset).
//   - Eyebrow label "» PLAN · BIS PASTE" in ember accent (replaces the
//     plain "Paste an Etro or XIVGear URL..." instruction).
//   - Italic quip subtitle in muted frost — falls back to default font
//     until v0.6.7.1 wires the Cormorant Garamond italic handle.
//   - Status message uses TtChrome severity palette (frost-blue note,
//     yellow warning, ember critical) instead of the v0.6.x hardcoded
//     Vector4 literals.
//   - Diff table gets its own card with eyebrow header. Match/farm
//     status uses HpGreen / Farm orange from the Track 2 palette.
//
// Signature change v0.6.7:
//   - v0.6.x:  PlanTab.Draw(plugin.Inventory)
//   - v0.6.7:  PlanTab.Draw(plugin)
//
//   The new signature passes the full Plugin instance so we can reach
//   `plugin.Fonts` (FontAtlasManager) for the Track 2 font handles. The
//   handles are nullable — if the .ttf file isn't in Assets/Fonts/ yet,
//   the helper falls back to default font gracefully. This means Brian
//   can drop in Cormorant + JetBrains Mono + Eorzea at his own pace
//   without breaking the build or the v0.6.7 chrome.
//
//   MainWindow.cs must be updated alongside this dropin — see CHANGELOG.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using GearGoblin.Planning;
using GearGoblin.Services;
using GearGoblin.Theme;
using Lumina.Excel.Sheets;

namespace GearGoblin.UI;

public static class PlanTab
{
    // Per-session state. Cleared on plugin reload.
    private static string s_urlInput = "";
    private static string s_status   = "";
    private static BisGearset? s_loadedSet;
    private static CancellationTokenSource? s_pendingFetch;
    private static bool s_isFetching;

    public static void Draw(Plugin plugin)
    {
        Theme.TtChrome.Push();
        try
        {
            Theme.TtChrome.Eyebrow(plugin.Fonts, "Plan · Diff Against Target");
            Theme.TtChrome.Quip(plugin.Fonts, "Paste an Etro or XIVGear URL to diff a target set against your equipped gear, slot by slot.");
            ImGui.Spacing();
            ImGui.Spacing();

            DrawPasteArea(plugin);

            ImGui.Spacing();
            ImGui.Spacing();

            if (s_loadedSet is null)
                DrawEmptyState(plugin);
            else
                DrawDiffArea(plugin, s_loadedSet);
        }
        finally
        {
            Theme.TtChrome.Pop();
        }
    }

    // ── Paste area ──────────────────────────────────────────────────────

    private static void DrawPasteArea(Plugin plugin)
    {
        ImGui.BeginGroup();
        
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.FgFaint, "TARGET SOURCE");
        }
        
        ImGui.PushItemWidth(-160);
        ImGui.InputText("##url", ref s_urlInput, 512);
        ImGui.PopItemWidth();

        ImGui.SameLine();
        if (s_isFetching)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Fetching...");
            ImGui.EndDisabled();
        }
        else
        {
            if (ImGui.Button("Fetch"))
                StartFetch(s_urlInput);
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            s_urlInput  = "";
            s_loadedSet = null;
            s_status    = "";
        }

        // Status message (if any)
        if (!string.IsNullOrEmpty(s_status))
        {
            ImGui.Spacing();
            var color = ResolveStatusColor(s_status);
            ImGui.TextColored(color, s_status);
        }

        ImGui.EndGroup();
    }

    private static Vector4 ResolveStatusColor(string status)
    {
        if (status.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            return Theme.TtChrome.Over;

        if (status.StartsWith("Loaded", StringComparison.OrdinalIgnoreCase))
            return Theme.TtChrome.Ok;

        return Theme.TtChrome.FgMuted;
    }

    // ── Empty state ────────────────────────────────────────────────

    private static void DrawEmptyState(Plugin plugin)
    {
        ImGui.Separator();
        ImGui.Spacing();

        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.FgFaint, "No BiS Loaded");
            ImGui.Spacing();
            ImGui.TextColored(Theme.TtChrome.FgMuted, "Drop a URL into the paste field above. Examples:");
            ImGui.Spacing();
            ImGui.BulletText("https://etro.gg/gearset/<uuid>");
            ImGui.BulletText("https://xivgear.app/?page=sl|<uuid>");
        }
    }

    // ── Diff Area ───────────────────────────────────────────────────────

    // Slot outcome after comparing the equipped piece against the BiS target.
    private enum SlotVerdict { Match, Remeld, Upgrade, Sidegrade, Downgrade, Missing }

    private static void DrawDiffArea(Plugin plugin, BisGearset bis)
    {
        var fonts = plugin.Fonts;

        var equipped = plugin.Inventory.ReadEquipped();
        var bySlot = new Dictionary<EquipSlot, EquippedPiece>();
        foreach (var e in equipped)
        {
            if (bySlot.ContainsKey(e.Slot)) continue;
            bySlot[e.Slot] = e;
        }

        // Tally: a slot only "matches" when item AND melds line up.
        int total = bis.Slots.Count;
        int match = 0;
        foreach (var b in bis.Slots)
        {
            var cur = bySlot.GetValueOrDefault(b.Slot);
            if (Verdict(cur, b) == SlotVerdict.Match) match++;
        }
        int differ = total - match;

        DrawTargetCard(plugin, bis, match, differ, total);
        ImGui.Spacing();
        ImGui.Spacing();

        using (fonts.Pixel.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.CobaltBright, $"{Theme.TtChrome.GlyphEyebrow} SLOT DIFF");
        }
        ImGui.Separator();
        ImGui.Spacing();

        var itemSheet = DalamudServices.DataManager.GetExcelSheet<Item>();
        foreach (var b in bis.Slots)
        {
            var cur = bySlot.GetValueOrDefault(b.Slot);
            DrawSlotRow(fonts, b, cur, itemSheet);
        }
    }

    // Target-source header: set name, provenance, and the match/differ tally.
    private static void DrawTargetCard(Plugin plugin, BisGearset bis, int match, int differ, int total)
    {
        Theme.TtChrome.BeginPanel("plan_target", 0f);

        using (plugin.Fonts.CinzelHeader.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.GoldBright,
                string.IsNullOrWhiteSpace(bis.Name) ? "Target gearset" : bis.Name);
        }
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.FgMuted,
                $"{bis.Source.ToUpperInvariant()} · {JobLabel(bis.JobId)} · {total} slots");
        }

        ImGui.Spacing();
        Theme.TtChrome.PillBox("PARSED", Theme.TtChrome.Ok);
        ImGui.SameLine();
        Theme.TtChrome.PillBox($"{match} match", Theme.TtChrome.Ok);
        ImGui.SameLine();
        Theme.TtChrome.PillBox($"{differ} differ", differ > 0 ? Theme.TtChrome.Warn : Theme.TtChrome.FgMuted);

        Theme.TtChrome.EndPanel();
    }

    private static void DrawSlotRow(FontAtlasManager fonts, BisSlot bis, EquippedPiece? cur, Lumina.Excel.ExcelSheet<Item> itemSheet)
    {
        Theme.TtChrome.BeginPanel("diff_" + bis.Slot, 64f);

        ImGui.BeginGroup();
        using (fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.FgMuted, bis.Slot.ToString());
        }
        ImGui.EndGroup();

        ImGui.SameLine(120);

        ImGui.BeginGroup();
        if (cur is not null)
        {
            using (fonts.JetBrainsMonoBody.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.Fg, cur.Name);
            }
            if (cur.IsHighQuality)
            {
                // HQ star via FontAwesome — no shipped text font carries U+2605,
                // so a literal "★" renders as a substitution glyph (the v1.5.7c
                // "diamond" bug).
                ImGui.SameLine(0, 4);
                using (fonts.IconFont.PushOrNull())
                    ImGui.TextColored(Theme.TtChrome.Gold, Dalamud.Interface.FontAwesomeIcon.Star.ToIconString());
            }
            using (fonts.Pixel.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.FgFaint, $"equipped · iLvl {cur.ItemLevel}");
            }
        }
        else
        {
            using (fonts.JetBrainsMonoBody.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.FgFaint, "(empty)");
            }
        }
        ImGui.EndGroup();

        var verdict = Verdict(cur, bis);
        var (label, color, sub) = VerdictDisplay(verdict, cur, bis, itemSheet);

        ImGui.SameLine(ImGui.GetWindowWidth() - 250f);
        ImGui.BeginGroup();
        Theme.TtChrome.PillBox(label, color);
        if (!string.IsNullOrEmpty(sub))
        {
            using (fonts.Pixel.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.FgMuted, sub);
            }
        }
        ImGui.EndGroup();

        Theme.TtChrome.EndPanel();
        ImGui.Spacing();
    }

    // ── Verdict logic ───────────────────────────────────────────────────

    private static SlotVerdict Verdict(EquippedPiece? cur, BisSlot bis)
    {
        if (cur is null) return SlotVerdict.Missing;
        if (cur.ItemId == bis.ItemId)
            return MeldsMatch(cur, bis) ? SlotVerdict.Match : SlotVerdict.Remeld;
        if (bis.ItemLevel > cur.ItemLevel) return SlotVerdict.Upgrade;
        if (bis.ItemLevel < cur.ItemLevel) return SlotVerdict.Downgrade;
        return SlotVerdict.Sidegrade;
    }

    // Melds match when the equipped melds and target melds share the same
    // multiset of (stat, value). Order and slot index are ignored.
    private static bool MeldsMatch(EquippedPiece cur, BisSlot bis)
    {
        if (cur.Materia.Count != bis.Melds.Count) return false;
        var curKeys = cur.Materia.Select(m => $"{m.StatName}:{m.StatValue}").OrderBy(x => x).ToList();
        var bisKeys = bis.Melds.Select(m => $"{m.StatName}:{m.StatValue}").OrderBy(x => x).ToList();
        for (int i = 0; i < curKeys.Count; i++)
            if (curKeys[i] != bisKeys[i]) return false;
        return true;
    }

    private static (string label, Vector4 color, string sub) VerdictDisplay(
        SlotVerdict v, EquippedPiece? cur, BisSlot bis, Lumina.Excel.ExcelSheet<Item> sheet)
    {
        var target = string.IsNullOrWhiteSpace(bis.ItemName) ? LookupItemName(sheet, bis.ItemId) : bis.ItemName;
        switch (v)
        {
            case SlotVerdict.Match:
                return ("MATCH", Theme.TtChrome.Ok, "item + melds identical");
            case SlotVerdict.Remeld:
                return ("REMELD", Theme.TtChrome.Warn, "right item, different melds");
            case SlotVerdict.Upgrade:
            {
                int d = (int)bis.ItemLevel - (int)(cur?.ItemLevel ?? 0);
                return ($"UPGRADE +{d}", Theme.TtChrome.CobaltBright, target);
            }
            case SlotVerdict.Sidegrade:
                return ("SWAP", Theme.TtChrome.Warn, target);
            case SlotVerdict.Downgrade:
                return ("TARGET LOWER", Theme.TtChrome.FgMuted, target);
            default:
                return ("ACQUIRE", Theme.TtChrome.Over, target);
        }
    }

    private static string JobLabel(uint jobId)
    {
        if (jobId == 0) return "All jobs";
        var sheet = DalamudServices.DataManager.GetExcelSheet<ClassJob>();
        var row = sheet.GetRowOrDefault(jobId);
        return row is null ? $"Job {jobId}" : row.Value.Abbreviation.ExtractText().ToUpperInvariant();
    }

    // ── Fetch (unchanged from v0.6.x) ───────────────────────────────────

    private static void StartFetch(string url)
    {
        s_pendingFetch?.Cancel();
        s_pendingFetch = new CancellationTokenSource();
        s_status      = "Fetching...";
        s_loadedSet   = null;
        s_isFetching  = true;

        _ = Task.Run(async () =>
        {
            var result = await BisFetcher.FetchAsync(url, s_pendingFetch.Token);
            _ = DalamudServices.Framework.RunOnFrameworkThread(() =>
            {
                s_isFetching = false;
                if (result.Error is not null)
                {
                    s_status    = $"Error: {result.Error}";
                    s_loadedSet = null;
                }
                else if (result.Gearset is not null)
                {
                    s_loadedSet = result.Gearset;
                    s_status    = $"Loaded: {result.Gearset.Name} ({result.Gearset.Source})";
                }
            });
        });
    }

    private static string LookupItemName(Lumina.Excel.ExcelSheet<Item> sheet, uint itemId)
    {
        var row = sheet.GetRowOrDefault(itemId);
        if (row is null) return $"Unknown item ({itemId})";
        return row.Value.Name.ExtractText();
    }
}

