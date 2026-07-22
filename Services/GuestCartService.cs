using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class GuestCartService
{
    private const string CookieName = "YAGOT.GuestCart";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly NeondbContext _context;
    private readonly CartService _cartService;

    public string? Message { get; private set; }

    public GuestCartService(IHttpContextAccessor httpContextAccessor, NeondbContext context, CartService cartService)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
        _cartService = cartService;
    }

    public async Task<Cart> GetCartAsync()
    {
        var guestItems = ReadItems();
        var productIds = guestItems.Select(i => i.ProductId).ToList();
        var products = await _context.Products
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        var cartItems = guestItems
            .Select(item =>
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product == null) return null;

                return new Cartitem
                {
                    Id = item.ProductId,
                    Cartid = 0,
                    Productid = item.ProductId,
                    Quantity = item.Quantity,
                    Product = product
                };
            })
            .Where(item => item != null)
            .Cast<Cartitem>()
            .ToList();

        return new Cart
        {
            Id = 0,
            Userid = 0,
            Cartitems = cartItems
        };
    }

    public async Task AddToCartAsync(int productId, int quantity)
    {
        Message = null;
        quantity = Math.Max(1, quantity);

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null || product.Stockquantity <= 0)
        {
            Message = "هذا المنتج غير متوفر حالياً.";
            return;
        }

        var items = ReadItems();
        var existingItem = items.FirstOrDefault(i => i.ProductId == productId);
        var requestedQuantity = (existingItem?.Quantity ?? 0) + quantity;

        if (requestedQuantity > product.Stockquantity)
        {
            Message = $"الكمية المتبقية من {product.Name}: {product.Stockquantity}.";
            requestedQuantity = product.Stockquantity;
        }

        if (existingItem == null)
        {
            items.Add(new GuestCartItem { ProductId = productId, Quantity = requestedQuantity });
        }
        else
        {
            existingItem.Quantity = requestedQuantity;
        }

        WriteItems(items);
    }

    public async Task UpdateQuantityAsync(int productId, int quantity)
    {
        Message = null;
        var items = ReadItems();
        var existingItem = items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem == null)
        {
            Message = "العنصر غير موجود في سلتك.";
            return;
        }

        if (quantity <= 0)
        {
            items.Remove(existingItem);
            WriteItems(items);
            Message = "تم حذف العنصر بنجاح.";
            return;
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null || product.Stockquantity <= 0)
        {
            items.Remove(existingItem);
            WriteItems(items);
            Message = "هذا المنتج غير متوفر حالياً.";
            return;
        }

        existingItem.Quantity = Math.Min(quantity, product.Stockquantity);
        if (quantity > product.Stockquantity)
        {
            Message = $"الكمية المتبقية من {product.Name}: {product.Stockquantity}.";
        }

        WriteItems(items);
    }

    public Task RemoveFromCartAsync(int productId)
    {
        Message = null;
        var items = ReadItems();
        var existingItem = items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem == null)
        {
            Message = "العنصر غير موجود في سلتك.";
            return Task.CompletedTask;
        }

        items.Remove(existingItem);
        WriteItems(items);
        Message = "تم حذف العنصر بنجاح.";
        return Task.CompletedTask;
    }

    public async Task MergeIntoUserCartAsync(int userId)
    {
        var items = ReadItems();
        if (!items.Any()) return;

        foreach (var item in items)
        {
            await _cartService.AddToCartAsync(userId, item.ProductId, item.Quantity);
        }

        Clear();
    }

    private List<GuestCartItem> ReadItems()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null ||
            !httpContext.Request.Cookies.TryGetValue(CookieName, out var cookieValue) ||
            string.IsNullOrWhiteSpace(cookieValue))
        {
            return [];
        }

        try
        {
            var json = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cookieValue));
            return JsonSerializer.Deserialize<List<GuestCartItem>>(json)?
                .Where(i => i.ProductId > 0 && i.Quantity > 0)
                .GroupBy(i => i.ProductId)
                .Select(g => new GuestCartItem { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToList() ?? [];
        }
        catch (FormatException)
        {
            Clear();
            return [];
        }
        catch (JsonException)
        {
            Clear();
            return [];
        }
    }

    private void WriteItems(List<GuestCartItem> items)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        var validItems = items.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();
        if (!validItems.Any())
        {
            Clear();
            return;
        }

        var json = JsonSerializer.Serialize(validItems);
        var value = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        httpContext.Response.Cookies.Append(CookieName, value, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    private void Clear()
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Delete(CookieName);
    }

    private sealed class GuestCartItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
