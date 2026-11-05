using System.Globalization;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class PayrollController : Controller
{
    private const int HistoryMonths = 24;

    private readonly IPayrollInterface _payroll;
    private readonly IStringLocalizer _L;

    public PayrollController(IPayrollInterface payroll, IStringLocalizerFactory localizerFactory)
    {
        _payroll = payroll;
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
        if (!TryResolvePeriod(year, month, out var start)) return ErrorResult("period", _L["ValDateInvalid"].Value);

        var rows = await _payroll.RowsAsync(start);
        return Ok(new
        {
            year = start.Year,
            month = start.Month,
            monthDays = DateTime.DaysInMonth(start.Year, start.Month),
            isGenerated = await _payroll.IsGeneratedAsync(start),
            departments = await _payroll.DepartmentsAsync(),
            rows,
            totalNetPay = rows.Sum(row => row.NetPay)
        });
    }

    [HttpGet]
    public async Task<IActionResult> EmployeeHistory([FromQuery] Guid? staffId, [FromQuery] int? year, [FromQuery] int? month)
    {
        if (staffId == null || staffId == Guid.Empty || !TryResolvePeriod(year, month, out var start))
            return ErrorResult("period", _L["ValDateInvalid"].Value);

        return Ok(new { rows = await _payroll.HistoryAsync(staffId.Value, start, HistoryMonths) });
    }

    /// <summary>The month's pay bill as on screen: employeeId is the staff filter (exact), q matches name or id.</summary>
    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? q, [FromQuery] string? employeeId = null)
    {
        if (!TryResolvePeriod(year, month, out var start)) return BadRequest();
        var rows = await _payroll.RowsAsync(start, q);
        if (!string.IsNullOrWhiteSpace(employeeId))
            rows = rows.Where(row => string.Equals(row.EmployeeId, employeeId.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        var output = new List<string?[]>
        {
            new[] { _L["EmployeeID"].Value, _L["StaffName"].Value, _L["Department"].Value, _L["Designation"].Value,
                _L["PaidDays"].Value, _L["MonthlySalary"].Value, _L["BasicPay"].Value, _L["Allowances"].Value, _L["OTHours"].Value,
                _L["OvertimeAmount"].Value, _L["Deductions"].Value, _L["NetPay"].Value }
        };
        output.AddRange(rows.Select(row => new string?[]
        {
            row.EmployeeId, row.EmployeeName, row.Department, row.Designation,
            row.PaidDays.ToString("0.##", CultureInfo.InvariantCulture),
            Money(row.MonthlySalary),
            Money(row.BasicPay),
            Money(row.Allowances),
            row.OvertimeHours.ToString("0.##", CultureInfo.InvariantCulture),
            Money(row.OvertimeAmount),
            Money(row.Deductions),
            Money(row.NetPay)
        }));
        output.Add(new string?[] { _L["GrandTotal"].Value, null, null, null, null, null, null, null, null, null, null, Money(rows.Sum(row => row.NetPay)) });
        return ExportExcel($"pay_bill_{start:yyyyMM}.xls", output);
    }

    [HttpGet]
    public async Task<IActionResult> Print([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? q)
    {
        if (!TryResolvePeriod(year, month, out var start)) return BadRequest();
        ViewData["Title"] = _L["PayBillTitle"].Value;
        var rows = await _payroll.RowsAsync(start, q);
        return View("Print", new PayBillPrintModel(
            _L["PayBillTitle"].Value,
            start.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            rows,
            rows.Sum(row => row.NetPay)));
    }

    private static bool TryResolvePeriod(int? year, int? month, out DateTime start)
    {
        start = default;
        if (year is null or < 2000 or > 9999 || month is null or < 1 or > 12) return false;
        start = new DateTime(year.Value, month.Value, 1);
        return true;
    }

    internal sealed record PayBillPrintModel(string Title, string Period, List<PayBillRow> Rows, decimal TotalNetPay);
}
