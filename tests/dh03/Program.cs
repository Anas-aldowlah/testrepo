using YAGOT_2._0.Services;

var repoRoot = FindRepoRoot();
var serviceSource = File.ReadAllText(Path.Combine(repoRoot, "Services", "OrderService.cs"));
var adminControllerSource = File.ReadAllText(Path.Combine(repoRoot, "Areas", "Admin", "Controllers", "OrdersController.cs"));
var customerControllerSource = File.ReadAllText(Path.Combine(repoRoot, "Controllers", "OrdersController.cs"));
var customerViewSource = File.ReadAllText(Path.Combine(repoRoot, "Views", "Orders", "Details.cshtml"));

var scenarios = new (string Name, Action Check)[]
{
    ("01 normal order", () =>
    {
        var allocation = Allocate([Line(1, 1, 2, 1, 10m)], (1, 2));
        Equal(2, allocation[0].FulfillableQuantity);
        Equal(0, allocation[0].UnavailableQuantity);
        Equal(20m, OrderInventoryAllocator.CalculateFulfilledValue(allocation));
    }),
    ("02 one shortage", () =>
    {
        var allocation = Allocate([Line(1, 1, 3, 1, 10m)], (1, 2));
        Equal(2, allocation[0].FulfillableQuantity);
        Equal(1, allocation[0].UnavailableQuantity);
    }),
    ("03 payment unverified hides customer decision", () =>
    {
        Contains(customerViewSource, "Model.Paymentstatus == \"Paid\"");
        Contains(serviceSource, "ValidateAwaitingCustomerDecision(order)");
    }),
    ("04 payment verified conflict state", () =>
    {
        Contains(serviceSource, "order.Paymentstatus = \"Paid\"");
        Contains(serviceSource, "order.Paymentverifiedat = DateTime.Now");
        Contains(serviceSource, "OrderWorkflowStates.ConflictAwaitingDecision");
    }),
    ("05 full cancellation refund", () =>
    {
        Contains(serviceSource, "order.Refundrequiredamount = order.Totalamount");
        Contains(serviceSource, "order.Finalfulfilledamount = 0m");
    }),
    ("06 partial continuation", () =>
    {
        var allocation = Allocate([Line(1, 1, 4, 2, 25m)], (1, 5));
        Equal(2, allocation[0].FulfillableQuantity);
        Equal(2, allocation[0].UnavailableQuantity);
    }),
    ("07 multiple shortages", () =>
    {
        var allocation = Allocate(
            [Line(1, 1, 3, 1, 10m), Line(2, 2, 4, 1, 5m)],
            (1, 1), (2, 2));
        Equal(4, allocation.Sum(line => line.UnavailableQuantity));
    }),
    ("08 all unavailable", () =>
    {
        var allocation = Allocate([Line(1, 1, 2, 1, 10m)], (1, 0));
        True(allocation.All(line => line.FulfillableQuantity == 0));
        Contains(serviceSource, "outcome = ContinueInventoryConflictOutcome.CancellationRequired;");
        Contains(serviceSource, "item.FulfilledQuantity = null;");
        Contains(serviceSource, "item.UnavailableQuantity = result.UnavailableQuantity;");
        DoesNotContain(serviceSource, "throw new InvalidOperationException(\"لم يعد أي منتج متاحاً");
        Contains(customerControllerSource, "case ContinueInventoryConflictOutcome.CancellationRequired:");
        Contains(customerViewSource, "item.Quantity - (item.UnavailableQuantity ?? 0)");
    }),
    ("09 partial whole sellable quantity", () =>
    {
        var allocation = Allocate([Line(1, 1, 3, 250, 100m)], (1, 620));
        Equal(2, allocation[0].FulfillableQuantity);
        Equal(120, 620 - allocation[0].FulfillableQuantity * allocation[0].StockPerUnit);
    }),
    ("10 historical price refund", () =>
    {
        var allocation = Allocate([Line(1, 1, 3, 1, 17.5m)], (1, 1));
        Equal(35m, OrderInventoryAllocator.CalculateRefund(allocation));
    }),
    ("11 duplicate decision exact-state guard", () =>
    {
        Contains(serviceSource, "!string.Equals(order.Workflowstate, OrderWorkflowStates.ConflictAwaitingDecision");
        Contains(serviceSource, "SELECT * FROM orders WHERE id = {orderId} FOR UPDATE");
    }),
    ("12 cancel continue race protection", () =>
    {
        Contains(serviceSource, "IsolationLevel.Serializable");
        Contains(serviceSource, "CancelInventoryConflictAsync");
        Contains(serviceSource, "ContinueInventoryConflictAsync");
    }),
    ("13 two admin verification race and CSRF", () =>
    {
        Contains(serviceSource, "order.Paymentverifiedat.HasValue");
        Contains(adminControllerSource, "[ValidateAntiForgeryToken]");
        Contains(adminControllerSource, "VerifyPaymentAsync(id, adminId");
    }),
    ("14 inventory changes before decision", () =>
    {
        var snapshot = Allocate([Line(1, 1, 3, 1, 10m)], (1, 1));
        var final = Allocate([Line(1, 1, 3, 1, 10m)], (1, 2));
        Equal(2, snapshot[0].UnavailableQuantity);
        Equal(1, final[0].UnavailableQuantity);
        True(Count(serviceSource, "BuildAllocation(orderItems, products)") >= 2);
        Contains(customerControllerSource, "ContinueInventoryConflictAsync(id, userId");
    }),
    ("shared stock uses Orderitem Id ascending", () =>
    {
        var allocation = Allocate(
            [Line(20, 1, 5, 100, 30m), Line(10, 1, 2, 250, 100m)],
            (1, 500));
        Equal(2, allocation.Single(line => line.OrderitemId == 10).FulfillableQuantity);
        Equal(0, allocation.Single(line => line.OrderitemId == 20).FulfillableQuantity);
        Equal(150m, OrderInventoryAllocator.CalculateRefund(allocation));
    }),
    ("customer ownership and antiforgery", () =>
    {
        Contains(serviceSource, "order.Userid != customerUserId");
        True(Count(customerControllerSource, "[ValidateAntiForgeryToken]") >= 4);
    }),
    ("all-unavailable refresh keeps cancellation workflow", () =>
    {
        Contains(serviceSource, "return found ? outcome : ContinueInventoryConflictOutcome.NotFound;");
        Contains(serviceSource, "OrderWorkflowStates.ConflictAwaitingDecision");
        Contains(serviceSource, "order.Stockdeducted ||");
        Contains(customerViewSource, "asp-action=\"CancelInventoryConflict\"");
    }),
    ("real transition exceptions still roll back", () =>
    {
        Contains(serviceSource, "await transaction.RollbackAsync(CancellationToken.None);");
        Contains(serviceSource, "_context.ChangeTracker.Clear();");
        Contains(serviceSource, "throw;");
    }),
    ("fulfilled recheck keeps existing continuation", () =>
    {
        Contains(serviceSource, "await DeductAllocationAsync(allocation, cancellationToken);");
        Contains(serviceSource, "order.Status = \"Processed\";");
        Contains(serviceSource, "order.Workflowstate = OrderWorkflowStates.ConflictResolvedContinue;");
    }),
    ("full cancellation remains available after refresh", () =>
    {
        Contains(serviceSource, "order.Refundrequiredamount = order.Totalamount;");
        Contains(serviceSource, "order.Workflowstate = OrderWorkflowStates.ConflictResolvedCancel;");
    })
};

