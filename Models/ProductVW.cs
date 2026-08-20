using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models;

public class ProductVW
{
    [Required(ErrorMessage = "معرّف المنتج مطلوب.")]
    public int Id { get; set; }

    [Display(Name = "التصنيف")]
    [Required(ErrorMessage = "التصنيف مطلوب.")]
    public int Categoryid { get; set; }

    [Display(Name = "اسم المنتج")]
    [Required(ErrorMessage = "اسم المنتج مطلوب.")]
    public string Name { get; set; } = null!;

    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Display(Name = "السعر")]
    [Required(ErrorMessage = "السعر مطلوب.")]
    [Range(0, 1000000.00, ErrorMessage = "يجب أن يكون السعر بين 0 و1,000,000.")]
    public decimal Price { get; set; }

    [Display(Name = "رصيد المخزون")]
    [Required(ErrorMessage = "رصيد المخزون مطلوب.")]
    [Range(0, 1000000, ErrorMessage = "يجب أن يكون رصيد المخزون بين 0 و1,000,000.")]
    public int Stockquantity { get; set; }

    [Display(Name = "وحدة المخزون")]
    [Required(ErrorMessage = "وحدة المخزون مطلوبة.")]
    public string StockUnit { get; set; } = "Piece";

    [Display(Name = "حجم العبوة الأساسي")]
    [Range(0, 1000000, ErrorMessage = "يجب أن يكون حجم العبوة الأساسي بين 0 و1,000,000 مل.")]
    public int? VolumeMl { get; set; }

    [Display(Name = "تفعيل أسعار التجزئة")]
    [Required(ErrorMessage = "حالة أسعار التجزئة مطلوبة.")]
    public bool IsRetailEnabled { get; set; }

    [Display(Name = "كمية المخزون الأولية")]
    [Required(ErrorMessage = "كمية المخزون الأولية مطلوبة.")]
    [Range(0, 1000000, ErrorMessage = "يجب أن تكون كمية المخزون الأولية بين 0 و1,000,000.")]
    public int InitialStockQuantity { get; set; }

    [Display(Name = "عدد العبوات الأولي")]
    [Required(ErrorMessage = "عدد العبوات الأولي مطلوب.")]
    [Range(0, 1000000, ErrorMessage = "يجب أن يكون عدد العبوات الأولي بين 0 و1,000,000.")]
    public int InitialBottleCount { get; set; }

    public List<ProductRetailPriceInput> RetailPrices { get; set; } = [];

    [Display(Name = "الماركة")]
    public string? Brand { get; set; }

    [Display(Name = "صورة المنتج")]
    public IFormFile? Imagefile { get; set; }
    public string? Existingimage { get; set; }
}

public class ProductRetailPriceInput
{
    public int? Id { get; set; }

    [Display(Name = "حجم التجزئة")]
    [Required(ErrorMessage = "حجم التجزئة مطلوب.")]
    [Range(1, 1000000, ErrorMessage = "يجب أن يكون حجم التجزئة بين 1 و1,000,000 مل.")]
    public int SizeMl { get; set; }

    [Display(Name = "سعر التجزئة")]
    [Required(ErrorMessage = "سعر التجزئة مطلوب.")]
    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "يجب أن يكون سعر التجزئة بين 0.01 و1,000,000.")]
    public decimal Price { get; set; }

    [Display(Name = "نشط")]
    [Required(ErrorMessage = "حالة سعر التجزئة مطلوبة.")]
    public bool IsActive { get; set; } = true;
}
