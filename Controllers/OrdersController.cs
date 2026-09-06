using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using YAGOT_2._0.Data;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using System.Security.Claims;

namespace YAGOT_2._0.Controllers;

[Authorize]
[ServiceFilter(typeof(SiteStatusFilter))]
public class OrdersController : Controller
{
    private const string CheckoutSuccessMarkerTempDataPrefix = "YAGOT_CheckoutSuccess";

    private readonly OrderService _orderService;
    private readonly CartService _cartService;
    private readonly GuestCartService _guestCartService;
    private readonly DealingAPI _dealingApiService;
    private readonly StoreSettingsService _settingsService;
    private readonly NeondbContext _context;
    private readonly UsersDbContext _dbUser;
    private readonly ILogger<OrdersController> _logger;
    private readonly ReceiptStorageService _receiptStorage;

    public OrdersController(
        OrderService orderService,
        CartService cartService,
        GuestCartService guestCartService,
        DealingAPI dealingApiService,
        StoreSettingsService settingsService,
        NeondbContext context,
        UsersDbContext users,
        ILogger<OrdersController> logger,
        ReceiptStorageService receiptStorage)
    {
        _context = context;
        _orderService = orderService;
        _cartService = cartService;
        _guestCartService = guestCartService;
        _dealingApiService = dealingApiService;
        _settingsService = settingsService;
        _dbUser = users;
        _logger = logger;
        _receiptStorage = receiptStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string status = "all", string? search = null, int page = 1)
    {
        var userId = await ResolveUserIdAsync();
        var model = await _orderService.GetUserOrdersPageAsync(
            userId,
            status,
            search,
            page,
            HttpContext.RequestAborted);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        var cart = await GetCurrentCartAsync();

        if (cart.Cartitems == null || !cart.Cartitems.Any())
        {
            TempData["Error"] = "السلة فارغة - لا يمكن إتمام الطلب بدون منتجات";
            return RedirectToAction("Index", "Cart");
        }

        var draftJson = HttpContext.Session.GetString(GetDraftSessionKey());
        if (!string.IsNullOrEmpty(draftJson))
        {
            ViewData["CheckoutDraftJson"] = draftJson;
        }

        return View(await BuildCheckoutViewModelAsync(cart));
    }

