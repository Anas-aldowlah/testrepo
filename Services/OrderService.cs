using System.Data;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public enum PaymentVerificationOutcome
{
    NotFound,
    Paid,
    Conflict
}

public enum CustomerInventoryConflictOutcome
{
    NotFound,
    Cancelled,
    Paid,
    Conflict
}

public class OrderService
{
    private readonly NeondbContext _context;
    private readonly CartLockService _cartLock;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<OrderService> _logger;

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

    public static string? NormalizeStatus(string? status) => OrderStatusPolicy.Normalize(status);

    public async Task<bool> UpdateStatusAsync(int orderId, string requestedStatus)
    {
        var status = NormalizeStatus(requestedStatus)
            ?? throw new ArgumentException("Invalid order status.", nameof(requestedStatus));

        return await ExecuteLockedOrderTransitionAsync(
            orderId,
            $"status-{status}",
            async order =>
            {
                if (!OrderStatusPolicy.CanTransition(order.Status, status))
                    throw new InvalidOperationException("انتقال حالة الطلب غير مسموح.");

                if (string.Equals(order.Status, status, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (status == OrderStatuses.Cancelled)
                {
                    var workflowState = order.Workflowstate == OrderWorkflowStates.ConflictAwaitingDecision
                        ? OrderWorkflowStates.ConflictResolvedCancel
                        : null;
                    await CancelLockedOrderAsync(order, workflowState, CancellationToken.None);
                    return true;
                }

                if (!order.Stockdeducted)
                    throw new InvalidOperationException("حالة الطلب غير متسقة: لا يمكن تقدم طلب لم يُخصم مخزونه.");

                order.Status = status;
                order.TimeState = DateTime.UtcNow;
                return true;
            },
            CancellationToken.None,
            includeOrderItems: status == OrderStatuses.Cancelled);
    }

    public async Task<PaymentVerificationOutcome> VerifyPaymentAsync(
        int orderId,
        int verifyingAdminUserId,
        CancellationToken cancellationToken = default)
    {
        if (verifyingAdminUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(verifyingAdminUserId));

        var outcome = PaymentVerificationOutcome.Paid;
        var found = await ExecuteLockedOrderTransitionAsync(
            orderId,
            "verify-payment",
            async order =>
            {
                var isLegacyConflictAwaitingReview =
                    string.Equals(order.Workflowstate, OrderWorkflowStates.ConflictAwaitingDecision, StringComparison.Ordinal) &&
                    !order.Paymentreviewedat.HasValue &&
                    !order.Paymentreviewedbyuserid.HasValue;
                if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                    order.Stockdeducted ||
                    order.Paymentverifiedat.HasValue ||
                    order.Paymentverifiedbyuserid.HasValue ||
                    (!isLegacyConflictAwaitingReview &&
                        (order.Paymentreviewedat.HasValue ||
                         order.Paymentreviewedbyuserid.HasValue ||
                         !string.IsNullOrWhiteSpace(order.Workflowstate))) ||
                    order.Paymentstatus is not ("Unpaid" or "Pending"))
                {
                    throw new InvalidOperationException("لا يمكن التحقق من دفع الطلب في حالته الحالية.");
                }

                var reviewedAt = DateTime.UtcNow;
                if (isLegacyConflictAwaitingReview)
                {
                    order.Paymentreviewedat = reviewedAt;
                    order.Paymentreviewedbyuserid = verifyingAdminUserId;
                    order.Paymentverifiedat = null;
                    order.Paymentverifiedbyuserid = null;
                    order.Finalfulfilledamount = null;
                    order.TimeState = reviewedAt;
                    outcome = PaymentVerificationOutcome.Conflict;
                    return true;
                }

                var orderItems = await LoadOrderItemsAsync(order.Id, cancellationToken);
                var products = await LockProductsAsync(orderItems, cancellationToken);
                var allocation = BuildAllocation(orderItems, products);
                var hasConflict = allocation.Any(line => line.UnavailableQuantity > 0);
                order.Paymentreviewedat = reviewedAt;
                order.Paymentreviewedbyuserid = verifyingAdminUserId;

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
                    order.Paymentstatus = order.Receipturl == null ? "Unpaid" : "Pending";
                    order.Paymentverifiedat = null;
                    order.Paymentverifiedbyuserid = null;
                    order.TimeState = DateTime.UtcNow;
                    outcome = PaymentVerificationOutcome.Conflict;
                    return true;
                }

                await DeductAllocationAsync(allocation, cancellationToken);
                foreach (var item in orderItems)
                {
                    item.FulfilledQuantity = item.Quantity;
                    item.UnavailableQuantity = 0;
                }

                order.Status = OrderStatuses.Paid;
                order.Stockdeducted = true;
                order.Finalfulfilledamount = order.Totalamount;
                order.Paymentstatus = "Paid";
                order.Paymentverifiedat = reviewedAt;
                order.Paymentverifiedbyuserid = verifyingAdminUserId;
                order.Workflowstate = null;
                order.TimeState = DateTime.UtcNow;
                return true;
            },
            cancellationToken);

        return found ? outcome : PaymentVerificationOutcome.NotFound;
    }

