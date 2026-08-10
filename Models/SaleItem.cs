using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class SaleItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }

    public int ProductId { get; set; }

    public int? RetailPriceId { get; set; }

    public int? RetailSizeMl { get; set; }

    public string? ProductName { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Discount { get; set; }

    public decimal Total { get; set; }

    public virtual Sale Sale { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ProductRetailPrice? RetailPrice { get; set; }
}
