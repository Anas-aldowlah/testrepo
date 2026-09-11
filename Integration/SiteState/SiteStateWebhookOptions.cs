using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Options;

namespace YAGOT_2._0.Integration.SiteState;

public sealed class SiteStateWebhookOptions
{
    public const string SectionName = "YagotIntegration";
    public const int BodySizeLimitBytes = 64 * 1024;
    public const int ApprovedTimestampToleranceSeconds = 300;

    public int SiteId { get; set; }
    public string? WebhookKeyId { get; set; }
    public string? WebhookSecret { get; set; }
    public int TimestampToleranceSeconds { get; set; }
}

public sealed class SiteStateWebhookOptionsValidator :
    IValidateOptions<SiteStateWebhookOptions>
{
    private readonly IConfiguration _configuration;

    public SiteStateWebhookOptionsValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(
        string? name,
        SiteStateWebhookOptions options)
    {
        var failures = new List<string>();

        if (options.SiteId != SiteStateContractV1.SiteId)
        {
            failures.Add($"{SiteStateWebhookOptions.SectionName}:SiteId must be {SiteStateContractV1.SiteId}.");
        }

        if (string.IsNullOrWhiteSpace(options.WebhookKeyId) ||
            !string.Equals(
                options.WebhookKeyId,
                options.WebhookKeyId.Trim(),
                StringComparison.Ordinal))
        {
            failures.Add($"{SiteStateWebhookOptions.SectionName}:WebhookKeyId must be nonblank and must not contain surrounding whitespace.");
        }

        if (string.IsNullOrEmpty(options.WebhookSecret) ||
            Encoding.UTF8.GetByteCount(options.WebhookSecret) < 32)
        {
            failures.Add($"{SiteStateWebhookOptions.SectionName}:WebhookSecret must contain at least 32 UTF-8 bytes.");
        }
        else if (ReusesAnotherCredential(options.WebhookSecret))
        {
            failures.Add($"{SiteStateWebhookOptions.SectionName}:WebhookSecret must be distinct from database, encryption, and snapshot credentials.");
        }

        if (options.TimestampToleranceSeconds !=
            SiteStateWebhookOptions.ApprovedTimestampToleranceSeconds)
        {
            failures.Add($"{SiteStateWebhookOptions.SectionName}:TimestampToleranceSeconds must be {SiteStateWebhookOptions.ApprovedTimestampToleranceSeconds}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private bool ReusesAnotherCredential(string webhookSecret)
    {
        var configuredCredentials = new List<string?>
        {
            _configuration[$"{SiteStateWebhookOptions.SectionName}:SnapshotApiKey"],
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
            string.Equals(webhookSecret, candidate, StringComparison.Ordinal));
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
