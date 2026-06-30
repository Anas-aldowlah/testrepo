using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Imageurl { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
