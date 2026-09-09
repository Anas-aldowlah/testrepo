using Xunit;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class ProductRetailCardSelectionTests
{
    [Fact]
    public void RetailProduct_InStockWithFullBottle_ShowsSelectSizeAndNotAddToCart()
    {
        // Arrange: Perfume sold by retail with 150ml in stock (VolumeMl = 100ml)
        var product = new Product
        {
            Id = 1,
            Name = "عطر الياقوت الملكي",
            StockUnit = "Ml",
            VolumeMl = 100,
            Stockquantity = 150,
            IsRetailEnabled = true,
            Price = 250m,
            RetailPrices =
            [
                new ProductRetailPrice { Id = 10, SizeMl = 10, Price = 35m, IsActive = true },
                new ProductRetailPrice { Id = 11, SizeMl = 50, Price = 140m, IsActive = true }
            ]
        };

        // Act
        var isAvailable = ProductRetailAvailability.IsSellable(product);
        var isRetailAvailable = ProductRetailAvailability.IsAvailable(product);
        var isBaseOptionAvailable = ProductRetailAvailability.IsBaseOptionSellable(product);

        // Assert: Product is available, retail is available, and base option quick-add is suppressed
        Assert.True(isAvailable, "Product should be sellable.");
        Assert.True(isRetailAvailable, "Product should be effectively retail available.");
        Assert.False(isBaseOptionAvailable, "Direct base option quick-add should be false for retail-enabled products.");
    }

    [Fact]
    public void RetailProduct_InStockWithPartialBottle_ShowsSelectSize()
    {
        // Arrange: Perfume sold by retail with 30ml in stock (less than full 100ml bottle, but enough for 10ml size)
        var product = new Product
        {
            Id = 2,
            Name = "مسك الختام",
            StockUnit = "Ml",
            VolumeMl = 100,
            Stockquantity = 30,
            IsRetailEnabled = true,
            Price = 200m,
            RetailPrices =
            [
                new ProductRetailPrice { Id = 20, SizeMl = 10, Price = 30m, IsActive = true },
                new ProductRetailPrice { Id = 21, SizeMl = 50, Price = 110m, IsActive = true }
            ]
        };

        // Act
        var isAvailable = ProductRetailAvailability.IsSellable(product);
        var isRetailAvailable = ProductRetailAvailability.IsAvailable(product);
        var isBaseOptionAvailable = ProductRetailAvailability.IsBaseOptionSellable(product);

        // Assert
        Assert.True(isAvailable, "Product should be sellable because 30ml is enough for 10ml retail size.");
        Assert.True(isRetailAvailable, "Product should be retail available.");
        Assert.False(isBaseOptionAvailable, "Base option quick-add should be false.");
    }

    [Fact]
    public void RetailProduct_OutOfStock_ShowsOutOfStock()
    {
        // Arrange: Perfume with 0 stock
        var product = new Product
        {
            Id = 3,
            Name = "عنبر خاص",
            StockUnit = "Ml",
            VolumeMl = 100,
            Stockquantity = 0,
            IsRetailEnabled = true,
            Price = 300m,
            RetailPrices =
            [
                new ProductRetailPrice { Id = 30, SizeMl = 10, Price = 40m, IsActive = true }
            ]
        };

        // Act
        var isAvailable = ProductRetailAvailability.IsSellable(product);
        var isBaseOptionAvailable = ProductRetailAvailability.IsBaseOptionSellable(product);

        // Assert
        Assert.False(isAvailable, "Product should NOT be sellable when stock is 0.");
        Assert.False(isBaseOptionAvailable, "Base option should NOT be available when stock is 0.");
    }

    [Fact]
    public void Product_ConvertedFromRetailToPiece_ShowsAddToCartDirectly()
    {
        // Arrange: Product was retail, but converted in Admin to piece (StockUnit = "Piece", IsRetailEnabled = false)
        var product = new Product
        {
            Id = 4,
            Name = "بخور الفاخر",
            StockUnit = "Piece",
            VolumeMl = null,
            Stockquantity = 25,
            IsRetailEnabled = false,
            Price = 75m,
            RetailPrices = []
        };

        // Act
        var isAvailable = ProductRetailAvailability.IsSellable(product);
        var isRetailAvailable = ProductRetailAvailability.IsAvailable(product);
        var isBaseOptionAvailable = ProductRetailAvailability.IsBaseOptionSellable(product);

        // Assert
        Assert.True(isAvailable, "Piece product should be sellable.");
        Assert.False(isRetailAvailable, "Piece product should NOT be retail available.");
        Assert.True(isBaseOptionAvailable, "Piece product in stock should have base option sellable (أضف للسلة).");
    }

    [Fact]
    public void Product_ConvertedFromPieceToRetail_ShowsSelectSize()
    {
        // Arrange: Product was piece, then converted in Admin to retail
        var product = new Product
        {
            Id = 5,
            Name = "دهن عود سيوفي",
            StockUnit = "Ml",
            VolumeMl = 12,
            Stockquantity = 50,
            IsRetailEnabled = true,
            Price = 500m,
            RetailPrices =
            [
                new ProductRetailPrice { Id = 50, SizeMl = 3, Price = 150m, IsActive = true },
                new ProductRetailPrice { Id = 51, SizeMl = 6, Price = 280m, IsActive = true }
            ]
        };

        // Act
        var isAvailable = ProductRetailAvailability.IsSellable(product);
        var isRetailAvailable = ProductRetailAvailability.IsAvailable(product);
        var isBaseOptionAvailable = ProductRetailAvailability.IsBaseOptionSellable(product);

        // Assert
        Assert.True(isAvailable, "Converted retail product should be sellable.");
        Assert.True(isRetailAvailable, "Converted retail product should be retail available.");
        Assert.False(isBaseOptionAvailable, "Direct quick-add should be false for retail product; card must show اختر الحجم.");
    }
}
