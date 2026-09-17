using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models
{
    public enum DiscountValueType
    {
        FixedAmount = 0,
        Percentage = 1
    }

    public partial class Promotion
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public string PromotionType { get; set; } = null!;

        public string TargetType { get; set; } = "All";

        public decimal DiscountValue { get; set; }

        public DiscountValueType SpendDiscountType { get; set; } = DiscountValueType.FixedAmount;

        public decimal? MinimumAmount { get; set; }

        public int? BuyQuantity { get; set; }

        public int? FreeQuantity { get; set; }

        public decimal? OfferPrice { get; set; }

        public DateTimeOffset StartDate { get; set; }

        public DateTimeOffset EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        public int Priority { get; set; } = 10;

        public bool CanBeCombined { get; set; } = false;

        public string? BannerImage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<PromotionProduct> PromotionProducts { get; set; } = new List<PromotionProduct>();

        public virtual ICollection<PromotionCategory> PromotionCategories { get; set; } = new List<PromotionCategory>();
    }
}
