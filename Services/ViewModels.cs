using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services
{

    public class ViewModels
    {
        public List<Category> Categories { get; set; } = [];
        public List<Product> NewArrivals { get; set; } = [];
        public List<Product> HeroSlides { get; set; } = [];
        public List<Product> BestSellingProducts { get; set; } = [];
        public List<Product> PromoProducts { get; set; } = [];
        public List<string> Brands { get; set; } = [];
        public StoreSettings StoreSettings { get; set; } = new();
    }
}
