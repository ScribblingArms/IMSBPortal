using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.Extensions.Localization;

namespace IMSB.Backend
{
    /// <summary>
    /// The Staff form rules and responses shared by IMSB.Admin and IMSB.Api. staff.js validateClient is the browser copy.
    /// tbl_Employee.EmpTypeID is a SERP.tbl_EMP_Types row, DepartmentID a dbo.departments row (the staff category), Desig a
    /// SERP.tbl_Designation row (the designation, holding the salary) and ShiftID a dbo.Shift row.
    /// </summary>
    public static class StaffValidation
    {
        public static readonly string[] Genders = { "Female", "Male", "Other" };

        /// <summary>Errors keyed by the snake_case field names the staff form uses for its err_* elements. existing is null when registering.</summary>
        public static async Task<Dictionary<string, string>> ValidateAsync(IStaffInterface staff, CreateStaffDto dto, Employee? existing, IStringLocalizer l,
            ILeavesInterface? leaves = null)
        {
            var errors = new Dictionary<string, string>();
            var existingId = existing?.EmpId;

            // an unchanged value is accepted so records saved by the SERP app (e.g. "M") can still be edited
            var gender = dto.Gender?.Trim() ?? "";
            var genderUnchanged = existing != null && string.Equals(existing.Gender, gender, StringComparison.OrdinalIgnoreCase);
            if (!genderUnchanged && !Genders.Contains(gender)) errors["gender"] = l["ValGender"];

            // employee type: a tbl_EMP_Types row
            var typeId = StaffMapper.ParseId(dto.EmployeeTypeId);
            var typeUnchanged = existing != null && typeId != null && existing.EmpTypeId == typeId;
            var typeValid = typeId != null && (typeUnchanged || await staff.EmployeeTypeExistsAsync(typeId.Value));
            if (!typeValid) errors["employee_type_id"] = l["ValEmployeeType"];

            // staff category (tbl_Employee.DepartmentID): a dbo.departments row. Required, except on records that have none yet
            // (registered by the SERP app); an unchanged value is kept as it is.
            if (string.IsNullOrWhiteSpace(dto.DepartmentId))
            {
                if (existing == null || existing.DepartmentId != null) errors["department_id"] = l["ValDepartment"];
            }
            else
            {
                var deptId = StaffMapper.ParseId(dto.DepartmentId);
                var deptUnchanged = existing != null && deptId != null && existing.DepartmentId == deptId;
                if (deptId == null || (!deptUnchanged && !await staff.DepartmentExistsAsync(deptId.Value)))
                    errors["department_id"] = l["ValDepartment"];
            }

            // designation (tbl_Employee.Desig -> tbl_Designation): typed; required unless the record already has one, which a blank keeps
            if (string.IsNullOrWhiteSpace(dto.Designation))
            {
                if (existing?.DesigId == null) errors["designation"] = l["ValDesignation"];
            }
            else if (dto.Designation.Trim().Length > EmployeeDesignation.NameMaxLength) errors["designation"] = l["ValDesignation"];

            if (dto.MonthlySalary is < 0m or > EmployeeDesignation.MaxSalary) errors["monthly_salary"] = l["ValDesignationSalary"];

            // shift: a dbo.Shift row the form offers; an unchanged one is kept as it is (a shift no longer offered, or a SERP id)
            if (!string.IsNullOrWhiteSpace(dto.ShiftId))
            {
                var shiftId = StaffMapper.ParseId(dto.ShiftId);
                var shiftUnchanged = existing != null && shiftId != null && existing.ShiftId == shiftId;
                if (shiftId == null || (!shiftUnchanged && !await staff.ShiftOfferedAsync(shiftId.Value))) errors["shift"] = l["ValShift"];
            }

            if (!string.IsNullOrWhiteSpace(dto.AgencyId))
            {
                var agencyId = StaffMapper.ParseId(dto.AgencyId);
                if (agencyId == null || !await staff.AgencyExistsAsync(agencyId.Value)) errors["agency_id"] = l["ValAgency"];
            }

            // an unchanged number is accepted so records saved under older rules can still be edited
            var mobile = MobileNumbers.Normalize(dto.Mobile);
            var mobileUnchanged = existing != null && mobile.Length > 0 && mobile == existing.Mobile;
            if (!mobileUnchanged && !MobileNumbers.IsValid(mobile)) errors["mobile"] = l["ValMobile"];
            else if (await staff.MobileExistsAsync(mobile, existingId)) errors["mobile"] = l["ValMobileExists"];

            if (string.IsNullOrWhiteSpace(dto.Aadhaar))
            {
                // older employees may have none on record, so it is only demanded when registering
                if (existing == null) errors["aadhaar"] = l["ValAadhaar"];
            }
            else if (!Aadhaar.IsValid(dto.Aadhaar)) errors["aadhaar"] = l["ValAadhaar"];
            else if (await staff.AadhaarExistsAsync(Aadhaar.Normalize(dto.Aadhaar), existingId)) errors["aadhaar"] = l["ValAadhaarExists"];

            // join date (tbl_Employee.JoinDate): required, except on SERP records that have none yet
            if (string.IsNullOrWhiteSpace(dto.JoinDate))
            {
                if (existing == null || existing.JoinDate != null) errors["join_date"] = l["ValJoinRequired"];
            }
            else if (!IMSB.Common.CommonHelpers.TryParseDate(dto.JoinDate, out var join)) errors["join_date"] = l["ValJoinInvalid"];
            else if (join > DateTime.Today) errors["join_date"] = l["ValJoinFuture"];

            if (!string.IsNullOrWhiteSpace(dto.Photo) && !EmployeePhotos.IsValid(dto.Photo)) errors["photo"] = l["ValPhoto"];

            if (!await LeaveQuotasValidAsync(staff, dto.LeaveQuotas, allowRemoval: existing != null)) errors["leave_quotas"] = l["ValLeaveQuotas"];
            // on edit the quotas are leave_entitlements with leave possibly taken against them (Leaves page rules)
            else if (existing != null && leaves != null && await leaves.LeaveQuotaProblemAsync(existing.EmpId, dto.LeaveQuotas) is { } problem)
                errors["leave_quotas"] = $"{problem.LeaveType}: {l[problem.ErrorKey]}";

            return errors;
        }

