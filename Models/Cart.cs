using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Cart
{
    public int Id { get; set; }

    public int Userid { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual ICollection<Cartitem> Cartitems { get; set; } = new List<Cartitem>();

    public virtual User User { get; set; } = null!;
}
