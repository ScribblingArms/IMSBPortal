using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Api.Controllers
{
    /// <summary>Monthly pay bill: basic pay prorated by paid attendance days, plus allowances and overtime, less deductions.</summary>
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
            if (!TryResolvePeriod(year, month, out var start)) return Invalid("period", "ValDateInvalid");
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
            if (!TryResolvePeriod(dto.Year, dto.Month, out var start)) return Invalid("period", "ValDateInvalid");
            if (dto.Rows.Select(row => row.StaffId).Distinct().Count() != dto.Rows.Count
                || dto.Rows.Any(row => row.StaffId == Guid.Empty)
                || !await _payroll.GenerateAsync(start, dto.Rows))
                return Invalid("rows", "CheckForm");
            return Ok(new { success = true, message = L["PayrollSaved"].Value });
        }

        /// <summary>The employee's saved pay bills before the month, newest first.</summary>
        [HttpGet("history/{staffId:guid}")]
        public async Task<IActionResult> History(Guid staffId, [FromQuery] int? year, [FromQuery] int? month)
        {
            if (staffId == Guid.Empty || !TryResolvePeriod(year, month, out var start)) return Invalid("period", "ValDateInvalid");
            return Ok(new { rows = await _payroll.HistoryAsync(staffId, start, HistoryMonths) });
        }

        private static bool TryResolvePeriod(int? year, int? month, out DateTime start)
        {
            start = default;
            if (year is null or < 2000 or > 9999 || month is null or < 1 or > 12) return false;
            start = new DateTime(year.Value, month.Value, 1);
            return true;
        }
    }
}
