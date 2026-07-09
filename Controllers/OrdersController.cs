using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly NeondbContext _context;

    public OrdersController(OrderService orderService, CartService cartService, NeondbContext context)
    {
        _context = context;
        _orderService = orderService;
        _cartService = cartService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = await ResolveUserIdAsync();
        var orders = await _orderService.GetUserOrdersAsync(userId);
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        var userId = await ResolveUserIdAsync();
        var cart = await _cartService.GetCartAsync(userId);

        if (cart.Cartitems == null || !cart.Cartitems.Any())
        {
            TempData["Error"] = "السلة فارغة - لا يمكن إتمام الطلب بدون منتجات";
            return RedirectToAction("Index", "Cart");
        }

        return View(cart);
    }

    [HttpPost]
    [ActionName("Checkout")]
    public async Task<IActionResult> CheckoutPost()
    {
        try
        {
            var userId = await ResolveUserIdAsync();
            var order = await _orderService.CreateOrderAsync(userId);
            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Cart");
        }
    }

    public async Task<IActionResult> Confirmation(int id)
    {
        var userId = await ResolveUserIdAsync();
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null || order.Userid != userId) return NotFound();
        return View(order);
    }

    private async Task<int> ResolveUserIdAsync()
    {
        var user = await _context.Users.FirstOrDefaultAsync(n => n.Name == User.Identity!.Name);
        return user?.Id ?? 1;
    }
}