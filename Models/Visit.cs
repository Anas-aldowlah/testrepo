using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Visit
{
    public long Id { get; set; }

    public DateTime Visitdate { get; set; }

    public string? Visitorname { get; set; }

    public string? Country { get; set; }

    public string? Governorate { get; set; }

    public string? City { get; set; }

    public string? Device { get; set; }

    public string? Browser { get; set; }
}
