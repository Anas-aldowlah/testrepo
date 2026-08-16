using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models;

public class ProductVW
{
    public int Id { get; set; }
    public int Categoryid { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    [Required]
    [Range(0, 1000000.00, ErrorMessage = "Price must be between 0 and 1,000,000.00.")]
    public decimal Price { get; set; }

    [Required]
    [Range(0, 1000000, ErrorMessage = "Stock quantity must be between 0 and 1,000,000.")]
    public int Stockquantity { get; set; }
    public string StockUnit { get; set; } = "Piece";

    [Range(0, 1000000, ErrorMessage = "Volume must be between 0 and 1,000,000 ml.")]
    public int? VolumeMl { get; set; }
    public bool IsRetailEnabled { get; set; }

    [Required]
    [Range(0, 1000000, ErrorMessage = "Initial stock quantity must be between 0 and 1,000,000.")]
    public int InitialStockQuantity { get; set; }

    [Required]
    [Range(0, 1000000, ErrorMessage = "Initial bottle count must be between 0 and 1,000,000.")]
    public int InitialBottleCount { get; set; }

    public List<ProductRetailPriceInput> RetailPrices { get; set; } = [];

    public string? Brand { get; set; }

    public IFormFile? Imagefile { get; set; }
    public string? Existingimage { get; set; }
}

public class ProductRetailPriceInput
{
    public int? Id { get; set; }

    [Required]
    [Range(0, 1000000, ErrorMessage = "Retail size must be between 0 and 1,000,000 ml.")]
    public int SizeMl { get; set; }

    [Required]
    [Range(0, 1000000.00, ErrorMessage = "Retail price must be between 0 and 1,000,000.00.")]
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
