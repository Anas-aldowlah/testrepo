using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Securitylog
{
    public int Id { get; set; }

    public int? Userid { get; set; }

    public string Action { get; set; } = null!;

    public string Ipaddress { get; set; } = null!;

    public string? Deviceinfo { get; set; }

    public int? Riskscore { get; set; }

    public DateTime? Createdat { get; set; }

    public virtual User? User { get; set; }
}