    [HttpPost]
    [ActionName("Checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckoutPost(CheckoutVM model)
    {
        var cart = await GetCurrentCartAsync();
        if (cart.Cartitems == null || !cart.Cartitems.Any())
        {
            TempData["Error"] = "السلة فارغة - لا يمكن إتمام الطلب بدون منتجات";
            return RedirectToAction("Index", "Cart");
        }

        model.Cart = cart;

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return ValidationProblem(ModelState);

            await PopulateCheckoutPaymentMethodsAsync(model);
            return View("Checkout", model);
        }

        var userId = await ResolveUserIdAsync();

        StagedReceipt? stagedReceipt = null;
        try
        {
            string? receiptUrl = null;
            if (model.ReceiptImage != null && model.ReceiptImage.Length > 0)
            {
                try
                {
                    stagedReceipt = await _receiptStorage.StageAsync(
                        model.ReceiptImage,
                        HttpContext.RequestAborted);
                    _receiptStorage.Promote(stagedReceipt);
                    receiptUrl = stagedReceipt.StorageKey;
                }
                catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Optional receipt storage failed for user {UserId}; checkout will continue.", userId);
                    receiptUrl = null;
                }
            }

            var order = await _orderService.CreateOrderAsync(
                userId,
                model,
                receiptUrl,
                HttpContext.RequestAborted);

            if (Guid.TryParseExact(model.CheckoutDraftId, "D", out var checkoutDraftId))
            {
                TempData[CheckoutSuccessMarkerKey(order.Id)] = $"{order.Id}:{checkoutDraftId:D}";

                var currentDraftJson = HttpContext.Session.GetString(GetDraftSessionKey());
                if (!string.IsNullOrEmpty(currentDraftJson))
                {
                    var currentDraft = System.Text.Json.JsonSerializer.Deserialize<CheckoutDraftState>(currentDraftJson);
                    if (currentDraft?.DraftId == model.CheckoutDraftId)
                    {
                        HttpContext.Session.Remove(GetDraftSessionKey());
                    }
                }

                HttpContext.Session.SetString("ClearedDraft_" + model.CheckoutDraftId, "true");
            }
            else
            {
                HttpContext.Session.Remove(GetDraftSessionKey());
            }

            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (OverflowException exception)
        {
            _logger.LogWarning(exception, "Checkout was rejected for user {UserId} because a numeric calculation overflowed.", userId);
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Checkout numeric limit exceeded",
                detail: "إحدى الكميات أو إجمالي الطلب يتجاوز الحد الرقمي المسموح.");
        }
        catch (Exception exception) when (exception is CartConcurrencyException or DbUpdateConcurrencyException)
        {
            _logger.LogWarning(exception, "Checkout cart conflict for user {UserId}.", userId);
            return await HandleCheckoutFailureAsync(
                model,
                StatusCodes.Status409Conflict,
                "Checkout cart conflict",
                "تغيرت السلة في نافذة أو جهاز آخر. يرجى تحديث صفحة إتمام الطلب والمحاولة مرة أخرى.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Checkout was rejected for user {UserId}.", userId);
            return await HandleCheckoutFailureAsync(
                model,
                StatusCodes.Status409Conflict,
                "Checkout rejected",
                exception.Message);
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException or TimeoutException)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            var postgresException = FindException<PostgresException>(exception);
            _logger.LogError(
                "Checkout provider failure {CorrelationId}: ExceptionType={ExceptionType}; SqlState={SqlState}; Constraint={Constraint}; Table={Table}; Column={Column}",
                correlationId,
                exception.GetType().FullName,
                postgresException?.SqlState,
                postgresException?.ConstraintName,
                postgresException?.TableName,
                postgresException?.ColumnName);
            return await HandleCheckoutFailureAsync(
                model,
                StatusCodes.Status503ServiceUnavailable,
                "Checkout temporarily unavailable",
                "تعذر إتمام الطلب حالياً بسبب مشكلة مؤقتة. يرجى المحاولة مرة أخرى.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected checkout failure for user {UserId}.", userId);
            return await HandleCheckoutFailureAsync(
                model,
                StatusCodes.Status500InternalServerError,
                "Checkout failed",
                "تعذر إتمام الطلب حالياً. يرجى المحاولة مرة أخرى.");
        }
        finally
        {
            ReceiptStorageService.DeleteTemporaryFile(stagedReceipt);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var userId = await ResolveUserIdAsync();
        var order = await _orderService.GetOrderByIdAsync(id, HttpContext.RequestAborted);
        if (order == null || order.Userid != userId) return NotFound();
        await PopulatePaymentMethodPresentationsAsync(order);
        var footerSettings = await _settingsService.GetFooterSettingsAsync();
        ViewData["CustomerOrderWhatsAppUrl"] = OrderWhatsAppLinkBuilder.BuildCustomerOrderNotification(
            order,
            footerSettings.WhatsAppNumber);
        ViewData["CheckoutDraftIdToClear"] = ConsumeCheckoutSuccessMarker(id);
        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = await ResolveUserIdAsync();
        var order = await _orderService.GetOrderByIdAsync(id, HttpContext.RequestAborted);
        if (order == null || order.Userid != userId) return NotFound();
        await PopulatePaymentMethodPresentationsAsync(order);
        var inventoryConflict = OrderService.BuildCustomerInventoryConflict(order);
        ViewData["InventoryConflict"] = inventoryConflict;
        if (inventoryConflict != null)
        {
            var footerSettings = await _settingsService.GetFooterSettingsAsync();
            ViewData["CustomerOrderWhatsAppUrl"] = OrderWhatsAppLinkBuilder.BuildCustomerConflictUpdateNotification(
                order,
                footerSettings.WhatsAppNumber);
        }
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveInventoryConflict(ResolveInventoryConflictRequest input)
    {
        var userId = await ResolveUserIdAsync();
        var isAjax = IsAjaxRequest();

        if (input.CancelEntireOrder)
        {
            var keysToRemove = ModelState.Keys.Where(k => k.StartsWith(nameof(input.Decisions))).ToList();
            foreach (var key in keysToRemove)
            {
                ModelState.Remove(key);
            }
        }

        if (!ModelState.IsValid)
        {
            const string invalidMessage = "يرجى مراجعة اختياراتك.";
            return isAjax
                ? BadRequest(new { success = false, message = invalidMessage })
                : RedirectToAction(nameof(Details), new { id = input.Id });
        }

        try
        {
            var outcome = await _orderService.ResolveInventoryConflictAsync(
                input.Id,
                userId,
                input.CancelEntireOrder,
                input.Decisions,
                HttpContext.RequestAborted);

            if (outcome == CustomerInventoryConflictOutcome.NotFound)
                return NotFound();

            if (outcome == CustomerInventoryConflictOutcome.Conflict)
            {
                const string changedMessage = "تغيّرت الكمية المتوفرة. راجع الخيارات الجديدة وحاول مرة أخرى.";
                return isAjax
                    ? Conflict(new { success = false, message = changedMessage, reload = true })
                    : RedirectToAction(nameof(Details), new { id = input.Id });
            }

            var presentation = BuildConflictResolutionPresentation(outcome, input.Decisions ?? []);
            var redirectUrl = outcome == CustomerInventoryConflictOutcome.Cancelled
                ? Url.Action(nameof(Index))
                : Url.Action(nameof(Details), new { id = input.Id });

            if (isAjax)
                return Json(new
                {
                    success = true,
                    resultKey = presentation.Key,
                    title = presentation.Title,
                    message = presentation.Message,
                    redirectUrl
                });

            TempData["Success"] = presentation.Message;
            return Redirect(redirectUrl ?? Url.Action(nameof(Index))!);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "Inventory conflict resolution was rejected for order {OrderId} and owner {UserId}.",
                input.Id,
                userId);
            const string message = "تعذر حفظ القرار. يرجى مراجعة الطلب والمحاولة مرة أخرى.";
            if (isAjax)
                return Conflict(new { success = false, message });

            TempData["Error"] = message;
            return RedirectToAction(nameof(Details), new { id = input.Id });
        }
    }

