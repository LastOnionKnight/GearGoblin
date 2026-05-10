// Materia/StatReader.cs
// Pulls the player's current substat values from PlayerState.Attributes,
// and the level from Dalamud's IObjectTable.LocalPlayer.Level (canonical).
//
// We previously tried PlayerState.ClassJobLevels[jobId] but that array's
// indexing convention isn't a simple jobId lookup — it returned 0 for
// Lv 100 jobs, breaking every percentage downstream. Using LocalPlayer.Level
// is what every other plugin uses and it Just Works.

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
    /// Read current substats off PlayerState plus level/job from Dalamud's LocalPlayer.
    /// Returns null if either is unavailable.
    /// </summary>
    public static StatSnapshot? ReadCurrent()
    {
        var ps = PlayerState.Instance();
        if (ps == null) return null;

        var player = GearGoblin.DalamudServices.ObjectTable.LocalPlayer;
        if (player is null) return null;

        var attrs = ps->Attributes;

        return new StatSnapshot(
            Crit:  attrs[(int)Substat.CriticalHit],
            Det:   attrs[(int)Substat.Determination],
            DH:    attrs[(int)Substat.DirectHit],
            SkS:   attrs[(int)Substat.SkillSpeed],
            SpS:   attrs[(int)Substat.SpellSpeed],
            Ten:   attrs[(int)Substat.Tenacity],
            Pie:   attrs[(int)Substat.Piety],
            Level: player.Level,
            JobId: player.ClassJob.RowId
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
