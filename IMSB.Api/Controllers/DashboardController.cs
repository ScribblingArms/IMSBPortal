using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>The operations dashboard.</summary>
    [Authorize(Roles = "Admin")]
    public class DashboardController : ApiControllerBase
    {
        private const int TopStalls = 5;
        private const int LowStockShown = 5;
        private const int ActivityShown = 8;
        public static readonly int[] TrendPeriods = { 40, 90, 180 };

        private readonly IDashboardInterface _dashboard;
        private readonly BazaarOptions _bazaar;

        public DashboardController(IDashboardInterface dashboard, IOptions<BazaarOptions> bazaar)
        {
            _dashboard = dashboard;
            _bazaar = bazaar.Value;
        }

        public record StallRank(string StallCode, string Category, decimal Revenue, int Quantity, int Pct);

        /// <summary>Header cards, sales trend (days = 40, 90 or 180), occupancy, category sales, stall ranking, low stock and recent activity.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] int? days)
        {
            var period = days != null && TrendPeriods.Contains(days.Value) ? days.Value : TrendPeriods[0];
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var stats = await _dashboard.StatsAsync(_bazaar);
            var apps = await _dashboard.ApplicationStatsAsync();
            var trend = await _dashboard.DailyTrendAsync(today, period);
            var categories = await _dashboard.ByCategoryAsync(monthStart, today.AddDays(1));
            var (top, low) = RankStalls(await _dashboard.StallPerformanceAsync(monthStart, today.AddDays(1)));

            return Ok(new
            {
                stats,
                period,
                // the "Approved" widget counts applications cleared this month
                applications = new { approved = apps.ApprovedThisMonth, review = apps.UnderReview, pending = apps.Pending },
                salesTrend = new { labels = trend.Select(t => t.Day.ToString("yyyy-MM-dd")), values = trend.Select(t => t.Revenue) },
                occupancy = new { occupied = stats.OccupiedStalls, vacant = stats.VacantStalls },
                categorySales = new
                {
                    month = monthStart.ToString("yyyy-MM"),
                    labels = categories.Select(c => c.Category),
                    values = categories.Select(c => c.Revenue)
                },
                topStalls = top,
                lowStalls = low,
                lowStock = (await _dashboard.LowStockAsync())
                    .OrderBy(p => p.MinStock == 0 ? 0.0 : (double)p.Quantity / p.MinStock).ThenBy(p => p.Name)
                    .Take(LowStockShown)
                    .Select(p => new
                    {
                        code = p.ProductCode,
                        name = p.Name,
                        categoryName = p.Category?.Name ?? string.Empty,
                        stallCode = p.StallCode,
                        quantity = p.Quantity,
                        minStock = p.MinStock,
                        pct = p.MinStock == 0 ? 0 : Math.Clamp((int)Math.Round(p.Quantity * 100.0 / p.MinStock), 0, 100),
                        urgency = StockStatuses.Urgency(p.Quantity, p.MinStock)
                    }),
                // latest sales, manual stock changes and staff registrations, newest first
                activity = (await _dashboard.RecentActivityAsync(ActivityShown))
                    .Select(a => new { type = a.Type, at = Utc(a.At), subject = a.Subject, reference = a.Reference, amount = a.Amount, quantity = a.Quantity, by = a.By })
            });
        }

        /// <summary>The applications behind an overview widget: APPROVED (this month), UNDER_REVIEW or PENDING.</summary>
        [HttpGet("applications")]
        public async Task<IActionResult> Applications([FromQuery] string? status)
        {
            var st = status?.Trim().ToUpperInvariant();
            if (st is not (ApplicationStatuses.Approved or ApplicationStatuses.UnderReview or ApplicationStatuses.Pending))
                return Invalid("status", "ValStatus");
            return Ok((await _dashboard.ApplicationsAsync(st)).Select(a => a.ToListItem()));
        }

        /// <summary>Best = highest revenue first; low = the remaining stalls, lowest first (a stall never appears in both lists).</summary>
        private static (List<StallRank> Top, List<StallRank> Low) RankStalls(List<SalesAnalytics.StallPerformance> stalls)
        {
            var max = stalls.Select(s => s.Revenue).DefaultIfEmpty(0).Max();
            StallRank Rank(SalesAnalytics.StallPerformance s) =>
                new(s.StallCode, s.Category, s.Revenue, s.Quantity, max == 0 ? 0 : (int)Math.Round(s.Revenue * 100 / max, MidpointRounding.AwayFromZero));

            var top = stalls.Where(s => s.Revenue > 0).OrderByDescending(s => s.Revenue).ThenBy(s => s.StallCode).Take(TopStalls).ToList();
            var low = stalls.Except(top).OrderBy(s => s.Revenue).ThenBy(s => s.StallCode).Take(TopStalls).ToList();
            return (top.Select(Rank).ToList(), low.Select(Rank).ToList());
        }
    }
}
