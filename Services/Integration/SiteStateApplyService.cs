using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Integration;

public sealed class SiteStateApplyService : ISiteStateApplyService
{
    // ASCII "YAGO". The second advisory-lock key is the configured SiteId.
    private const int AdvisoryLockNamespace = 0x5941474F;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public SiteStateApplyService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    public async Task<SiteStateApplyResult> ApplyAsync(
        SiteStateSnapshotV1 snapshot,
        SiteStateDeliveryContext? delivery = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        delivery?.Validate();

        await using var strategyScope = _scopeFactory.CreateAsyncScope();
        var strategyContext = strategyScope.ServiceProvider
            .GetRequiredService<NeondbContext>();
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async attemptCancellationToken =>
        {
            await using var attemptScope = _scopeFactory.CreateAsyncScope();
            var attemptContext = attemptScope.ServiceProvider
                .GetRequiredService<NeondbContext>();

            return await ApplyAttemptAsync(
                attemptContext,
                snapshot,
                delivery,
                attemptCancellationToken);
        }, cancellationToken);
    }

    private async Task<SiteStateApplyResult> ApplyAttemptAsync(
        NeondbContext context,
        SiteStateSnapshotV1 snapshot,
        SiteStateDeliveryContext? delivery,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({AdvisoryLockNamespace}, {snapshot.SiteId})",
            cancellationToken);

        if (delivery is not null)
        {
            var existingReceipt = await context.SiteStateEventReceipts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.DeliveryId == delivery.DeliveryId,
                    cancellationToken);

            if (existingReceipt is not null)
            {
                var samePayload = existingReceipt.PayloadSha256.Length ==
                                  delivery.PayloadSha256.Length &&
                                  CryptographicOperations.FixedTimeEquals(
                                      existingReceipt.PayloadSha256,
                                      delivery.PayloadSha256);

                await transaction.CommitAsync(cancellationToken);

                return samePayload
                    ? new SiteStateApplyResult(
                        SiteStateApplyOutcome.DuplicateDelivery,
                        ParseDecision(existingReceipt.Decision))
                    : new SiteStateApplyResult(
                        SiteStateApplyOutcome.DeliveryIdPayloadConflict,
                        ParseDecision(existingReceipt.Decision));
            }
        }

        var current = await context.LocalSiteStateSnapshots
            .SingleOrDefaultAsync(
                candidate => candidate.SiteId == snapshot.SiteId,
                cancellationToken);

        var decision = Decide(snapshot, current);

        if (decision == SiteStateReceiptDecision.Applied)
        {
            if (current is null)
            {
                context.LocalSiteStateSnapshots.Add(
                    LocalSiteStateSnapshot.FromContract(snapshot));
            }
            else
            {
                current.Apply(snapshot);
            }
        }

        if (delivery is not null)
        {
            context.SiteStateEventReceipts.Add(new SiteStateEventReceipt
            {
                DeliveryId = delivery.DeliveryId,
                SiteId = snapshot.SiteId,
                Revision = snapshot.Revision,
                PayloadSha256 = delivery.PayloadSha256.ToArray(),
                Decision = decision.ToString(),
                RecordedAtUtc = _timeProvider.GetUtcNow()
            });
        }

        if (decision == SiteStateReceiptDecision.Applied || delivery is not null)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new SiteStateApplyResult(ToOutcome(decision));
    }

    private static SiteStateReceiptDecision Decide(
        SiteStateSnapshotV1 incoming,
        LocalSiteStateSnapshot? current)
    {
        if (current is null || incoming.Revision > current.Revision)
        {
            return SiteStateReceiptDecision.Applied;
        }

        if (incoming.Revision < current.Revision)
        {
            return SiteStateReceiptDecision.Stale;
        }

        return incoming.LogicallyEquals(current.ToContract())
            ? SiteStateReceiptDecision.Equal
            : SiteStateReceiptDecision.EqualConflict;
    }

    private static SiteStateApplyOutcome ToOutcome(SiteStateReceiptDecision decision) =>
        decision switch
        {
            SiteStateReceiptDecision.Applied => SiteStateApplyOutcome.Applied,
            SiteStateReceiptDecision.Equal => SiteStateApplyOutcome.Equal,
            SiteStateReceiptDecision.EqualConflict => SiteStateApplyOutcome.EqualConflict,
            SiteStateReceiptDecision.Stale => SiteStateApplyOutcome.Stale,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, null)
        };

    private static SiteStateReceiptDecision ParseDecision(string decision) =>
        Enum.TryParse<SiteStateReceiptDecision>(decision, ignoreCase: false, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Stored site-state receipt decision '{decision}' is invalid.");
}