    private static (string Key, string Title, string Message) BuildConflictResolutionPresentation(
        CustomerInventoryConflictOutcome outcome,
        IReadOnlyCollection<InventoryConflictLineDecisionInput> decisions)
    {
        if (outcome == CustomerInventoryConflictOutcome.Cancelled)
        {
            return (
                "Cancelled",
                "تم إلغاء طلبك بنجاح",
                "أُلغي الطلب ولن تتم متابعة تجهيزه.");
        }

        var removedCount = decisions.Count(decision =>
            string.Equals(decision.Decision, InventoryConflictDecisions.Remove, StringComparison.Ordinal));
        var continuedCount = decisions.Count(decision =>
            string.Equals(decision.Decision, InventoryConflictDecisions.Continue, StringComparison.Ordinal));

        const string successTitle = "تم تحديث الطلب وتأكيد الدفع بنجاح";
        const string successMessage = "تم تحديث الطلب وتأكيد الدفع بنجاح.";

        if (removedCount > 0 && continuedCount > 0)
        {
            return (
                "Mixed",
                successTitle,
                successMessage);
        }

        if (removedCount > 1)
        {
            return (
                "RemovedMultiple",
                successTitle,
                successMessage);
        }

        if (removedCount == 1)
        {
            return (
                "Removed",
                successTitle,
                successMessage);
        }

        return (
            "Continued",
            successTitle,
            successMessage);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveCheckoutDraft([FromBody] CheckoutDraftState draft)
    {
        if (draft == null) return BadRequest();
        if (!string.IsNullOrEmpty(draft.DraftId) && HttpContext.Session.GetString("ClearedDraft_" + draft.DraftId) == "true")
        {
            return Ok();
        }

        var key = GetDraftSessionKey();
        var json = System.Text.Json.JsonSerializer.Serialize(draft);
        HttpContext.Session.SetString(key, json);
        return Ok();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearCheckoutDraft()
    {
        var key = GetDraftSessionKey();
        HttpContext.Session.Remove(key);
        return Ok();
    }

    private string GetDraftSessionKey()
    {
        var id = TryResolveUserId(out var userId) ? userId.ToString() : "Guest";
        return $"CheckoutDraft_{id}";
    }

    private async Task<Cart> GetCurrentCartAsync()
    {
        return TryResolveUserId(out var userId)
            ? await _cartService.GetCartAsync(userId)
            : await _guestCartService.GetCartAsync();
    }

    private bool TryResolveUserId(out int userId)
    {
        var userIdVal = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdVal, out userId);
    }

    private Task<int> ResolveUserIdAsync()
    {
        var userIdVal = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdVal, out var userId) && userId > 0)
        {
            return Task.FromResult(userId);
        }

