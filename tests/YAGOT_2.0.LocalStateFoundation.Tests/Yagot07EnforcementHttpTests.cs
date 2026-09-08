using System.Net;
using Microsoft.Extensions.Time.Testing;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class Yagot07EnforcementHttpTests
{
    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task FullModeRoleMatrix_UsesRealDurableProviderForHtmlAndJson()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        long revision = 0;

        foreach (var item in Matrix())
        {
            await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(++revision, item.Mode));
            foreach (var json in new[] { false, true })
            {
                host.ResetActions();
                using var response = await host.RequestAsync(item.Surface, item.Role, json);
                if (item.Allowed)
                {
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.Equal(1, host.ActionCount);
                    continue;
                }

                Assert.Equal(0, host.ActionCount);
                if (json)
                {
                    await Yagot07HttpAssertions.HasJsonCodeAsync(
                        response, HttpStatusCode.ServiceUnavailable, item.Code!);
                }
                else
                {
                    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                    Assert.Contains(item.Redirect!, response.Headers.Location?.OriginalString,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task AdminAuthenticationAndAuthorizationRunBeforeSiteState()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online));
        var readsBefore = host.SnapshotQueries.Count;

        using (var anonymous = await host.RequestAsync(SiteAccessSurface.Admin, json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                anonymous, HttpStatusCode.Unauthorized, "session_expired");
        using (var customer = await host.RequestAsync(SiteAccessSurface.Admin, "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                customer, HttpStatusCode.Forbidden, "forbidden");

        Assert.Equal(readsBefore, host.SnapshotQueries.Count);
        Assert.Equal(0, host.ActionCount);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task MissingAndRealReadFailure_UseExistingFailSafePolicy()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);

        using (var missing = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                missing, HttpStatusCode.ServiceUnavailable, "site_state_missing");
        host.ResetActions();
        using (var developerMissing = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Developer"))
        {
            Assert.Equal(HttpStatusCode.OK, developerMissing.StatusCode);
            Assert.Equal(1, host.ActionCount);
        }

        await using var failingDatabase = await PostgreSqlTestDatabase.CreateAsync();
        await using var failingHost = await Yagot07EndToEndHost.CreateAsync(failingDatabase);
        failingHost.SnapshotQueries.FailReads = true;
        using (var failure = await failingHost.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                failure, HttpStatusCode.ServiceUnavailable, "site_state_storage_unavailable");
        failingHost.ResetActions();
        using (var developerFailure = await failingHost.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Developer"))
        {
            Assert.Equal(HttpStatusCode.OK, developerFailure.StatusCode);
            Assert.Equal(1, failingHost.ActionCount);
        }
        Assert.Equal(0, host.ControlPanel.CallCount);
        Assert.Equal(0, failingHost.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task ExpiryBoundaryChangesHttpDecisionWithoutChangingDurableStateOrReloading()
    {
        var expiry = new DateTimeOffset(2026, 10, 7, 21, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(expiry.AddTicks(-1));
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database, clock);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(
            11, SiteStateContractV1.Online, expiry));

        using (var before = await host.RequestAsync(SiteAccessSurface.Storefront))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var reads = host.SnapshotQueries.Count;

        clock.SetUtcNow(expiry);
        using (var exact = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                exact, HttpStatusCode.ServiceUnavailable, "site_offline");
        clock.Advance(TimeSpan.FromTicks(1));
        using (var after = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                after, HttpStatusCode.ServiceUnavailable, "site_offline");

        Assert.Equal(reads, host.SnapshotQueries.Count);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(11, durable.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Online, durable.Snapshot.Mode);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task DevelopmentExpiryBecomesOfflineThroughFilteredHttpWithoutIngressOrRefresh()
    {
        var expiry = new DateTimeOffset(2026, 10, 7, 21, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(expiry.AddTicks(-1));
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database, clock);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(
            12, SiteStateContractV1.Development, expiry));

        using (var before = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                before, HttpStatusCode.ServiceUnavailable, "site_development");
        var reads = host.SnapshotQueries.Count;

        clock.SetUtcNow(expiry);
        using (var exact = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                exact, HttpStatusCode.ServiceUnavailable, "site_offline");
        clock.Advance(TimeSpan.FromTicks(1));
        using (var after = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                after, HttpStatusCode.ServiceUnavailable, "site_offline");

        Assert.Equal(reads, host.SnapshotQueries.Count);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(12, durable.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Development, durable.Snapshot.Mode);
        Assert.Equal(0, durable.ReceiptCount);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    private static IReadOnlyList<PolicyCase> Matrix() =>
    [
        new(SiteAccessSurface.Storefront, null, SiteStateContractV1.Online, true),
        new(SiteAccessSurface.Storefront, null, SiteStateContractV1.Development, false,
            "site_development", "/DirectiveDevClose/Developer"),
        new(SiteAccessSurface.Storefront, null, SiteStateContractV1.Offline, false,
            "site_offline", "/DirectiveDevClose/Developer"),
        new(SiteAccessSurface.Storefront, "Customer", SiteStateContractV1.Online, true),
        new(SiteAccessSurface.Storefront, "Customer", SiteStateContractV1.Development, false,
            "site_development", "/DirectiveDevClose/Developer"),
        new(SiteAccessSurface.Storefront, "Customer", SiteStateContractV1.Offline, false,
            "site_offline", "/DirectiveDevClose/Developer"),
        new(SiteAccessSurface.Storefront, "Admin", SiteStateContractV1.Online, true),
        new(SiteAccessSurface.Storefront, "Admin", SiteStateContractV1.Development, true),
        new(SiteAccessSurface.Storefront, "Admin", SiteStateContractV1.Offline, false,
            "site_offline", "/DirectiveDevClose/close"),
        new(SiteAccessSurface.Storefront, "Developer", SiteStateContractV1.Online, true),
        new(SiteAccessSurface.Storefront, "Developer", SiteStateContractV1.Development, true),
        new(SiteAccessSurface.Storefront, "Developer", SiteStateContractV1.Offline, true),
        new(SiteAccessSurface.Admin, "Admin", SiteStateContractV1.Online, true),
        new(SiteAccessSurface.Admin, "Admin", SiteStateContractV1.Development, true),
        new(SiteAccessSurface.Admin, "Admin", SiteStateContractV1.Offline, false,
            "site_offline", "/DirectiveDevClose/close"),
        new(SiteAccessSurface.Admin, "Developer", SiteStateContractV1.Online, true),
        new(SiteAccessSurface.Admin, "Developer", SiteStateContractV1.Development, true),
        new(SiteAccessSurface.Admin, "Developer", SiteStateContractV1.Offline, true)
    ];

    private sealed record PolicyCase(
        SiteAccessSurface Surface,
        string? Role,
        string Mode,
        bool Allowed,
        string? Code = null,
        string? Redirect = null);
}
