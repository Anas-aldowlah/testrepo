

namespace YAGOT_2._0.Services.Integration;

internal interface ILocalSiteRuntimeStateInvalidator
{
    void ObserveCommittedRevision(long revision);
    void InvalidateForCommittedDelivery();
}
