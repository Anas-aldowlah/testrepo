using System.ComponentModel.DataAnnotations.Schema;

namespace YAGOT_2._0.Models;

public class ProductVW
{
    public int Id { get; set; }
    public int Categoryid { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stockquantity { get; set; }

    public IFormFile? Imagefile { get; set; }
    public string? Existingimage { get; set; }
}