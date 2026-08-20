using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models.Admin;

public sealed class AdminProductEditViewModel
{
    [Required(ErrorMessage = "بيانات المنتج مطلوبة.")]
    public ProductVW Product { get; set; } = new();

    [Required(ErrorMessage = "بيانات تسوية المخزون مطلوبة.")]
    public StockAdjustmentViewModel StockAdjustment { get; set; } = new();
    public IReadOnlyList<StockAdjustmentSizeViewModel> StockSizes { get; set; } = [];
}

public sealed record StockAdjustmentSizeViewModel(string Value, int SizeMl, string Label);
