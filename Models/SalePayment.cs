using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class SalePayment
{
    public int Id { get; set; }

    public int SaleId { get; set; }

    public int PaymentMethodId { get; set; }

    public decimal Amount { get; set; }

    public string? TransactionReference { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Sale Sale { get; set; } = null!;

    public virtual Paymentmethod PaymentMethod { get; set; } = null!;
}
