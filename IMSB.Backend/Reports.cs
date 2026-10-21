using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Reports : BackendBase, IReportsInterface
    {
        public Reports(IMSBContext context) : base(context) { }

        public Task<ProductCategory?> CategoryAsync(Guid categoryId) =>
            _context.ProductCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId);

        public Task<List<string>> BlocksAsync() => _context.Stalls.AsNoTracking().Select(s => s.Block).Distinct().OrderBy(b => b).ToListAsync();

        private IQueryable<SaleItem> Items(DateTime from, DateTime to, string? categoryName)
        {
            var items = SalesAnalytics.ItemsBetween(_context, from, to);
            return categoryName == null ? items : items.Where(i => i.CategoryName == categoryName);
        }

        public async Task<ReportKpis> KpisAsync(ReportPeriod period, ProductCategory? category)
        {
            var sales = await Items(period.From, period.To, category?.Name).SumAsync(i => i.LineTotal);
            // the same span of the previous month
            var prevStart = period.MonthStart.AddMonths(-1);
            var prevEnd = prevStart.AddDays(Math.Min(period.Days, DateTime.DaysInMonth(prevStart.Year, prevStart.Month)));
            var previous = await Items(prevStart, prevEnd, category?.Name).SumAsync(i => i.LineTotal);

            var (occupied, total) = OccupancyOn(await StallHistoryAsync(category?.Id), period.LastDay);

            var artisans = _context.Artisans.AsNoTracking().Where(a => a.Status == ArtisanStatuses.Active);
            var approved = _context.ArtisanApplications.AsNoTracking().Where(a => a.Status == ApplicationStatuses.Approved
                && a.DecidedAt >= period.From.ToUniversalTime() && a.DecidedAt < period.To.ToUniversalTime());
            var lowStock = _context.Products.AsNoTracking().Where(p => p.Quantity <= 0 || p.Quantity < p.MinStock);
            if (category != null)
            {
                artisans = artisans.Where(a => a.CategoryId == category.Id);
                approved = approved.Where(a => a.CategoryId == category.Id);
                lowStock = lowStock.Where(p => p.CategoryId == category.Id);
            }

            return new ReportKpis(
                Month: period.Key,
                TotalSales: sales,
                SalesChangePct: SalesAnalytics.PercentChange(sales, previous),
                OccupiedStalls: occupied,
                TotalStalls: total,
                OccupancyPct: total == 0 ? 0 : (int)Math.Round(occupied * 100.0 / total),
                ActiveArtisans: await artisans.CountAsync(),
                AvgAttendancePct: null,
                ApplicationsApproved: await approved.CountAsync(),
                LowStockItems: await lowStock.CountAsync());
        }

        public async Task<List<(DateTime Month, decimal Current, decimal Previous)>> SalesTrendAsync(ReportPeriod period, string? categoryName, int months)
        {
            var first = period.MonthStart.AddMonths(-(months - 1));
            var rows = await Items(first.AddYears(-1), period.To, categoryName)
                .Select(i => new { i.SalesEntry!.SoldAt, i.LineTotal }).ToListAsync();
            var byMonth = rows.GroupBy(r => new DateTime(r.SoldAt.Year, r.SoldAt.Month, 1)).ToDictionary(g => g.Key, g => g.Sum(r => r.LineTotal));
            decimal At(DateTime m) => byMonth.TryGetValue(m, out var v) ? v : 0m;
            return Enumerable.Range(0, months).Select(i => first.AddMonths(i))
                .Select(m => (m, At(m), At(m.AddYears(-1)))).ToList();
        }

        public Task<List<(string Category, decimal Revenue, int Quantity)>> ByCategoryAsync(DateTime from, DateTime to) =>
            SalesAnalytics.ByCategoryAsync(_context, from, to);

        private record StallHistory(DateTime CreatedOn, DateTime? AllottedDate, bool Occupied);

        private async Task<List<StallHistory>> StallHistoryAsync(Guid? categoryId)
        {
            var stalls = _context.Stalls.AsNoTracking();
            if (categoryId != null) stalls = stalls.Where(s => s.CategoryId == categoryId);
            return (await stalls.Select(s => new { s.CreatedAt, s.AllottedDate, s.ArtisanId }).ToListAsync())
                .Select(s => new StallHistory(DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc).ToLocalTime().Date, s.AllottedDate, s.ArtisanId != null))
                .ToList();
        }

        /// <summary>
        /// Stalls registered and occupied on a day, from the current allotments: a stall counts as occupied from its
        /// allotted date. (Earlier tenancies that have since ended are not stored, so past months can read low.)
        /// </summary>
        private static (int Occupied, int Total) OccupancyOn(List<StallHistory> stalls, DateTime day)
        {
            bool OccupiedOn(StallHistory s) => s.Occupied && s.AllottedDate != null && s.AllottedDate.Value.Date <= day;
            return (stalls.Count(OccupiedOn), stalls.Count(s => s.CreatedOn <= day || OccupiedOn(s)));
        }

        public async Task<List<(DateTime Month, int Occupied, int Total)>> OccupancyTrendAsync(ReportPeriod period, Guid? categoryId, int months)
        {
            var stalls = await StallHistoryAsync(categoryId);
            var first = period.MonthStart.AddMonths(-(months - 1));
            return Enumerable.Range(0, months).Select(i => first.AddMonths(i)).Select(m =>
            {
                // month end, or the period's last day for the selected (possibly current) month
                var day = m == period.MonthStart ? period.LastDay : m.AddMonths(1).AddDays(-1);
                var (occupied, total) = OccupancyOn(stalls, day);
                return (m, occupied, total);
            }).ToList();
        }

        public async Task<List<StallReportRow>> StallRowsAsync(ReportPeriod period, string? q, string? status, string? block, string? categoryId)
        {
            var stalls = await Stalls.ApplyFilters(_context.Stalls.AsNoTracking().Include(s => s.Category).Include(s => s.Artisan), q, status, categoryId, block)
                .OrderBy(s => s.Block).ThenBy(s => s.StallCode).ToListAsync();
            var sales = (await SalesAnalytics.ItemsBetween(_context, period.From, period.To).Select(i => new { i.StallCode, i.LineTotal }).ToListAsync())
                .GroupBy(i => i.StallCode.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.Sum(i => i.LineTotal));
            var stock = (await _context.Products.AsNoTracking().Select(p => new { p.StallCode, p.Quantity }).ToListAsync())
                .GroupBy(p => p.StallCode.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => (Products: g.Count(), Units: g.Sum(p => p.Quantity)));

            return stalls.Select(s =>
            {
                var key = s.StallCode.ToUpperInvariant();
                var days = 0;
                if (s.ArtisanId != null && s.AllottedDate != null)
                {
                    var start = s.AllottedDate.Value.Date > period.From ? s.AllottedDate.Value.Date : period.From;
                    days = Math.Max(0, (period.LastDay - start).Days + 1);
                }
                var st = stock.TryGetValue(key, out var x) ? x : (0, 0);
                return new StallReportRow(s.StallCode, s.Block, s.Artisan?.ArtisanCode, s.Artisan?.FullName, s.Category?.Name,
                    sales.TryGetValue(key, out var v) ? v : 0m, st.Item1, st.Item2, days, period.Days, s.Status);
            }).ToList();
        }

        public async Task<List<ArtisanReportRow>> ArtisanRowsAsync(ReportPeriod period, string? q, string? status, string? categoryId)
        {
            var artisans = _context.Artisans.AsNoTracking().Include(a => a.Category).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                artisans = artisans.Where(a => a.FullName.ToLower().Contains(term) || a.ArtisanCode.ToLower().Contains(term)
                    || a.District.ToLower().Contains(term) || _context.Stalls.Any(s => s.ArtisanId == a.Id && s.StallCode.ToLower().Contains(term)));
            }
            var st = status?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(st) && ArtisanStatuses.All.Contains(st)) artisans = artisans.Where(a => a.Status == st);
            if (Guid.TryParse(categoryId, out var catId)) artisans = artisans.Where(a => a.CategoryId == catId);
            var list = await artisans.OrderBy(a => a.FullName).ToListAsync();

            var stallOf = await _context.Stalls.AsNoTracking().Where(s => s.ArtisanId != null).ToDictionaryAsync(s => s.ArtisanId!.Value, s => s.StallCode);
            var items = await SalesAnalytics.ItemsBetween(_context, period.From, period.To).Select(i => new { i.StallCode, i.LineTotal, i.Quantity }).ToListAsync();
            var byStall = items.GroupBy(i => i.StallCode.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => (Revenue: g.Sum(i => i.LineTotal), Units: g.Sum(i => i.Quantity)));
            var products = (await _context.Products.AsNoTracking().Select(p => p.StallCode).ToListAsync())
                .GroupBy(c => c.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.Count());
            var allSales = items.Sum(i => i.LineTotal);

            var rows = list.Select(a =>
            {
                var stall = stallOf.TryGetValue(a.Id, out var s) ? s : null;
                var key = stall?.ToUpperInvariant() ?? "";
                var sold = stall != null && byStall.TryGetValue(key, out var x) ? x : (0m, 0);
                return (a, stall, sold.Item1, sold.Item2, stall != null && products.TryGetValue(key, out var n) ? n : 0);
            }).ToList();
            var best = rows.Select(r => r.Item3).DefaultIfEmpty(0).Max();

            return rows.Select(r => new ArtisanReportRow(r.a.ArtisanCode, r.a.FullName, r.a.District, r.a.Category?.Name, r.stall,
                r.Item3, r.Item4, r.Item5,
                allSales == 0 ? 0 : Math.Round(r.Item3 * 100 / allSales, 1),
                Rating(r.Item3, best), r.a.Status)).ToList();
        }

        /// <summary>1-5 compared with the best seller; 0 = no sales.</summary>
        public static int Rating(decimal sales, decimal best) =>
            sales <= 0 || best <= 0 ? 0 : Math.Clamp((int)Math.Ceiling(sales * 5 / best), 1, 5);

        public async Task<ApplicationReportData> ApplicationsAsync(ReportPeriod period, Guid? categoryId)
        {
            var query = _context.ArtisanApplications.AsNoTracking().Include(a => a.Category).AsQueryable();
            if (categoryId != null) query = query.Where(a => a.CategoryId == categoryId);
            var fromUtc = period.From.ToUniversalTime();
            var toUtc = period.To.ToUniversalTime();
            var submitted = await query.Where(a => a.SubmittedDate >= period.From && a.SubmittedDate < period.To).OrderBy(a => a.ApplicationNo).ToListAsync();
            var decided = await query.Where(a => a.DecidedAt >= fromUtc && a.DecidedAt < toUtc).Select(a => a.Status).ToListAsync();
            var open = await query.CountAsync(a => a.Status == ApplicationStatuses.Pending || a.Status == ApplicationStatuses.UnderReview);
            return new ApplicationReportData(submitted, decided, open);
        }

        public Task<List<Product>> InventoryAsync(Guid? categoryId)
        {
            var query = _context.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
            if (categoryId != null) query = query.Where(p => p.CategoryId == categoryId);
            return query.OrderBy(p => p.StallCode).ThenBy(p => p.Name).ToListAsync();
        }

        public async Task<List<SaleLine>> SaleLinesAsync(ReportPeriod period, string? categoryName) =>
            (await Items(period.From, period.To, categoryName)
                .Select(i => new { i.SalesEntryId, i.SalesEntry!.SoldAt, i.StallCode, i.Quantity, i.LineTotal }).ToListAsync())
            .Select(i => new SaleLine(i.SalesEntryId, i.SoldAt, i.StallCode, i.Quantity, i.LineTotal)).ToList();
    }
}
