using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class CartService
{
    private readonly NeondbContext _context;

    public string? MESSAGE = null;

    public CartService(NeondbContext context)
    {
        _context = context;
    }

    public async Task<Cart> GetCartAsync(int userId)
    {
        var cart = await _context.Carts.FirstOrDefaultAsync(c => c.Userid == userId);
        if (cart == null)
        {
            cart = new Cart { Userid = userId };
            await _context.Carts.AddAsync(cart);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request may have created the user's one allowed cart.
                _context.Entry(cart).State = EntityState.Detached;
                var concurrentCart = await _context.Carts.SingleOrDefaultAsync(c => c.Userid == userId);
                if (concurrentCart == null)
                    throw;

                cart = concurrentCart;
            }
        }

        cart.Cartitems = await _context.Cartitems
            .Where(ci => ci.Cartid == cart.Id)
            .Include(ci => ci.Product)
            .ToListAsync();

        return cart;
    }

    public async Task AddToCartAsync(int userId, int productId, int quantity)
    {
        MESSAGE = null;
        quantity = Math.Max(1, quantity);

        var cart = await GetCartAsync(userId);
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null || product.Stockquantity <= 0)
        {
            MESSAGE = "هذا المنتج غير متوفر حالياً.";
            return;
        }

        var existingItem = await _context.Cartitems.FirstOrDefaultAsync(i => i.Cartid == cart.Id && i.Productid == productId);
        if (existingItem != null)
        {
            var requestedQuantity = existingItem.Quantity + quantity;
            existingItem.Quantity = Math.Min(requestedQuantity, product.Stockquantity);
            if (requestedQuantity > product.Stockquantity)
            {
                MESSAGE = $"الكمية المتبقية من {product.Name}: {product.Stockquantity}.";
            }

            await _context.SaveChangesAsync();
            return;
        }

        if (quantity > product.Stockquantity)
        {
            MESSAGE = $"الكمية المتبقية من {product.Name}: {product.Stockquantity}.";
            quantity = product.Stockquantity;
        }

        _context.Cartitems.Add(new Cartitem
        {
            Cartid = cart.Id,
            Productid = productId,
            Quantity = quantity,
            Product = product
        });
        await _context.SaveChangesAsync();
    }

    public async Task UpdateQuantityAsync(int userId, int cartItemId, int quantity)
    {
        MESSAGE = null;

        var cart = await GetCartAsync(userId);
        var item = await _context.Cartitems.FirstOrDefaultAsync(i => i.Id == cartItemId && i.Cartid == cart.Id);
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

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.Productid);
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
    }

    public async Task RemoveFromCartAsync(int userId, int cartItemId)
    {
        var cart = await GetCartAsync(userId);
        var item = await _context.Cartitems.FindAsync(cartItemId);
        if (item != null && item.Cartid == cart.Id)
        {
            _context.Cartitems.Remove(item);
            try
            {
                await _context.SaveChangesAsync();
                MESSAGE = "تم حذف العنصر بنجاح";
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                MESSAGE = $"فشلت عملية الحذف: {ex.Message}";
            }
        }
        else
        {
            MESSAGE = "العنصر غير موجود في سلتك";
        }
    }

    public async Task ClearCartAsync(int userId)
    {
        var cart = await GetCartAsync(userId);
        var items = _context.Cartitems.Where(ci => ci.Cartid == cart.Id).ToList();

        foreach (var item in items)
        {
            _context.Cartitems.Remove(item);
        }

        await _context.SaveChangesAsync();
    }
}
