using IMSB.Dto.Models;
using IMSB.Interfaces;

namespace IMSB.Backend.Mappers
{
    /// <summary>One row of the Staff directory (SERP.tbl_Employee). The Aadhaar number itself is never included.</summary>
    /// <remarks>
    /// EmployeeId is the EMP_ID (the number the Staff URLs use); EmployeeCode is tbl_Employee.EmployeeId, "EMP001", the code
    /// shown to people (the other pages show the same code as Staff.EmployeeId).
    /// DepartmentId/Name is the Staff Category (dbo.departments, tbl_Employee.DepartmentID); DesignationId/Name the designation
    /// (SERP.tbl_Designation, tbl_Employee.Desig). MonthlySalary is the designation's salary (tbl_Designation.BaseSalary), null unless
    /// the caller is an admin. The photo itself is served by Staff/Photo/{id} when HasPhoto.
    /// </remarks>
    public record StaffListItem(int EmployeeId, string FullName, string? EmployeeTypeId, string? EmployeeTypeName,
        string? DepartmentId, string DepartmentName, string? AgencyId, string? AgencyName, string Mobile, string Gender,
        string? ShiftId, string? ShiftName, int? StallNumber, string Status, string? AadhaarMasked, string? Remarks,
        string? JoinDate = null, decimal? MonthlySalary = null, bool HasPhoto = false,
        string? DesignationId = null, string? DesignationName = null, string? EmployeeCode = null);

    /// <summary>One leave type's quota for the year (leave_entitlements), with the days taken and left, as the Staff form shows it.</summary>
    public record LeaveQuotaItem(string LeaveType, decimal AnnualQuota, decimal Taken, decimal Balance);

    /// <summary>A dbo.departments row (a staff category), with how many employees it has.</summary>
    public record DepartmentItem(string Id, string Name, int Count, string Color);

    /// <summary>A tbl_EMP_Types row, with how many employees it has.</summary>
    public record EmployeeTypeItem(string Id, string Name, int Count);

    /// <summary>A dbo.Shift row offered by the form; Id is what tbl_Employee.ShiftID stores.</summary>
    public record StaffShiftOption(string Id, string Name);

    /// <summary>A tbl_Designation row the Designation box suggests. Salary (BaseSalary) is null unless the caller is an admin.</summary>
    public record DesignationOption(string Id, string Name, string? EmployeeTypeId, decimal? Salary);

    public static class StaffMapper
    {
        /// <summary>The salary is left out unless includeSalary (admins only).</summary>
        public static StaffListItem ToListItem(this Employee e, EmployeeLookups lookups, bool includeSalary = false)
        {
            var designation = lookups.Designation(e.DesigId);
            return new(
                e.EmpId, e.EmpName ?? "", e.EmpTypeId?.ToString(), lookups.EmployeeTypeName(e.EmpTypeId),
                e.DepartmentId?.ToString(), lookups.DepartmentName(e.DepartmentId) ?? "", e.AgencyId?.ToString(), lookups.AgencyName(e.AgencyId),
                e.Mobile ?? "", e.Gender ?? "", e.ShiftId?.ToString(), lookups.ShiftName(e.ShiftId), e.StallNum, e.Status, e.MaskedAadhaar, e.Remarks,
                e.JoinDate?.ToString("yyyy-MM-dd"), includeSalary ? designation?.BaseSalary : null, e.HasPhoto,
                e.DesigId?.ToString(), designation?.Name, e.EmployeeId);
        }

        public static LeaveQuotaItem ToQuotaItem(this LeaveBalanceRow b) => new(b.LeaveType, b.Entitlement, b.Taken, b.Balance);

        public static StaffShiftOption ToStaffOption(this Shift s) => new(s.Id.ToString(), string.IsNullOrWhiteSpace(s.Name) ? s.Code : s.Name);

        public static OptionItem ToOption(this ArtisansTypeAgency a) => new(a.AgencyId.ToString(), a.Name ?? a.AgencyId.ToString());

        public static DesignationOption ToOption(this EmployeeDesignation d, bool includeSalary) =>
            new(d.DesigId.ToString(), d.Name ?? "", d.EmpTypeId?.ToString(), includeSalary ? d.BaseSalary : null);

        public static int? ParseId(string? value) => int.TryParse(value?.Trim(), out var id) ? id : null;

        /// <summary>
        /// Copies a validated form onto the employee, with the join date and photo (ids are already known to parse and exist,
        /// the join date and photo to be valid). The designation and its salary go through <see cref="ToDesignationInput"/>.
        /// </summary>
        public static void ApplyFormTo(this CreateStaffDto dto, Employee entity)
        {
            dto.ApplyTo(entity);
            if (IMSB.Common.CommonHelpers.TryParseDate(dto.JoinDate, out var join)) entity.JoinDate = join;
            if (EmployeePhotos.Decode(dto.Photo) is { Length: > 0 } photo) entity.ImageBase64 = Convert.ToBase64String(photo);
            else if (dto.RemovePhoto) entity.ImageBase64 = null;
        }

        /// <summary>Copies a validated form onto the employee: everything but the designation, join date and photo.</summary>
        public static void ApplyTo(this CreateStaffDto dto, Employee entity)
        {
            entity.EmpName = dto.FullName.Trim();
            entity.Mobile = MobileNumbers.Normalize(dto.Mobile);
            if (!string.IsNullOrWhiteSpace(dto.Aadhaar)) entity.Aadhar = Aadhaar.Normalize(dto.Aadhaar);
            entity.Gender = dto.Gender.Trim();
            entity.EmpTypeId = ParseId(dto.EmployeeTypeId);
            entity.DepartmentId = ParseId(dto.DepartmentId);
            entity.AgencyId = ParseId(dto.AgencyId);
            entity.ShiftId = ParseId(dto.ShiftId);
            entity.StallNum = dto.StallNumber;
        }

        /// <summary>
        /// The designation to save in the employee's own tbl_Designation row, or null to keep the one on record (no name typed).
        /// The salary is only written for admins (a blank one clears it); otherwise the one on record stays.
        /// </summary>
        public static DesignationInput? ToDesignationInput(this CreateStaffDto dto, bool includeSalary) =>
            string.IsNullOrWhiteSpace(dto.Designation) ? null
            : new DesignationInput(dto.Designation.Trim(), includeSalary ? dto.MonthlySalary : null, includeSalary);
    }
}
