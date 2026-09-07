using System.Security.Claims;
using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Services.Integration;

public interface ISiteAccessDecisionService
{
    Task<SiteAccessDecision> DecideAsync(
        SiteAccessSurface surface,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
