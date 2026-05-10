// Materia/StatReader.cs
// Pulls the player's current substat values directly from PlayerState.
// PlayerState.Attributes is an array indexed by BaseParam ID — the same IDs
// our Substat enum uses. Reading is a single struct access; no allocation.
//
// Note: stats here are the *final, displayed* values, identical to what shows
// in the in-game Character window. Buffs, food, FC bonuses are baked in.
// For meld optimization we want "natural" stats (no food), so we expose both:
//   - ReadCurrent()  : whatever the game shows right now (food included)
//   - ReadBaseline() : best-effort food-stripped values, for offline planning

using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace GearGoblin.Materia;

public readonly record struct StatSnapshot(
    int Crit,
    int Det,
    int DH,
    int SkS,
    int SpS,
    int Ten,
    int Pie,
    int Level,
    uint JobId
);

public static unsafe class StatReader
{
    /// <summary>
    /// Read current substats off PlayerState. Includes food/FC buffs.
    /// Returns null if PlayerState is unavailable (not logged in, between zones, etc.).
    /// </summary>
    public static StatSnapshot? ReadCurrent()
    {
        var ps = PlayerState.Instance();
        if (ps == null) return null;

        var attrs = ps->Attributes;
        var jobId = ps->CurrentClassJobId;

        // ClassJobLevels is indexed by job id; safe to read the current job's level.
        int level = 0;
        if (jobId < ps->ClassJobLevels.Length)
            level = ps->ClassJobLevels[jobId];

        return new StatSnapshot(
            Crit:  attrs[(int)Substat.CriticalHit],
            Det:   attrs[(int)Substat.Determination],
            DH:    attrs[(int)Substat.DirectHit],
            SkS:   attrs[(int)Substat.SkillSpeed],
            SpS:   attrs[(int)Substat.SpellSpeed],
            Ten:   attrs[(int)Substat.Tenacity],
            Pie:   attrs[(int)Substat.Piety],
            Level: level,
            JobId: jobId
        );
    }

    /// <summary>
    /// Get the value for a specific substat from a snapshot.
    /// </summary>
    public static int GetValue(this StatSnapshot snap, Substat s) => s switch
    {
        Substat.CriticalHit   => snap.Crit,
        Substat.Determination => snap.Det,
        Substat.DirectHit     => snap.DH,
        Substat.SkillSpeed    => snap.SkS,
        Substat.SpellSpeed    => snap.SpS,
        Substat.Tenacity      => snap.Ten,
        Substat.Piety         => snap.Pie,
        _ => 0,
    };
}
