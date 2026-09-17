using System;
using System.Collections.Generic;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Models;

public class OffersViewModel
{
    public IEnumerable<Product> Products { get; set; } = new List<Product>();
    public IEnumerable<Category> Categories { get; set; } = new List<Category>();

    // Filter Parameters
    public string? SearchQuery { get; set; }
    public int? CategoryId { get; set; }
    public string? PromoType { get; set; }
    public string? Sort { get; set; }

    // Pagination Parameters
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}
