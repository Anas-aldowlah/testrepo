using YAGOT_2._0.Services.Caching.Dtos;

namespace YAGOT_2._0.Services.Caching;

public interface IStorefrontCacheService
{
    Task<HomeShowcaseDto> GetHomeShowcaseAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategorySummaryDto>> GetCategoriesListAsync(CancellationToken cancellationToken = default);
}
