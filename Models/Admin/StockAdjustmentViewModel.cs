using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models.Admin;

public sealed class StockAdjustmentViewModel
{
    [Display(Name = "المنتج")]
    [Required(ErrorMessage = "معرّف المنتج مطلوب.")]
    [Range(1, int.MaxValue, ErrorMessage = "المنتج المحدد غير صالح.")]
    public int Id { get; set; }

    [Display(Name = "نوع التسوية")]
    [Required(ErrorMessage = "اختر نوع التسوية.")]
    public StockAdjustmentOperation? Operation { get; set; } = StockAdjustmentOperation.Add;

    [Display(Name = "حجم التسوية")]
    [Required(ErrorMessage = "اختر حجم التسوية.")]
    public string SizeOption { get; set; } = StockAdjustmentSizeOptions.Base;

    [Display(Name = "الحجم المخصص")]
    [Range(1, StockAdjustmentLimits.MaximumQuantity, ErrorMessage = "يجب أن يكون الحجم المخصص بين 1 و1,000,000 مل.")]
    public int? CustomSizeMl { get; set; }

    [Display(Name = "الكمية")]
    [Required(ErrorMessage = "أدخل الكمية.")]
    [Range(1, StockAdjustmentLimits.MaximumQuantity, ErrorMessage = "يجب أن تكون الكمية بين 1 و1,000,000.")]
    public int? Quantity { get; set; } = 1;
}

public enum StockAdjustmentOperation
{
    Add,
    Subtract
}

public static class StockAdjustmentSizeOptions
{
    public const string Base = "Base";
    public const string Custom = "Custom";
    public const string RetailPrefix = "Retail:";
}

public static class StockAdjustmentLimits
{
    public const int MaximumQuantity = 1000000;
}
