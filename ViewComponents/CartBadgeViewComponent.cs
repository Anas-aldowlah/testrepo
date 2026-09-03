using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.ViewComponents;

public class CartBadgeViewComponent : ViewComponent
{
    private readonly CartService _cartService;
    private readonly GuestCartService _guestCartService;
    private readonly ILogger<CartBadgeViewComponent> _logger;

    public CartBadgeViewComponent(
        CartService cartService,
        GuestCartService guestCartService,
        ILogger<CartBadgeViewComponent> logger)
    {
        _cartService = cartService;
        _guestCartService = guestCartService;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            var userIdValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var quantity = int.TryParse(userIdValue, out var userId)
                ? await _cartService.GetCartQuantityAsync(userId)
                : await _guestCartService.GetCartQuantityAsync();

            return View(quantity);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load the cart badge quantity.");
            return View(0);
        }
    }
}
