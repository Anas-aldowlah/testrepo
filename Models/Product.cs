using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Product
{
    public int Id { get; set; }

    public int Categoryid { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int Stockquantity { get; set; }

    public string? Imageurl { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual ICollection<Cartitem> Cartitems { get; set; } = new List<Cartitem>();

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();
}
