using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Services.Integration;

namespace YAGOT_2._0.Core.Capabilities;

public static class CapabilityReconciliationServiceCollectionExtensions
{
    public static IServiceCollection AddCapabilityReconciliation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CapabilityReconciliationOptions>()
            .Bind(configuration.GetSection(CapabilityReconciliationOptions.SectionName));

        services.AddHttpClient<
                IControlPanelCapabilitySnapshotClient,
                ControlPanelCapabilitySnapshotClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<
                    IOptions<CapabilityReconciliationOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(options.ControlPanelBaseUrl))
                {
                    client.BaseAddress = new Uri(options.ControlPanelBaseUrl);
                }
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(provider =>
            {
                var env = provider.GetService<IHostEnvironment>();
                var handler = new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.None
                };

                if (env?.IsDevelopment() == true)
                {
                    handler.SslOptions.RemoteCertificateValidationCallback =
                        (sender, certificate, chain, errors) =>
                        {
                            if (errors == SslPolicyErrors.None)
                            {
                                return true;
                            }

                            if (certificate is X509Certificate2 cert &&
                                (string.Equals(cert.GetNameInfo(X509NameType.DnsName, false), "localhost", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(cert.Subject, "CN=localhost", StringComparison.OrdinalIgnoreCase)))
                            {
                                return true;
                            }

                            return false;
                        };
                }

                return handler;
            });

        services.AddScoped<ICapabilityReconciliationService, CapabilityReconciliationService>();
        services.AddHostedService<CapabilityReconciliationBackgroundService>();
        return services;
    }
}
