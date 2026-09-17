using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Services.Promotions
{
    public class PromotionResult
    {
        public int? PromotionId { get; set; }
        public string? PromotionTitle { get; set; }
        public string? PromotionType { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalPrice { get; set; }
        public int Quantity { get; set; } = 1;
        // Earned free units for the evaluated quantity; used by cart, order, and POS inventory calculations.
        public int FreeQuantity { get; set; } = 0;
        // Configured Y value; used to describe Buy-X-Get-Y before the cart threshold is reached.
        public int? ConfiguredFreeQuantity { get; set; }
        public bool HasPromotion { get; set; } = false;
        public string? SummaryMessage { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public int? Priority { get; set; }
        public string? Description { get; set; }
        public string? BannerImage { get; set; }
        public int? BuyQuantity { get; set; }
        public decimal? DiscountValue { get; set; }
        public decimal? OfferPrice { get; set; }
        public decimal? MinimumAmount { get; set; }
        public List<AppliedPromotionDetail> AppliedPromotions { get; set; } = new();
    }

    public class AppliedPromotionDetail
    {
        public int PromotionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public string Scope { get; set; } = string.Empty; // "Product", "Category", "Storewide", "Cart"
        public int Priority { get; set; }
        public bool CanBeCombined { get; set; }
        public decimal ConfiguredValue { get; set; }
    }

    public class FreeProductItem
    {
        public int ProductId { get; set; }
        public int? RetailPriceId { get; set; }
        public int? RetailSizeMl { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int FreeQuantity { get; set; }
        public string PromotionTitle { get; set; } = string.Empty;
    }

    public class CartItemPromotionResult
    {
        public int ProductId { get; set; }
        public int? RetailPriceId { get; set; }
        public int? RetailSizeMl { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalPrice { get; set; }
        public int FreeQuantity { get; set; }
        public List<AppliedPromotionDetail> AppliedPromotions { get; set; } = new();
    }

    public class CartPromotionResult
    {
        public decimal OriginalTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal FinalTotal { get; set; }
        public string CurrencyCode { get; set; } = "YER";
        public string CurrencySymbol { get; set; } = "ر.ي";
        public List<AppliedPromotionDetail> AppliedPromotions { get; set; } = new();
        public List<FreeProductItem> FreeProducts { get; set; } = new();
        public List<string> PromotionMessages { get; set; } = new();
        public List<CartItemPromotionResult> ItemResults { get; set; } = new();
    }

    public class PromotionCalculationContext
    {
        public List<PromotionCalculationLineItem> Items { get; set; } = new();
        public DateTimeOffset EvaluationTimeUtc { get; set; } = DateTimeOffset.UtcNow;
        public string? Channel { get; set; } = "Online"; // "Online" or "POS"
        public string? CustomerId { get; set; }
        public bool BypassCache { get; set; } = false; // Forced DB read for Checkout/POS
        public string? CurrencyCode { get; set; }
    }

    public class PromotionCalculationLineItem
    {
        public string LineIdentifier { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int? CategoryId { get; set; }
        public int? RetailPriceId { get; set; }
        public int? RetailSizeMl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Authoritative unit price from InventoryService
        public decimal LineGross => UnitPrice * Quantity;
    }

    public class PromotionCalculationResult
    {
        public decimal GrossSubtotal { get; set; }
        public decimal TotalItemDiscounts { get; set; }
        public decimal SpendAmountDiscount { get; set; }
        public decimal TotalDiscounts => TotalItemDiscounts + SpendAmountDiscount;
        public decimal NetTotal => Math.Max(0.00m, GrossSubtotal - TotalDiscounts);
        public string CurrencyCode { get; set; } = "YER";
        public string CurrencySymbol { get; set; } = "ر.ي";

        public List<PromotionLineResult> Lines { get; set; } = new();
        public List<AppliedPromotionDetail> AppliedSpendPromotions { get; set; } = new();
        public List<FreeProductItem> FreeProducts { get; set; } = new();

        /// <summary>
        /// Immutable JSON snapshot payload for order/sale persistence.
        /// </summary>
        public string PromotionSnapshotJson { get; set; } = "{}";
    }

    public class PromotionLineResult
    {
        public string LineIdentifier { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int? CategoryId { get; set; }
        public int? RetailPriceId { get; set; }
        public int? RetailSizeMl { get; set; }
        public decimal OriginalUnitPrice { get; set; }
        public decimal FinalUnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineGross => OriginalUnitPrice * Quantity;
        public decimal TotalDiscount { get; set; }
        public decimal FinalLineTotal => Math.Max(0.00m, LineGross - TotalDiscount);
        public int FreeQuantity { get; set; }
        public List<AppliedPromotionDetail> AppliedPromotions { get; set; } = new();
        public string AppliedPromotionsJson { get; set; } = "[]";
    }

    public class ProductPromotionTeaserDto
    {
        public int ProductId { get; set; }
        public int? RetailPriceId { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal FinalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool HasPromotion { get; set; }
        public string? PromotionTitle { get; set; }
        public string? PromotionType { get; set; }
        public string? BadgeText { get; set; }
        public int? FreeQuantity { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string CurrencyCode { get; set; } = "YER";
        public string CurrencySymbol { get; set; } = "ر.ي";
    }
}
