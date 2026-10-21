using IMSB.Admin.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers
{
    /// <summary>
    /// Reports &amp; Analytics for a month (optionally one craft category): KPIs, monthly trends, the stall occupancy
    /// and artisan performance reports, and CSV / PDF exports of every section. Attendance is not recorded in the
    /// system yet, so its figures are reported as unavailable rather than invented.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        public const int TrendMonths = 12;
        public const int MonthChoices = 12;

        private readonly IReportsInterface _reports;
        private readonly IStringLocalizer _L;

        public ReportsController(IReportsInterface reports, IStringLocalizerFactory localizerFactory)
        {
            _reports = reports;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(ReportsController).Assembly.GetName().Name!);
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = _L["Reports"].Value;
            var period = ReportPeriod.ForMonth(null, DateTime.Today);
            ViewBag.Kpis = await KpisAsync(period, null);
            ViewBag.Months = MonthOptions(DateTime.Today);
            ViewBag.Categories = (await _reports.CategoriesAsync())
                .Select(c => new KeyValuePair<string, string>(c.Id.ToString(), c.Name)).ToList();
            return View();
        }

        /// <summary>The last twelve months, newest first, as (yyyy-MM, "October 2026" in the current culture).</summary>
        internal static List<KeyValuePair<string, string>> MonthOptions(DateTime today) =>
            Enumerable.Range(0, MonthChoices)
                .Select(i => new DateTime(today.Year, today.Month, 1).AddMonths(-i))
                .Select(m => new KeyValuePair<string, string>(m.ToString("yyyy-MM"), MonthLabel(m)))
                .ToList();

        private static string MonthLabel(DateTime m) => m.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

        // ── Data for the page ─────────────────────────────────────────

        /// <summary>KPIs and the four overview charts.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? month, [FromQuery] string? categoryId)
        {
            var period = ReportPeriod.ForMonth(month, DateTime.Today);
            var category = await CategoryAsync(categoryId);
            var trend = await SalesTrendAsync(period, category?.Name);
            var categories = await _reports.ByCategoryAsync(period.From, period.To);
            var occupancy = await OccupancyTrendAsync(period, category?.Id);

            return Ok(new
            {
                month = period.Key,
                monthLabel = MonthLabel(period.MonthStart),
                categoryId = category?.Id.ToString(),
                kpis = await KpisAsync(period, category),
                salesTrend = new
                {
                    labels = trend.Select(r => r.Month.ToString("yyyy-MM")),
                    current = trend.Select(r => r.Current),
                    previous = trend.Select(r => r.Previous)
                },
                categoryRevenue = new { labels = categories.Select(c => c.Category), values = categories.Select(c => c.Revenue) },
                // attendance is not recorded in the system yet: the page shows an empty state
                attendance = (object?)null,
                occupancyTrend = new
                {
                    labels = occupancy.Select(o => o.Month.ToString("yyyy-MM")),
                    occupied = occupancy.Select(o => o.Occupied),
                    vacant = occupancy.Select(o => o.Total - o.Occupied)
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> StallReport([FromQuery] string? month, [FromQuery] string? q, [FromQuery] string? status,
            [FromQuery] string? block, [FromQuery] string? categoryId)
        {
            var period = ReportPeriod.ForMonth(month, DateTime.Today);
            return Ok(new
            {
                month = period.Key,
                rows = await StallRowsAsync(period, q, status, block, categoryId),
                blocks = await _reports.BlocksAsync()
            });
        }

        [HttpGet]
        public async Task<IActionResult> ArtisanReport([FromQuery] string? month, [FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId)
        {
            var period = ReportPeriod.ForMonth(month, DateTime.Today);
            return Ok(new { month = period.Key, rows = await ArtisanRowsAsync(period, q, status, categoryId) });
        }

        // ── Calculations (in the Reports backend) ─────────────────────

        private async Task<ProductCategory?> CategoryAsync(string? categoryId) =>
            Guid.TryParse(categoryId, out var id) ? await _reports.CategoryAsync(id) : null;

        internal Task<ReportKpis> KpisAsync(ReportPeriod period, ProductCategory? category) => _reports.KpisAsync(period, category);

        /// <summary>Revenue for the twelve months ending with the selected one, and for the same months a year earlier.</summary>
        internal Task<List<(DateTime Month, decimal Current, decimal Previous)>> SalesTrendAsync(ReportPeriod period, string? categoryName) =>
            _reports.SalesTrendAsync(period, categoryName, TrendMonths);

        internal Task<List<(DateTime Month, int Occupied, int Total)>> OccupancyTrendAsync(ReportPeriod period, Guid? categoryId) =>
            _reports.OccupancyTrendAsync(period, categoryId, TrendMonths);

        internal Task<List<StallReportRow>> StallRowsAsync(ReportPeriod period, string? q, string? status, string? block, string? categoryId) =>
            _reports.StallRowsAsync(period, q, status, block, categoryId);

        /// <summary>
        /// Sales made at each artisan's stall in the period. Rating 1-5 compares an artisan's sales with the best seller
        /// in the list (0 = no sales); share is of all sales in the period.
        /// </summary>
        internal Task<List<ArtisanReportRow>> ArtisanRowsAsync(ReportPeriod period, string? q, string? status, string? categoryId) =>
            _reports.ArtisanRowsAsync(period, q, status, categoryId);

        internal static int Rating(decimal sales, decimal best) => Backend.Reports.Rating(sales, best);

        // ── Export ────────────────────────────────────────────────────

        /// <summary>
        /// /Reports/Export/{id}?format=csv|pdf. CSV downloads a file; pdf returns a print-ready page that opens the
        /// browser's print dialog (Save as PDF), which renders Tamil, Telugu and Hindi text correctly.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Export(string id, [FromQuery] string? format, [FromQuery] string? month, [FromQuery] string? categoryId,
            [FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? block)
        {
            var reportId = (id ?? "").Trim().ToLowerInvariant();
            if (!ReportIds.Every.Contains(reportId)) return NotFound();
            var period = ReportPeriod.ForMonth(month, DateTime.Today);
            var doc = await BuildAsync(reportId, period, categoryId, q, status, block);

            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                ViewData["Title"] = doc.Title;
                return View("Print", doc);
            }
            return Csv(ToCsv(doc), $"report_{reportId}_{period.MonthStart:yyyyMM}_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }

        internal async Task<ReportDocument> BuildAsync(string id, ReportPeriod period, string? categoryId, string? q, string? status, string? block)
        {
            var category = await CategoryAsync(categoryId);
            var tables = new List<ReportTable>();
            var all = id == ReportIds.All;

            if (all || id == ReportIds.Kpis) tables.Add(KpiTable(await KpisAsync(period, category)));
            if (all || id == ReportIds.SalesTrend) tables.Add(SalesTrendTable(await SalesTrendAsync(period, category?.Name)));
            if (all || id == ReportIds.CategoryRevenue) tables.Add(CategoryTable(await _reports.ByCategoryAsync(period.From, period.To)));
            if (all || id == ReportIds.OccupancyTrend) tables.Add(OccupancyTable(await OccupancyTrendAsync(period, category?.Id)));
            // the stall and artisan reports take their own filters; "all" uses only the page's category
            if (all || id == ReportIds.Stalls) tables.Add(StallTable(await StallRowsAsync(period, all ? null : q, all ? null : status, all ? null : block, categoryId)));
            if (all || id == ReportIds.Artisans) tables.Add(ArtisanTable(await ArtisanRowsAsync(period, all ? null : q, all ? null : status, categoryId)));
            if (all || id == ReportIds.Applications) tables.AddRange(ApplicationTables(await _reports.ApplicationsAsync(period, category?.Id)));
            if (all || id == ReportIds.Inventory) tables.Add(InventoryTable(await _reports.InventoryAsync(category?.Id)));
            if (all || id == ReportIds.Sales) tables.AddRange(SalesTables(period, await _reports.SaleLinesAsync(period, category?.Name)));

            var title = id switch
            {
                ReportIds.Kpis => _L["KeyPerformanceIndicators"].Value,
                ReportIds.SalesTrend => _L["MonthlySalesTrend"].Value,
                ReportIds.CategoryRevenue => _L["CategoryWiseRevenue"].Value,
                ReportIds.OccupancyTrend => _L["StallOccupancyOverTime"].Value,
                ReportIds.Stalls => _L["RptStallOccupancy"].Value,
                ReportIds.Artisans => _L["ArtisanPerformanceReport"].Value,
                ReportIds.Applications => _L["RptApplications"].Value,
                ReportIds.Inventory => _L["RptInventory"].Value,
                ReportIds.Sales => _L["RptSales"].Value,
                _ => _L["ReportsAnalytics"].Value
            };
            var periodText = MonthLabel(period.MonthStart) + (category != null ? " · " + category.Name : "");
            return new ReportDocument(title, periodText, User.Identity?.Name ?? "", DateTime.Now, tables);
        }

        private static string Pct(decimal v) => v.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        private static string Ym(DateTime m) => m.ToString("yyyy-MM");

        private ReportTable KpiTable(ReportKpis k) => new(_L["KeyPerformanceIndicators"], new[] { _L["Metric"].Value, _L["Value"].Value }, new()
        {
            new[] { _L["TotalMonthlySales"], Money(k.TotalSales) },
            new[] { _L["ChangeVsLastMonth"], k.SalesChangePct == null ? _L["NotAvailable"] : Pct(k.SalesChangePct.Value) },
            new[] { _L["StallOccupancy"], $"{k.OccupiedStalls}/{k.TotalStalls} ({k.OccupancyPct}%)" },
            new[] { _L["ActiveArtisans"], Int(k.ActiveArtisans) },
            new[] { _L["AvgAttendancePct"], k.AvgAttendancePct == null ? _L["NotAvailable"] : Pct(k.AvgAttendancePct.Value) },
            new[] { _L["ApplicationsApproved"], Int(k.ApplicationsApproved) },
            new[] { _L["LowStockItems"], Int(k.LowStockItems) },
        }) { NumericColumns = { 1 } };

        private ReportTable SalesTrendTable(List<(DateTime Month, decimal Current, decimal Previous)> rows) =>
            new(_L["MonthlySalesTrend"], new[] { _L["Month"].Value, _L["SalesThisYear"].Value, _L["SalesPreviousYear"].Value },
                rows.Select(r => new string?[] { Ym(r.Month), Money(r.Current), Money(r.Previous) }).ToList()) { NumericColumns = { 1, 2 } };

        private ReportTable CategoryTable(List<(string Category, decimal Revenue, int Quantity)> rows) =>
            new(_L["CategoryWiseRevenue"], new[] { _L["Category"].Value, _L["QtySold"].Value, _L["Revenue"].Value },
                rows.Select(r => new string?[] { r.Category, Int(r.Quantity), Money(r.Revenue) }).ToList()) { NumericColumns = { 1, 2 } };

        private ReportTable OccupancyTable(List<(DateTime Month, int Occupied, int Total)> rows) =>
            new(_L["StallOccupancyOverTime"], new[] { _L["Month"].Value, _L["Occupied"].Value, _L["Vacant"].Value, _L["TotalStalls"].Value, _L["OccupancyRate"].Value },
                rows.Select(r => new string?[] { Ym(r.Month), Int(r.Occupied), Int(r.Total - r.Occupied), Int(r.Total),
                    (r.Total == 0 ? 0 : (int)Math.Round(r.Occupied * 100.0 / r.Total)) + "%" }).ToList(),
                _L["OccupancyHistoryNote"]) { NumericColumns = { 1, 2, 3, 4 } };

        private ReportTable StallTable(List<StallReportRow> rows) =>
            new(_L["RptStallOccupancy"], new[] { _L["Stall"].Value, _L["BlockZone"].Value, _L["ArtisanID"].Value, _L["Artisan"].Value, _L["Category"].Value,
                    _L["MonthlySales"].Value, _L["Products"].Value, _L["StockItems"].Value, _L["OccupancyDays"].Value, _L["Status"].Value },
                rows.Select(r => new string?[] { r.StallCode, r.Block, r.ArtisanCode, r.ArtisanName, r.Category, Money(r.MonthlySales),
                    Int(r.Products), Int(r.StockUnits), $"{r.OccupancyDays}/{r.DaysInPeriod}", StatusLabels.Stall(_L, r.Status) }).ToList())
            { NumericColumns = { 5, 6, 7, 8 } };

        private ReportTable ArtisanTable(List<ArtisanReportRow> rows) =>
            new(_L["ArtisanPerformanceReport"], new[] { _L["ArtisanID"].Value, _L["Artisan"].Value, _L["District"].Value, _L["Category"].Value, _L["Stall"].Value,
                    _L["MonthlySales"].Value, _L["UnitsSoldLabel"].Value, _L["Products"].Value, _L["SalesShare"].Value, _L["Rating"].Value, _L["Status"].Value },
                rows.Select(r => new string?[] { r.ArtisanCode, r.Name, r.District, r.Category, r.StallCode, Money(r.MonthlySales), Int(r.UnitsSold),
                    Int(r.Products), Pct(r.SharePct), r.Rating == 0 ? "—" : $"{r.Rating}/5", StatusLabels.Artisan(_L, r.Status) }).ToList(),
                _L["RatingNote"]) { NumericColumns = { 5, 6, 7, 8, 9 } };

        /// <summary>Applications submitted in the period (with their current status), and the period's decisions.</summary>
        private List<ReportTable> ApplicationTables(ApplicationReportData data)
        {
            var summary = new ReportTable(_L["ApplicationsSummary"], new[] { _L["Metric"].Value, _L["Value"].Value }, new()
            {
                new[] { _L["SubmittedThisPeriod"], Int(data.Submitted.Count) },
                new[] { _L["ApprovedLabel"], Int(data.DecidedStatuses.Count(s => s == ApplicationStatuses.Approved)) },
                new[] { _L["RejectedLabel"], Int(data.DecidedStatuses.Count(s => s == ApplicationStatuses.Rejected)) },
                new[] { _L["StillOpen"], Int(data.StillOpen) },
            }) { NumericColumns = { 1 } };
            var list = new ReportTable(_L["RptApplications"], new[] { _L["AppID"].Value, _L["Applicant"].Value, _L["Category"].Value, _L["District"].Value,
                    _L["Submitted"].Value, _L["Status"].Value, _L["ArtisanID"].Value, _L["StallAssigned"].Value },
                data.Submitted.Select(a => new string?[] { a.ApplicationNo, a.FullName, a.Category?.Name, a.District, Date(a.SubmittedDate),
                    StatusLabels.Application(_L, a.Status), a.ArtisanCode, a.StallCode }).ToList());
            return new() { summary, list };
        }

        private ReportTable InventoryTable(List<Product> products) =>
            new(_L["RptInventory"], new[] { _L["ProductCode"].Value, _L["ProductName"].Value, _L["Category"].Value, _L["Stall"].Value,
                    _L["Quantity"].Value, _L["MinStock"].Value, _L["UnitPrice"].Value, _L["StockValue"].Value, _L["Status"].Value },
                products.Select(p => new string?[] { p.ProductCode, p.Name, p.Category?.Name, p.StallCode, Int(p.Quantity), Int(p.MinStock),
                    Money(p.UnitPrice), Money(p.UnitPrice * p.Quantity), StatusLabels.Stock(_L, p.StockStatus) }).ToList())
            { NumericColumns = { 4, 5, 6, 7 } };

        /// <summary>Day-by-day totals for the period and the stall-wise breakdown.</summary>
        private List<ReportTable> SalesTables(ReportPeriod period, List<SaleLine> items)
        {
            var byDay = items.ToLookup(i => i.SoldAt.Date);
            var daily = Enumerable.Range(0, period.Days).Select(i => period.From.AddDays(i)).Select(d =>
            {
                var day = byDay[d];
                return new string?[] { Date(d), Int(day.Select(i => i.SalesEntryId).Distinct().Count()), Int(day.Sum(i => i.Quantity)), Money(day.Sum(i => i.LineTotal)) };
            }).ToList();
            var byStall = items.GroupBy(i => i.StallCode.Trim().ToUpperInvariant())
                .Select(g => (Stall: g.Key, Quantity: g.Sum(i => i.Quantity), Revenue: g.Sum(i => i.LineTotal)))
                .OrderByDescending(g => g.Revenue).ThenBy(g => g.Stall)
                .Select(g => new string?[] { g.Stall, Int(g.Quantity), Money(g.Revenue) }).ToList();

            return new()
            {
                new ReportTable(_L["DailySales"], new[] { _L["Date"].Value, _L["Transactions"].Value, _L["ItemsSold"].Value, _L["Revenue"].Value }, daily) { NumericColumns = { 1, 2, 3 } },
                new ReportTable(_L["StallWiseSales"], new[] { _L["Stall"].Value, _L["QtySold"].Value, _L["Revenue"].Value }, byStall) { NumericColumns = { 1, 2 } }
            };
        }

        internal StringBuilder ToCsv(ReportDocument doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Row(doc.Title, doc.Period));
            sb.AppendLine(Row(_L["GeneratedOn"], doc.GeneratedAt.ToString("yyyy-MM-dd HH:mm"), doc.GeneratedBy));
            foreach (var t in doc.Tables)
            {
                sb.AppendLine();
                sb.AppendLine(Row(t.Title));
                sb.AppendLine(Row(t.Headers));
                foreach (var r in t.Rows) sb.AppendLine(Row(r));
                if (t.Rows.Count == 0) sb.AppendLine(Row(_L["NoDataForPeriod"]));
                if (t.Note != null) sb.AppendLine(Row(t.Note));
            }
            return sb;
        }
    }
}
