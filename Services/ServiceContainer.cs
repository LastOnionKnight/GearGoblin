using Microsoft.Extensions.DependencyInjection;
using System;

namespace GearGoblin.Services;

public static class ServiceContainer
{
    public static IServiceProvider CreateProvider(Plugin plugin)
    {
        var services = new ServiceCollection();

        // Singletons
        services.AddSingleton(plugin); // Inject the plugin root if needed
        services.AddSingleton<IInventoryReader, InventoryReader>();
        services.AddSingleton<IGearsetExporter, GearsetExporter>();

        return services.BuildServiceProvider();
    }
}
