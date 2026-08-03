using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Orderdetail
{
    public int Id { get; set; }

    public int Orderid { get; set; }

    public string? Recipientname { get; set; }

    public string? Recipientphone { get; set; }

    public string? Governorate { get; set; }

    public string? Region { get; set; }

    public string? District { get; set; }

    public string? Fulladdress { get; set; }

    public string? Paymentmethod { get; set; }

    public string? Accountname { get; set; }

    public string? Accountnumber { get; set; }

    public string? Transferreferencenumber { get; set; }

    public string? Paymentimagepath { get; set; }

    public string? Paymentnotes { get; set; }

    public DateTime? Createdat { get; set; }

    public DateTime? Updatedat { get; set; }

    public virtual Order Order { get; set; } = null!;
}
