using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Extensions;
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
    public async Task<IActionResult> Add(AddCartItemInput input)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            string? message;
            string? warningCode;
            int? availableQuantity;
            if (TryResolveUserId(out var userId))
            {
                await _cartService.AddToCartAsync(userId, input.ProductId, input.Quantity, input.RetailPriceId);
                message = _cartService.MESSAGE;
                warningCode = _cartService.WARNING_CODE;
                availableQuantity = _cartService.AVAILABLE_QUANTITY;
            }
            else
            {
                await _guestCartService.AddToCartAsync(input.ProductId, input.Quantity, input.RetailPriceId);
                message = _guestCartService.Message;
                warningCode = _guestCartService.WarningCode;
                availableQuantity = _guestCartService.AvailableQuantity;
            }

            var stockRejected = warningCode == "InsufficientStock";
            if (IsAjaxRequest())
            {
                var state = await BuildCartStateAsync(
                    success: !stockRejected,
                    conflict: false,
                    message,
                    productId: input.ProductId,
                    retailPriceId: input.RetailPriceId,
                    warningCode: warningCode,
                    availableQuantity: availableQuantity);
                return stockRejected ? BadRequest(state) : Json(state);
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
    public async Task<IActionResult> Update(UpdateCartItemInput input)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            string? message;
            string? warningCode;
            int? availableQuantity;
            if (TryResolveUserId(out var userId))
            {
                await _cartService.UpdateQuantityAsync(
                    userId,
                    input.CartItemId,
                    input.Quantity,
                    input.ExpectedQuantity);
                message = _cartService.MESSAGE;
                warningCode = _cartService.WARNING_CODE;
                availableQuantity = _cartService.AVAILABLE_QUANTITY;
            }
            else
            {
                await _guestCartService.UpdateQuantityAsync(input.ProductId, input.Quantity, input.RetailPriceId);
                message = _guestCartService.Message;
                warningCode = _guestCartService.WarningCode;
                availableQuantity = _guestCartService.AvailableQuantity;
            }

            if (IsAjaxRequest())
            {
                var stockRejected = warningCode == "InsufficientStock" &&
                    availableQuantity.HasValue &&
                    input.Quantity > availableQuantity.Value;
                var state = await BuildCartStateAsync(
                    success: !stockRejected,
                    conflict: false,
                    message,
                    input.CartItemId,
                    input.ProductId,
                    input.RetailPriceId,
                    warningCode: warningCode,
                    availableQuantity: availableQuantity);
                return stockRejected ? BadRequest(state) : Json(state);
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
                input.CartItemId,
                input.ProductId,
                input.RetailPriceId);
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
        int? retailPriceId = null,
        string? warningCode = null,
        int? availableQuantity = null)
    {
        var summary = TryResolveUserId(out var userId)
            ? await _cartService.GetCartStateSummaryAsync(userId, cartItemId, productId, retailPriceId)
            : await _guestCartService.GetCartStateSummaryAsync(cartItemId, productId, retailPriceId);
        var lineTotal = summary.Item == null
            ? 0m
            : checked(summary.Item.Quantity * summary.Item.UnitPrice);

        return new
        {
            success = success && summary.Item?.Quantity > 0,
            conflict,
            message,
            warningCode,
            availableQuantity,
            totalQuantity = summary.TotalQuantity,
            uniqueItemCount = summary.UniqueItemCount,
            subtotal = summary.Subtotal,
            subtotalText = $"{summary.Subtotal.ToYaqutPrice()}",
            item = summary.Item == null
                ? null
                : new
                {
                    cartItemId = summary.Item.CartItemId,
                    productId = summary.Item.ProductId,
                    retailPriceId = summary.Item.RetailPriceId,
                    quantity = summary.Item.Quantity,
                    lineTotal,
                    lineTotalText = $"{lineTotal.ToYaqutPrice()}"
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
        var isOverflow = exception is OverflowException;
        var isConcurrencyConflict = exception is CartConcurrencyException or DbUpdateConcurrencyException;
        var isDatabaseFailure = exception is DbUpdateException or NpgsqlException or TimeoutException;
        var statusCode = isOverflow
            ? StatusCodes.Status400BadRequest
            : isConcurrencyConflict
            ? StatusCodes.Status409Conflict
            : isDatabaseFailure
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;
        var detail = isOverflow
            ? "القيمة الرقمية أو إجمالي السلة يتجاوز الحد المسموح."
            : isConcurrencyConflict
            ? "تغيرت السلة في نافذة أو جهاز آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى."
            : "تعذر تحديث السلة حالياً. يرجى المحاولة مرة أخرى.";

        if (isOverflow)
        {
            _logger.LogWarning(exception, "Rejected {OperationName} because a numeric value overflowed.", operationName);
        }
        else if (isConcurrencyConflict)
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
