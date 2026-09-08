using System.Net;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class Yagot07SecurityBoundaryTests
{
    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task RequestInputCannotSpoofLocalModeRevisionOrMaintenanceMetadata()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(
            1, SiteStateContractV1.Offline, siteName: "Authoritative Local Site"));

        using var spoof = new HttpRequestMessage(
            HttpMethod.Get,
            "/_yagot07/storefront?mode=Online&revision=999&SiteName=UNTRUSTED_QUERY");
        spoof.Headers.Accept.ParseAdd("application/json");
        spoof.Headers.TryAddWithoutValidation("X-Site-Mode", "Online");
        spoof.Headers.TryAddWithoutValidation("X-Site-Revision", "999");
        using var denied = await host.Client.SendAsync(spoof);
        await Yagot07HttpAssertions.HasJsonCodeAsync(
            denied, HttpStatusCode.ServiceUnavailable, "site_offline");
        var durableAfterSpoof = await host.ReadDatabaseAsync();
        Assert.Equal(1, durableAfterSpoof.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Offline, durableAfterSpoof.Snapshot.Mode);
        Assert.Equal(0, durableAfterSpoof.ReceiptCount);

        using var close = await host.Client.GetAsync(
            "/DirectiveDevClose/close?SiteName=UNTRUSTED_QUERY&Url=https%3A%2F%2Funtrusted.example");
        var closeBody = await close.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Contains("Authoritative Local Site", closeBody, StringComparison.Ordinal);
        Assert.DoesNotContain("UNTRUSTED_QUERY", closeBody, StringComparison.Ordinal);
        Assert.DoesNotContain("untrusted.example", closeBody, StringComparison.Ordinal);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task IntegrationResponsesAndRedirectsDoNotExposeCredentialsOrPayloadMarkers()
    {
        Assert.NotEqual(
            SiteStateWebhookAuthenticationTests.TestSecret,
            Yagot07EndToEndHost.SnapshotApiKey);
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        var marker = "YAGOT07_SENSITIVE_PAYLOAD_MARKER";
        var snapshot = Yagot07EndToEndHost.Snapshot(
            1, SiteStateContractV1.Development, siteName: marker);

        using (var rejected = await host.SendWebhookAsync(
                   snapshot, secret: "wrong-secret-that-is-still-long-enough"))
        {
            var body = await rejected.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
            Assert.DoesNotContain(marker, body, StringComparison.Ordinal);
            Assert.DoesNotContain(SiteStateWebhookAuthenticationTests.TestSecret, body,
                StringComparison.Ordinal);
            Assert.DoesNotContain(Yagot07EndToEndHost.SnapshotApiKey, body,
                StringComparison.Ordinal);
        }

        using (var accepted = await host.SendWebhookAsync(snapshot))
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var restricted = await host.RequestAsync(SiteAccessSurface.Storefront);
        Assert.Equal(HttpStatusCode.Redirect, restricted.StatusCode);
        var location = restricted.Headers.Location?.OriginalString ?? string.Empty;
        Assert.DoesNotContain(SiteStateWebhookAuthenticationTests.TestSecret, location,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Yagot07EndToEndHost.SnapshotApiKey, location,
            StringComparison.Ordinal);
        Assert.Equal("no-store", restricted.Headers.CacheControl?.ToString());
    }
}
