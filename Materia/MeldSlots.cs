// Materia/MeldSlots.cs
// Per-piece meld slot model.
//
// v0.3.1 (Bug B): real slot counts now come from the Item sheet via
// EquippedPiece.MateriaSlotCount (guaranteed slots, 0-2) and
// EquippedPiece.IsOvermeldAllowed (can the player add slots 2-4 via overmeld?).
//
// Total slots = MateriaSlotCount + (IsOvermeldAllowed ? extras : 0).
//   - Crafted gear:        2 guaranteed + 3 overmeld = 5
//   - Augmented tomestone: 2 guaranteed + 3 overmeld = 5
//   - Raid drops:          2 guaranteed + 0 overmeld = 2
//   - Job stone, etc.:     0 guaranteed + 0 overmeld = 0
// Overmeld success rates: ~36% / 25% / 20% for slots 2/3/4.

using System.Collections.Generic;
using System.Linq;
using GearGoblin.Services;

namespace GearGoblin.Materia;

public sealed class MeldSlot
{
    public int  SlotIndex   { get; init; }
    public bool IsGuaranteed{ get; init; }
    public MateriaSpec? Current { get; set; }
    public double SuccessRate { get; init; } = 1.0;
    public bool IsEmpty => Current is null;
}

public sealed class MeldablePiece
{
    public EquipSlot Slot      { get; init; }
    public string    Name      { get; init; } = "";
    public uint      ItemId    { get; init; }
    public uint      ItemLevel { get; init; }
    public bool      IsHighQuality { get; init; }
    public List<MeldSlot> Slots { get; init; } = new();
    public Dictionary<Substat, int> CurrentMeldStats { get; init; } = new();
    public int EmptySlotCount => Slots.Count(s => s.IsEmpty);
}

public static class MeldSlotsBuilder
{
    /// <summary>Total slots a piece can theoretically support (guaranteed + overmeld).</summary>
    private const int MaxOvermeldSlots = 5;

    public static MeldablePiece FromEquipped(EquippedPiece piece)
    {
        // v0.3.1: derive real slot count from the item's metadata.
        // Guaranteed count comes straight from the Item sheet; overmeld adds up to slot 4
        // only if the item explicitly allows it.
        int guaranteed = piece.MateriaSlotCount;
        int total      = piece.IsOvermeldAllowed ? MaxOvermeldSlots : guaranteed;

        var melds = new List<MeldSlot>();
        var stats = new Dictionary<Substat, int>();

        // Filled slots come straight from the inventory data.
        foreach (var m in piece.Materia)
        {
            var spec = MateriaCatalog.FromGrade(m.StatName, m.Grade, m.StatValue);
            melds.Add(new MeldSlot
            {
                SlotIndex    = m.SlotIndex,
                IsGuaranteed = m.SlotIndex < guaranteed,
                Current      = spec,
                SuccessRate  = SuccessRateForSlot(m.SlotIndex),
            });

            if (spec.Stat != Substat.None)
            {
                stats.TryGetValue(spec.Stat, out var existing);
                stats[spec.Stat] = existing + spec.Value;
            }
        }

        var existingIndices = new HashSet<int>();
        foreach (var m in melds) existingIndices.Add(m.SlotIndex);

        // Empty slots: only as many as the piece actually supports.
        for (int i = 0; i < total; i++)
        {
            if (existingIndices.Contains(i)) continue;
            melds.Add(new MeldSlot
            {
                SlotIndex    = i,
                IsGuaranteed = i < guaranteed,
                Current      = null,
                SuccessRate  = SuccessRateForSlot(i),
            });
        }

        melds.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

        return new MeldablePiece
        {
            Slot          = piece.Slot,
            Name          = piece.Name,
            ItemId        = piece.ItemId,
            ItemLevel     = piece.ItemLevel,
            IsHighQuality = piece.IsHighQuality,
            Slots         = melds,
            CurrentMeldStats = stats,
        };
    }

    private static double SuccessRateForSlot(int slotIndex) => slotIndex switch
    {
        0 => 1.00,
        1 => 1.00,
        2 => 0.36,
        3 => 0.25,
        4 => 0.20,
        _ => 0.10,
    };
}
