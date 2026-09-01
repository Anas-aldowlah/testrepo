using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services
{

    public class ViewModels
    {
        public List<Category> Categories { get; set; } = [];
        public List<Product> Products { get; set; } = [];
        public List<Product> BestSellingProducts { get; set; } = [];
        public StoreSettings StoreSettings { get; set; } = new();
    }
}
