using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public OrdersController(
        OrderService orderService,
        CartService cartService,
        GuestCartService guestCartService,
        DealingAPI dealingApiService,
        StoreSettingsService settingsService,
        NeondbContext context,
        UsersDbContext users)
    {
        _context = context;
        _orderService = orderService;
        _cartService = cartService;
        _guestCartService = guestCartService;
        _dealingApiService = dealingApiService;
        _settingsService = settingsService;
        _dbUser = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = await ResolveUserIdAsync();
        var orders = await _orderService.GetUserOrdersAsync(userId);
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

        try
        {
            string? receiptUrl = null;
            if (model.ReceiptImage != null && model.ReceiptImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "receipts");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"receipt_{Guid.NewGuid().ToString().Substring(0, 8)}{Path.GetExtension(model.ReceiptImage.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ReceiptImage.CopyToAsync(stream);
                }
                receiptUrl = Url.Content($"~/uploads/receipts/{fileName}");
            }

            var order = await _orderService.CreateOrderAsync(userId, model, receiptUrl);

            var whatsappNumber = NormalizeWhatsAppNumber((await _settingsService.GetSettingsAsync()).WhatsAppNumber);
            if (!string.IsNullOrWhiteSpace(whatsappNumber))
            {
                var request = HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
                var orderLink = $"{baseUrl}/Orders/Details/{order.Id}";
                var textMessage = Uri.EscapeDataString($"مرحباً، أود تأكيد طلبي.\nرقم الطلب: {order.Id}\nرابط الطلب: {orderLink}");
                TempData["WhatsAppUrl"] = $"https://wa.me/{whatsappNumber}?text={textMessage}";
            }

            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateCheckoutPaymentMethodsAsync(model);
            return View("Checkout", model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var userId = await ResolveUserIdAsync();
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null || order.Userid != userId) return NotFound();
        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = await ResolveUserIdAsync();
        var order = await _orderService.GetOrderByIdAsync(id);
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
        if (int.TryParse(userIdVal, out var userId))
        {
            return Task.FromResult(userId);
        }
        return Task.FromResult(1);
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

    private static string NormalizeWhatsAppNumber(string? value)
    {
        return string.Concat((value ?? string.Empty).Where(char.IsDigit));
    }
}
