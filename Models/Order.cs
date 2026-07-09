using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Order
{
    public int Id { get; set; }

    public int Userid { get; set; }

    public DateTime? Orderdate { get; set; }

    public decimal Totalamount { get; set; }

    public string Status { get; set; } = null!;

    public string? Trackingnumber { get; set; }

    public DateTime? TimeState { get; set; }

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();

    public virtual User User { get; set; } = null!;
}
