using System;
using System.Collections.Generic;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Models.Admin;

public class QuickSalesIndexViewModel
{
    public SalesDay? CurrentOpenDay { get; set; }
    public List<Sale> ActiveDrafts { get; set; } = new();
    public int TodayCompletedSalesCount { get; set; }
    public decimal TodayCompletedSalesTotal { get; set; }
}

public class OpenSalesDayViewModel
{
    public DateTime Date { get; set; } = DateTime.Today;
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
    public decimal DiscountTotal { get; set; }
    public List<SaveDraftItemModel> Items { get; set; } = new();
}

public class SaveDraftItemModel
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
}

public class ProductSearchResultDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Brand { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string ImageUrl { get; set; } = null!;
}

public class CompleteSaleRequestModel
{
    public int? SaleId { get; set; }
    public int SalesDayId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Notes { get; set; }
    public decimal DiscountTotal { get; set; }
    public List<SaveDraftItemModel> Items { get; set; } = new();
    public List<CompleteSalePaymentModel> Payments { get; set; } = new();
}

public class CompleteSalePaymentModel
{
    public int PaymentMethodId { get; set; }
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
