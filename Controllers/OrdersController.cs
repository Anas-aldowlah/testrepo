using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Data;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.Controllers;

[Authorize]
[ServiceFilter(typeof(SiteStatusFilter))]
public class OrdersController : Controller
{
    private readonly OrderService _orderService;
    private readonly CartService _cartService;
    private readonly GuestCartService _guestCartService;
    private readonly NeondbContext _context;
    private readonly UsersDbContext _dbUser;

    public OrdersController(OrderService orderService, CartService cartService, GuestCartService guestCartService, NeondbContext context,UsersDbContext users)
    {
        _context = context;
        _orderService = orderService;
        _cartService = cartService;
        _guestCartService = guestCartService;
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

        return View(cart);
    }

    [AllowAnonymous]
    [HttpPost]
    [ActionName("Checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckoutPost()
    {
        if (!TryResolveUserId(out var userId))
        {
            var cart = await _guestCartService.GetCartAsync();
            if (cart.Cartitems == null || !cart.Cartitems.Any())
            {
                TempData["Error"] = "السلة فارغة - لا يمكن إتمام الطلب بدون منتجات";
                return RedirectToAction("Index", "Cart");
            }

            if (Request.HasFormContentType)
            {
                ViewData["CheckoutFormValues"] = Request.Form;
            }

            ViewData["ShowAuthModal"] = true;
            return View(cart);
        }

        try
        {
            var order = await _orderService.CreateOrderAsync(userId);
            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Cart");
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
}
