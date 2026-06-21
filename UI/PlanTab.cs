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

    private static void DrawDiffArea(Plugin plugin, BisGearset bis)
    {
        using (plugin.Fonts.Pixel.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.CobaltBright, $"{Theme.TtChrome.GlyphEyebrow} SLOT DIFF");
        }
        ImGui.Separator();
        ImGui.Spacing();

        DrawDiff(bis, plugin.Inventory, plugin.Fonts);
    }

    private static void DrawDiff(BisGearset bis, IInventoryReader inventory, FontAtlasManager fonts)
    {
        var equipped = inventory.ReadEquipped();
        var equippedBySlot = new Dictionary<EquipSlot, EquippedPiece>();
        foreach (var e in equipped)
        {
            if (equippedBySlot.ContainsKey(e.Slot)) continue;
            equippedBySlot[e.Slot] = e;
        }

        var itemSheet = DalamudServices.DataManager.GetExcelSheet<Item>();

        foreach (var bisSlot in bis.Slots)
        {
            Theme.TtChrome.BeginPanel("diff_" + bisSlot.Slot, 64f);
            
            ImGui.BeginGroup();
            using (fonts.JetBrainsMonoBody.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.FgMuted, bisSlot.Slot.ToString());
            }
            ImGui.EndGroup();

            ImGui.SameLine(120);

            ImGui.BeginGroup();
            if (equippedBySlot.TryGetValue(bisSlot.Slot, out var current))
            {
                using (fonts.JetBrainsMonoBody.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.Fg, current.IsHighQuality ? $"{current.Name} ★" : current.Name);
                }
                using (fonts.Pixel.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.FgFaint, $"equipped · iLvl {current.ItemLevel}");
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

            var targetName = LookupItemName(itemSheet, bisSlot.ItemId);
            
            var avail = ImGui.GetContentRegionAvail();
            ImGui.SameLine(ImGui.GetWindowWidth() - 250f);
            
            ImGui.BeginGroup();
            if (equippedBySlot.TryGetValue(bisSlot.Slot, out var c) && c.ItemId == bisSlot.ItemId)
            {
                using (fonts.JetBrainsMonoBody.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.Ok, "✓ identical");
                }
            }
            else
            {
                using (fonts.JetBrainsMonoBody.PushOrNull())
                {
                    ImGui.TextColored(Theme.TtChrome.Warn, $"target wants {targetName}");
                }
            }
            ImGui.EndGroup();

            Theme.TtChrome.EndPanel();
            ImGui.Spacing();
        }
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

