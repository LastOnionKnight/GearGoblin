// Services/DerivationHelpers.cs
//
// v0.4.5 introduces two small concerns that the StatusPanelInjector consumes:
//
//   1. CprDetection — is CharacterPanelRefined installed and active? If so,
//      we default our derived-stat injection OFF so the two plugins don't
//      stack their derived rows on top of each other. The user can override
//      via Configuration.ForceDerivationsOverCpr.
//
//   2. DerivedStatFormatter — renders Formulas output into the short strings
//      we show in injected rows. Two modes: standard (one stat per row) and
//      compact (all derivations for a substat collapsed onto one line).
//
// Both are kept here rather than inside StatusPanelInjector to keep that
// file focused on AtkNode plumbing.

using System;
using System.Linq;
using GearGoblin.Materia;

namespace GearGoblin.Services;

internal static class CprDetection
{
    /// <summary>
    /// CharacterPanelRefined's official Dalamud internal name.
    /// </summary>
    private const string CprInternalName = "CharacterPanelRefined";

    /// <summary>
    /// Returns true if CPR is currently installed AND loaded into the running
    /// Dalamud session. Both conditions matter — a CPR install with the plugin
    /// disabled in /xlplugins is not actively injecting, so we can inject normally.
    /// </summary>
    public static bool IsCprActive()
    {
        try
        {
            var pi = GearGoblin.DalamudServices.PluginInterface;
            if (pi is null) return false;

            // InstalledPlugins lists every plugin Dalamud knows about, loaded
            // or not. IsLoaded narrows to currently-active.
            return pi.InstalledPlugins.Any(p =>
                p.IsLoaded &&
                string.Equals(p.InternalName, CprInternalName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            // If the API throws for any reason, fail open (assume CPR is not
            // active so we keep injecting). Worst case is a double-display
            // which the user can fix manually; missing data is worse.
            GearGoblin.DalamudServices.Log.Warning(
                ex, "CprDetection: probe threw, assuming CPR is not active.");
            return false;
        }
    }
}

internal static class DerivedStatFormatter
{
    // ── Critical Hit ────────────────────────────────────────────────────

    /// <summary>
    /// Compact: "20.8% · ×1.556 · +11.6% dmg" — chance, damage multiplier,
    /// and the contribution to overall damage (chance × (dmg-1)).
    /// </summary>
    public static string CritCompact(int crit, in LevelMod mod)
    {
        var rate = Formulas.CritRate(crit, mod);
        var dmg  = Formulas.CritDmg(crit, mod);
        var contribution = rate.DisplayValue * (dmg.DisplayValue - 1.0);
        return $"{rate.DisplayValue * 100:0.0}% · ×{dmg.DisplayValue:0.000} · +{contribution * 100:0.0}% dmg";
    }

    public static string CritChance(int crit, in LevelMod mod)
        => $"{Formulas.CritRate(crit, mod).DisplayValue * 100:0.0}%";

    public static string CritDamage(int crit, in LevelMod mod)
        => $"×{Formulas.CritDmg(crit, mod).DisplayValue:0.000}";

    public static string CritDamageIncrease(int crit, in LevelMod mod)
    {
        var rate = Formulas.CritRate(crit, mod);
        var dmg  = Formulas.CritDmg(crit, mod);
        var contribution = rate.DisplayValue * (dmg.DisplayValue - 1.0);
        return $"+{contribution * 100:0.0}% dmg";
    }

    // ── Determination ───────────────────────────────────────────────────

    /// <summary>
    /// Compact: "+11.3% dmg" — Det is one number, so compact == single value.
    /// </summary>
    public static string DetCompact(int det, in LevelMod mod)
        => $"+{Formulas.Determination(det, mod).DisplayValue * 100:0.0}% dmg";

    // ── Direct Hit Rate ─────────────────────────────────────────────────

    /// <summary>
    /// Compact: "23.5% · +5.9% dmg" — chance and overall damage contribution
    /// (chance × 0.25, since a DH deals +25% damage on hit).
    /// </summary>
    public static string DhCompact(int dh, in LevelMod mod)
    {
        var rate = Formulas.DirectHit(dh, mod);
        var contribution = rate.DisplayValue * 0.25;
        return $"{rate.DisplayValue * 100:0.0}% · +{contribution * 100:0.0}% dmg";
    }

    public static string DhChance(int dh, in LevelMod mod)
        => $"{Formulas.DirectHit(dh, mod).DisplayValue * 100:0.0}%";

    public static string DhDamageIncrease(int dh, in LevelMod mod)
    {
        var contribution = Formulas.DirectHit(dh, mod).DisplayValue * 0.25;
        return $"+{contribution * 100:0.0}% dmg";
    }

    // ── Tenacity ────────────────────────────────────────────────────────

    /// <summary>
    /// Compact: "+2.5% dmg · -2.5% taken" — tank-only stat with both
    /// offensive and defensive contribution rolled into one line.
    /// </summary>
    public static string TenacityCompact(int ten, in LevelMod mod)
    {
        var dmg = Formulas.TenacityDamage(ten, mod).DisplayValue;
        var mit = Formulas.TenacityMitigation(ten, mod).DisplayValue;
        return $"+{dmg * 100:0.0}% dmg · -{mit * 100:0.0}% taken";
    }

    // ── Piety ───────────────────────────────────────────────────────────

    /// <summary>
    /// Piety doesn't render a percentage; it renders flat MP/tick.
    /// </summary>
    public static string PietyMpPerTick(int pie, in LevelMod mod)
        => $"{(int)Formulas.Piety(pie, mod).DisplayValue} MP/tick";

    // ── Speed Damage ────────────────────────────────────────────────────

    /// <summary>
    /// Speed contributes a small damage bonus to GCD-tagged abilities.
    /// Shows as "+1.2% dmg" — usually small enough to not matter for
    /// substat-priority decisions but useful for completeness.
    /// </summary>
    public static string SpeedDamage(int speed, in LevelMod mod)
        => $"+{Formulas.SpeedDamage(speed, mod).DisplayValue * 100:0.0}% dmg";
}
