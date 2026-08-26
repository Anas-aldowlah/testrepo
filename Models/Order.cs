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

    public bool Stockdeducted { get; set; }

    public string? Trackingnumber { get; set; }

    public DateTime? TimeState { get; set; }

    public string? Notes { get; set; }

    public string? Paymentmethod { get; set; }

    public string Paymentstatus { get; set; } = null!;

    public string? Receipturl { get; set; }

    public DateTime? Paymentverifiedat { get; set; }

    public int? Paymentverifiedbyuserid { get; set; }

    public string? Workflowstate { get; set; }

    public decimal? Refundrequiredamount { get; set; }

    public string? Refundreason { get; set; }

    public decimal? Finalfulfilledamount { get; set; }

    public virtual Deliveryorder? Deliveryorder { get; set; }

    public virtual Orderdetail? Orderdetail { get; set; }

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();
}
