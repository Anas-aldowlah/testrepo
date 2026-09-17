using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Promotions
{
    public sealed class PromotionSnapshotReader
    {
        private static readonly IReadOnlyDictionary<string, string> ArabicTypeLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Percentage"] = "خصم بنسبة مئوية",
                ["PercentageDiscount"] = "خصم بنسبة مئوية",
                ["FixedAmount"] = "خصم بمبلغ ثابت",
                ["FixedDiscount"] = "خصم بمبلغ ثابت",
                ["QuantityPrice"] = "سعر خاص للكمية",
                ["BuyXGetY"] = "اشترِ واحصل على كمية مجانية",
                ["BuyXGetDiscount"] = "خصم عند شراء كمية",
                ["SpendAmount"] = "خصم عند بلوغ مبلغ محدد"
            };

        public static string GetArabicTypeLabel(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return "عرض خاص";
            return ArabicTypeLabels.TryGetValue(type, out var label) ? label : type;
        }

        public HistoricalOrderSnapshot ReadOrderSnapshot(Order order)
        {
            if (order == null) return new HistoricalOrderSnapshot();

            if (!string.IsNullOrWhiteSpace(order.Promotionsnapshotjson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(order.Promotionsnapshotjson);
                    var root = doc.RootElement;
                    var currency = root.TryGetProperty("currency_code", out var c) ? c.GetString() ?? "YER" : order.Currencycode ?? "YER";
                    var symbol = root.TryGetProperty("currency_symbol", out var s) ? s.GetString() ?? CurrencyHelper.GetSymbol(currency) : CurrencyHelper.GetSymbol(currency);
                    var gross = root.TryGetProperty("gross_subtotal", out var g) ? g.GetDecimal() : order.Totalamount + order.Discounttotal;
                    var totalDiscounts = root.TryGetProperty("total_discounts", out var d) ? d.GetDecimal() : order.Discounttotal;
                    var net = root.TryGetProperty("net_total", out var n) ? n.GetDecimal() : order.Totalamount;

                    return new HistoricalOrderSnapshot
                    {
                        CurrencyCode = currency,
                        CurrencySymbol = symbol,
                        GrossSubtotal = gross,
                        TotalDiscounts = totalDiscounts,
                        NetTotal = net,
                        RawJson = order.Promotionsnapshotjson
                    };
                }
                catch
                {
                    // Fallback to columns
                }
            }

            var fallbackCurrency = order.Currencycode ?? "YER";
            return new HistoricalOrderSnapshot
            {
                CurrencyCode = fallbackCurrency,
                CurrencySymbol = CurrencyHelper.GetSymbol(fallbackCurrency),
                GrossSubtotal = order.Totalamount + order.Discounttotal,
                TotalDiscounts = order.Discounttotal,
                NetTotal = order.Totalamount
            };
        }

        public HistoricalSaleSnapshot ReadSaleSnapshot(Sale sale)
        {
            if (sale == null) return new HistoricalSaleSnapshot();

            if (!string.IsNullOrWhiteSpace(sale.PromotionSnapshotJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(sale.PromotionSnapshotJson);
                    var root = doc.RootElement;
                    var currency = root.TryGetProperty("currency_code", out var c) ? c.GetString() ?? "YER" : sale.CurrencyCode ?? "YER";
                    var symbol = root.TryGetProperty("currency_symbol", out var s) ? s.GetString() ?? CurrencyHelper.GetSymbol(currency) : CurrencyHelper.GetSymbol(currency);
                    var gross = root.TryGetProperty("gross_subtotal", out var g) ? g.GetDecimal() : sale.TotalAmount;
                    var promoDisc = root.TryGetProperty("promotion_discount_total", out var pd) ? pd.GetDecimal() : sale.PromotionDiscountTotal;
                    var manualDisc = root.TryGetProperty("manual_discount_total", out var md) ? md.GetDecimal() : sale.ManualDiscountTotal;
                    var net = root.TryGetProperty("net_total", out var n) ? n.GetDecimal() : sale.FinalAmount;

                    return new HistoricalSaleSnapshot
                    {
                        CurrencyCode = currency,
                        CurrencySymbol = symbol,
                        GrossSubtotal = gross,
                        PromotionDiscountTotal = promoDisc,
                        ManualDiscountTotal = manualDisc,
                        TotalDiscounts = promoDisc + manualDisc,
                        FinalAmount = net,
                        RawJson = sale.PromotionSnapshotJson
                    };
                }
                catch
                {
                    // Fallback to columns
                }
            }

            var fallbackCurrency = sale.CurrencyCode ?? "YER";
            return new HistoricalSaleSnapshot
            {
                CurrencyCode = fallbackCurrency,
                CurrencySymbol = CurrencyHelper.GetSymbol(fallbackCurrency),
                GrossSubtotal = sale.TotalAmount,
                PromotionDiscountTotal = sale.PromotionDiscountTotal,
                ManualDiscountTotal = sale.ManualDiscountTotal,
                TotalDiscounts = sale.DiscountTotal,
                FinalAmount = sale.FinalAmount
            };
        }
    }

    public class HistoricalOrderSnapshot
    {
        public string CurrencyCode { get; set; } = "YER";
        public string CurrencySymbol { get; set; } = "ر.ي";
        public decimal GrossSubtotal { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal NetTotal { get; set; }
        public string? RawJson { get; set; }
    }

    public class HistoricalSaleSnapshot
    {
        public string CurrencyCode { get; set; } = "YER";
        public string CurrencySymbol { get; set; } = "ر.ي";
        public decimal GrossSubtotal { get; set; }
        public decimal PromotionDiscountTotal { get; set; }
        public decimal ManualDiscountTotal { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal FinalAmount { get; set; }
        public string? RawJson { get; set; }
    }
}
