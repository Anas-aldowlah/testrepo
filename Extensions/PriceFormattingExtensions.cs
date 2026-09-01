namespace YAGOT_2._0.Extensions
{
    public static class PriceFormattingExtensions
    {
        public static string ToYaqutPrice(this decimal amount)
        {
            var formatted = amount.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
            return $"{formatted} ر.س";
        }
        
        public static string ToYaqutPrice(this decimal? amount)
        {
            if (!amount.HasValue) return "—";
            return amount.Value.ToYaqutPrice();
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
