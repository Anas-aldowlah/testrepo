using YAGOT_2._0.Services;

namespace YAGOT_2._0.Extensions
{
    public static class PriceFormattingExtensions
    {
        public static string ToYaqutPrice(this decimal amount, string? currencyCode = null)
        {
            return CurrencyHelper.Format(amount, currencyCode);
        }
        
        public static string ToYaqutPrice(this decimal? amount, string? currencyCode = null)
        {
            return CurrencyHelper.Format(amount, currencyCode);
        }

        public static string ToYaqutAmount(this decimal amount)
        {
            return amount.ToString(
                "#,##0.##",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ToYaqutAmount(this decimal? amount)
        {
            return amount.HasValue
                ? amount.Value.ToYaqutAmount()
                : "—";
        }
    }
}
