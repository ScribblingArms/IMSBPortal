using System.Globalization;
using IMSB.Dto.Models;

namespace IMSB.Admin.Mappers
{
    public record StaffListItem(string EmployeeId, string FullName, string DepartmentId, string DepartmentName, string Designation, string Mobile,
        string? Email, string DateOfBirth, string Gender, string JoinDate, string Shift, string Status);

    public record DepartmentItem(string Id, string Name, int Count, string Color);

    public static class StaffMapper
    {
        public static StaffListItem ToListItem(this Staff s, IReadOnlyDictionary<Guid, string> departmentNames) => new(
            s.EmployeeId, s.FullName, s.DepartmentId.ToString(), departmentNames.GetValueOrDefault(s.DepartmentId) ?? string.Empty, s.Designation,
            s.Mobile, s.Email, s.DateOfBirth.ToString("yyyy-MM-dd"), s.Gender, s.JoinDate.ToString("yyyy-MM-dd"), s.Shift, s.Status);

        public static OptionItem ToOption(this Shift s) => new(s.Code, s.Name);

        /// <summary>Copies a validated form onto the entity (dates are already known to parse).</summary>
        public static void ApplyTo(this CreateStaffDto dto, Staff entity)
        {
            entity.FullName = dto.FullName.Trim();
            entity.Mobile = dto.Mobile.Trim();
            entity.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            entity.DateOfBirth = DateTime.ParseExact(dto.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            entity.Gender = dto.Gender;
            entity.DepartmentId = Guid.Parse(dto.DepartmentId);
            entity.Designation = dto.Designation.Trim();
            entity.JoinDate = DateTime.ParseExact(dto.JoinDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            entity.Shift = dto.Shift;
        }
    }
}
