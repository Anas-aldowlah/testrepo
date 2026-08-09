using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public sealed class CartLockService
{
    public const int CartLockNamespace = 149745236;

    private readonly NeondbContext _context;

    public CartLockService(NeondbContext context)
    {
        _context = context;
    }

    public async Task AcquireAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction == null)
            throw new InvalidOperationException("The cart advisory lock requires an active database transaction.");

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({CartLockNamespace}, {userId})",
            cancellationToken);
    }
}
