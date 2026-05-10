// Materia/Formulas.cs
// Substat → percentage conversion formulas, with breakpoint analysis.
// Re-derived from public datamining sources, NOT copied from any AGPL plugin.
//
// Primary source:
//   Akhmorning Allagan Studies, https://www.akhmorning.com/allagan-studies/stats/
// Cross-references:
//   FFXIV datamining repo (xivapi/ffxiv-datamining)
//   The Balance Discord pinned formulas (community-verified)
//
// All formulas verified to match in-game Character Window display when fed
// real stat values. Discrepancies indicate either (a) a level-table constant
// changed in a patch we haven't tracked, or (b) we're computing the wrong
// thing (e.g., Crit Damage vs Crit Rate). Cross-check both before assuming
// a formula is wrong.

using System;

namespace GearGoblin.Materia;

public readonly record struct StatBreakdown(
    int CurrentValue,        // raw stat (e.g., 2474)
    double DisplayValue,     // human-meaningful value (e.g., 0.255 = 25.5% rate, or 1.545 = damage multiplier)
    int    PrevTier,         // stat value at the breakpoint at or below CurrentValue
    int    NextTier,         // stat value where DisplayValue would tick up by one minimum increment
    double PointsPerTier     // how many stat points buy one minimum increment
);

public static class Formulas
{
    // ---------- Critical Hit ----------

    /// <summary>
    /// Critical Hit Rate: percent chance for any direct attack to crit.
    /// Returns 0.255 for 25.5%. Floor of 5% (50 in our 1000-scale) is built in.
    /// Formula: floor(200 * (crit - subBase) / div + 50) / 1000
    /// </summary>
    public static StatBreakdown CritRate(int crit, in LevelMod mod)
    {
        var raw = Math.Floor(200d * (crit - mod.Sub) / mod.Div + 50) / 1000d;
        var prev = (int)Math.Ceiling((raw * 1000d - 50.0000001) * mod.Div / 200d + mod.Sub);
        var next = (int)Math.Ceiling(((raw + 0.001) * 1000d - 50.0000001) * mod.Div / 200d + mod.Sub);
        return new StatBreakdown(crit, raw, prev, next, mod.Div / 200d);
    }

    /// <summary>
    /// Critical Hit Damage multiplier: 1.545 means crits deal 154.5% of normal hit damage.
    /// Floor of 1.4 baked in. Same stat as CritRate; this is what your hits do *when* they crit.
    /// </summary>
    public static StatBreakdown CritDmg(int crit, in LevelMod mod)
    {
        var raw = Math.Floor(200d * (crit - mod.Sub) / mod.Div + 1400) / 1000d;
        var prev = (int)Math.Ceiling((raw * 1000d - 1400.0000001) * mod.Div / 200d + mod.Sub);
        var next = (int)Math.Ceiling(((raw + 0.001) * 1000d - 1400.0000001) * mod.Div / 200d + mod.Sub);
        return new StatBreakdown(crit, raw, prev, next, mod.Div / 200d);
    }

    // ---------- Direct Hit ----------

    /// <summary>
    /// Direct Hit Rate: percent chance for any attack to direct-hit (deal 1.25× damage).
    /// Returns 0.124 for 12.4%. No floor; bottoms at 0%.
    /// </summary>
    public static StatBreakdown DirectHit(int dh, in LevelMod mod)
    {
        var raw = Math.Floor(550d * (dh - mod.Sub) / mod.Div) / 1000d;
        var prev = (int)Math.Ceiling(raw * 1000d * mod.Div / 550d + mod.Sub);
        var next = (int)Math.Ceiling((raw + 0.001) * 1000d * mod.Div / 550d + mod.Sub);
        return new StatBreakdown(dh, raw, prev, next, mod.Div / 550d);
    }

    // ---------- Determination ----------

