using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private const int TopStalls = 5;
        private const int LowStockShown = 5;
        private const int ActivityShown = 8;
        public static readonly int[] TrendPeriods = { 40, 90, 180 };

        private readonly IDashboardInterface _dashboard;
        private readonly IStringLocalizer _L;
        private readonly BazaarOptions _bazaar;

        public DashboardController(IDashboardInterface dashboard, IStringLocalizerFactory localizerFactory, IOptions<BazaarOptions> bazaar)
        {
            _dashboard = dashboard;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(DashboardController).Assembly.GetName().Name!);
            _bazaar = bazaar.Value;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = _L["OperationsDashboard"].Value;
            ViewBag.Stats = await ComputeStatsAsync();
            return View();
        }

        /// <summary>
        /// Header cards. Attendance has no table yet, so its figures are null (shown as "—") rather than invented.
        /// Pending applications = new + under review.
        /// </summary>
        internal Task<DashboardStats> ComputeStatsAsync() => _dashboard.StatsAsync(_bazaar);

        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] int? days)
        {
            var period = NormalizePeriod(days);
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var stats = await ComputeStatsAsync();
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
                // modules without data yet: the page shows an empty state for these
                attendance = (object?)null,
                deptAttendance = (object?)null,
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
                lowStock = await LowStockAsync(),
                activity = await RecentActivityAsync()
            });
        }

        internal static int NormalizePeriod(int? days) => days != null && TrendPeriods.Contains(days.Value) ? days.Value : TrendPeriods[0];

        public record StallRank(string StallCode, string Category, decimal Revenue, int Quantity, int Pct);

        /// <summary>Best = highest revenue first; low = the remaining stalls, lowest first (a stall never appears in both lists).</summary>
        internal static (List<StallRank> Top, List<StallRank> Low) RankStalls(List<SalesAnalytics.StallPerformance> stalls)
        {
            var max = stalls.Select(s => s.Revenue).DefaultIfEmpty(0).Max();
            StallRank Rank(SalesAnalytics.StallPerformance s) =>
                new(s.StallCode, s.Category, s.Revenue, s.Quantity, max == 0 ? 0 : (int)Math.Round(s.Revenue * 100 / max, MidpointRounding.AwayFromZero));

            var top = stalls.Where(s => s.Revenue > 0)
                .OrderByDescending(s => s.Revenue).ThenBy(s => s.StallCode).Take(TopStalls).ToList();
            var low = stalls.Except(top)
                .OrderBy(s => s.Revenue).ThenBy(s => s.StallCode).Take(TopStalls).ToList();
            return (top.Select(Rank).ToList(), low.Select(Rank).ToList());
        }

        private async Task<List<object>> LowStockAsync()
        {
            var products = await _dashboard.LowStockAsync();
            return products
                .OrderBy(p => p.MinStock == 0 ? 0.0 : (double)p.Quantity / p.MinStock).ThenBy(p => p.Name)
                .Take(LowStockShown)
                .Select(p => (object)new
                {
                    code = p.ProductCode,
                    name = p.Name,
                    categoryName = p.Category?.Name ?? string.Empty,
                    stallCode = p.StallCode,
                    quantity = p.Quantity,
                    minStock = p.MinStock,
                    pct = p.MinStock == 0 ? 0 : Math.Clamp((int)Math.Round(p.Quantity * 100.0 / p.MinStock), 0, 100),
                    urgency = StockStatuses.Urgency(p.Quantity, p.MinStock)
                }).ToList();
        }

        /// <summary>Latest sales, manual stock changes and staff registrations, newest first. Texts are built in the page (localized).</summary>
        private async Task<List<object>> RecentActivityAsync()
        {
            return (await _dashboard.RecentActivityAsync(ActivityShown))
                .Select(a => (object)new
                {
                    type = a.Type,
                    at = Utc(a.At),
                    subject = a.Subject,
                    reference = a.Reference,
                    amount = a.Amount,
                    quantity = a.Quantity,
                    by = a.By
                }).ToList();
        }

        /// <summary>Dashboard report as CSV: summary figures, sales trend, category sales, stall performance and low stock.</summary>
        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] int? days)
        {
            var period = NormalizePeriod(days);
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var s = await ComputeStatsAsync();
            string Na(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? _L["NotAvailable"];

            var rows = new List<string?[]>
            {
                new string?[] { _L["OperationsDashboard"], today.ToString("yyyy-MM-dd") },
                Array.Empty<string?>(),
                new string?[] { _L["Summary"] },
                new string?[] { _L["Metric"], _L["Value"] },
                new string?[] { _L["TotalStalls"], Int(s.TotalStalls) },
                new string?[] { _L["OccupiedStalls"], Int(s.OccupiedStalls) },
                new string?[] { _L["VacantStalls"], Int(s.VacantStalls) },
                new string?[] { _L["OccupancyRate"], s.OccupancyPct + "%" },
                new string?[] { _L["TotalArtisans"], Na(s.TotalArtisans) },
                new string?[] { _L["TodaysSales"], Money(s.TodaySales) },
                new string?[] { _L["ChangeVsYesterday"], s.SalesChangePct == null ? _L["NotAvailable"] : s.SalesChangePct.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%" },
                new string?[] { _L["LowStockItems"], Int(s.LowStockItems) },
                new string?[] { _L["OutOfStock"], Int(s.OutOfStockItems) },
                new string?[] { _L["PendingApplications"], Na(s.PendingApplications) },
                new string?[] { _L["TotalStaff"], Int(s.TotalStaff) }
            };

            rows.Add(Array.Empty<string?>());
            rows.Add(new string?[] { _L["SalesTrend"], string.Format(_L["LastNDays"], period) });
            rows.Add(new string?[] { _L["Date"], _L["Revenue"] });
            foreach (var (day, revenue) in await _dashboard.DailyTrendAsync(today, period))
                rows.Add(new string?[] { day.ToString("yyyy-MM-dd"), Money(revenue) });

            rows.Add(Array.Empty<string?>());
            rows.Add(new string?[] { _L["CategoryWiseSales"], monthStart.ToString("yyyy-MM") });
            rows.Add(new string?[] { _L["Category"], _L["QtySold"], _L["Revenue"] });
            foreach (var (category, revenue, qty) in await _dashboard.ByCategoryAsync(monthStart, today.AddDays(1)))
                rows.Add(new string?[] { category, Int(qty), Money(revenue) });

            var (top, low) = RankStalls(await _dashboard.StallPerformanceAsync(monthStart, today.AddDays(1)));
            foreach (var (title, list) in new[] { (_L["BestPerformingStalls"].Value, top), (_L["LowPerformingStalls"].Value, low) })
            {
                rows.Add(Array.Empty<string?>());
                rows.Add(new string?[] { title, monthStart.ToString("yyyy-MM") });
                rows.Add(new string?[] { _L["Stall"], _L["Category"], _L["QtySold"], _L["Revenue"] });
                foreach (var r in list) rows.Add(new string?[] { r.StallCode, r.Category, Int(r.Quantity), Money(r.Revenue) });
            }

            rows.Add(Array.Empty<string?>());
            rows.Add(new string?[] { _L["LowStockAlerts"] });
            rows.Add(new string?[] { _L["ProductCode"], _L["ProductName"], _L["Category"], _L["Stall"], _L["CurrentQty"], _L["MinRequired"] });
            var lowStock = await _dashboard.LowStockAsync();
            foreach (var p in lowStock)
                rows.Add(new string?[] { p.ProductCode, p.Name, p.Category?.Name ?? string.Empty, p.StallCode, Int(p.Quantity), Int(p.MinStock) });

            return ExportExcel($"dashboard_report_{today:yyyyMMdd}.xls", rows);
        }
    }
}
