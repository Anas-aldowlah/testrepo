using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    private readonly ILogger<CartController> _logger;

    public CartController(
        CartService cartService,
        GuestCartService guestCartService,
        ILogger<CartController> logger)
    {
        _cartService = cartService;
        _guestCartService = guestCartService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var cart = await GetCurrentCartAsync();
            return View(cart);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load the current cart.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Cart temporarily unavailable",
                detail: "تعذر تحميل السلة حالياً. يرجى المحاولة مرة أخرى.",
                type: "https://httpstatuses.com/503");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity, int? retailPriceId)
    {
        try
        {
            string? message;
            if (TryResolveUserId(out var userId))
            {
                await _cartService.AddToCartAsync(userId, productId, quantity, retailPriceId);
                message = _cartService.MESSAGE;
            }
            else
            {
                await _guestCartService.AddToCartAsync(productId, quantity, retailPriceId);
                message = _guestCartService.Message;
            }

            if (IsAjaxRequest())
            {
                return Json(await BuildCartStateAsync(
                    success: true,
                    conflict: false,
                    message,
                    productId: productId,
                    retailPriceId: retailPriceId));
            }

            TempData["Message"] = message;
            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleCartFailure(exception, "add an item");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        int cartItemId,
        int productId,
        int quantity,
        int? retailPriceId,
        int? expectedQuantity)
    {
        try
        {
            string? message;
            if (TryResolveUserId(out var userId))
            {
                await _cartService.UpdateQuantityAsync(
                    userId,
                    cartItemId,
                    quantity,
                    expectedQuantity);
                message = _cartService.MESSAGE;
            }
            else
            {
                await _guestCartService.UpdateQuantityAsync(productId, quantity, retailPriceId);
                message = _guestCartService.Message;
            }

            if (IsAjaxRequest())
            {
                return Json(await BuildCartStateAsync(
                    success: true,
                    conflict: false,
                    message,
                    cartItemId,
                    productId,
                    retailPriceId));
            }

            TempData["Message"] = message;
            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (CartConcurrencyException exception) when (IsAjaxRequest())
        {
            _logger.LogWarning(exception, "The cart quantity changed before the update was applied.");
            var state = await BuildCartStateAsync(
                success: false,
                conflict: true,
                exception.Message,
                cartItemId,
                productId,
                retailPriceId);
            return StatusCode(StatusCodes.Status409Conflict, state);
        }
        catch (Exception exception)
        {
            return HandleCartFailure(exception, "update a quantity");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartItemId, int productId, int? retailPriceId)
    {
        try
        {
            if (TryResolveUserId(out var userId))
            {
                await _cartService.RemoveFromCartAsync(userId, cartItemId);
                TempData["Message"] = _cartService.MESSAGE;
            }
            else
            {
                await _guestCartService.RemoveFromCartAsync(productId, retailPriceId);
                TempData["Message"] = _guestCartService.Message;
            }

            if (IsAjaxRequest())
            {
                var cart = await GetCurrentCartAsync();
                return View("Index", cart);
            }

            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HandleCartFailure(exception, "remove an item");
        }
    }

    private async Task<Cart> GetCurrentCartAsync()
    {
        return TryResolveUserId(out var userId)
            ? await _cartService.GetCartAsync(userId)
            : await _guestCartService.GetCartAsync();
    }

    private async Task<object> BuildCartStateAsync(
        bool success,
        bool conflict,
        string? message,
        int? cartItemId = null,
        int? productId = null,
        int? retailPriceId = null)
    {
        var cart = await GetCurrentCartAsync();
        var items = cart.Cartitems
            .Where(item => item.Product != null)
            .ToList();
        var targetItem = cartItemId is > 0
            ? items.FirstOrDefault(item => item.Id == cartItemId.Value)
            : items.FirstOrDefault(item =>
                item.Productid == productId &&
                item.RetailPriceId == retailPriceId);
        var subtotal = items.Sum(item =>
            item.Quantity * (item.RetailPrice?.Price ?? item.Product.Price));

        return new
        {
            success = success && targetItem?.Quantity > 0,
            conflict,
            message,
            totalQuantity = items.Sum(item => item.Quantity),
            uniqueItemCount = items.Count,
            subtotal,
            subtotalText = $"{subtotal:N0} ر.س",
            item = targetItem == null
                ? null
                : new
                {
                    cartItemId = targetItem.Id,
                    productId = targetItem.Productid,
                    retailPriceId = targetItem.RetailPriceId,
                    quantity = targetItem.Quantity,
                    lineTotal = targetItem.Quantity * (targetItem.RetailPrice?.Price ?? targetItem.Product.Price),
                    lineTotalText = $"{targetItem.Quantity * (targetItem.RetailPrice?.Price ?? targetItem.Product.Price):N0} ر.س"
                }
        };
    }

    private bool TryResolveUserId(out int userId)
    {
        var userIdVal = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdVal, out userId);
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult HandleCartFailure(Exception exception, string operationName)
    {
        var isConcurrencyConflict = exception is CartConcurrencyException or DbUpdateConcurrencyException;
        var isDatabaseFailure = exception is DbUpdateException or NpgsqlException or TimeoutException;
        var statusCode = isConcurrencyConflict
            ? StatusCodes.Status409Conflict
            : isDatabaseFailure
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;
        var detail = isConcurrencyConflict
            ? "تغيرت السلة في نافذة أو جهاز آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى."
            : "تعذر تحديث السلة حالياً. يرجى المحاولة مرة أخرى.";

        if (isConcurrencyConflict)
        {
            _logger.LogWarning(exception, "Could not {OperationName} because the cart changed concurrently.", operationName);
        }
        else
        {
            _logger.LogError(exception, "Could not {OperationName} in the cart.", operationName);
        }

        if (IsAjaxRequest())
        {
            return Problem(
                statusCode: statusCode,
                title: isConcurrencyConflict ? "Cart state conflict" : "Cart operation failed",
                detail: detail,
                type: $"https://httpstatuses.com/{statusCode}");
        }

        TempData["Error"] = detail;
        return RedirectToAction(nameof(Index));
    }
}
