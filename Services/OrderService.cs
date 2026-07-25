using Microsoft.EntityFrameworkCore;

using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class OrderService
{
    private readonly NeondbContext _context;
    private readonly CartService _cartService;
    private static readonly HashSet<string> AllowedPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "al-amqi",
        "bin-dawl",
        "al-basiri",
        "other"
    };

    public int NextOrderId()
    {
        return _context.Orders.Any() ? _context.Orders.Max(o => o.Id) + 1 : 1;
    }

    public int NextOrderItemId()
    {
        return _context.Orderitems.Any() ? _context.Orderitems.Max(oi => oi.Id) + 1 : 1;
    }

    public OrderService(NeondbContext context, CartService cartService)
    {
        _context = context;
        _cartService = cartService;
    }

    public async Task<Order> CreateOrderAsync(int userId, CheckoutVM checkout)
    {
        ValidateCheckout(checkout);

        var cart = await _cartService.GetCartAsync(userId);
        if (cart.Cartitems == null || !cart.Cartitems.Any())
            throw new InvalidOperationException("السلة فارغة - لا يمكن إنشاء طلب بدون منتجات");

        // التحقق من المخزون قبل إنشاء الطلب
        foreach (var item in cart.Cartitems)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == item.Productid);
            if (product == null || product.Stockquantity < item.Quantity)
                throw new InvalidOperationException($"المنتج '{product?.Name ?? "غير معروف"}' غير متوفر بالكمية المطلوبة");
        }

        int currentOrderId = NextOrderId();
        int currentOrderItemId = NextOrderItemId();

        var order = new Order
        {
            Id = currentOrderId,
            Userid = userId,
            Orderdate = DateTime.UtcNow,
            Status = "Pending",
            Totalamount = cart.Cartitems.Sum(i => i.Quantity * (i.Product?.Price ?? 0)),
            Trackingnumber = $"YAG-{Guid.NewGuid().ToString()[..8].ToUpper()}"
        };
        _context.Orders.Add(order);

        _context.Deliveryorders.Add(new Deliveryorder
        {
            Orderid = order.Id,
            Fullname = checkout.CustomerName,
            Phonenumber = checkout.CustomerPhone,
            Governorate = checkout.Governorate,
            City = checkout.City,
            District = checkout.District
        });

        // إنشاء عناصر الطلب (بدون Product حتى لا يحاول EF إعادة حفظه)
        foreach (var item in cart.Cartitems)
        {
            _context.Orderitems.Add(new Orderitem
            {
                Id = currentOrderItemId++,
                Orderid = order.Id,
                Productid = item.Productid,
                Quantity = item.Quantity,
                Unitprice = item.Product?.Price ?? 0
                // لا نضع Product هنا حتى لا يتعارض EF Core مع بيانات المنتجات الموجودة
            });
        }

        // حفظ الطلب وعناصره وبيانات التوصيل دفعة واحدة
        await _context.SaveChangesAsync();

        // تفريغ السلة بعد حفظ الطلب بنجاح
        await _cartService.ClearCartAsync(userId);

        return order;
    }

    private static void ValidateCheckout(CheckoutVM checkout)
    {
        if (checkout == null)
            throw new ArgumentNullException(nameof(checkout));

        if (string.IsNullOrWhiteSpace(checkout.CustomerName))
            throw new InvalidOperationException("اسم العميل مطلوب.");

        if (string.IsNullOrWhiteSpace(checkout.CustomerPhone))
            throw new InvalidOperationException("رقم الجوال مطلوب.");

        if (string.IsNullOrWhiteSpace(checkout.Governorate))
            throw new InvalidOperationException("المحافظة مطلوبة.");

        if (string.IsNullOrWhiteSpace(checkout.City))
            throw new InvalidOperationException("المدينة مطلوبة.");

        if (string.IsNullOrWhiteSpace(checkout.District))
            throw new InvalidOperationException("الحي مطلوب.");

        if (string.IsNullOrWhiteSpace(checkout.Street))
            throw new InvalidOperationException("الشارع أو العنوان مطلوب.");

        if (string.IsNullOrWhiteSpace(checkout.PaymentMethod) || !AllowedPaymentMethods.Contains(checkout.PaymentMethod))
            throw new InvalidOperationException("الرجاء اختيار طريقة دفع صحيحة.");
    }

    public Task<IEnumerable<Order>> GetUserOrdersAsync(int userId)
    {
        var orders = _context.Orders.Where(o => o.Userid == userId).OrderByDescending(o => o.Orderdate).ToList();
        foreach (var order in orders)
        {
            order.Orderitems = _context.Orderitems.Where(oi => oi.Orderid == order.Id).ToList();
            foreach (var item in order.Orderitems)
            {
                item.Product = _context.Products.FirstOrDefault(p => p.Id == item.Productid);
            }
        }
        return Task.FromResult(orders.AsEnumerable());
    }

    public Task<Order?> GetOrderByIdAsync(int orderId)
    {
        var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order != null)
        {
            order.Orderitems = _context.Orderitems.Where(oi => oi.Orderid == order.Id).ToList();
            foreach (var item in order.Orderitems)
            {
                item.Product = _context.Products.FirstOrDefault(p => p.Id == item.Productid);
            }
        }
        return Task.FromResult(order);
    }
}
