namespace YAGOT_2._0.Models;

public sealed class CustomerOrdersIndexViewModel
{
    public IReadOnlyList<Order> Orders { get; init; } = [];
    public IReadOnlyDictionary<string, int> StatusCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public string Status { get; init; } = "all";
    public string Search { get; init; } = string.Empty;
    public int CurrentPage { get; init; } = 1;
    public int PageSize { get; init; } = 9;
    public int TotalCount { get; init; }
    public int TotalPages { get; init; } = 1;
    public int AllCount => StatusCounts.Values.Sum();
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    public int ReviewRequiredCount { get; init; }
    public int? FirstReviewRequiredOrderId { get; init; }
}
