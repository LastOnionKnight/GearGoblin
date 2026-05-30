// Materia/MeldOptimizer.cs
// Core meld recommendation engine.
//
// Algorithm:
//   1. Score function: how much does adding N stat-X to piece-P improve the build?
//      score = weight(X) * marginal_pct(X, current_total + N) - overcap_penalty
//   2. Plan mode: greedy fill of empty slots, picking highest-scoring (stat, slot)
//      pair each iteration. After each pick, recompute totals (because Crit
//      breakpoints shift Det's marginal value, etc.).
//   3. Audit mode: for each *filled* slot, compare its current contribution
//      against the best-possible alternative. Flag if better choice exists,
//      the slot is overcapped, or the tier is outdated.

using System;
using System.Collections.Generic;
using System.Linq;
using GearGoblin.Services;

namespace GearGoblin.Materia;

/// <summary>
/// One concrete recommendation: "put this materia in this slot of this piece".
/// </summary>
public sealed record MeldRecommendation(
    EquipSlot   Piece,
    string      PieceName,
    int         SlotIndex,
    bool        IsGuaranteedSlot,
    MateriaSpec Materia,
    double      ScoreGain,
    string      Reasoning
);

/// <summary>
/// Severity of an audit finding — used for color-coding in the UI.
/// </summary>
public enum AuditSeverity { Good, Minor, Warning, Critical }

/// <summary>
/// One issue (or non-issue) found in the user's existing melds.
/// </summary>
public sealed record MeldAudit(
    EquipSlot      Piece,
    string         PieceName,
    int            SlotIndex,
    MateriaSpec?   Current,
    AuditSeverity  Severity,
    string         Headline,
    string         Detail,
    MateriaSpec?   SuggestedReplacement = null,
    double         GainIfReplaced       = 0
);

public sealed class OptimizerResult
{
    /// <summary>For empty slots: what should be melded there?</summary>
    public List<MeldRecommendation> PlanRecommendations { get; init; } = new();

    /// <summary>For each existing meld + empty slot: what's its status?</summary>
    public List<MeldAudit> Audits { get; init; } = new();

    /// <summary>Sum of all PlanRecommendations.ScoreGain (rough total expected DPS gain).</summary>
    public double TotalProjectedGain { get; init; }
}

