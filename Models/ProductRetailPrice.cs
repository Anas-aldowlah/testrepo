using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class ProductRetailPrice
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int SizeMl { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<Cartitem> Cartitems { get; set; } = new List<Cartitem>();

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();

    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
}
