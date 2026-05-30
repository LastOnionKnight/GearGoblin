using System.Collections.Generic;
using GearGoblin.Core.Materia;

namespace GearGoblin.Services;

public static class MeldablePieceExtensions
{
    private const int MaxOvermeldSlots = 5;

    public static MeldablePiece FromEquipped(this EquippedPiece piece)
    {
        int guaranteed = piece.MateriaSlotCount;
        int total      = piece.IsOvermeldAllowed ? MaxOvermeldSlots : guaranteed;

        var melds = new List<MeldSlot>();
        var stats = new Dictionary<Substat, int>();

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
            BaseSubstats  = piece.BaseSubstats,
            SubstatCap    = piece.SubstatCap,
        };
    }

    private static double SuccessRateForSlot(int slotIndex) => slotIndex switch
    {
        0 => 1.00,
        1 => 1.00,
        2 => 0.17,
        3 => 0.10,
        4 => 0.07,
        _ => 0.00,
    };
}

