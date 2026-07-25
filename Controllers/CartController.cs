using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace Yagot.Controllers;

[ServiceFilter(typeof(SiteStatusFilter))]
public class CartController : Controller
{
    private readonly CartService _cartService;
    private readonly GuestCartService _guestCartService;

    public CartController(CartService cartService, GuestCartService guestCartService)
    {
        _cartService = cartService;
        _guestCartService = guestCartService;
    }

    public async Task<IActionResult> Index()
    {
        var cart = await GetCurrentCartAsync();
        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity)
    {
        if (TryResolveUserId(out var userId))
        {
            await _cartService.AddToCartAsync(userId, productId, quantity);
            TempData["Message"] = _cartService.MESSAGE;
        }
        else
        {
            await _guestCartService.AddToCartAsync(productId, quantity);
            TempData["Message"] = _guestCartService.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int cartItemId, int productId, int quantity)
    {
        if (TryResolveUserId(out var userId))
        {
            await _cartService.UpdateQuantityAsync(userId, cartItemId, quantity);
            TempData["Message"] = _cartService.MESSAGE;
        }
        else
        {
            await _guestCartService.UpdateQuantityAsync(productId, quantity);
            TempData["Message"] = _guestCartService.Message;
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var cart = await GetCurrentCartAsync();
            return View("Index", cart);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartItemId, int productId)
    {
        if (TryResolveUserId(out var userId))
        {
            await _cartService.RemoveFromCartAsync(userId, cartItemId);
            TempData["Message"] = _cartService.MESSAGE;
        }
        else
        {
            await _guestCartService.RemoveFromCartAsync(productId);
            TempData["Message"] = _guestCartService.Message;
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var cart = await GetCurrentCartAsync();
            return View("Index", cart);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<Cart> GetCurrentCartAsync()
    {
        return TryResolveUserId(out var userId)
            ? await _cartService.GetCartAsync(userId)
            : await _guestCartService.GetCartAsync();
    }

    private bool TryResolveUserId(out int userId)
    {
        var userIdVal = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdVal, out userId);
    }
}
