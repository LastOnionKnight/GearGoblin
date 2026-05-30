using GearGoblin.Core;
using GearGoblin.Core.Materia;
// Planning/BisGearset.cs
// Common gearset model. Etro and XIVGear use different JSON shapes; we parse
// them both into this neutral structure so the diff/UI code doesn't care
// which source the user pasted.

using System.Collections.Generic;
using GearGoblin.Services;

namespace GearGoblin.Planning;

/// <summary>
/// One slot's worth of BiS data: the item, optional materia melds, and
/// metadata for display.
/// </summary>
public sealed class BisSlot
{
    public EquipSlot Slot { get; init; }
    public uint   ItemId   { get; init; }
    public string ItemName { get; init; } = "";
    public uint   ItemLevel{ get; init; }
    public bool   IsHighQuality { get; init; }

    /// <summary>Recommended materia melds for this slot, indexed 0-4.</summary>
    public List<BisMeld> Melds { get; init; } = new();
}

public sealed class BisMeld
{
    public int    SlotIndex { get; init; }
    public string StatName  { get; init; } = "";
    public int    StatValue { get; init; }
    public int    Tier      { get; init; }
}

public sealed class BisGearset
{
    /// <summary>Display name from the source (e.g., "VPR 7.5 BiS - Crit/DH").</summary>
    public string Name { get; init; } = "";

    /// <summary>Source URL the user pasted (Etro or XIVGear).</summary>
    public string SourceUrl { get; init; } = "";

    /// <summary>Source platform: "etro" or "xivgear".</summary>
    public string Source { get; init; } = "";

    /// <summary>Job ID this set is for (FFXIV ClassJob row ID).</summary>
    public uint JobId { get; init; }

    /// <summary>Per-slot BiS items.</summary>
    public List<BisSlot> Slots { get; init; } = new();

    /// <summary>Free-form description from source.</summary>
    public string Description { get; init; } = "";
}

