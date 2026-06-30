using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;
namespace YAGOT_2._0.Services;

public class CartService
{
    private readonly NeondbContext _context;

    public string ? MESSAGE = null;

    public CartService(NeondbContext context)
    {
        _context = context;
    }

    public int nextCartId()
    {
        
        return _context.Carts.Any() ? _context.Carts.Max(c => c.Id) + 1 : 1;
    }
    public int nextCartItemId()
    {
        return _context.Cartitems.Any() ? _context.Cartitems.Max(ci => ci.Id) + 1 : 1;
    }
    public async Task<Cart> GetCartAsync(int userId)
    {
        var cart = await _context.Carts.FirstOrDefaultAsync(c => c.Userid == userId);
        if (cart == null)
        {
            cart = new Cart { Id = nextCartId(), Userid = userId };
            await _context.Carts.AddAsync(cart);
            await _context.SaveChangesAsync();
        }
        // تحميل عناصر السلة من جدول CartItems
        cart.Cartitems = await _context.Cartitems.Where(ci => ci.Cartid == cart.Id).ToListAsync();
        // ربط المنتج بعنصر السلة
        foreach (var item in cart.Cartitems)
        {
            item.Product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.Productid)!;
        }
        return cart;
    }

    public async Task AddToCartAsync(int userId, int productId, int quantity)
    {
        var cart = await GetCartAsync(userId);
        var existingItem = _context.Cartitems.FirstOrDefault(i => i.Cartid == cart.Id && i.Productid == productId);

        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
            var product = _context.Products.FirstOrDefault(p => p.Id == productId);
            if (product.Stockquantity < existingItem.Quantity)
            {
                MESSAGE = $"كمية {product.Name} المتبقية : {product.Stockquantity} اقل من الكمية المطلوبة : {existingItem.Quantity}";
            }
            await _context.SaveChangesAsync();
        }
        else
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == productId);
            if (product != null && product.Stockquantity >= quantity)
            {
                _context.Cartitems.Add(new Cartitem
                {
                    Id = nextCartItemId(),
                    Cartid = cart.Id,
                    Productid = productId,
                    Quantity = quantity,
                    Product = product
                });
                await _context.SaveChangesAsync();
            }
        }
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
