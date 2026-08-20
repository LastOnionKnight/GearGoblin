using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using GearGoblin.Core;
using GearGoblin.Planning;
using GearGoblin.Services;
using GearGoblin.Theme;
using Lumina.Excel.Sheets;

namespace GearGoblin.UI;

/// <summary>Etro/XIVGear target-set fetch and slot-by-slot comparison surface.</summary>
public static class PlanTab
{
    private static string s_urlInput = string.Empty;
    private static string s_status = string.Empty;
    private static BisGearset? s_loadedSet;
    private static CancellationTokenSource? s_pendingFetch;
    private static bool s_isFetching;

    public static void Draw(Plugin plugin)
    {
        TtChrome.Push();
        try
        {
            TtChrome.Eyebrow(plugin.Fonts, "Plan · Diff Against Target");
            TtChrome.Quip(plugin.Fonts,
                "Paste an Etro or XIVGear URL to compare a target set against your equipped gear, slot by slot.");
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
            TtChrome.Pop();
        }
    }

    private static void DrawPasteArea(Plugin plugin)
    {
        ImGui.BeginGroup();

        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
            ImGui.TextColored(TtChrome.FgFaint, "TARGET SOURCE");

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
        else if (ImGui.Button("Fetch"))
        {
            StartFetch(s_urlInput);
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            s_pendingFetch?.Cancel();
            s_urlInput = string.Empty;
            s_loadedSet = null;
            s_status = string.Empty;
            s_isFetching = false;
        }

        if (!string.IsNullOrEmpty(s_status))
        {
            ImGui.Spacing();
            ImGui.TextColored(ResolveStatusColor(s_status), s_status);
        }

        ImGui.EndGroup();
    }

    private static Vector4 ResolveStatusColor(string status)
    {
        if (status.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            return TtChrome.Over;

        if (status.StartsWith("Loaded", StringComparison.OrdinalIgnoreCase))
            return TtChrome.Ok;

        return TtChrome.FgMuted;
    }

    private static void DrawEmptyState(Plugin plugin)
    {
        ImGui.Separator();
        ImGui.Spacing();

        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(TtChrome.FgFaint, "No BiS Loaded");
            ImGui.Spacing();
            ImGui.TextColored(TtChrome.FgMuted, "Drop a URL into the paste field above. Examples:");
            ImGui.Spacing();
            ImGui.BulletText("https://etro.gg/gearset/<uuid>");
            ImGui.BulletText("https://xivgear.app/?page=sl|<uuid>");
        }
    }

    private enum SlotVerdict
    {
        Match,
        ItemMatch,
        Remeld,
        Upgrade,
        Sidegrade,
        Downgrade,
        Missing,
    }

    private static void DrawDiffArea(Plugin plugin, BisGearset bis)
    {
        var fonts = plugin.Fonts;
        var equipped = plugin.Inventory.ReadEquipped();
        var bySlot = new Dictionary<EquipSlot, EquippedPiece>();

        foreach (var piece in equipped)
        {
            if (!bySlot.ContainsKey(piece.Slot))
                bySlot[piece.Slot] = piece;
        }

        int total = bis.Slots.Count;
        int exactMatch = 0;
        int itemMatch = 0;
        foreach (var target in bis.Slots)
        {
            var current = bySlot.GetValueOrDefault(target.Slot);
            switch (Verdict(current, target))
            {
                case SlotVerdict.Match:
                    exactMatch++;
                    break;
                case SlotVerdict.ItemMatch:
                    itemMatch++;
                    break;
            }
        }

        int differ = total - exactMatch - itemMatch;
        DrawTargetCard(plugin, bis, exactMatch, itemMatch, differ, total);
        ImGui.Spacing();
        ImGui.Spacing();

        using (fonts.Pixel.PushOrNull())
            ImGui.TextColored(TtChrome.CobaltBright, $"{TtChrome.GlyphEyebrow} SLOT DIFF");

        ImGui.Separator();
        ImGui.Spacing();

        var itemSheet = DalamudServices.DataManager.GetExcelSheet<Item>();
        foreach (var target in bis.Slots)
        {
            var current = bySlot.GetValueOrDefault(target.Slot);
            DrawSlotRow(fonts, target, current, itemSheet);
        }
    }

