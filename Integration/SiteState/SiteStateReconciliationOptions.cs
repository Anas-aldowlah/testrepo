using System.Data.Common;
using Microsoft.Extensions.Options;

namespace YAGOT_2._0.Integration.SiteState;

public sealed class SiteStateReconciliationOptions
{
    public const string SectionName = "YagotIntegration";
    public const int ApprovedIntervalMinutes = 30;
    public const int ApprovedHttpTimeoutSeconds = 10;
    public const int ResponseSizeLimitBytes = 64 * 1024;

    public int SiteId { get; set; }
    public string? ControlPanelBaseUrl { get; set; }
    public string? SnapshotApiKey { get; set; }
    public int ReconciliationIntervalMinutes { get; set; }
    public int SnapshotHttpTimeoutSeconds { get; set; }
}

public sealed class SiteStateReconciliationOptionsValidator :
    IValidateOptions<SiteStateReconciliationOptions>
{
    private readonly IConfiguration _configuration;

    public SiteStateReconciliationOptionsValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(
        string? name,
        SiteStateReconciliationOptions options)
    {
        var failures = new List<string>();

        if (options.SiteId != SiteStateContractV1.SiteId)
        {
            failures.Add($"{SiteStateReconciliationOptions.SectionName}:SiteId must be {SiteStateContractV1.SiteId}.");
        }

        if (!IsValidControlPanelOrigin(options.ControlPanelBaseUrl))
        {
            failures.Add(
                $"{SiteStateReconciliationOptions.SectionName}:ControlPanelBaseUrl must be an absolute HTTPS origin without userinfo, query, fragment, or path.");
        }

        if (string.IsNullOrWhiteSpace(options.SnapshotApiKey) ||
            !string.Equals(
                options.SnapshotApiKey,
                options.SnapshotApiKey.Trim(),
                StringComparison.Ordinal))
        {
            failures.Add(
                $"{SiteStateReconciliationOptions.SectionName}:SnapshotApiKey must be supplied by secure configuration without surrounding whitespace.");
        }
        else if (ReusesAnotherCredential(options.SnapshotApiKey))
        {
            failures.Add(
                $"{SiteStateReconciliationOptions.SectionName}:SnapshotApiKey must be distinct from webhook, database, and encryption credentials.");
        }

        if (options.ReconciliationIntervalMinutes !=
            SiteStateReconciliationOptions.ApprovedIntervalMinutes)
        {
            failures.Add(
                $"{SiteStateReconciliationOptions.SectionName}:ReconciliationIntervalMinutes must be {SiteStateReconciliationOptions.ApprovedIntervalMinutes}.");
        }

        if (options.SnapshotHttpTimeoutSeconds !=
            SiteStateReconciliationOptions.ApprovedHttpTimeoutSeconds)
        {
            failures.Add(
                $"{SiteStateReconciliationOptions.SectionName}:SnapshotHttpTimeoutSeconds must be {SiteStateReconciliationOptions.ApprovedHttpTimeoutSeconds}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidControlPanelOrigin(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return uri.AbsolutePath == "/";
    }

    private bool ReusesAnotherCredential(string snapshotApiKey)
    {
        var configuredCredentials = new List<string?>
        {
            _configuration[$"{SiteStateReconciliationOptions.SectionName}:WebhookSecret"],
            _configuration["Encryption:Key"],
            _configuration["Encryption:IV"]
        };

        foreach (var connectionString in _configuration
                     .GetSection("ConnectionStrings")
                     .GetChildren()
                     .Select(section => section.Value))
        {
            configuredCredentials.Add(connectionString);
            configuredCredentials.Add(ReadPassword(connectionString));
        }

        return configuredCredentials.Any(candidate =>
            !string.IsNullOrEmpty(candidate) &&
            string.Equals(snapshotApiKey, candidate, StringComparison.Ordinal));
    }

    private static string? ReadPassword(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            foreach (var key in new[] { "Password", "Pwd" })
            {
                if (builder.TryGetValue(key, out var value))
                {
                    return Convert.ToString(value);
                }
            }
        }
        catch (ArgumentException)
        {
            // Another configuration validator owns malformed connection strings.
        }

        return null;
    }
}
