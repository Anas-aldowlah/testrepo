using Microsoft.AspNetCore.Http;

namespace YAGOT_2._0.Integration.Capabilities;

public static class CapabilityWebhookRoute
{
    public const string AttributePattern = "api/integration/capabilities";
    public const string Path = "/api/integration/capabilities";

    public static bool Matches(PathString requestPath) =>
        requestPath.Equals(new PathString(Path));
}

public sealed record CapabilityWebhookResponse(
    bool Success,
    string Code,
    string? Outcome = null);
