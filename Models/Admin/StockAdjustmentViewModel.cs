using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models.Admin;

public sealed class StockAdjustmentViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid product is required.")]
    public int Id { get; set; }

    [Range(0, 1000000, ErrorMessage = "Added stock quantity must be between 0 and 1,000,000.")]
    public int AddedStockQuantity { get; set; }

    [Range(0, 1000000, ErrorMessage = "Added bottle count must be between 0 and 1,000,000.")]
    public int AddedBottleCount { get; set; }

    [Range(1, 1000000, ErrorMessage = "Bottle volume must be between 1 and 1,000,000 ml.")]
    public int? VolumeMl { get; set; }
}
