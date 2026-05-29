using System;

namespace GearGoblin.Services;

public interface IConfigurationService
{
    Configuration Current { get; }
    void Save();

    // TODO Phase 1.6: add Changed event for mediator wiring
    // event Action<ConfigurationChange> Changed;
}
