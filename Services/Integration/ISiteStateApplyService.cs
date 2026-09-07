using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Services.Integration;

public interface ISiteStateApplyService
{
    Task<SiteStateApplyResult> ApplyAsync(
        SiteStateSnapshotV1 snapshot,
        SiteStateDeliveryContext? delivery = null,
        CancellationToken cancellationToken = default);
}