        /// <summary>
        /// False unless each quota is for a different known leave type, in half-days from 0 to 366.
        /// Null days (remove the quota) are only allowed when editing.
        /// </summary>
        public static async Task<bool> LeaveQuotasValidAsync(IStaffInterface staff, IReadOnlyCollection<LeaveQuotaDto>? quotas, bool allowRemoval)
        {
            if (quotas == null || quotas.Count == 0) return true;
            var known = new HashSet<string>(await staff.LeaveTypesAsync(), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return quotas.All(q => q != null
                && (q.AnnualQuota == null ? allowRemoval : q.AnnualQuota is >= 0m and <= 366m && q.AnnualQuota % 0.5m == 0m)
                && known.Contains(q.LeaveType?.Trim() ?? "") && seen.Add(q.LeaveType!.Trim()));
        }

        /// <summary>Copies the status and remarks of an edit onto the employee.</summary>
        public static void ApplyStatus(UpdateStaffDto dto, Employee entity)
        {
            if (!string.IsNullOrWhiteSpace(dto.Status))
                entity.IsActive = dto.Status.Trim().ToUpperInvariant() == EmployeeStatuses.Active ? 1 : 0;
            if (dto.Remarks != null) entity.Remarks = string.IsNullOrWhiteSpace(dto.Remarks) ? null : dto.Remarks.Trim();
        }

        public record Stats(int Total, int Active, int Inactive, int Departments, int AgencyStaff, int EmployeeTypes);

        public static Stats ComputeStats(IReadOnlyCollection<Employee> all) => new(
            all.Count,
            all.Count(e => e.Active),
            all.Count(e => !e.Active),
            all.Where(e => e.DepartmentId != null).Select(e => e.DepartmentId).Distinct().Count(),
            all.Count(e => e.AgencyId != null),
            all.Where(e => e.EmpTypeId != null).Select(e => e.EmpTypeId).Distinct().Count());

        /// <summary>Departments with their employee counts.</summary>
        public static IEnumerable<DepartmentItem> DepartmentItems(IReadOnlyCollection<Employee> all, EmployeeLookups lookups)
        {
            var perDept = all.Where(e => e.DepartmentId != null).GroupBy(e => e.DepartmentId!.Value).ToDictionary(g => g.Key, g => g.Count());
            return lookups.Departments.Select(d => new DepartmentItem(d.Id.ToString(), d.Name, perDept.GetValueOrDefault(d.Id), "#2558a8"));
        }

        /// <summary>Employee types with their employee counts.</summary>
        public static IEnumerable<EmployeeTypeItem> EmployeeTypeItems(IReadOnlyCollection<Employee> all, EmployeeLookups lookups)
        {
            var perType = all.Where(e => e.EmpTypeId != null).GroupBy(e => e.EmpTypeId!.Value).ToDictionary(g => g.Key, g => g.Count());
            return lookups.EmployeeTypes.Select(t => new EmployeeTypeItem(t.EmpTypeId.ToString(), t.Name ?? t.EmpTypeId.ToString(), perType.GetValueOrDefault(t.EmpTypeId)));
        }

        /// <summary>
        /// The body of /Staff/IndexData and GET api/staff: the masters, the leave types, the filtered list and the totals.
        /// Salaries are included for admins only.
        /// </summary>
        public static async Task<object> ListPayloadAsync(IStaffInterface staff, string? q, string? departmentId, string? status, string? employeeTypeId,
            bool includeSalary = false)
        {
            var all = await staff.AllAsync();
            var lookups = await staff.LookupsAsync();
            var filtered = await staff.FilterAsync(q, departmentId, status, employeeTypeId);
            return new
            {
                employeeTypes = EmployeeTypeItems(all, lookups),
                depts = DepartmentItems(all, lookups),
                agencies = lookups.Agencies.Select(a => a.ToOption()),
                shifts = lookups.OfferedShifts.Select(s => s.ToStaffOption()),
                leaveTypes = await staff.LeaveTypesAsync(),
                // tbl_Designation names for the Designation box (salaries for admins only)
                designations = lookups.Designations.Where(d => !string.IsNullOrWhiteSpace(d.Name)).Select(d => d.ToOption(includeSalary)),
                staff = filtered.Select(e => e.ToListItem(lookups, includeSalary)),
                stats = ComputeStats(all)
            };
        }

        /// <summary>The body of Staff/LeaveQuotas/{id} and GET api/staff/{id}/leave-quotas: this year's quotas.</summary>
        public static async Task<object> LeaveQuotasPayloadAsync(ILeavesInterface leaves, int employeeId)
        {
            var year = DateTime.Today.Year;
            var rows = (await leaves.BalancesAsync(year, employeeId.ToString())).OrderBy(b => b.LeaveType, StringComparer.OrdinalIgnoreCase);
            return new { year, rows = rows.Select(b => b.ToQuotaItem()) };
        }

        /// <summary>The photo as a file (JPEG or PNG), or null when there is none or it is not an image.</summary>
        public static async Task<(byte[] Bytes, string ContentType)?> PhotoFileAsync(IStaffInterface staff, int employeeId)
        {
            var bytes = EmployeePhotos.Decode(await staff.PhotoAsync(employeeId));
            if (bytes == null || bytes.Length == 0) return null;
            var type = EmployeePhotos.ContentType(bytes);
            return type == null ? null : (bytes, type);
        }

        public static readonly string[] ExportHeader =
            { "EmployeeId", "FullName", "EmployeeType", "Department", "Designation", "Agency", "Mobile", "Gender", "Shift", "StallNumber", "JoinDate", "Status" };

        /// <summary>No salary or Aadhaar: exports get passed around.</summary>
        public static string?[] ExportRow(StaffListItem s) => new[]
        {
            s.EmployeeCode ?? s.EmployeeId.ToString(), s.FullName, s.EmployeeTypeName, s.DepartmentName, s.DesignationName, s.AgencyName, s.Mobile, s.Gender,
            s.ShiftName, s.StallNumber?.ToString(), s.JoinDate, s.Status
        };

        /// <summary>The rows of the staff export (header first).</summary>
        public static async Task<List<string?[]>> ExportRowsAsync(IStaffInterface staff, string? q, string? departmentId, string? status, string? employeeTypeId)
        {
            var employees = await staff.FilterAsync(q, departmentId, status, employeeTypeId);
            var lookups = await staff.LookupsAsync();
            var rows = new List<string?[]> { ExportHeader };
            rows.AddRange(employees.Select(e => ExportRow(e.ToListItem(lookups))));
            return rows;
        }
    }
}
