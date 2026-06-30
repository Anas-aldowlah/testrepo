using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class CategoryVW
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }
    public IFormFile? ImageFile { get; set; }

    public string? Existingimage { get; set; }
}
