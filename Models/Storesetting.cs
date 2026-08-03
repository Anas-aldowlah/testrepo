using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Storesetting
{
    public int Id { get; set; }

    public string? Whatsappnumber { get; set; }

    public string? Contactemail { get; set; }

    public string? Instagramlink { get; set; }

    public string? Twitterlink { get; set; }

    public string? Tiktoklink { get; set; }

    public int? Featuredcategoryid { get; set; }

    public string? Heromarketingtext { get; set; }

    public string? Heromarketingdesc { get; set; }

    public virtual ICollection<Paymentmethod> Paymentmethods { get; set; } = new List<Paymentmethod>();
}
