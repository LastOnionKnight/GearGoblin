// Materia/MeldSlots.cs
// Per-piece meld slot model. Each equipped item has 0-2 guaranteed slots
// (depending on whether it's high-quality / overmeldable) plus 0-3 overmeld
// slots that have decreasing success rates: ~36% / ~25% / ~20% / ~10% / ~5%.

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
    public static MeldablePiece FromEquipped(EquippedPiece piece, int totalAllowedSlots = 5)
    {
        var melds = new List<MeldSlot>();
        var stats = new Dictionary<Substat, int>();

        foreach (var m in piece.Materia)
        {
            var spec = MateriaCatalog.FromGrade(m.StatName, m.Grade, m.StatValue);
            melds.Add(new MeldSlot
            {
                SlotIndex    = m.SlotIndex,
                IsGuaranteed = m.SlotIndex < 2,
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

        for (int i = 0; i < totalAllowedSlots; i++)
        {
            if (existingIndices.Contains(i)) continue;
            melds.Add(new MeldSlot
            {
                SlotIndex    = i,
                IsGuaranteed = i < 2,
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
