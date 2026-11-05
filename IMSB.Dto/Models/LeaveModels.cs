using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models;

/// <summary>The leave types offered when applying for leave.</summary>
[Table("leave_types")]
public class LeaveType
{
    [Key]
    [Column("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;
}

[Table("leave_entitlements")]
public class LeaveEntitlement
{
    [Key]
    [Column("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("StaffId")]
    public Guid? StaffId { get; set; }

    public Staff? Staff { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("StaffCategory")]
    public string StaffCategory { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("LeaveType")]
    public string LeaveType { get; set; } = string.Empty;

    [Column("Year")]
    public int Year { get; set; }

    [Column("AnnualQuota", TypeName = "decimal(8,2)")]
    public decimal AnnualQuota { get; set; }

    [Column("AttendanceLeaveType")]
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
    [Column("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("StaffId")]
    public Guid StaffId { get; set; }

    public Staff? Staff { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("LeaveType")]
    public string LeaveType { get; set; } = string.Empty;

    [Column("Year")]
    public int Year { get; set; }

    [Column("FromDate", TypeName = "date")]
    public DateTime FromDate { get; set; }

    [Column("ToDate", TypeName = "date")]
    public DateTime ToDate { get; set; }

    [Column("Days", TypeName = "decimal(6,2)")]
    public decimal Days { get; set; }

    [MaxLength(500)]
    [Column("Remarks")]
    public string? Remarks { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    [Column("CreatedBy")]
    public string? CreatedBy { get; set; }

    [Column("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("UpdatedBy")]
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
    public List<string> LeaveTypes { get; set; } = new();
    public List<LeaveEntitlement> Entitlements { get; set; } = new();
    public List<StaffLeaveEntry> Entries { get; set; } = new();
}
