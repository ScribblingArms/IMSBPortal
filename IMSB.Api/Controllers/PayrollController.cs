using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>
    /// Monthly pay bill: basic pay prorated by paid attendance days, plus allowances and overtime, less deductions.
    /// Employees are SERP.tbl_Employee rows: staffId is the EMP_ID, employeeId their employee code ("EMP001"; an EMP_ID number is also accepted where one is looked up), and the monthly salary is the
    /// BaseSalary of the employee's tbl_Designation row.
    /// </summary>
    [Authorize(Roles = "Admin,Staff")]
    public class PayrollController : ApiControllerBase
    {
        private const int HistoryMonths = 24;

        private readonly IPayrollInterface _payroll;

        public PayrollController(IPayrollInterface payroll) => _payroll = payroll;

        /// <summary>The month's pay bill; q matches employee name or id.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? q)
        {
            if (!TryMonthStart(year, month, out var start)) return Invalid("period", "ValDateInvalid");
            var rows = await _payroll.RowsAsync(start, q);
            return Ok(new
            {
                year = start.Year,
                month = start.Month,
                monthDays = DateTime.DaysInMonth(start.Year, start.Month),
                isGenerated = await _payroll.IsGeneratedAsync(start),
                rows,
                totalNetPay = rows.Sum(row => row.NetPay)
            });
        }

        /// <summary>Saves the month's pay bill with the allowances, overtime amounts and deductions given per employee.</summary>
        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] GeneratePayBillDto dto)
        {
            if (!TryMonthStart(dto.Year, dto.Month, out var start)) return Invalid("period", "ValDateInvalid");
            if (dto.Rows.Select(row => row.StaffId).Distinct().Count() != dto.Rows.Count
                || dto.Rows.Any(row => row.StaffId == 0)
                || !await _payroll.GenerateAsync(start, dto.Rows))
                return Invalid("rows", "CheckForm");
            return Ok(new { success = true, message = L["PayrollSaved"].Value });
        }

        /// <summary>The employee's saved pay bills before the month, newest first.</summary>
        [HttpGet("history/{staffId:int}")]
        public async Task<IActionResult> History(int staffId, [FromQuery] int? year, [FromQuery] int? month)
        {
            if (staffId == 0 || !TryMonthStart(year, month, out var start)) return Invalid("period", "ValDateInvalid");
            return Ok(new { rows = await _payroll.HistoryAsync(staffId, start, HistoryMonths) });
        }
    }
}
