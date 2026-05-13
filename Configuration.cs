using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace GearGoblin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Per-character, per-job plans, keyed by ContentId then JobId.</summary>
    public Dictionary<ulong, Dictionary<uint, JobPlanData>> JobPlans { get; set; } = new();

    /// <summary>Cached Etro/XIVGear responses by URL, persisted to avoid redundant fetches.</summary>
    public Dictionary<string, CachedBis> BisCache { get; set; } = new();

    /// <summary>
    /// v0.4.0: enables the native AtkNode-injected stat panel inside the Character
    /// window (breakpoint hints, derived GCD, Materia Advisor section). When false,
    /// the standalone /goblin window remains the only UI surface. Default is true;
    /// existing configs upgrading to v0.4.0 will pick this up via the property
    /// initializer since the field is absent from their persisted JSON.
    /// </summary>
    public bool EnableNativeStatPanel { get; set; } = true;

    /// <summary>
    /// v0.4.5: GearGoblin now ships full CPR-equivalent derived stat injection
    /// (Crit Chance %, Crit Damage %, DH Chance %, Det Damage Increase %, etc.).
    /// When true, GG injects derived percentages alongside its breakpoint hints.
    /// Auto-disabled if CharacterPanelRefined is also installed and active, so
    /// the two plugins don't double-inject — unless the user explicitly toggles
    /// <see cref="ForceDerivationsOverCpr"/> on. Default true.
    /// </summary>
    public bool EnableDerivedStatInjection { get; set; } = true;

    /// <summary>
    /// v0.4.5: per-section visibility for derived stat rows. Each substat has its
    /// own toggle so users can dial back what gets shown without losing the
    /// breakpoint hint or the Materia Advisor (those remain controlled by
    /// <see cref="EnableNativeStatPanel"/>). All default true.
    /// </summary>
    public bool ShowCritDerivations  { get; set; } = true;
    public bool ShowDetDerivations   { get; set; } = true;
    public bool ShowDhDerivations    { get; set; } = true;
    public bool ShowSpeedDerivations { get; set; } = true;
    public bool ShowTenacityRow      { get; set; } = true;  // tank-only; ignored on other jobs
    public bool ShowPietyRow         { get; set; } = true;  // healer-only; ignored on other jobs

    /// <summary>
    /// v0.4.5: when true, GG will inject its derivations even if
    /// CharacterPanelRefined is detected as active. The user gets double-display
    /// in exchange for explicit override. Off by default — CPR users typically
    /// want one source of truth.
    /// </summary>
    public bool ForceDerivationsOverCpr { get; set; } = false;

    /// <summary>
    /// v0.4.5: if true, derived stats render in compact one-line form
    /// (<c>20.8% / ×1.556 / +11.6% dmg</c>) rather than CPR-style one-row-per-stat.
    /// Saves ~6 rows of vertical real estate at the cost of slightly denser text.
    /// Off by default to match CPR's familiar layout.
    /// </summary>
    public bool CompactDerivationLayout { get; set; } = false;

    /// <summary>
    /// v0.4.6: when true, the StatusPanelInjector emits Verbose-level log
    /// lines on every Materia Advisor update tick (rec count, empty-state
    /// flag, audit counts). Default true for v0.4.6 to give us instant
    /// observability after the v0.4.5 advisor-visibility bug — users can
    /// toggle off later if log volume becomes a concern.
    /// </summary>
    public bool EnableVerboseInjectorLogging { get; set; } = true;

    [NonSerialized] private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi) => pluginInterface = pi;

    public void Save() => pluginInterface?.SavePluginConfig(this);
}

[Serializable]
public class JobPlanData
{
    public PlanMode Mode { get; set; } = PlanMode.Casual;

    /// <summary>Etro or XIVGear URL when Mode == Raider.</summary>
    public string? BisUrl { get; set; }

    /// <summary>Casual preset key when Mode == Casual.</summary>
    public string? CasualPresetKey { get; set; }

    /// <summary>
    /// v0.4.7: when Mode == Imported, the raw JSON body of the
    /// <c>GG-PLAN:v1:</c> payload that was last imported via
    /// <c>/goblinimport</c>. Stored as the decoded JSON (not the
    /// prefix+base64 wire form) so it's inspectable in the config
    /// file and survives wire-format schema bumps that only touch
    /// the prefix or encoding. Null in all other modes.
    /// </summary>
    public string? ImportedPlanJson { get; set; }

    /// <summary>
    /// v0.4.7: timestamp the imported plan was applied. Surfaced in
    /// the Plan tab's "Active Plan" section so users can tell whether
    /// the plan they're looking at is fresh or weeks-stale relative
    /// to whatever gear they have equipped right now.
    /// </summary>
    public DateTime? ImportedAt { get; set; }

    /// <summary>
    /// v0.4.7: per-meld completion flags for the imported plan's
    /// checklist UI. Indices align with <c>PlanMeldV1[].Melds</c>
    /// inside <see cref="ImportedPlanJson"/>. Persisted so tick state
    /// survives plugin reloads. Reset to all-false on each fresh
    /// import; left untouched on other mode changes so re-selecting
    /// Imported restores the previous tick state.
    /// </summary>
    public List<bool> MeldCompletion { get; set; } = new();

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public enum PlanMode { Casual, Raider, Imported }

[Serializable]
public class CachedBis
{
    public string Url { get; set; } = "";
    public string Json { get; set; } = ""; // raw payload, parsed on demand
    public DateTime FetchedAt { get; set; }
}
