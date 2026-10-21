using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    /// <summary>Shared by the backend classes: the request's IMSBContext and lookups several pages need.</summary>
    public abstract class BackendBase
    {
        protected readonly IMSBContext _context;

        protected BackendBase(IMSBContext context)
        {
            _context = context;
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        /// <summary>Product / craft categories in display order.</summary>
        public Task<List<ProductCategory>> CategoriesAsync() =>
            _context.ProductCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();

        public Task<bool> CategoryExistsAsync(Guid categoryId) =>
            _context.ProductCategories.AnyAsync(c => c.Id == categoryId);

        /// <summary>Rows per value, counted by the database (one GROUP BY instead of loading every row).</summary>
        internal static Task<Dictionary<string, int>> CountByAsync(IQueryable<string> values) =>
            values.GroupBy(v => v).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
    }
}
