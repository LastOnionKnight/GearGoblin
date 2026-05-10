// Materia/OptimizerWeights.cs
// The optimizer's score function is parameterized on per-substat weights.
// Two modes:
//   - PureMath: every relevant substat has weight 1.0 — pure marginal % gain ranking
//   - BalancePreset: per-job weights from JobProfile.BalanceWeights
//
// The user toggles between these in the Materia tab UI.

using System.Collections.Generic;

namespace GearGoblin.Materia;

public enum WeightMode
{
    /// <summary>Uniform weights. Pick whatever stat gives the highest raw % gain.</summary>
    PureMath,

    /// <summary>Per-job weights from community consensus (The Balance, etc.).</summary>
    BalancePreset,
}

public static class OptimizerWeights
{
    /// <summary>
    /// Get the weight for a substat given the active mode and job profile.
    /// </summary>
    public static double WeightFor(WeightMode mode, JobProfile profile, Substat stat)
    {
        if (mode == WeightMode.BalancePreset)
        {
            return profile.BalanceWeights.TryGetValue(stat, out var w) ? w : 0.0;
        }

        // PureMath: 1.0 if the job uses this stat, 0 otherwise.
        // Without this filter we'd recommend Piety melds for a Viper.
        foreach (var s in profile.RelevantStats)
            if (s == stat) return 1.0;

        return 0.0;
    }

    /// <summary>
    /// Tag the active mode with a human-readable name for the UI label.
    /// </summary>
    public static string DisplayName(WeightMode mode) => mode switch
    {
        WeightMode.PureMath      => "Pure math",
        WeightMode.BalancePreset => "Balance preset",
        _ => mode.ToString(),
    };
}
