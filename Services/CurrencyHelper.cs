using System;
using System.Collections.Generic;
using System.Globalization;

namespace YAGOT_2._0.Services
{
    public static class CurrencyHelper
    {
        public const string CurrencyYemeniRial = "YER";
        public const string CurrencySaudiRiyal = "SAR";
        public const string CurrencyUsDollar = "USD";

        public static readonly IReadOnlyList<string> AllowedCurrencies =
        [
            CurrencyYemeniRial,
            CurrencySaudiRiyal,
            CurrencyUsDollar
        ];

        private static readonly Dictionary<string, (string Symbol, string NameAr, string NameEn)> Metadata =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [CurrencyYemeniRial] = ("ر.ي", "ريال يمني", "Yemeni Rial"),
                [CurrencySaudiRiyal] = ("ر.س", "ريال سعودي", "Saudi Riyal"),
                [CurrencyUsDollar] = ("$", "دولار أمريكي", "US Dollar")
            };

        // Thread-safe fallback default
        private static volatile string _activeCurrencyCode = CurrencyYemeniRial;

        public static string ActiveCurrencyCode
        {
            get => _activeCurrencyCode;
            internal set
            {
                if (IsValid(value))
                {
                    _activeCurrencyCode = Normalize(value);
                }
            }
        }

        public static bool IsValid(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            return Metadata.ContainsKey(code.Trim());
        }

        public static string Normalize(string? code, string fallback = CurrencyYemeniRial)
        {
            if (string.IsNullOrWhiteSpace(code)) return fallback;
            var trimmed = code.Trim().ToUpperInvariant();
            return Metadata.ContainsKey(trimmed) ? trimmed : fallback;
        }

        public static string GetSymbol(string? currencyCode = null)
        {
            var code = string.IsNullOrWhiteSpace(currencyCode) ? _activeCurrencyCode : currencyCode.Trim();
            if (Metadata.TryGetValue(code, out var meta))
            {
                return meta.Symbol;
            }
            return "ر.ي";
        }

        public static string GetNameAr(string? currencyCode = null)
        {
            var code = string.IsNullOrWhiteSpace(currencyCode) ? _activeCurrencyCode : currencyCode.Trim();
            if (Metadata.TryGetValue(code, out var meta))
            {
                return meta.NameAr;
            }
            return "ريال يمني";
        }

        public static string Format(decimal amount, string? currencyCode = null)
        {
            var symbol = GetSymbol(currencyCode);
            var formatted = amount.ToString("#,##0.##", CultureInfo.InvariantCulture);
            return $"{formatted} {symbol}";
        }

        public static string Format(decimal? amount, string? currencyCode = null)
        {
            if (!amount.HasValue) return "—";
            return Format(amount.Value, currencyCode);
        }
    }

    public static class CurrencyDisplay
    {
        public static string Symbol => CurrencyHelper.GetSymbol();
    }
}
