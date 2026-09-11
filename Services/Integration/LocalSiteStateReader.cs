using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Integration;

public sealed class LocalSiteStateReader : ILocalSiteStateReader
{
    private readonly NeondbContext _context;

    public LocalSiteStateReader(NeondbContext context)
    {
        _context = context;
    }

    public async Task<LocalSiteStateReadResult> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.LocalSiteStateSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.SiteId == SiteStateContractV1.SiteId,
                cancellationToken);

        return entity is null
            ? LocalSiteStateReadResult.Missing
            : LocalSiteStateReadResult.Found(entity.ToContract());
    }
}
