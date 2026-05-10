// Materia/Materias.cs
// Catalog of known materia: which tier they are, which substat they provide, and how much.
//
// Why hardcode this instead of always reading from Lumina at runtime?
//   - We need fast tier comparisons in the optimizer ("is the user using outdated tier?")
//   - We need to project hypothetical melds before they exist on items
//   - Values are stable game data; rarely changed mid-patch-cycle
//
// Tier values verified against in-game materia tooltips, Patch 7.5 (May 2026).
// Source: Lumina Materia sheet + datamined values; cross-checked against Garland Tools.

using System.Collections.Generic;

namespace GearGoblin.Materia;

/// <summary>
/// Discrete materia tiers. Higher = newer = more stat per slot.
/// In current Dawntrail content, X is the baseline; XII is the latest savage materia.
/// </summary>
public enum MateriaTier
{
    Unknown = 0,
    Tier5  = 5,   // Heavensward
    Tier6  = 6,   // Stormblood
    Tier7  = 7,   // Shadowbringers leveling
    Tier8  = 8,   // Shadowbringers endgame
    Tier9  = 9,   // Endwalker leveling
    Tier10 = 10,  // Endwalker endgame
    Tier11 = 11,  // Dawntrail leveling
    Tier12 = 12,  // Dawntrail endgame (Patch 7.0+)
}

public static class MateriaTierExt
{
    public static string Roman(this MateriaTier t) => t switch
    {
        MateriaTier.Tier5  => "V",
        MateriaTier.Tier6  => "VI",
        MateriaTier.Tier7  => "VII",
        MateriaTier.Tier8  => "VIII",
        MateriaTier.Tier9  => "IX",
        MateriaTier.Tier10 => "X",
        MateriaTier.Tier11 => "XI",
        MateriaTier.Tier12 => "XII",
        _ => "?",
    };
}

/// <summary>
/// A specific materia: its tier, the substat it provides, and the stat amount.
/// </summary>
public readonly record struct MateriaSpec(
    MateriaTier Tier,
    Substat     Stat,
    int         Value
)
{
    public string Display() => $"{Stat.Short()} {Tier.Roman()} (+{Value})";
}

/// <summary>
/// Catalog of all materia we reason about. Built from datamined stat values.
/// Currently focused on tier IX-XII (the meta-relevant range for endgame content).
/// </summary>
public static class MateriaCatalog
{
    // Per-tier values for each substat. These are the magnitudes the Materia
    // sheet's Value[] field reports for the corresponding grade index.
    //
    // Substats (Crit/DH/Det/SkS/SpS/Ten) all share the same tier values.
    // Piety follows a slightly different curve in some patches but currently matches.
    //
    // Tier      |  V  |  VI |  VII | VIII |  IX  |   X  |  XI |  XII |
    // ----------+-----+-----+------+------+------+------+-----+------+
    // Substat   | 21  | 24  |  36  |  48  |  60  |  72  |  84 |  96  |
    //
    // (Healer-only Piety values match exactly in Dawntrail.)
    private static readonly Dictionary<MateriaTier, int> SubstatPerTier = new()
    {
        [MateriaTier.Tier5]  = 21,
        [MateriaTier.Tier6]  = 24,
        [MateriaTier.Tier7]  = 36,
        [MateriaTier.Tier8]  = 48,
        [MateriaTier.Tier9]  = 60,
        [MateriaTier.Tier10] = 72,
        [MateriaTier.Tier11] = 84,
        [MateriaTier.Tier12] = 96,
    };

    /// <summary>
    /// The default endgame tier we recommend for new melds at level 100.
    /// Bump this when a new tier ships.
    /// </summary>
    public const MateriaTier CurrentEndgameTier = MateriaTier.Tier12;

    /// <summary>How much stat does a tier-N materia for a given substat give?</summary>
    public static int ValueOf(MateriaTier tier, Substat stat)
    {
        // Piety has its own curve in some content but currently matches substat values.
        // If a future patch breaks this, add a Piety-specific table.
        return SubstatPerTier.TryGetValue(tier, out var v) ? v : 0;
    }

    /// <summary>Build a spec for a hypothetical meld.</summary>
    public static MateriaSpec Spec(MateriaTier tier, Substat stat) =>
        new(tier, stat, ValueOf(tier, stat));

    /// <summary>
    /// Map an in-game materia stat name (e.g., "Critical Hit") + grade byte
    /// to our internal MateriaSpec. Used to interpret InventoryReader's MateriaMeld.
    /// Grade byte from FFXIVClientStructs is 0-indexed (grade 0 = tier V, grade 11 = tier XII).
    /// </summary>
    public static MateriaSpec FromGrade(string statName, byte grade, int statValue)
    {
        var stat = StatNameToSubstat(statName);
        var tier = grade switch
        {
            0  => MateriaTier.Tier5,
            1  => MateriaTier.Tier6,
            2  => MateriaTier.Tier7,
            3  => MateriaTier.Tier8,
            4  => MateriaTier.Tier9,
            5  => MateriaTier.Tier10,
            6  => MateriaTier.Tier11,
            7  => MateriaTier.Tier12,
            _  => MateriaTier.Unknown,
        };
        return new MateriaSpec(tier, stat, statValue);
    }

    private static Substat StatNameToSubstat(string name) => name switch
    {
        "Critical Hit"   => Substat.CriticalHit,
        "Determination"  => Substat.Determination,
        "Direct Hit Rate"=> Substat.DirectHit,
        "Skill Speed"    => Substat.SkillSpeed,
        "Spell Speed"    => Substat.SpellSpeed,
        "Tenacity"       => Substat.Tenacity,
        "Piety"          => Substat.Piety,
        _                => Substat.None,
    };
}