    private static void DrawTargetCard(
        Plugin plugin,
        BisGearset bis,
        int exactMatch,
        int itemMatch,
        int differ,
        int total)
    {
        TtChrome.BeginPanel("plan_target", 0f);

        using (plugin.Fonts.CinzelHeader.PushOrNull())
        {
            ImGui.TextColored(TtChrome.GoldBright,
                string.IsNullOrWhiteSpace(bis.Name) ? "Target gearset" : bis.Name);
        }

        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(TtChrome.FgMuted,
                $"{bis.Source.ToUpperInvariant()} · {JobLabel(bis.JobId)} · {total} slots");
        }

        ImGui.Spacing();
        TtChrome.PillBox("PARSED", TtChrome.Ok);
        ImGui.SameLine();
        TtChrome.PillBox($"{exactMatch} exact", TtChrome.Ok);

        if (itemMatch > 0)
        {
            ImGui.SameLine();
            TtChrome.PillBox($"{itemMatch} item match", TtChrome.CobaltBright);
        }

        if (differ > 0)
        {
            ImGui.SameLine();
            TtChrome.PillBox($"{differ} differ", TtChrome.Warn);
        }

        TtChrome.EndPanel();
    }

    private static void DrawSlotRow(
        FontAtlasManager fonts,
        BisSlot target,
        EquippedPiece? current,
        Lumina.Excel.ExcelSheet<Item> itemSheet)
    {
        TtChrome.BeginPanel("diff_" + target.Slot, 64f);

        ImGui.BeginGroup();
        using (fonts.JetBrainsMonoBody.PushOrNull())
            ImGui.TextColored(TtChrome.FgMuted, target.Slot.ToString());
        ImGui.EndGroup();

        ImGui.SameLine(120);
        ImGui.BeginGroup();

        if (current is not null)
        {
            using (fonts.JetBrainsMonoBody.PushOrNull())
                ImGui.TextColored(TtChrome.Fg, current.Name);

            if (current.IsHighQuality)
            {
                ImGui.SameLine(0, 4);
                using (fonts.IconFont.PushOrNull())
                    ImGui.TextColored(TtChrome.Gold, Dalamud.Interface.FontAwesomeIcon.Star.ToIconString());
            }

            using (fonts.Pixel.PushOrNull())
                ImGui.TextColored(TtChrome.FgFaint, $"equipped · iLvl {current.ItemLevel}");
        }
        else
        {
            using (fonts.JetBrainsMonoBody.PushOrNull())
                ImGui.TextColored(TtChrome.FgFaint, "(empty)");
        }

        ImGui.EndGroup();

        var verdict = Verdict(current, target);
        var (label, color, detail) = VerdictDisplay(verdict, current, target, itemSheet);

        ImGui.SameLine(ImGui.GetWindowWidth() - 250f);
        ImGui.BeginGroup();
        TtChrome.PillBox(label, color);
        if (!string.IsNullOrEmpty(detail))
        {
            using (fonts.Pixel.PushOrNull())
                ImGui.TextColored(TtChrome.FgMuted, detail);
        }
        ImGui.EndGroup();

        TtChrome.EndPanel();
        ImGui.Spacing();
    }

    private static SlotVerdict Verdict(EquippedPiece? current, BisSlot target)
    {
        if (current is null)
            return SlotVerdict.Missing;

        if (current.ItemId == target.ItemId)
        {
            if (!target.MeldDataComplete)
                return SlotVerdict.ItemMatch;

            return MeldsMatch(current, target)
                ? SlotVerdict.Match
                : SlotVerdict.Remeld;
        }

        // Unknown item level must never be interpreted as a downgrade.
        if (target.ItemLevel == 0)
            return SlotVerdict.Sidegrade;

        if (target.ItemLevel > current.ItemLevel)
            return SlotVerdict.Upgrade;
        if (target.ItemLevel < current.ItemLevel)
            return SlotVerdict.Downgrade;

        return SlotVerdict.Sidegrade;
    }

    private static bool MeldsMatch(EquippedPiece current, BisSlot target)
    {
        if (!target.MeldDataComplete || current.Materia.Count != target.Melds.Count)
            return false;

        var currentKeys = current.Materia
            .Select(m => $"{CanonicalStat(m.StatName)}:{m.StatValue}")
            .OrderBy(x => x)
            .ToList();
        var targetKeys = target.Melds
            .Select(m => $"{CanonicalStat(m.StatName)}:{m.StatValue}")
            .OrderBy(x => x)
            .ToList();

        return currentKeys.SequenceEqual(targetKeys);
    }

    private static string CanonicalStat(string value) => value switch
    {
        "Direct Hit Rate" => "Direct Hit",
        _ => value,
    };

    private static (string Label, Vector4 Color, string Detail) VerdictDisplay(
        SlotVerdict verdict,
        EquippedPiece? current,
        BisSlot target,
        Lumina.Excel.ExcelSheet<Item> itemSheet)
    {
        var targetName = string.IsNullOrWhiteSpace(target.ItemName)
            ? LookupItemName(itemSheet, target.ItemId)
            : target.ItemName;

        return verdict switch
        {
            SlotVerdict.Match =>
                ("MATCH", TtChrome.Ok, "item + melds identical"),
            SlotVerdict.ItemMatch =>
                ("ITEM MATCH", TtChrome.CobaltBright, "target meld data unresolved"),
            SlotVerdict.Remeld =>
                ("REMELD", TtChrome.Warn, "right item, different melds"),
            SlotVerdict.Upgrade =>
                ($"UPGRADE +{(int)target.ItemLevel - (int)(current?.ItemLevel ?? 0)}", TtChrome.CobaltBright, targetName),
            SlotVerdict.Sidegrade =>
                ("SWAP", TtChrome.Warn, targetName),
            SlotVerdict.Downgrade =>
                ("TARGET LOWER", TtChrome.FgMuted, targetName),
            _ =>
                ("ACQUIRE", TtChrome.Over, targetName),
        };
    }

    private static string JobLabel(uint jobId)
    {
        if (jobId == 0)
            return "Unknown job";

        var sheet = DalamudServices.DataManager.GetExcelSheet<ClassJob>();
        var row = sheet.GetRowOrDefault(jobId);
        return row is null
            ? $"Job {jobId}"
            : row.Value.Abbreviation.ExtractText().ToUpperInvariant();
    }

    private static void StartFetch(string url)
    {
        s_pendingFetch?.Cancel();
        s_pendingFetch = new CancellationTokenSource();
        var token = s_pendingFetch.Token;

        s_status = "Fetching...";
        s_loadedSet = null;
        s_isFetching = true;

        _ = Task.Run(async () =>
        {
            var result = await BisFetcher.FetchAsync(url, token);
            if (token.IsCancellationRequested)
                return;

            await DalamudServices.Framework.RunOnFrameworkThread(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                s_isFetching = false;
                if (result.Error is not null)
                {
                    s_status = $"Error: {result.Error}";
                    s_loadedSet = null;
                    return;
                }

                if (result.Gearset is not null)
                {
                    s_loadedSet = result.Gearset;
                    s_status = $"Loaded: {result.Gearset.Name} ({result.Gearset.Source})";
                }
            });
        }, token);
    }

    private static string LookupItemName(Lumina.Excel.ExcelSheet<Item> sheet, uint itemId)
    {
        var row = sheet.GetRowOrDefault(itemId);
        return row is null
            ? $"Unknown item ({itemId})"
            : row.Value.Name.ExtractText();
    }
}
