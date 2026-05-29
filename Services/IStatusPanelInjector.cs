using System;

namespace GearGoblin.Services;

public interface IStatusPanelInjector : IDisposable
{
    StatusPanelInjector.DiagnosticSnapshot GetDiagnostics();
    void ForceReinject();
}
