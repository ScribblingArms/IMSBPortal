using System.Globalization;
using IMSB.Dto.Models;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Backend.Mappers
{
    public record StaffListItem(string EmployeeId, string FullName, string DepartmentId, string DepartmentName, string Designation, string StaffCategory, string Mobile,
        string? Email, string DateOfBirth, string Gender, string JoinDate, string Shift, string Status, decimal? MonthlySalary,
        string? OtFromTime = null, string? OtToTime = null);

    public record DepartmentItem(string Id, string Name, int Count, string Color);

    public static class StaffMapper
    {
        /// <summary>Salary and OT assignment are admin-only and left null otherwise.</summary>
        public static StaffListItem ToListItem(this Staff s, IReadOnlyDictionary<Guid, string> departmentNames, bool includeSalary = false) => new(
            s.EmployeeId, s.FullName, s.DepartmentId.ToString(), departmentNames.GetValueOrDefault(s.DepartmentId) ?? string.Empty, s.Designation,
            s.StaffCategory, s.Mobile, s.Email, s.DateOfBirth.ToString("yyyy-MM-dd"), s.Gender, s.JoinDate.ToString("yyyy-MM-dd"), s.Shift, s.Status,
            includeSalary ? s.MonthlySalary : null,
            includeSalary ? Time(s.OtFromTime) : null, includeSalary ? Time(s.OtToTime) : null);

        public static OptionItem ToOption(this Shift s) => new(s.Code, s.Name);

        private static string? Time(TimeSpan? t) => t?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

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
            entity.StaffCategory = dto.StaffCategory.Trim();
            entity.JoinDate = DateTime.ParseExact(dto.JoinDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            entity.Shift = dto.Shift;
        }

        /// <summary>
        /// OT assignment: both times or neither, and To later than From on the same day.
        /// Returns the failing field and its SharedResource key, or null when valid.
        /// </summary>
        public static (string Field, string Key)? ValidateOvertime(this CreateStaffDto dto)
        {
            var fromGiven = !string.IsNullOrWhiteSpace(dto.OtFromTime);
            var toGiven = !string.IsNullOrWhiteSpace(dto.OtToTime);
            if (!fromGiven && !toGiven) return null;
            if (!fromGiven || !TryParseTime(dto.OtFromTime, out var from)) return ("ot_from_time", fromGiven ? "ValTime" : "ValOtTimes");
            if (!toGiven || !TryParseTime(dto.OtToTime, out var to)) return ("ot_to_time", toGiven ? "ValTime" : "ValOtTimes");
            return to <= from ? ("ot_to_time", "ValOtToAfterFrom") : null;
        }

        /// <summary>Copies the (validated) OT assignment onto the entity; empty clears it.</summary>
        public static void ApplyOvertimeTo(this CreateStaffDto dto, Staff entity)
        {
            entity.OtFromTime = TryParseTime(dto.OtFromTime, out var from) ? from : null;
            entity.OtToTime = TryParseTime(dto.OtToTime, out var to) ? to : null;
        }
    }
}
