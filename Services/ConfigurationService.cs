using System;

namespace GearGoblin.Services;

public sealed class ConfigurationService : IConfigurationService
{
    public Configuration Current { get; }

    public ConfigurationService(Plugin plugin)
    {
        this.Current = plugin.Configuration;
    }

    public void Save()
    {
        Current.Save();
    }

    public void SetEnableNativeStatPanel(bool enabled)
    {
        Current.EnableNativeStatPanel = enabled;
        Save();
    }

    public void SetForceDerivationsOverCpr(bool enabled)
    {
        Current.ForceDerivationsOverCpr = enabled;
        Save();
    }

    public void SetCompactDerivationLayout(bool enabled)
    {
        Current.CompactDerivationLayout = enabled;
        Save();
    }

    public void SetEnableDerivedStatInjection(bool enabled)
    {
        Current.EnableDerivedStatInjection = enabled;
        Save();
    }

    public void SetShowCritDerivations(bool enabled)
    {
        Current.ShowCritDerivations = enabled;
        Save();
    }

    public void SetShowDetDerivations(bool enabled)
    {
        Current.ShowDetDerivations = enabled;
        Save();
    }

    public void SetShowDhDerivations(bool enabled)
    {
        Current.ShowDhDerivations = enabled;
        Save();
    }

    public void SetShowSpeedDerivations(bool enabled)
    {
        Current.ShowSpeedDerivations = enabled;
        Save();
    }

    public void SetShowTenacityRow(bool enabled)
    {
        Current.ShowTenacityRow = enabled;
        Save();
    }

    public void SetShowPietyRow(bool enabled)
    {
        Current.ShowPietyRow = enabled;
        Save();
    }

    public void SetEnableVerboseInjectorLogging(bool enabled)
    {
        Current.EnableVerboseInjectorLogging = enabled;
        Save();
    }
}
