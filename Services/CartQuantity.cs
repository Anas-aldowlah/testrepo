using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public static class CartQuantity
{
    public static int Total(IEnumerable<Cartitem> items)
    {
        var total = 0;
        checked
        {
            foreach (var item in items)
                total += item.Quantity;
        }

        return total;
    }
}
