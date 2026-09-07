using System.Net;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateWebhookDurableEndpointTests
{
    [Fact]
    [Trait("Suite", "YAGOT02Durable")]
    public async Task Endpoint_PreservesAllDurableApplyDecisions()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await WebhookTestHost.CreateWithDatabaseAsync(database);
        var appliedId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var equalId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var staleId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var conflictId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var appliedBody = SiteStateWebhookEndpointTests.ValidBody(
            revision: 5,
            siteName: "Newest");

        using var applied = await host.SendSignedAsync(appliedBody, appliedId);
        using var ordinaryDuplicate = await host.SendSignedAsync(
            appliedBody,
            appliedId);
        using var equal = await host.SendSignedAsync(
            appliedBody,
            equalId);
        using var stale = await host.SendSignedAsync(
            SiteStateWebhookEndpointTests.ValidBody(
                revision: 4,
                siteName: "Must not replace"),
            staleId);
        var conflictBody = SiteStateWebhookEndpointTests.ValidBody(
            revision: 5,
            siteName: "Equal revision conflict");
        using var conflict = await host.SendSignedAsync(
            conflictBody,
            conflictId);
        using var duplicateConflict = await host.SendSignedAsync(
            conflictBody,
            conflictId);
        using var deliveryPayloadConflict = await host.SendSignedAsync(
            SiteStateWebhookEndpointTests.ValidBody(
                revision: 9,
                siteName: "Reused delivery must not apply"),
            appliedId);

        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ordinaryDuplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, equal.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateConflict.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            deliveryPayloadConflict.StatusCode);

        await using var verification = database.CreateContext();
        var snapshot = await verification.LocalSiteStateSnapshots.SingleAsync();
        Assert.Equal(5, snapshot.Revision);
        Assert.Equal("Newest", snapshot.SiteName);
        Assert.Equal(4, await verification.SiteStateEventReceipts.CountAsync());
        Assert.Equal(4, await verification.SiteStateEventReceipts
            .Select(receipt => receipt.DeliveryId)
            .Distinct()
            .CountAsync());
    }
}
