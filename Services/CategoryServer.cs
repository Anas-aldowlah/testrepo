using Microsoft.EntityFrameworkCore;
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

        public async Task<Category?> GetCategoryByID(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }
    }
}
