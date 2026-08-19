using System.Data;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public enum DraftWriteRejection
{
    None,
    NotDraft,
    LockExpired,
    SessionMismatch,
    StaleRevision
}

public sealed record DraftEditSessionResult(
    bool Acquired,
    bool Exists,
    Guid? EditSessionId,
    long DraftRevision,
    DateTime? ExpiresAt);

public sealed record DraftStateResult(
    bool Exists,
    string State,
    long DraftRevision,
    bool OwnsEditLock,
    DateTime? ExpiresAt);

public interface IDraftEditSessionService
{
    TimeSpan LeaseDuration { get; }
    Task<DraftEditSessionResult> AcquireAsync(int saleId, string userName, CancellationToken cancellationToken = default);
    Task<bool> RenewAsync(int saleId, Guid editSessionId, CancellationToken cancellationToken = default);
    Task<bool> ReleaseAsync(int saleId, Guid editSessionId, CancellationToken cancellationToken = default);
    Task<DraftStateResult> GetStateAsync(int saleId, Guid? editSessionId, CancellationToken cancellationToken = default);
    DraftWriteRejection ValidateWrite(Sale sale, Guid editSessionId, long expectedRevision, DateTime utcNow);
    void AdvanceRevisionAndLease(Sale sale, DateTime utcNow);
}

public sealed class DraftEditSessionService : IDraftEditSessionService
{
    private readonly NeondbContext _context;

    public DraftEditSessionService(NeondbContext context)
    {
        _context = context;
    }

    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(90);

    public async Task<DraftEditSessionResult> AcquireAsync(
        int saleId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var rows = await _context.Sales
                .FromSqlInterpolated($"SELECT * FROM sales WHERE id = {saleId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            var sale = rows.SingleOrDefault();
            if (sale == null || !string.Equals(sale.Status, "Draft", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DraftEditSessionResult(false, sale != null, null, sale?.DraftRevision ?? 0, null);
            }

            var utcNow = DateTime.UtcNow;
            if (sale.EditSessionId.HasValue && sale.EditLockExpiresAt > utcNow)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DraftEditSessionResult(false, true, null, sale.DraftRevision, sale.EditLockExpiresAt);
            }

            sale.EditSessionId = Guid.NewGuid();
            sale.EditLockedBy = userName;
            sale.EditLockExpiresAt = utcNow.Add(LeaseDuration);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new DraftEditSessionResult(true, true, sale.EditSessionId, sale.DraftRevision, sale.EditLockExpiresAt);
        });
    }

    public async Task<bool> RenewAsync(int saleId, Guid editSessionId, CancellationToken cancellationToken = default)
    {
        if (editSessionId == Guid.Empty)
            return false;

        var utcNow = DateTime.UtcNow;
        var newExpiry = utcNow.Add(LeaseDuration);
        var affected = await _context.Sales
            .Where(s => s.Id == saleId &&
                        s.Status == "Draft" &&
                        s.EditSessionId == editSessionId &&
                        s.EditLockExpiresAt > utcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.EditLockExpiresAt, newExpiry), cancellationToken);
        return affected == 1;
    }

    public async Task<bool> ReleaseAsync(int saleId, Guid editSessionId, CancellationToken cancellationToken = default)
    {
        if (editSessionId == Guid.Empty)
            return false;

        var affected = await _context.Sales
            .Where(s => s.Id == saleId && s.Status == "Draft" && s.EditSessionId == editSessionId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.EditSessionId, (Guid?)null)
                .SetProperty(s => s.EditLockedBy, (string?)null)
                .SetProperty(s => s.EditLockExpiresAt, (DateTime?)null), cancellationToken);
        return affected == 1;
    }

    public async Task<DraftStateResult> GetStateAsync(
        int saleId,
        Guid? editSessionId,
        CancellationToken cancellationToken = default)
    {
        var state = await _context.Sales
            .AsNoTracking()
            .Where(s => s.Id == saleId)
            .Select(s => new { s.Status, s.DraftRevision, s.EditSessionId, s.EditLockExpiresAt })
            .SingleOrDefaultAsync(cancellationToken);

        if (state == null)
            return new DraftStateResult(false, "Deleted", 0, false, null);

        var utcNow = DateTime.UtcNow;
        var ownsLock = editSessionId.HasValue &&
                       state.EditSessionId == editSessionId &&
                       state.EditLockExpiresAt > utcNow;
        return new DraftStateResult(true, state.Status, state.DraftRevision, ownsLock, state.EditLockExpiresAt);
    }

    public DraftWriteRejection ValidateWrite(Sale sale, Guid editSessionId, long expectedRevision, DateTime utcNow)
    {
        if (!string.Equals(sale.Status, "Draft", StringComparison.Ordinal))
            return DraftWriteRejection.NotDraft;
        if (!sale.EditSessionId.HasValue || sale.EditLockExpiresAt <= utcNow)
            return DraftWriteRejection.LockExpired;
        if (editSessionId == Guid.Empty || sale.EditSessionId.Value != editSessionId)
            return DraftWriteRejection.SessionMismatch;
        if (sale.DraftRevision != expectedRevision)
            return DraftWriteRejection.StaleRevision;
        return DraftWriteRejection.None;
    }

    public void AdvanceRevisionAndLease(Sale sale, DateTime utcNow)
    {
        sale.DraftRevision = checked(sale.DraftRevision + 1);
        sale.EditLockExpiresAt = utcNow.Add(LeaseDuration);
    }
}
