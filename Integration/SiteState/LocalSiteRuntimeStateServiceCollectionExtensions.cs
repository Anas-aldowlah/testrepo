using Microsoft.Extensions.DependencyInjection.Extensions;
using YAGOT_2._0.Services.Integration;

namespace YAGOT_2._0.Integration.SiteState;

public static class LocalSiteRuntimeStateServiceCollectionExtensions
{
    public static IServiceCollection AddLocalSiteRuntimeState(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddLogging();
        services.TryAddScoped<ILocalSiteStateReader, LocalSiteStateReader>();
        services.TryAddSingleton<LocalSiteRuntimeStateProvider>();
        services.TryAddSingleton<ILocalSiteRuntimeStateProvider>(provider =>
            provider.GetRequiredService<LocalSiteRuntimeStateProvider>());
        services.TryAddSingleton<ILocalSiteRuntimeStateInvalidator>(provider =>
            provider.GetRequiredService<LocalSiteRuntimeStateProvider>());

        // The concrete writer is deliberately NOT registered as a resolvable service.
        services.RemoveAll<SiteStateApplyService>();
        services.RemoveAll<ISiteStateApplyService>();
        services.AddScoped<ISiteStateApplyService>(provider =>
            new SiteStateRuntimeAwareApplyService(
                new SiteStateApplyService(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    provider.GetRequiredService<TimeProvider>()),
                provider.GetRequiredService<ILocalSiteRuntimeStateInvalidator>(),
                provider.GetRequiredService<ILogger<SiteStateRuntimeAwareApplyService>>()));
        return services;
    }
}
