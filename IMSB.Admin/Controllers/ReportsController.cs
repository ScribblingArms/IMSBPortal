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
    /// Reports &amp; Analytics, including the artisan performance, monthly attendance, salary, and abstract reports.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        public const int TrendMonths = 12;
        public const int MonthChoices = 12;

        private readonly IReportsInterface _reports;
        private readonly IAttendanceInterface _attendance;
        private readonly IArtisansInterface _artisans;
        private readonly IStringLocalizer _L;

        public ReportsController(
            IReportsInterface reports,
            IAttendanceInterface attendance,
            IArtisansInterface artisans,
            IStringLocalizerFactory localizerFactory)
        {
            _reports = reports;
            _attendance = attendance;
            _artisans = artisans;
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

        public async Task<IActionResult> ArtisanPerformance()
        {
            ViewData["Title"] = _L["ArtisanPerformanceReport"].Value;
            ViewBag.Months = MonthOptions(DateTime.Today);
            ViewBag.Categories = (await _reports.CategoriesAsync())
                .Select(c => new KeyValuePair<string, string>(c.Id.ToString(), c.Name)).ToList();
            return View();
        }

        public IActionResult MonthlyReports()
        {
            ViewData["Title"] = _L["MonthlyReports"].Value;
            SetAttendanceReportOptions();
            return View();
        }

        public IActionResult SalaryReport()
        {
            ViewData["Title"] = _L["SalaryReport"].Value;
            SetAttendanceReportOptions();
            return View();
        }

        public IActionResult AbstractReport()
        {
            ViewData["Title"] = _L["AbstractReport"].Value;
            return View();
        }

        private void SetAttendanceReportOptions()
        {
            var today = DateTime.Today;
            ViewBag.Years = Enumerable.Range(0, 11).Select(i => today.Year - i).ToList();
            ViewBag.MonthsOfYear = Enumerable.Range(1, 12)
                .Select(i => new KeyValuePair<int, string>(
                    i, new DateTime(2000, i, 1).ToString("MMMM", CultureInfo.CurrentCulture))).ToList();
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

        [HttpGet]
        public async Task<IActionResult> MonthlyAttendance([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? type)
        {
            if (!ModelState.IsValid || !TryResolveAttendanceMonth(year, month, out var start))
                return BadRequest(new { errors = new Dictionary<string, string> { ["period"] = _L["ValDateInvalid"] } });

            return Ok(await MonthlyAttendanceDataAsync(start, type));
        }

        [HttpGet]
        public async Task<IActionResult> MonthlyAttendanceExport([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? type)
        {
            if (!ModelState.IsValid || !TryResolveAttendanceMonth(year, month, out var start))
                return BadRequest(new { errors = new Dictionary<string, string> { ["period"] = _L["ValDateInvalid"] } });

            var report = await MonthlyAttendanceDataAsync(start, type);
            var rows = new List<string?[]>
            {
                new[] { _L["SerialNumber"].Value, _L["EmployeeID"].Value, _L["StaffName"].Value, _L["Designation"].Value,
                    _L["TotalDaysInMonth"].Value, _L["TotalWorkingDays"].Value }
            };
            rows.AddRange(report.Rows.Select((row, index) => new string?[]
            {
                (index + 1).ToString(CultureInfo.InvariantCulture),
                row.EmployeeId,
                row.Name,
                row.Designation,
                row.TotalDaysInMonth.ToString(CultureInfo.InvariantCulture),
                row.TotalWorkingDays.ToString(CultureInfo.InvariantCulture)
            }));

            return ExportExcel($"monthly_attendance_{start:yyyyMM}.xls", rows);
        }

        [HttpGet]
        public async Task<IActionResult> AbstractData([FromQuery] string? date)
        {
            date ??= DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!TryResolveAbstractDate(date, out var selectedDate))
                return BadRequest(new { errors = new Dictionary<string, string> { ["date"] = _L["ValDateInvalid"].Value } });

            return Ok(await AbstractDataAsync(selectedDate));
        }

        [HttpGet]
        public async Task<IActionResult> AbstractExport(
            [FromQuery] string? date, [FromQuery] string? section, [FromQuery] string? metric)
        {
            date ??= DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!TryResolveAbstractDate(date, out var selectedDate))
                return BadRequest(new { errors = new Dictionary<string, string> { ["date"] = _L["ValDateInvalid"].Value } });

            var report = await AbstractDataAsync(selectedDate);
            if (!string.IsNullOrWhiteSpace(section) && report.Sections.All(item => item.Key != section))
                return BadRequest(new { errors = new Dictionary<string, string> { ["section"] = _L["ValDateInvalid"].Value } });
            metric = string.IsNullOrWhiteSpace(metric) ? "total" : metric;
            if (metric is not ("total" or "attended" or "notAttended"))
                return BadRequest(new { errors = new Dictionary<string, string> { ["metric"] = _L["ValDateInvalid"].Value } });

            var rows = report.Employees
                .Where(employee => string.IsNullOrWhiteSpace(section) || employee.SectionKey == section)
                .Where(employee => MatchesAbstractMetric(employee, metric))
                .Select((employee, index) => new string?[]
                {
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    employee.Name,
                    employee.EmployeeId,
                    employee.Designation,
                    employee.AttendanceStatus ?? ""
                }).ToList();
            rows.Insert(0, new[]
            {
                _L["SerialNumber"].Value, _L["StaffName"].Value, _L["EmployeeID"].Value,
                _L["Designation"].Value, _L["Status"].Value
            });

            return ExportExcel($"abstract_report_{selectedDate:yyyyMMdd}.xls", rows);
        }

        private static bool MatchesAbstractMetric(AbstractEmployee employee, string metric) => metric switch
        {
            "attended" => employee.Attended == true,
            "notAttended" => employee.Attended == false,
            _ => true
        };

        private static bool TryResolveAbstractDate(string? date, out DateTime selectedDate) =>
            DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out selectedDate);

        internal record AbstractSection(string Key, string Name, int Total, int? Attended, int? NotAttended);
        internal record AbstractEmployee(
            string SectionKey, string EmployeeId, string Name, string EmployeeType, string Designation,
            string Gender, string Mobile, bool? Attended, string? AttendanceStatus);
        internal record AbstractReportData(
            string Date, List<AbstractSection> Sections, List<AbstractEmployee> Employees);

        private async Task<AbstractReportData> AbstractDataAsync(DateTime date)
        {
            var staff = await _attendance.SheetAsync(date);
            var artisans = await _artisans.FilterAsync(null, null, null, null, null);
            var sections = new List<AbstractSection>();
            var employees = new List<AbstractEmployee>();

            foreach (var group in staff
                .GroupBy(row => string.IsNullOrWhiteSpace(row.DepartmentName) ? _L["StaffHR"].Value : row.DepartmentName.Trim())
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                var key = "staff:" + group.Key;
                var rows = group.OrderBy(row => row.Staff.FullName, StringComparer.OrdinalIgnoreCase).ToList();
                var attended = rows.Count(row => row.Record?.Status is AttendanceStatuses.Present or AttendanceStatuses.HalfDay);
                sections.Add(new AbstractSection(key, group.Key, rows.Count, attended, rows.Count - attended));
                employees.AddRange(rows.Select(row =>
                {
                    var isAttended = row.Record?.Status is AttendanceStatuses.Present or AttendanceStatuses.HalfDay;
                    return new AbstractEmployee(
                        key,
                        row.Staff.EmployeeId,
                        row.Staff.FullName,
                        group.Key,
                        row.Staff.Designation,
                        row.Staff.Gender,
                        row.Staff.Mobile,
                        isAttended,
                        row.Record?.Status ?? "NOT_MARKED");
                }));
            }

            const string artisanSectionKey = "artisans";
            if (artisans.Count > 0)
            {
                sections.Insert(0, new AbstractSection(artisanSectionKey, _L["Artisans"].Value, artisans.Count, null, null));
                employees.InsertRange(0, artisans.OrderBy(artisan => artisan.FullName, StringComparer.OrdinalIgnoreCase)
                    .Select(artisan => new AbstractEmployee(
                        artisanSectionKey, artisan.ArtisanCode, artisan.FullName, _L["Artisans"].Value, _L["Artisans"].Value,
                        artisan.Gender, artisan.Mobile, null, null)));
            }

            return new AbstractReportData(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), sections, employees);
        }

        [HttpGet]
        public async Task<IActionResult> Salary([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? type, [FromQuery] string? q)
        {
            if (!ModelState.IsValid || !TryResolveAttendanceMonth(year, month, out var start))
                return BadRequest(new { errors = new Dictionary<string, string> { ["period"] = _L["ValDateInvalid"] } });

            return Ok(await SalaryDataAsync(start, type, q));
        }

        [HttpGet]
        public async Task<IActionResult> SalaryExport([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? type, [FromQuery] string? q)
        {
            if (!ModelState.IsValid || !TryResolveAttendanceMonth(year, month, out var start))
                return BadRequest(new { errors = new Dictionary<string, string> { ["period"] = _L["ValDateInvalid"] } });

            var report = await SalaryDataAsync(start, type, q);
            var rows = new List<string?[]>
            {
                new[] { _L["SerialNumber"].Value, _L["EmployeeID"].Value, _L["StaffName"].Value, _L["EmployeeType"].Value,
                    _L["Designation"].Value, _L["TotalDaysInMonth"].Value, _L["WorkedDays"].Value, _L["TotalSalary"].Value }
            };
            rows.AddRange(report.Rows.Select((row, index) => new string?[]
            {
                (index + 1).ToString(CultureInfo.InvariantCulture),
                row.EmployeeId,
                row.Name,
                row.Department,
                row.Designation,
                row.MonthDays.ToString(CultureInfo.InvariantCulture),
                row.WorkedDays.ToString("0.##", CultureInfo.InvariantCulture),
                row.TotalSalary.ToString("0.00", CultureInfo.InvariantCulture)
            }));

            var totalRow = new string?[8];
            totalRow[0] = _L["GrandTotal"].Value;
            totalRow[7] = report.GrandTotalSalary.ToString("0.00", CultureInfo.InvariantCulture);
            rows.Add(totalRow);
            return ExportExcel($"salary_report_{start:yyyyMM}.xls", rows);
        }

        internal record SalaryReportRow(
            string EmployeeId, string Name, string Department, string Designation,
            int MonthDays, decimal WorkedDays, decimal TotalSalary);
        internal record SalaryReportData(int Year, int Month, int MonthDays, List<string> Types, List<SalaryReportRow> Rows, decimal GrandTotalSalary);

        private async Task<SalaryReportData> SalaryDataAsync(DateTime start, string? type, string? query)
        {
            var end = start.AddMonths(1).AddDays(-1);
            var staff = await _attendance.MonthlyStaffAsync(start, end);
            var departments = await _attendance.DepartmentNamesAsync();
            var types = departments.Values.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

            if (!string.IsNullOrWhiteSpace(type))
                staff = staff.Where(s => string.Equals(
                    departments.GetValueOrDefault(s.DepartmentId), type.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            var records = await _attendance.HistoryAsync(null, null, null, start, end);
            var recordsByStaff = records.ToLookup(r => r.StaffId);
            var monthDays = DateTime.DaysInMonth(start.Year, start.Month);
            var rows = staff.Select(s =>
            {
                var department = departments.GetValueOrDefault(s.DepartmentId) ?? string.Empty;
                var workedDays = recordsByStaff[s.Id].Sum(r => r.Status switch
                {
                    AttendanceStatuses.Present => 1m,
                    AttendanceStatuses.HalfDay => 0.5m,
                    _ => 0m
                });
                var totalSalary = Math.Round(s.MonthlySalary * workedDays / monthDays, 2, MidpointRounding.AwayFromZero);
                return new SalaryReportRow(s.EmployeeId, s.FullName, department, s.Designation, monthDays,
                    workedDays, totalSalary);
            }).Where(row => MatchesSalaryQuery(row, query))
              .OrderBy(row => row.Department, StringComparer.OrdinalIgnoreCase)
              .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
              .ThenBy(row => row.EmployeeId, StringComparer.OrdinalIgnoreCase)
              .ToList();

            return new SalaryReportData(start.Year, start.Month, monthDays, types, rows, rows.Sum(row => row.TotalSalary));
        }

        private static bool MatchesSalaryQuery(SalaryReportRow row, string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            var value = query.Trim();
            return row.EmployeeId.Contains(value, StringComparison.OrdinalIgnoreCase)
                || row.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
                || row.Department.Contains(value, StringComparison.OrdinalIgnoreCase)
                || row.Designation.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        internal record MonthlyAttendanceRow(string EmployeeId, string Name, string Designation, int TotalDaysInMonth, int TotalWorkingDays);
        internal record MonthlyAttendanceData(int Year, int Month, int TotalDaysInMonth, List<string> Types, List<MonthlyAttendanceRow> Rows);

        private async Task<MonthlyAttendanceData> MonthlyAttendanceDataAsync(DateTime start, string? type)
        {
            var end = start.AddMonths(1).AddDays(-1);
            var staff = await _attendance.MonthlyStaffAsync(start, end);
            var types = staff.Select(s => s.Designation.Trim()).Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();

            if (!string.IsNullOrWhiteSpace(type))
                staff = staff.Where(s => string.Equals(s.Designation.Trim(), type.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            var records = await _attendance.HistoryAsync(null, null, null, start, end);
            var recordsByStaff = records.ToLookup(r => r.StaffId);
            var rows = staff.Select(s => new MonthlyAttendanceRow(
                s.EmployeeId,
                s.FullName,
                s.Designation,
                DateTime.DaysInMonth(start.Year, start.Month),
                recordsByStaff[s.Id].Count(r => r.Status == AttendanceStatuses.Present || r.Status == AttendanceStatuses.HalfDay)))
                .ToList();

            return new MonthlyAttendanceData(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month), types, rows);
        }

        private static bool TryResolveAttendanceMonth(int? year, int? month, out DateTime start)
        {
            var today = DateTime.Today;
            var selectedYear = year ?? today.Year;
            var selectedMonth = month ?? today.Month;
            if (selectedYear < today.Year - 10 || selectedYear > today.Year || selectedMonth < 1 || selectedMonth > 12)
            {
                start = default;
                return false;
            }

            start = new DateTime(selectedYear, selectedMonth, 1);
            return true;
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
        /// /Reports/Export/{id}?format=xls|pdf. Excel downloads a file; pdf returns a print-ready page that opens the
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
            return ExportExcel($"report_{reportId}_{period.MonthStart:yyyyMM}_{DateTime.Now:yyyyMMddHHmmss}.xls", ToExcelRows(doc));
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

        internal List<string?[]> ToExcelRows(ReportDocument doc)
        {
            var rows = new List<string?[]>
            {
                new string?[] { doc.Title, doc.Period },
                new string?[] { _L["GeneratedOn"], doc.GeneratedAt.ToString("yyyy-MM-dd HH:mm"), doc.GeneratedBy }
            };
            foreach (var t in doc.Tables)
            {
                rows.Add(Array.Empty<string?>());
                rows.Add(new string?[] { t.Title });
                rows.Add(t.Headers);
                rows.AddRange(t.Rows);
                if (t.Rows.Count == 0) rows.Add(new string?[] { _L["NoDataForPeriod"] });
                if (t.Note != null) rows.Add(new string?[] { t.Note });
            }
            return rows;
        }
    }
}
