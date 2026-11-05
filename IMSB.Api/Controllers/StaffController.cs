using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>Staff / HR: the directory with its figures, and register / edit / delete. Salaries are visible to admins only.</summary>
    [Authorize(Roles = "Staff,Admin")]
    public class StaffController : ApiControllerBase
    {
        private const int ContractExpiryWindowDays = 30;

        private readonly IStaffInterface _staff;

        public StaffController(IStaffInterface staff) => _staff = staff;

        public record StaffStats(int Total, int Active, int OnLeave, int Departments, int ContractExpiring);

        /// <summary>Departments, shifts, the filtered staff list and the totals.</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status)
        {
            var allStaff = await _staff.AllAsync();
            var departments = await _staff.DepartmentsAsync();
            var perDept = allStaff.GroupBy(s => s.DepartmentId).ToDictionary(g => g.Key, g => g.Count());
            var deptNames = departments.ToDictionary(d => d.Id, d => d.Name);

            return Ok(new
            {
                depts = departments.Select(d => new DepartmentItem(d.Id.ToString(), d.Name, perDept.GetValueOrDefault(d.Id), "#2558a8")),
                shifts = (await _staff.ShiftsAsync()).Select(s => s.ToOption()),
                staff = (await _staff.FilterAsync(q, departmentId, status)).Select(s => s.ToListItem(deptNames, User.IsInRole("Admin"))),
                stats = ComputeStats(allStaff)
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var staff = await _staff.FindAsync(id);
            return staff == null ? NotFound() : Ok(staff.ToListItem(await _staff.DepartmentNamesAsync(), User.IsInRole("Admin")));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateStaffDto dto)
        {
            var errors = await ValidateAsync(dto, null, null, null);
            if (errors.Count > 0) return Invalid(errors);

            var entity = new Staff
            {
                // next number after the highest existing one, in the EMP001 format used by existing records
                EmployeeId = NextCode(await _staff.EmployeeIdsAsync(), "EMP", 3),
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };
            dto.ApplyTo(entity);
            if (User.IsInRole("Admin")) entity.MonthlySalary = dto.MonthlySalary;
            await _staff.AddAsync(entity);
            return Ok(new { employeeId = entity.EmployeeId });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateStaffDto dto)
        {
            var entity = await _staff.FindAsync(id);
            if (entity == null) return NotFound();
            var errors = await ValidateAsync(dto, entity.Id, entity.Shift, entity.Mobile);
            if (errors.Count > 0) return Invalid(errors);

            dto.ApplyTo(entity);
            if (User.IsInRole("Admin")) entity.MonthlySalary = dto.MonthlySalary;
            if (!string.IsNullOrWhiteSpace(dto.Status)) entity.Status = dto.Status.Trim().ToUpperInvariant();
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = UserName;
            await _staff.UpdateAsync(entity);
            return Ok(new { employeeId = entity.EmployeeId });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var entity = await _staff.FindAsync(id);
            if (entity == null) return NotFound();
            await _staff.RemoveAsync(entity);
            return Ok(new { employeeId = id });
        }

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

        private async Task<Dictionary<string, string>> ValidateAsync(CreateStaffDto dto, Guid? existingId, string? currentShift, string? currentMobile)
        {
            var errors = new Dictionary<string, string>();
            var today = DateTime.Today;
            if (!TryParseDate(dto.DateOfBirth, out var dob)) errors["date_of_birth"] = L["ValDobInvalid"];
            else if (dob > today) errors["date_of_birth"] = L["ValDobFuture"];
            else if (dob.AddYears(18) > today) errors["date_of_birth"] = L["ValMinAge"];

            if (!TryParseDate(dto.JoinDate, out var join)) errors["join_date"] = L["ValJoinInvalid"];
            else if (join > today) errors["join_date"] = L["ValJoinFuture"];
            else if (!errors.ContainsKey("date_of_birth") && join < dob.AddYears(18)) errors["join_date"] = L["ValJoinAfterDob"];

            if (!Guid.TryParse(dto.DepartmentId, out var deptId) || !await _staff.DepartmentExistsAsync(deptId))
                errors["department_id"] = L["ValDepartment"];

            // an unchanged shift is accepted so records holding older values (e.g. "Night") can still be edited
            var shiftUnchanged = currentShift != null && string.Equals(currentShift, dto.Shift, StringComparison.OrdinalIgnoreCase);
            if (!shiftUnchanged && await _staff.AnyShiftsAsync() && !await _staff.ShiftExistsAsync(dto.Shift))
                errors["shift"] = L["ValShift"];

            // an unchanged number is accepted so records saved under the older 9-12 digit rule can still be edited
            var mobile = MobileNumbers.Normalize(dto.Mobile);
            if (mobile != currentMobile && !MobileNumbers.IsValid(mobile)) errors["mobile"] = L["ValMobile"];
            else if (await _staff.MobileExistsAsync(mobile, existingId)) errors["mobile"] = L["ValMobileExists"];

            if (string.IsNullOrWhiteSpace(dto.Aadhaar))
            {
                // existing staff may have none on record, so it is only demanded when registering
                if (existingId == null) errors["aadhaar"] = L["ValAadhaar"];
            }
            else if (!Aadhaar.IsValid(dto.Aadhaar)) errors["aadhaar"] = L["ValAadhaar"];
            else if (await _staff.AadhaarExistsAsync(Aadhaar.Hash(dto.Aadhaar), existingId)) errors["aadhaar"] = L["ValAadhaarExists"];

            var email = dto.Email?.Trim().ToLower();
            if (!string.IsNullOrEmpty(email) && await _staff.EmailExistsAsync(email, existingId)) errors["email"] = L["ValEmailExists"];
            return errors;
        }
    }
}
