using System;

namespace YAGOT_2._0.Models
{
    public partial class PromotionCategory
    {
        public int PromotionId { get; set; }

        public int CategoryId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Promotion Promotion { get; set; } = null!;

        public virtual Category Category { get; set; } = null!;
    }
}
