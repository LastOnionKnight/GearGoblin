using System.Collections.Generic;
using GearGoblin.Core;

namespace GearGoblin.Planning;

/// <summary>
/// One slot's worth of target-set data, normalized across Etro and XIVGear.
/// </summary>
public sealed class BisSlot
{
    public EquipSlot Slot { get; init; }
    public uint ItemId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public uint ItemLevel { get; init; }
    public bool IsHighQuality { get; init; }

    /// <summary>Recommended materia melds for this slot, indexed 0-4.</summary>
    public List<BisMeld> Melds { get; init; } = new();

    /// <summary>
    /// True only when every non-empty target materia entry supplied by the
    /// source was resolved to a concrete stat/value. The Plan tab must not
    /// claim exact meld equality while this is false.
    /// </summary>
    public bool MeldDataComplete { get; init; } = true;
}

public sealed class BisMeld
{
    public int SlotIndex { get; init; }
    public uint MateriaItemId { get; init; }
    public string StatName { get; init; } = string.Empty;
    public int StatValue { get; init; }
    public int Tier { get; init; }
}

public sealed class BisGearset
{
    public string Name { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public uint JobId { get; init; }
    public List<BisSlot> Slots { get; init; } = new();
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Food item selected by the source planner, when provided. XIVGear's
    /// current SetExport supplies this directly as an FFXIV item ID.
    /// Reserved for the Raider consumables surface.
    /// </summary>
    public uint? FoodItemId { get; init; }
}
