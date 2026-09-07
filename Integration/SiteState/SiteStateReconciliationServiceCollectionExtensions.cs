using Microsoft.Extensions.Options;
using YAGOT_2._0.Services.Integration;

namespace YAGOT_2._0.Integration.SiteState;

public static class SiteStateReconciliationServiceCollectionExtensions
{
    public static IServiceCollection AddSiteStateReconciliation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<
            IValidateOptions<SiteStateReconciliationOptions>,
            SiteStateReconciliationOptionsValidator>();
        services.AddOptions<SiteStateReconciliationOptions>()
            .Bind(configuration.GetSection(
                SiteStateReconciliationOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<
                IControlPanelSnapshotClient,
                ControlPanelSnapshotClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<
                    IOptions<SiteStateReconciliationOptions>>().Value;
                client.BaseAddress = new Uri(options.ControlPanelBaseUrl!);
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = System.Net.DecompressionMethods.None
            });

        services.AddSingleton<ISiteStateReconciliationPolicy,
            SiteStateReconciliationPolicy>();
        services.AddScoped<ISiteStateSyncDiagnosticsStore,
            SiteStateSyncDiagnosticsStore>();
        services.AddScoped<ISiteStateReconciliationService,
            SiteStateReconciliationService>();
        services.AddSingleton<ISiteStateReconciliationCoordinator,
            SiteStateReconciliationCoordinator>();
        services.AddHostedService<SiteStateReconciliationBackgroundService>();
        return services;
    }
}
