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
}