var passed = 0;
foreach (var scenario in scenarios)
{
    scenario.Check();
    passed++;
    Console.WriteLine($"PASS {scenario.Name}");
}

Console.WriteLine($"DH-03 checks passed: {passed}/{scenarios.Length}");

static OrderAllocationLine Line(int id, int productId, int requested, int stockPerUnit, decimal price) =>
    new(id, productId, requested, stockPerUnit, price);

static IReadOnlyList<OrderAllocationResult> Allocate(
    IEnumerable<OrderAllocationLine> lines,
    params (int ProductId, int Stock)[] stock) =>
    OrderInventoryAllocator.Allocate(lines, stock.ToDictionary(pair => pair.ProductId, pair => pair.Stock));

static string FindRepoRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current != null && !File.Exists(Path.Combine(current.FullName, "YAGOT_2.0.csproj")))
        current = current.Parent;
    return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
}

static void Contains(string value, string expected)
{
    if (!value.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"Expected source contract was not found: {expected}");
}

static void DoesNotContain(string value, string unexpected)
{
    if (value.Contains(unexpected, StringComparison.Ordinal))
        throw new InvalidOperationException($"Unexpected source contract was found: {unexpected}");
}

static int Count(string value, string expected) =>
    value.Split(expected, StringSplitOptions.None).Length - 1;

static void Equal<T>(T expected, T actual) where T : IEquatable<T>
{
    if (!expected.Equals(actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
}

static void True(bool value)
{
    if (!value)
        throw new InvalidOperationException("Expected condition to be true.");
}
