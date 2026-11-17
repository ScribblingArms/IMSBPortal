using System.ComponentModel.DataAnnotations;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>
    /// Leave management for the current year: entitlements per employee, leave taken, and balances.
    /// Employees are SERP.tbl_Employee rows: staffId is the EMP_ID, employeeId the same as text, and the staff category is
    /// their dbo.departments name. The Staff page's Annual Quota writes the same entitlements.
    /// </summary>
    [Authorize(Roles = "Admin,Staff")]
    public class LeavesController : ApiControllerBase
    {
        private readonly ILeavesInterface _leaves;

        public LeavesController(ILeavesInterface leaves) => _leaves = leaves;

        private static int Year => DateTime.Today.Year;

        /// <summary>Entitlement, taken and balance per employee and leave type.</summary>
        [HttpGet("balances")]
        public async Task<IActionResult> Balances([FromQuery] string? employeeId, [FromQuery] string? leaveType) =>
            Ok(new { year = Year, rows = await _leaves.BalancesAsync(Year, employeeId, leaveType) });

        /// <summary>Staff (with categories) and the year's entitlements, for the entitlement form.</summary>
        [HttpGet("entitlements")]
        public async Task<IActionResult> Entitlements()
        {
            var staff = await _leaves.StaffAsync();
            return Ok(new
            {
                year = Year,
                categories = staff.Select(s => s.StaffCategory.Trim()).Where(c => c.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c),
                staff,
                entitlements = (await _leaves.EntitlementsWithStaffAsync(Year)).Select(e => new
                {
                    id = e.Id,
                    staffId = e.StaffId,
                    employeeId = e.Staff?.EmployeeId,
                    staffName = e.Staff?.FullName,
                    staffCategory = staff.FirstOrDefault(s => s.Id == e.StaffId)?.StaffCategory,
                    leaveType = e.LeaveType,
                    annualQuota = e.AnnualQuota,
                    isAttendanceLeaveType = e.IsAttendanceLeaveType
                })
            });
        }

        /// <summary>Adds or changes an employee's quota for a leave type (in steps of 0.5 days).</summary>
        [HttpPost("entitlements")]
        public async Task<IActionResult> SaveEntitlement([FromBody] LeaveEntitlementForm form)
        {
            if (form.StaffId == 0 || string.IsNullOrWhiteSpace(form.LeaveType) || form.AnnualQuota % 0.5m != 0m)
                return Invalid("entitlement", "ValLeaveEntitlement");
            var result = await _leaves.SaveEntitlementAsync(form.StaffId, form.LeaveType, form.AnnualQuota);
            return FromResult(result, "entitlement", () => Ok(new { message = L["LeaveEntitlementSaved"].Value }));
        }

        [HttpDelete("entitlements/{id:int}")]
        public async Task<IActionResult> DeleteEntitlement(int id) =>
            FromResult(await _leaves.DeleteEntitlementAsync(id), "entitlement", () => Ok(new { message = L["LeaveEntitlementDeleted"].Value }));

        /// <summary>Active staff, entitlements and the year's leave entries, for the leave-taken form.</summary>
        [HttpGet("entries")]
        public async Task<IActionResult> Entries() =>
            Ok(new
            {
                year = Year,
                staff = await _leaves.ActiveStaffAsync(),
                leaveTypes = LeaveBalanceRules.WithLop(await _leaves.LeaveTypesAsync()),
                entitlements = (await _leaves.EntitlementsAsync(Year)).Select(e => new { id = e.Id, staffId = e.StaffId, leaveType = e.LeaveType, annualQuota = e.AnnualQuota }),
                entries = (await _leaves.EntriesAsync(Year)).Select(ToItem)
            });

        /// <summary>Records future leave (dates yyyy-MM-dd, this year) against the employee's balance.</summary>
        [HttpPost("entries")]
        public async Task<IActionResult> Record([FromBody] LeaveEntryForm form)
        {
            var result = await _leaves.RecordEntryAsync(form.StaffId, form.LeaveType, ParseDate(form.FromDate), ParseDate(form.ToDate),
                form.Remarks, UserName);
            return FromResult(result, "entry", () => Ok(new { message = L["LeaveEntrySaved"].Value }));
        }

        public class LeaveEntryUpdate
        {
            [Required] public string FromDate { get; set; } = string.Empty;
            [Required] public string ToDate { get; set; } = string.Empty;
            [StringLength(500, ErrorMessage = "ValRemarks")] public string? Remarks { get; set; }
        }

        [HttpPut("entries/{id:int}")]
        public async Task<IActionResult> UpdateEntry(int id, [FromBody] LeaveEntryUpdate form)
        {
            var result = await _leaves.UpdateEntryAsync(id, ParseDate(form.FromDate), ParseDate(form.ToDate), form.Remarks, UserName);
            return FromResult(result, "entry", () => Ok(new { message = L["LeaveEntrySaved"].Value }));
        }

        [HttpDelete("entries/{id:int}")]
        public async Task<IActionResult> DeleteEntry(int id) =>
            FromResult(await _leaves.DeleteEntryAsync(id), "entry", () => Ok(new { message = L["LeaveEntryDeleted"].Value }));

        private static object ToItem(StaffLeaveEntry e) => new
        {
            id = e.Id,
            staffId = e.StaffId,
            employeeId = e.Staff?.EmployeeId,
            staffName = e.Staff?.FullName,
            leaveType = e.LeaveType,
            fromDate = Date(e.FromDate),
            toDate = Date(e.ToDate),
            days = e.Days,
            remarks = e.Remarks,
            by = e.UpdatedBy ?? e.CreatedBy
        };
    }
}
