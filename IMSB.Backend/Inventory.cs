using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Inventory : BackendBase, IInventoryInterface
    {
        public Inventory(IMSBContext context) : base(context) { }

        public Task<List<Product>> ProductsAsync() => _context.Products.AsNoTracking().ToListAsync();

        public Task<List<Product>> FilterAsync(string? q, string? categoryId, string? status) =>
            ApplyFilters(_context.Products.AsNoTracking(), q, categoryId, status).OrderBy(p => p.Name).ToListAsync();

        // status is derived from quantity/min stock, so it is filtered on those columns
        private static IQueryable<Product> ApplyFilters(IQueryable<Product> query, string? q, string? categoryId, string? status)
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(term) || p.ProductCode.ToLower().Contains(term) || p.StallCode.ToLower().Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(categoryId) && Guid.TryParse(categoryId, out var catId))
            {
                query = query.Where(p => p.CategoryId == catId);
            }
            switch (status?.Trim().ToUpperInvariant())
            {
                case StockStatuses.OutOfStock: query = query.Where(p => p.Quantity <= 0); break;
                case StockStatuses.LowStock: query = query.Where(p => p.Quantity > 0 && p.Quantity < p.MinStock); break;
                case StockStatuses.InStock: query = query.Where(p => p.Quantity > 0 && p.Quantity >= p.MinStock); break;
            }
            return query;
        }

        public Task<int> CategoryCountAsync() => _context.ProductCategories.CountAsync();

        public Task<Dictionary<Guid, string>> CategoryNamesAsync() =>
            _context.ProductCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);

        public Task<List<StockMovement>> RecentMovementsAsync(int take) =>
            _context.StockMovements.AsNoTracking().OrderByDescending(m => m.CreatedAt).Take(take).ToListAsync();

        public Task<List<string>> ProductCodesAsync() => _context.Products.AsNoTracking().Select(p => p.ProductCode).ToListAsync();

        public Task<bool> ProductExistsAsync(string name, string stallCode, Guid? exceptId)
        {
            var n = name.Trim().ToLower();
            var stall = stallCode.Trim().ToUpper();
            return _context.Products.AnyAsync(p => p.Name.ToLower() == n && p.StallCode.ToUpper() == stall && p.Id != exceptId);
        }

        public Task<Product?> FindAsync(string productCode) => _context.Products.FirstOrDefaultAsync(p => p.ProductCode == productCode);

        public async Task AddAsync(Product product, StockMovement opening)
        {
            _context.StockMovements.Add(opening);
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
        }

        public Task UpdateAsync(Product product) => _context.SaveChangesAsync();

        public async Task UpdateStockAsync(Product product, StockMovement movement)
        {
            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Product product)
        {
            // keep the stock history; it still shows the code/name snapshot
            var history = await _context.StockMovements.Where(m => m.ProductId == product.Id).ToListAsync();
            history.ForEach(m => m.ProductId = null);
            // and its sales (sale items keep their own snapshot too)
            var sold = await _context.SaleItems.Where(i => i.ProductId == product.Id).ToListAsync();
            sold.ForEach(i => i.ProductId = null);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }
}
