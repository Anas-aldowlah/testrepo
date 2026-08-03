namespace YAGOT_2._0.Models
{
    public class StoreSettings
    {
        public int Id { get; set; }

        // ============================================
        // 1. الإعدادات العامة (General Settings)
        // ============================================
        public string? WhatsAppNumber { get; set; } = "";
        public string? ContactEmail { get; set; } = "";
        public string? InstagramLink { get; set; } = "";
        public string? TwitterLink { get; set; } = "";
        public string? TikTokLink { get; set; } = "";

        // ============================================
        // 2. إعدادات الواجهة (Storefront Settings)
        // ============================================
        public string? BrandsMarquee { get; set; } = "ياقوت, ديور, شانيل, الماجد للعود, توم فورد";
        
        public int? FeaturedCategoryId { get; set; }
        
        public string? HeroMarketingText { get; set; } = "حيث تولد الروائح كقطع فنية خالدة";
        
        public string? HeroMarketingDesc { get; set; } = "اكتشف عالماً من العطور الشرقية والغربية الأصيلة، المصنوعة بأجود المكونات لتجربة فاخرة تأسر حواسك من أول رشة.";

        // ============================================
        // 3. الشحن والدفع (Shipping & Payments)
        // ============================================
        public string? OmqiPaymentName { get; set; } = "العمقي";
        public string? OmqiAccountName { get; set; } = "مؤسسة ياقوت للتجارة";
        public string? OmqiAccountNumber { get; set; } = "";

        public string? BusairiPaymentName { get; set; } = "البسيري";
        public string? BusairiAccountName { get; set; } = "مؤسسة ياقوت للتجارة";
        public string? BusairiAccountNumber { get; set; } = "";

        public string? BinDowalPaymentName { get; set; } = "بن دول";
        public string? BinDowalAccountName { get; set; } = "مؤسسة ياقوت للتجارة";
        public string? BinDowalAccountNumber { get; set; } = "";

        public string? OtherPaymentName { get; set; } = "أخرى";
        public string? OtherPaymentInstructions { get; set; } = "سيتم التواصل معك هاتفياً للاتفاق على طريقة الدفع المناسبة";

        public List<PaymentMethodSetting> AdditionalPaymentMethods { get; set; } = new();
    }

    public class PaymentMethodSetting
    {
        public int? Id { get; set; }

        public string? Type { get; set; }

        public string? Name { get; set; }

        public string? AccountHolderName { get; set; }

        public string? AccountNumber { get; set; }

        public string? Instructions { get; set; }

        public bool IsActive { get; set; } = true;

        public bool Delete { get; set; }
    }
}