    /// <summary>
    /// Determination: flat damage multiplier (and healing for healers).
    /// Returns 0.086 for +8.6% damage. Uses Main baseline, not Sub.
    /// </summary>
    public static StatBreakdown Determination(int det, in LevelMod mod)
    {
        var raw = Math.Floor(140d * (det - mod.Main) / mod.Div) / 1000d;
        var prev = (int)Math.Ceiling(raw * 1000d * mod.Div / 140d + mod.Main);
        var next = (int)Math.Ceiling((raw + 0.001) * 1000d * mod.Div / 140d + mod.Main);
        return new StatBreakdown(det, raw, prev, next, mod.Div / 140d);
    }

    // ---------- Skill / Spell Speed ----------

    /// <summary>
    /// Speed: damage multiplier from skill/spell speed (auto-attack scaling, GCD reduction).
    /// Returns 0.012 for +1.2% damage. Same formula for Skill and Spell speed.
    /// Note: GCD calculation uses speed differently — see GcdFromSpeed for that.
    /// </summary>
    public static StatBreakdown SpeedDamage(int speed, in LevelMod mod)
    {
        var raw = Math.Floor(130d * (speed - mod.Sub) / mod.Div) / 1000d;
        var prev = (int)Math.Ceiling(raw * 1000d * mod.Div / 130d + mod.Sub);
        var next = (int)Math.Ceiling((raw + 0.001) * 1000d * mod.Div / 130d + mod.Sub);
        return new StatBreakdown(speed, raw, prev, next, mod.Div / 130d);
    }

    /// <summary>
    /// Compute the GCD time (in seconds) for a 2.50s base GCD given speed stat.
    /// </summary>
    public static double GcdFromSpeed(int speed, in LevelMod mod, double baseGcd = 2.50)
    {
        // GCD = floor(baseGcd * (1000 - floor(130 * (speed - sub) / div)) / 1000) * 0.01
        // (The standard datamined GCD formula, in full.)
        var speedScalar = Math.Floor(130d * (speed - mod.Sub) / mod.Div);
        var modifier = (1000d - speedScalar) / 1000d;
        var gcd = Math.Floor(baseGcd * modifier * 100d) / 100d;
        return gcd;
    }

    // ---------- Tenacity ----------

    /// <summary>
    /// Tenacity: tank-only stat. Provides both damage dealt (positive) and damage taken (mitigation).
    /// Returns 0.025 for +2.5% damage / -2.5% damage taken (approximate, mit is slightly different).
    /// </summary>
    public static StatBreakdown TenacityDamage(int ten, in LevelMod mod)
    {
        var raw = Math.Floor(112d * (ten - mod.Sub) / mod.Div) / 1000d;
        var prev = (int)Math.Ceiling(raw * 1000d * mod.Div / 112d + mod.Sub);
        var next = (int)Math.Ceiling((raw + 0.001) * 1000d * mod.Div / 112d + mod.Sub);
        return new StatBreakdown(ten, raw, prev, next, mod.Div / 112d);
    }

    public static StatBreakdown TenacityMitigation(int ten, in LevelMod mod)
    {
        var raw = Math.Floor(200d * (ten - mod.Sub) / mod.Div) / 1000d;
        var prev = (int)Math.Ceiling(raw * 1000d * mod.Div / 200d + mod.Sub);
        var next = (int)Math.Ceiling((raw + 0.001) * 1000d * mod.Div / 200d + mod.Sub);
        return new StatBreakdown(ten, raw, prev, next, mod.Div / 200d);
    }

    // ---------- Piety ----------

    /// <summary>
    /// Piety: healer-only stat. MP regen per tick (3-second tick).
    /// Returns total MP/tick value (e.g., 245 for "you regenerate 245 MP per tick").
    /// </summary>
    public static StatBreakdown Piety(int pie, in LevelMod mod)
    {
        var mpPerTick = Math.Floor(150d * (pie - mod.Main) / mod.Div) + 200;
        return new StatBreakdown(pie, mpPerTick, 0, 0, 0); // tier analysis less meaningful for Piety
    }
}
