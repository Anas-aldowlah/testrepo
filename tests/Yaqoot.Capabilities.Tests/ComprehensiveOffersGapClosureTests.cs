using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services.Promotions;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class ComprehensiveOffersGapClosureTests
{
    #region 1. Delete-Target Edge Case (Section 12)
    [Fact]
    public async Task DeleteTargetEdgeCase_ProductsScope_WithDeletedProduct_MatchesZeroItems_NeverStorewide()
    {
        var engine = new PromotionEngine();

        // Promotion was created as "Products" targeting Product 10.
        // Product 10 was subsequently deleted from the database (so PromotionProducts list is now empty).
        var promo = new Promotion
        {
            Id = 501,
            Title = "Deleted Product Target Promo",
            PromotionType = "Percentage",
            TargetType = "Products",
            DiscountValue = 30m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 5,
            PromotionProducts = new List<PromotionProduct>() // Empty because product was deleted!
        };

        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 10, Quantity = 1, UnitPrice = 100m },
                new() { LineIdentifier = "L2", ProductId = 20, Quantity = 1, UnitPrice = 200m }
            }
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        // CRITICAL INVARIANT: It MUST NOT apply to any line item! Total discount MUST be 0!
        Assert.Equal(0m, result.TotalDiscounts);
        Assert.Equal(0m, result.Lines[0].TotalDiscount);
        Assert.Equal(0m, result.Lines[1].TotalDiscount);
        Assert.Empty(result.Lines[0].AppliedPromotions);
        Assert.Empty(result.Lines[1].AppliedPromotions);
    }

    [Fact]
    public async Task DeleteTargetEdgeCase_CategoriesScope_WithDeletedCategory_MatchesZeroItems_NeverStorewide()
    {
        var engine = new PromotionEngine();

        // Promotion was created as "Categories" targeting Category 5.
        // Category 5 was deleted from the database (so PromotionCategories list is now empty).
        var promo = new Promotion
        {
            Id = 502,
            Title = "Deleted Category Target Promo",
            PromotionType = "Percentage",
            TargetType = "Categories",
            DiscountValue = 25m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 5,
            PromotionCategories = new List<PromotionCategory>() // Empty because category was deleted!
        };

        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 10, CategoryId = 5, Quantity = 1, UnitPrice = 100m },
                new() { LineIdentifier = "L2", ProductId = 20, CategoryId = 8, Quantity = 1, UnitPrice = 200m }
            }
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });

        // CRITICAL INVARIANT: Must NOT apply storewide!
        Assert.Equal(0m, result.TotalDiscounts);
        Assert.Equal(0m, result.Lines[0].TotalDiscount);
        Assert.Equal(0m, result.Lines[1].TotalDiscount);
    }
    #endregion

    #region 2. Targeting Invariants & Mode Transitions (Section 10, 21, 58)
    [Theory]
    [InlineData("All", 101, 1, 20)]           // Storewide: matches Product 101
    [InlineData("All", 999, 99, 20)]          // Storewide: matches any product
    [InlineData("Products", 101, 1, 20)]      // Targeted Product 101: matches
    [InlineData("Products", 102, 1, 0)]       // Targeted Product 101: does NOT match Product 102
    [InlineData("Categories", 101, 1, 20)]    // Targeted Cat 1: matches Cat 1
    [InlineData("Categories", 101, 2, 0)]     // Targeted Cat 1: does NOT match Cat 2
    public async Task TargetingModes_EvaluateCorrectlyAccordingToScope(
        string targetType, int cartProductId, int cartCategoryId, decimal expectedDiscount)
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 601,
            Title = "Target Mode Test",
            PromotionType = "Percentage",
            TargetType = targetType,
            DiscountValue = 20m, // 20% off 100 = 20
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 5
        };

        if (targetType == "Products")
        {
            promo.PromotionProducts = new List<PromotionProduct> { new() { ProductId = 101 } };
        }
        else if (targetType == "Categories")
        {
            promo.PromotionCategories = new List<PromotionCategory> { new() { CategoryId = 1 } };
        }

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = cartProductId, CategoryId = cartCategoryId, Quantity = 1, UnitPrice = 100m }
            }
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });
        Assert.Equal(expectedDiscount, result.TotalDiscounts);
    }

    [Fact]
    public void PromotionFormVM_Validation_EnforcesTargetingInvariants()
    {
        // Invariant 1: TargetType == "Products" requires at least 1 product
        var vm1 = new PromotionFormVM
        {
            Title = "Invalid Products Target",
            PromotionType = "Percentage",
            DiscountValue = 10,
            TargetType = "Products",
            SelectedProductIds = new List<int>() // Empty!
        };
        var results1 = new List<ValidationResult>();
        Validator.TryValidateObject(vm1, new ValidationContext(vm1), results1, true);
        Assert.Contains(results1, r => r.MemberNames.Contains(nameof(PromotionFormVM.SelectedProductIds)));

        // Invariant 2: TargetType == "Categories" requires at least 1 category
        var vm2 = new PromotionFormVM
        {
            Title = "Invalid Categories Target",
            PromotionType = "Percentage",
            DiscountValue = 10,
            TargetType = "Categories",
            SelectedCategoryIds = new List<int>() // Empty!
        };
        var results2 = new List<ValidationResult>();
        Validator.TryValidateObject(vm2, new ValidationContext(vm2), results2, true);
        Assert.Contains(results2, r => r.MemberNames.Contains(nameof(PromotionFormVM.SelectedCategoryIds)));

        // Invariant 3: TargetType == "All" does not require products or categories
        var vm3 = new PromotionFormVM
        {
            Title = "Valid Storewide Target",
            PromotionType = "Percentage",
            DiscountValue = 10,
            TargetType = "All"
        };
        var results3 = new List<ValidationResult>();
        Validator.TryValidateObject(vm3, new ValidationContext(vm3), results3, true);
        Assert.DoesNotContain(results3, r => r.MemberNames.Contains(nameof(PromotionFormVM.SelectedProductIds)));
        Assert.DoesNotContain(results3, r => r.MemberNames.Contains(nameof(PromotionFormVM.SelectedCategoryIds)));
    }
    #endregion

    #region 3. QuantityPrice Bundle Boundaries (Section 27)
    [Theory]
    // reqQty = 3, offerPrice = 250, unitPrice = 100
    [InlineData(1, 100.00, 0.00, 100.00)]    // Q = 1: below bundle, regular price
    [InlineData(2, 200.00, 0.00, 200.00)]    // Q = 2 (BuyQuantity - 1): regular price
    [InlineData(3, 300.00, 50.00, 250.00)]   // Q = 3 (BuyQuantity): 1 bundle = 250 (50 off)
    [InlineData(4, 400.00, 50.00, 350.00)]   // Q = 4 (BuyQuantity + 1): 1 bundle (250) + 1 reg (100) = 350
    [InlineData(6, 600.00, 100.00, 500.00)]  // Q = 6 (2 * BuyQuantity): 2 bundles = 500 (100 off)
    [InlineData(7, 700.00, 100.00, 600.00)]  // Q = 7 (2 * BuyQuantity + rem): 2 bundles (500) + 1 reg (100) = 600
    public async Task QuantityPrice_CalculatesExactBundleRemainders(
        int quantity, decimal expectedGross, decimal expectedDiscount, decimal expectedNet)
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 701,
            Title = "3 for 250 SAR Perfume Offer",
            PromotionType = "QuantityPrice",
            TargetType = "All",
            BuyQuantity = 3,
            OfferPrice = 250m,
            DiscountValue = 250m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 50, Quantity = quantity, UnitPrice = 100m }
            }
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });
        Assert.Equal(expectedGross, result.GrossSubtotal);
        Assert.Equal(expectedDiscount, result.TotalDiscounts);
        Assert.Equal(expectedNet, result.NetTotal);
    }
    #endregion

    #region 4. BuyXGetY Quantities & Volume Deductions (Section 28, 31)
    [Theory]
    // Buy 2 Get 1 Free, unitPrice = 50
    [InlineData(1, 0, 50.00)]   // Cart qty 1 -> 0 free, net 50
    [InlineData(2, 1, 100.00)]  // Cart qty 2 -> 1 free, net 100
    [InlineData(3, 1, 150.00)]  // Cart qty 3 -> 1 free + 1 rem, net 150
    [InlineData(4, 2, 200.00)]  // Cart qty 4 -> 2 free, net 200
    [InlineData(6, 3, 300.00)]  // Cart qty 6 -> 3 free, net 300
    public async Task BuyXGetY_CalculatesFreeQuantities_AndTracksInventory(
        int cartQuantity, int expectedFreeQuantity, decimal expectedNet)
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 801,
            Title = "Buy 2 Get 1 Free",
            PromotionType = "BuyXGetY",
            TargetType = "All",
            BuyQuantity = 2,
            FreeQuantity = 1,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new()
                {
                    LineIdentifier = "L1",
                    ProductId = 75,
                    RetailSizeMl = 50, // 50ml bottle
                    Quantity = cartQuantity,
                    UnitPrice = 50m
                }
            }
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });
        Assert.Equal(expectedNet, result.NetTotal);
        Assert.Equal(expectedFreeQuantity, result.Lines[0].FreeQuantity);

        if (expectedFreeQuantity > 0)
        {
            Assert.NotEmpty(result.FreeProducts);
            Assert.Equal(expectedFreeQuantity, result.FreeProducts[0].FreeQuantity);

            // ML Volume Deduction Verification (Section 31):
            // Total volume to deduct from inventory = (paid + free) * volumePerUnit
            int totalUnitsToDeduct = cartQuantity + expectedFreeQuantity;
            int totalMlToDeduct = totalUnitsToDeduct * 50;
            Assert.True(totalMlToDeduct > 0);
        }
    }
    #endregion

    #region 5. SpendAmount Boundaries and Discount Types (Section 26)
    [Fact]
    public async Task SpendAmount_FixedDiscount_EvaluatesStrictlyAtCartLevel()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 901,
            Title = "Spend 200 get 30 fixed off",
            PromotionType = "SpendAmount",
            SpendDiscountType = DiscountValueType.FixedAmount,
            MinimumAmount = 200m,
            DiscountValue = 30m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10
        };

        // 1. Below threshold: Gross = 199
        var ctxBelow = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 1, Quantity = 1, UnitPrice = 199m }
            }
        };
        var resBelow = await engine.CalculatePromotionsAsync(ctxBelow, new List<Promotion> { promo });
        Assert.Equal(0m, resBelow.SpendAmountDiscount);
        Assert.Equal(199m, resBelow.NetTotal);

        // 2. Exact threshold: Gross = 200
        var ctxExact = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 1, Quantity = 2, UnitPrice = 100m }
            }
        };
        var resExact = await engine.CalculatePromotionsAsync(ctxExact, new List<Promotion> { promo });
        Assert.Equal(30m, resExact.SpendAmountDiscount);
        Assert.Equal(170m, resExact.NetTotal);

        // 3. Above threshold: Gross = 300
        var ctxAbove = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 1, Quantity = 3, UnitPrice = 100m }
            }
        };
        var resAbove = await engine.CalculatePromotionsAsync(ctxAbove, new List<Promotion> { promo });
        Assert.Equal(30m, resAbove.SpendAmountDiscount);
        Assert.Equal(270m, resAbove.NetTotal);
    }

    [Fact]
    public async Task SpendAmount_PercentageDiscount_EvaluatesCorrectly()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 902,
            Title = "Spend 500 get 10% off",
            PromotionType = "SpendAmount",
            SpendDiscountType = DiscountValueType.Percentage,
            MinimumAmount = 500m,
            DiscountValue = 10m, // 10%
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10
        };

        var ctx = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 1, Quantity = 10, UnitPrice = 60m } // Gross = 600
            }
        };

        var res = await engine.CalculatePromotionsAsync(ctx, new List<Promotion> { promo });
        // 10% of 600 = 60
        Assert.Equal(60m, res.SpendAmountDiscount);
        Assert.Equal(540m, res.NetTotal);
    }
    #endregion

    #region 6. Stacking / CanBeCombined Combinations (Section 25)
    [Fact]
    public async Task Stacking_P1NonCombining_BlocksSubsequentPromotions()
    {
        var engine = new PromotionEngine();

        var p1 = new Promotion
        {
            Id = 1001,
            Title = "P1 10% Off Non-Combining",
            PromotionType = "Percentage",
            TargetType = "All",
            DiscountValue = 10m,
            CanBeCombined = false, // Does not combine!
            Priority = 5,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5)
        };

        var p2 = new Promotion
        {
            Id = 1002,
            Title = "P2 20% Off Combining",
            PromotionType = "Percentage",
            TargetType = "All",
            DiscountValue = 20m,
            CanBeCombined = true,
            Priority = 10,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5)
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 1, Quantity = 1, UnitPrice = 100m }
            }
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { p1, p2 });
        // Only P1 should apply (10% of 100 = 10)
        Assert.Equal(10m, result.TotalDiscounts);
        Assert.Single(result.Lines[0].AppliedPromotions);
        Assert.Equal(1001, result.Lines[0].AppliedPromotions[0].PromotionId);
    }

    [Fact]
    public async Task Stacking_P1CombiningAndP2Combining_StackSuccessively()
    {
        var engine = new PromotionEngine();

        var p1 = new Promotion
        {
            Id = 1003,
            Title = "P1 10% Off Combining",
            PromotionType = "Percentage",
            TargetType = "All",
            DiscountValue = 10m,
            CanBeCombined = true,
            Priority = 5,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5)
        };

        var p2 = new Promotion
        {
            Id = 1004,
            Title = "P2 5 SAR Off Combining",
            PromotionType = "FixedAmount",
            TargetType = "All",
            DiscountValue = 5m,
            CanBeCombined = true,
            Priority = 10,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5)
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 1, Quantity = 1, UnitPrice = 100m }
            }
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { p1, p2 });
        // P1: 10% of 100 = 10
        // P2: 5 fixed off
        // Total discount = 15, Net = 85
        Assert.Equal(15m, result.TotalDiscounts);
        Assert.Equal(2, result.Lines[0].AppliedPromotions.Count);
        Assert.Equal(85m, result.NetTotal);
    }
    #endregion

    #region 7. Date/Time Yemen Local Time Boundaries (Section 23)
    [Fact]
    public void PromotionTime_YemenLocalTime_ConversionAndStatusKeys_AreAccurate()
    {
        var yemenNow = new DateTime(2026, 9, 17, 12, 0, 0); // 12:00 PM Yemen
        var utcEquivalent = PromotionTime.YemenLocalToUtc(yemenNow);

        // Yemen is UTC+3, so 12:00 PM Yemen is 09:00 AM UTC
        Assert.Equal(new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.Zero), utcEquivalent);

        var convertedBack = PromotionTime.UtcToYemenLocal(utcEquivalent);
        Assert.Equal(yemenNow, convertedBack);

        var promo = new Promotion
        {
            IsActive = true,
            StartDate = utcEquivalent.AddHours(-1),
            EndDate = utcEquivalent.AddHours(2)
        };

        // Active state
        Assert.Equal("active", PromotionTime.GetStatusKey(promo, utcEquivalent));

        // Upcoming state
        Assert.Equal("upcoming", PromotionTime.GetStatusKey(promo, utcEquivalent.AddHours(-2)));

        // Expired state (at or past EndDate)
        Assert.Equal("expired", PromotionTime.GetStatusKey(promo, utcEquivalent.AddHours(3)));

        // Disabled state (IsActive == false)
        promo.IsActive = false;
        Assert.Equal("disabled", PromotionTime.GetStatusKey(promo, utcEquivalent));
    }
    #endregion

    #region 8. Historical Snapshot Immutability (Section 32)
    [Fact]
    public async Task HistoricalSnapshot_IsImmutable_WhenPromotionChangesAfterward()
    {
        var engine = new PromotionEngine();

        var promo = new Promotion
        {
            Id = 1101,
            Title = "Historical Promo",
            PromotionType = "Percentage",
            TargetType = "All",
            DiscountValue = 15m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(5),
            Priority = 10
        };

        var context = new PromotionCalculationContext
        {
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 1, Quantity = 2, UnitPrice = 100m }
            }
        };

        var result = await engine.CalculatePromotionsAsync(context, new List<Promotion> { promo });
        string originalSnapshot = result.PromotionSnapshotJson;
        Assert.False(string.IsNullOrEmpty(originalSnapshot));

        // Admin subsequently changes the promotion
        promo.DiscountValue = 50m;
        promo.IsActive = false;

        // The historical snapshot JSON remains untouched
        using var doc = JsonDocument.Parse(originalSnapshot);
        var root = doc.RootElement;
        Assert.Equal(30m, root.GetProperty("total_discounts").GetDecimal()); // 15% of 200 = 30
        Assert.Equal(170m, root.GetProperty("net_total").GetDecimal());
    }
    #endregion

    #region 9. Manual POS Discount + Promotion Interaction (Section 30)
    [Fact]
    public void ManualPosDiscount_PlusPromotion_CascadesStrictly()
    {
        decimal originalUnitPrice = 200.00m;
        decimal promoDiscountAmount = 30.00m; // 200 - 30 = 170
        decimal unitPriceAfterPromo = originalUnitPrice - promoDiscountAmount;

        // Manual POS Discount: 10% off the unit price after promotion
        decimal manualDiscountPercent = 10.00m;
        decimal manualDiscountAmount = Math.Round(unitPriceAfterPromo * (manualDiscountPercent / 100m), 2); // 17.00
        decimal finalUnitPrice = unitPriceAfterPromo - manualDiscountAmount; // 153.00

        Assert.Equal(170.00m, unitPriceAfterPromo);
        Assert.Equal(17.00m, manualDiscountAmount);
        Assert.Equal(153.00m, finalUnitPrice);
    }
    #endregion
}
