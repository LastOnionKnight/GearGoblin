using System;

namespace GearGoblin.Services;

/// <summary>
/// All mutation methods on this interface persist immediately.
/// Callers should not call Save() after a mutation method; it is redundant.
/// Save() remains exposed for explicit no-op-mutation persistence scenarios (rare).
/// </summary>
public interface IConfigurationService
{
    Configuration Current { get; }
    void Save();

    void SetEnableNativeStatPanel(bool enabled);
    void SetForceDerivationsOverCpr(bool enabled);
    void SetCompactDerivationLayout(bool enabled);
    void SetEnableDerivedStatInjection(bool enabled);
    void SetShowCritDerivations(bool enabled);
    void SetShowDetDerivations(bool enabled);
    void SetShowDhDerivations(bool enabled);
    void SetShowSpeedDerivations(bool enabled);
    void SetShowTenacityRow(bool enabled);
    void SetShowPietyRow(bool enabled);
    void SetEnableVerboseInjectorLogging(bool enabled);

    // TODO Phase 1.6: add Changed event for mediator wiring
    // event Action<ConfigurationChange> Changed;
}
