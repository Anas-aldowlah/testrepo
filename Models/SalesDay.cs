using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class SalesDay
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public string Status { get; set; } = "Open";

    public decimal OpeningBalance { get; set; }

    public decimal TotalSales { get; set; }

    public decimal TotalCash { get; set; }

    public decimal TotalTransfer { get; set; }

    public decimal TotalWallet { get; set; }

    public decimal TotalReturns { get; set; }

    public decimal NetTotal { get; set; }

    public string? Notes { get; set; }

    public string? CreatedBy { get; set; }

    public string? ClosedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
