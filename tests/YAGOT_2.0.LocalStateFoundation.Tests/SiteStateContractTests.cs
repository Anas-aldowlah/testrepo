using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateContractTests
{
    [Theory]
    [InlineData(SiteStateContractV1.Online)]
    [InlineData(SiteStateContractV1.Development)]
    [InlineData(SiteStateContractV1.Offline)]
    public void ValidContract_AcceptsEveryDefinedMode(string mode)
    {
        ValidSnapshot(mode: mode).Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RevisionBelowOne_IsRejected(long revision)
    {
        Assert.Throws<SiteStateContractValidationException>(
            () => ValidSnapshot(revision: revision).Validate());
    }

    [Theory]
    [InlineData(0, 1, SiteStateContractV1.Online)]
    [InlineData(1, 2, SiteStateContractV1.Online)]
    [InlineData(1, 1, "online")]
    [InlineData(1, 1, "Expired")]
    public void WrongVersionSiteOrMode_IsRejected(
        int contractVersion,
        int siteId,
        string mode)
    {
        var snapshot = ValidSnapshot(mode: mode) with
        {
            ContractVersion = contractVersion,
            SiteId = siteId
        };

        Assert.Throws<SiteStateContractValidationException>(snapshot.Validate);
    }

    [Fact]
    public void NonUtcTimestamp_IsRejected()
    {
        var snapshot = ValidSnapshot() with
        {
            EffectiveAtUtc = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.FromHours(3))
        };

        Assert.Throws<SiteStateContractValidationException>(snapshot.Validate);
    }

    [Fact]
    public void SiteNameOverTwoHundredCharacters_IsRejected()
    {
        var snapshot = ValidSnapshot() with { SiteName = new string('N', 201) };

        Assert.Throws<SiteStateContractValidationException>(snapshot.Validate);
    }

    [Fact]
    public void SiteUrlHasNoInventedMaximumLength()
    {
        var snapshot = ValidSnapshot() with
        {
            SiteUrl = $"https://example.test/{new string('u', 2000)}"
        };

        snapshot.Validate();
    }

    [Fact]
    public void NonPositiveDuration_IsRejected()
    {
        var snapshot = ValidSnapshot() with { OriginalDurationDays = 0 };

        Assert.Throws<SiteStateContractValidationException>(
            snapshot.Validate);
    }

    [Fact]
    public void DeliveryContext_RequiresGuidAndExactSha256Length()
    {
        Assert.Throws<SiteStateContractValidationException>(
            () => new SiteStateDeliveryContext(Guid.Empty, Hash(1)).Validate());
        Assert.Throws<SiteStateContractValidationException>(
            () => new SiteStateDeliveryContext(Guid.NewGuid(), new byte[31]).Validate());

        new SiteStateDeliveryContext(Guid.NewGuid(), Hash(1)).Validate();
    }

    [Fact]
    public void EfModel_UsesExactPostgreSqlFoundationSchema()
    {
        var options = new DbContextOptionsBuilder<NeondbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=model_only;Username=test;Pooling=false")
            .Options;
        using var context = new NeondbContext(options);

        var snapshot = context.Model.FindEntityType(typeof(LocalSiteStateSnapshot))!;
        Assert.Equal("local_site_state_snapshots", snapshot.GetTableName());
        Assert.Equal("integer", snapshot.FindProperty(nameof(LocalSiteStateSnapshot.SiteId))!.GetColumnType());
        Assert.Equal("bigint", snapshot.FindProperty(nameof(LocalSiteStateSnapshot.Revision))!.GetColumnType());
        Assert.Equal("character varying(20)", snapshot.FindProperty(nameof(LocalSiteStateSnapshot.Mode))!.GetColumnType());
        Assert.Equal(200, snapshot.FindProperty(nameof(LocalSiteStateSnapshot.SiteName))!.GetMaxLength());
        Assert.Equal("text", snapshot.FindProperty(nameof(LocalSiteStateSnapshot.SiteUrl))!.GetColumnType());
        Assert.Equal("date", snapshot.FindProperty(nameof(LocalSiteStateSnapshot.StartDate))!.GetColumnType());
        Assert.Equal("timestamp with time zone", snapshot.FindProperty(nameof(LocalSiteStateSnapshot.EffectiveAtUtc))!.GetColumnType());
        Assert.Equal("timestamp with time zone", snapshot.FindProperty(nameof(LocalSiteStateSnapshot.ExpiresAtUtc))!.GetColumnType());

        var receipt = context.Model.FindEntityType(typeof(SiteStateEventReceipt))!;
        Assert.Equal("site_state_event_receipts", receipt.GetTableName());
        Assert.Equal("uuid", receipt.FindProperty(nameof(SiteStateEventReceipt.DeliveryId))!.GetColumnType());
        Assert.Equal("bytea", receipt.FindProperty(nameof(SiteStateEventReceipt.PayloadSha256))!.GetColumnType());
        Assert.Equal("timestamp with time zone", receipt.FindProperty(nameof(SiteStateEventReceipt.RecordedAtUtc))!.GetColumnType());
        Assert.Contains(receipt.GetIndexes(), index =>
            index.GetDatabaseName() == "ix_site_state_event_receipts_site_revision" &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(SiteStateEventReceipt.SiteId), nameof(SiteStateEventReceipt.Revision) }));
    }

    internal static SiteStateSnapshotV1 ValidSnapshot(
        long revision = 1,
        string mode = SiteStateContractV1.Online,
        string siteName = "YAGOT") => new(
        SiteStateContractV1.ContractVersion,
        SiteStateContractV1.SiteId,
        mode,
        revision,
        new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 10, 7, 21, 0, 0, TimeSpan.Zero),
        siteName,
        "https://example.test",
        new DateOnly(2026, 9, 7),
        30);

    internal static byte[] Hash(byte value) => Enumerable.Repeat(value, 32).ToArray();
}
