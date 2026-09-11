using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Services.Integration;

public interface ILocalSiteStateReader
{
    Task<LocalSiteStateReadResult> ReadAsync(CancellationToken cancellationToken = default);
}
