namespace YAGOT_2._0.Services;

/// <summary>
/// Centralized free-shipping threshold rule, shared across Cart and Checkout views
/// to avoid duplicating this business rule in Razor (see CLAUDE.md: no business logic in Views).
/// </summary>
public static class ShippingPolicy
{
    public const decimal FreeShippingThreshold = 500m;

    public static bool QualifiesForFreeShipping(decimal subtotal) =>
        subtotal >= FreeShippingThreshold;

    public static decimal RemainingForFreeShipping(decimal subtotal) =>
        Math.Max(0, FreeShippingThreshold - subtotal);
}