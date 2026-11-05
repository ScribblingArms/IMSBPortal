using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models;

/// <summary>The window a staff member may work overtime; at most one per staff member.</summary>
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

public record OtAssignmentRow(Guid Id, Guid StaffId, string EmployeeId, string StaffName, string StaffCategory,
    string Designation, string FromDateTime, string ToDateTime, string? By);

public record OtStaffOption(Guid Id, string EmployeeId, string FullName);
