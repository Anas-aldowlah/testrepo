using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Data;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace Yagot.Controllers;

[Authorize]
[ServiceFilter(typeof(SiteStatusFilter))]
public class CartController : Controller
{
    private readonly NeondbContext _context;
    private readonly CartService _cartService;
    private readonly UsersDbContext _dbUser;

    public CartController(NeondbContext context, CartService cartService,UsersDbContext User)
    {
        // ADD CHANGE
        _context = context;
        _cartService = cartService;
        _dbUser = User;
    }

    public int nextCartId()
    {
        return _context.Carts.Any() ? _context.Carts.Max(c => c.Id) + 1 : 1;
    }

    public async Task<IActionResult> Index()
    {
        var userId = await ResolveUserIdAsync();
        var cart = await _cartService.GetCartAsync(userId);
        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> Add(int productId, int quantity)
    {
        var userId = await ResolveUserIdAsync();
        await _cartService.AddToCartAsync(userId, productId, quantity);
        TempData["Message"] = _cartService.MESSAGE;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartItemId)
    {
        var userId = await ResolveUserIdAsync();
        await _cartService.RemoveFromCartAsync(userId, cartItemId);
        TempData["Message"] = _cartService.MESSAGE;
        return RedirectToAction(nameof(Index));
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