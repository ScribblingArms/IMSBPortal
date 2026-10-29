using System.Globalization;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class PayrollController : Controller
{
    private readonly IMSBContext _context;
    private readonly IStringLocalizer _L;

    public PayrollController(IMSBContext context, IStringLocalizerFactory localizerFactory)
    {
        _context = context;
        _L = localizerFactory.Create("SharedResource", typeof(PayrollController).Assembly.GetName().Name!);
    }

    public IActionResult Index()
    {
        ViewData["Title"] = _L["PayBillTitle"].Value;
        var today = DateTime.Today;
        ViewBag.Years = Enumerable.Range(0, 11).Select(i => today.Year - i).ToList();
        ViewBag.Months = Enumerable.Range(1, 12)
            .Select(i => new KeyValuePair<int, string>(
                i, new DateTime(2000, i, 1).ToString("MMMM", CultureInfo.CurrentCulture))).ToList();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] int? year, [FromQuery] int? month)
    {
        if (!TryResolvePeriod(year, month, out var start))
            return BadRequest(new { errors = new Dictionary<string, string> { ["period"] = _L["ValDateInvalid"].Value } });

        var rows = await BuildRowsAsync(start);
        return Ok(new
        {
            year = start.Year,
            month = start.Month,
            monthDays = DateTime.DaysInMonth(start.Year, start.Month),
            isGenerated = await _context.PayBillEntries.AnyAsync(e => e.Year == start.Year && e.Month == start.Month),
            rows,
            totalNetPay = rows.Sum(row => row.NetPay)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate([FromBody] GeneratePayBillDto? dto)
    {
        if (dto == null)
            return BadRequest(new { errors = new Dictionary<string, string> { ["request"] = _L["CheckForm"].Value } });

        if (!TryResolvePeriod(dto.Year, dto.Month, out var start))
            ModelState.AddModelError("period", _L["ValDateInvalid"].Value);

        if (!ModelState.IsValid)
            return BadRequest(new { errors = ModelState.ToDictionary(
                entry => entry.Key,
                entry => entry.Value?.Errors.FirstOrDefault()?.ErrorMessage ?? _L["CheckForm"].Value) });

        if (dto.Rows.Select(row => row.StaffId).Distinct().Count() != dto.Rows.Count
            || dto.Rows.Any(row => row.StaffId == Guid.Empty))
            return BadRequest(new { errors = new Dictionary<string, string> { ["rows"] = _L["CheckForm"].Value } });

        var from = start.Date;
        var to = start.AddMonths(1).AddDays(-1).Date;
        var endExclusive = to.AddDays(1);
        var existing = await _context.PayBillEntries
            .Where(entry => entry.Year == start.Year && entry.Month == start.Month)
            .ToDictionaryAsync(entry => entry.StaffId);
        var markedStaff = _context.AttendanceRecords
            .Where(record => record.AttendanceDate >= from && record.AttendanceDate <= to)
            .Select(record => record.StaffId);
        var staff = await _context.Staff
            .Where(employee => employee.JoinDate < endExclusive
                && (employee.Status.ToUpper() != "INACTIVE" || existing.Keys.Contains(employee.Id) || markedStaff.Contains(employee.Id)))
            .ToListAsync();
        var staffIds = staff.Select(employee => employee.Id).ToList();
        var submittedIds = dto.Rows.Select(row => row.StaffId).ToList();
        if (submittedIds.Except(staffIds).Any())
            return BadRequest(new { errors = new Dictionary<string, string> { ["rows"] = _L["CheckForm"].Value } });

        var attendance = await _context.AttendanceRecords.AsNoTracking()
            .Where(record => staffIds.Contains(record.StaffId)
                && record.AttendanceDate >= from && record.AttendanceDate <= to)
            .ToListAsync();
        var departments = await _context.Departments.AsNoTracking().ToDictionaryAsync(department => department.Id, department => department.Name);
        var adjustments = dto.Rows.ToDictionary(row => row.StaffId);
        var monthDays = DateTime.DaysInMonth(start.Year, start.Month);

        foreach (var employee in staff)
        {
            existing.TryGetValue(employee.Id, out var entry);
            adjustments.TryGetValue(employee.Id, out var adjustment);
            var attendanceRows = attendance.Where(record => record.StaffId == employee.Id
                && record.AttendanceDate.Date >= employee.JoinDate.Date).ToList();
            var paidDays = PaidDays(attendanceRows);
            var basicPay = ProratedPay(employee.MonthlySalary, paidDays, monthDays);
            var allowances = adjustment?.Allowances ?? entry?.Allowances ?? 0m;
            var overtimeAmount = adjustment?.OvertimeAmount ?? entry?.OvertimeAmount ?? 0m;
            var deductions = adjustment?.Deductions ?? entry?.Deductions ?? 0m;
            var netPay = Math.Max(0m, basicPay + allowances + overtimeAmount - deductions);

            if (entry == null)
            {
                entry = new PayBillEntry { StaffId = employee.Id, Year = start.Year, Month = start.Month };
                _context.PayBillEntries.Add(entry);
            }

            entry.EmployeeId = employee.EmployeeId;
            entry.EmployeeName = employee.FullName;
            entry.Department = departments.GetValueOrDefault(employee.DepartmentId) ?? string.Empty;
            entry.Designation = employee.Designation;
            entry.MonthDays = monthDays;
            entry.PaidDays = paidDays;
            entry.MonthlySalary = employee.MonthlySalary;
            entry.BasicPay = basicPay;
            entry.Allowances = allowances;
            entry.OvertimeHours = attendanceRows.Sum(record => record.OvertimeHours);
            entry.OvertimeAmount = overtimeAmount;
            entry.Deductions = deductions;
            entry.NetPay = netPay;
            entry.GeneratedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = _L["PayrollSaved"].Value });
    }

    [HttpGet]
    public async Task<IActionResult> EmployeeHistory([FromQuery] Guid? staffId, [FromQuery] int? year, [FromQuery] int? month)
    {
        if (staffId == null || staffId == Guid.Empty || !TryResolvePeriod(year, month, out var start))
            return BadRequest(new { errors = new Dictionary<string, string> { ["period"] = _L["ValDateInvalid"].Value } });

        var history = await _context.PayBillEntries.AsNoTracking()
            .Where(entry => entry.StaffId == staffId.Value
                && (entry.Year < start.Year || (entry.Year == start.Year && entry.Month < start.Month)))
            .OrderByDescending(entry => entry.Year).ThenByDescending(entry => entry.Month)
            .Take(24)
            .Select(entry => new
            {
                entry.Year,
                entry.Month,
                entry.PaidDays,
                entry.BasicPay,
                entry.Allowances,
                entry.OvertimeAmount,
                entry.Deductions,
                entry.NetPay
            })
            .ToListAsync();
        return Ok(new { rows = history });
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? q)
    {
        if (!TryResolvePeriod(year, month, out var start)) return BadRequest();
        var rows = await BuildRowsAsync(start, q);
        var output = new List<string?[]>
        {
            new[] { _L["EmployeeID"].Value, _L["StaffName"].Value, _L["Department"].Value, _L["Designation"].Value,
                _L["PaidDays"].Value, _L["BasicPay"].Value, _L["Allowances"].Value, _L["OTHours"].Value,
                _L["OvertimeAmount"].Value, _L["Deductions"].Value, _L["NetPay"].Value }
        };
        output.AddRange(rows.Select(row => new string?[]
        {
            row.EmployeeId, row.EmployeeName, row.Department, row.Designation,
            row.PaidDays.ToString("0.##", CultureInfo.InvariantCulture),
            row.BasicPay.ToString("0.00", CultureInfo.InvariantCulture),
            row.Allowances.ToString("0.00", CultureInfo.InvariantCulture),
            row.OvertimeHours.ToString("0.##", CultureInfo.InvariantCulture),
            row.OvertimeAmount.ToString("0.00", CultureInfo.InvariantCulture),
            row.Deductions.ToString("0.00", CultureInfo.InvariantCulture),
            row.NetPay.ToString("0.00", CultureInfo.InvariantCulture)
        }));
        return ExportExcel($"pay_bill_{start:yyyyMM}.xls", output);
    }

    [HttpGet]
    public async Task<IActionResult> Print([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? q)
    {
        if (!TryResolvePeriod(year, month, out var start)) return BadRequest();
        ViewData["Title"] = _L["PayBillTitle"].Value;
        var rows = await BuildRowsAsync(start, q);
        return View("Print", new PayBillPrintModel(
            _L["PayBillTitle"].Value,
            start.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            rows,
            rows.Sum(row => row.NetPay)));
    }

    private async Task<List<PayBillRow>> BuildRowsAsync(DateTime start, string? query = null)
    {
        var end = start.AddMonths(1).AddDays(-1);
        var endExclusive = end.AddDays(1);
        var saved = await _context.PayBillEntries.AsNoTracking()
            .Where(entry => entry.Year == start.Year && entry.Month == start.Month)
            .ToListAsync();
        var savedByStaff = saved.ToDictionary(entry => entry.StaffId);
        var markedStaff = _context.AttendanceRecords
            .Where(record => record.AttendanceDate >= start && record.AttendanceDate <= end)
            .Select(record => record.StaffId);
        var staff = await _context.Staff.AsNoTracking()
            .Where(employee => employee.JoinDate < endExclusive
                && (employee.Status.ToUpper() != "INACTIVE" || savedByStaff.Keys.Contains(employee.Id) || markedStaff.Contains(employee.Id)))
            .OrderBy(employee => employee.DepartmentId).ThenBy(employee => employee.FullName)
            .ToListAsync();
        var staffIds = staff.Select(employee => employee.Id).ToList();
        var attendance = await _context.AttendanceRecords.AsNoTracking()
            .Where(record => staffIds.Contains(record.StaffId)
                && record.AttendanceDate >= start && record.AttendanceDate <= end)
            .ToListAsync();
        var departments = await _context.Departments.AsNoTracking().ToDictionaryAsync(department => department.Id, department => department.Name);
        var days = DateTime.DaysInMonth(start.Year, start.Month);

        var rows = staff.Select(employee =>
        {
            savedByStaff.TryGetValue(employee.Id, out var entry);
            var records = attendance.Where(record => record.StaffId == employee.Id
                && record.AttendanceDate.Date >= employee.JoinDate.Date).ToList();
            var paidDays = entry?.PaidDays ?? PaidDays(records);
            var basicPay = entry?.BasicPay ?? ProratedPay(employee.MonthlySalary, paidDays, days);
            var allowances = entry?.Allowances ?? 0m;
            var overtimeAmount = entry?.OvertimeAmount ?? 0m;
            var deductions = entry?.Deductions ?? 0m;
            return new PayBillRow(
                employee.Id,
                employee.EmployeeId,
                employee.FullName,
                departments.GetValueOrDefault(employee.DepartmentId) ?? string.Empty,
                employee.Designation,
                entry?.MonthDays ?? days,
                paidDays,
                entry?.MonthlySalary ?? employee.MonthlySalary,
                basicPay,
                allowances,
                entry?.OvertimeHours ?? records.Sum(record => record.OvertimeHours),
                overtimeAmount,
                deductions,
                entry?.NetPay ?? Math.Max(0m, basicPay + allowances + overtimeAmount - deductions));
        }).ToList();

        var currentIds = rows.Select(row => row.StaffId).ToHashSet();
        rows.AddRange(saved.Where(entry => !currentIds.Contains(entry.StaffId)).Select(entry => new PayBillRow(
            entry.StaffId, entry.EmployeeId, entry.EmployeeName, entry.Department, entry.Designation, entry.MonthDays,
            entry.PaidDays, entry.MonthlySalary, entry.BasicPay, entry.Allowances, entry.OvertimeHours,
            entry.OvertimeAmount, entry.Deductions, entry.NetPay)));
        return rows.Where(row => MatchesStaffFilter(row, query))
            .OrderBy(row => row.Department, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.EmployeeId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool MatchesStaffFilter(PayBillRow row, string? query) =>
        string.IsNullOrWhiteSpace(query)
        || row.EmployeeName.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
        || row.EmployeeId.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);

    internal static decimal PaidDays(IEnumerable<AttendanceRecord> records) =>
        records.Sum(record => record.Status switch
        {
            AttendanceStatuses.Present => 1m,
            AttendanceStatuses.HalfDay => 0.5m,
            AttendanceStatuses.Leave when !record.LeaveWithoutPay => 1m,
            _ => 0m
        });

    internal static decimal ProratedPay(decimal monthlySalary, decimal paidDays, int monthDays) =>
        Math.Round(monthlySalary * paidDays / monthDays, 2, MidpointRounding.AwayFromZero);

    private static bool TryResolvePeriod(int? year, int? month, out DateTime start)
    {
        start = default;
        if (year is null or < 2000 or > 9999 || month is null or < 1 or > 12) return false;
        start = new DateTime(year.Value, month.Value, 1);
        return true;
    }

    internal sealed record PayBillRow(
        Guid StaffId, string EmployeeId, string EmployeeName, string Department, string Designation,
        int MonthDays, decimal PaidDays, decimal MonthlySalary, decimal BasicPay, decimal Allowances,
        decimal OvertimeHours, decimal OvertimeAmount, decimal Deductions, decimal NetPay);

    internal sealed record PayBillPrintModel(string Title, string Period, List<PayBillRow> Rows, decimal TotalNetPay);
}
