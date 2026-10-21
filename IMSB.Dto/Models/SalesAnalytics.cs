using Microsoft.EntityFrameworkCore;

namespace IMSB.Dto.Models
{
    /// <summary>Sales figures shared by the Sales page and the Dashboard. Day boundaries use the server's local date (SoldAt is local).</summary>
    public static class SalesAnalytics
    {
        /// <summary>Sale lines whose entry was sold in [from, to) (to is exclusive).</summary>
        public static IQueryable<SaleItem> ItemsBetween(IMSBContext ctx, DateTime from, DateTime to) =>
            ctx.SaleItems.AsNoTracking().Where(i => i.SalesEntry!.SoldAt >= from && i.SalesEntry.SoldAt < to);

        public static Task<decimal> RevenueBetweenAsync(IMSBContext ctx, DateTime from, DateTime to) =>
            ctx.SalesEntries.AsNoTracking().Where(e => e.SoldAt >= from && e.SoldAt < to).SumAsync(e => e.TotalAmount);

        /// <summary>Percent change from previous to current; null when there is nothing to compare with.</summary>
        public static decimal? PercentChange(decimal current, decimal previous) =>
            previous == 0 ? null : Math.Round((current - previous) / previous * 100, 1);

        public static async Task<SalesSummary> SummaryAsync(IMSBContext ctx, DateTime date, int totalStalls)
        {
            var day = date.Date;
            var entries = await ctx.SalesEntries.AsNoTracking()
                .Where(e => e.SoldAt >= day && e.SoldAt < day.AddDays(1))
                .Select(e => new { e.TotalAmount, e.TotalQuantity }).ToListAsync();
            var activeStalls = await ItemsBetween(ctx, day, day.AddDays(1)).Select(i => i.StallCode).Distinct().CountAsync();

            var total = entries.Sum(e => e.TotalAmount);
            var previousDay = await RevenueBetweenAsync(ctx, day.AddDays(-1), day);

            // month to date vs the same span of the previous month
            var monthStart = new DateTime(day.Year, day.Month, 1);
            var month = await RevenueBetweenAsync(ctx, monthStart, day.AddDays(1));
            var prevMonthStart = monthStart.AddMonths(-1);
            var prevMonthEnd = prevMonthStart.AddDays(Math.Min(day.Day, DateTime.DaysInMonth(prevMonthStart.Year, prevMonthStart.Month)));
            var previousMonth = await RevenueBetweenAsync(ctx, prevMonthStart, prevMonthEnd);

            return new SalesSummary(
                Date: day,
                TotalSales: total,
                ItemsSold: entries.Sum(e => e.TotalQuantity),
                Transactions: entries.Count,
                ActiveStalls: activeStalls,
                PreviousDaySales: previousDay,
                ChangeVsPreviousDay: PercentChange(total, previousDay),
                MonthSales: month,
                PreviousMonthSales: previousMonth,
                ChangeVsPreviousMonth: PercentChange(month, previousMonth),
                AverageTransaction: entries.Count == 0 ? 0 : Math.Round(total / entries.Count, 2),
                TotalStalls: totalStalls);
        }

        /// <summary>Revenue per day for the <paramref name="days"/> days ending on <paramref name="lastDay"/>, zero-filled.</summary>
        public static async Task<List<(DateTime Day, decimal Revenue)>> DailyTrendAsync(IMSBContext ctx, DateTime lastDay, int days)
        {
            var from = lastDay.Date.AddDays(-(days - 1));
            var rows = await ctx.SalesEntries.AsNoTracking()
                .Where(e => e.SoldAt >= from && e.SoldAt < lastDay.Date.AddDays(1))
                .Select(e => new { e.SoldAt, e.TotalAmount }).ToListAsync();
            var byDay = rows.GroupBy(r => r.SoldAt.Date).ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount));
            return Enumerable.Range(0, days).Select(i => from.AddDays(i))
                .Select(d => (d, byDay.TryGetValue(d, out var v) ? v : 0m)).ToList();
        }

        /// <summary>Revenue per category in [from, to): every configured category (in sort order, zeros included), then any snapshot-only names.</summary>
        public static async Task<List<(string Category, decimal Revenue, int Quantity)>> ByCategoryAsync(IMSBContext ctx, DateTime from, DateTime to)
        {
            var categories = await ctx.ProductCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => c.Name).ToListAsync();
            var items = await ItemsBetween(ctx, from, to).Select(i => new { i.CategoryName, i.LineTotal, i.Quantity }).ToListAsync();
            var grouped = items.GroupBy(i => i.CategoryName).ToDictionary(g => g.Key, g => (Revenue: g.Sum(i => i.LineTotal), Qty: g.Sum(i => i.Quantity)));
            var names = categories.Concat(grouped.Keys.Where(k => !string.IsNullOrEmpty(k) && !categories.Contains(k)).OrderBy(k => k));
            return names.Select(n => (n, grouped.TryGetValue(n, out var g) ? g.Revenue : 0m, grouped.TryGetValue(n, out var q) ? q.Qty : 0)).ToList();
        }

        /// <summary>Revenue per hour of the day. Covers 08:00-20:00 and widens to include any hour that had sales.</summary>
        public static async Task<List<(int Hour, decimal Revenue)>> HourlyAsync(IMSBContext ctx, DateTime date)
        {
            var day = date.Date;
            var rows = await ctx.SalesEntries.AsNoTracking()
                .Where(e => e.SoldAt >= day && e.SoldAt < day.AddDays(1))
                .Select(e => new { e.SoldAt, e.TotalAmount }).ToListAsync();
            var byHour = rows.GroupBy(r => r.SoldAt.Hour).ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount));
            var first = Math.Min(8, byHour.Keys.DefaultIfEmpty(8).Min());
            var last = Math.Max(20, byHour.Keys.DefaultIfEmpty(20).Max());
            return Enumerable.Range(first, last - first + 1).Select(h => (h, byHour.TryGetValue(h, out var v) ? v : 0m)).ToList();
        }

        public record StallPerformance(string StallCode, string Category, decimal Revenue, int Quantity);

        /// <summary>
        /// Revenue per stall in [from, to) for every stall that has inventory or sales, so occupied stalls with no
        /// sales show up as low performers. Category = the stall's most common product category.
        /// </summary>
        public static async Task<List<StallPerformance>> StallPerformanceAsync(IMSBContext ctx, DateTime from, DateTime to)
        {
            var items = await ItemsBetween(ctx, from, to).Select(i => new { i.StallCode, i.CategoryName, i.LineTotal, i.Quantity }).ToListAsync();
            var products = await ctx.Products.AsNoTracking().Select(p => new { p.StallCode, CategoryName = p.Category!.Name }).ToListAsync();

            var stalls = products.Select(p => p.StallCode.ToUpperInvariant()).Concat(items.Select(i => i.StallCode.ToUpperInvariant())).Distinct();
            return stalls.Select(s =>
            {
                var sold = items.Where(i => i.StallCode.ToUpperInvariant() == s).ToList();
                var category = products.Where(p => p.StallCode.ToUpperInvariant() == s).Select(p => p.CategoryName)
                    .Concat(sold.Select(i => i.CategoryName))
                    .Where(c => !string.IsNullOrEmpty(c))
                    .GroupBy(c => c).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).FirstOrDefault() ?? string.Empty;
                return new StallPerformance(s, category, sold.Sum(i => i.LineTotal), sold.Sum(i => i.Quantity));
            }).ToList();
        }
    }
}
