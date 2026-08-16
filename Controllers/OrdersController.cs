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
    public async Task<IActionResult> Index()
    {
        var userId = await ResolveUserIdAsync();
        var orders = await _orderService.GetUserOrdersAsync(userId, HttpContext.RequestAborted);
        return View(orders);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        var cart = await GetCurrentCartAsync();

        if (cart.Cartitems == null || !cart.Cartitems.Any())
        {
            TempData["Error"] = "السلة فارغة - لا يمكن إتمام الطلب بدون منتجات";
            return RedirectToAction("Index", "Cart");
        }

        return View(await BuildCheckoutViewModelAsync(cart));
    }

    [AllowAnonymous]
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
            await PopulateCheckoutPaymentMethodsAsync(model);
            return View("Checkout", model);
        }

        if (!TryResolveUserId(out var userId))
        {
            ViewData["ShowAuthModal"] = true;
            await PopulateCheckoutPaymentMethodsAsync(model);
            return View("Checkout", model);
        }

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
                    // Receipt storage is optional; WhatsApp remains the fallback.
                    _logger.LogWarning(exception, "Optional receipt storage failed for user {UserId}; checkout will continue.", userId);
                    receiptUrl = null;
                }
            }

            var order = await _orderService.CreateOrderAsync(
                userId,
                model,
                receiptUrl,
                HttpContext.RequestAborted);

            try
            {
                var whatsappNumber = NormalizeWhatsAppNumber((await _settingsService.GetSettingsAsync()).WhatsAppNumber);
                if (!string.IsNullOrWhiteSpace(whatsappNumber))
                {
                    var request = HttpContext.Request;
                    var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
                    var orderLink = $"{baseUrl}/Orders/Details/{order.Id}";
                    var textMessage = Uri.EscapeDataString($"مرحباً، أود تأكيد طلبي.\nرقم الطلب: {order.Id}\nرقم التتبع: {orderLink}\nتم رفع سند الدفع: {(string.IsNullOrEmpty(receiptUrl) ? "لا" : "نعم")}");
                    TempData["WhatsAppUrl"] = $"https://wa.me/{whatsappNumber}?text={textMessage}";
                }
            }
            catch (Exception exception)
            {
                // An already committed order must never be reported as failed because
                // the optional WhatsApp handoff could not be prepared.
                _logger.LogWarning(exception, "WhatsApp handoff preparation failed for order {OrderId}.", order.Id);
            }

            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
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
            _logger.LogError(exception, "Database failure during checkout for user {UserId}.", userId);
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
        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = await ResolveUserIdAsync();
        var order = await _orderService.GetOrderByIdAsync(id, HttpContext.RequestAborted);
        if (order == null || order.Userid != userId) return NotFound();
        return View(order);
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

    private static string NormalizeWhatsAppNumber(string? value)
    {
        return string.Concat((value ?? string.Empty).Where(char.IsDigit));
    }
}
