using System;

namespace YAGOT_2._0.Models
{
    public partial class PromotionProduct
    {
        public int PromotionId { get; set; }

        public int ProductId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Promotion Promotion { get; set; } = null!;

        public virtual Product Product { get; set; } = null!;
    }
}
