using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Models.Admin
{
    public class PromotionListViewModel
    {
        public IEnumerable<PromotionListItemVM> Promotions { get; set; } = new List<PromotionListItemVM>();

        // Summary Cards Metrics
        public int TotalPromotions { get; set; }
        public int ActivePromotions { get; set; }
        public int UpcomingPromotions { get; set; }
        public int ExpiredPromotions { get; set; }
        public int DisabledPromotions { get; set; }

        // Filter & Search Parameters
        public string? Search { get; set; }
        public string? TypeFilter { get; set; }
        public string? StatusFilter { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalItems { get; set; }
    }

    public class PromotionListItemVM
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PromotionType { get; set; } = string.Empty;
        public string TargetSummary { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }

        public DiscountValueType SpendDiscountType { get; set; } = DiscountValueType.FixedAmount;
        public decimal? OfferPrice { get; set; }
        public int? BuyQuantity { get; set; }
        public int? FreeQuantity { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public int Priority { get; set; }
        public string? BannerImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public string StatusKey { get; set; } = "disabled";

        public string StatusBadgeText
        {
            get
            {
                return StatusKey switch
                {
                    "upcoming" => "قادم",
                    "expired" => "منتهي",
                    "active" => "نشط",
                    _ => "غير مفعل"
                };
            }
        }

        public string StatusBadgeClass
        {
            get
            {
                return StatusKey switch
                {
                    "upcoming" => "bg-info text-dark",
                    "expired" => "bg-danger text-white",
                    "active" => "bg-success text-white",
                    _ => "bg-secondary text-white"
                };
            }
        }
    }

    public class PromotionFormVM : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان العرض مطلوب")]
        [StringLength(200, ErrorMessage = "عنوان العرض يجب ألا يتجاوز 200 حرف")]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "نوع العرض مطلوب")]
        public string PromotionType { get; set; } = "Percentage"; // Percentage, FixedAmount, BuyXGetY, QuantityPrice, SpendAmount

        [Range(0, 1000000, ErrorMessage = "قيمة الخصم يجب ألا تكون بالسالب")]
        public decimal? DiscountValue { get; set; }

        public DiscountValueType SpendDiscountType { get; set; } = DiscountValueType.FixedAmount;

        [Range(0, 1000000, ErrorMessage = "الحد الأدنى للمبلغ يجب ألا يكون بالسالب")]
        public decimal? MinimumAmount { get; set; }

        [Range(0, 10000, ErrorMessage = "الكمية المطلوبة يجب ألا تكون بالسالب")]
        public int? BuyQuantity { get; set; }

        [Range(0, 10000, ErrorMessage = "الكمية المجانية يجب ألا تكون بالسالب")]
        public int? FreeQuantity { get; set; }

        [Range(0, 1000000, ErrorMessage = "سعر العرض يجب ألا يكون بالسالب")]
        public decimal? OfferPrice { get; set; }

        [Required(ErrorMessage = "تاريخ البداية مطلوب")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, 1000, ErrorMessage = "الأولوية يجب أن تكون صفر أو أكثر")]
        public int Priority { get; set; } = 10;

        public bool CanBeCombined { get; set; } = false;

        // Target Selection: All (Storewide), Products, Categories
        [Required(ErrorMessage = "يرجى تحديد النطاق المستهدف")]
        public string TargetType { get; set; } = "All";

        public List<int> SelectedProductIds { get; set; } = new List<int>();
        public List<int> SelectedCategoryIds { get; set; } = new List<int>();

        // Banner File Upload
        public IFormFile? BannerFile { get; set; }
        public string? ExistingBanner { get; set; }

        // Lists for selection UI
        public List<ProductSelectOption> AvailableProducts { get; set; } = new List<ProductSelectOption>();
        public List<CategorySelectOption> AvailableCategories { get; set; } = new List<CategorySelectOption>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var supportedPromotionTypes = new[] { "Percentage", "FixedAmount", "BuyXGetY", "QuantityPrice", "SpendAmount" };
            var supportedTargetTypes = new[] { "All", "Products", "Categories" };

            if (!supportedPromotionTypes.Contains(PromotionType, StringComparer.Ordinal))
            {
                yield return new ValidationResult("نوع العرض غير صالح", new[] { nameof(PromotionType) });
            }

            if (!supportedTargetTypes.Contains(TargetType, StringComparer.Ordinal))
            {
                yield return new ValidationResult("نطاق العرض غير صالح", new[] { nameof(TargetType) });
            }

            if (PromotionType == "Percentage" && (!DiscountValue.HasValue || DiscountValue.Value <= 0 || DiscountValue.Value > 100))
            {
                yield return new ValidationResult("نسبة الخصم يجب أن تكون أكبر من صفر وألا تتجاوز 100%", new[] { nameof(DiscountValue) });
            }

            if (PromotionType == "FixedAmount" && (!DiscountValue.HasValue || DiscountValue.Value <= 0))
            {
                yield return new ValidationResult("مبلغ الخصم يجب أن يكون أكبر من صفر", new[] { nameof(DiscountValue) });
            }

            if (PromotionType == "BuyXGetY")
            {
                if (!BuyQuantity.HasValue || BuyQuantity.Value <= 0)
                    yield return new ValidationResult("كمية الشراء يجب أن تكون أكبر من صفر", new[] { nameof(BuyQuantity) });
                if (!FreeQuantity.HasValue || FreeQuantity.Value <= 0)
                    yield return new ValidationResult("الكمية المجانية يجب أن تكون أكبر من صفر", new[] { nameof(FreeQuantity) });
            }

            if (PromotionType == "QuantityPrice")
            {
                if (!BuyQuantity.HasValue || BuyQuantity.Value <= 0)
                    yield return new ValidationResult("كمية العرض يجب أن تكون أكبر من صفر", new[] { nameof(BuyQuantity) });
                if (!OfferPrice.HasValue || OfferPrice.Value <= 0)
                    yield return new ValidationResult("سعر العرض يجب أن يكون أكبر من صفر", new[] { nameof(OfferPrice) });
            }

            if (PromotionType == "SpendAmount")
            {
                if (!MinimumAmount.HasValue || MinimumAmount.Value <= 0)
                    yield return new ValidationResult("الحد الأدنى للشراء يجب أن يكون أكبر من صفر", new[] { nameof(MinimumAmount) });
                if (!DiscountValue.HasValue || DiscountValue.Value <= 0)
                    yield return new ValidationResult("قيمة خصم سلة الشراء يجب أن تكون أكبر من صفر", new[] { nameof(DiscountValue) });
                if (SpendDiscountType == DiscountValueType.Percentage && DiscountValue.HasValue && DiscountValue.Value > 100)
                    yield return new ValidationResult("نسبة خصم سلة الشراء يجب ألا تتجاوز 100%", new[] { nameof(DiscountValue) });
            }

            if (EndDate <= StartDate)
            {
                yield return new ValidationResult("تاريخ النهاية يجب أن يكون بعد تاريخ البداية", new[] { nameof(EndDate) });
            }

            if (TargetType == "Products" && (SelectedProductIds == null || SelectedProductIds.Count == 0))
            {
                yield return new ValidationResult("يجب اختيار منتج واحد على الأقل عند اختيار المنتجات كنطاق مستهدف", new[] { nameof(SelectedProductIds) });
            }

            if (TargetType == "Categories" && (SelectedCategoryIds == null || SelectedCategoryIds.Count == 0))
            {
                yield return new ValidationResult("يجب اختيار تصنيف واحد على الأقل عند اختيار التصنيفات كنطاق مستهدف", new[] { nameof(SelectedCategoryIds) });
            }
        }
    }

    public class ProductSelectOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public decimal Price { get; set; }
        public string? CategoryName { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsSelected { get; set; }
    }

    public class CategorySelectOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int ProductCount { get; set; }
        public bool IsSelected { get; set; }
    }

    public class PromotionDetailsVM
    {
        public Promotion Promotion { get; set; } = null!;
        public DateTime StartDateLocal { get; set; }
        public DateTime EndDateLocal { get; set; }
        public string StatusKey { get; set; } = "disabled";
        public string TargetSummary { get; set; } = string.Empty;
        public List<Product> LinkedProducts { get; set; } = new List<Product>();
        public List<Category> LinkedCategories { get; set; } = new List<Category>();
    }
}
