// UI/PlanTab.cs
// Plan tab: paste an Etro/XIVGear URL, fetch BiS, see a slot-by-slot diff
// against currently equipped gear.
//
// This is intentionally minimal in v0.3: shows current vs target item ID and
// item name. v0.4 can add ilvl deltas, materia diffs, and "what to farm next."

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using GearGoblin.Planning;
using GearGoblin.Services;
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

    public static void Draw(InventoryReader inventory)
    {
        ImGui.TextUnformatted("Paste an Etro or XIVGear URL to compare against your current gear:");
        ImGui.Spacing();

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
            s_urlInput = "";
            s_loadedSet = null;
            s_status = "";
        }

        if (!string.IsNullOrEmpty(s_status))
        {
            ImGui.Spacing();
            var color = s_status.StartsWith("Error") || s_status.StartsWith("Failed")
                ? new Vector4(1.0f, 0.5f, 0.5f, 1f)
                : new Vector4(0.7f, 0.85f, 1.0f, 1f);
            ImGui.TextColored(color, s_status);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (s_loadedSet is null)
        {
            ImGui.TextDisabled("No BiS loaded yet. Examples:");
            ImGui.BulletText("https://etro.gg/gearset/<uuid>");
            ImGui.BulletText("https://xivgear.app/?page=sl|<uuid>");
            return;
        }

        DrawDiff(s_loadedSet, inventory);
    }

    private static void StartFetch(string url)
    {
        s_pendingFetch?.Cancel();
        s_pendingFetch = new CancellationTokenSource();
        s_status   = "Fetching...";
        s_loadedSet = null;
        s_isFetching = true;

        // Fire-and-forget; don't block UI
        _ = Task.Run(async () =>
        {
            var result = await BisFetcher.FetchAsync(url, s_pendingFetch.Token);
            // Marshal back to framework thread for safety when we mutate static state.
            // v0.6.5.2: explicit _ = discard silences CS4014 — the fire-and-forget
            // pattern is intentional. We're already inside a Task.Run, so awaiting
            // the framework dispatch would just add latency without changing
            // behavior: the static state mutations happen on the framework tick
            // regardless of whether this outer Task waits for them.
            _ = DalamudServices.Framework.RunOnFrameworkThread(() =>
            {
                s_isFetching = false;
                if (result.Error is not null)
                {
                    s_status = $"Error: {result.Error}";
                    s_loadedSet = null;
                }
                else if (result.Gearset is not null)
                {
                    s_loadedSet = result.Gearset;
                    s_status = $"Loaded: {result.Gearset.Name} ({result.Gearset.Source})";
                }
            });
        });
    }

    private static void DrawDiff(BisGearset bis, InventoryReader inventory)
    {
        ImGui.TextUnformatted($"BiS: {bis.Name}");
        if (!string.IsNullOrEmpty(bis.Description))
            ImGui.TextDisabled(bis.Description);
        ImGui.Spacing();

        var equipped = inventory.ReadEquipped();
        // Defensive: skip duplicate-slot entries rather than crash.
        var equippedBySlot = new Dictionary<EquipSlot, EquippedPiece>();
        foreach (var e in equipped)
        {
            if (equippedBySlot.ContainsKey(e.Slot)) continue;
            equippedBySlot[e.Slot] = e;
        }

        var itemSheet = DalamudServices.DataManager.GetExcelSheet<Item>();

        if (ImGui.BeginTable("##plandiff", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Slot",     ImGuiTableColumnFlags.WidthFixed,  90);
            ImGui.TableSetupColumn("Current",  ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Target",   ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Status",   ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();

            foreach (var bisSlot in bis.Slots)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(bisSlot.Slot.ToString());

                ImGui.TableNextColumn();
                if (equippedBySlot.TryGetValue(bisSlot.Slot, out var current))
                {
                    ImGui.TextUnformatted(current.IsHighQuality ? $"{current.Name} ★" : current.Name);
                    ImGui.TextDisabled($"iLvl {current.ItemLevel}");
                }
                else
                {
                    ImGui.TextDisabled("(empty)");
                }

                ImGui.TableNextColumn();
                var targetName = LookupItemName(itemSheet, bisSlot.ItemId);
                ImGui.TextUnformatted(targetName);
                ImGui.TextDisabled($"id {bisSlot.ItemId}");

                ImGui.TableNextColumn();
                if (equippedBySlot.TryGetValue(bisSlot.Slot, out var c) && c.ItemId == bisSlot.ItemId)
                    ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.5f, 1f), "✓ match");
                else
                    ImGui.TextColored(new Vector4(1.0f, 0.75f, 0.3f, 1f), "✗ farm");
            }

            ImGui.EndTable();
        }
    }

    private static string LookupItemName(Lumina.Excel.ExcelSheet<Item> sheet, uint itemId)
    {
        var row = sheet.GetRowOrDefault(itemId);
        if (row is null) return $"Unknown item ({itemId})";
        return row.Value.Name.ExtractText();
    }
}
