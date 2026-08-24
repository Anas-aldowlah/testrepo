using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace YAGOT_2._0.Models;

public partial class Order
{
    [NotMapped]
    public UserSite? User { get; set; }
}

public static class OrderRevenueExtensions
{
    public static readonly string[] RevenueEligibleStatuses = ["Processed", "Shipped", "Delivered"];

    public static IQueryable<Order> WhereRevenueEligible(this IQueryable<Order> query)
    {
        return query.Where(o =>
            (o.Status == "Processed" || o.Status == "Shipped" || o.Status == "Delivered"));
    }
}
