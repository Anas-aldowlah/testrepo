using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UAParser;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public sealed class VisitQueueBackgroundService : BackgroundService
{
    private readonly IVisitBackgroundQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VisitQueueBackgroundService> _logger;

    public VisitQueueBackgroundService(
        IVisitBackgroundQueue queue,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<VisitQueueBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VisitQueueBackgroundService started.");

        var parser = Parser.GetDefault();

        await foreach (var item in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                var client = parser.Parse(item.UserAgent ?? string.Empty);
                var browser = client.UA.Family ?? string.Empty;
                string device;
                if (client.Device.Family != "Other")
                    device = client.Device.Family;
                else if (client.OS.Family.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                         client.OS.Family.Contains("iOS", StringComparison.OrdinalIgnoreCase))
                    device = "Mobile";
                else
                    device = "Desktop";

                var country = string.Empty;
                var governorate = string.Empty;
                var city = string.Empty;

                var ip = item.Ip?.Trim() ?? string.Empty;
                if (ip == "::1" || ip == "127.0.0.1")
                {
                    ip = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(ip))
                {
                    try
                    {
                        var httpClient = _httpClientFactory.CreateClient("IpWhoIs");
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, stoppingToken);

                        var geoResult = await httpClient.GetFromJsonAsync<IpWhoIsResponse>(
                            $"https://ipwho.is/{ip}",
                            linkedCts.Token);

                        if (geoResult != null && geoResult.Success)
                        {
                            country = geoResult.Country ?? string.Empty;
                            governorate = geoResult.Region ?? string.Empty;
                            city = geoResult.City ?? string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not resolve geolocation for IP {Ip}.", ip);
                    }
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NeondbContext>();

                var visit = new Visit
                {
                    Visitdate = item.TimestampUtc.AddHours(3),
                    Visitorname = string.IsNullOrWhiteSpace(item.VisitorName) ? "زائر" : item.VisitorName,
                    Country = country,
                    Governorate = governorate,
                    City = city,
                    Device = device,
                    Browser = browser
                };

                db.Visits.Add(visit);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing queued visit item for visitor {Visitor}.", item.VisitorName);
            }
        }

        _logger.LogInformation("VisitQueueBackgroundService stopping.");
    }
}
