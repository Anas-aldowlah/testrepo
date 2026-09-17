using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Promotions
{
    /// <summary>
    /// The single authoritative promotion calculation engine shared across
    /// Storefront, Cart, Checkout, and POS.
    /// </summary>
    public interface IPromotionEngine
    {
        /// <summary>
        /// Evaluates promotions for a collection of line items (Cart, Checkout, or POS Sale).
        /// Currency-aware: monetary values reflect the configured canonical currency.
        /// </summary>
        Task<PromotionCalculationResult> CalculatePromotionsAsync(
            PromotionCalculationContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates promotions for a collection of line items using a supplied active promotions list.
        /// Useful for testing, offline evaluation, or cached pipeline execution.
        /// </summary>
        Task<PromotionCalculationResult> CalculatePromotionsAsync(
            PromotionCalculationContext context,
            List<Promotion>? activePromotions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates discount for a single product and quantity (respecting retail/ML decants).
        /// </summary>
        Task<PromotionResult> CalculateProductDiscountAsync(
            Product product,
            int quantity = 1,
            ProductRetailPrice? retailPrice = null,
            List<Promotion>? activePromotions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates discount for an entire cart (product-level + spend-amount promotions).
        /// </summary>
        Task<CartPromotionResult> CalculateCartDiscountAsync(
            Cart cart,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates discount for a collection of cart items directly.
        /// </summary>
        Task<CartPromotionResult> CalculateCartItemsDiscountAsync(
            IEnumerable<Cartitem> cartItems,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates applicable promotions for a single product display (PDP / Catalog teaser).
        /// </summary>
        Task<ProductPromotionTeaserDto> GetProductPromotionTeaserAsync(
            Product product,
            ProductRetailPrice? retailPrice = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all currently active promotions.
        /// </summary>
        Task<List<Promotion>> GetActivePromotionsAsync(
            bool bypassCache = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears the cached active promotion list.
        /// </summary>
        void InvalidateActivePromotionsCache();

        /// <summary>
        /// Returns the timestamp of the nearest upcoming promotion transition (start or end) in UTC.
        /// </summary>
        Task<DateTimeOffset?> GetNextPromotionTransitionUtcAsync(CancellationToken cancellationToken = default);
    }
}
