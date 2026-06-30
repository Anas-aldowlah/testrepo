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
        public Task<int> NextCounter()
        {
            int nextId = _context.Categories.Any() ? _context.Categories.Max(c => c.Id) + 1 : 1;
            return Task.FromResult(nextId);
        }
        public Task<Category?> GetCategoryByID(int id)
        {
           
            return Task.FromResult(_context.Categories.FirstOrDefault(c => c.Id == id));
        }
        
    }
}
