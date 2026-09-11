using System.Security.Claims;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Integration;

public sealed class SiteAccessDecisionService(
    ILocalSiteRuntimeStateProvider runtimeState,
    TimeProvider timeProvider,
    ILogger<SiteAccessDecisionService> logger) : ISiteAccessDecisionService
{
    private static readonly TimeZoneInfo AdenTimeZone = ResolveAdenTimeZone();

    public async Task<SiteAccessDecision> DecideAsync(
        SiteAccessSurface surface,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var isDeveloper = user.IsInRole("Developer");
        LocalSiteStateReadResult result;

        try
        {
            result = await runtimeState.ReadAsync(cancellationToken);
        }
        catch (LocalSiteRuntimeStateReadException exception)
        {
            return FailureOrDeveloperBypass(MapFailure(exception.Category), isDeveloper);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Local site access evaluation failed; category ProviderUnavailable; failure type {FailureType}.",
                exception.GetType().Name);
            return FailureOrDeveloperBypass(
                SiteAccessDecisionKind.ProviderUnavailable,
                isDeveloper);
        }

        if (result.Status == LocalSiteStateReadStatus.Missing)
        {
            return isDeveloper
                ? Allow()
                : Deny(SiteAccessDecisionKind.Missing);
        }

        var snapshot = result.Snapshot;
        if (result.Status != LocalSiteStateReadStatus.Found || snapshot is null)
        {
            return FailureOrDeveloperBypass(
                SiteAccessDecisionKind.InvalidDurableState,
                isDeveloper);
        }

        var effectiveMode = timeProvider.GetUtcNow() >= snapshot.ExpiresAtUtc
            ? SiteStateContractV1.Offline
            : snapshot.Mode;

        if (isDeveloper || effectiveMode == SiteStateContractV1.Online)
        {
            return Allow(effectiveMode, snapshot);
        }

        var isAdmin = user.IsInRole("Admin");
        if (effectiveMode == SiteStateContractV1.Development)
        {
            if (surface == SiteAccessSurface.Admin)
            {
                return isAdmin
                    ? Allow(effectiveMode, snapshot)
                    : Restrict(
                        SiteAccessDecisionKind.DevelopmentRestricted,
                        SiteAccessHtmlTarget.Developer,
                        effectiveMode,
                        snapshot);
            }

            if (isAdmin)
            {
                return Restrict(
                    SiteAccessDecisionKind.DevelopmentRestricted,
                    SiteAccessHtmlTarget.AdminDashboard,
                    effectiveMode,
                    snapshot);
            }

            return Restrict(
                SiteAccessDecisionKind.DevelopmentRestricted,
                SiteAccessHtmlTarget.Developer,
                effectiveMode,
                snapshot);
        }

        if (effectiveMode == SiteStateContractV1.Offline)
        {
            SiteMaintenanceViewModel maintenance;
            try
            {
                maintenance = CreateMaintenanceModel(snapshot);
            }
            catch (Exception exception) when (
                exception is ArgumentOutOfRangeException or OverflowException)
            {
                logger.LogError(
                    "Local site maintenance metadata is inconsistent; site {SiteId}; revision {Revision}.",
                    snapshot.SiteId,
                    snapshot.Revision);
                return FailureOrDeveloperBypass(
                    SiteAccessDecisionKind.InvalidDurableState,
                    isDeveloper);
            }

            var target = surface == SiteAccessSurface.Admin || isAdmin
                ? SiteAccessHtmlTarget.Close
                : SiteAccessHtmlTarget.Developer;
            return Restrict(
                SiteAccessDecisionKind.OfflineRestricted,
                target,
                effectiveMode,
                snapshot,
                maintenance);
        }

        return FailureOrDeveloperBypass(
            SiteAccessDecisionKind.InvalidDurableState,
            isDeveloper);
    }

    private static SiteMaintenanceViewModel CreateMaintenanceModel(
        SiteStateSnapshotV1 snapshot)
    {
        var adenExpiry = TimeZoneInfo.ConvertTime(snapshot.ExpiresAtUtc, AdenTimeZone);
        var endDate = DateOnly.FromDateTime(adenExpiry.DateTime).AddDays(-1);
        var expectedEndDate = snapshot.StartDate.AddDays(snapshot.OriginalDurationDays);
        if (endDate != expectedEndDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshot),
                "Snapshot duration does not match its expiration boundary.");
        }

        Uri? clickableUrl = null;
        if (Uri.TryCreate(snapshot.SiteUrl, UriKind.Absolute, out var parsed) &&
            parsed.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(parsed.UserInfo))
        {
            clickableUrl = parsed;
        }

        return new SiteMaintenanceViewModel(
            snapshot.SiteName,
            snapshot.SiteUrl,
            clickableUrl,
            snapshot.StartDate,
            endDate,
            snapshot.OriginalDurationDays);
    }

    private static SiteAccessDecisionKind MapFailure(
        LocalSiteRuntimeStateReadFailure failure) => failure switch
        {
            LocalSiteRuntimeStateReadFailure.StorageUnavailable =>
                SiteAccessDecisionKind.StorageUnavailable,
            LocalSiteRuntimeStateReadFailure.LoadTimeout =>
                SiteAccessDecisionKind.LoadTimeout,
            LocalSiteRuntimeStateReadFailure.InvalidDurableState =>
                SiteAccessDecisionKind.InvalidDurableState,
            LocalSiteRuntimeStateReadFailure.RevisionRegression =>
                SiteAccessDecisionKind.RevisionRegression,
            LocalSiteRuntimeStateReadFailure.EqualRevisionConflict =>
                SiteAccessDecisionKind.EqualRevisionConflict,
            LocalSiteRuntimeStateReadFailure.ProviderStopped =>
                SiteAccessDecisionKind.ProviderUnavailable,
            _ => SiteAccessDecisionKind.ProviderUnavailable
        };

    private static SiteAccessDecision FailureOrDeveloperBypass(
        SiteAccessDecisionKind failure,
        bool isDeveloper) => isDeveloper ? Allow() : Deny(failure);

    private static SiteAccessDecision Allow(
        string? effectiveMode = null,
        SiteStateSnapshotV1? snapshot = null,
        SiteMaintenanceViewModel? maintenance = null) =>
        new(
            SiteAccessDecisionKind.Allow,
            SiteAccessHtmlTarget.None,
            effectiveMode,
            snapshot,
            maintenance);

    private static SiteAccessDecision Deny(SiteAccessDecisionKind kind) =>
        new(kind, SiteAccessHtmlTarget.Unavailable, null, null, null);

    private static SiteAccessDecision Restrict(
        SiteAccessDecisionKind kind,
        SiteAccessHtmlTarget target,
        string effectiveMode,
        SiteStateSnapshotV1 snapshot,
        SiteMaintenanceViewModel? maintenance = null) =>
        new(kind, target, effectiveMode, snapshot, maintenance);

    private static TimeZoneInfo ResolveAdenTimeZone()
    {
        const string timeZoneId = "Asia/Aden";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (
            TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }
}
