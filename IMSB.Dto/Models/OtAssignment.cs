using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models;

/// <summary>The window a staff member may work overtime on one date; at most one for today or later per staff member.</summary>
[Table("ot_assignments")]
public class OtAssignment
{
    [Key]
    [Column("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("StaffId")]
    public Guid StaffId { get; set; }

    public Staff? Staff { get; set; }

    [Column("FromDateTime")]
    public DateTime FromDateTime { get; set; }

    [Column("ToDateTime")]
    public DateTime ToDateTime { get; set; }

    /// <summary>
    /// The assignment's OT hours (To − From, e.g. 16:00–20:00 = 4), saved when it is added or edited. The Monthly Reports,
    /// Salary Report and Pay Bill total these per month (by the OT date) and the Pay Bill pays them (Housekeeping: × ₹50).
    /// </summary>
    [Column("OtHours", TypeName = "decimal(5,2)")]
    public decimal OtHours { get; set; }

    /// <summary>Hours between two date-times, to 2 decimal places (15 minutes = 0.25).</summary>
    public static decimal HoursBetween(DateTime from, DateTime to) =>
        to <= from ? 0m : Math.Round((decimal)(to - from).TotalMinutes / 60m, 2, MidpointRounding.AwayFromZero);

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

/// <summary>The add / edit form. StaffId is only read when adding; date-times are yyyy-MM-ddTHH:mm.</summary>
public class OtAssignmentDto
{
    public Guid? StaffId { get; set; }
    public string? FromDateTime { get; set; }
    public string? ToDateTime { get; set; }
}

/// <summary>
/// ShiftName is the staff member's shift (null when none is set); ShiftStart / ShiftEnd are HH:mm, null when the timings
/// cannot be read from the shift name (OT is then not checked against the shift).
/// </summary>
public record OtAssignmentRow(Guid Id, Guid StaffId, string EmployeeId, string StaffName, string StaffCategory,
    string Designation, string FromDateTime, string ToDateTime, string? By,
    string? ShiftName = null, string? ShiftStart = null, string? ShiftEnd = null, decimal OtHours = 0m);

public record OtStaffOption(Guid Id, string EmployeeId, string FullName,
    string? ShiftName = null, string? ShiftStart = null, string? ShiftEnd = null);
