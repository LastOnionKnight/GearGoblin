// Materia/Substat.cs
// The seven substats GearGoblin's advisor reasons about, mapped to FFXIV BaseParam IDs.
// BaseParam IDs are stable game data, datamined and verified across patches:
//   https://github.com/xivapi/ffxiv-datamining/blob/master/csv/BaseParam.csv

namespace GearGoblin.Materia;

public enum Substat
{
    None = 0,
    CriticalHit = 27,
    Determination = 44,
    DirectHit = 22,
    SkillSpeed = 45,
    SpellSpeed = 46,
    Tenacity = 19,
    Piety = 6,
}

public static class SubstatExt
{
    /// <summary>Display name for UI.</summary>
    public static string Display(this Substat s) => s switch
    {
        Substat.CriticalHit   => "Critical Hit",
        Substat.Determination => "Determination",
        Substat.DirectHit     => "Direct Hit",
        Substat.SkillSpeed    => "Skill Speed",
        Substat.SpellSpeed    => "Spell Speed",
        Substat.Tenacity      => "Tenacity",
        Substat.Piety         => "Piety",
        _                     => "Unknown",
    };

    /// <summary>Short name for compact tables.</summary>
    public static string Short(this Substat s) => s switch
    {
        Substat.CriticalHit   => "Crit",
        Substat.Determination => "Det",
        Substat.DirectHit     => "DH",
        Substat.SkillSpeed    => "SkS",
        Substat.SpellSpeed    => "SpS",
        Substat.Tenacity      => "Ten",
        Substat.Piety         => "Pie",
        _                     => "??",
    };
}
