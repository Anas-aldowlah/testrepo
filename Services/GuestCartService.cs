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
    private readonly CartLockService _cartLock;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<GuestCartService> _logger;

    public string? Message { get; private set; }

    public GuestCartService(
        IHttpContextAccessor httpContextAccessor,
        NeondbContext context,
        CartLockService cartLock,
        IInventoryService inventoryService,
        ILogger<GuestCartService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
        _cartLock = cartLock;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<Cart> GetCartAsync()
    {
        var guestItems = ReadItems();
        var productIds = guestItems.Select(i => i.ProductId).Distinct().ToList();
        var retailPriceIds = guestItems.Where(i => i.RetailPriceId.HasValue)
            .Select(i => i.RetailPriceId!.Value)
            .Distinct()
            .ToList();

        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();
        var productsById = products.ToDictionary(p => p.Id);

        var retailPrices = await _context.ProductRetailPrices
            .AsNoTracking()
            .Where(price => retailPriceIds.Contains(price.Id))
            .ToDictionaryAsync(price => price.Id);

        var cartItems = guestItems
            .Select((item, index) =>
            {
                if (!productsById.TryGetValue(item.ProductId, out var product)) return null;

                retailPrices.TryGetValue(item.RetailPriceId ?? 0, out var retailPrice);
                return new Cartitem
                {
                    Id = index + 1,
                    Cartid = 0,
                    Productid = item.ProductId,
                    RetailPriceId = item.RetailPriceId,
                    Quantity = item.Quantity,
                    Product = product,
                    RetailPrice = retailPrice
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

    public async Task AddToCartAsync(int productId, int quantity, int? retailPriceId = null)
    {
        Message = null;
        quantity = Math.Max(1, quantity);

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null || product.Stockquantity <= 0)
        {
            Message = "هذا المنتج غير متوفر حالياً.";
            return;
        }

        var retailPrice = await _inventoryService.ValidateRetailPriceAsync(product, retailPriceId);
        var maxUnits = _inventoryService.GetAvailableSaleUnits(product, retailPrice?.SizeMl);
        if (maxUnits <= 0)
        {
            Message = "هذا المنتج غير متوفر حالياً.";
            return;
        }

        var items = ReadItems();
        var existingItem = items.FirstOrDefault(i =>
            i.ProductId == productId &&
            i.RetailPriceId == retailPriceId);
        var requestedQuantity = (existingItem?.Quantity ?? 0) + quantity;

        if (requestedQuantity > maxUnits)
        {
            Message = $"الكمية المتبقية من {product.Name}: {maxUnits}.";
            requestedQuantity = maxUnits;
        }

        if (existingItem == null)
        {
            items.Add(new GuestCartItem
            {
                ProductId = productId,
                RetailPriceId = retailPriceId,
                Quantity = requestedQuantity
            });
        }
        else
        {
            existingItem.Quantity = requestedQuantity;
        }

        WriteItems(items);
    }

    public async Task UpdateQuantityAsync(int productId, int quantity, int? retailPriceId = null)
    {
        Message = null;
        var items = ReadItems();
        var existingItem = items.FirstOrDefault(i =>
            i.ProductId == productId &&
            i.RetailPriceId == retailPriceId);
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

        var retailPrice = await _inventoryService.ValidateRetailPriceAsync(product, retailPriceId);
        var maxUnits = _inventoryService.GetAvailableSaleUnits(product, retailPrice?.SizeMl);
        existingItem.Quantity = Math.Min(quantity, maxUnits);
        if (quantity > maxUnits)
            Message = $"الكمية المتبقية من {product.Name}: {maxUnits}.";

        WriteItems(items);
    }

    public Task RemoveFromCartAsync(int productId, int? retailPriceId = null)
    {
        Message = null;
        var items = ReadItems();
        var existingItem = items.FirstOrDefault(i =>
            i.ProductId == productId &&
            i.RetailPriceId == retailPriceId);
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
        var guestItems = ReadItems()
            .GroupBy(item => new { item.ProductId, item.RetailPriceId })
            .Select(group => new GuestCartItem
            {
                ProductId = group.Key.ProductId,
                RetailPriceId = group.Key.RetailPriceId,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToList();
        if (guestItems.Count == 0) return;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _cartLock.AcquireAsync(userId);

                var cart = await _context.Carts.SingleOrDefaultAsync(c => c.Userid == userId);
                if (cart == null)
                {
                    cart = new Cart { Userid = userId };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                var productIds = guestItems.Select(i => i.ProductId).Distinct().ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                var existingCartItems = await _context.Cartitems
                    .Where(i => i.Cartid == cart.Id && productIds.Contains(i.Productid))
                    .Include(i => i.RetailPrice)
                    .ToListAsync();

                var existingItems = existingCartItems.ToDictionary(
                    item => (item.Productid, item.RetailPriceId));

                foreach (var guestItem in guestItems)
                {
                    if (!products.TryGetValue(guestItem.ProductId, out var product) || product.Stockquantity <= 0)
                        continue;

                    var retailPrice = await _inventoryService.ValidateRetailPriceAsync(product, guestItem.RetailPriceId);
                    var maxUnits = _inventoryService.GetAvailableSaleUnits(product, retailPrice?.SizeMl);
                    if (maxUnits <= 0)
                        continue;

                    var key = (guestItem.ProductId, guestItem.RetailPriceId);
                    if (existingItems.TryGetValue(key, out var existingItem))
                    {
                        existingItem.Quantity = Math.Min(existingItem.Quantity + guestItem.Quantity, maxUnits);
                        continue;
                    }

                    var newItem = new Cartitem
                    {
                        Cart = cart,
                        Productid = guestItem.ProductId,
                        RetailPriceId = guestItem.RetailPriceId,
                        Quantity = Math.Min(guestItem.Quantity, maxUnits)
                    };
                    _context.Cartitems.Add(newItem);
                    existingItems.Add(key, newItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogWarning(rollbackException, "Rollback failed while merging a guest cart for user {UserId}.", userId);
                }

                _context.ChangeTracker.Clear();
                _logger.LogError(exception, "Merging the guest cart failed for user {UserId}.", userId);
                await _context.Database.CloseConnectionAsync();
                throw;
            }
        });

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
                .GroupBy(i => new { i.ProductId, i.RetailPriceId })
                .Select(g => new GuestCartItem
                {
                    ProductId = g.Key.ProductId,
                    RetailPriceId = g.Key.RetailPriceId,
                    Quantity = g.Sum(i => i.Quantity)
                })
                .ToList() ?? [];
        }
        catch (FormatException exception)
        {
            _logger.LogWarning(exception, "Discarding a malformed guest-cart cookie.");
            Clear();
            return [];
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Discarding an invalid guest-cart cookie payload.");
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
        public int? RetailPriceId { get; set; }
        public int Quantity { get; set; }
    }
}
