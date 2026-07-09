using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Deliveryorder
{
    public int Orderid { get; set; }

    public string Fullname { get; set; } = null!;

    public string Phonenumber { get; set; } = null!;

    public string? Secondphonenumber { get; set; }

    public string Governorate { get; set; } = null!;

    public string City { get; set; } = null!;

    public string District { get; set; } = null!;
}
