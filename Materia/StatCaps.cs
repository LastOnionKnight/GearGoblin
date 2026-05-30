// Materia/StatCaps.cs
// Per-piece substat caps. Items have a maximum stat any single substat can reach,
// computed from their item level and slot type. Materia melded above this cap
// is wasted ("overcap").

namespace GearGoblin.Materia;

using GearGoblin.Services;

public static class StatCaps
{
    /// <summary>
    /// Calculate how much of a given substat we have *room* to add before overcapping.
    /// Uses the exact per-piece cap and base stats loaded directly from the Lumina
    /// Item and ItemLevel sheets.
    /// </summary>
    public static int RoomFor(MeldablePiece piece, Substat stat)
    {
        var cap = piece.SubstatCap;
        piece.CurrentMeldStats.TryGetValue(stat, out var melded);
        piece.BaseSubstats.TryGetValue(stat, out var baseStat);
        return cap - (melded + baseStat);
    }
}
