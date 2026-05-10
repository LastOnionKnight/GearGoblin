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

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public enum PlanMode { Casual, Raider }

[Serializable]
public class CachedBis
{
    public string Url { get; set; } = "";
    public string Json { get; set; } = ""; // raw payload, parsed on demand
    public DateTime FetchedAt { get; set; }
}
