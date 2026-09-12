using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Integration;

internal sealed class CapabilityRuntimeInitializer(
    IServiceScopeFactory scopeFactory,
    ICapabilitySnapshotV1JsonParser parser,
    ICapabilityRuntimePublisher runtime,
    IOptions<CapabilityIntegrationOptions> options,
    ILogger<CapabilityRuntimeInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<NeondbContext>();
            var stored = await context.LocalCapabilitySnapshots.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SiteId == options.Value.SiteId, stoppingToken);
            if (stored is null) { runtime.Report(CapabilityHealthStatus.NoSnapshot); return; }
            var parsed = parser.Parse(System.Text.Encoding.UTF8.GetBytes(stored.SnapshotJson));
            if (parsed.Snapshot is null) { runtime.Report(CapabilityHealthStatus.InvalidSnapshot); return; }
            var hash = CapabilitySnapshotCanonicalizer.Hash(parsed.Snapshot);
            if (stored.PayloadSha256.Length != hash.Length || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(stored.PayloadSha256, hash))
            {
                runtime.Report(CapabilityHealthStatus.InvalidSnapshot); return;
            }
            runtime.Publish(parsed.Snapshot);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            runtime.Report(CapabilityHealthStatus.StorageFailure);
            logger.LogWarning(exception, "Capability runtime initialization is degraded; permissive baseline remains active.");
        }
    }
}
