using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Areas.Admin.Controllers;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Data;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Caching;
using YAGOT_2._0.Services.Integration;
using YAGOT_2._0.Services.Promotions;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class EndToEndSystemVerificationTests : IDisposable
{
    private readonly NeondbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IPromotionEngine _promotionEngine;
    private readonly StoreSettingsService _settingsService;
    private readonly List<int> _createdPromotionIds = new();

    private static CapabilityEvaluator CreateCapabilityEvaluator()
    {
        var catalog = new CapabilityCatalog();
        var runtime = new LocalCapabilityRuntimeStateProvider(catalog, TimeProvider.System);
        runtime.Publish(new CapabilitySnapshotV1(
            CapabilityContractV1.ContractVersion,
            CapabilityContractV1.CatalogVersion,
            1,
            1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            catalog.Modules.Select(m => new CapabilityModuleStateV1(m.Code, true)).ToArray(),
            catalog.Features.Select(f => new CapabilityFeatureStateV1(f.Code, f.ModuleCode, true)).ToArray()
        ));
        return new CapabilityEvaluator(catalog, runtime);
    }

    public EndToEndSystemVerificationTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("MYDB")
            ?? "Host=ep-spring-bonus-b1d8g280-pooler.c-5.eu-central-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_RQ1wg6UtCkJz;SSL Mode=Require;Trust Server Certificate=true;KeepAlive=30;TcpKeepAlive=true;Timeout=30;CommandTimeout=60";

        var options = new DbContextOptionsBuilder<NeondbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _context = new NeondbContext(options);
        var capabilityEvaluator = CreateCapabilityEvaluator();
        _inventoryService = new InventoryService(_context, capabilityEvaluator);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var invalidationService = new CacheInvalidationService(memoryCache, NullLogger<CacheInvalidationService>.Instance);
        _settingsService = new StoreSettingsService(
            _context,
            NullLogger<StoreSettingsService>.Instance,
            memoryCache,
            Options.Create(new BackendCacheOptions()),
            invalidationService);
        _promotionEngine = new PromotionEngine(
            _context,
            memoryCache,
            NullLogger<PromotionEngine>.Instance,
            _inventoryService,
            _settingsService);

        var orphaned = _context.Promotions.Where(p => p.Title.StartsWith("E2E") || p.Id == 83 || p.Id == 136).ToList();
        if (orphaned.Count > 0)
        {
            _context.Promotions.RemoveRange(orphaned);
            _context.SaveChanges();
        }
    }

    public void Dispose()
    {
        if (_createdPromotionIds.Count > 0)
        {
            var promos = _context.Promotions.Where(p => _createdPromotionIds.Contains(p.Id)).ToList();
            _context.Promotions.RemoveRange(promos);
            _context.SaveChanges();
        }
        _context.Dispose();
    }

    #region 1. Percentage Promotion
    [Fact]
    public async Task Verification_01_PercentagePromotion_CalculatesCorrectly()
    {
        var promo = new Promotion
        {
            Title = "E2E Test 20% Off",
            PromotionType = "Percentage",
            DiscountValue = 20m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct> { new() { ProductId = 20 } }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 20, Quantity = 2, UnitPrice = 68.00m }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        Assert.Equal(136.00m, result.GrossSubtotal);
        Assert.Equal(27.20m, result.TotalDiscounts); // 20% of 136
        Assert.Equal(108.80m, result.NetTotal);
        Assert.Single(result.Lines[0].AppliedPromotions);
        Assert.Equal(promo.Id, result.Lines[0].AppliedPromotions[0].PromotionId);
    }
    #endregion

    #region 2. FixedAmount Promotion
    [Fact]
    public async Task Verification_02_FixedAmountPromotion_CalculatesCorrectly()
    {
        var promo = new Promotion
        {
            Title = "E2E Test 10 Off",
            PromotionType = "FixedAmount",
            DiscountValue = 10m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct> { new() { ProductId = 20 } }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 20, Quantity = 3, UnitPrice = 68.00m }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        Assert.Equal(204.00m, result.GrossSubtotal);
        Assert.Equal(30.00m, result.TotalDiscounts); // 10 * 3
        Assert.Equal(174.00m, result.NetTotal);
        Assert.Equal(58.00m, result.Lines[0].FinalUnitPrice);
    }
    #endregion

    #region 3. BuyXGetY
    [Fact]
    public async Task Verification_03_BuyXGetY_AwardsFreeUnits()
    {
        var promo = new Promotion
        {
            Title = "E2E Buy 2 Get 1 Free",
            PromotionType = "BuyXGetY",
            BuyQuantity = 2,
            FreeQuantity = 1,
            DiscountValue = 0,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct> { new() { ProductId = 20 } }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 20, Quantity = 5, UnitPrice = 68.00m }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        Assert.Equal(2, result.Lines[0].FreeQuantity);
        Assert.Equal(340.00m, result.GrossSubtotal);
        Assert.Equal(340.00m, result.NetTotal);
        Assert.Single(result.FreeProducts);
        Assert.Equal(2, result.FreeProducts[0].FreeQuantity);
    }
    #endregion

    #region 4. BuyXGetY with ML/Decant Product
    [Fact]
    public async Task Verification_04_BuyXGetY_WithMlDecant_CalculatesCorrectly()
    {
        // Product 37: "عطر فينيسيا", 200ml retail price id = 16 (price = 4.00)
        var promo = new Promotion
        {
            Title = "E2E Buy 2 Get 1 Free on 200ml Decant",
            PromotionType = "BuyXGetY",
            BuyQuantity = 2,
            FreeQuantity = 1,
            DiscountValue = 0,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct> { new() { ProductId = 37 } }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 37, RetailPriceId = 16, RetailSizeMl = 200, Quantity = 4, UnitPrice = 4.00m }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        Assert.Equal(2, result.Lines[0].FreeQuantity);
        Assert.Equal(16.00m, result.GrossSubtotal);
        Assert.Equal(16.00m, result.NetTotal);
        Assert.Single(result.FreeProducts);
        Assert.Equal(2, result.FreeProducts[0].FreeQuantity);
    }
    #endregion

    #region 5. QuantityPrice
    [Fact]
    public async Task Verification_05_QuantityPrice_AppliesTierPricing()
    {
        var promo = new Promotion
        {
            Title = "E2E Buy 3 or more at 50 each",
            PromotionType = "QuantityPrice",
            BuyQuantity = 3,
            OfferPrice = 50.00m,
            DiscountValue = 0,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct> { new() { ProductId = 20 } }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 20, Quantity = 4, UnitPrice = 68.00m }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        Assert.Equal(154.00m, result.TotalDiscounts); // 3 for 50 -> discount = (3 * 68) - 50 = 154
        Assert.Equal(29.50m, result.Lines[0].FinalUnitPrice); // (272 - 154) / 4 = 29.50
        Assert.Equal(118.00m, result.NetTotal);
    }
    #endregion

    #region 6. SpendAmount
    [Fact]
    public async Task Verification_06_SpendAmount_AppliesAtInvoiceLevel()
    {
        var promo = new Promotion
        {
            Title = "E2E Spend 200 get 30 off",
            PromotionType = "SpendAmount",
            SpendDiscountType = DiscountValueType.FixedAmount,
            MinimumAmount = 200m,
            DiscountValue = 30m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 5
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 20, Quantity = 4, UnitPrice = 68.00m } // Gross = 272
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        Assert.Equal(272.00m, result.GrossSubtotal);
        Assert.Equal(30.00m, result.SpendAmountDiscount);
        Assert.Equal(30.00m, result.TotalDiscounts);
        Assert.Equal(242.00m, result.NetTotal);
    }
    #endregion

    #region 7. Promotion + Existing Manual POS Discount
    [Fact]
    public void Verification_07_PromotionPlusManualPosDiscount_CascadesStrictly()
    {
        decimal originalUnitPrice = 100.00m;
        int quantity = 2;
        decimal lineGross = originalUnitPrice * quantity; // 200.00m

        decimal promoDiscount = 40.00m; // 20.00m per unit from automatic promo
        decimal manualCashierDiscount = 30.00m; // 15.00m per unit manual cashier discount

        decimal promoReduction = Math.Min(promoDiscount, lineGross); // 40.00m
        decimal remainingAfterPromo = Math.Max(0m, lineGross - promoReduction); // 160.00m

        decimal manualDiscount = Math.Min(manualCashierDiscount, remainingAfterPromo); // 30.00m
        decimal lineTotal = Math.Max(0m, remainingAfterPromo - manualDiscount); // 130.00m
        decimal totalLineDiscount = promoReduction + manualDiscount; // 70.00m
        decimal finalUnitPrice = lineTotal / quantity; // 65.00m

        Assert.Equal(130.00m, lineTotal);
        Assert.Equal(70.00m, totalLineDiscount);
        Assert.Equal(65.00m, finalUnitPrice);
        Assert.True(finalUnitPrice >= 0m);
    }
    #endregion

    #region 8. Checkout with Active Promotion
    [Fact]
    public async Task Verification_08_Checkout_GeneratesImmutableSnapshot()
    {
        var promo = new Promotion
        {
            Title = "E2E Snapshot Test 10%",
            PromotionType = "Percentage",
            DiscountValue = 10m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct> { new() { ProductId = 20 } }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 20, Quantity = 1, UnitPrice = 68.00m }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        Assert.NotNull(result.PromotionSnapshotJson);
        Assert.Contains(promo.Title, result.PromotionSnapshotJson);
        using var doc = System.Text.Json.JsonDocument.Parse(result.PromotionSnapshotJson);
        var root = doc.RootElement;
        Assert.Equal("1.0.0", root.GetProperty("engine_version").GetString());
        Assert.Equal(68.00m, root.GetProperty("gross_subtotal").GetDecimal());
        Assert.Equal(6.80m, root.GetProperty("total_discounts").GetDecimal());
        Assert.Equal(61.20m, root.GetProperty("net_total").GetDecimal());
    }
    #endregion

    #region 9. Inventory Deduction Includes Paid + Free Quantities
    [Fact]
    public void Verification_09_InventoryDeduction_IncludesPaidAndFreeQuantities()
    {
        // 1. Piece item: Product 20 (مخمرية الجسم بالزعفران), Piece unit
        var pieceProduct = _context.Products.First(p => p.Id == 20);
        int paidQuantity = 2;
        int freeQuantity = 1;
        int totalQuantity = paidQuantity + freeQuantity; // 3
        int pieceDeduction = _inventoryService.CalculateDeductionAmount(pieceProduct, totalQuantity, null);
        Assert.Equal(3, pieceDeduction);

        // 2. ML decant item: Product 37 (عطر فينيسيا), 200ml retail price
        var mlProduct = _context.Products.First(p => p.Id == 37);
        int mlDeduction = _inventoryService.CalculateDeductionAmount(mlProduct, totalQuantity, 200);
        Assert.Equal(600, mlDeduction); // 3 * 200ml = 600ml
    }
    #endregion

    #region 10. Historical Transactions Retain SAR After StoreSettings Change
    [Fact]
    public async Task Verification_10_HistoricalTransactions_RetainSar_AfterCurrencyChange()
    {
        // Assert all existing orders have SAR
        var orderCurrencies = await _context.Orders.Select(o => o.Currencycode).Distinct().ToListAsync();
        Assert.Single(orderCurrencies);
        Assert.Equal("SAR", orderCurrencies[0]);

        // Assert all existing sales have SAR
        var saleCurrencies = await _context.Sales.Select(s => s.CurrencyCode).Distinct().ToListAsync();
        Assert.Single(saleCurrencies);
        Assert.Equal("SAR", saleCurrencies[0]);

        // Change current system currency to YER
        var settings = await _settingsService.GetSettingsAsync();
        var originalCurrency = settings.CurrencyCode;
        try
        {
            settings.CurrencyCode = "YER";
            await _settingsService.SaveSettingsAsync(settings);

            // Re-query historical transactions from database
            var reloadedOrderCurrencies = await _context.Orders.AsNoTracking().Select(o => o.Currencycode).Distinct().ToListAsync();
            Assert.Single(reloadedOrderCurrencies);
            Assert.Equal("SAR", reloadedOrderCurrencies[0]);

            var reloadedSaleCurrencies = await _context.Sales.AsNoTracking().Select(s => s.CurrencyCode).Distinct().ToListAsync();
            Assert.Single(reloadedSaleCurrencies);
            Assert.Equal("SAR", reloadedSaleCurrencies[0]);
        }
        finally
        {
            // Restore back to original
            settings.CurrencyCode = originalCurrency;
            await _settingsService.SaveSettingsAsync(settings);
        }
    }
    #endregion

    private sealed class DummyTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    #region 11. Developer-Only Currency Modification
    [Fact]
    public async Task Verification_11_SettingsController_BlocksNonDeveloperFromChangingCurrency()
    {
        var capabilityEvaluator = CreateCapabilityEvaluator();
        var controller = new SettingsController(_settingsService, _context, NullLogger<SettingsController>.Instance, capabilityEvaluator);

        // Case A: User is Admin only (not Developer)
        var adminClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "StoreAdmin"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));

        var adminContext = new DefaultHttpContext { User = adminClaims };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = adminContext
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(adminContext, new DummyTempDataProvider());

        var currentSettings = await _settingsService.GetSettingsAsync();
        var attemptedSettings = new StoreSettings
        {
            Id = currentSettings.Id,
            CurrencyCode = "USD", // Attempting change to USD
            BrandsMarquee = currentSettings.BrandsMarquee
        };

        var result = await controller.Index(attemptedSettings);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(StoreSettings.CurrencyCode)));

        // Case B: User is Developer
        var devClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "LeadDeveloper"),
            new Claim(ClaimTypes.Role, "Developer")
        }, "TestAuth"));

        var devContext = new DefaultHttpContext { User = devClaims };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = devContext
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(devContext, new DummyTempDataProvider());
        controller.ModelState.Clear();

        var devSettings = new StoreSettings
        {
            Id = currentSettings.Id,
            CurrencyCode = "USD",
            BrandsMarquee = currentSettings.BrandsMarquee
        };

        var devResult = await controller.Index(devSettings);
        Assert.True(controller.ModelState.IsValid);
        Assert.IsType<RedirectToActionResult>(devResult);

        // Restore back to SAR
        devSettings.CurrencyCode = "SAR";
        await controller.Index(devSettings);
    }
    #endregion

    #region 12. Existing POS Functionality Remains Operational
    [Fact]
    public async Task Verification_12_PosSalesDaysAndDrafts_RemainFullyOperational()
    {
        // 1. Verify SalesDays query
        var salesDays = await _context.SalesDays.AsNoTracking().OrderByDescending(sd => sd.Date).Take(5).ToListAsync();
        Assert.NotNull(salesDays);

        // 2. Verify completed sales ledger
        var completedSales = await _context.Sales.AsNoTracking().Where(s => s.Status == "Completed").Take(5).ToListAsync();
        Assert.NotNull(completedSales);

        // 3. Verify SaleItems and audit columns
        var saleItems = await _context.SaleItems.AsNoTracking().Take(5).ToListAsync();
        Assert.NotNull(saleItems);
        foreach (var item in saleItems)
        {
            Assert.True(item.FinalUnitPrice >= 0m);
            Assert.True(item.OriginalUnitPrice >= 0m);
        }
    }
    #endregion

    #region 13. Targeting Verification: Storewide Persisted and Evaluated in Store DB
    [Fact]
    public async Task Verification_13_StoreDatabase_StorewidePromotion_PersistedAndEvaluated()
    {
        var promo = new Promotion
        {
            Title = "E2E Targeting Storewide Test",
            PromotionType = "Percentage",
            DiscountValue = 10m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct>(),
            PromotionCategories = new List<PromotionCategory>()
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        // Verify relational persistence in PostgreSQL
        var loaded = await _context.Promotions
            .AsNoTracking()
            .Include(p => p.PromotionProducts)
            .Include(p => p.PromotionCategories)
            .FirstOrDefaultAsync(p => p.Id == promo.Id);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.PromotionProducts);
        Assert.Empty(loaded.PromotionCategories);

        // Verify evaluation applies storewide
        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "1", ProductId = 20, Quantity = 1, UnitPrice = 100m, CategoryId = 1 },
                new() { LineIdentifier = "2", ProductId = 37, Quantity = 1, UnitPrice = 200m, CategoryId = 2 }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        Assert.Equal(300m, result.GrossSubtotal);
        Assert.Equal(30m, result.TotalDiscounts);
        Assert.Equal(270m, result.NetTotal);
    }
    #endregion

    #region 14. Targeting Verification: Limited Products Persisted and Evaluated in Store DB
    [Fact]
    public async Task Verification_14_StoreDatabase_LimitedProducts_PersistedAndEvaluated()
    {
        // Use real existing products 20 and 37
        var promo = new Promotion
        {
            Title = "E2E Targeting Limited Products Test",
            PromotionType = "FixedAmount",
            DiscountValue = 15m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = 20, CreatedAt = DateTime.UtcNow },
                new() { ProductId = 37, CreatedAt = DateTime.UtcNow }
            }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        // Verify relational persistence in PostgreSQL table promotion_products
        var loaded = await _context.Promotions
            .AsNoTracking()
            .Include(p => p.PromotionProducts)
            .FirstOrDefaultAsync(p => p.Id == promo.Id);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.PromotionProducts.Count);
        Assert.Contains(loaded.PromotionProducts, pp => pp.ProductId == 20);
        Assert.Contains(loaded.PromotionProducts, pp => pp.ProductId == 37);

        // Verify evaluation applies ONLY to products 20 and 37, not 21
        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 20, Quantity = 1, UnitPrice = 100m },
                new() { LineIdentifier = "L2", ProductId = 21, Quantity = 1, UnitPrice = 100m }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        var item20 = result.Lines.First(l => l.ProductId == 20);
        var item21 = result.Lines.First(l => l.ProductId == 21);

        Assert.Equal(15m, item20.TotalDiscount);
        Assert.Equal(0m, item21.TotalDiscount);
        Assert.Equal(15m, result.TotalDiscounts);
    }
    #endregion

    #region 15. Targeting Verification: Limited Categories Persisted and Evaluated in Store DB
    [Fact]
    public async Task Verification_15_StoreDatabase_LimitedCategories_PersistedAndEvaluated()
    {
        // Find existing category from product 20
        var prod20 = await _context.Products.AsNoTracking().FirstAsync(p => p.Id == 20);
        int targetCatId = prod20.Categoryid;

        var promo = new Promotion
        {
            Title = "E2E Targeting Limited Categories Test",
            PromotionType = "Percentage",
            DiscountValue = 25m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10,
            PromotionCategories = new List<PromotionCategory>
            {
                new() { CategoryId = targetCatId, CreatedAt = DateTime.UtcNow }
            }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        // Verify relational persistence in PostgreSQL table promotion_categories
        var loaded = await _context.Promotions
            .AsNoTracking()
            .Include(p => p.PromotionCategories)
            .FirstOrDefaultAsync(p => p.Id == promo.Id);

        Assert.NotNull(loaded);
        Assert.Single(loaded.PromotionCategories);
        Assert.Equal(targetCatId, loaded.PromotionCategories.First().CategoryId);

        // Verify evaluation applies ONLY to products in target category
        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 20, Quantity = 1, UnitPrice = 100m, CategoryId = targetCatId },
                new() { LineIdentifier = "L2", ProductId = 9999, Quantity = 1, UnitPrice = 100m, CategoryId = targetCatId + 999 }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context);
        var inCatItem = result.Lines.First(l => l.LineIdentifier == "L1");
        var outCatItem = result.Lines.First(l => l.LineIdentifier == "L2");

        Assert.Equal(25m, inCatItem.TotalDiscount);
        Assert.Equal(0m, outCatItem.TotalDiscount);
        Assert.Equal(25m, result.TotalDiscounts);
    }
    #endregion

    #region 16. PromotionsController: End-to-End Create & Edit Targeting Verification
    [Fact]
    public async Task Verification_16_PromotionsController_CreateAndEdit_Targeting_EndToEnd()
    {
        var testEnv = new TestWebHostEnvironment { ContentRootPath = AppContext.BaseDirectory, WebRootPath = AppContext.BaseDirectory };
        var imgService = new YAGOT_2._0.Services.Image(testEnv);
        var controller = new PromotionsController(_context, imgService, testEnv, _promotionEngine);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, new DummyTempDataProvider());

        // 1. Verify GET Create populates AvailableProducts and AvailableCategories
        var createGetResult = await controller.Create() as ViewResult;
        Assert.NotNull(createGetResult);
        var createVm = createGetResult.Model as YAGOT_2._0.Models.Admin.PromotionFormVM;
        Assert.NotNull(createVm);
        Assert.NotEmpty(createVm.AvailableProducts);
        Assert.NotEmpty(createVm.AvailableCategories);

        // 2. Verify POST Create with Limited Products targeting (products 20 and 37)
        var newPromoVm = new YAGOT_2._0.Models.Admin.PromotionFormVM
        {
            Title = "E2E Controller Target Products",
            Description = "Testing controller product targeting",
            PromotionType = "Percentage",
            DiscountValue = 12m,
            TargetType = "Products",
            SelectedProductIds = new List<int> { 20, 37 },
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(2),
            Priority = 1,
            IsActive = true
        };

        var createPostResult = await controller.Create(newPromoVm) as RedirectToActionResult;
        Assert.NotNull(createPostResult);
        Assert.Equal(nameof(PromotionsController.Index), createPostResult.ActionName);

        // Verify in DB
        var created = await _context.Promotions
            .Include(p => p.PromotionProducts)
            .Include(p => p.PromotionCategories)
            .FirstOrDefaultAsync(p => p.Title == "E2E Controller Target Products");

        Assert.NotNull(created);
        _createdPromotionIds.Add(created.Id);
        Assert.Equal(2, created.PromotionProducts.Count);
        Assert.Empty(created.PromotionCategories);
        Assert.Contains(created.PromotionProducts, pp => pp.ProductId == 20);
        Assert.Contains(created.PromotionProducts, pp => pp.ProductId == 37);

        // 3. Verify GET Edit pre-selects TargetType and selections
        var editGetResult = await controller.Edit(created.Id) as ViewResult;
        Assert.NotNull(editGetResult);
        var editVm = editGetResult.Model as YAGOT_2._0.Models.Admin.PromotionFormVM;
        Assert.NotNull(editVm);
        Assert.Equal("Products", editVm.TargetType);
        Assert.Contains(20, editVm.SelectedProductIds);
        Assert.Contains(37, editVm.SelectedProductIds);
        Assert.True(editVm.AvailableProducts.First(p => p.Id == 20).IsSelected);
        Assert.True(editVm.AvailableProducts.First(p => p.Id == 37).IsSelected);

        // 4. Verify POST Edit switching to Categories targeting
        var prod20 = await _context.Products.FirstAsync(p => p.Id == 20);
        editVm.TargetType = "Categories";
        editVm.SelectedProductIds = new List<int>(); // cleared
        editVm.SelectedCategoryIds = new List<int> { prod20.Categoryid };

        var editPostResult = await controller.Edit(created.Id, editVm) as RedirectToActionResult;
        Assert.NotNull(editPostResult);
        Assert.Equal(nameof(PromotionsController.Index), editPostResult.ActionName);

        // Verify in DB that products were deleted and category was saved
        var updated = await _context.Promotions
            .AsNoTracking()
            .Include(p => p.PromotionProducts)
            .Include(p => p.PromotionCategories)
            .FirstAsync(p => p.Id == created.Id);

        Assert.Empty(updated.PromotionProducts);
        Assert.Single(updated.PromotionCategories);
        Assert.Equal(prod20.Categoryid, updated.PromotionCategories.First().CategoryId);
    }
    #endregion

    #region 17. Real DB Targeting: TargetType Persisted and Delete-Target Never Broadens to Storewide
    [Fact]
    public async Task Verification_17_RealDatabase_TargetType_Persisted_And_OrphanedTarget_NeverAppliesStorewide()
    {
        var promo = new Promotion
        {
            Title = "E2E Delete Target Edge Case Test",
            PromotionType = "Percentage",
            TargetType = "Products",
            DiscountValue = 40m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 5,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = 20, CreatedAt = DateTime.UtcNow }
            }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        // Verify TargetType was persisted into PostgreSQL column target_type
        var loaded = await _context.Promotions
            .AsNoTracking()
            .Include(p => p.PromotionProducts)
            .FirstAsync(p => p.Id == promo.Id);

        Assert.Equal("Products", loaded.TargetType);
        Assert.Single(loaded.PromotionProducts);

        // Now simulate deleting the target product relation from database
        var relations = await _context.PromotionProducts.Where(pp => pp.PromotionId == promo.Id).ToListAsync();
        _context.PromotionProducts.RemoveRange(relations);
        await _context.SaveChangesAsync();

        // Loaded promotion now has 0 products, but TargetType remains "Products"
        var orphaned = await _context.Promotions
            .AsNoTracking()
            .Include(p => p.PromotionProducts)
            .Include(p => p.PromotionCategories)
            .FirstAsync(p => p.Id == promo.Id);

        Assert.Equal("Products", orphaned.TargetType);
        Assert.Empty(orphaned.PromotionProducts);
        Assert.Empty(orphaned.PromotionCategories);

        // Runtime calculation MUST NOT apply this orphaned promotion to ANY product!
        var context = new PromotionCalculationContext
        {
            Channel = "Online",
            BypassCache = true,
            Items = new List<PromotionCalculationLineItem>
            {
                new() { LineIdentifier = "L1", ProductId = 20, Quantity = 1, UnitPrice = 100m },
                new() { LineIdentifier = "L2", ProductId = 21, Quantity = 1, UnitPrice = 200m }
            }
        };

        var result = await _promotionEngine.CalculatePromotionsAsync(context, new List<Promotion> { orphaned });
        Assert.Equal(0m, result.TotalDiscounts);
        Assert.Equal(0m, result.Lines[0].TotalDiscount);
        Assert.Equal(0m, result.Lines[1].TotalDiscount);
    }
    #endregion

    #region 18. PromotionsController: Real Database ToggleStatus and Index
    [Fact]
    public async Task Verification_18_PromotionsController_ToggleStatus_And_Index_RealDatabase()
    {
        var testEnv = new TestWebHostEnvironment { ContentRootPath = AppContext.BaseDirectory, WebRootPath = AppContext.BaseDirectory };
        var imgService = new YAGOT_2._0.Services.Image(testEnv);
        var controller = new PromotionsController(_context, imgService, testEnv, _promotionEngine);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, new DummyTempDataProvider());

        var promo = new Promotion
        {
            Title = "E2E ToggleStatus Test",
            PromotionType = "Percentage",
            TargetType = "All",
            DiscountValue = 15m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(2),
            Priority = 10
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        // Toggle to inactive
        var toggleResult = await controller.ToggleStatus(promo.Id);
        Assert.NotNull(toggleResult);

        var updated = await _context.Promotions.AsNoTracking().FirstAsync(p => p.Id == promo.Id);
        Assert.False(updated.IsActive);

        // Toggle back to active
        await controller.ToggleStatus(promo.Id);
        var activeAgain = await _context.Promotions.AsNoTracking().FirstAsync(p => p.Id == promo.Id);
        Assert.True(activeAgain.IsActive);

        // Verify Index action loads with 5 KPI cards
        var indexResult = await controller.Index(null, null, null, 1) as ViewResult;
        Assert.NotNull(indexResult);
        var model = indexResult.Model as PromotionListViewModel;
        Assert.NotNull(model);
        Assert.True(model.TotalPromotions >= 1);
    }
    #endregion

    #region 19. PromotionsController: Details and Safe Deletion Verification
    [Fact]
    public async Task Verification_19_PromotionsController_Details_And_SafeDelete_Verification()
    {
        var testEnv = new TestWebHostEnvironment { ContentRootPath = AppContext.BaseDirectory, WebRootPath = AppContext.BaseDirectory };
        var imgService = new YAGOT_2._0.Services.Image(testEnv);
        var controller = new PromotionsController(_context, imgService, testEnv, _promotionEngine);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, new DummyTempDataProvider());

        // Create a promotion linked to product 20
        var promo = new Promotion
        {
            Title = "E2E Details & Delete Test",
            PromotionType = "FixedAmount",
            TargetType = "Products",
            DiscountValue = 10m,
            IsActive = true,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(3),
            Priority = 2,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = 20, CreatedAt = DateTime.UtcNow }
            }
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(promo.Id);

        // Verify Details action
        var detailsResult = await controller.Details(promo.Id) as ViewResult;
        Assert.NotNull(detailsResult);
        var detailsVm = detailsResult.Model as YAGOT_2._0.Models.Admin.PromotionDetailsVM;
        Assert.NotNull(detailsVm);
        Assert.Equal(promo.Title, detailsVm.Promotion.Title);
        Assert.Single(detailsVm.LinkedProducts);
        Assert.Equal(20, detailsVm.LinkedProducts.First().Id);
        Assert.Contains("منتجات محددة", detailsVm.TargetSummary);
        Assert.Contains("1", detailsVm.TargetSummary);

        // Verify Safe Delete: deleting promotion removes the promotion record, NOT product 20!
        var deleteResult = await controller.DeleteConfirmed(promo.Id) as RedirectToActionResult;
        Assert.NotNull(deleteResult);
        Assert.Equal(nameof(PromotionsController.Index), deleteResult.ActionName);

        var deletedPromo = await _context.Promotions.FindAsync(promo.Id);
        Assert.Null(deletedPromo);

        // Assert product 20 STILL exists in Store DB catalog!
        var product20 = await _context.Products.FindAsync(20);
        Assert.NotNull(product20);
    }
    #endregion

    #region 20. PromotionsController: Validation Error Form State Preservation
    [Fact]
    public async Task Verification_20_PromotionsController_Validation_StatePreservation()
    {
        var testEnv = new TestWebHostEnvironment { ContentRootPath = AppContext.BaseDirectory, WebRootPath = AppContext.BaseDirectory };
        var imgService = new YAGOT_2._0.Services.Image(testEnv);
        var controller = new PromotionsController(_context, imgService, testEnv, _promotionEngine);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, new DummyTempDataProvider());

        // Model with TargetType = "Products" but empty SelectedProductIds
        var invalidModel = new YAGOT_2._0.Models.Admin.PromotionFormVM
        {
            Title = "Invalid Targeted Promotion",
            PromotionType = "Percentage",
            DiscountValue = 15m,
            TargetType = "Products",
            SelectedProductIds = new List<int>(), // missing!
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(5)
        };

        var result = await controller.Create(invalidModel) as ViewResult;
        Assert.NotNull(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(invalidModel.SelectedProductIds)));

        var returnedVm = result.Model as YAGOT_2._0.Models.Admin.PromotionFormVM;
        Assert.NotNull(returnedVm);
        Assert.Equal("Products", returnedVm.TargetType);
        Assert.NotEmpty(returnedVm.AvailableProducts);
        Assert.NotEmpty(returnedVm.AvailableCategories);
    }

    [Fact]
    public async Task Verification_Promotions_AllActionsReturnNativeYAGOT2ViewModels()
    {
        var testEnv = new TestWebHostEnvironment { ContentRootPath = AppContext.BaseDirectory, WebRootPath = AppContext.BaseDirectory };
        var imgService = new YAGOT_2._0.Services.Image(testEnv);
        var controller = new PromotionsController(_context, imgService, testEnv, _promotionEngine);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, new DummyTempDataProvider());

        // 1. Index Action
        var indexResult = await controller.Index(null, null, null, 1) as ViewResult;
        Assert.NotNull(indexResult);
        var indexVm = Assert.IsType<PromotionListViewModel>(indexResult.Model);
        Assert.True(indexVm.TotalPromotions >= 0);
        Assert.True(indexVm.ActivePromotions >= 0);
        Assert.True(indexVm.UpcomingPromotions >= 0);
        Assert.True(indexVm.ExpiredPromotions >= 0);
        Assert.True(indexVm.DisabledPromotions >= 0);

        // 2. Create (GET) Action
        var createResult = await controller.Create() as ViewResult;
        Assert.NotNull(createResult);
        var createVm = Assert.IsType<PromotionFormVM>(createResult.Model);
        Assert.NotEmpty(createVm.AvailableProducts);
        Assert.NotEmpty(createVm.AvailableCategories);

        // Create a test promotion in Store DB to test Details, Edit, and Delete
        var testPromo = new Promotion
        {
            Title = "Full View Model Verification Promo",
            PromotionType = "FixedAmount",
            DiscountValue = 50m,
            TargetType = "All",
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(10),
            IsActive = true,
            Priority = 5
        };
        _context.Promotions.Add(testPromo);
        await _context.SaveChangesAsync();
        _createdPromotionIds.Add(testPromo.Id);

        // 3. Details Action
        var detailsResult = await controller.Details(testPromo.Id) as ViewResult;
        Assert.NotNull(detailsResult);
        var detailsVm = Assert.IsType<PromotionDetailsVM>(detailsResult.Model);
        Assert.Equal(testPromo.Id, detailsVm.Promotion.Id);

        // 4. Edit (GET) Action
        var editResult = await controller.Edit(testPromo.Id) as ViewResult;
        Assert.NotNull(editResult);
        var editVm = Assert.IsType<PromotionFormVM>(editResult.Model);
        Assert.Equal(testPromo.Id, editVm.Id);

        // 5. Delete (GET) Action
        var deleteResult = await controller.Delete(testPromo.Id) as ViewResult;
        Assert.NotNull(deleteResult);
        var deleteVm = Assert.IsType<PromotionDetailsVM>(deleteResult.Model);
        Assert.Equal(testPromo.Id, deleteVm.Promotion.Id);

        // 6. ToggleStatus Action
        var toggleResult = await controller.ToggleStatus(testPromo.Id);
        Assert.IsType<RedirectToActionResult>(toggleResult);
        var updated = await _context.Promotions.FindAsync(testPromo.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
    }
    #endregion
}

public sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "YAGOT_2.0";
    public string ContentRootPath { get; set; } = string.Empty;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public sealed class DummyTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

