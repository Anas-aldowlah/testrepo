using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Cartitem
{
    public int Id { get; set; }

    public int Cartid { get; set; }

    public int Productid { get; set; }

    public int Quantity { get; set; }

    public virtual Cart Cart { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
