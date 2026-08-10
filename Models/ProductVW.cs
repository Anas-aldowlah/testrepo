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
    public string StockUnit { get; set; } = "Piece";
    public int? VolumeMl { get; set; }
    public bool IsRetailEnabled { get; set; }
    public int InitialStockQuantity { get; set; }
    public int InitialBottleCount { get; set; }
    public int AddedStockQuantity { get; set; }
    public int AddedBottleCount { get; set; }
    public List<ProductRetailPriceInput> RetailPrices { get; set; } = [];

    public string? Brand { get; set; }

    public IFormFile? Imagefile { get; set; }
    public string? Existingimage { get; set; }
}

public class ProductRetailPriceInput
{
    public int? Id { get; set; }
    public int SizeMl { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
