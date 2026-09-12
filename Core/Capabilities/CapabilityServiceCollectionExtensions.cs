using Microsoft.Extensions.DependencyInjection.Extensions;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Services.Integration;

namespace YAGOT_2._0.Core.Capabilities;

public static class CapabilityServiceCollectionExtensions
{
    public static IServiceCollection AddCapabilityFoundation(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        if (configuration is null) services.AddOptions<CapabilityIntegrationOptions>();
        else services.AddOptions<CapabilityIntegrationOptions>().Bind(configuration.GetSection(CapabilityIntegrationOptions.SectionName))
            .Validate(x => x.SiteId > 0, "CapabilityIntegration:SiteId must be positive.");
        services.TryAddSingleton<ICapabilityCatalog, CapabilityCatalog>();
        services.TryAddSingleton<LocalCapabilityRuntimeStateProvider>();
        services.TryAddSingleton<ICapabilityStateProvider>(sp => sp.GetRequiredService<LocalCapabilityRuntimeStateProvider>());
        services.TryAddSingleton<ILocalCapabilityRuntimeState>(sp => sp.GetRequiredService<LocalCapabilityRuntimeStateProvider>());
        services.TryAddSingleton<ICapabilityRuntimePublisher>(sp => sp.GetRequiredService<LocalCapabilityRuntimeStateProvider>());
        services.TryAddSingleton<ICapabilityEvaluator, CapabilityEvaluator>();
        services.TryAddSingleton<ICapabilitySnapshotV1JsonParser, CapabilitySnapshotV1JsonParser>();
        services.TryAddScoped<ICapabilityApplyService, CapabilityApplyService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CapabilityRuntimeInitializer>());
        return services;
    }
}