    public static CustomerInventoryConflictViewModel? BuildCustomerInventoryConflict(Order order)
    {
        if (!string.Equals(order.Status, OrderStatuses.Pending, StringComparison.Ordinal) ||
            !string.Equals(order.Workflowstate, OrderWorkflowStates.ConflictAwaitingDecision, StringComparison.Ordinal))
        {
            return null;
        }

        var orderedItems = order.Orderitems.OrderBy(item => item.Id).ToArray();
        var conflictedItems = orderedItems
            .Where(item => (item.UnavailableQuantity ?? 0) > 0)
            .ToArray();
        if (conflictedItems.Length == 0)
            return null;

        var conflictedIds = conflictedItems.Select(item => item.Id).ToHashSet();
        var fixedAcceptedQuantity = orderedItems
            .Where(item => !conflictedIds.Contains(item.Id))
            .Sum(item => item.Quantity);
        var availableByItem = conflictedItems.ToDictionary(
            item => item.Id,
            item => Math.Clamp(
                item.Quantity - (item.UnavailableQuantity ?? item.Quantity),
                0,
                item.Quantity));

        return new CustomerInventoryConflictViewModel
        {
            OrderId = order.Id,
            Products = orderedItems.Select(item => new CustomerInventoryConflictProductViewModel
            {
                OrderitemId = item.Id,
                ProductName = item.Product?.Name ?? "المنتج",
                RequestedQuantity = item.Quantity,
                AvailableQuantity = availableByItem.TryGetValue(item.Id, out var available)
                    ? available
                    : item.Quantity,
                IsConflict = conflictedIds.Contains(item.Id)
            }).ToArray(),
            Lines = conflictedItems.Select(item => new CustomerInventoryConflictLineViewModel
            {
                OrderitemId = item.Id,
                ProductName = item.Product?.Name ?? "المنتج",
                RequestedQuantity = item.Quantity,
                AvailableQuantity = availableByItem[item.Id],
                CanRemove = fixedAcceptedQuantity + availableByItem
                    .Where(pair => pair.Key != item.Id)
                    .Sum(pair => pair.Value) > 0
            }).ToArray()
        };
    }

