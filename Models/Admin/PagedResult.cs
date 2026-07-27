using Microsoft.EntityFrameworkCore;

namespace YAGOT_2._0.Models.Admin;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }

    public int TotalPages => TotalItems == 0 ? 1 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    public int FirstItemNumber => TotalItems == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
    public int LastItemNumber => Math.Min(CurrentPage * PageSize, TotalItems);

    public static async Task<PagedResult<T>> CreateAsync(IQueryable<T> query, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 5, 10);
        var totalItems = await query.CountAsync();
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var currentPage = Math.Clamp(page < 1 ? 1 : page, 1, totalPages);
        var items = await query
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }
}
