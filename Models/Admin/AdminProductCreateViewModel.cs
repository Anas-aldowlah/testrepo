using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models.Admin;

public sealed class AdminProductCreateViewModel
{
    [Display(Name = "التصنيف")]
    [Required(ErrorMessage = "التصنيف مطلوب.")]
    [Range(1, int.MaxValue, ErrorMessage = "التصنيف مطلوب.")]
    public int? Categoryid { get; set; }

    [Display(Name = "اسم المنتج")]
    [Required(ErrorMessage = "اسم المنتج مطلوب.")]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [Display(Name = "سعر البيع")]
    [Required(ErrorMessage = "سعر البيع مطلوب.")]
    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "يجب أن يكون سعر البيع بين 0 و1,000,000.")]
    public decimal? Price { get; set; }

    [Display(Name = "الكمية")]
    [Required(ErrorMessage = "الكمية مطلوبة.")]
    [Range(0, 1000000, ErrorMessage = "يجب أن تكون الكمية بين 0 و1,000,000.")]
    public int? Stockquantity { get; set; }

    [Display(Name = "الوحدة")]
    [Required(ErrorMessage = "الوحدة مطلوبة.")]
    [RegularExpression("^(Piece|Ml)$", ErrorMessage = "وحدة المخزون المحددة غير صالحة.")]
    public string? StockUnit { get; set; } = "Piece";

    [Display(Name = "حجم العبوة")]
    [Range(1, 1000000, ErrorMessage = "يجب أن يكون حجم العبوة بين 1 و1,000,000 مل.")]
    public int? VolumeMl { get; set; }

    public bool IsRetailEnabled { get; set; }
    public List<AdminProductCreateRetailPriceInput> RetailPrices { get; set; } = [];
    public string? Brand { get; set; }
    public IFormFile? Imagefile { get; set; }
}

public sealed class AdminProductCreateRetailPriceInput
{
    public int? Id { get; set; }

    [Display(Name = "حجم التجزئة")]
    [Range(1, 1000000, ErrorMessage = "يجب أن يكون حجم التجزئة بين 1 و1,000,000 مل.")]
    public int? SizeMl { get; set; }

    [Display(Name = "سعر التجزئة")]
    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "يجب أن يكون سعر التجزئة بين 0.01 و1,000,000.")]
    public decimal? Price { get; set; }

    public bool IsActive { get; set; } = true;
}
