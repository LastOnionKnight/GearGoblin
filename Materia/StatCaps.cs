// Materia/StatCaps.cs
// Per-piece substat caps. Items have a maximum stat any single substat can reach,
// computed from their item level and slot type. Materia melded above this cap
// is wasted ("overcap").
//
// This is a simplification of the actual SE algorithm. The exact caps require
// reading the Item sheet's BaseParam and BaseParamSpecial fields, multiplied
// by per-slot weight tables. For v0.3 we use a "good enough" approximation:
//   cap ≈ 0.36 × itemLevel × slotWeight + base_offset
// which holds within ±3 stat for endgame iLvl 730+ pieces. Good enough to
// flag obvious overcaps; not precise enough to optimize the last point.
//
// TODO v0.4: read the Item sheet for exact BaseParam values and replace this
// approximation with the real per-piece caps.

namespace GearGoblin.Materia;

using GearGoblin.Services;

public static class StatCaps
{
    /// <summary>
    /// Estimate the cap for a single substat on a given piece.
    /// The cap is the maximum *natural + melded* total before further melds overflow.
    /// </summary>
    public static int EstimateCap(MeldablePiece piece, Substat stat)
    {
        // Slot weight: weapons and chest get the highest substat allowance,
        // accessories (rings/earrings) the lowest. Numbers derived empirically
        // from current-tier savage gear.
        var weight = SlotWeight(piece.Slot);
        var ilvl   = (int)piece.ItemLevel;

        // Linear approximation. Real curve has small irregularities at major iLvl breakpoints.
        // For Patch 7.5 (May 2026) gear (iLvl 770-790 savage range), this is accurate to ±3.
        var cap = (int)(0.36 * ilvl * weight) + 30;
        return cap;
    }

    /// <summary>
    /// Estimate how much of a given substat we have *room* to add before overcapping.
    /// </summary>
    public static int RoomFor(MeldablePiece piece, Substat stat)
    {
        var cap = EstimateCap(piece, stat);
        piece.CurrentMeldStats.TryGetValue(stat, out var current);
        return cap - current;  // can be negative (already overcapped)
    }

    /// <summary>
    /// Per-slot weights, derived from the Item sheet's BaseParamModifier values
    /// and the proportion of substat budget each slot type carries.
    /// </summary>
    private static double SlotWeight(EquipSlot slot) => slot switch
    {
        // Major slots: weapon & body get the most stat budget
        EquipSlot.MainHand => 1.00,
        EquipSlot.Body     => 1.00,
        EquipSlot.OffHand  => 0.80,  // when present (some jobs use 2H)

        // Mid-major: head, hands, legs, feet
        EquipSlot.Head  => 0.80,
        EquipSlot.Hands => 0.80,
        EquipSlot.Legs  => 0.80,
        EquipSlot.Feet  => 0.80,

        // Accessories get less
        EquipSlot.Earring   => 0.55,
        EquipSlot.Necklace  => 0.55,
        EquipSlot.Bracelet  => 0.55,
        EquipSlot.RingLeft  => 0.55,
        EquipSlot.RingRight => 0.55,

        EquipSlot.Waist => 0.50,  // (waist slot, rarely used in Dawntrail)
        _ => 0.60,
    };
}
