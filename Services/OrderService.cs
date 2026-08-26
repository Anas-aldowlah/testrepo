using System.Data;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public enum ContinueInventoryConflictOutcome
{
    NotFound,
    Continued,
    CancellationRequired
}

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

    public async Task<bool> UpdateStatusAsync(int orderId, string requestedStatus)
    {
        var status = NormalizeStatus(requestedStatus)
            ?? throw new ArgumentException("Invalid order status.", nameof(requestedStatus));

        return await UpdateOrderStateAsync(orderId, _ => status, status);
    }

    public Task<bool> VerifyPaymentAsync(
        int orderId,
        int verifyingAdminUserId,
        CancellationToken cancellationToken = default)
    {
        if (verifyingAdminUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(verifyingAdminUserId));

        return ExecuteLockedOrderTransitionAsync(
            orderId,
            "verify-payment",
            async order =>
            {
                if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                    order.Stockdeducted ||
                    order.Paymentverifiedat.HasValue ||
                    order.Paymentverifiedbyuserid.HasValue ||
                    !string.IsNullOrWhiteSpace(order.Workflowstate) ||
                    order.Paymentstatus is not ("Unpaid" or "Pending"))
                {
                    throw new InvalidOperationException("لا يمكن التحقق من دفع الطلب في حالته الحالية.");
                }

                var orderItems = await LoadOrderItemsAsync(order.Id, cancellationToken);
                var products = await LockProductsAsync(orderItems, cancellationToken);
                var allocation = BuildAllocation(orderItems, products);
                var hasConflict = allocation.Any(line => line.UnavailableQuantity > 0);

                order.Paymentstatus = "Paid";
                order.Paymentverifiedat = DateTime.Now;
                order.Paymentverifiedbyuserid = verifyingAdminUserId;
                order.TimeState = DateTime.Now;

                if (hasConflict)
                {
                    foreach (var item in orderItems)
                    {
                        var result = allocation.Single(line => line.OrderitemId == item.Id);
                        item.FulfilledQuantity = null;
                        item.UnavailableQuantity = result.UnavailableQuantity;
                    }

                    order.Workflowstate = OrderWorkflowStates.ConflictAwaitingDecision;
                    order.Finalfulfilledamount = null;
                    order.Refundrequiredamount = null;
                    order.Refundreason = null;
                    return true;
                }

                await DeductAllocationAsync(allocation, cancellationToken);
                foreach (var item in orderItems)
                {
                    item.FulfilledQuantity = item.Quantity;
                    item.UnavailableQuantity = 0;
                }

                order.Status = "Processed";
                order.Stockdeducted = true;
                order.Finalfulfilledamount = order.Totalamount;
                order.Refundrequiredamount = 0m;
                order.Refundreason = null;
                return true;
            },
            cancellationToken);
    }

    public Task<bool> CancelInventoryConflictAsync(
        int orderId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteLockedOrderTransitionAsync(
            orderId,
            "cancel-inventory-conflict",
            order =>
            {
                if (order.Userid != customerUserId)
                    return Task.FromResult(false);

                ValidateAwaitingCustomerDecision(order);
                foreach (var item in order.Orderitems)
                    item.FulfilledQuantity = 0;

                order.Status = "Cancelled";
                order.Workflowstate = OrderWorkflowStates.ConflictResolvedCancel;
                order.Finalfulfilledamount = 0m;
                order.Refundrequiredamount = order.Totalamount;
                order.Refundreason = OrderWorkflowStates.FullCancellationRefund;
                order.TimeState = DateTime.Now;
                return Task.FromResult(true);
            },
            cancellationToken,
            includeOrderItems: true);
    }

    public async Task<ContinueInventoryConflictOutcome> ContinueInventoryConflictAsync(
        int orderId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        var outcome = ContinueInventoryConflictOutcome.Continued;
        var found = await ExecuteLockedOrderTransitionAsync(
            orderId,
            "continue-inventory-conflict",
            async order =>
            {
                outcome = ContinueInventoryConflictOutcome.Continued;
                if (order.Userid != customerUserId)
                    return false;

                ValidateAwaitingCustomerDecision(order);
                var orderItems = order.Orderitems.OrderBy(item => item.Id).ToList();
                if (orderItems.Count == 0)
                    throw new InvalidOperationException("لا يمكن تنفيذ طلب لا يحتوي على منتجات.");

                var products = await LockProductsAsync(orderItems, cancellationToken);
                var allocation = BuildAllocation(orderItems, products);
                if (allocation.All(line => line.FulfillableQuantity == 0))
                {
                    foreach (var item in orderItems)
                    {
                        var result = allocation.Single(line => line.OrderitemId == item.Id);
                        item.FulfilledQuantity = null;
                        item.UnavailableQuantity = result.UnavailableQuantity;
                    }

                    outcome = ContinueInventoryConflictOutcome.CancellationRequired;
                    return true;
                }

                await DeductAllocationAsync(allocation, cancellationToken);
                foreach (var item in orderItems)
                {
                    var result = allocation.Single(line => line.OrderitemId == item.Id);
                    item.FulfilledQuantity = result.FulfillableQuantity;
                    item.UnavailableQuantity = result.UnavailableQuantity;
                }

                order.Status = "Processed";
                order.Stockdeducted = true;
                order.Workflowstate = OrderWorkflowStates.ConflictResolvedContinue;
                order.Refundrequiredamount = OrderInventoryAllocator.CalculateRefund(allocation);
                order.Refundreason = order.Refundrequiredamount > 0m
                    ? OrderWorkflowStates.PartialUnavailableRefund
                    : null;
                order.Finalfulfilledamount = OrderInventoryAllocator.CalculateFulfilledValue(allocation);
                order.TimeState = DateTime.Now;
                return true;
            },
            cancellationToken,
            includeOrderItems: true);

        return found ? outcome : ContinueInventoryConflictOutcome.NotFound;
    }

    private async Task<bool> UpdateOrderStateAsync(
        int orderId,
        Func<Order, string> resolveStatus,
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
                    if (string.Equals(order.Workflowstate, OrderWorkflowStates.ConflictAwaitingDecision, StringComparison.Ordinal))
                        throw new InvalidOperationException("الطلب بانتظار قرار العميل بشأن تعارض المخزون.");
                    if (!string.IsNullOrWhiteSpace(order.Workflowstate) && ReleasingStatuses.Contains(status))
                        throw new InvalidOperationException("لا يمكن تجاوز مسار تعارض المخزون بإجراء إداري عام.");
                    if (FulfillingStatuses.Contains(status) &&
                        !FulfillingStatuses.Contains(order.Status) &&
                        (!string.Equals(order.Paymentstatus, "Paid", StringComparison.Ordinal) ||
                         !order.Paymentverifiedat.HasValue ||
                         !order.Paymentverifiedbyuserid.HasValue))
                    {
                        throw new InvalidOperationException("يجب التحقق من الدفع أولاً عبر الإجراء المخصص.");
                    }
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

                        var products = await LockProductsAsync(orderItems, CancellationToken.None);
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

    private async Task<Dictionary<int, Product>> LockProductsAsync(
        IEnumerable<Orderitem> orderItems,
        CancellationToken cancellationToken)
    {
        var products = new Dictionary<int, Product>();
        foreach (var productId in orderItems.Select(item => item.Productid).Distinct().OrderBy(id => id))
        {
            var product = await _context.Products
                .FromSqlInterpolated($"SELECT * FROM products WHERE id = {productId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
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
                var quantity = item.FulfilledQuantity ?? item.Quantity;
                if (quantity == 0)
                    continue;

                var amount = _inventoryService.CalculateDeductionAmount(
                    products[item.Productid],
                    quantity,
                    item.RetailSizeMl);
                totals[item.Productid] = totals.GetValueOrDefault(item.Productid) + amount;
            }
        }

        return totals;
    }

    private async Task<List<Orderitem>> LoadOrderItemsAsync(int orderId, CancellationToken cancellationToken)
    {
        var orderItems = await _context.Orderitems
            .Where(item => item.Orderid == orderId)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (orderItems.Count == 0)
            throw new InvalidOperationException("لا يمكن تنفيذ طلب لا يحتوي على منتجات.");

        return orderItems;
    }

    private IReadOnlyList<OrderAllocationResult> BuildAllocation(
        IEnumerable<Orderitem> orderItems,
        IReadOnlyDictionary<int, Product> products)
    {
        var lines = orderItems.Select(item => new OrderAllocationLine(
            item.Id,
            item.Productid,
            item.Quantity,
            _inventoryService.CalculateDeductionAmount(products[item.Productid], 1, item.RetailSizeMl),
            item.Unitprice));
        var stock = products.ToDictionary(pair => pair.Key, pair => pair.Value.Stockquantity);
        return OrderInventoryAllocator.Allocate(lines, stock);
    }

    private async Task DeductAllocationAsync(
        IEnumerable<OrderAllocationResult> allocation,
        CancellationToken cancellationToken)
    {
        var deductions = allocation
            .Where(line => line.FulfillableQuantity > 0)
            .GroupBy(line => line.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(line => checked(line.FulfillableQuantity * line.StockPerUnit)));

        foreach (var deduction in deductions.OrderBy(pair => pair.Key))
            await _inventoryService.DeductStockAsync(deduction.Key, deduction.Value, cancellationToken);
    }

    private static void ValidateAwaitingCustomerDecision(Order order)
    {
        if (!string.Equals(order.Status, "Pending", StringComparison.Ordinal) ||
            !string.Equals(order.Paymentstatus, "Paid", StringComparison.Ordinal) ||
            !order.Paymentverifiedat.HasValue ||
            !order.Paymentverifiedbyuserid.HasValue ||
            order.Stockdeducted ||
            !string.Equals(order.Workflowstate, OrderWorkflowStates.ConflictAwaitingDecision, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("لم يعد الطلب متاحاً لاتخاذ هذا القرار.");
        }
    }

    private async Task<bool> ExecuteLockedOrderTransitionAsync(
        int orderId,
        string operationName,
        Func<Order, Task<bool>> transition,
        CancellationToken cancellationToken,
        bool includeOrderItems = false)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                try
                {
                    var query = _context.Orders
                        .FromSqlInterpolated($"SELECT * FROM orders WHERE id = {orderId} FOR UPDATE");
                    if (includeOrderItems)
                        query = query.Include(order => order.Orderitems);

                    var order = await query.SingleOrDefaultAsync(cancellationToken);
                    if (order == null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        return false;
                    }

                    var succeeded = await transition(order);
                    if (!succeeded)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        return false;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Order transition {Operation} failed for order {OrderId}.", operationName, orderId);
            throw;
        }
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
