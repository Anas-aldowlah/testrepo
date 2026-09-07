using System.Text;
using Microsoft.Extensions.Configuration;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateReconciliationOptionsTests
{
    [Fact]
    public void ApprovedConfiguration_PassesValidation()
    {
        var configuration = Configuration();
        var result = new SiteStateReconciliationOptionsValidator(configuration)
            .Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("userinfo")]
    [InlineData("path")]
    [InlineData("query")]
    [InlineData("fragment")]
    [InlineData("site")]
    [InlineData("interval")]
    [InlineData("timeout")]
    [InlineData("missing-key")]
    [InlineData("reused-webhook")]
    [InlineData("reused-encryption")]
    [InlineData("reused-database-password")]
    public void InvalidOriginIdentityPolicyOrCredential_IsRejected(
        string variation)
    {
        var configuration = Configuration();
        var options = ValidOptions();
        switch (variation)
        {
            case "http":
                options.ControlPanelBaseUrl = "http://control-panel.test/";
                break;
            case "userinfo":
                options.ControlPanelBaseUrl = "https://user@control-panel.test/";
                break;
            case "path":
                options.ControlPanelBaseUrl = "https://control-panel.test/base/";
                break;
            case "query":
                options.ControlPanelBaseUrl = "https://control-panel.test/?x=1";
                break;
            case "fragment":
                options.ControlPanelBaseUrl = "https://control-panel.test/#x";
                break;
            case "site":
                options.SiteId = 2;
                break;
            case "interval":
                options.ReconciliationIntervalMinutes = 29;
                break;
            case "timeout":
                options.SnapshotHttpTimeoutSeconds = 11;
                break;
            case "missing-key":
                options.SnapshotApiKey = null;
                break;
            case "reused-webhook":
                options.SnapshotApiKey = configuration[
                    "YagotIntegration:WebhookSecret"];
                break;
            case "reused-encryption":
                options.SnapshotApiKey = configuration["Encryption:Key"];
                break;
            case "reused-database-password":
                options.SnapshotApiKey = "database-password-marker";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variation));
        }

        var result = new SiteStateReconciliationOptionsValidator(configuration)
            .Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void TrackedSettingsContainNoSnapshotCredentialOrUnverifiedOrigin()
    {
        var root = FindRepositoryRoot();
        foreach (var path in Directory.EnumerateFiles(
                     root,
                     "appsettings*.json",
                     SearchOption.TopDirectoryOnly))
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            Assert.DoesNotContain("SnapshotApiKey", json, StringComparison.Ordinal);
            Assert.DoesNotContain("ControlPanelBaseUrl", json, StringComparison.Ordinal);
        }
    }

    private static SiteStateReconciliationOptions ValidOptions() => new()
    {
        SiteId = 1,
        ControlPanelBaseUrl = "https://control-panel.test/",
        SnapshotApiKey = "unique-snapshot-key",
        ReconciliationIntervalMinutes = 30,
        SnapshotHttpTimeoutSeconds = 10
    };

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["YagotIntegration:WebhookSecret"] = "unique-webhook-secret",
                ["Encryption:Key"] = "unique-encryption-key",
                ["Encryption:IV"] = "unique-encryption-iv",
                ["ConnectionStrings:MYDB"] =
                    "Host=localhost;Database=test;Username=test;Password=database-password-marker"
            })
            .Build();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "YAGOT_2.0.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException(
            "Could not locate the repository root.");
    }
}
