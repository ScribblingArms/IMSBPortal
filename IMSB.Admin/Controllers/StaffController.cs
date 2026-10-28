using IMSB.Admin.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Text;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers
{
    [Authorize(Roles = "Staff,Admin")]
    public class StaffController : Controller
    {
        private const int ContractExpiryWindowDays = 30;

        private readonly IStaffInterface _staff;
        private readonly IStringLocalizer _L;

        public StaffController(IStaffInterface staff, IStringLocalizerFactory localizerFactory)
        {
            _staff = staff;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(StaffController).Assembly.GetName().Name!);
        }

        public async Task<IActionResult> Index([FromQuery] bool register = false)
        {
            var staffEntities = await _staff.AllAsync();

            // Dashboard counts
            var stats = ComputeStats(staffEntities);
            ViewBag.TotalStaff = stats.Total;
            ViewBag.ActiveStaff = stats.Active;
            ViewBag.OnLeave = stats.OnLeave;
            ViewBag.Departments = stats.Departments;
            ViewBag.ContractExpiring = stats.ContractExpiring;
            ViewBag.RegisterOnLoad = register;

            return View(staffEntities);
        }

        internal record StaffStats(int Total, int Active, int OnLeave, int Departments, int ContractExpiring);

        private static StaffStats ComputeStats(List<Staff> all)
        {
            var today = DateTime.Today;
            var soon = today.AddDays(ContractExpiryWindowDays);
            return new(
                all.Count,
                all.Count(x => string.Equals(x.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)),
                all.Count(x => string.Equals(x.Status, "ON_LEAVE", StringComparison.OrdinalIgnoreCase)),
                all.Select(x => x.DepartmentId).Distinct().Count(),
                all.Count(x => x.ContractEndDate >= today && x.ContractEndDate <= soon));
        }

        // Returns JSON used by client-side initialization (depts, shifts, staff list)
        [HttpGet]
        public async Task<IActionResult> IndexData([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status)
        {
            // all staff for the totals; the list itself is filtered
            var allStaff = await _staff.AllAsync();
            var departments = await _staff.DepartmentsAsync();
            var shifts = await _staff.ShiftsAsync();
            var filtered = await _staff.FilterAsync(q, departmentId, status);

            var perDept = allStaff.GroupBy(s => s.DepartmentId).ToDictionary(g => g.Key, g => g.Count());
            var deptNames = departments.ToDictionary(d => d.Id, d => d.Name);

            return Ok(new
            {
                depts = departments.Select(d => new DepartmentItem(d.Id.ToString(), d.Name, perDept.GetValueOrDefault(d.Id), "#2558a8")),
                shifts = shifts.Select(s => s.ToOption()),
                staff = filtered.Select(s => s.ToListItem(deptNames, User.IsInRole("Admin"))),
                stats = ComputeStats(allStaff)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status)
        {
            var staff = await _staff.FilterAsync(q, departmentId, status);
            var depts = await _staff.DepartmentNamesAsync();

            var rows = new List<string?[]>
            {
                new string?[] { "EmployeeId", "FullName", "Department", "Designation", "StaffCategory", "Mobile", "Email", "DateOfBirth", "Gender", "JoinDate", "Shift", "Status" }
            };
            foreach (var s in staff.Select(s => s.ToListItem(depts)))
            {
                rows.Add(new string?[] { s.EmployeeId, s.FullName, s.DepartmentName, s.Designation, s.StaffCategory, s.Mobile, s.Email, s.DateOfBirth, s.Gender,
                    s.JoinDate, s.Shift, s.Status });
            }
            return ExportExcel($"staff_{DateTime.UtcNow:yyyyMMddHHmmss}.xls", rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateStaffDto? dto)
        {
            if (dto == null) return BadRequest();
            var errors = await ValidateStaffAsync(dto, null, null);
            if (errors.Count > 0) return BadRequest(new { errors });

            var entity = new Staff
            {
                // next number after the highest existing one, in the EMP001 format used by existing records
                EmployeeId = NextCode(await _staff.EmployeeIdsAsync(), "EMP", 3),
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name
            };
            dto.ApplyTo(entity);
            if (User.IsInRole("Admin")) entity.MonthlySalary = dto.MonthlySalary;
            await _staff.AddAsync(entity);

            return Ok(new { employeeId = entity.EmployeeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateStaffDto? dto)
        {
            if (dto == null) return BadRequest();
            var entity = await _staff.FindAsync(id);
            if (entity == null) return NotFound();

            var errors = await ValidateStaffAsync(dto, entity.Id, entity.Shift);
            if (errors.Count > 0) return BadRequest(new { errors });

            dto.ApplyTo(entity);
            if (User.IsInRole("Admin")) entity.MonthlySalary = dto.MonthlySalary;
            if (!string.IsNullOrWhiteSpace(dto.Status)) entity.Status = dto.Status.Trim().ToUpperInvariant();
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = User.Identity?.Name;
            await _staff.UpdateAsync(entity);

            return Ok(new { employeeId = entity.EmployeeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var entity = await _staff.FindAsync(id);
            if (entity == null) return NotFound();

            await _staff.RemoveAsync(entity);
            return Ok(new { employeeId = id });
        }

        // Errors are keyed by the snake_case field names the staff form uses for its err_* elements
        private async Task<Dictionary<string, string>> ValidateStaffAsync(CreateStaffDto dto, Guid? existingId, string? currentShift)
        {
            if (!ModelState.IsValid) return ModelStateErrors(ModelState);
            var errors = new Dictionary<string, string>();

            var today = DateTime.Today;
            if (!TryParseDate(dto.DateOfBirth, out var dob)) errors["date_of_birth"] = _L["ValDobInvalid"];
            else if (dob > today) errors["date_of_birth"] = _L["ValDobFuture"];
            else if (dob.AddYears(18) > today) errors["date_of_birth"] = _L["ValMinAge"];

            if (!TryParseDate(dto.JoinDate, out var join)) errors["join_date"] = _L["ValJoinInvalid"];
            else if (join > today) errors["join_date"] = _L["ValJoinFuture"];
            else if (!errors.ContainsKey("date_of_birth") && join < dob.AddYears(18)) errors["join_date"] = _L["ValJoinAfterDob"];

            if (!Guid.TryParse(dto.DepartmentId, out var deptId) || !await _staff.DepartmentExistsAsync(deptId))
                errors["department_id"] = _L["ValDepartment"];

            // an unchanged shift is accepted so records holding older values (e.g. "Night") can still be edited
            var shiftUnchanged = currentShift != null && string.Equals(currentShift, dto.Shift, StringComparison.OrdinalIgnoreCase);
            if (!shiftUnchanged && await _staff.AnyShiftsAsync() && !await _staff.ShiftExistsAsync(dto.Shift))
                errors["shift"] = _L["ValShift"];

            if (await _staff.MobileExistsAsync(dto.Mobile.Trim(), existingId))
                errors["mobile"] = _L["ValMobileExists"];

            var email = dto.Email?.Trim().ToLower();
            if (!string.IsNullOrEmpty(email) && await _staff.EmailExistsAsync(email, existingId))
                errors["email"] = _L["ValEmailExists"];

            return errors;
        }
    }
}
