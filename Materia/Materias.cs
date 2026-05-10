// Materia/Materias.cs
// Catalog of materia tiers and their stat values.
//
// v0.3.1 (Bug C): grade-to-tier mapping was off by four (we assumed grade 0 = Tier V,
// but grade 0 is actually Tier I — every materia displayed "?" and every meld read
// as "outdated tier"). Fixed: grade N maps to Tier (N+1). Tier enum now spans I-XII.
//
// Hardcoded SubstatPerTier table is retained as a fallback for *projecting* hypothetical
// melds (Plan mode "what if I added a Crit XII here?"), but actual values for *existing*
// melds always come from MateriaMeld.StatValue which is read from the Lumina sheet at
// inventory-read time. The table only matters when proposing materia that don't yet exist
// in the player's inventory.

using System.Collections.Generic;

namespace GearGoblin.Materia;

/// <summary>
/// Discrete materia tiers. Grade byte from FFXIVClientStructs is 0-indexed:
/// grade 0 → Tier I, grade 11 → Tier XII.
/// </summary>
public enum MateriaTier
{
    Unknown = 0,
    Tier1  = 1,
    Tier2  = 2,
    Tier3  = 3,
    Tier4  = 4,
    Tier5  = 5,
    Tier6  = 6,
    Tier7  = 7,
    Tier8  = 8,
    Tier9  = 9,
    Tier10 = 10,
    Tier11 = 11,
    Tier12 = 12,  // Dawntrail endgame (Patch 7.0+)
}

public static class MateriaTierExt
{
    public static string Roman(this MateriaTier t) => t switch
    {
        MateriaTier.Tier1  => "I",
        MateriaTier.Tier2  => "II",
        MateriaTier.Tier3  => "III",
        MateriaTier.Tier4  => "IV",
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

public readonly record struct MateriaSpec(
    MateriaTier Tier,
    Substat     Stat,
    int         Value
)
{
    public string Display() => $"{Stat.Short()} {Tier.Roman()} (+{Value})";
}

public static class MateriaCatalog
{
    // Per-tier stat values used only for *projecting* melds that the player doesn't
    // already own (Plan-mode "what if you added a Crit XII?"). For actual existing
    // melds, MateriaMeld.StatValue is the source of truth — it comes straight from
    // Lumina at inventory read time.
    //
    // Tier      |  I  |  II |  III|  IV |  V  | VI  | VII | VIII|  IX |  X  |  XI |  XII|
    // ----------+-----+-----+-----+-----+-----+-----+-----+-----+-----+-----+-----+-----+
    // Substat   |  3  |  4  |  6  |  8  | 12  | 16  | 24  | 36  | 48  | 60  | 72  | 96  |
    //
    // Values match in-game tooltips for substat materia as of Patch 7.5 (May 2026).
    private static readonly Dictionary<MateriaTier, int> SubstatPerTier = new()
    {
        [MateriaTier.Tier1]  = 3,
        [MateriaTier.Tier2]  = 4,
        [MateriaTier.Tier3]  = 6,
        [MateriaTier.Tier4]  = 8,
        [MateriaTier.Tier5]  = 12,
        [MateriaTier.Tier6]  = 16,
        [MateriaTier.Tier7]  = 24,
        [MateriaTier.Tier8]  = 36,
        [MateriaTier.Tier9]  = 48,
        [MateriaTier.Tier10] = 60,
        [MateriaTier.Tier11] = 72,
        [MateriaTier.Tier12] = 96,
    };

    /// <summary>Default tier we recommend for new melds at level 100.</summary>
    public const MateriaTier CurrentEndgameTier = MateriaTier.Tier12;

    public static int ValueOf(MateriaTier tier, Substat stat)
    {
        return SubstatPerTier.TryGetValue(tier, out var v) ? v : 0;
    }

    public static MateriaSpec Spec(MateriaTier tier, Substat stat) =>
        new(tier, stat, ValueOf(tier, stat));

    /// <summary>
    /// Map an in-game materia stat name + grade byte to our internal MateriaSpec.
    /// Grade is 0-indexed in FFXIVClientStructs (grade 0 = Tier I, grade 11 = Tier XII).
    /// </summary>
    public static MateriaSpec FromGrade(string statName, byte grade, int statValue)
    {
        var stat = StatNameToSubstat(statName);
        // v0.3.1 fix: was assuming grade 0 = Tier V (off by 4). It's grade 0 = Tier I.
        var tier = grade <= 11 ? (MateriaTier)(grade + 1) : MateriaTier.Unknown;
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
