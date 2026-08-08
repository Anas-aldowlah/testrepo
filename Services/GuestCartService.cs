using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class GuestCartService
{
    private const string CookieName = "YAGOT.GuestCart";
    private const int CartLockNamespace = 149745236;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly NeondbContext _context;

    public string? Message { get; private set; }

    public GuestCartService(IHttpContextAccessor httpContextAccessor, NeondbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public async Task<Cart> GetCartAsync()
    {
        var guestItems = ReadItems();
        var productIds = guestItems.Select(i => i.ProductId).ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();
        var productsById = products.ToDictionary(p => p.Id);

        var cartItems = guestItems
            .Select(item =>
            {
                if (!productsById.TryGetValue(item.ProductId, out var product)) return null;

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
        var guestItems = ReadItems();
        if (guestItems.Count == 0) return;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({CartLockNamespace}, {userId})");

                var cart = await _context.Carts.SingleOrDefaultAsync(c => c.Userid == userId);
                if (cart == null)
                {
                    cart = new Cart { Userid = userId };
                    _context.Carts.Add(cart);
                }

                var productIds = guestItems.Select(i => i.ProductId).Distinct().ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                var existingItems = cart.Id == 0
                    ? new Dictionary<int, Cartitem>()
                    : await _context.Cartitems
                        .Where(i => i.Cartid == cart.Id && productIds.Contains(i.Productid))
                        .ToDictionaryAsync(i => i.Productid);

                foreach (var guestItem in guestItems)
                {
                    if (!products.TryGetValue(guestItem.ProductId, out var product) || product.Stockquantity <= 0)
                        continue;

                    if (existingItems.TryGetValue(guestItem.ProductId, out var existingItem))
                    {
                        existingItem.Quantity = Math.Min(
                            existingItem.Quantity + guestItem.Quantity,
                            product.Stockquantity);
                        continue;
                    }

                    var newItem = new Cartitem
                    {
                        Cart = cart,
                        Productid = guestItem.ProductId,
                        Quantity = Math.Min(guestItem.Quantity, product.Stockquantity)
                    };
                    _context.Cartitems.Add(newItem);
                    existingItems.Add(guestItem.ProductId, newItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Preserve the original exception if rollback also fails.
                }

                _context.ChangeTracker.Clear();
                try
                {
                    await _context.Database.CloseConnectionAsync();
                }
                catch
                {
                    // A broken Npgsql connector is already discarded by the pool.
                }

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
