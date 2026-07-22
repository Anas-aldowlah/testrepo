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

        return View(BuildCheckoutViewModel(cart));
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

    private CheckoutVM BuildCheckoutViewModel(Cart cart)
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
        }

        return model;
    }
}
