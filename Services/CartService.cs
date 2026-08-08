using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class CartService
{
    private const int CartLockNamespace = 149745236;
    private readonly NeondbContext _context;
    private readonly ILogger<CartService> _logger;

    public string? MESSAGE = null;

    public CartService(NeondbContext context, ILogger<CartService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Cart> GetCartAsync(int userId)
    {
        try
        {
            var cart = await _context.Carts
                .AsNoTracking()
                .Include(c => c.Cartitems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.Category)
                .SingleOrDefaultAsync(c => c.Userid == userId);

            // Reading an empty cart must not write to the database. The persisted
            // cart is created only when the user actually adds an item.
            return cart ?? new Cart { Userid = userId };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not load cart for user {UserId}; returning an empty cart.",
                userId);
            return new Cart { Userid = userId };
        }
    }

    public async Task AddToCartAsync(int userId, int productId, int quantity)
    {
        var quantityToAdd = Math.Max(1, quantity);

        await ExecuteInCartTransactionAsync(async () =>
        {
            MESSAGE = null;
            await LockUserCartAsync(userId);

            var cart = await GetOrCreateCartAsync(userId);
            var product = await _context.Products.SingleOrDefaultAsync(p => p.Id == productId);
            if (product == null || product.Stockquantity <= 0)
            {
                MESSAGE = "هذا المنتج غير متوفر حالياً.";
                return;
            }

            var existingItem = await _context.Cartitems
                .Where(i => i.Cartid == cart.Id && i.Productid == productId)
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync();
            if (existingItem != null)
            {
                var requestedQuantity = existingItem.Quantity + quantityToAdd;
                existingItem.Quantity = Math.Min(requestedQuantity, product.Stockquantity);
                if (requestedQuantity > product.Stockquantity)
                {
                    MESSAGE = $"الكمية المتبقية من {product.Name}: {product.Stockquantity}.";
                }

                await _context.SaveChangesAsync();
                return;
            }

            var quantityToSave = quantityToAdd;
            if (quantityToSave > product.Stockquantity)
            {
                MESSAGE = $"الكمية المتبقية من {product.Name}: {product.Stockquantity}.";
                quantityToSave = product.Stockquantity;
            }

            _context.Cartitems.Add(new Cartitem
            {
                Cartid = cart.Id,
                Productid = productId,
                Quantity = quantityToSave
            });
            await _context.SaveChangesAsync();
        });
    }

    public async Task UpdateQuantityAsync(int userId, int cartItemId, int quantity)
    {
        await ExecuteInCartTransactionAsync(async () =>
        {
            MESSAGE = null;
            await LockUserCartAsync(userId);

            var item = await _context.Cartitems
                .Include(i => i.Product)
                .SingleOrDefaultAsync(i => i.Id == cartItemId && i.Cart.Userid == userId);
            if (item == null)
            {
                MESSAGE = "العنصر غير موجود في سلتك";
                return;
            }

            if (quantity <= 0)
            {
                _context.Cartitems.Remove(item);
                await _context.SaveChangesAsync();
                MESSAGE = "تم حذف العنصر بنجاح";
                return;
            }

            var product = item.Product;
            if (product == null || product.Stockquantity <= 0)
            {
                _context.Cartitems.Remove(item);
                await _context.SaveChangesAsync();
                MESSAGE = "هذا المنتج غير متوفر حالياً.";
                return;
            }

            item.Quantity = Math.Min(quantity, product.Stockquantity);
            if (quantity > product.Stockquantity)
            {
                MESSAGE = $"الكمية المتبقية من {product.Name}: {product.Stockquantity}.";
            }

            await _context.SaveChangesAsync();
        });
    }

    public async Task RemoveFromCartAsync(int userId, int cartItemId)
    {
        await ExecuteInCartTransactionAsync(async () =>
        {
            MESSAGE = null;
            await LockUserCartAsync(userId);

            var deleted = await _context.Cartitems
                .Where(i => i.Id == cartItemId && i.Cart.Userid == userId)
                .ExecuteDeleteAsync();

            MESSAGE = deleted > 0
                ? "تم حذف العنصر بنجاح"
                : "العنصر غير موجود في سلتك";
        });
    }

    public async Task ClearCartAsync(int userId)
    {
        await ExecuteInCartTransactionAsync(async () =>
        {
            await LockUserCartAsync(userId);
            await _context.Cartitems
                .Where(ci => ci.Cart.Userid == userId)
                .ExecuteDeleteAsync();
        });
    }

    private async Task<Cart> GetOrCreateCartAsync(int userId)
    {
        var cart = await _context.Carts.SingleOrDefaultAsync(c => c.Userid == userId);
        if (cart != null)
            return cart;

        cart = new Cart { Userid = userId };
        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();
        return cart;
    }

    private async Task LockUserCartAsync(int userId)
    {
        // Serialize cart mutations for this user even before a cart row exists.
        // This avoids entity tracking/composable SQL issues and does not depend on
        // a UserSite row being materialized merely to acquire a lock.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({CartLockNamespace}, {userId})");
    }

    private async Task ExecuteInCartTransactionAsync(Func<Task> operation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await operation();
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
    }
}
