using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Orderitem
{
    public int Id { get; set; }

    public int Orderid { get; set; }

    public int Productid { get; set; }

    public int? RetailPriceId { get; set; }

    public int? RetailSizeMl { get; set; }

    public int Quantity { get; set; }

    public decimal Unitprice { get; set; }

    public decimal Originalunitprice { get; set; } = 0.00m;

    public decimal Discountamount { get; set; } = 0.00m;

    public decimal Finalunitprice { get; set; } = 0.00m;

    public int Freequantity { get; set; } = 0;

    public int? Appliedpromotionid { get; set; }

    public string? Appliedpromotiontitle { get; set; }

    public string? Appliedpromotionsjson { get; set; }

    public int? FulfilledQuantity { get; set; }

    public int? UnavailableQuantity { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ProductRetailPrice? RetailPrice { get; set; }
}
