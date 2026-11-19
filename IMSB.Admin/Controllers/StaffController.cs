using IMSB.Backend;
using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers
{
    /// <summary>
    /// The Staff page: employees in SERP.tbl_Employee. EmpTypeID comes from SERP.tbl_EMP_Types, Desig from SERP.tbl_Designation (the designation and its salary), DepartmentID from dbo.departments
    /// (the staff category), ShiftID from dbo.Shift and AgencyID from SERP.tbl_ArtisansTypeAgency.
    /// </summary>
    [Authorize(Roles = "Staff,Admin")]
    public class StaffController : Controller
    {
        private readonly IStaffInterface _staff;
        private readonly ILeavesInterface _leaves;
        private readonly IStringLocalizer _L;

        public StaffController(IStaffInterface staff, ILeavesInterface leaves, IStringLocalizerFactory localizerFactory)
        {
            _staff = staff;
            _leaves = leaves;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(StaffController).Assembly.GetName().Name!);
        }

        public async Task<IActionResult> Index([FromQuery] bool register = false)
        {
            var employees = await _staff.AllAsync();

            // Dashboard counts
            var stats = StaffValidation.ComputeStats(employees);
            ViewBag.TotalStaff = stats.Total;
            ViewBag.ActiveStaff = stats.Active;
            ViewBag.InactiveStaff = stats.Inactive;
            ViewBag.Departments = stats.Departments;
            ViewBag.AgencyStaff = stats.AgencyStaff;
            ViewBag.RegisterOnLoad = register;

            return View(employees);
        }

        /// <summary>JSON used by staff.js: employee types, departments, agencies, shifts, the filtered staff list and the totals.</summary>
        [HttpGet]
        public async Task<IActionResult> IndexData([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status,
            [FromQuery] string? employeeTypeId = null) =>
            Ok(await StaffValidation.ListPayloadAsync(_staff, q, departmentId, status, employeeTypeId, includeSalary: IsAdmin));

        /// <summary>This year's leave quotas of one employee (leave_entitlements) with the days taken and left, shown when editing.</summary>
        [HttpGet]
        public async Task<IActionResult> LeaveQuotas(int id)
        {
            if (await _staff.FindAsync(id) == null) return NotFound();
            return Ok(await StaffValidation.LeaveQuotasPayloadAsync(_leaves, id));
        }

        /// <summary>The employee's photo (tbl_Employee.ImageBase64) as an image; 404 when there is none.</summary>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Photo(int id) =>
            await StaffValidation.PhotoFileAsync(_staff, id) is { } photo ? File(photo.Bytes, photo.ContentType) : NotFound();

        private bool IsAdmin => User.IsInRole("Admin");

        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status,
            [FromQuery] string? employeeTypeId = null) =>
            ExportExcel($"staff_{DateTime.UtcNow:yyyyMMddHHmmss}.xls", await StaffValidation.ExportRowsAsync(_staff, q, departmentId, status, employeeTypeId));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateStaffDto? dto)
        {
            if (dto == null) return BadRequest();
            var errors = await ValidateAsync(dto, null);
            if (errors.Count > 0) return BadRequest(new { errors });

            var entity = new Employee { IsActive = 1, CreateOn = DateTime.Now };
            dto.ApplyFormTo(entity);
            await _staff.AddAsync(entity, dto.LeaveQuotas, dto.ToDesignationInput(includeSalary: IsAdmin));

            return Ok(new { employeeId = entity.EmpId, employeeCode = entity.EmployeeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateStaffDto? dto)
        {
            if (dto == null) return BadRequest();
            var entity = await _staff.FindAsync(id);
            if (entity == null) return NotFound();

            var errors = await ValidateAsync(dto, entity);
            if (errors.Count > 0) return BadRequest(new { errors });

            dto.ApplyFormTo(entity);
            StaffValidation.ApplyStatus(dto, entity);
            entity.ModifiedOn = DateTime.Now;
            await _staff.UpdateAsync(entity, dto.ToDesignationInput(includeSalary: IsAdmin));

            // the quotas are the Leaves page's leave_entitlements; only the listed types change
            if (dto.LeaveQuotas is { Count: > 0 })
            {
                var saved = await _leaves.SaveQuotasAsync(entity.EmpId, dto.LeaveQuotas);
                if (saved.ErrorKey != null)
                    return BadRequest(new { errors = new Dictionary<string, string> { ["leave_quotas"] = _L[saved.ErrorKey] } });
            }

            return Ok(new { employeeId = entity.EmpId, employeeCode = entity.EmployeeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _staff.FindAsync(id);
            if (entity == null) return NotFound();

            if (!await _staff.RemoveAsync(entity)) return Conflict(new { error = _L["StaffDeleteInUse"].Value });
            return Ok(new { employeeId = id, employeeCode = entity.EmployeeId });
        }

        private async Task<Dictionary<string, string>> ValidateAsync(CreateStaffDto dto, Employee? existing)
        {
            if (!ModelState.IsValid) return ModelStateErrors(ModelState);
            return await StaffValidation.ValidateAsync(_staff, dto, existing, _L, _leaves);
        }
    }
}
