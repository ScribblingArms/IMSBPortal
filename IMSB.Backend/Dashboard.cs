using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Dashboard : BackendBase, IDashboardInterface
    {
        public Dashboard(IMSBContext context) : base(context) { }

        public async Task<DashboardStats> StatsAsync(BazaarOptions bazaar)
        {
            var today = DateTime.Today;
            var (total, occupied) = await BazaarStats.StallsAsync(_context, bazaar);
            var todaySales = await SalesAnalytics.RevenueBetweenAsync(_context, today, today.AddDays(1));
            var yesterday = await SalesAnalytics.RevenueBetweenAsync(_context, today.AddDays(-1), today);
            var apps = await Applications.StatsAsync(_context);

            return new DashboardStats(
                TotalStalls: total,
                OccupiedStalls: occupied,
                VacantStalls: total - occupied,
                OccupancyPct: total == 0 ? 0 : (int)Math.Round(occupied * 100.0 / total),
                TotalArtisans: await _context.Artisans.CountAsync(),
                TodaySales: todaySales,
                SalesChangePct: SalesAnalytics.PercentChange(todaySales, yesterday),
                // same rule as StockStatuses.For, counted by the database
                LowStockItems: await _context.Products.CountAsync(p => p.Quantity <= 0 || p.Quantity < p.MinStock),
                OutOfStockItems: await _context.Products.CountAsync(p => p.Quantity <= 0),
                PendingApplications: apps.Pending + apps.UnderReview,
                TotalStaff: await _context.Staff.CountAsync(),
                ActiveStaff: await _context.Staff.CountAsync(s => s.Status == "ACTIVE"));
        }

        public Task<ApplicationStats> ApplicationStatsAsync() => Applications.StatsAsync(_context);

        public Task<List<(DateTime Day, decimal Revenue)>> DailyTrendAsync(DateTime lastDay, int days) =>
            SalesAnalytics.DailyTrendAsync(_context, lastDay, days);

        public Task<List<(string Category, decimal Revenue, int Quantity)>> ByCategoryAsync(DateTime from, DateTime to) =>
            SalesAnalytics.ByCategoryAsync(_context, from, to);

        public Task<List<SalesAnalytics.StallPerformance>> StallPerformanceAsync(DateTime from, DateTime to) =>
            SalesAnalytics.StallPerformanceAsync(_context, from, to);

        public Task<List<Product>> LowStockAsync() =>
            _context.Products.AsNoTracking().Include(p => p.Category)
                .Where(p => p.Quantity < p.MinStock || p.Quantity <= 0).OrderBy(p => p.Name).ToListAsync();

        public async Task<List<DashboardActivity>> RecentActivityAsync(int take)
        {
            var sales = (await _context.SalesEntries.AsNoTracking().OrderByDescending(e => e.CreatedAt).Take(take)
                    .Select(e => new { e.CreatedAt, e.EntryNo, e.TotalAmount, e.TotalQuantity, e.CreatedBy }).ToListAsync())
                .Select(e => new DashboardActivity("SALE", e.CreatedAt, e.EntryNo, null, e.TotalAmount, e.TotalQuantity, e.CreatedBy));

            // sale deductions are already represented by the sale itself
            var stock = (await _context.StockMovements.AsNoTracking()
                    .Where(m => m.MovementType != StockMovementTypes.Sale && m.MovementType != StockMovementTypes.SaleReversal)
                    .OrderByDescending(m => m.CreatedAt).Take(take)
                    .Select(m => new { m.CreatedAt, m.MovementType, m.ProductName, m.QuantityAfter, m.Quantity, m.CreatedBy }).ToListAsync())
                .Select(m => new DashboardActivity("STOCK_" + m.MovementType, m.CreatedAt, m.ProductName, null, null,
                    m.MovementType == StockMovementTypes.Correction ? m.QuantityAfter : m.Quantity, m.CreatedBy));

            var staff = (await _context.Staff.AsNoTracking().OrderByDescending(s => s.CreatedAt).Take(take)
                    .Select(s => new { s.CreatedAt, s.FullName, s.EmployeeId, s.CreatedBy }).ToListAsync())
                .Select(s => new DashboardActivity("STAFF_ADDED", s.CreatedAt, s.FullName, s.EmployeeId, null, null, s.CreatedBy));

            return sales.Concat(stock).Concat(staff).OrderByDescending(a => a.At).Take(take).ToList();
        }
    }
}
