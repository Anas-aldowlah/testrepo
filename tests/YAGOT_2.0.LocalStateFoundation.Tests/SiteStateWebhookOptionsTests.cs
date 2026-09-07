using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateWebhookOptionsTests
{
    [Fact]
    [Trait("Suite", "YAGOT02Helper")]
    public void ApprovedConfiguration_PassesValidation()
    {
        var validator = Validator();

        var result = validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("site")]
    [InlineData("key")]
    [InlineData("secret")]
    [InlineData("tolerance")]
    [InlineData("reused-encryption")]
    [InlineData("reused-database-password")]
    [InlineData("reused-snapshot")]
    [Trait("Suite", "YAGOT02Helper")]
    public void InvalidOrReusedConfiguration_FailsValidation(string variation)
    {
        var options = ValidOptions();
        var configuration = Configuration();
        switch (variation)
        {
            case "site":
                options.SiteId = 2;
                break;
            case "key":
                options.WebhookKeyId = " key-with-whitespace ";
                break;
            case "secret":
                options.WebhookSecret = "too-short";
                break;
            case "tolerance":
                options.TimestampToleranceSeconds = 301;
                break;
            case "reused-encryption":
                options.WebhookSecret = configuration["Encryption:Key"];
                break;
            case "reused-database-password":
                options.WebhookSecret = "database-password-marker-123456789";
                break;
            case "reused-snapshot":
                options.WebhookSecret = configuration[
                    "YagotIntegration:SnapshotApiKey"];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variation));
        }

        var result = new SiteStateWebhookOptionsValidator(configuration)
            .Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Helper")]
    public void TrackedDefaultsContainNoWebhookCredential()
    {
        var paths = Directory.EnumerateFiles(
            FindRepositoryRoot(),
            "appsettings*.json",
            SearchOption.TopDirectoryOnly);

        foreach (var path in paths)
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            Assert.DoesNotContain(
                "WebhookSecret",
                json,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "WebhookKeyId",
                json,
                StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("missing-key")]
    [InlineData("missing-secret")]
    [InlineData("short-secret")]
    [Trait("Suite", "YAGOT02Startup")]
    public async Task ProductionRegistration_InvalidCredential_FailsStartupWithoutDisclosure(
        string variation)
    {
        var values = ValidConfigurationValues();
        switch (variation)
        {
            case "missing-key":
                values["YagotIntegration:WebhookKeyId"] = null;
                break;
            case "missing-secret":
                values["YagotIntegration:WebhookSecret"] = null;
                break;
            case "short-secret":
                values["YagotIntegration:WebhookSecret"] =
                    "short-secret-marker";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variation));
        }

        var logs = new CapturingLogProvider();
        await using var app = BuildHost(values, logs);

        var exception = await Assert.ThrowsAnyAsync<OptionsValidationException>(
            () => app.StartAsync());
        var output = exception + Environment.NewLine +
                     string.Join(Environment.NewLine, logs.Messages);

        Assert.DoesNotContain(
            "startup-secret-marker-0123456789abcdef",
            output,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "short-secret-marker",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Startup")]
    public async Task ProductionRegistration_ExternalTestCredentials_AllowStartupWithoutDisclosure()
    {
        var logs = new CapturingLogProvider();
        await using var app = BuildHost(ValidConfigurationValues(), logs);

        await app.StartAsync();
        await app.StopAsync();

        Assert.DoesNotContain(
            "startup-secret-marker-0123456789abcdef",
            string.Join(Environment.NewLine, logs.Messages),
            StringComparison.Ordinal);
    }

    private static SiteStateWebhookOptionsValidator Validator() =>
        new(Configuration());

    private static SiteStateWebhookOptions ValidOptions() => new()
    {
        SiteId = 1,
        WebhookKeyId = "future-yagot-1",
        WebhookSecret = "webhook-secret-marker-12345678901234567890",
        TimestampToleranceSeconds = 300
    };

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] =
                    "encryption-key-marker-123456789012345678",
                ["YagotIntegration:SnapshotApiKey"] =
                    "snapshot-key-marker-12345678901234567890",
                ["ConnectionStrings:MYDB"] =
                    "Host=localhost;Database=test;Username=test;Password=database-password-marker-123456789"
            })
            .Build();

    private static Dictionary<string, string?> ValidConfigurationValues() => new()
    {
        ["YagotIntegration:SiteId"] = "1",
        ["YagotIntegration:WebhookKeyId"] = "startup-test-key",
        ["YagotIntegration:WebhookSecret"] =
            "startup-secret-marker-0123456789abcdef",
        ["YagotIntegration:TimestampToleranceSeconds"] = "300"
    };

    private static WebApplication BuildHost(
        Dictionary<string, string?> values,
        CapturingLogProvider logs)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Configuration.AddInMemoryCollection(values);
        builder.Services.AddSiteStateWebhook(builder.Configuration);
        return builder.Build();
    }

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
