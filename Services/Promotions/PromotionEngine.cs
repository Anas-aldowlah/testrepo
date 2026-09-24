using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Promotions
{
    public class PromotionEngine : IPromotionEngine
    {
        private const string CacheKeyActivePromotions = "App_Active_Promotions_List";
        private static readonly SemaphoreSlim CacheLock = new(1, 1);
        private const decimal MinimumCharge = 0.00m; // Clamped to non-negative

        private readonly NeondbContext? _context;
        private readonly IMemoryCache? _cache;
        private readonly ILogger<PromotionEngine>? _logger;
        private readonly IInventoryService? _inventoryService;
        private readonly StoreSettingsService? _settingsService;
        private readonly ICapabilityEvaluator? _capabilityEvaluator;

        public PromotionEngine(
            NeondbContext? context = null,
            IMemoryCache? cache = null,
            ILogger<PromotionEngine>? logger = null,
            IInventoryService? inventoryService = null,
            StoreSettingsService? settingsService = null,
            ICapabilityEvaluator? capabilityEvaluator = null)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _inventoryService = inventoryService;
            _settingsService = settingsService;
            _capabilityEvaluator = capabilityEvaluator;
        }

        public void InvalidateActivePromotionsCache()
        {
            _cache?.Remove(CacheKeyActivePromotions);
        }

        public async Task<List<Promotion>> GetActivePromotionsAsync(
            bool bypassCache = false,
            CancellationToken cancellationToken = default)
        {
            if (_capabilityEvaluator != null && !_capabilityEvaluator.IsFeatureEnabled(CapabilityFeatureCodes.OfferDiscountPricing))
            {
                return [];
            }

            var utcNow = DateTimeOffset.UtcNow;

            if (bypassCache || _cache == null)
            {
                return await QueryActivePromotionsFromDbAsync(utcNow, cancellationToken);
            }

            if (_cache.TryGetValue(CacheKeyActivePromotions, out List<Promotion>? cached) && cached != null)
            {
                return cached.Where(p => PromotionTime.IsActive(p, utcNow)).ToList();
            }

            await CacheLock.WaitAsync(cancellationToken);
            try
            {
                if (_cache.TryGetValue(CacheKeyActivePromotions, out cached) && cached != null)
                {
                    return cached.Where(p => PromotionTime.IsActive(p, utcNow)).ToList();
                }

                if (_context == null) return new List<Promotion>();
                var enabled = await _context.Promotions
                    .AsNoTracking()
                    .Include(p => p.PromotionProducts)
                    .Include(p => p.PromotionCategories)
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Priority)
                    .ThenByDescending(p => p.CreatedAt)
                    .ToListAsync(cancellationToken);

                _cache.Set(CacheKeyActivePromotions, enabled, TimeSpan.FromMinutes(5));
                return enabled.Where(p => PromotionTime.IsActive(p, utcNow)).ToList();
            }
            finally
            {
                CacheLock.Release();
            }
        }

        public async Task<DateTimeOffset?> GetNextPromotionTransitionUtcAsync(CancellationToken cancellationToken = default)
        {
            if (_context == null) return null;
            try
            {
                var utcNow = DateTimeOffset.UtcNow;
                List<Promotion>? enabled = null;
                if (_cache != null && _cache.TryGetValue(CacheKeyActivePromotions, out List<Promotion>? cached) && cached != null)
                {
                    enabled = cached;
                }
                else
                {
                    enabled = await _context.Promotions
                        .AsNoTracking()
                        .Where(p => p.IsActive)
                        .ToListAsync(cancellationToken);
                }

                return enabled
                    .SelectMany(p => new[] { p.StartDate, p.EndDate })
                    .Where(t => t > utcNow)
                    .OrderBy(t => t)
                    .Cast<DateTimeOffset?>()
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching next promotion transition");
                return null;
            }
        }

        private async Task<List<Promotion>> QueryActivePromotionsFromDbAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken)
        {
            if (_context == null) return new List<Promotion>();
            var promotions = await _context.Promotions
                .AsNoTracking()
                .Include(p => p.PromotionProducts)
                .Include(p => p.PromotionCategories)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Priority)
                .ThenByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            return promotions.Where(p => PromotionTime.IsActive(p, utcNow)).ToList();
        }

        public Task<PromotionCalculationResult> CalculatePromotionsAsync(
            PromotionCalculationContext context,
            CancellationToken cancellationToken = default)
        {
            return CalculatePromotionsAsync(context, null, cancellationToken);
        }

        public async Task<PromotionCalculationResult> CalculatePromotionsAsync(
            PromotionCalculationContext context,
            List<Promotion>? activePromotions,
            CancellationToken cancellationToken = default)
        {
            var result = new PromotionCalculationResult();
            var currencyCode = !string.IsNullOrWhiteSpace(context.CurrencyCode)
                ? CurrencyHelper.Normalize(context.CurrencyCode)
                : (_settingsService != null ? await _settingsService.GetCurrencyCodeAsync() : CurrencyHelper.ActiveCurrencyCode);

            result.CurrencyCode = currencyCode;
            result.CurrencySymbol = CurrencyHelper.GetSymbol(currencyCode);

            if (context.Items == null || context.Items.Count == 0)
            {
                result.PromotionSnapshotJson = BuildSnapshotJson(result, context);
                return result;
            }

            if (_capabilityEvaluator != null && !_capabilityEvaluator.IsFeatureEnabled(CapabilityFeatureCodes.OfferDiscountPricing))
            {
                activePromotions = [];
            }
            else
            {
                activePromotions ??= await GetActivePromotionsAsync(context.BypassCache, cancellationToken);
            }
            decimal grossSubtotal = 0m;
            decimal totalItemDiscounts = 0m;

            // 1. Calculate Line-Level Promotions
            foreach (var item in context.Items)
            {
                var lineGross = RoundMoney(item.UnitPrice * item.Quantity);
                grossSubtotal += lineGross;

                var applicablePromos = GetApplicableLinePromotions(item.ProductId, item.CategoryId, activePromotions);
                var lineResult = new PromotionLineResult
                {
                    LineIdentifier = item.LineIdentifier,
                    ProductId = item.ProductId,
                    CategoryId = item.CategoryId,
                    RetailPriceId = item.RetailPriceId,
                    RetailSizeMl = item.RetailSizeMl,
                    OriginalUnitPrice = item.UnitPrice,
                    Quantity = item.Quantity
                };

                if (applicablePromos.Count == 0 || item.Quantity <= 0 || item.UnitPrice <= 0)
                {
                    lineResult.FinalUnitPrice = item.UnitPrice;
                    lineResult.TotalDiscount = 0m;
                    result.Lines.Add(lineResult);
                    continue;
                }

                decimal accumulatedDiscount = 0m;
                int freeQuantity = 0;
                var appliedDetails = new List<AppliedPromotionDetail>();

                // Stacking logic
                var firstPromo = applicablePromos.First();
                var canCombine = firstPromo.CanBeCombined;

                foreach (var promo in applicablePromos)
                {
                    if (appliedDetails.Count > 0 && !canCombine)
                    {
                        break; // First promo does not combine
                    }

                    if (appliedDetails.Count > 0 && !promo.CanBeCombined)
                    {
                        continue; // Subsequent promo refuses to combine
                    }

                    var remainingLineBase = Math.Max(0m, lineGross - accumulatedDiscount);
                    if (remainingLineBase <= 0) break;

                    var (discount, freeUnits) = EvaluateLinePromotion(promo, item.UnitPrice, item.Quantity, remainingLineBase);
                    if (discount > 0 || freeUnits > 0)
                    {
                        var allowedDiscount = Math.Min(discount, remainingLineBase);
                        accumulatedDiscount += allowedDiscount;
                        freeQuantity += freeUnits;

                        appliedDetails.Add(new AppliedPromotionDetail
                        {
                            PromotionId = promo.Id,
                            Title = promo.Title,
                            Type = promo.PromotionType,
                            DiscountAmount = RoundMoney(allowedDiscount),
                            Scope = GetScope(promo),
                            Priority = promo.Priority,
                            CanBeCombined = promo.CanBeCombined,
                            ConfiguredValue = promo.DiscountValue
                        });

                        if (!promo.CanBeCombined)
                        {
                            break; // Stop evaluating further
                        }
                    }
                }

                accumulatedDiscount = RoundMoney(Math.Min(lineGross, accumulatedDiscount));
                lineResult.TotalDiscount = accumulatedDiscount;
                lineResult.FreeQuantity = freeQuantity;
                lineResult.FinalUnitPrice = item.Quantity > 0
                    ? RoundMoney(Math.Max(0m, lineGross - accumulatedDiscount) / item.Quantity)
                    : item.UnitPrice;
                lineResult.AppliedPromotions = appliedDetails;
                lineResult.AppliedPromotionsJson = JsonSerializer.Serialize(appliedDetails);

                if (freeQuantity > 0)
                {
                    result.FreeProducts.Add(new FreeProductItem
                    {
                        ProductId = item.ProductId,
                        RetailPriceId = item.RetailPriceId,
                        RetailSizeMl = item.RetailSizeMl,
                        FreeQuantity = freeQuantity,
                        PromotionTitle = appliedDetails.FirstOrDefault()?.Title ?? "عرض مجاني"
                    });
                }

                totalItemDiscounts += accumulatedDiscount;
                result.Lines.Add(lineResult);
            }

            // 2. Cart / Invoice Level: SpendAmount Promotions
            var cartSubtotalAfterItemDiscounts = RoundMoney(Math.Max(0m, grossSubtotal - totalItemDiscounts));
            decimal spendDiscount = 0m;

            var spendPromotions = activePromotions
                .Where(p => p.PromotionType == "SpendAmount" &&
                            p.MinimumAmount.HasValue &&
                            cartSubtotalAfterItemDiscounts >= p.MinimumAmount.Value)
                .OrderBy(p => p.Priority)
                .ThenByDescending(p => p.CreatedAt)
                .ToList();

            if (spendPromotions.Count > 0)
            {
                var highestSpend = spendPromotions.First();
                var allItemDetails = result.Lines.SelectMany(l => l.AppliedPromotions).ToList();
                var canStackWithItems = allItemDetails.Count == 0 ||
                    (highestSpend.CanBeCombined && allItemDetails.All(p => p.CanBeCombined));

                if (canStackWithItems && highestSpend.DiscountValue > 0)
                {
                    if (highestSpend.SpendDiscountType == DiscountValueType.Percentage && highestSpend.DiscountValue <= 100m)
                    {
                        spendDiscount = RoundMoney(cartSubtotalAfterItemDiscounts * (highestSpend.DiscountValue / 100m));
                    }
                    else if (highestSpend.SpendDiscountType == DiscountValueType.FixedAmount)
                    {
                        spendDiscount = RoundMoney(Math.Min(cartSubtotalAfterItemDiscounts, highestSpend.DiscountValue));
                    }

                    spendDiscount = RoundMoney(Math.Min(cartSubtotalAfterItemDiscounts, spendDiscount));

                    if (spendDiscount > 0)
                    {
                        var spendDetail = new AppliedPromotionDetail
                        {
                            PromotionId = highestSpend.Id,
                            Title = highestSpend.Title,
                            Type = "SpendAmount",
                            DiscountAmount = spendDiscount,
                            Scope = "Cart",
                            Priority = highestSpend.Priority,
                            CanBeCombined = highestSpend.CanBeCombined,
                            ConfiguredValue = highestSpend.DiscountValue
                        };
                        result.AppliedSpendPromotions.Add(spendDetail);
                    }
                }
            }

            result.GrossSubtotal = RoundMoney(grossSubtotal);
            result.TotalItemDiscounts = RoundMoney(totalItemDiscounts);
            result.SpendAmountDiscount = RoundMoney(spendDiscount);
            result.PromotionSnapshotJson = BuildSnapshotJson(result, context);

            return result;
        }

        public async Task<PromotionResult> CalculateProductDiscountAsync(
            Product product,
            int quantity = 1,
            ProductRetailPrice? retailPrice = null,
            List<Promotion>? activePromotions = null,
            CancellationToken cancellationToken = default)
        {
            if (product == null || quantity <= 0)
            {
                var price = product != null ? _inventoryService.GetUnitPrice(product, retailPrice) : 0m;
                return new PromotionResult
                {
                    OriginalPrice = price * Math.Max(0, quantity),
                    UnitPrice = price,
                    FinalPrice = price * Math.Max(0, quantity),
                    Quantity = quantity,
                    HasPromotion = false
                };
            }

            var unitPrice = _inventoryService.GetUnitPrice(product, retailPrice);
            var lineGross = RoundMoney(unitPrice * quantity);
            var promotions = activePromotions ?? await GetActivePromotionsAsync(false, cancellationToken);
            var applicablePromos = GetApplicableLinePromotions(product.Id, product.Categoryid, promotions);

            if (applicablePromos.Count == 0)
            {
                return new PromotionResult
                {
                    OriginalPrice = lineGross,
                    UnitPrice = unitPrice,
                    FinalPrice = lineGross,
                    DiscountAmount = 0m,
                    Quantity = quantity,
                    HasPromotion = false
                };
            }

            decimal accumulatedDiscount = 0m;
            int freeQuantity = 0;
            var appliedDetails = new List<AppliedPromotionDetail>();
            var firstPromo = applicablePromos.First();
            var canCombine = firstPromo.CanBeCombined;

            foreach (var promo in applicablePromos)
            {
                if (appliedDetails.Count > 0 && !canCombine) break;
                if (appliedDetails.Count > 0 && !promo.CanBeCombined) continue;

                var remaining = Math.Max(0m, lineGross - accumulatedDiscount);
                if (remaining <= 0) break;

                var (discount, freeUnits) = EvaluateLinePromotion(promo, unitPrice, quantity, remaining);
                if (discount > 0 || freeUnits > 0)
                {
                    var allowed = Math.Min(discount, remaining);
                    accumulatedDiscount += allowed;
                    freeQuantity += freeUnits;

                    appliedDetails.Add(new AppliedPromotionDetail
                    {
                        PromotionId = promo.Id,
                        Title = promo.Title,
                        Type = promo.PromotionType,
                        DiscountAmount = RoundMoney(allowed),
                        Scope = GetScope(promo),
                        Priority = promo.Priority,
                        CanBeCombined = promo.CanBeCombined,
                        ConfiguredValue = promo.DiscountValue
                    });

                    if (!promo.CanBeCombined) break;
                }
            }

            accumulatedDiscount = RoundMoney(Math.Min(lineGross, accumulatedDiscount));
            var finalPrice = RoundMoney(Math.Max(0m, lineGross - accumulatedDiscount));
            var displayPromo = appliedDetails.Count > 0
                ? applicablePromos.FirstOrDefault(p => p.Id == appliedDetails[0].PromotionId)
                : applicablePromos.FirstOrDefault();

            return new PromotionResult
            {
                PromotionId = displayPromo?.Id,
                PromotionTitle = displayPromo?.Title,
                PromotionType = displayPromo?.PromotionType,
                OriginalPrice = lineGross,
                UnitPrice = unitPrice,
                DiscountAmount = accumulatedDiscount,
                FinalPrice = finalPrice,
                Quantity = quantity,
                FreeQuantity = freeQuantity,
                ConfiguredFreeQuantity = displayPromo?.FreeQuantity,
                HasPromotion = accumulatedDiscount > 0 || freeQuantity > 0 || applicablePromos.Count > 0,
                StartDate = displayPromo?.StartDate,
                EndDate = displayPromo?.EndDate,
                Priority = displayPromo?.Priority,
                Description = displayPromo?.Description,
                BannerImage = displayPromo?.BannerImage,
                BuyQuantity = displayPromo?.BuyQuantity,
                DiscountValue = displayPromo?.DiscountValue,
                OfferPrice = displayPromo?.OfferPrice,
                MinimumAmount = displayPromo?.MinimumAmount,
                AppliedPromotions = appliedDetails
            };
        }

        public async Task<CartPromotionResult> CalculateCartDiscountAsync(
            Cart cart,
            CancellationToken cancellationToken = default)
        {
            if (cart == null || cart.Cartitems == null || !cart.Cartitems.Any())
            {
                var curCode = _settingsService != null ? await _settingsService.GetCurrencyCodeAsync() : CurrencyHelper.ActiveCurrencyCode;
                return new CartPromotionResult
                {
                    CurrencyCode = curCode,
                    CurrencySymbol = CurrencyHelper.GetSymbol(curCode)
                };
            }

            return await CalculateCartItemsDiscountAsync(cart.Cartitems, cancellationToken);
        }

        public async Task<CartPromotionResult> CalculateCartItemsDiscountAsync(
            IEnumerable<Cartitem> cartItems,
            CancellationToken cancellationToken = default)
        {
            var result = new CartPromotionResult();
            var currencyCode = _settingsService != null ? await _settingsService.GetCurrencyCodeAsync() : CurrencyHelper.ActiveCurrencyCode;
            result.CurrencyCode = currencyCode;
            result.CurrencySymbol = CurrencyHelper.GetSymbol(currencyCode);

            var itemsList = cartItems?.Where(i => i != null && i.Quantity > 0).ToList() ?? new List<Cartitem>();
            if (itemsList.Count == 0) return result;

            var missingProductIds = itemsList.Where(i => i.Product == null).Select(i => i.Productid).Distinct().ToList();
            var productsDict = (missingProductIds.Count == 0 || _context == null)
                ? new Dictionary<int, Product>()
                : await _context.Products
                    .AsNoTracking()
                    .Include(p => p.RetailPrices)
                    .Where(p => missingProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

            var calcItems = new List<PromotionCalculationLineItem>();
            foreach (var ci in itemsList)
            {
                var prod = ci.Product ?? productsDict.GetValueOrDefault(ci.Productid);
                if (prod == null) continue;

                var retailPrice = ci.RetailPrice ?? prod.RetailPrices?.FirstOrDefault(rp => rp.Id == ci.RetailPriceId);
                var unitPrice = _inventoryService != null
                    ? _inventoryService.GetUnitPrice(prod, retailPrice)
                    : (retailPrice?.Price ?? prod.Price);

                calcItems.Add(new PromotionCalculationLineItem
                {
                    LineIdentifier = ci.Id.ToString(),
                    ProductId = prod.Id,
                    CategoryId = prod.Categoryid,
                    RetailPriceId = ci.RetailPriceId,
                    RetailSizeMl = ci.RetailPrice?.SizeMl ?? retailPrice?.SizeMl,
                    Quantity = ci.Quantity,
                    UnitPrice = unitPrice
                });
            }

            var calcContext = new PromotionCalculationContext
            {
                Items = calcItems,
                Channel = "Online",
                CurrencyCode = currencyCode
            };

            var calcResult = await CalculatePromotionsAsync(calcContext, cancellationToken);

            result.OriginalTotal = calcResult.GrossSubtotal;
            result.DiscountTotal = calcResult.TotalDiscounts;
            result.FinalTotal = calcResult.NetTotal;
            result.AppliedPromotions = calcResult.Lines.SelectMany(l => l.AppliedPromotions).Concat(calcResult.AppliedSpendPromotions).ToList();
            result.FreeProducts = calcResult.FreeProducts;

            foreach (var line in calcResult.Lines)
            {
                var ci = itemsList.FirstOrDefault(i => i.Id.ToString() == line.LineIdentifier)
                         ?? itemsList.FirstOrDefault(i => i.Productid == line.ProductId && i.RetailPriceId == line.RetailPriceId);

                var prodName = ci?.Product?.Name ?? productsDict.GetValueOrDefault(line.ProductId)?.Name ?? $"منتج #{line.ProductId}";

                result.ItemResults.Add(new CartItemPromotionResult
                {
                    ProductId = line.ProductId,
                    RetailPriceId = line.RetailPriceId,
                    RetailSizeMl = line.RetailSizeMl,
                    ProductName = prodName,
                    Quantity = line.Quantity,
                    UnitPrice = line.OriginalUnitPrice,
                    OriginalPrice = line.LineGross,
                    DiscountAmount = line.TotalDiscount,
                    FinalPrice = line.FinalLineTotal,
                    FreeQuantity = line.FreeQuantity,
                    AppliedPromotions = line.AppliedPromotions
                });
            }

            return result;
        }

        public async Task<ProductPromotionTeaserDto> GetProductPromotionTeaserAsync(
            Product product,
            ProductRetailPrice? retailPrice = null,
            CancellationToken cancellationToken = default)
        {
            var currencyCode = _settingsService != null ? await _settingsService.GetCurrencyCodeAsync() : CurrencyHelper.ActiveCurrencyCode;
            var unitPrice = _inventoryService != null
                ? _inventoryService.GetUnitPrice(product, retailPrice)
                : (retailPrice?.Price ?? product.Price);
            var promoResult = await CalculateProductDiscountAsync(product, 1, retailPrice, null, cancellationToken);

            string? badgeText = null;
            if (promoResult.AppliedPromotions.Any())
            {
                var p = promoResult.AppliedPromotions.First();
                badgeText = p.Type switch
                {
                    "Percentage" => $"-{p.ConfiguredValue:0.##}%",
                    "FixedAmount" => $"-{p.DiscountAmount:0.##} {CurrencyHelper.GetSymbol(currencyCode)}",
                    "BuyXGetY" => "اشترِ واحصل على مجاناً",
                    "QuantityPrice" => "سعر خاص للكمية",
                    _ => "عرض خاص"
                };
            }
            else if (promoResult.HasPromotion && promoResult.PromotionType == "BuyXGetY")
            {
                badgeText = "اشترِ واحصل على مجاناً";
            }

            return new ProductPromotionTeaserDto
            {
                ProductId = product.Id,
                RetailPriceId = retailPrice?.Id,
                OriginalPrice = unitPrice,
                FinalPrice = promoResult.FinalPrice,
                DiscountAmount = promoResult.DiscountAmount,
                HasPromotion = promoResult.HasPromotion,
                PromotionTitle = promoResult.PromotionTitle,
                PromotionType = promoResult.PromotionType,
                BadgeText = badgeText,
                FreeQuantity = promoResult.ConfiguredFreeQuantity,
                EndDate = promoResult.EndDate,
                CurrencyCode = currencyCode,
                CurrencySymbol = CurrencyHelper.GetSymbol(currencyCode)
            };
        }

        private static List<Promotion> GetApplicableLinePromotions(
            int productId,
            int? categoryId,
            List<Promotion> activePromotions)
        {
            var matching = activePromotions.Where(p =>
            {
                if (p.PromotionType == "SpendAmount") return false;

                var hasProductRelations = p.PromotionProducts != null && p.PromotionProducts.Any();
                var hasCategoryRelations = p.PromotionCategories != null && p.PromotionCategories.Any();

                var isExplicitProductTarget = string.Equals(p.TargetType, "Products", StringComparison.OrdinalIgnoreCase);
                var isExplicitCategoryTarget = string.Equals(p.TargetType, "Categories", StringComparison.OrdinalIgnoreCase);
                var isExplicitStorewide = string.Equals(p.TargetType, "All", StringComparison.OrdinalIgnoreCase);

                // Storewide ONLY if explicitly "All" (and has no targeting relations) OR (not explicitly Products/Categories and has no relations)
                var isStorewide = (isExplicitStorewide && !hasProductRelations && !hasCategoryRelations) ||
                                  (!isExplicitProductTarget && !isExplicitCategoryTarget && !hasProductRelations && !hasCategoryRelations);

                // Matches product if targeted to products and contains this product
                var matchesProduct = (isExplicitProductTarget || hasProductRelations) &&
                                     hasProductRelations &&
                                     p.PromotionProducts!.Any(pp => pp.ProductId == productId);

                // Matches category if targeted to categories and contains this category
                var matchesCategory = (isExplicitCategoryTarget || hasCategoryRelations) &&
                                      hasCategoryRelations &&
                                      categoryId.HasValue &&
                                      p.PromotionCategories!.Any(pc => pc.CategoryId == categoryId.Value);

                return isStorewide || matchesProduct || matchesCategory;
            }).OrderBy(p => p.Priority).ThenByDescending(p => p.CreatedAt).ToList();

            if (matching.Count == 0) return matching;

            var highest = matching.First();
            if (!highest.CanBeCombined)
            {
                return new List<Promotion> { highest };
            }

            return matching.Where(p => p.CanBeCombined).ToList();
        }

        private static (decimal discount, int freeQty) EvaluateLinePromotion(
            Promotion promo,
            decimal unitPrice,
            int quantity,
            decimal currentLineBase)
        {
            decimal discount = 0m;
            int freeQty = 0;

            switch (promo.PromotionType)
            {
                case "Percentage":
                    if (promo.DiscountValue > 0 && promo.DiscountValue <= 100m)
                    {
                        discount = RoundMoney(currentLineBase * (promo.DiscountValue / 100m));
                    }
                    break;

                case "FixedAmount":
                    if (promo.DiscountValue > 0)
                    {
                        var discountPerUnit = Math.Min(unitPrice, promo.DiscountValue);
                        discount = RoundMoney(discountPerUnit * quantity);
                    }
                    break;

                case "BuyXGetY":
                    int buyQty = promo.BuyQuantity ?? 1;
                    int freePerSet = promo.FreeQuantity ?? 1;

                    if (buyQty > 0 && quantity >= buyQty && freePerSet > 0)
                    {
                        int fullSets = quantity / buyQty;
                        freeQty = fullSets * freePerSet;
                        discount = 0m;
                    }
                    break;

                case "QuantityPrice":
                    int reqQty = promo.BuyQuantity ?? 1;
                    decimal offerPrice = promo.OfferPrice ?? promo.DiscountValue;

                    if (reqQty > 0 && quantity >= reqQty && offerPrice > 0)
                    {
                        int fullSets = quantity / reqQty;
                        decimal standardPriceForSets = fullSets * reqQty * unitPrice;
                        decimal offerPriceForSets = fullSets * offerPrice;
                        if (standardPriceForSets > offerPriceForSets)
                        {
                            discount = RoundMoney(standardPriceForSets - offerPriceForSets);
                        }
                    }
                    break;
            }

            return (discount, freeQty);
        }

        private static string GetScope(Promotion p)
        {
            if (p.PromotionType == "SpendAmount") return "Cart";
            if (string.Equals(p.TargetType, "Products", StringComparison.OrdinalIgnoreCase) || (p.PromotionProducts != null && p.PromotionProducts.Any())) return "Product";
            if (string.Equals(p.TargetType, "Categories", StringComparison.OrdinalIgnoreCase) || (p.PromotionCategories != null && p.PromotionCategories.Any())) return "Category";
            return "Storewide";
        }

        private static decimal RoundMoney(decimal amount) =>
            Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        private static string BuildSnapshotJson(PromotionCalculationResult result, PromotionCalculationContext context)
        {
            var snapshotObj = new
            {
                engine_version = "1.0.0",
                evaluated_at_utc = context.EvaluationTimeUtc.ToString("O"),
                channel = context.Channel ?? "Online",
                currency_code = result.CurrencyCode,
                currency_symbol = result.CurrencySymbol,
                gross_subtotal = result.GrossSubtotal,
                total_item_discounts = result.TotalItemDiscounts,
                spend_amount_discount = result.SpendAmountDiscount,
                total_discounts = result.TotalDiscounts,
                net_total = result.NetTotal,
                applied_spend_promotions = result.AppliedSpendPromotions.Select(p => new
                {
                    promotion_id = p.PromotionId,
                    title = p.Title,
                    discount_amount = p.DiscountAmount,
                    type = p.Type,
                    configured_value = p.ConfiguredValue
                }),
                line_snapshots = result.Lines.Select(l => new
                {
                    line_identifier = l.LineIdentifier,
                    product_id = l.ProductId,
                    retail_price_id = l.RetailPriceId,
                    retail_size_ml = l.RetailSizeMl,
                    quantity = l.Quantity,
                    original_unit_price = l.OriginalUnitPrice,
                    final_unit_price = l.FinalUnitPrice,
                    line_gross = l.LineGross,
                    total_discount = l.TotalDiscount,
                    final_line_total = l.FinalLineTotal,
                    free_quantity = l.FreeQuantity,
                    applied_promotions = l.AppliedPromotions.Select(ap => new
                    {
                        promotion_id = ap.PromotionId,
                        title = ap.Title,
                        promotion_type = ap.Type,
                        discount_amount = ap.DiscountAmount,
                        configured_value = ap.ConfiguredValue
                    })
                })
            };

            return JsonSerializer.Serialize(snapshotObj, new JsonSerializerOptions { WriteIndented = false });
        }
    }
}