    public async Task<CustomerInventoryConflictOutcome> ResolveInventoryConflictAsync(
        int orderId,
        int ownerUserId,
        bool cancelEntireOrder,
        IReadOnlyList<InventoryConflictLineDecisionInput> decisions,
        CancellationToken cancellationToken = default)
    {
        if (ownerUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(ownerUserId));

        var outcome = CustomerInventoryConflictOutcome.Paid;
        var found = await ExecuteLockedOrderTransitionAsync(
            orderId,
            "resolve-inventory-conflict",
            async order =>
            {
                if (order.Userid != ownerUserId)
                    return false;

                ValidateAwaitingCustomerDecision(order);
                if (cancelEntireOrder)
                {
                    await CancelLockedOrderAsync(
                        order,
                        OrderWorkflowStates.ConflictResolvedCancel,
                        cancellationToken);
                    outcome = CustomerInventoryConflictOutcome.Cancelled;
                    return true;
                }

                var orderItems = order.Orderitems.OrderBy(item => item.Id).ToList();
                if (orderItems.Count == 0)
                    throw new InvalidOperationException("لا يمكن تنفيذ طلب لا يحتوي على منتجات.");

                if (!order.Paymentreviewedat.HasValue || !order.Paymentreviewedbyuserid.HasValue)
                    throw new InvalidOperationException("تعذر إكمال الطلب الآن. يرجى التواصل مع المتجر.");

                var affectedItems = orderItems
                    .Where(item => (item.UnavailableQuantity ?? 0) > 0)
                    .ToDictionary(item => item.Id);
                if (affectedItems.Count == 0)
                    throw new InvalidOperationException("لم يعد الطلب بحاجة إلى مراجعة.");

                decisions ??= [];
                var decisionGroups = decisions.GroupBy(decision => decision.OrderitemId).ToArray();
                if (decisionGroups.Length != affectedItems.Count ||
                    decisionGroups.Any(group => group.Count() != 1 || !affectedItems.ContainsKey(group.Key)))
                {
                    throw new InvalidOperationException("يرجى اختيار قرار لكل منتج متأثر.");
                }

                var acceptedQuantities = orderItems.ToDictionary(item => item.Id, item => item.Quantity);
                foreach (var group in decisionGroups)
                {
                    var decision = group.Single();
                    var item = affectedItems[group.Key];
                    var availableQuantity = Math.Clamp(
                        item.Quantity - (item.UnavailableQuantity ?? item.Quantity),
                        0,
                        item.Quantity);

                    if (string.Equals(decision.Decision, InventoryConflictDecisions.Remove, StringComparison.Ordinal))
                    {
                        acceptedQuantities[item.Id] = 0;
                    }
                    else if (string.Equals(decision.Decision, InventoryConflictDecisions.Continue, StringComparison.Ordinal) &&
                             availableQuantity > 0)
                    {
                        acceptedQuantities[item.Id] = availableQuantity;
                    }
                    else
                    {
                        throw new InvalidOperationException("أحد الخيارات لم يعد متاحاً. يرجى مراجعة الطلب.");
                    }
                }

                if (acceptedQuantities.Values.All(quantity => quantity == 0))
                    throw new InvalidOperationException("اختر إلغاء الطلب إذا لم ترغب في أي منتج.");

                var products = await LockProductsAsync(orderItems, cancellationToken);
                var allocation = BuildAllocation(orderItems, products, acceptedQuantities);
                if (allocation.Any(line => line.FulfillableQuantity != line.RequestedQuantity))
                {
                    foreach (var item in orderItems)
                    {
                        var result = allocation.Single(line => line.OrderitemId == item.Id);
                        item.FulfilledQuantity = null;
                        item.UnavailableQuantity = item.Quantity - result.FulfillableQuantity;
                    }

                    order.Paymentstatus = order.Receipturl == null ? "Unpaid" : "Pending";
                    order.Paymentverifiedat = null;
                    order.Paymentverifiedbyuserid = null;
                    order.Finalfulfilledamount = null;
                    order.TimeState = DateTime.UtcNow;
                    outcome = CustomerInventoryConflictOutcome.Conflict;
                    return true;
                }

                await DeductAllocationAsync(allocation, cancellationToken);
                foreach (var item in orderItems)
                {
                    var result = allocation.Single(line => line.OrderitemId == item.Id);
                    item.FulfilledQuantity = result.FulfillableQuantity;
                    item.UnavailableQuantity = item.Quantity - result.FulfillableQuantity;
                }

                order.Status = OrderStatuses.Paid;
                order.Stockdeducted = true;
                order.Workflowstate = OrderWorkflowStates.ConflictResolvedContinue;
                order.Finalfulfilledamount = OrderInventoryAllocator.CalculateFulfilledValue(allocation);
                order.Paymentstatus = "Paid";
                order.Paymentverifiedat = DateTime.UtcNow;
                order.Paymentverifiedbyuserid = order.Paymentreviewedbyuserid;
                order.TimeState = DateTime.UtcNow;
                return true;
            },
            cancellationToken,
            includeOrderItems: true);

        return found ? outcome : CustomerInventoryConflictOutcome.NotFound;
    }

