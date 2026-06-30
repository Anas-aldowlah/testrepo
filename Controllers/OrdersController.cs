using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Services;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly OrderService _orderService;
    private readonly NeondbContext _context;

    public OrdersController(OrderService orderService, NeondbContext context)
    {
        _context = context;
        _orderService = orderService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = await ResolveUserIdAsync();
        var orders = await _orderService.GetUserOrdersAsync(userId);
        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> Checkout()
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