        throw new UnauthorizedAccessException("The authenticated user has no valid user identifier claim.");
    }

    private async Task<CheckoutVM> BuildCheckoutViewModelAsync(Cart cart)
    {
        var model = new CheckoutVM
        {
            Cart = cart,
            PaymentMethods = await _settingsService.GetCheckoutPaymentMethodsAsync()
        };

        if (User.Identity?.IsAuthenticated == true)
        {
            model.CustomerName = User.Identity?.Name ?? string.Empty;
            var encryptedPhone = User.FindFirst(ClaimTypes.MobilePhone)?.Value;
            model.CustomerPhone = _dealingApiService.DecryptPhone(encryptedPhone);

            // جلب البريد الإلكتروني مباشرة من قاعدة البيانات بدل الـ Claim
            if (TryResolveUserId(out var uid))
            {
                var dbUser = await _dbUser.Users.FindAsync(uid);
                model.CustomerEmail = dbUser?.Email;
            }
        }

        return model;
    }

    private async Task PopulateCheckoutPaymentMethodsAsync(CheckoutVM model)
    {
        model.PaymentMethods = await _settingsService.GetCheckoutPaymentMethodsAsync();
    }

    private async Task<IActionResult> HandleCheckoutFailureAsync(
        CheckoutVM model,
        int statusCode,
        string title,
        string detail)
    {
        if (IsAjaxRequest())
        {
            return Problem(
                statusCode: statusCode,
                title: title,
                detail: detail,
                type: $"https://httpstatuses.com/{statusCode}");
        }

        ModelState.AddModelError(string.Empty, detail);
        await PopulateCheckoutPaymentMethodsAsync(model);
        return View("Checkout", model);
    }

    private bool IsAjaxRequest() =>
        string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

    private string? ConsumeCheckoutSuccessMarker(int orderId)
    {
        var markerKey = CheckoutSuccessMarkerKey(orderId);
        var markerValue = TempData.Peek(markerKey)?.ToString();
        var separatorIndex = markerValue?.IndexOf(':') ?? -1;
        if (separatorIndex <= 0 ||
            !int.TryParse(markerValue![..separatorIndex], out var successfulOrderId) ||
            successfulOrderId != orderId ||
            !Guid.TryParseExact(markerValue[(separatorIndex + 1)..], "D", out var checkoutDraftId))
        {
            return null;
        }

        TempData.Remove(markerKey);
        return checkoutDraftId.ToString("D");
    }

    private static string CheckoutSuccessMarkerKey(int orderId) =>
        $"{CheckoutSuccessMarkerTempDataPrefix}:{orderId}";

    private async Task PopulatePaymentMethodPresentationsAsync(Order order)
    {
        ViewData["PaymentMethodPresentations"] = await _settingsService.GetPaymentMethodPresentationsAsync(
            [order.Paymentmethod],
            HttpContext.RequestAborted);
    }

    private static TException? FindException<TException>(Exception exception)
        where TException : Exception
    {
        for (var current = exception; current != null; current = current.InnerException!)
        {
            if (current is TException match)
                return match;
            if (current.InnerException == null)
                break;
        }

        return null;
    }

}
