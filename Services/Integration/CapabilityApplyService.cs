using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Integration;

public sealed class CapabilityApplyService(
    IServiceScopeFactory scopeFactory,
    ICapabilityCatalog catalog,
    IOptions<CapabilityIntegrationOptions> options,
    ICapabilityRuntimePublisher runtime,
    TimeProvider timeProvider,
    ILogger<CapabilityApplyService> logger) : ICapabilityApplyService
{
    private const int AdvisoryLockNamespace = 0x59414350; // ASCII "YACP"

    public async Task<CapabilityApplyResult> ApplyAsync(CapabilitySnapshotV1 snapshot, CapabilityDeliveryContext? delivery = null, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            snapshot.Validate(catalog, options.Value.SiteId);
            delivery?.Validate();
        }
        catch (Exception exception) when (exception is CapabilityContractValidationException or ArgumentException)
        {
            runtime.Report(CapabilityHealthStatus.InvalidSnapshot);
            return new(CapabilityApplyOutcome.Rejected, CapabilityHealthStatus.InvalidSnapshot, Diagnostic: exception.Message);
        }

        var hash = CapabilitySnapshotCanonicalizer.Hash(snapshot);
        var canonicalJson = CapabilitySnapshotCanonicalizer.ToCanonicalJson(snapshot);
        try
        {
            await using var strategyScope = scopeFactory.CreateAsyncScope();
            var strategyContext = strategyScope.ServiceProvider.GetRequiredService<NeondbContext>();
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            var attempt = await strategy.ExecuteAsync(async token =>
            {
                await using var attemptScope = scopeFactory.CreateAsyncScope();
                var context = attemptScope.ServiceProvider.GetRequiredService<NeondbContext>();
                return await ApplyAttemptAsync(context, snapshot, delivery, hash, canonicalJson, token);
            }, cancellationToken);

            if (attempt.Publish) runtime.Publish(snapshot);
            else runtime.Report(attempt.Result.Health);
            return attempt.Result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            runtime.Report(CapabilityHealthStatus.StorageFailure);
            logger.LogWarning(exception, "Capability snapshot storage failed for SiteId {SiteId}, revision {Revision}.", snapshot.SiteId, snapshot.Revision);
            return new(CapabilityApplyOutcome.Rejected, CapabilityHealthStatus.StorageFailure, Diagnostic: "Capability snapshot storage failed.");
        }
    }

    private async Task<AttemptResult> ApplyAttemptAsync(NeondbContext context, CapabilitySnapshotV1 snapshot,
        CapabilityDeliveryContext? delivery, byte[] hash, string canonicalJson, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockNamespace}, {snapshot.SiteId})", cancellationToken);

        if (delivery is not null)
        {
            var prior = await context.CapabilityEventReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.DeliveryId == delivery.DeliveryId, cancellationToken);
            if (prior is not null)
            {
                var same = prior.PayloadSha256.Length == hash.Length && CryptographicOperations.FixedTimeEquals(prior.PayloadSha256, hash);
                var publish = false;
                if (same)
                {
                    var durable = await context.LocalCapabilitySnapshots.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.SiteId == snapshot.SiteId, cancellationToken);
                    publish = durable is not null && durable.Revision == snapshot.Revision &&
                              durable.PayloadSha256.Length == hash.Length &&
                              CryptographicOperations.FixedTimeEquals(durable.PayloadSha256, hash);
                }
                await transaction.CommitAsync(cancellationToken);
                var original = ParseDecision(prior.Decision);
                return new(new(same ? CapabilityApplyOutcome.DuplicateDelivery : CapabilityApplyOutcome.Conflict,
                    same ? CapabilityHealthStatus.Healthy : CapabilityHealthStatus.RevisionConflict, original), publish);
            }
        }

        var now = timeProvider.GetUtcNow();
        var current = await context.LocalCapabilitySnapshots.SingleOrDefaultAsync(x => x.SiteId == snapshot.SiteId, cancellationToken);
        var decision = CapabilityRevisionDecider.Decide(snapshot.Revision, hash, current?.Revision, current?.PayloadSha256 ?? []);
        var health = decision switch
        {
            CapabilityReceiptDecision.Stale => CapabilityHealthStatus.StaleRemote,
            CapabilityReceiptDecision.EqualConflict => CapabilityHealthStatus.RevisionConflict,
            _ => CapabilityHealthStatus.Healthy
        };

        if (decision == CapabilityReceiptDecision.Applied)
        {
            current ??= new LocalCapabilitySnapshot { SiteId = snapshot.SiteId };
            if (context.Entry(current).State == EntityState.Detached) context.LocalCapabilitySnapshots.Add(current);
            current.ContractVersion = snapshot.ContractVersion;
            current.CatalogVersion = snapshot.CatalogVersion;
            current.Revision = snapshot.Revision;
            current.SnapshotJson = canonicalJson;
            current.PayloadSha256 = hash.ToArray();
            current.GeneratedAtUtc = snapshot.GeneratedAtUtc;
            current.EffectiveAtUtc = snapshot.EffectiveAtUtc;
            current.AppliedAtUtc = now;
        }

        if (delivery is not null)
            context.CapabilityEventReceipts.Add(new CapabilityEventReceipt { DeliveryId = delivery.DeliveryId, SiteId = snapshot.SiteId, Revision = snapshot.Revision, PayloadSha256 = hash.ToArray(), Decision = decision.ToString(), RecordedAtUtc = now });

        var checkpoint = await context.CapabilitySyncCheckpoints.SingleOrDefaultAsync(x => x.SiteId == snapshot.SiteId, cancellationToken);
        checkpoint ??= new CapabilitySyncCheckpoint { SiteId = snapshot.SiteId };
        if (context.Entry(checkpoint).State == EntityState.Detached) context.CapabilitySyncCheckpoints.Add(checkpoint);
        checkpoint.LastAttemptedRevision = snapshot.Revision;
        checkpoint.LastObservedRemoteRevision = Math.Max(checkpoint.LastObservedRemoteRevision ?? 0, snapshot.Revision);
        checkpoint.LastAttemptAtUtc = now;
        checkpoint.Health = health.ToString();
        checkpoint.LastFailureCategory = health == CapabilityHealthStatus.Healthy ? null : health.ToString();
        checkpoint.UpdatedAtUtc = now;
        if (decision == CapabilityReceiptDecision.Applied)
        {
            checkpoint.LastSuccessfullyAppliedRevision = snapshot.Revision;
            checkpoint.LastSuccessAtUtc = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(new(ToOutcome(decision), health), decision is CapabilityReceiptDecision.Applied or CapabilityReceiptDecision.Equal);
    }

    private static CapabilityApplyOutcome ToOutcome(CapabilityReceiptDecision decision) => decision switch
    {
        CapabilityReceiptDecision.Applied => CapabilityApplyOutcome.Applied,
        CapabilityReceiptDecision.Equal => CapabilityApplyOutcome.Equal,
        CapabilityReceiptDecision.Stale => CapabilityApplyOutcome.Stale,
        CapabilityReceiptDecision.EqualConflict => CapabilityApplyOutcome.EqualConflict,
        _ => throw new ArgumentOutOfRangeException(nameof(decision))
    };

    private static CapabilityReceiptDecision ParseDecision(string value) => Enum.TryParse<CapabilityReceiptDecision>(value, false, out var parsed)
        ? parsed : throw new InvalidOperationException($"Stored capability receipt decision '{value}' is invalid.");

    private sealed record AttemptResult(CapabilityApplyResult Result, bool Publish);
}
