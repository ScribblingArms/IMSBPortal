using IMSB.Backend;
using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>
    /// Staff / HR: the directory with its figures, and register / edit / delete. Employees are kept in SERP.tbl_Employee;
    /// EmpTypeID comes from SERP.tbl_EMP_Types, Desig from SERP.tbl_Designation (the designation and its salary), DepartmentID from dbo.departments (the staff category), ShiftID from dbo.Shift and
    /// AgencyID from SERP.tbl_ArtisansTypeAgency.
    /// employeeId is the EMP_ID (used in the URLs); employeeCode is tbl_Employee.EmployeeId, "EMP001", given on create and never
    /// changed (the attendance, leave and payroll endpoints show the same code as their employeeId).
    /// </summary>
    [Authorize(Roles = "Staff,Admin")]
    public class StaffController : ApiControllerBase
    {
        private readonly IStaffInterface _staff;
        private readonly ILeavesInterface _leaves;

        public StaffController(IStaffInterface staff, ILeavesInterface leaves)
        {
            _staff = staff;
            _leaves = leaves;
        }

        /// <summary>GET api/staff: employee types, departments, agencies, shifts, the filtered staff list and the totals.</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status,
            [FromQuery] string? employeeTypeId = null) =>
            Ok(await StaffValidation.ListPayloadAsync(_staff, q, departmentId, status, employeeTypeId, includeSalary: IsAdmin));

        private bool IsAdmin => User.IsInRole("Admin");

        /// <summary>GET api/staff/{id}/leave-quotas: this year's annual leave quotas (leave_entitlements) with the days taken and left.</summary>
        [HttpGet("{id:int}/leave-quotas")]
        public async Task<IActionResult> LeaveQuotas(int id)
        {
            if (await _staff.FindAsync(id) == null) return NotFound();
            return Ok(await StaffValidation.LeaveQuotasPayloadAsync(_leaves, id));
        }

        /// <summary>GET api/staff/{id}/photo: the photo (tbl_Employee.ImageBase64) as a JPEG or PNG; 404 when there is none.</summary>
        [HttpGet("{id:int}/photo")]
        [Produces("image/jpeg", "image/png")]
        public async Task<IActionResult> Photo(int id) =>
            await StaffValidation.PhotoFileAsync(_staff, id) is { } photo ? File(photo.Bytes, photo.ContentType) : NotFound();

        /// <summary>GET api/staff/export: the filtered list as an Excel-readable file.</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status,
            [FromQuery] string? employeeTypeId = null) =>
            ExportExcel($"staff_{DateTime.UtcNow:yyyyMMddHHmmss}.xls", await StaffValidation.ExportRowsAsync(_staff, q, departmentId, status, employeeTypeId));

        /// <summary>GET api/staff/{id}</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var employee = await _staff.FindAsync(id);
            return employee == null ? NotFound() : Ok(employee.ToListItem(await _staff.LookupsAsync(), includeSalary: IsAdmin));
        }

        /// <summary>POST api/staff</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateStaffDto dto)
        {
            var errors = await StaffValidation.ValidateAsync(_staff, dto, null, L);
            if (errors.Count > 0) return Invalid(errors);

            var entity = new Employee { IsActive = 1, CreateOn = DateTime.Now };
            dto.ApplyFormTo(entity);
            await _staff.AddAsync(entity, dto.LeaveQuotas, dto.ToDesignationInput(includeSalary: IsAdmin));
            return Ok(new { employeeId = entity.EmpId, employeeCode = entity.EmployeeId });
        }

        /// <summary>PUT api/staff/{id}</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateStaffDto dto)
        {
            var entity = await _staff.FindAsync(id);
            if (entity == null) return NotFound();
            var errors = await StaffValidation.ValidateAsync(_staff, dto, entity, L, _leaves);
            if (errors.Count > 0) return Invalid(errors);

            dto.ApplyFormTo(entity);
            StaffValidation.ApplyStatus(dto, entity);
            entity.ModifiedOn = DateTime.Now;
            await _staff.UpdateAsync(entity, dto.ToDesignationInput(includeSalary: IsAdmin));

            // the quotas are the Leaves page's leave_entitlements; only the listed types change
            if (dto.LeaveQuotas is { Count: > 0 })
            {
                var saved = await _leaves.SaveQuotasAsync(entity.EmpId, dto.LeaveQuotas);
                if (saved.ErrorKey != null) return Invalid(new Dictionary<string, string> { ["leave_quotas"] = L[saved.ErrorKey] });
            }
            return Ok(new { employeeId = entity.EmpId, employeeCode = entity.EmployeeId });
        }

        /// <summary>DELETE api/staff/{id}: 409 when the database refuses (attendance on record).</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _staff.FindAsync(id);
            if (entity == null) return NotFound();
            if (!await _staff.RemoveAsync(entity)) return Conflict(new { error = L["StaffDeleteInUse"].Value });
            return Ok(new { employeeId = id, employeeCode = entity.EmployeeId });
        }
    }
}
