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

    /// <summary>
    /// Draw the Plan tab. v0.6.7 takes the full Plugin instance so it can
    /// reach the FontAtlasManager for Track 2 typography. Replaces the
    /// v0.6.x Draw(InventoryReader) signature — MainWindow.cs caller must
    /// be updated to pass `plugin` instead of `plugin.Inventory`.
    /// </summary>
    public static void Draw(Plugin plugin)
    {
        TtChrome.Push();
        try
        {
            DrawPasteCard(plugin);

            ImGui.Spacing();
            ImGui.Spacing();

            if (s_loadedSet is null)
                DrawEmptyStateCard(plugin);
            else
                DrawDiffCard(plugin, s_loadedSet);
        }
        finally
        {
            TtChrome.Pop();
        }
    }

    // ── Paste card ──────────────────────────────────────────────────────

    private static void DrawPasteCard(Plugin plugin)
    {
        TtChrome.BeginCard();

        TtChrome.Eyebrow(plugin.Fonts, "Plan · BiS Paste");
        ImGui.Spacing();
        TtChrome.Quip(plugin.Fonts,
            "Paste an Etro or XIVGear URL. Grub-Grub will compare it to your kit.");
        ImGui.Spacing();
        ImGui.Spacing();

        // URL input + Fetch + Clear in one row.
        // Reserves 160px on the right for the two buttons + spacing.
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

        // Status message (if any) — colored by severity.
        if (!string.IsNullOrEmpty(s_status))
        {
            ImGui.Spacing();
            var color = ResolveStatusColor(s_status);
            ImGui.TextColored(color, s_status);
        }

        TtChrome.EndCard();
    }

    private static Vector4 ResolveStatusColor(string status)
    {
        if (status.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            return TtChrome.SeverityCritical;

        if (status.StartsWith("Loaded", StringComparison.OrdinalIgnoreCase))
            return TtChrome.HpGreen;

        // "Fetching..." and anything else
        return TtChrome.SeverityNote;
    }

    // ── Empty state card ────────────────────────────────────────────────

    private static void DrawEmptyStateCard(Plugin plugin)
    {
        TtChrome.BeginCard();

        TtChrome.Eyebrow(plugin.Fonts, "No BiS Loaded");
        ImGui.Spacing();
        TtChrome.Quip(plugin.Fonts,
            "Drop a URL into the paste field above. Examples:");
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Text, TtChrome.FrostMuted);
        ImGui.BulletText("https://etro.gg/gearset/<uuid>");
        ImGui.BulletText("https://xivgear.app/?page=sl|<uuid>");
        ImGui.PopStyleColor();

        TtChrome.EndCard();
    }

    // ── Diff card ───────────────────────────────────────────────────────

    private static void DrawDiffCard(Plugin plugin, BisGearset bis)
    {
        TtChrome.BeginCard();

        TtChrome.Eyebrow(plugin.Fonts, $"BiS · {bis.Name}");
        if (!string.IsNullOrEmpty(bis.Description))
        {
            ImGui.Spacing();
            TtChrome.Quip(plugin.Fonts, bis.Description);
        }
        ImGui.Spacing();
        ImGui.Spacing();

        DrawDiff(bis, plugin.Inventory, plugin.Fonts);

        TtChrome.EndCard();
    }

    private static void DrawDiff(BisGearset bis, InventoryReader inventory, FontAtlasManager fonts)
    {
        var equipped = inventory.ReadEquipped();
        // Defensive: skip duplicate-slot entries rather than crash.
        var equippedBySlot = new Dictionary<EquipSlot, EquippedPiece>();
        foreach (var e in equipped)
        {
            if (equippedBySlot.ContainsKey(e.Slot)) continue;
            equippedBySlot[e.Slot] = e;
        }

        var itemSheet = DalamudServices.DataManager.GetExcelSheet<Item>();

        // Slightly de-emphasized header text so the data reads first.
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, TtChrome.InkDeeper);

        if (ImGui.BeginTable("##plandiff", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Slot",    ImGuiTableColumnFlags.WidthFixed,  90);
            ImGui.TableSetupColumn("Current", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Target",  ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Status",  ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();

            foreach (var bisSlot in bis.Slots)
            {
                ImGui.TableNextRow();

                // Slot label
                ImGui.TableNextColumn();
                ImGui.PushStyleColor(ImGuiCol.Text, TtChrome.FrostMuted);
                ImGui.TextUnformatted(bisSlot.Slot.ToString());
                ImGui.PopStyleColor();

                // Current
                ImGui.TableNextColumn();
                if (equippedBySlot.TryGetValue(bisSlot.Slot, out var current))
                {
                    ImGui.TextUnformatted(current.IsHighQuality ? $"{current.Name} ★" : current.Name);
                    TtChrome.Number(fonts, $"iLvl {current.ItemLevel}", TtChrome.FrostFaint);
                }
                else
                {
                    ImGui.TextDisabled("(empty)");
                }

                // Target
                ImGui.TableNextColumn();
                var targetName = LookupItemName(itemSheet, bisSlot.ItemId);
                ImGui.TextUnformatted(targetName);
                TtChrome.Number(fonts, $"id {bisSlot.ItemId}", TtChrome.FrostFaint);

                // Status pill
                ImGui.TableNextColumn();
                if (equippedBySlot.TryGetValue(bisSlot.Slot, out var c) && c.ItemId == bisSlot.ItemId)
                    TtChrome.Pill("✓ match", TtChrome.HpGreen);
                else
                    TtChrome.Pill("✗ farm",  TtChrome.Farm);
            }

            ImGui.EndTable();
        }

        ImGui.PopStyleColor();  // TableHeaderBg
    }

    // ── Fetch (unchanged from v0.6.x) ───────────────────────────────────

    private static void StartFetch(string url)
    {
        s_pendingFetch?.Cancel();
        s_pendingFetch = new CancellationTokenSource();
        s_status      = "Fetching...";
        s_loadedSet   = null;
        s_isFetching  = true;

        // Fire-and-forget; don't block UI.
        _ = Task.Run(async () =>
        {
            var result = await BisFetcher.FetchAsync(url, s_pendingFetch.Token);
            // Marshal back to framework thread for safety when we mutate static
            // state. v0.6.5.2: explicit _ = discard silences CS4014 — the
            // fire-and-forget pattern is intentional. We're already inside a
            // Task.Run, so awaiting the framework dispatch would just add
            // latency without changing behavior: the static state mutations
            // happen on the framework tick regardless of whether this outer
            // Task waits for them.
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
