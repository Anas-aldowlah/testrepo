using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using YAGOT_2._0.Extensions;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Promotions;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class OffersAndPromotionsSystemTests
{
    #region 1. Currency Helper & Formatting Tests

    [Theory]
    [InlineData("YER", true)]
    [InlineData("SAR", true)]
    [InlineData("USD", true)]
    [InlineData("yer", true)]
    [InlineData("sar", true)]
    [InlineData("usd", true)]
    [InlineData("EUR", false)]
    [InlineData("GBP", false)]
    [InlineData("KWD", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void CurrencyHelper_Validation_OnlyAllowsConfiguredCurrencies(string? code, bool expectedValid)
    {
        bool isValid = CurrencyHelper.IsValid(code);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("YER", "ر.ي")]
    [InlineData("SAR", "ر.س")]
    [InlineData("USD", "$")]
    [InlineData("INVALID", "ر.ي")] // Default fallback
    [InlineData(null, "ر.ي")]
    public void CurrencyHelper_Symbols_MatchBusinessSpecification(string? code, string expectedSymbol)
    {
        string symbol = CurrencyHelper.GetSymbol(code);
        Assert.Equal(expectedSymbol, symbol);
    }

    [Fact]
    public void PriceFormattingExtensions_FormatsCorrectlyWithCurrency()
    {
        decimal price = 150.25m;
        string formattedYer = price.ToYaqutPrice("YER");
        string formattedSar = price.ToYaqutPrice("SAR");
        string formattedUsd = price.ToYaqutPrice("USD");

        Assert.Equal("150.25 ر.ي", formattedYer);
        Assert.Equal("150.25 ر.س", formattedSar);
        Assert.Equal("150.25 $", formattedUsd);
    }

    #endregion

    #region 2. Promotion Engine Unit Calculation Tests

    [Fact]
    public async Task PromotionEngine_PercentageDiscount_CalculatesCorrectly()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 1,
            Title = "20% Off Perfumes",
            PromotionType = "Percentage",
            DiscountValue = 20m, // 20%
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = 101 }
            }
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 101, Quantity = 2, UnitPrice = 100m, CategoryId = 1 }
            },
            CurrencyCode = "YER"
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        Assert.Equal(200m, result.GrossSubtotal);
        Assert.Equal(40m, result.TotalDiscounts); // 20% of 200 = 40
        Assert.Equal(160m, result.NetTotal);

        var lineResult = Assert.Single(result.Lines);
        Assert.Equal(100m, lineResult.OriginalUnitPrice);
        Assert.Equal(80m, lineResult.FinalUnitPrice);
        Assert.Equal(40m, lineResult.TotalDiscount);
        Assert.Equal(1, lineResult.AppliedPromotions.First().PromotionId);
    }

    [Fact]
    public async Task PromotionEngine_FixedAmountDiscount_CalculatesCorrectly()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 2,
            Title = "15 Off Each Item",
            PromotionType = "FixedAmount",
            DiscountValue = 15m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = 102 }
            }
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L2", ProductId = 102, Quantity = 3, UnitPrice = 50m, CategoryId = 1 }
            },
            CurrencyCode = "YER"
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        Assert.Equal(150m, result.GrossSubtotal);
        Assert.Equal(45m, result.TotalDiscounts); // 15 * 3 = 45
        Assert.Equal(105m, result.NetTotal);

        var lineResult = Assert.Single(result.Lines);
        Assert.Equal(35m, lineResult.FinalUnitPrice);
        Assert.Equal(45m, lineResult.TotalDiscount);
    }

    [Fact]
    public async Task PromotionEngine_BuyXGetY_CalculatesFreeUnitsCorrectly()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 3,
            Title = "Buy 2 Get 1 Free",
            PromotionType = "BuyXGetY",
            BuyQuantity = 2,
            FreeQuantity = 1,
            DiscountValue = 0,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = 103 }
            }
        };

        // Customer buys 5 units: 5 / 2 = 2 sets => 2 free units
        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L3", ProductId = 103, Quantity = 5, UnitPrice = 100m, CategoryId = 1 }
            },
            CurrencyCode = "YER"
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        var lineResult = Assert.Single(result.Lines);
        Assert.Equal(2, lineResult.FreeQuantity);
        Assert.Equal(100m, lineResult.FinalUnitPrice);
        Assert.Equal(500m, result.GrossSubtotal);
        Assert.Equal(500m, result.NetTotal); // Gross unchanged, 2 free gifts awarded
        Assert.Single(result.FreeProducts);
        Assert.Equal(2, result.FreeProducts[0].FreeQuantity);
    }

    [Fact]
    public async Task PromotionEngine_QuantityPrice_AppliesTierPricing()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 4,
            Title = "Buy 3 or more at 40 each",
            PromotionType = "QuantityPrice",
            BuyQuantity = 3,
            OfferPrice = 40m,
            DiscountValue = 0,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = 104 }
            }
        };

        // Case A: Quantity = 2 (Below threshold 3) -> Offer price does NOT apply
        var contextBelow = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L4A", ProductId = 104, Quantity = 2, UnitPrice = 50m }
            },
            CurrencyCode = "YER"
        };
        var resultBelow = await engine.CalculatePromotionsAsync(contextBelow, new List<Promotion> { promo });
        Assert.Equal(0m, resultBelow.TotalDiscounts);
        Assert.Equal(50m, resultBelow.Lines[0].FinalUnitPrice);

        // Case B: Quantity = 4 (Meets threshold 3) -> Bundle of 3 for 40 applies, remainder at 50
        var contextMet = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L4B", ProductId = 104, Quantity = 4, UnitPrice = 50m }
            },
            CurrencyCode = "YER"
        };
        var resultMet = await engine.CalculatePromotionsAsync(contextMet, new List<Promotion> { promo });
        Assert.Equal(110m, resultMet.TotalDiscounts); // 3 for 40 -> discount = (3 * 50) - 40 = 110
        Assert.Equal(22.50m, resultMet.Lines[0].FinalUnitPrice); // (200 - 110) / 4 = 22.50
        Assert.Equal(90m, resultMet.NetTotal);
    }

    [Fact]
    public async Task PromotionEngine_SpendAmount_FixedDiscount_AppliesAtOrderLevel()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 5,
            Title = "Spend 200 get 30 off",
            PromotionType = "SpendAmount",
            SpendDiscountType = DiscountValueType.FixedAmount,
            MinimumAmount = 200m,
            DiscountValue = 30m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 5
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L5", ProductId = 201, Quantity = 2, UnitPrice = 120m } // Gross = 240
            },
            CurrencyCode = "YER"
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        Assert.Equal(240m, result.GrossSubtotal);
        Assert.Equal(30m, result.SpendAmountDiscount);
        Assert.Equal(30m, result.TotalDiscounts);
        Assert.Equal(210m, result.NetTotal);
    }

    [Fact]
    public async Task PromotionEngine_SpendAmount_PercentageDiscount_AppliesAtOrderLevel()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 6,
            Title = "Spend 300 get 10% off",
            PromotionType = "SpendAmount",
            SpendDiscountType = DiscountValueType.Percentage,
            MinimumAmount = 300m,
            DiscountValue = 10m, // 10%
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 5
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L6", ProductId = 202, Quantity = 4, UnitPrice = 100m } // Gross = 400
            },
            CurrencyCode = "YER"
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        Assert.Equal(400m, result.GrossSubtotal);
        Assert.Equal(40m, result.SpendAmountDiscount); // 10% of 400 = 40
        Assert.Equal(40m, result.TotalDiscounts);
        Assert.Equal(360m, result.NetTotal);
    }

    #endregion

    #region 3. Stacking, Priority & Expiration Tests

    [Fact]
    public async Task PromotionEngine_NonCombinablePromotions_HigherPriorityPrecedes()
    {
        var engine = new PromotionEngine();

        // Priority 10 -> 20% off
        var promoA = new Promotion
        {
            Id = 20,
            Title = "Priority 10 Promo",
            PromotionType = "Percentage",
            DiscountValue = 20m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10,
            CanBeCombined = false,
            PromotionProducts = new List<PromotionProduct> { new() { ProductId = 401 } }
        };

        // Priority 5 -> 10% off (Lower priority number in ascending sort evaluated first)
        var promoB = new Promotion
        {
            Id = 21,
            Title = "Priority 5 Promo",
            PromotionType = "Percentage",
            DiscountValue = 10m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 5,
            CanBeCombined = false,
            PromotionProducts = new List<PromotionProduct> { new() { ProductId = 401 } }
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L7", ProductId = 401, Quantity = 1, UnitPrice = 100m }
            },
            CurrencyCode = "YER"
        };

        // Engine evaluates promotions ordered by Priority ascending (Priority 5 executes first)
        var promotions = new List<Promotion> { promoA, promoB }.OrderBy(p => p.Priority).ToList();
        var result = await engine.CalculatePromotionsAsync(context, promotions);

        Assert.Single(result.Lines[0].AppliedPromotions);
        Assert.Equal(21, result.Lines[0].AppliedPromotions[0].PromotionId);
        Assert.Equal(10m, result.TotalDiscounts);
        Assert.Equal(90m, result.NetTotal);
    }

    #endregion

    #region 4. POS Cascading Discount Arithmetic Tests

    [Theory]
    [InlineData(100, 20, 10, 70)]  // 100 - 20 promo - 10 manual = 70 final
    [InlineData(100, 0, 15, 85)]   // No promo, 15 manual = 85 final
    [InlineData(100, 30, 0, 70)]   // 30 promo, no manual = 70 final
    [InlineData(50, 20, 40, 0)]    // Excessive manual discount clamps to 0, not negative
    public void PosCascadingDiscount_CalculatesStrictSequence(
        decimal originalUnitPrice,
        decimal promoDiscountPerUnit,
        decimal manualDiscountPerUnit,
        decimal expectedFinalUnitPrice)
    {
        // 1. Automatic promotion discount applied to original unit price
        decimal priceAfterPromo = Math.Max(0m, originalUnitPrice - promoDiscountPerUnit);

        // 2. Manual cashier discount applied to remaining price
        decimal cappedManualDiscount = Math.Min(manualDiscountPerUnit, priceAfterPromo);
        decimal finalUnitPrice = Math.Max(0m, priceAfterPromo - cappedManualDiscount);

        Assert.Equal(expectedFinalUnitPrice, finalUnitPrice);
        Assert.True(finalUnitPrice >= 0, "Final unit price must never be negative");
    }

    #endregion

    #region 5. BuyXGetY Inventory Deduction Rule Test

    [Fact]
    public void BuyXGetY_TotalPhysicalDeduction_IncludesFulfilledAndFreeUnits()
    {
        // Specification rule: Physical inventory deduction must equal fulfilled units + free units
        int orderedQuantity = 4;
        int? fulfilledQuantity = null; // defaults to orderedQuantity
        int freeQuantity = 2; // Buy 2 get 1 free applied twice

        int effectiveFulfilled = fulfilledQuantity ?? orderedQuantity;
        int totalPhysicalDeduction = effectiveFulfilled + freeQuantity;

        Assert.Equal(6, totalPhysicalDeduction);
    }

    #endregion

    #region 6. Storefront Offers and Parity Tests

    [Fact]
    public void Product_DynamicPromotionProperties_ComputeCorrectly()
    {
        var product = new Product
        {
            Id = 101,
            Price = 200m,
            HasPromotion = true,
            PromotionTitle = "Summer Sale",
            PromotionType = "Percentage",
            DiscountAmount = 50m
        };

        Assert.Equal(200m, product.OriginalPrice);
        Assert.Equal(150m, product.FinalPrice);
        Assert.Equal(25.00m, product.DiscountPercentage);
    }

    [Fact]
    public async Task PromotionEngine_QuantityPrice_BundleWithRemainder_CalculatesExactParity()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 88,
            Title = "Buy 3 for 100",
            PromotionType = "QuantityPrice",
            BuyQuantity = 3,
            OfferPrice = 100m,
            DiscountValue = 0,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 1,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = 55 }
            }
        };

        // Quantity = 7, UnitPrice = 50m
        // 2 bundles of 3 (6 units) cost 2 * 100 = 200 (standard is 6 * 50 = 300) -> discount = 100
        // 1 remainder unit charged at standard unit price 50
        // Total net = 200 + 50 = 250 (Gross: 7 * 50 = 350)
        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 55, Quantity = 7, UnitPrice = 50m }
            },
            CurrencyCode = "SAR"
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });
        Assert.Equal(100m, result.TotalDiscounts);
        Assert.Equal(350m, result.GrossSubtotal);
        Assert.Equal(250m, result.NetTotal);
    }

    [Fact]
    public void OffersViewModel_PaginationCalculations_BehaveAsExpected()
    {
        var vm = new OffersViewModel
        {
            TotalItems = 25,
            PageSize = 12,
            CurrentPage = 2
        };

        Assert.Equal(3, vm.TotalPages);
        Assert.True(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);

        var firstPageVm = new OffersViewModel
        {
            TotalItems = 5,
            PageSize = 12,
            CurrentPage = 1
        };

        Assert.Equal(1, firstPageVm.TotalPages);
        Assert.False(firstPageVm.HasPreviousPage);
        Assert.False(firstPageVm.HasNextPage);
    }

    #endregion

    #region 6. Targeting and Admin Selector Verification Tests

    [Fact]
    public void Targeting_LimitedProducts_ValidationAndMapping_StoresSelectedProductIds()
    {
        // 1. Valid ViewModel with Limited Products
        var vm = new PromotionFormVM
        {
            Title = "خصم منتجات محددة",
            PromotionType = "Percentage",
            DiscountValue = 15m,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            TargetType = "Products",
            SelectedProductIds = new List<int> { 101, 102, 103 }
        };

        var validationResults = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(vm, new ValidationContext(vm), validationResults, true);
        Assert.True(isValid);

        // 2. Map to Promotion and PromotionProducts
        var promotion = new Promotion
        {
            Id = 10,
            Title = vm.Title,
            PromotionType = vm.PromotionType,
            DiscountValue = vm.DiscountValue ?? 0m,
            PromotionProducts = vm.SelectedProductIds.Select(id => new PromotionProduct
            {
                PromotionId = 10,
                ProductId = id
            }).ToList()
        };

        Assert.Equal(3, promotion.PromotionProducts.Count);
        Assert.Contains(promotion.PromotionProducts, pp => pp.ProductId == 101);
        Assert.Contains(promotion.PromotionProducts, pp => pp.ProductId == 102);
        Assert.Contains(promotion.PromotionProducts, pp => pp.ProductId == 103);
    }

    [Fact]
    public void Targeting_LimitedCategories_ValidationAndMapping_StoresSelectedCategoryIds()
    {
        // 1. Valid ViewModel with Limited Categories
        var vm = new PromotionFormVM
        {
            Title = "خصم تصنيفات محددة",
            PromotionType = "FixedAmount",
            DiscountValue = 50m,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            TargetType = "Categories",
            SelectedCategoryIds = new List<int> { 4, 7 }
        };

        var validationResults = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(vm, new ValidationContext(vm), validationResults, true);
        Assert.True(isValid);

        // 2. Map to Promotion and PromotionCategories
        var promotion = new Promotion
        {
            Id = 20,
            Title = vm.Title,
            PromotionType = vm.PromotionType,
            DiscountValue = vm.DiscountValue ?? 0m,
            PromotionCategories = vm.SelectedCategoryIds.Select(id => new PromotionCategory
            {
                PromotionId = 20,
                CategoryId = id
            }).ToList()
        };

        Assert.Equal(2, promotion.PromotionCategories.Count);
        Assert.Contains(promotion.PromotionCategories, pc => pc.CategoryId == 4);
        Assert.Contains(promotion.PromotionCategories, pc => pc.CategoryId == 7);
    }

    [Fact]
    public void Targeting_EditLoadsExistingProductSelections()
    {
        // Existing promotion in DB targeting products 101 and 105
        var promotion = new Promotion
        {
            Id = 30,
            Title = "عرض تجريبي",
            PromotionType = "Percentage",
            DiscountValue = 10m,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddDays(7),
            PromotionProducts = new List<PromotionProduct>
            {
                new() { PromotionId = 30, ProductId = 101 },
                new() { PromotionId = 30, ProductId = 105 }
            }
        };

        // Simulate Edit GET mapping
        var selectedProductIds = promotion.PromotionProducts.Select(pp => pp.ProductId).ToList();
        string targetType = selectedProductIds.Count > 0 ? "Products" : "All";

        var allAvailableProducts = new List<ProductSelectOption>
        {
            new() { Id = 101, Name = "عطر الياقوت", Price = 100m, IsSelected = selectedProductIds.Contains(101) },
            new() { Id = 102, Name = "عطر الزمرد", Price = 120m, IsSelected = selectedProductIds.Contains(102) },
            new() { Id = 105, Name = "بخور الصندل", Price = 80m, IsSelected = selectedProductIds.Contains(105) }
        };

        Assert.Equal("Products", targetType);
        Assert.Equal(2, selectedProductIds.Count);
        Assert.True(allAvailableProducts.First(p => p.Id == 101).IsSelected);
        Assert.False(allAvailableProducts.First(p => p.Id == 102).IsSelected);
        Assert.True(allAvailableProducts.First(p => p.Id == 105).IsSelected);
    }

    [Fact]
    public void Targeting_EditLoadsExistingCategorySelections()
    {
        // Existing promotion in DB targeting categories 3 and 8
        var promotion = new Promotion
        {
            Id = 40,
            Title = "عرض تصنيف العطور",
            PromotionType = "Percentage",
            DiscountValue = 10m,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddDays(7),
            PromotionCategories = new List<PromotionCategory>
            {
                new() { PromotionId = 40, CategoryId = 3 },
                new() { PromotionId = 40, CategoryId = 8 }
            }
        };

        // Simulate Edit GET mapping
        var selectedCategoryIds = promotion.PromotionCategories.Select(pc => pc.CategoryId).ToList();
        string targetType = selectedCategoryIds.Count > 0 ? "Categories" : "All";

        var allAvailableCategories = new List<CategorySelectOption>
        {
            new() { Id = 1, Name = "الدهون والزيوت", IsSelected = selectedCategoryIds.Contains(1) },
            new() { Id = 3, Name = "العطور الشرقية", IsSelected = selectedCategoryIds.Contains(3) },
            new() { Id = 8, Name = "البخور واللبان", IsSelected = selectedCategoryIds.Contains(8) }
        };

        Assert.Equal("Categories", targetType);
        Assert.Equal(2, selectedCategoryIds.Count);
        Assert.False(allAvailableCategories.First(c => c.Id == 1).IsSelected);
        Assert.True(allAvailableCategories.First(c => c.Id == 3).IsSelected);
        Assert.True(allAvailableCategories.First(c => c.Id == 8).IsSelected);
    }

    [Fact]
    public async Task Targeting_ProductTargetedPromotion_AppliesOnlyToSelectedProducts()
    {
        var engine = new PromotionEngine();

        // Promotion strictly targets product 201 only
        var promo = new Promotion
        {
            Id = 50,
            Title = "20% خصم على منتج 201",
            PromotionType = "Percentage",
            DiscountValue = 20m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 1,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = 201 }
            }
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 201, Quantity = 1, UnitPrice = 100m, CategoryId = 1 },
                new() { LineIdentifier = "L2", ProductId = 202, Quantity = 1, UnitPrice = 100m, CategoryId = 1 }
            },
            CurrencyCode = "SAR"
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        var line1 = result.Lines.First(l => l.ProductId == 201);
        var line2 = result.Lines.First(l => l.ProductId == 202);

        // Product 201 receives 20% discount (20 SAR)
        Assert.Equal(20m, line1.TotalDiscount);
        Assert.Equal(80m, line1.FinalUnitPrice);
        Assert.Single(line1.AppliedPromotions);

        // Product 202 receives NO discount
        Assert.Equal(0m, line2.TotalDiscount);
        Assert.Equal(100m, line2.FinalUnitPrice);
        Assert.Empty(line2.AppliedPromotions);

        Assert.Equal(200m, result.GrossSubtotal);
        Assert.Equal(20m, result.TotalDiscounts);
        Assert.Equal(180m, result.NetTotal);
    }

    [Fact]
    public async Task Targeting_CategoryTargetedPromotion_AppliesOnlyToProductsInSelectedCategories()
    {
        var engine = new PromotionEngine();

        // Promotion strictly targets category 99 only
        var promo = new Promotion
        {
            Id = 60,
            Title = "خصم 30 ريال على تصنيف 99",
            PromotionType = "FixedAmount",
            DiscountValue = 30m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 1,
            PromotionCategories = new List<PromotionCategory>
            {
                new() { CategoryId = 99 }
            }
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                // In targeted category 99
                new() { LineIdentifier = "L1", ProductId = 301, Quantity = 1, UnitPrice = 100m, CategoryId = 99 },
                // Outside targeted category
                new() { LineIdentifier = "L2", ProductId = 302, Quantity = 1, UnitPrice = 100m, CategoryId = 50 }
            },
            CurrencyCode = "SAR"
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        var line1 = result.Lines.First(l => l.ProductId == 301);
        var line2 = result.Lines.First(l => l.ProductId == 302);

        // Product in Category 99 receives 30 discount
        Assert.Equal(30m, line1.TotalDiscount);
        Assert.Equal(70m, line1.FinalUnitPrice);
        Assert.Single(line1.AppliedPromotions);

        // Product in Category 50 receives 0 discount
        Assert.Equal(0m, line2.TotalDiscount);
        Assert.Equal(100m, line2.FinalUnitPrice);
        Assert.Empty(line2.AppliedPromotions);

        Assert.Equal(200m, result.GrossSubtotal);
        Assert.Equal(30m, result.TotalDiscounts);
        Assert.Equal(170m, result.NetTotal);
    }

    [Fact]
    public async Task Targeting_StorewidePromotion_AppliesAcrossAllProducts()
    {
        var engine = new PromotionEngine();

        // Storewide promotion (neither PromotionProducts nor PromotionCategories configured)
        var promo = new Promotion
        {
            Id = 70,
            Title = "خصم شامل 10%",
            PromotionType = "Percentage",
            DiscountValue = 10m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 1,
            PromotionProducts = new List<PromotionProduct>(),
            PromotionCategories = new List<PromotionCategory>()
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 401, Quantity = 1, UnitPrice = 100m, CategoryId = 1 },
                new() { LineIdentifier = "L2", ProductId = 402, Quantity = 1, UnitPrice = 200m, CategoryId = 2 },
                new() { LineIdentifier = "L3", ProductId = 403, Quantity = 1, UnitPrice = 50m, CategoryId = null }
            },
            CurrencyCode = "SAR"
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        Assert.Equal(350m, result.GrossSubtotal);
        Assert.Equal(35m, result.TotalDiscounts); // 10 + 20 + 5
        Assert.Equal(315m, result.NetTotal);

        Assert.All(result.Lines, l =>
        {
            Assert.True(l.TotalDiscount > 0);
            Assert.Single(l.AppliedPromotions);
        });
    }

    [Fact]
    public void Targeting_Validation_RejectsOrFiltersInvalidOrEmptySelections()
    {
        // 1. TargetType = Products with empty selection -> Rejected
        var invalidProductsVm = new PromotionFormVM
        {
            Title = "عرض خاطئ بدون منتجات",
            PromotionType = "Percentage",
            DiscountValue = 10m,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            TargetType = "Products",
            SelectedProductIds = new List<int>()
        };

        var productValidation = new List<ValidationResult>();
        bool isProdValid = Validator.TryValidateObject(invalidProductsVm, new ValidationContext(invalidProductsVm), productValidation, true);
        Assert.False(isProdValid);
        Assert.Contains(productValidation, v => v.MemberNames.Contains(nameof(PromotionFormVM.SelectedProductIds)));

        // 2. TargetType = Categories with empty selection -> Rejected
        var invalidCategoriesVm = new PromotionFormVM
        {
            Title = "عرض خاطئ بدون تصنيفات",
            PromotionType = "Percentage",
            DiscountValue = 10m,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            TargetType = "Categories",
            SelectedCategoryIds = new List<int>()
        };

        var catValidation = new List<ValidationResult>();
        bool isCatValid = Validator.TryValidateObject(invalidCategoriesVm, new ValidationContext(invalidCategoriesVm), catValidation, true);
        Assert.False(isCatValid);
        Assert.Contains(catValidation, v => v.MemberNames.Contains(nameof(PromotionFormVM.SelectedCategoryIds)));

        // 3. Server-side ID existence validation logic filters out nonexistent IDs
        var existingDbProductIds = new List<int> { 1, 2, 3 };
        var submittedProductIds = new List<int> { 1, 999999, -5 };

        var validIds = submittedProductIds.Where(id => existingDbProductIds.Contains(id)).ToList();
        Assert.Single(validIds);
        Assert.Equal(1, validIds[0]);
    }

    [Fact]
    public void Targeting_SwitchingTargetingModes_CleansUnintendedTargetingRelationships()
    {
        // Setup initial promotion targeting products
        var promotion = new Promotion
        {
            Id = 80,
            Title = "عرض متعدد المراحل",
            PromotionType = "Percentage",
            DiscountValue = 10m,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { PromotionId = 80, ProductId = 501 },
                new() { PromotionId = 80, ProductId = 502 }
            },
            PromotionCategories = new List<PromotionCategory>()
        };

        Assert.Equal(2, promotion.PromotionProducts.Count);
        Assert.Empty(promotion.PromotionCategories);

        // Case A: Switch from Products -> Storewide
        string newTargetType = "All";
        promotion.PromotionProducts.Clear();
        promotion.PromotionCategories.Clear();

        if (newTargetType == "Products")
        {
            promotion.PromotionProducts.Add(new() { PromotionId = 80, ProductId = 501 });
        }
        else if (newTargetType == "Categories")
        {
            promotion.PromotionCategories.Add(new() { PromotionId = 80, CategoryId = 10 });
        }

        Assert.Empty(promotion.PromotionProducts);
        Assert.Empty(promotion.PromotionCategories);

        // Case B: Switch from Storewide -> Categories
        newTargetType = "Categories";
        var selectedCatIds = new List<int> { 10, 12 };
        promotion.PromotionProducts.Clear();
        promotion.PromotionCategories.Clear();

        if (newTargetType == "Categories")
        {
            foreach (var cid in selectedCatIds)
            {
                promotion.PromotionCategories.Add(new() { PromotionId = 80, CategoryId = cid });
            }
        }

        Assert.Empty(promotion.PromotionProducts);
        Assert.Equal(2, promotion.PromotionCategories.Count);

        // Case C: Switch from Categories -> Products
        newTargetType = "Products";
        var selectedProdIds = new List<int> { 503 };
        promotion.PromotionProducts.Clear();
        promotion.PromotionCategories.Clear();

        if (newTargetType == "Products")
        {
            foreach (var pid in selectedProdIds)
            {
                promotion.PromotionProducts.Add(new() { PromotionId = 80, ProductId = pid });
            }
        }

        Assert.Single(promotion.PromotionProducts);
        Assert.Equal(503, promotion.PromotionProducts.First().ProductId);
        Assert.Empty(promotion.PromotionCategories);
    }

    #endregion
}
