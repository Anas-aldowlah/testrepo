using System.Data;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class OrderService
{
    private readonly NeondbContext _context;
    private readonly CartLockService _cartLock;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<OrderService> _logger;

    private static readonly HashSet<string> FulfillingStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Processed", "Shipped", "Delivered" };

    private static readonly HashSet<string> ReleasingStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Pending", "Cancelled", "Refunded" };

    public OrderService(
        NeondbContext context,
        CartLockService cartLock,
        IInventoryService inventoryService,
        ILogger<OrderService> logger)
    {
        _context = context;
        _cartLock = cartLock;
        _inventoryService = inventoryService;
        _logger = logger;
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
            try
            {
                await _cartLock.AcquireAsync(userId, cancellationToken);

                var cart = await _context.Carts
                    .Include(c => c.Cartitems)
                        .ThenInclude(item => item.RetailPrice)
                    .SingleOrDefaultAsync(c => c.Userid == userId, cancellationToken);

                if (cart == null || cart.Cartitems.Count == 0)
                    throw new InvalidOperationException("السلة فارغة - لا يمكن إنشاء طلب بدون منتجات");

                var products = await LockCheckoutProductsAsync(
                    cart.Cartitems.Select(item => item.Productid),
                    cancellationToken);

                ValidateSubmittedCart(checkout, cart.Cartitems, products);

                var orderTotal = CalculateOrderTotal(cart.Cartitems, products);

                var order = new Order
                {
                    Userid = userId,
                    Orderdate = DateTime.UtcNow,
                    Status = "Pending",
                    // Pending orders do not reserve inventory. Stock is claimed only
                    // when an admin moves the order into a fulfilling status.
                    Stockdeducted = false,
                    Totalamount = orderTotal,
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
                    _context.Orderitems.Add(new Orderitem
                    {
                        Orderid = order.Id,
                        Productid = item.Productid,
                        RetailPriceId = item.RetailPriceId,
                        RetailSizeMl = item.RetailPrice?.SizeMl,
                        Quantity = item.Quantity,
                        Unitprice = _inventoryService.GetUnitPrice(products[item.Productid], item.RetailPrice)
                    });
                }

                _context.Cartitems.RemoveRange(cart.Cartitems);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return order;
            }
            catch (Exception exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogWarning(rollbackException, "Rollback failed after checkout for user {UserId}.", userId);
                }

                _context.ChangeTracker.Clear();
                if (exception is InvalidOperationException or CartConcurrencyException)
                    _logger.LogWarning(exception, "Checkout was rejected for user {UserId}.", userId);
                else
                    _logger.LogError(exception, "Checkout failed for user {UserId}.", userId);

                throw;
            }
        });
    }

    private async Task<Dictionary<int, Product>> LockCheckoutProductsAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken)
    {
        var products = new Dictionary<int, Product>();
        foreach (var productId in productIds.Distinct().OrderBy(id => id))
        {
            var product = await _context.Products
                .FromSqlInterpolated($"SELECT * FROM products WHERE id = {productId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("تغيرت محتويات السلة. يرجى تحديث صفحة إتمام الطلب والمحاولة مرة أخرى.");

            products.Add(productId, product);
        }

        return products;
    }

    private void ValidateSubmittedCart(
        CheckoutVM checkout,
        IEnumerable<Cartitem> cartItems,
        IReadOnlyDictionary<int, Product> products)
    {
        const string staleCartMessage =
            "تغيرت محتويات السلة أو الأسعار. يرجى تحديث صفحة إتمام الطلب والمحاولة مرة أخرى.";

        var submittedCartItems = checkout.SubmittedCartItems;
        if (submittedCartItems == null || submittedCartItems.Count == 0)
            throw new InvalidOperationException(staleCartMessage);

        var submittedItems = submittedCartItems
            .GroupBy(item => new { item.ProductId, item.RetailPriceId })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.RetailPriceId,
                Quantity = CheckedQuantitySum(group.Select(item => item.Quantity)),
                UnitPrices = group.Select(item => item.UnitPrice).Distinct().ToArray()
            })
            .OrderBy(item => item.ProductId)
            .ThenBy(item => item.RetailPriceId ?? 0)
            .ToArray();

        var currentItems = cartItems
            .GroupBy(item => new { item.Productid, item.RetailPriceId })
            .Select(group =>
            {
                var sample = group.First();
                return new
                {
                    ProductId = group.Key.Productid,
                    group.Key.RetailPriceId,
                    Quantity = CheckedQuantitySum(group.Select(item => item.Quantity)),
                    UnitPrice = _inventoryService.GetUnitPrice(products[group.Key.Productid], sample.RetailPrice)
                };
            })
            .OrderBy(item => item.ProductId)
            .ThenBy(item => item.RetailPriceId ?? 0)
            .ToArray();

        if (submittedItems.Length != currentItems.Length)
            throw new InvalidOperationException(staleCartMessage);

        for (var index = 0; index < currentItems.Length; index++)
        {
            var submitted = submittedItems[index];
            var current = currentItems[index];
            if (submitted.ProductId != current.ProductId ||
                submitted.RetailPriceId != current.RetailPriceId ||
                submitted.Quantity <= 0 ||
                submitted.Quantity != current.Quantity ||
                submitted.UnitPrices.Length != 1 ||
                submitted.UnitPrices[0] != current.UnitPrice)
            {
                throw new InvalidOperationException(staleCartMessage);
            }
        }

        decimal currentTotal = 0m;
        checked
        {
            foreach (var item in currentItems)
                currentTotal += item.Quantity * item.UnitPrice;
        }
        if (checkout.SubmittedCartTotal != currentTotal)
            throw new InvalidOperationException(staleCartMessage);
    }

    public static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant() switch
        {
            "pending" or "قيد الانتظار" => "Pending",
            "processed" or "processing" or "قيد المعالجة" or "تم الدفع" or "جاري التجهيز" => "Processed",
            "shipped" or "تم الشحن" => "Shipped",
            "delivered" or "completed" or "تم التوصيل" or "مكتمل" => "Delivered",
            "cancelled" or "canceled" or "ملغي" => "Cancelled",
            "refunded" or "مرتجع" or "مسترجع" => "Refunded",
            _ => null
        };
    }

    public async Task<bool> UpdateStatusAsync(int orderId, string requestedStatus, string? paymentStatus = null)
    {
        var status = NormalizeStatus(requestedStatus)
            ?? throw new ArgumentException("Invalid order status.", nameof(requestedStatus));

        return await UpdateOrderStateAsync(orderId, _ => status, paymentStatus, status);
    }

    public async Task<bool> UpdatePaymentStatusAsync(int orderId, string paymentStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentStatus);

        return await UpdateOrderStateAsync(
            orderId,
            order => paymentStatus switch
            {
                // Paying a Pending order is the inventory-claiming transition.
                "Paid" when string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase) => "Processed",
                "Paid" when FulfillingStatuses.Contains(order.Status) => order.Status,
                "Paid" => throw new InvalidOperationException("لا يمكن تعليم طلب ملغي أو مسترجع كمدفوع."),
                "Refunded" => "Refunded",
                _ => order.Status
            },
            paymentStatus,
            $"payment:{paymentStatus}");
    }

    private async Task<bool> UpdateOrderStateAsync(
        int orderId,
        Func<Order, string> resolveStatus,
        string? paymentStatus,
        string operationName)
    {

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var order = await _context.Orders
                        .FromSqlInterpolated($"SELECT * FROM orders WHERE id = {orderId} FOR UPDATE")
                        .SingleOrDefaultAsync();

                    if (order == null)
                    {
                        await transaction.CommitAsync();
                        return false;
                    }

                    var status = resolveStatus(order);
                    var statusChanged = !string.Equals(order.Status, status, StringComparison.OrdinalIgnoreCase);
                    var shouldDeductStock = statusChanged &&
                                            FulfillingStatuses.Contains(status) &&
                                            !order.Stockdeducted;
                    var shouldRestoreStock = statusChanged &&
                                             ReleasingStatuses.Contains(status) &&
                                             order.Stockdeducted;

                    if (!statusChanged && FulfillingStatuses.Contains(status) && !order.Stockdeducted)
                    {
                        throw new InvalidOperationException("حالة الطلب غير متسقة: الطلب منفذ دون خصم المخزون.");
                    }

                    if (shouldDeductStock || shouldRestoreStock)
                    {
                        var orderItems = await _context.Orderitems
                            .Where(item => item.Orderid == orderId)
                            .ToListAsync();
                        if (orderItems.Count == 0)
                            throw new InvalidOperationException("لا يمكن تحديث المخزون لأن الطلب لا يحتوي على منتجات.");

                        var products = await LockProductsAsync(orderItems);
                        var requiredAmounts = BuildOrderDeductions(orderItems, products);

                        foreach (var amount in requiredAmounts)
                        {
                            if (shouldDeductStock)
                                await _inventoryService.DeductStockAsync(amount.Key, amount.Value);
                            else
                                await _inventoryService.RestoreStockAsync(amount.Key, amount.Value);
                        }

                        order.Stockdeducted = shouldDeductStock;
                    }

                    order.Status = status;
                    order.TimeState = DateTime.Now;
                    if (paymentStatus != null)
                        order.Paymentstatus = paymentStatus;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                    catch (Exception rollbackException)
                    {
                        _logger.LogWarning(rollbackException, "Rollback failed while updating order {OrderId}.", orderId);
                    }

                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Updating order {OrderId} with operation {Operation} failed.", orderId, operationName);
            throw;
        }
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

    private Dictionary<int, int> BuildOrderDeductions(
        IEnumerable<Orderitem> orderItems,
        IReadOnlyDictionary<int, Product> products)
    {
        var totals = new Dictionary<int, int>();
        checked
        {
            foreach (var item in orderItems)
            {
                var amount = _inventoryService.CalculateDeductionAmount(
                    products[item.Productid],
                    item.Quantity,
                    item.RetailSizeMl);
                totals[item.Productid] = totals.GetValueOrDefault(item.Productid) + amount;
            }
        }

        return totals;
    }

    private decimal CalculateOrderTotal(
        IEnumerable<Cartitem> cartItems,
        IReadOnlyDictionary<int, Product> products)
    {
        decimal total = 0m;
        checked
        {
            foreach (var item in cartItems)
                total += item.Quantity * _inventoryService.GetUnitPrice(products[item.Productid], item.RetailPrice);
        }

        if (total > 1000000.00m)
            throw new OverflowException("Order total exceeds the allowed currency limit.");

        return total;
    }

    private static int CheckedQuantitySum(IEnumerable<int> quantities)
    {
        var total = 0;
        checked
        {
            foreach (var quantity in quantities)
                total += quantity;
        }

        return total;
    }

    private async Task ValidateCheckoutAsync(CheckoutVM checkout, CancellationToken cancellationToken)
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

    private async Task<bool> IsAllowedPaymentMethodAsync(string paymentMethod, CancellationToken cancellationToken)
    {
        var normalized = paymentMethod.Trim().ToLowerInvariant();
        if (normalized is "al-amqi" or "bin-dawl" or "al-basiri" or "other")
            return true;

        return await _context.Paymentmethods.AnyAsync(
            method => method.Isactive && method.Type.ToLower() == normalized,
            cancellationToken);
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .Where(order => order.Userid == userId)
            .Include(order => order.Orderitems)
                .ThenInclude(item => item.Product)
            .Include(order => order.Orderitems)
                .ThenInclude(item => item.RetailPrice)
            .OrderByDescending(order => order.Orderdate)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken = default) =>
        _context.Orders
            .AsNoTracking()
            .Include(order => order.Orderitems)
                .ThenInclude(item => item.Product)
            .Include(order => order.Orderitems)
                .ThenInclude(item => item.RetailPrice)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
}
