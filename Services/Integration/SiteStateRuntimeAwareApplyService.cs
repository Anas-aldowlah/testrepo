using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Services.Integration;

internal sealed class SiteStateRuntimeAwareApplyService(
    ISiteStateApplyService writer,
    ILocalSiteRuntimeStateInvalidator invalidator,
    ILogger<SiteStateRuntimeAwareApplyService> logger) : ISiteStateApplyService
{
    public async Task<SiteStateApplyResult> ApplyAsync(
        SiteStateSnapshotV1 snapshot,
        SiteStateDeliveryContext? delivery = null,
        CancellationToken cancellationToken = default)
    {
        var result = await writer.ApplyAsync(snapshot, delivery, cancellationToken);
        // Outside the writer's transaction AND execution strategy. Cache failure cannot
        // change a durable outcome, and no incoming snapshot is published to memory.
        try
        {
            if (result.Outcome is SiteStateApplyOutcome.Applied or SiteStateApplyOutcome.Equal)
            {
                invalidator.ObserveCommittedRevision(snapshot.Revision);
            }
            else if (result.Outcome == SiteStateApplyOutcome.DuplicateDelivery &&
                     result.OriginalDeliveryDecision == SiteStateReceiptDecision.Applied)
            {
                invalidator.InvalidateForCommittedDelivery();
            }
        }
        catch (Exception)
        {
            // Even a failing logging sink must not report a committed write as failed.
            try
            {
                logger.LogWarning("Local site-state cache notification failed; category NotificationFailure.");
            }
            catch (Exception) { }
        }

        return result;
    }
}