public static class MeldOptimizer
{
    /// <summary>
    /// Run both Plan and Audit analysis on the given gearset.
    /// </summary>
    public static OptimizerResult Optimize(
        IReadOnlyList<MeldablePiece> pieces,
        StatSnapshot                 stats,
        LevelMod                     mod,
        JobProfile                   profile,
        WeightMode                   weightMode)
    {
        var plan   = GeneratePlan(pieces, stats, mod, profile, weightMode);
        var audits = GenerateAudits(pieces, stats, mod, profile, weightMode);
        var totalGain = plan.Sum(r => r.ScoreGain);

        return new OptimizerResult
        {
            PlanRecommendations = plan,
            Audits              = audits,
            TotalProjectedGain  = totalGain,
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // PLAN MODE
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Greedy fill: for each empty slot across all pieces, pick the materia
    /// that gives the highest weighted marginal gain. Iterate until all
    /// empty slots are filled or no positive-gain meld remains.
    /// </summary>
    private static List<MeldRecommendation> GeneratePlan(
        IReadOnlyList<MeldablePiece> pieces,
        StatSnapshot                 stats,
        LevelMod                     mod,
        JobProfile                   profile,
        WeightMode                   weightMode)
    {
        var recs = new List<MeldRecommendation>();

        // Working copies of stat totals — we modify these as we plan melds
        var workingStats = new Dictionary<Substat, int>
        {
            [Substat.CriticalHit]   = stats.Crit,
            [Substat.Determination] = stats.Det,
            [Substat.DirectHit]     = stats.DH,
            [Substat.SkillSpeed]    = stats.SkS,
            [Substat.SpellSpeed]    = stats.SpS,
            [Substat.Tenacity]      = stats.Ten,
            [Substat.Piety]         = stats.Pie,
        };

        // Working copy of per-piece meld totals (to track overcap during planning).
        // Defensive: if InventoryReader returns two pieces with the same slot
        // (which shouldn't happen but has in past patches), keep the first and skip
        // the rest rather than crashing the whole tab.
        var workingPieceMelds = new Dictionary<EquipSlot, Dictionary<Substat, int>>();
        foreach (var p in pieces)
        {
            if (workingPieceMelds.ContainsKey(p.Slot)) continue;
            workingPieceMelds[p.Slot] = new Dictionary<Substat, int>(p.CurrentMeldStats);
        }

        // Collect all empty slots, paired with their pieces
        var emptySlots = new List<(MeldablePiece piece, MeldSlot slot)>();
        foreach (var piece in pieces)
            foreach (var slot in piece.Slots)
                if (slot.IsEmpty)
                    emptySlots.Add((piece, slot));

        // Sort: guaranteed slots first (we plan those before low-success overmelds)
        emptySlots.Sort((a, b) =>
        {
            if (a.slot.IsGuaranteed != b.slot.IsGuaranteed)
                return a.slot.IsGuaranteed ? -1 : 1;
            return a.slot.SlotIndex.CompareTo(b.slot.SlotIndex);
        });

        // Greedy: for each empty slot, pick the best materia
        foreach (var (piece, slot) in emptySlots)
        {
            var best = BestMateriaForSlot(piece, slot, workingStats, workingPieceMelds[piece.Slot],
                                          mod, profile, weightMode);
            if (best is null) continue;

            recs.Add(best);

            // Update working state
            workingStats[best.Materia.Stat] += best.Materia.Value;
            workingPieceMelds[piece.Slot].TryGetValue(best.Materia.Stat, out var v);
            workingPieceMelds[piece.Slot][best.Materia.Stat] = v + best.Materia.Value;
        }

        return recs;
    }

    /// <summary>
    /// For one empty slot: which materia gives the most weighted gain?
    /// Returns null if no positive-gain option exists (e.g., all overcapped).
    /// </summary>
    private static MeldRecommendation? BestMateriaForSlot(
        MeldablePiece                piece,
        MeldSlot                     slot,
        Dictionary<Substat, int>     workingStats,
        Dictionary<Substat, int>     pieceMeldTotals,
        LevelMod                     mod,
        JobProfile                   profile,
        WeightMode                   weightMode)
    {
        var tier = MateriaCatalog.CurrentEndgameTier;
        MeldRecommendation? best = null;

        foreach (var stat in profile.RelevantStats)
        {
            var weight = OptimizerWeights.WeightFor(weightMode, profile, stat);
            if (weight <= 0) continue;

            var spec = MateriaCatalog.Spec(tier, stat);

            // Calculate actual stat gained, clamping to the piece's cap
            pieceMeldTotals.TryGetValue(stat, out var currentOnPiece);
            piece.BaseSubstats.TryGetValue(stat, out var baseOnPiece);
            
            var roomForStat = Math.Max(0, piece.SubstatCap - (currentOnPiece + baseOnPiece));
            var actualGain  = Math.Min(spec.Value, roomForStat);
            
            if (actualGain <= 0) continue;

            // Marginal % gain at the new total uses only the *actual* stat gained
            var currentTotal = workingStats[stat];
            var marginalPct  = MarginalPctForStat(stat, currentTotal, currentTotal + actualGain, mod);

            // Small penalty for wasted points to break ties against fully-utilized melds
            var wasted = spec.Value - actualGain;
            var overcapPenalty = wasted * 0.001;  // each wasted point cuts score

            // Apply slot success rate (overmeld slots score lower)
            var rawScore = weight * marginalPct - overcapPenalty;
            var score    = rawScore * slot.SuccessRate;

            if (score <= 0) continue;
            if (best is null || score > best.ScoreGain)
            {
                var reasoning = BuildPlanReasoning(stat, marginalPct, weight, weightMode, wasted, slot);
                best = new MeldRecommendation(
                    Piece            : piece.Slot,
                    PieceName        : piece.Name,
                    SlotIndex        : slot.SlotIndex,
                    IsGuaranteedSlot : slot.IsGuaranteed,
                    Materia          : spec,
                    ScoreGain        : score,
                    Reasoning        : reasoning);
            }
        }

        return best;
    }

    private static string BuildPlanReasoning(
        Substat stat, double marginalPct, double weight, WeightMode mode,
        int overcap, MeldSlot slot)
    {
        var parts = new List<string>();
        parts.Add($"+{marginalPct * 100:0.00}% {stat.Display()}");
        if (mode == WeightMode.BalancePreset && weight < 1.0)
            parts.Add($"weighted ×{weight:0.00}");
        if (overcap > 0)
            parts.Add($"⚠ {overcap} overcap");
        if (!slot.IsGuaranteed)
            parts.Add($"overmeld ({slot.SuccessRate * 100:0}% success)");
        return string.Join(", ", parts);
    }

    // ─────────────────────────────────────────────────────────────────────
    // AUDIT MODE
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For each existing meld, decide if it's good, mid, or bad. Three failure modes:
    ///   1. Wrong stat: a different substat would have given more
    ///   2. Outdated tier: same stat but the user could use a higher tier
    ///   3. Overcapped: this materia is hitting the stat cap and partially wasted
    /// </summary>
    private static List<MeldAudit> GenerateAudits(
        IReadOnlyList<MeldablePiece> pieces,
        StatSnapshot                 stats,
        LevelMod                     mod,
        JobProfile                   profile,
        WeightMode                   weightMode)
    {
        var audits = new List<MeldAudit>();

        // Build per-stat totals (current state, no projections)
        var totals = new Dictionary<Substat, int>
        {
            [Substat.CriticalHit]   = stats.Crit,
            [Substat.Determination] = stats.Det,
            [Substat.DirectHit]     = stats.DH,
            [Substat.SkillSpeed]    = stats.SkS,
            [Substat.SpellSpeed]    = stats.SpS,
            [Substat.Tenacity]      = stats.Ten,
            [Substat.Piety]         = stats.Pie,
        };

        foreach (var piece in pieces)
        {
            foreach (var slot in piece.Slots)
            {
                if (slot.IsEmpty || slot.Current is null) continue;
                var current = slot.Current.Value;

                // v0.6.6.1 BUG-003 guard: skip melds with unrecognized stat type.
                // MateriaCatalog.StatNameToSubstat() returns Substat.None for
                // unknown stat strings; AuditSingleMeld would then throw
                // KeyNotFoundException on totals[current.Stat] (line ~292).
                // Quietly skip — we can't meaningfully audit a materia whose
                // stat we don't know how to score.
                if (current.Stat == Substat.None) continue;

                var audit = AuditSingleMeld(piece, slot, current, totals, mod, profile, weightMode);
                audits.Add(audit);
            }
        }

        return audits;
    }

    private static MeldAudit AuditSingleMeld(
        MeldablePiece                piece,
        MeldSlot                     slot,
        MateriaSpec                  current,
        Dictionary<Substat, int>     totals,
        LevelMod                     mod,
        JobProfile                   profile,
        WeightMode                   weightMode)
    {
        // Score what's currently melded
        var currentWeight = OptimizerWeights.WeightFor(weightMode, profile, current.Stat);
        var currentTotal  = totals[current.Stat];
        var currentMarginalPct = MarginalPctForStat(
            current.Stat,
            currentTotal - current.Value,  // what would we have lost without this materia?
            currentTotal,
            mod);
        var currentScore = currentWeight * currentMarginalPct;

        // What's the best alternative?
        MateriaSpec? bestAlt = null;
        double       bestAltScore = 0;
        double       bestAltMarginal = 0;
        foreach (var stat in profile.RelevantStats)
        {
            if (stat == current.Stat) continue;
            var w = OptimizerWeights.WeightFor(weightMode, profile, stat);
            if (w <= 0) continue;

            var altSpec = MateriaCatalog.Spec(MateriaCatalog.CurrentEndgameTier, stat);
            var altTotal = totals[stat];
            var altMarg = MarginalPctForStat(stat, altTotal, altTotal + altSpec.Value, mod);
            var altScore = w * altMarg;
            if (altScore > bestAltScore)
            {
                bestAlt = altSpec;
                bestAltScore = altScore;
                bestAltMarginal = altMarg;
            }
        }

        // Overcap check on the piece
        piece.CurrentMeldStats.TryGetValue(current.Stat, out var pieceTotal);
        piece.BaseSubstats.TryGetValue(current.Stat, out var baseStat);
        var cap = piece.SubstatCap;
        var overcap = Math.Max(0, pieceTotal + baseStat - cap);

        // Outdated tier check
        var bestTierForStat = MateriaCatalog.ValueOf(MateriaCatalog.CurrentEndgameTier, current.Stat);
        var outdated = current.Tier < MateriaCatalog.CurrentEndgameTier
                        && current.Value < bestTierForStat;

        // Decide severity. Highest-priority issue wins.
        if (overcap >= current.Value)
        {
            // Most or all of this materia is wasted
            return new MeldAudit(
                Piece                : piece.Slot,
                PieceName            : piece.Name,
                SlotIndex            : slot.SlotIndex,
                Current              : current,
                Severity             : AuditSeverity.Critical,
                Headline             : "Mostly wasted: overcap",
                Detail               : $"{overcap} of {current.Value} stat is over the piece's {current.Stat.Short()} cap",
                SuggestedReplacement : bestAlt,
                GainIfReplaced       : bestAltScore - currentScore);
        }

        if (overcap > 0)
        {
            return new MeldAudit(
                Piece                : piece.Slot,
                PieceName            : piece.Name,
                SlotIndex            : slot.SlotIndex,
                Current              : current,
                Severity             : AuditSeverity.Warning,
                Headline             : $"Partial overcap (-{overcap})",
                Detail               : $"{overcap} stat over the {current.Stat.Short()} cap on this piece",
                SuggestedReplacement : bestAlt,
                GainIfReplaced       : bestAltScore - currentScore);
        }

        if (currentWeight <= 0)
        {
            return new MeldAudit(
                Piece                : piece.Slot,
                PieceName            : piece.Name,
                SlotIndex            : slot.SlotIndex,
                Current              : current,
                Severity             : AuditSeverity.Critical,
                Headline             : $"Wasted on {profile.Name}",
                Detail               : $"{profile.Name} doesn't use {current.Stat.Display()}",
                SuggestedReplacement : bestAlt,
                GainIfReplaced       : bestAltScore);
        }

        if (bestAlt is not null && bestAltScore > currentScore * 1.1)
        {
            // 10% threshold to avoid noisy "swap to barely-better" recommendations
            return new MeldAudit(
                Piece                : piece.Slot,
                PieceName            : piece.Name,
                SlotIndex            : slot.SlotIndex,
                Current              : current,
                Severity             : AuditSeverity.Minor,
                Headline             : $"{bestAlt.Value.Stat.Short()} would be better",
                Detail               : $"Currently +{currentMarginalPct * 100:0.00}%, swap gives +{bestAltMarginal * 100:0.00}%",
                SuggestedReplacement : bestAlt,
                GainIfReplaced       : bestAltScore - currentScore);
        }

        if (outdated)
        {
            var upgrade = MateriaCatalog.Spec(MateriaCatalog.CurrentEndgameTier, current.Stat);
            return new MeldAudit(
                Piece                : piece.Slot,
                PieceName            : piece.Name,
                SlotIndex            : slot.SlotIndex,
                Current              : current,
                Severity             : AuditSeverity.Minor,
                Headline             : $"Outdated tier ({current.Tier.Roman()} → {upgrade.Tier.Roman()})",
                Detail               : $"+{upgrade.Value - current.Value} stat available",
                SuggestedReplacement : upgrade,
                GainIfReplaced       : 0);
        }

        return new MeldAudit(
            Piece     : piece.Slot,
            PieceName : piece.Name,
            SlotIndex : slot.SlotIndex,
            Current   : current,
            Severity  : AuditSeverity.Good,
            Headline  : "Solid",
            Detail    : $"+{currentMarginalPct * 100:0.00}% {current.Stat.Short()}");
    }

    // ─────────────────────────────────────────────────────────────────────
    // SCORING HELPERS
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Marginal % gain when raising a substat from `before` to `after`.
    /// Returns the difference in display-value (e.g. 0.001 for +0.1%).
    /// </summary>
    private static double MarginalPctForStat(Substat stat, int before, int after, LevelMod mod)
    {
        if (before <= 0) before = 1;
        if (after  <= 0) after  = 1;

        var pBefore = StatToDisplay(stat, before, mod);
        var pAfter  = StatToDisplay(stat, after,  mod);
        return Math.Max(0, pAfter - pBefore);
    }

    private static double StatToDisplay(Substat stat, int value, in LevelMod mod) => stat switch
    {
        Substat.CriticalHit   => Formulas.CritRate(value, mod).DisplayValue,
        Substat.Determination => Formulas.Determination(value, mod).DisplayValue,
        Substat.DirectHit     => Formulas.DirectHit(value, mod).DisplayValue,
        Substat.SkillSpeed    => Formulas.SpeedDamage(value, mod).DisplayValue,
        Substat.SpellSpeed    => Formulas.SpeedDamage(value, mod).DisplayValue,
        Substat.Tenacity      => Formulas.TenacityDamage(value, mod).DisplayValue,
        Substat.Piety         => Formulas.Piety(value, mod).DisplayValue / 1000.0,  // normalize to a comparable scale
        _ => 0.0,
    };
}
