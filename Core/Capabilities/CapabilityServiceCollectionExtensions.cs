using Microsoft.Extensions.DependencyInjection.Extensions;

namespace YAGOT_2._0.Core.Capabilities;

public static class CapabilityServiceCollectionExtensions
{
    public static IServiceCollection AddCapabilityFoundation(this IServiceCollection services)
    {
        services.TryAddSingleton<ICapabilityCatalog, CapabilityCatalog>();
        services.TryAddSingleton<ICapabilityStateProvider, CurrentApplicationCapabilityStateProvider>();
        services.TryAddSingleton<ICapabilityEvaluator, CapabilityEvaluator>();
        return services;
    }
}
