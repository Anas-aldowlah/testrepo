using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class CartService
{
    private readonly NeondbContext _context;
    private readonly ILogger<CartService> _logger;
    private readonly CartLockService _cartLock;
    private readonly IInventoryService _inventoryService;

    public string? MESSAGE = null;
    public string? WARNING_CODE = null;
    public int? AVAILABLE_QUANTITY = null;

    public CartService(
        NeondbContext context,
        ILogger<CartService> logger,
        CartLockService cartLock,
        IInventoryService inventoryService)
    {
        _context = context;
        _logger = logger;
        _cartLock = cartLock;
        _inventoryService = inventoryService;
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
                .Include(c => c.Cartitems)
                    .ThenInclude(ci => ci.RetailPrice)
                .SingleOrDefaultAsync(c => c.Userid == userId);

            return cart ?? new Cart { Userid = userId };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load cart for user {UserId}.", userId);
            throw;
        }
    }

    public async Task AddToCartAsync(int userId, int productId, int quantity, int? retailPriceId = null)
    {
        var quantityToAdd = Math.Max(1, quantity);

        await ExecuteInCartTransactionAsync(userId, "add item", async () =>
        {
            MESSAGE = null;
            WARNING_CODE = null;
            AVAILABLE_QUANTITY = null;
            await _cartLock.AcquireAsync(userId);

            var cart = await GetOrCreateCartAsync(userId);
            var product = await _context.Products.SingleOrDefaultAsync(p => p.Id == productId);
            if (product == null || product.Stockquantity <= 0)
            {
                MESSAGE = "هذا المنتج غير متوفر حالياً.";
                WARNING_CODE = "InsufficientStock";
                AVAILABLE_QUANTITY = 0;
                return;
            }

            var retailPrice = await _inventoryService.ValidateRetailPriceAsync(product, retailPriceId);
            var maxUnits = _inventoryService.GetAvailableSaleUnits(product, retailPrice?.SizeMl);
            if (maxUnits <= 0)
            {
                MESSAGE = "هذا المنتج غير متوفر حالياً.";
                WARNING_CODE = "InsufficientStock";
                AVAILABLE_QUANTITY = 0;
                return;
            }

            var existingItem = await _context.Cartitems
                .Where(i => i.Cartid == cart.Id &&
                            i.Productid == productId &&
                            i.RetailPriceId == retailPriceId)
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync();

            if (existingItem != null)
            {
                var requestedQuantity = checked(existingItem.Quantity + quantityToAdd);
                existingItem.Quantity = Math.Min(requestedQuantity, maxUnits);
                if (requestedQuantity > maxUnits)
                {
                    MESSAGE = $"المتوفر حاليًا {(maxUnits == 1 ? "قطعة واحدة" : maxUnits == 2 ? "قطعتان" : maxUnits + " قطع")} فقط.";
                    WARNING_CODE = "InsufficientStock";
                    AVAILABLE_QUANTITY = maxUnits;
                }

                await _context.SaveChangesAsync();
                return;
            }

            var quantityToSave = Math.Min(quantityToAdd, maxUnits);
            if (quantityToAdd > maxUnits)
            {
                MESSAGE = $"المتوفر حاليًا {(maxUnits == 1 ? "قطعة واحدة" : maxUnits == 2 ? "قطعتان" : maxUnits + " قطع")} فقط.";
                WARNING_CODE = "InsufficientStock";
                AVAILABLE_QUANTITY = maxUnits;
            }

            _context.Cartitems.Add(new Cartitem
            {
                Cartid = cart.Id,
                Productid = productId,
                RetailPriceId = retailPriceId,
                Quantity = quantityToSave
            });
            await _context.SaveChangesAsync();
        });
    }

    public async Task UpdateQuantityAsync(
        int userId,
        int cartItemId,
        int quantity,
        int? expectedQuantity = null)
    {
        await ExecuteInCartTransactionAsync(userId, "update quantity", async () =>
        {
            MESSAGE = null;
            WARNING_CODE = null;
            AVAILABLE_QUANTITY = null;
            await _cartLock.AcquireAsync(userId);

            var item = await _context.Cartitems
                .Include(i => i.Product)
                .Include(i => i.RetailPrice)
                .SingleOrDefaultAsync(i => i.Id == cartItemId && i.Cart.Userid == userId);
            if (item == null)
            {
                throw new CartConcurrencyException(
                    "تغيرت السلة في نافذة أو جهاز آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.");
            }

            if (expectedQuantity.HasValue && item.Quantity != expectedQuantity.Value)
            {
                throw new CartConcurrencyException(
                    "تم تعديل كمية المنتج في نافذة أو جهاز آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.");
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
                WARNING_CODE = "InsufficientStock";
                AVAILABLE_QUANTITY = 0;
                return;
            }

            var maxUnits = _inventoryService.GetAvailableSaleUnits(product, item.RetailPrice?.SizeMl);
            if (quantity > maxUnits)
            {
                MESSAGE = $"الكمية المطلوبة غير متوفرة، المتوفر حاليًا {maxUnits} فقط.";
                WARNING_CODE = "InsufficientStock";
                AVAILABLE_QUANTITY = maxUnits;
                return;
            }

            item.Quantity = quantity;
            await _context.SaveChangesAsync();
        });
    }

    public async Task RemoveFromCartAsync(int userId, int cartItemId)
    {
        await ExecuteInCartTransactionAsync(userId, "remove item", async () =>
        {
            MESSAGE = null;
            await _cartLock.AcquireAsync(userId);

            var deleted = await _context.Cartitems
                .Where(i => i.Id == cartItemId && i.Cart.Userid == userId)
                .ExecuteDeleteAsync();

            if (deleted == 0)
                throw new CartConcurrencyException(
                    "تغيرت السلة في نافذة أو جهاز آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.");

            MESSAGE = "تم حذف العنصر بنجاح";
        });
    }

    public async Task ClearCartAsync(int userId)
    {
        await ExecuteInCartTransactionAsync(userId, "clear cart", async () =>
        {
            await _cartLock.AcquireAsync(userId);
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

    private async Task ExecuteInCartTransactionAsync(
        int userId,
        string operationName,
        Func<Task> operation)
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
            catch (DbUpdateConcurrencyException exception)
            {
                await RollbackAsync(transaction, userId, operationName);
                _context.ChangeTracker.Clear();
                _logger.LogWarning(exception, "Cart concurrency conflict while attempting to {OperationName} for user {UserId}.", operationName, userId);
                throw new CartConcurrencyException("تغيرت السلة في نافذة أو جهاز آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.", exception);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintConflict(exception))
            {
                await RollbackAsync(transaction, userId, operationName);
                _context.ChangeTracker.Clear();
                _logger.LogWarning(exception, "Cart uniqueness conflict while attempting to {OperationName} for user {UserId}.", operationName, userId);
                throw new CartConcurrencyException("تم تعديل السلة بالتزامن من نافذة أو جهاز آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.", exception);
            }
            catch (CartConcurrencyException exception)
            {
                await RollbackAsync(transaction, userId, operationName);
                _context.ChangeTracker.Clear();
                _logger.LogWarning(exception, "Cart state changed while attempting to {OperationName} for user {UserId}.", operationName, userId);
                throw;
            }
            catch (Exception exception)
            {
                await RollbackAsync(transaction, userId, operationName);
                _context.ChangeTracker.Clear();
                _logger.LogError(exception, "Cart operation {OperationName} failed for user {UserId}.", operationName, userId);
                await CloseConnectionAfterFailureAsync(userId, operationName);
                throw;
            }
        });
    }

    private async Task RollbackAsync(IDbContextTransaction transaction, int userId, string operationName)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            _logger.LogWarning(rollbackException, "Rollback failed after cart operation {OperationName} for user {UserId}.", operationName, userId);
        }
    }

    private async Task CloseConnectionAfterFailureAsync(int userId, string operationName)
    {
        try
        {
            await _context.Database.CloseConnectionAsync();
        }
        catch (Exception closeException)
        {
            _logger.LogWarning(closeException, "Closing the database connection failed after cart operation {OperationName} for user {UserId}.", operationName, userId);
        }
    }

    private static bool IsUniqueConstraintConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
}
