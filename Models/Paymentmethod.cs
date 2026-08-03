using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Paymentmethod
{
    public int Id { get; set; }

    public string Type { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Accountholdername { get; set; } = null!;

    public string Accountnumber { get; set; } = null!;

    public string? Instructions { get; set; }

    public string? Cardcolor { get; set; }

    public bool Isactive { get; set; }

    public int Storesettingsid { get; set; }

    public virtual Storesetting Storesettings { get; set; } = null!;
}
