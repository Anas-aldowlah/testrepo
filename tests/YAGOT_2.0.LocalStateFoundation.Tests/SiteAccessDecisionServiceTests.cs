using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteAccessDecisionServiceTests
{
    private static readonly DateTimeOffset Expiry =
        new(2026, 10, 7, 21, 0, 0, TimeSpan.Zero);

    [Theory]
    [MemberData(nameof(ApprovedModeRoleMatrix))]
    public async Task ModeRoleMatrix_IsPreserved(
        string mode,
        string? role,
        SiteAccessSurface surface,
        SiteAccessDecisionKind kind,
        SiteAccessHtmlTarget target)
    {
        var service = Create(
            LocalSiteStateReadResult.Found(Snapshot(mode)),
            Expiry.AddTicks(-1));

        var decision = await service.DecideAsync(surface, User(role));

        Assert.Equal(kind, decision.Kind);
        Assert.Equal(target, decision.HtmlTarget);
    }

    public static TheoryData<
        string,
        string?,
        SiteAccessSurface,
        SiteAccessDecisionKind,
        SiteAccessHtmlTarget> ApprovedModeRoleMatrix => new()
        {
            { SiteStateContractV1.Online, null, SiteAccessSurface.Storefront, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Development, null, SiteAccessSurface.Storefront, SiteAccessDecisionKind.DevelopmentRestricted, SiteAccessHtmlTarget.Developer },
            { SiteStateContractV1.Offline, null, SiteAccessSurface.Storefront, SiteAccessDecisionKind.OfflineRestricted, SiteAccessHtmlTarget.Developer },
            { SiteStateContractV1.Online, "Customer", SiteAccessSurface.Storefront, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Development, "Customer", SiteAccessSurface.Storefront, SiteAccessDecisionKind.DevelopmentRestricted, SiteAccessHtmlTarget.Developer },
            { SiteStateContractV1.Offline, "Customer", SiteAccessSurface.Storefront, SiteAccessDecisionKind.OfflineRestricted, SiteAccessHtmlTarget.Developer },
            { SiteStateContractV1.Online, "Admin", SiteAccessSurface.Storefront, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Development, "Admin", SiteAccessSurface.Storefront, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Offline, "Admin", SiteAccessSurface.Storefront, SiteAccessDecisionKind.OfflineRestricted, SiteAccessHtmlTarget.Close },
            { SiteStateContractV1.Online, "Developer", SiteAccessSurface.Storefront, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Development, "Developer", SiteAccessSurface.Storefront, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Offline, "Developer", SiteAccessSurface.Storefront, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Online, "Admin", SiteAccessSurface.Admin, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Development, "Admin", SiteAccessSurface.Admin, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Offline, "Admin", SiteAccessSurface.Admin, SiteAccessDecisionKind.OfflineRestricted, SiteAccessHtmlTarget.Close },
            { SiteStateContractV1.Online, "Developer", SiteAccessSurface.Admin, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Development, "Developer", SiteAccessSurface.Admin, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None },
            { SiteStateContractV1.Offline, "Developer", SiteAccessSurface.Admin, SiteAccessDecisionKind.Allow, SiteAccessHtmlTarget.None }
        };

    [Theory]
    [InlineData(-1, SiteStateContractV1.Development)]
    [InlineData(0, SiteStateContractV1.Offline)]
    [InlineData(1, SiteStateContractV1.Offline)]
    public async Task Expiry_IsEvaluatedAtEveryExactBoundary(
        long ticksFromExpiry,
        string expectedMode)
    {
        var clock = new FakeTimeProvider(Expiry.AddTicks(ticksFromExpiry));
        var service = Create(
            LocalSiteStateReadResult.Found(Snapshot(SiteStateContractV1.Development)),
            clock);

        var first = await service.DecideAsync(SiteAccessSurface.Storefront, User());
        Assert.Equal(expectedMode, first.EffectiveMode);

        clock.SetUtcNow(Expiry.AddTicks(1));
        var second = await service.DecideAsync(SiteAccessSurface.Storefront, User());
        Assert.Equal(SiteStateContractV1.Offline, second.EffectiveMode);
    }

    [Fact]
    public async Task Missing_DeniesEveryoneExceptDeveloper()
    {
        var service = Create(LocalSiteStateReadResult.Missing, Expiry.AddDays(-1));

        Assert.Equal(
            SiteAccessDecisionKind.Missing,
            (await service.DecideAsync(SiteAccessSurface.Storefront, User())).Kind);
        Assert.True((await service.DecideAsync(
            SiteAccessSurface.Admin,
            User("Developer"))).IsAllowed);
    }

    [Fact]
    public async Task UnknownProviderStatus_IsInvalidDurableState()
    {
        var malformed = new LocalSiteStateReadResult(
            (LocalSiteStateReadStatus)int.MaxValue,
            Snapshot(SiteStateContractV1.Online));
        var service = Create(malformed, Expiry.AddDays(-1));

        var decision = await service.DecideAsync(
            SiteAccessSurface.Storefront,
            User());

        Assert.Equal(SiteAccessDecisionKind.InvalidDurableState, decision.Kind);
    }

    [Theory]
    [InlineData(LocalSiteRuntimeStateReadFailure.StorageUnavailable, SiteAccessDecisionKind.StorageUnavailable)]
    [InlineData(LocalSiteRuntimeStateReadFailure.LoadTimeout, SiteAccessDecisionKind.LoadTimeout)]
    [InlineData(LocalSiteRuntimeStateReadFailure.InvalidDurableState, SiteAccessDecisionKind.InvalidDurableState)]
    [InlineData(LocalSiteRuntimeStateReadFailure.RevisionRegression, SiteAccessDecisionKind.RevisionRegression)]
    [InlineData(LocalSiteRuntimeStateReadFailure.EqualRevisionConflict, SiteAccessDecisionKind.EqualRevisionConflict)]
    [InlineData(LocalSiteRuntimeStateReadFailure.ProviderStopped, SiteAccessDecisionKind.ProviderUnavailable)]
    public async Task ProviderFailure_IsCategorizedAndOnlyDeveloperBypasses(
        LocalSiteRuntimeStateReadFailure failure,
        SiteAccessDecisionKind expected)
    {
        var service = Create(
            _ => throw new LocalSiteRuntimeStateReadException(failure),
            new FakeTimeProvider(Expiry.AddDays(-1)));

        Assert.Equal(expected, (await service.DecideAsync(
            SiteAccessSurface.Storefront,
            User("Admin"))).Kind);
        Assert.True((await service.DecideAsync(
            SiteAccessSurface.Admin,
            User("Developer"))).IsAllowed);
    }

    [Theory]
    [InlineData("https://example.test/path", true)]
    [InlineData("http://example.test/path", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://user@example.test/path", false)]
    public async Task OfflineMaintenanceMetadata_IsDerivedAndUrlIsSafe(
        string url,
        bool clickable)
    {
        var service = Create(
            LocalSiteStateReadResult.Found(Snapshot(SiteStateContractV1.Offline) with
            {
                SiteUrl = url
            }),
            Expiry.AddDays(-1));

        var decision = await service.DecideAsync(
            SiteAccessSurface.Admin,
            User("Admin"));

        var metadata = Assert.IsType<YAGOT_2._0.Models.SiteMaintenanceViewModel>(
            decision.Maintenance);
        Assert.Equal("YAGOT", metadata.SiteName);
        Assert.Equal(new DateOnly(2026, 10, 7), metadata.EndDate);
        Assert.Equal(new DateOnly(2026, 9, 7), metadata.StartDate);
        Assert.Equal(30, metadata.OriginalDurationDays);
        Assert.Equal(clickable, metadata.ClickableSiteUrl is not null);
    }

    [Fact]
    public async Task InconsistentOfflineMetadata_IsInvalidDurableState()
    {
        var service = Create(
            LocalSiteStateReadResult.Found(Snapshot(SiteStateContractV1.Offline) with
            {
                OriginalDurationDays = 29
            }),
            Expiry.AddDays(-1));

        var decision = await service.DecideAsync(
            SiteAccessSurface.Admin,
            User("Admin"));

        Assert.Equal(SiteAccessDecisionKind.InvalidDurableState, decision.Kind);
        Assert.Null(decision.Maintenance);
    }

    private static SiteAccessDecisionService Create(
        LocalSiteStateReadResult result,
        DateTimeOffset now) => Create(result, new FakeTimeProvider(now));

    private static SiteAccessDecisionService Create(
        LocalSiteStateReadResult result,
        FakeTimeProvider clock) => Create(_ => Task.FromResult(result), clock);

    private static SiteAccessDecisionService Create(
        Func<CancellationToken, Task<LocalSiteStateReadResult>> read,
        FakeTimeProvider clock) => new(
            new StubRuntimeProvider(read),
            clock,
            NullLogger<SiteAccessDecisionService>.Instance);

    private static SiteStateSnapshotV1 Snapshot(string mode) =>
        SiteStateContractTests.ValidSnapshot(mode: mode) with
        {
            ExpiresAtUtc = Expiry
        };

    private static ClaimsPrincipal User(string? role = null)
    {
        var claims = role is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.Role, role) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed class StubRuntimeProvider(
        Func<CancellationToken, Task<LocalSiteStateReadResult>> read)
        : ILocalSiteRuntimeStateProvider
    {
        public Task<LocalSiteStateReadResult> ReadAsync(
            CancellationToken cancellationToken = default) => read(cancellationToken);
    }
}
