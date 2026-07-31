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
    private readonly NeondbContext _context;
    private readonly UsersDbContext _dbUser;

    public OrdersController(
        OrderService orderService,
        CartService cartService,
        GuestCartService guestCartService,
        DealingAPI dealingApiService,
        NeondbContext context,
        UsersDbContext users)
    {
        _context = context;
        _orderService = orderService;
        _cartService = cartService;
        _guestCartService = guestCartService;
        _dealingApiService = dealingApiService;
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

        // 1. التحقق من أمان الصورة والامتداد قبل إتمام الطلب
        string safeExtension = string.Empty;
        if (model.ReceiptImage != null && model.ReceiptImage.Length > 0)
        {
            var (isValid, errorMessage, detectedExtension) = await IsValidImageFileAsync(model.ReceiptImage);
            if (!isValid)
            {
                ModelState.AddModelError("ReceiptImage", errorMessage);
                return View("Checkout", model);
            }

            safeExtension = detectedExtension;
        }

        if (!ModelState.IsValid)
        {
            return View("Checkout", model);
        }

        if (!TryResolveUserId(out var userId))
        {
            ViewData["ShowAuthModal"] = true;
            return View("Checkout", model);
        }

        try
        {
            var order = await _orderService.CreateOrderAsync(userId, model);

            string? receiptUrl = null;
            if (model.ReceiptImage != null && model.ReceiptImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "receipts");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"receipt_{order.Id}_{Guid.NewGuid().ToString().Substring(0, 8)}{safeExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ReceiptImage.CopyToAsync(stream);
                }
                receiptUrl = Url.Content($"~/uploads/receipts/{fileName}");
            }

            // Construct WhatsApp link
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";

            var textMessage = $"مرحباً، أود تأكيد طلبي.%0Aرقم الطلب: {order.Id}%0Aرقم التتبع: {order.Trackingnumber}";
            if (!string.IsNullOrEmpty(receiptUrl))
            {
                textMessage += $"%0Aتم رفع سند الدفع: نعم  ";
            }
            else
            {
                textMessage += $"%0Aتم رفع سند الدفع: لا  ";
            }

            var whatsappUrl = $"https://wa.me/967775458250?text={textMessage}";
            TempData["WhatsAppUrl"] = whatsappUrl;

            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
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
            Cart = cart
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

    /// <summary>
    /// دالة فحص أمان واستحقاق الصورة المرفوعة
    /// </summary>
    private async Task<(bool IsValid, string ErrorMessage, string SafeExtension)> IsValidImageFileAsync(IFormFile file)
    {
        // 1. فحص الحجم الأقصى (5 ميجابايت)
        const long maxSizeBytes = 5 * 1024 * 1024;
        if (file.Length > maxSizeBytes)
        {
            return (false, "حجم صورة السند كبير جداً. الحد الأقصى المسموح به هو 5 ميجابايت.", string.Empty);
        }

        // 2. فحص الامتداد الظاهري للملف
        var rawExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (string.IsNullOrEmpty(rawExtension) || !allowedExtensions.Contains(rawExtension))
        {
            return (false, "نوع الملف غير مدعوم. يرجى رفع صورة بصيغة (JPG, PNG, WEBP) فقط.", string.Empty);
        }

        // 3. فحص MIME Type المبعوث مع الطلب
        var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return (false, "الملف المرفوع ليس صورة صالحة.", string.Empty);
        }

        // 4. قراءة التوقيع الثنائي للهيدر (Magic Bytes)
        using var stream = file.OpenReadStream();
        Memory<byte> headerBytes = new byte[8];
        await stream.ReadAsync(headerBytes);

        var span = headerBytes.Span;

        // JPEG: FF D8 FF
        if (span[0] == 0xFF && span[1] == 0xD8 && span[2] == 0xFF)
        {
            return (true, string.Empty, ".jpg");
        }

        // PNG: 89 50 4E 47
        if (span[0] == 0x89 && span[1] == 0x50 && span[2] == 0x4E && span[3] == 0x47)
        {
            return (true, string.Empty, ".png");
        }

        // WEBP: 52 49 46 46 (RIFF)
        if (span[0] == 0x52 && span[1] == 0x49 && span[2] == 0x46 && span[3] == 0x46)
        {
            return (true, string.Empty, ".webp");
        }

        return (false, "محتوى الملف لا يطابق هيكل الصور. يُرجى رفع صورة سند صحيحة.", string.Empty);
    }
}