using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models;

[Table("leave_entitlements")]
public class LeaveEntitlement
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("staff_id")]
    public Guid? StaffId { get; set; }

    public Staff? Staff { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("staff_category")]
    public string StaffCategory { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("leave_type")]
    public string LeaveType { get; set; } = string.Empty;

    [Column("year")]
    public int Year { get; set; }

    [Column("annual_quota", TypeName = "decimal(8,2)")]
    public decimal AnnualQuota { get; set; }

    [Column("attendance_leave_type")]
    public bool IsAttendanceLeaveType { get; set; }
}

public static class LeaveBalanceRules
{
    public static decimal AttendanceDays(AttendanceRecord record) => record.Status switch
    {
        AttendanceStatuses.Leave => 1m,
        AttendanceStatuses.HalfDay => 0.5m,
        _ => 0m
    };

    public static bool IsCoveredByManualLeave(AttendanceRecord record, IEnumerable<StaffLeaveEntry> entries) =>
        entries.Any(entry => entry.StaffId == record.StaffId && entry.Year == record.AttendanceDate.Year
            && entry.FromDate.Date <= record.AttendanceDate.Date && entry.ToDate.Date >= record.AttendanceDate.Date);

    public static decimal Taken(Guid staffId, int year, string leaveType, bool isAttendanceLeaveType,
        IEnumerable<StaffLeaveEntry> leaveEntries, IEnumerable<AttendanceRecord> attendanceRecords)
    {
        var entries = leaveEntries.Where(entry => entry.StaffId == staffId && entry.Year == year
            && string.Equals(entry.LeaveType, leaveType, StringComparison.OrdinalIgnoreCase)).ToList();
        var attendanceDays = isAttendanceLeaveType
            ? attendanceRecords.Where(record => record.StaffId == staffId
                && record.AttendanceDate.Year == year && !record.LeaveWithoutPay && AttendanceDays(record) > 0
                && !IsCoveredByManualLeave(record, leaveEntries))
                .Sum(AttendanceDays)
            : 0m;
        return entries.Sum(entry => entry.Days) + attendanceDays;
    }
}

[Table("staff_leave_entries")]
public class StaffLeaveEntry
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("staff_id")]
    public Guid StaffId { get; set; }

    public Staff? Staff { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("leave_type")]
    public string LeaveType { get; set; } = string.Empty;

    [Column("year")]
    public int Year { get; set; }

    [Column("from_date", TypeName = "date")]
    public DateTime FromDate { get; set; }

    [Column("to_date", TypeName = "date")]
    public DateTime ToDate { get; set; }

    [Column("days", TypeName = "decimal(6,2)")]
    public decimal Days { get; set; }

    [MaxLength(500)]
    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
}

public class LeaveEntitlementForm
{
    [Required]
    public Guid StaffId { get; set; }

    [Required, StringLength(100)]
    public string LeaveType { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal AnnualQuota { get; set; }

}

public class LeaveEntryForm
{
    public Guid? Id { get; set; }

    [Required]
    public Guid StaffId { get; set; }

    [Required, StringLength(100)]
    public string LeaveType { get; set; } = string.Empty;

    [Required]
    public string FromDate { get; set; } = string.Empty;

    [Required]
    public string ToDate { get; set; } = string.Empty;

    public decimal Days { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }
}

public record LeaveBalanceRow(string EmployeeId, string StaffName, string StaffCategory, string LeaveType,
    decimal Entitlement, decimal Taken, decimal Balance);

public class LeaveManagementViewModel
{
    public int Year { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<LeaveStaffOption> Staff { get; set; } = new();
    public List<LeaveEntitlement> Entitlements { get; set; } = new();
    public List<LeaveBalanceRow> Balances { get; set; } = new();
}

public record LeaveStaffOption(Guid Id, string EmployeeId, string FullName, string StaffCategory);

public class LeaveEntryViewModel
{
    public int Year { get; set; }
    public LeaveEntryForm Form { get; set; } = new();
    public List<LeaveStaffOption> Staff { get; set; } = new();
    public List<LeaveEntitlement> Entitlements { get; set; } = new();
    public List<StaffLeaveEntry> Entries { get; set; } = new();
}
