using System.Globalization;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Api.Controllers
{
    /// <summary>Reports &amp; analytics. month = yyyy-MM (default this month).</summary>
    [Authorize(Roles = "Admin")]
    public class ReportsController : ApiControllerBase
    {
        public const int TrendMonths = 12;

        private readonly IReportsInterface _reports;
        private readonly IAttendanceInterface _attendance;

        public ReportsController(IReportsInterface reports, IAttendanceInterface attendance)
        {
            _reports = reports;
            _attendance = attendance;
        }

        /// <summary>KPIs and the overview charts, optionally for one category.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? month, [FromQuery] string? categoryId)
        {
            var period = ReportPeriod.ForMonth(month, DateTime.Today);
            var category = Guid.TryParse(categoryId, out var id) ? await _reports.CategoryAsync(id) : null;
            var trend = await _reports.SalesTrendAsync(period, category?.Name, TrendMonths);
            var categories = await _reports.ByCategoryAsync(period.From, period.To);
            var occupancy = await _reports.OccupancyTrendAsync(period, category?.Id, TrendMonths);

            return Ok(new
            {
                month = period.Key,
                monthLabel = period.MonthStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                categoryId = category?.Id.ToString(),
                kpis = await _reports.KpisAsync(period, category),
                salesTrend = new
                {
                    labels = trend.Select(r => r.Month.ToString("yyyy-MM")),
                    current = trend.Select(r => r.Current),
                    previous = trend.Select(r => r.Previous)
                },
                categoryRevenue = new { labels = categories.Select(c => c.Category), values = categories.Select(c => c.Revenue) },
                occupancyTrend = new
                {
                    labels = occupancy.Select(o => o.Month.ToString("yyyy-MM")),
                    occupied = occupancy.Select(o => o.Occupied),
                    vacant = occupancy.Select(o => o.Total - o.Occupied)
                },
                categories = (await _reports.CategoriesAsync()).Select(c => new { id = c.Id.ToString(), name = c.Name })
            });
        }

        [HttpGet("stalls")]
        public async Task<IActionResult> Stalls([FromQuery] string? month, [FromQuery] string? q, [FromQuery] string? status,
            [FromQuery] string? block, [FromQuery] string? categoryId)
        {
            var period = ReportPeriod.ForMonth(month, DateTime.Today);
            return Ok(new
            {
                month = period.Key,
                rows = await _reports.StallRowsAsync(period, q, status, block, categoryId),
                blocks = await _reports.BlocksAsync()
            });
        }

        /// <summary>Sales at each artisan's stall: share of all sales and a 1-5 rating against the best seller.</summary>
        [HttpGet("artisans")]
        public async Task<IActionResult> Artisans([FromQuery] string? month, [FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId)
        {
            var period = ReportPeriod.ForMonth(month, DateTime.Today);
            return Ok(new { month = period.Key, rows = await _reports.ArtisanRowsAsync(period, q, status, categoryId) });
        }

        /// <summary>Days worked per staff member; type = designation. Year within the last ten years, default this month.</summary>
        [HttpGet("monthly-attendance")]
        public async Task<IActionResult> MonthlyAttendance([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? type) =>
            TryResolveAttendanceMonth(year, month, out var start)
                ? Ok(await _attendance.MonthlyAttendanceReportAsync(start, type))
                : Invalid("period", "ValDateInvalid");

        /// <summary>Salary prorated by days worked; type = department, q matches id, name, department or designation.</summary>
        [HttpGet("salary")]
        public async Task<IActionResult> Salary([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? type, [FromQuery] string? q) =>
            TryResolveAttendanceMonth(year, month, out var start)
                ? Ok(await _attendance.SalaryReportAsync(start, type, q))
                : Invalid("period", "ValDateInvalid");

        /// <summary>The day's head count (default today): artisans, then staff per department with who attended.</summary>
        [HttpGet("abstract")]
        public async Task<IActionResult> Abstract([FromQuery] string? date)
        {
            var value = date ?? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return Invalid("date", "ValDateInvalid");
            return Ok(await _attendance.AbstractReportAsync(day, L["StaffHR"].Value, L["Artisans"].Value));
        }

        private static bool TryResolveAttendanceMonth(int? year, int? month, out DateTime start)
        {
            var today = DateTime.Today;
            var selectedYear = year ?? today.Year;
            var selectedMonth = month ?? today.Month;
            start = default;
            if (selectedYear < today.Year - 10 || selectedYear > today.Year || selectedMonth < 1 || selectedMonth > 12) return false;
            start = new DateTime(selectedYear, selectedMonth, 1);
            return true;
        }
    }
}
