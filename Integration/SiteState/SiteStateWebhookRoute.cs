using Microsoft.AspNetCore.Http;

namespace YAGOT_2._0.Integration.SiteState;

public static class SiteStateWebhookRoute
{
    public const string AttributePattern = "api/integration/site-state";
    public const string Path = "/api/integration/site-state";

    public static bool Matches(PathString requestPath) =>
        requestPath.Equals(new PathString(Path));
}
