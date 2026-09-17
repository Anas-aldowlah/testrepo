using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class Product
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

    public string? Imageurl { get; set; }

    public DateTime? Createdat { get; set; }

    public string? Brand { get; set; }

    public int TotalSold { get; set; } = 0;

    public DateTime? SalesLastUpdatedAt { get; set; }

    public virtual ICollection<Cartitem> Cartitems { get; set; } = new List<Cartitem>();

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();

    public virtual ICollection<ProductRetailPrice> RetailPrices { get; set; } = new List<ProductRetailPrice>();

    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

    // Dynamic Runtime Promotion Properties (Not Mapped to DB)
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HasPromotion { get; set; } = false;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int? PromotionId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PromotionTitle { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PromotionType { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal OriginalPrice => Price;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal DiscountAmount { get; set; } = 0;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal FinalPrice => Price - DiscountAmount;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal? DiscountPercentage => Price > 0 && DiscountAmount > 0 ? Math.Round((DiscountAmount / Price) * 100m, 2) : null;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int? FreeQuantity { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int? BuyQuantity { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PromotionDescription { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PromotionBannerImage { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal? PromoDiscountValue { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal? OfferPrice { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal? MinimumAmount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTimeOffset? PromotionStartDate { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTimeOffset? PromotionEndDate { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int? PromotionPriority { get; set; }
}
