using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace IMSB.Backend
{
    public class Sales : BackendBase, ISalesInterface
    {
        public Sales(IMSBContext context) : base(context) { }

        public async Task<int> TotalStallsAsync(BazaarOptions bazaar) => (await BazaarStats.StallsAsync(_context, bazaar)).Total;

        public Task<SalesSummary> SummaryAsync(DateTime day, int totalStalls) => SalesAnalytics.SummaryAsync(_context, day, totalStalls);

        public Task<List<(string Category, decimal Revenue, int Quantity)>> ByCategoryAsync(DateTime from, DateTime to) =>
            SalesAnalytics.ByCategoryAsync(_context, from, to);

        public Task<List<(int Hour, decimal Revenue)>> HourlyAsync(DateTime day) => SalesAnalytics.HourlyAsync(_context, day);

        public Task<List<SalesEntry>> EntriesOnAsync(DateTime day, string? stall)
        {
            var query = _context.SalesEntries.AsNoTracking().Include(e => e.Items)
                .Where(e => e.SoldAt >= day && e.SoldAt < day.AddDays(1));
            if (!string.IsNullOrEmpty(stall)) query = query.Where(e => e.Items.Any(i => i.StallCode == stall));
            return query.OrderByDescending(e => e.SoldAt).ThenByDescending(e => e.EntryNo).ToListAsync();
        }

        public Task<SalesEntry?> GetEntryAsync(string entryNo) =>
            _context.SalesEntries.AsNoTracking().Include(e => e.Items).FirstOrDefaultAsync(e => e.EntryNo == entryNo);

        public Task<SalesEntry?> FindEntryAsync(string entryNo) =>
            _context.SalesEntries.Include(e => e.Items).FirstOrDefaultAsync(e => e.EntryNo == entryNo);

        public Task<List<Product>> ProductsAsync() =>
            _context.Products.AsNoTracking().Include(p => p.Category).OrderBy(p => p.Name).ToListAsync();

        public Task<Dictionary<string, Product>> ProductsByCodeAsync(List<string> codes) =>
            _context.Products.Include(p => p.Category).Where(p => codes.Contains(p.ProductCode)).ToDictionaryAsync(p => p.ProductCode);

        public Task<Dictionary<Guid, Product>> ProductsByIdAsync(IEnumerable<Guid> ids)
        {
            var list = ids.ToList();
            return _context.Products.Where(p => list.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        }

        public Task<List<string>> SaleStallsAsync() => _context.SaleItems.AsNoTracking().Select(i => i.StallCode).Distinct().ToListAsync();

        public async Task<List<ProductReportRow>> ProductReportRowsAsync(string? q, string? categoryId, string? stall, DateTime? fromDay, DateTime? toDay)
        {
            var query = _context.SaleItems.AsNoTracking().AsQueryable();
            if (fromDay != null) query = query.Where(i => i.SalesEntry!.SoldAt >= fromDay.Value);
            if (toDay != null) query = query.Where(i => i.SalesEntry!.SoldAt < toDay.Value.AddDays(1));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(i => i.ProductName.ToLower().Contains(term) || i.ProductCode.ToLower().Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(categoryId) && Guid.TryParse(categoryId, out var catId))
                query = query.Where(i => i.CategoryId == catId);
            if (!string.IsNullOrWhiteSpace(stall))
            {
                var s = stall.Trim().ToUpperInvariant();
                query = query.Where(i => i.StallCode == s);
            }

            var items = await query.Select(i => new
            {
                i.ProductId, i.ProductCode, i.ProductName, i.CategoryName, i.StallCode, i.Quantity, i.LineTotal, i.SalesEntry!.SoldAt
            }).ToListAsync();
            var productIds = items.Where(i => i.ProductId != null).Select(i => i.ProductId!.Value).Distinct().ToList();
            var stock = await _context.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => (p.Quantity, p.MinStock));

            return items
                .GroupBy(i => new { i.ProductCode, Day = i.SoldAt.Date })
                .Select(g =>
                {
                    var latest = g.OrderByDescending(i => i.SoldAt).First();
                    var qty = g.Sum(i => i.Quantity);
                    var revenue = g.Sum(i => i.LineTotal);
                    var hasStock = latest.ProductId != null && stock.ContainsKey(latest.ProductId.Value);
                    return new ProductReportRow(
                        latest.ProductCode, latest.ProductName, latest.CategoryName, latest.StallCode, qty,
                        qty == 0 ? 0 : Math.Round(revenue / qty, 2), revenue,
                        hasStock ? stock[latest.ProductId!.Value].Quantity : null,
                        hasStock ? stock[latest.ProductId!.Value].MinStock : null,
                        g.Key.Day.ToString("yyyy-MM-dd"));
                })
                .OrderByDescending(r => r.Date).ThenByDescending(r => r.Revenue).ThenBy(r => r.Name)
                .ToList();
        }

        public async Task<(List<StockMovement> Movements, Dictionary<Guid, Product> Products)> DeductionLogAsync(DateTime? day, int take)
        {
            var query = _context.StockMovements.AsNoTracking()
                .Where(m => m.MovementType == StockMovementTypes.Sale || m.MovementType == StockMovementTypes.SaleReversal);
            if (day != null) query = query.Where(m => m.MovementDate == day.Value);
            var movements = await query.OrderByDescending(m => m.CreatedAt).Take(take).ToListAsync();
            var productIds = movements.Where(m => m.ProductId != null).Select(m => m.ProductId!.Value).Distinct().ToList();
            var products = await _context.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            return (movements, products);
        }

        public async Task<List<SaleExportRow>> ExportRowsAsync(DateTime from, DateTime to, string? stall)
        {
            var query = SalesAnalytics.ItemsBetween(_context, from, to);
            if (!string.IsNullOrWhiteSpace(stall))
            {
                var s = stall.Trim().ToUpperInvariant();
                query = query.Where(i => i.StallCode == s);
            }
            return (await query.Select(i => new
            {
                i.SalesEntry!.EntryNo, i.SalesEntry.SoldAt, i.SalesEntry.CreatedBy, i.ProductCode, i.ProductName, i.CategoryName, i.StallCode, i.Quantity, i.UnitPrice, i.LineTotal
            }).ToListAsync())
            .Select(i => new SaleExportRow(i.EntryNo, i.SoldAt, i.CreatedBy, i.ProductCode, i.ProductName, i.CategoryName, i.StallCode, i.Quantity, i.UnitPrice, i.LineTotal))
            .ToList();
        }

        public async Task<string> NextEntryNoAsync()
        {
            var existing = await _context.SalesEntries.AsNoTracking().Select(e => e.EntryNo).ToListAsync();
            var next = existing
                .Select(c => Regex.Match(c, @"(\d+)$"))
                .Select(m => m.Success && int.TryParse(m.Value, out var n) ? n : 0)
                .DefaultIfEmpty(0).Max() + 1;
            return $"SAL-{next:D6}";
        }

        public void Add(SalesEntry entry) => _context.SalesEntries.Add(entry);

        public void Remove(SalesEntry entry) => _context.SalesEntries.Remove(entry);

        // new lines already carry a Guid key, so they must be marked as inserts explicitly;
        // found only through the navigation EF would treat them as existing rows and UPDATE them
        public void AddItems(IEnumerable<SaleItem> items) => _context.SaleItems.AddRange(items);

        public void RemoveItems(IEnumerable<SaleItem> items) => _context.SaleItems.RemoveRange(items);

        public void AddMovement(StockMovement movement) => _context.StockMovements.Add(movement);
    }
}
