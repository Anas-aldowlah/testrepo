namespace YAGOT_2._0.Services;

/// <summary>
/// Central shipping rule shared across order totals and storefront views.
/// </summary>
public static class ShippingPolicy
{
    public const decimal Charge = 0m;
    public const decimal FreeShippingThreshold = 0m;

    public static decimal CalculateCharge(decimal subtotal) => Charge;

    public static bool QualifiesForFreeShipping(decimal subtotal) => true;

    public static decimal RemainingForFreeShipping(decimal subtotal) => 0m;
}
