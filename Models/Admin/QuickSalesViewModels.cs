using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Models.Admin;

public class QuickSalesIndexViewModel
{
    public SalesDay? CurrentOpenDay { get; set; }
    public List<Sale> ActiveDrafts { get; set; } = new();
    public int TodayCompletedSalesCount { get; set; }
    public decimal TodayCompletedSalesTotal { get; set; }
}

public class NewSaleViewModel
{
    public Sale Sale { get; init; } = new();
    public SalesDay SalesDay { get; init; } = new();
    public IReadOnlyList<ProductSearchResultDto> AvailableProducts { get; init; } = Array.Empty<ProductSearchResultDto>();
    public IReadOnlyList<QuickSaleCustomerDto> Customers { get; init; } = Array.Empty<QuickSaleCustomerDto>();
    public bool IsExistingDraft { get; init; }
}

public class QuickSaleCustomerDto
{
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
}

public class OpenSalesDayViewModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    [Required]
    [Range(0, 1000000.00, ErrorMessage = "Opening balance must be between 0 and 1,000,000.00.")]
    public decimal OpeningBalance { get; set; } = 0m;
    public string? Notes { get; set; }
}

public class SaveDraftRequestModel
{
    public int? SaleId { get; set; }
    public int SalesDayId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Notes { get; set; }
    [Required]
    [Range(0, 1000000.00, ErrorMessage = "Discount total must be between 0 and 1,000,000.00.")]
    public decimal DiscountTotal { get; set; }
    public List<SaveDraftItemModel> Items { get; set; } = new();
}

public class SaveDraftItemModel
{
    public int ProductId { get; set; }
    public int? RetailPriceId { get; set; }
    [Range(0, 1000000, ErrorMessage = "Retail size must be between 0 and 1,000,000 ml.")]
    public int? RetailSizeMl { get; set; }
    public string? ProductName { get; set; }
    [Required]
    [Range(0, 1000000, ErrorMessage = "Quantity must be between 0 and 1,000,000.")]
    public int Quantity { get; set; }

    [Required]
    [Range(0, 1000000.00, ErrorMessage = "Unit price must be between 0 and 1,000,000.00.")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Range(0, 1000000.00, ErrorMessage = "Discount must be between 0 and 1,000,000.00.")]
    public decimal Discount { get; set; }
}

public class ProductSearchResultDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Brand { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string StockUnit { get; set; } = "Piece";
    public int? VolumeMl { get; set; }
    public bool IsRetailEnabled { get; set; }
    public List<ProductRetailPriceDto> RetailPrices { get; set; } = new();
    public string ImageUrl { get; set; } = null!;
}

public class ProductRetailPriceDto
{
    public int Id { get; set; }
    public int SizeMl { get; set; }
    public decimal Price { get; set; }
}

public class CompleteSaleRequestModel
{
    public int? SaleId { get; set; }
    public int SalesDayId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Notes { get; set; }
    [Required]
    [Range(0, 1000000.00, ErrorMessage = "Discount total must be between 0 and 1,000,000.00.")]
    public decimal DiscountTotal { get; set; }
    public List<SaveDraftItemModel> Items { get; set; } = new();
    public List<CompleteSalePaymentModel> Payments { get; set; } = new();
}

public class CompleteSalePaymentModel
{
    public int PaymentMethodId { get; set; }
    [Required]
    [Range(0, 1000000.00, ErrorMessage = "Payment amount must be between 0 and 1,000,000.00.")]
    public decimal Amount { get; set; }
    public string? TransactionReference { get; set; }
}

public class PaymentMethodOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? CardColor { get; set; }
}

public class CompleteSaleResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public int? SaleId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public string? CompletedAt { get; set; }
}

public class SalesDayLedgerViewModel
{
    public SalesDay SalesDay { get; set; } = null!;
    public int CompletedSalesCount { get; set; }
    public int DraftsCount { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal NetTotalAmount { get; set; }
    public List<PaymentMethodTotalDto> PaymentTotals { get; set; } = new();
    public List<Sale> CompletedSales { get; set; } = new();
    public bool IsCurrentOpenDay { get; set; }
}

public class PaymentMethodTotalDto
{
    public int PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string PaymentMethodType { get; set; } = null!;
    public decimal TotalAmount { get; set; }
}

public class SalesDayHistoryItemDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = null!;
    public decimal OpeningBalance { get; set; }
    public int CompletedSalesCount { get; set; }
    public int DraftsCount { get; set; }
    public decimal TotalSales { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
}

public class CloseSalesDayRequestModel
{
    public int SalesDayId { get; set; }
    public string? Notes { get; set; }
}

public class QuickSalesReportsIndexViewModel
{
    public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-7);
    public DateTime EndDate { get; set; } = DateTime.Today;
    public int TotalOperations { get; set; }
    public decimal GrossSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal NetSales { get; set; }
    public List<PaymentMethodTotalDto> PaymentTotals { get; set; } = new();
    public List<ProductSalesSummaryDto> TopProducts { get; set; } = new();
    public List<Sale> SampleCompletedSales { get; set; } = new();
}

public class ProductSalesSummaryDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string? Brand { get; set; }
    public int? RetailSizeMl { get; set; }
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class ReportsFilterInputModel
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? PaymentMethodId { get; set; }
    public string? ReportType { get; set; }
}
