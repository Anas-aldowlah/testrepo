using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("YAGOT_2.0.LocalStateFoundation.Tests")]

namespace YAGOT_2._0.Services.Integration;

internal interface ILocalSiteRuntimeStateInvalidator
{
    void ObserveCommittedRevision(long revision);
    void InvalidateForCommittedDelivery();
}
