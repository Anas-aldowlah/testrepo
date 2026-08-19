using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Sale
{
    public int Id { get; set; }

    public int SalesDayId { get; set; }

    public string? InvoiceNumber { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal FinalAmount { get; set; }

    public string Status { get; set; } = "Draft";

    public string? Notes { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long DraftRevision { get; set; }

    public Guid? EditSessionId { get; set; }

    public string? EditLockedBy { get; set; }

    public DateTime? EditLockExpiresAt { get; set; }

    public virtual SalesDay SalesDay { get; set; } = null!;

    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

    public virtual ICollection<SalePayment> SalePayments { get; set; } = new List<SalePayment>();
}
