// Materia/JobProfile.cs
// Per-job knowledge: which substats each job uses, and what the community
// consensus weights are if the user opts into "Balance preset" mode.
//
// IDs match FFXIV ClassJob sheet rowIDs. Public game data, datamined.
// Source: https://github.com/xivapi/ffxiv-datamining/blob/master/csv/ClassJob.csv
//
// Weight philosophy:
//   - "Pure math" mode uses uniform weights — every stat scored only on its
//     mathematical percentage delta, no opinion baked in beyond the formulas.
//   - "Balance preset" mode applies these per-job weights; values reflect
//     widely-cited community consensus circa Patch 7.5 (May 2026). They will
//     drift over time and we should be honest about that in the UI.

using System.Collections.Generic;

namespace GearGoblin.Materia;

public enum Role { Tank, MeleeDps, PhysicalRangedDps, MagicalRangedDps, Healer, Crafter, Gatherer, Unknown }

public sealed record JobProfile(
    uint   JobId,
    string Name,
    Role   Role,
    Substat[] RelevantStats,        // stats this job uses at all (formulas apply)
    Dictionary<Substat, double> BalanceWeights  // community-preset weights, sum need not equal 1
);

public static class JobProfiles
{
    // Common substat sets, used by multiple jobs.
    private static readonly Substat[] DpsCore   = { Substat.CriticalHit, Substat.DirectHit, Substat.Determination, Substat.SkillSpeed };
    private static readonly Substat[] CasterCore = { Substat.CriticalHit, Substat.DirectHit, Substat.Determination, Substat.SpellSpeed };
    private static readonly Substat[] TankCore  = { Substat.CriticalHit, Substat.DirectHit, Substat.Determination, Substat.SkillSpeed, Substat.Tenacity };
    private static readonly Substat[] HealCore  = { Substat.CriticalHit, Substat.Determination, Substat.SpellSpeed, Substat.Piety };

    // Conventional weights for endgame (verified against The Balance pinned posts, May 2026).
    // Crit is canonically the highest-value stat for nearly all damage-dealers.
    private static Dictionary<Substat, double> CritFirstDpsWeights() => new()
    {
        [Substat.CriticalHit]   = 1.00,
        [Substat.DirectHit]     = 0.85,
        [Substat.Determination] = 0.70,
        [Substat.SkillSpeed]    = 0.10,
        [Substat.SpellSpeed]    = 0.10,
    };

    // Tank: Balance current consensus is "meld for damage" with Tenacity discounted.
    private static Dictionary<Substat, double> TankDamageWeights() => new()
    {
        [Substat.CriticalHit]   = 1.00,
        [Substat.DirectHit]     = 0.85,
        [Substat.Determination] = 0.75,
        [Substat.SkillSpeed]    = 0.05,
        [Substat.Tenacity]      = 0.30,  // factored low; tanks meld DPS in current meta
    };

    private static Dictionary<Substat, double> HealerWeights() => new()
    {
        [Substat.CriticalHit]   = 1.00,
        [Substat.Determination] = 0.80,
        [Substat.SpellSpeed]    = 0.30,  // some SpS for cast-time reduction
        [Substat.Piety]         = 0.50,  // heavily fight-dependent
    };

    public static readonly Dictionary<uint, JobProfile> All = new()
    {
        // Tanks
        [19] = new(19, "Paladin",     Role.Tank, TankCore, TankDamageWeights()),
        [21] = new(21, "Warrior",     Role.Tank, TankCore, TankDamageWeights()),
        [32] = new(32, "Dark Knight", Role.Tank, TankCore, TankDamageWeights()),
        [37] = new(37, "Gunbreaker",  Role.Tank, TankCore, TankDamageWeights()),
        // Melee DPS
        [20] = new(20, "Monk",        Role.MeleeDps, DpsCore, CritFirstDpsWeights()),
        [22] = new(22, "Dragoon",     Role.MeleeDps, DpsCore, CritFirstDpsWeights()),
        [30] = new(30, "Ninja",       Role.MeleeDps, DpsCore, CritFirstDpsWeights()),
        [34] = new(34, "Samurai",     Role.MeleeDps, DpsCore, CritFirstDpsWeights()),
        [39] = new(39, "Reaper",      Role.MeleeDps, DpsCore, CritFirstDpsWeights()),
        [41] = new(41, "Viper",       Role.MeleeDps, DpsCore, CritFirstDpsWeights()),
        // Physical Ranged
        [23] = new(23, "Bard",        Role.PhysicalRangedDps, DpsCore, CritFirstDpsWeights()),
        [31] = new(31, "Machinist",   Role.PhysicalRangedDps, DpsCore, CritFirstDpsWeights()),
        [38] = new(38, "Dancer",      Role.PhysicalRangedDps, DpsCore, CritFirstDpsWeights()),
        // Magical Ranged
        [25] = new(25, "Black Mage",  Role.MagicalRangedDps, CasterCore, CritFirstDpsWeights()),
        [27] = new(27, "Summoner",    Role.MagicalRangedDps, CasterCore, CritFirstDpsWeights()),
        [35] = new(35, "Red Mage",    Role.MagicalRangedDps, CasterCore, CritFirstDpsWeights()),
        [42] = new(42, "Pictomancer", Role.MagicalRangedDps, CasterCore, CritFirstDpsWeights()),
        // Healers
        [24] = new(24, "White Mage",  Role.Healer, HealCore, HealerWeights()),
        [28] = new(28, "Scholar",     Role.Healer, HealCore, HealerWeights()),
        [33] = new(33, "Astrologian", Role.Healer, HealCore, HealerWeights()),
        [40] = new(40, "Sage",        Role.Healer, HealCore, HealerWeights()),
    };

    /// <summary>
    /// Get the profile for a job; returns a permissive default if unrecognized
    /// (e.g., for Crafters/Gatherers which we don't optimize).
    /// </summary>
    public static JobProfile GetOrDefault(uint jobId)
    {
        return All.TryGetValue(jobId, out var profile)
            ? profile
            : new JobProfile(jobId, $"Job {jobId}", Role.Unknown, System.Array.Empty<Substat>(), new());
    }
}