    private async Task CancelLockedOrderAsync(
        Order order,
        string? workflowState = null,
        CancellationToken cancellationToken = default)
    {
        if (order.Stockdeducted)
        {
            var orderItems = order.Orderitems.Count > 0
                ? order.Orderitems.OrderBy(item => item.Id).ToList()
                : await LoadOrderItemsAsync(order.Id, cancellationToken);
            var products = await LockProductsAsync(orderItems, cancellationToken);
            foreach (var amount in BuildOrderDeductions(orderItems, products).OrderBy(pair => pair.Key))
                await _inventoryService.RestoreStockAsync(amount.Key, amount.Value, cancellationToken);
            order.Stockdeducted = false;
        }

        foreach (var item in order.Orderitems)
            item.FulfilledQuantity ??= 0;

        order.Status = OrderStatuses.Cancelled;
        order.Workflowstate = workflowState ?? order.Workflowstate;
        order.Finalfulfilledamount ??= 0m;
        order.CancelledAt = DateTime.UtcNow;
        order.TimeState = DateTime.UtcNow;
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
        IReadOnlyDictionary<int, Product> products,
        IReadOnlyDictionary<int, int>? requestedQuantities = null)
    {
        var lines = orderItems.Select(item => new OrderAllocationLine(
            item.Id,
            item.Productid,
            requestedQuantities?.GetValueOrDefault(item.Id) ?? item.Quantity,
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

        return total + ShippingPolicy.CalculateCharge(total);
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

    public async Task<CustomerOrdersIndexViewModel> GetUserOrdersPageAsync(
        int userId,
        string? status,
        string? search,
        int page,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 9;
        var allowedStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            OrderStatuses.Pending,
            OrderStatuses.Paid,
            OrderStatuses.Processed,
            OrderStatuses.Shipped,
            OrderStatuses.Delivered,
            OrderStatuses.Cancelled
        };
        var normalizedStatus = allowedStatuses.Contains(status ?? string.Empty) ? status! : "all";
        var normalizedSearch = (search ?? string.Empty).Trim();
        if (normalizedSearch.Length > 100)
            normalizedSearch = normalizedSearch[..100];

        var customerOrders = _context.Orders
            .AsNoTracking()
            .Where(order => order.Userid == userId);

        var groupedCounts = await customerOrders
            .GroupBy(order => order.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var statusCounts = groupedCounts.ToDictionary(
            item => item.Status,
            item => item.Count,
            StringComparer.Ordinal);

        var filtered = customerOrders;
        if (normalizedStatus != "all")
            filtered = filtered.Where(order => order.Status == normalizedStatus);

        if (normalizedSearch.Length > 0)
        {
            filtered = filtered.Where(order =>
                (order.Trackingnumber != null && order.Trackingnumber.Contains(normalizedSearch)) ||
                order.Orderitems.Any(item => item.Product != null && item.Product.Name.Contains(normalizedSearch)));
        }

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);
        var orders = await filtered
            .OrderByDescending(order => order.Orderdate)
            .ThenByDescending(order => order.Id)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Include(order => order.Orderitems)
                .ThenInclude(item => item.Product)
            .Include(order => order.Orderitems)
                .ThenInclude(item => item.RetailPrice)
            .ToListAsync(cancellationToken);

        return new CustomerOrdersIndexViewModel
        {
            Orders = orders,
            StatusCounts = statusCounts,
            Status = normalizedStatus,
            Search = normalizedSearch,
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
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
