using System;
using GearGoblin.Core;
using Lumina.Excel.Sheets;

namespace GearGoblin.Planning;

/// <summary>
/// Hydrates source-neutral BiS records from local Lumina data.
/// Remote planners only need to provide stable FFXIV item IDs; names,
/// item levels, HQ capability, and standard combat-materia effects are
/// resolved against the user's installed game data.
/// </summary>
internal static class BisMetadataResolver
{
    public static BisSlot HydrateItem(EquipSlot slot, uint itemId, System.Collections.Generic.List<BisMeld>? melds = null, bool meldDataComplete = true)
    {
        var itemSheet = DalamudServices.DataManager.GetExcelSheet<Item>();
        var item = itemSheet.GetRowOrDefault(itemId);

        return new BisSlot
        {
            Slot = slot,
            ItemId = itemId,
            ItemName = item?.Name.ExtractText() ?? string.Empty,
            ItemLevel = item?.LevelItem.RowId ?? 0,
            IsHighQuality = item?.CanBeHq ?? false,
            Melds = melds ?? new(),
            MeldDataComplete = meldDataComplete,
        };
    }

    /// <summary>
    /// Resolve a standard battle materia item ID into the shared BiS meld shape.
    /// Returns null for unknown/custom/non-battle materia; callers then mark the
    /// target meld data incomplete rather than manufacturing a comparison.
    /// </summary>
    public static BisMeld? ResolveMateriaItem(uint materiaItemId, int slotIndex)
    {
        if (materiaItemId == 0)
            return null;

        var itemSheet = DalamudServices.DataManager.GetExcelSheet<Item>();
        var item = itemSheet.GetRowOrDefault(materiaItemId);
        if (item is null)
            return null;

        var name = item.Value.Name.ExtractText();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var statName = ResolveStatName(name);
        if (statName is null)
            return null;

        var tier = ResolveTier(name);
        if (tier <= 0 || tier > MateriaTiers.CurrentCapTier)
            return null;

        return new BisMeld
        {
            SlotIndex = slotIndex,
            MateriaItemId = materiaItemId,
            StatName = statName,
            StatValue = MateriaTiers.SubstatValue(tier),
            Tier = tier,
        };
    }

    private static string? ResolveStatName(string itemName)
    {
        if (itemName.StartsWith("Savage Aim Materia ", StringComparison.OrdinalIgnoreCase))
            return "Critical Hit";
        if (itemName.StartsWith("Heavens' Eye Materia ", StringComparison.OrdinalIgnoreCase))
            return "Direct Hit";
        if (itemName.StartsWith("Savage Might Materia ", StringComparison.OrdinalIgnoreCase))
            return "Determination";
        if (itemName.StartsWith("Quickarm Materia ", StringComparison.OrdinalIgnoreCase))
            return "Skill Speed";
        if (itemName.StartsWith("Quicktongue Materia ", StringComparison.OrdinalIgnoreCase))
            return "Spell Speed";
        if (itemName.StartsWith("Battledance Materia ", StringComparison.OrdinalIgnoreCase))
            return "Tenacity";
        if (itemName.StartsWith("Piety Materia ", StringComparison.OrdinalIgnoreCase))
            return "Piety";

        return null;
    }

    private static int ResolveTier(string itemName)
    {
        int lastSpace = itemName.LastIndexOf(' ');
        if (lastSpace < 0 || lastSpace + 1 >= itemName.Length)
            return 0;

        return itemName[(lastSpace + 1)..].ToUpperInvariant() switch
        {
            "I" => 1,
            "II" => 2,
            "III" => 3,
            "IV" => 4,
            "V" => 5,
            "VI" => 6,
            "VII" => 7,
            "VIII" => 8,
            "IX" => 9,
            "X" => 10,
            "XI" => 11,
            "XII" => 12,
            _ => 0,
        };
    }
}
