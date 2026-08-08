using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services
{
    
    public class CategoryServer
    {
        private readonly NeondbContext _context;
        public CategoryServer(NeondbContext context)
        {
            _context = context;
        }
        public Task<Category?> GetCategoryByID(int id)
        {
           
            return Task.FromResult(_context.Categories.FirstOrDefault(c => c.Id == id));
        }
        
    }
}
