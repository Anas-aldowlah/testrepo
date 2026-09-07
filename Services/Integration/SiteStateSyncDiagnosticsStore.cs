using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Integration;

public sealed class SiteStateSyncDiagnosticsStore :
    ISiteStateSyncDiagnosticsStore
{
    private readonly NeondbContext _context;

    public SiteStateSyncDiagnosticsStore(NeondbContext context)
    {
        _context = context;
    }

    public Task RecordAttemptAsync(
        int siteId,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO site_state_sync_checkpoints (
                siteid,
                lastattemptatutc,
                lastsuccessatutc,
                lastobservedremoterevision,
                consecutivefailures,
                lastfailurecode,
                updatedatutc)
            VALUES (
                {siteId},
                {attemptedAtUtc},
                NULL,
                NULL,
                0,
                NULL,
                {attemptedAtUtc})
            ON CONFLICT (siteid) DO UPDATE SET
                lastattemptatutc = GREATEST(
                    site_state_sync_checkpoints.lastattemptatutc,
                    EXCLUDED.lastattemptatutc),
                updatedatutc = GREATEST(
                    site_state_sync_checkpoints.updatedatutc,
                    EXCLUDED.updatedatutc);
            """, cancellationToken);

    public Task RecordSuccessAsync(
        int siteId,
        DateTimeOffset attemptedAtUtc,
        DateTimeOffset completedAtUtc,
        long remoteRevision,
        CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO site_state_sync_checkpoints (
                siteid,
                lastattemptatutc,
                lastsuccessatutc,
                lastobservedremoterevision,
                consecutivefailures,
                lastfailurecode,
                updatedatutc)
            VALUES (
                {siteId},
                {attemptedAtUtc},
                {completedAtUtc},
                {remoteRevision},
                0,
                NULL,
                {completedAtUtc})
            ON CONFLICT (siteid) DO UPDATE SET
                lastattemptatutc = GREATEST(
                    site_state_sync_checkpoints.lastattemptatutc,
                    EXCLUDED.lastattemptatutc),
                lastsuccessatutc = GREATEST(
                    site_state_sync_checkpoints.lastsuccessatutc,
                    EXCLUDED.lastsuccessatutc),
                lastobservedremoterevision = EXCLUDED.lastobservedremoterevision,
                consecutivefailures = 0,
                lastfailurecode = NULL,
                updatedatutc = GREATEST(
                    site_state_sync_checkpoints.updatedatutc,
                    EXCLUDED.updatedatutc);
            """, cancellationToken);

    public Task RecordFailureAsync(
        int siteId,
        DateTimeOffset attemptedAtUtc,
        DateTimeOffset completedAtUtc,
        long? remoteRevision,
        string failureCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Length > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureCode),
                "Failure code cannot exceed 64 characters.");
        }

        return _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO site_state_sync_checkpoints (
                siteid,
                lastattemptatutc,
                lastsuccessatutc,
                lastobservedremoterevision,
                consecutivefailures,
                lastfailurecode,
                updatedatutc)
            VALUES (
                {siteId},
                {attemptedAtUtc},
                NULL,
                {remoteRevision},
                1,
                {failureCode},
                {completedAtUtc})
            ON CONFLICT (siteid) DO UPDATE SET
                lastattemptatutc = GREATEST(
                    site_state_sync_checkpoints.lastattemptatutc,
                    EXCLUDED.lastattemptatutc),
                lastobservedremoterevision = COALESCE(
                    EXCLUDED.lastobservedremoterevision,
                    site_state_sync_checkpoints.lastobservedremoterevision),
                consecutivefailures =
                    site_state_sync_checkpoints.consecutivefailures + 1,
                lastfailurecode = EXCLUDED.lastfailurecode,
                updatedatutc = GREATEST(
                    site_state_sync_checkpoints.updatedatutc,
                    EXCLUDED.updatedatutc);
            """, cancellationToken);
    }
}
