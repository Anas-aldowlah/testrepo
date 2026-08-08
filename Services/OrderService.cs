using System.Data;
using Microsoft.EntityFrameworkCore;

using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class OrderService
{
    private readonly NeondbContext _context;

    private static readonly HashSet<string> FulfillingStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Processed", "Shipped", "Delivered" };

    private static readonly HashSet<string> ReleasingStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Pending", "Cancelled", "Refunded" };

    public OrderService(NeondbContext context)
    {
        _context = context;
    }

    public async Task<Order> CreateOrderAsync(
        int userId,
        CheckoutVM checkout,
        string? receiptUrl = null,
        CancellationToken cancellationToken = default)
    {
        await ValidateCheckoutAsync(checkout, cancellationToken);

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            var cart = await _context.Carts
                .Include(c => c.Cartitems)
                .SingleOrDefaultAsync(c => c.Userid == userId, cancellationToken);

            if (cart == null || cart.Cartitems.Count == 0)
                throw new InvalidOperationException("السلة فارغة - لا يمكن إنشاء طلب بدون منتجات");

            // Checkout only validates availability; stock is not reserved or changed
            // until an admin moves the order into a fulfilling state. A single
            // non-locking read avoids waiting behind inventory update transactions.
            var productIds = cart.Cartitems
                .Select(item => item.Productid)
                .Distinct()
                .ToArray();
            var products = await _context.Products
                .AsNoTracking()
                .Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);

            if (products.Count != productIds.Length)
                throw new InvalidOperationException("أحد منتجات السلة لم يعد متوفراً.");

            foreach (var item in cart.Cartitems)
            {
                var product = products[item.Productid];
                if (product.Stockquantity < item.Quantity)
                    throw new InvalidOperationException($"المنتج '{product.Name}' غير متوفر بالكمية المطلوبة");
            }

            var order = new Order
            {
                Userid = userId,
                Orderdate = DateTime.UtcNow,
                Status = "Pending",
                Stockdeducted = false,
                Totalamount = cart.Cartitems.Sum(i => i.Quantity * products[i.Productid].Price),
                Trackingnumber = $"YAG-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Paymentmethod = checkout.PaymentMethod,
                Paymentstatus = receiptUrl != null ? "Pending" : "Unpaid",
                Receipturl = receiptUrl,
                Notes = checkout.DeliveryNotes
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);

            _context.Deliveryorders.Add(new Deliveryorder
            {
                Orderid = order.Id,
                Fullname = checkout.CustomerName,
                Phonenumber = checkout.CustomerPhone,
                Governorate = checkout.Governorate,
                City = checkout.City,
                District = checkout.District,
                Secondphonenumber = checkout.Street
            });

            foreach (var item in cart.Cartitems)
            {
                var product = products[item.Productid];

                _context.Orderitems.Add(new Orderitem
                {
                    Orderid = order.Id,
                    Productid = item.Productid,
                    Quantity = item.Quantity,
                    Unitprice = product.Price
                });
            }

            _context.Cartitems.RemoveRange(cart.Cartitems);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return order;
        });
    }

    public static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant() switch
        {
            "pending" or "قيد الانتظار" => "Pending",
            "processed" or "processing" or "قيد المعالجة" => "Processed",
            "shipped" or "تم الشحن" => "Shipped",
            "delivered" or "completed" or "تم التوصيل" => "Delivered",
            "cancelled" or "canceled" or "ملغي" => "Cancelled",
            "refunded" or "مرتجع" => "Refunded",
            _ => null
        };
    }

    public async Task<bool> UpdateStatusAsync(
        int orderId,
        string requestedStatus,
        string? paymentStatus = null)
    {
        var status = NormalizeStatus(requestedStatus)
            ?? throw new ArgumentException("Invalid order status.", nameof(requestedStatus));

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // Serialize status changes for this order so two administrators cannot
            // independently deduct or restore the same stock.
            var order = await _context.Orders
                .FromSqlInterpolated($"SELECT * FROM orders WHERE id = {orderId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (order == null)
            {
                await transaction.CommitAsync();
                return false;
            }

            var orderItems = await _context.Orderitems
                .Where(item => item.Orderid == orderId)
                .ToListAsync();

            if (FulfillingStatuses.Contains(status) && !order.Stockdeducted)
            {
                var products = await LockProductsAsync(orderItems);
                foreach (var quantity in RequiredQuantities(orderItems))
                {
                    var product = products[quantity.Key];
                    if (product.Stockquantity < quantity.Value)
                    {
                        throw new InvalidOperationException(
                            $"The product '{product.Name}' does not have sufficient stock.");
                    }
                }

                foreach (var quantity in RequiredQuantities(orderItems))
                    products[quantity.Key].Stockquantity -= quantity.Value;

                order.Stockdeducted = true;
            }
            else if (ReleasingStatuses.Contains(status) && order.Stockdeducted)
            {
                // Returning a fulfilling order to Pending releases its reserved stock.
                // The persisted flag makes repeated Pending/Cancelled/Refunded updates no-ops.
                var products = await LockProductsAsync(orderItems);
                foreach (var quantity in RequiredQuantities(orderItems))
                    products[quantity.Key].Stockquantity += quantity.Value;

                order.Stockdeducted = false;
            }

            order.Status = status;
            order.TimeState = DateTime.Now;
            if (paymentStatus != null)
                order.Paymentstatus = paymentStatus;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        });
    }

    private async Task<Dictionary<int, Product>> LockProductsAsync(IEnumerable<Orderitem> orderItems)
    {
        var products = new Dictionary<int, Product>();
        foreach (var productId in orderItems.Select(item => item.Productid).Distinct().OrderBy(id => id))
        {
            var product = await _context.Products
                .FromSqlInterpolated($"SELECT * FROM products WHERE id = {productId} FOR UPDATE")
                .SingleOrDefaultAsync()
                ?? throw new InvalidOperationException($"Product {productId} no longer exists.");

            products.Add(productId, product);
        }

        return products;
    }

    private static IReadOnlyDictionary<int, int> RequiredQuantities(IEnumerable<Orderitem> orderItems) =>
        orderItems
            .GroupBy(item => item.Productid)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

    private async Task ValidateCheckoutAsync(
        CheckoutVM checkout,
        CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(checkout.PaymentMethod) ||
            !await IsAllowedPaymentMethodAsync(checkout.PaymentMethod, cancellationToken))
            throw new InvalidOperationException("الرجاء اختيار طريقة دفع صحيحة.");
    }

    private async Task<bool> IsAllowedPaymentMethodAsync(
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        var normalized = paymentMethod.Trim().ToLowerInvariant();
        if (normalized is "al-amqi" or "bin-dawl" or "al-basiri" or "other")
        {
            return true;
        }

        return await _context.Paymentmethods.AnyAsync(
            method => method.Isactive && method.Type.ToLower() == normalized,
            cancellationToken);
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .Where(order => order.Userid == userId)
            .Include(order => order.Orderitems)
                .ThenInclude(item => item.Product)
            .OrderByDescending(order => order.Orderdate)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetOrderByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default) =>
        _context.Orders
            .AsNoTracking()
            .Include(order => order.Orderitems)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
}
