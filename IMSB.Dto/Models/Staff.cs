using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("staff")]
    public class Staff
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("EmployeeId")]
        public string EmployeeId { get; set; } = string.Empty; // EMP-0001

        [Required]
        [Column("FullName")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Column("Mobile")]
        public string Mobile { get; set; } = string.Empty;

        [Column("Email")]
        public string? Email { get; set; }

        /// <summary>As for artisans, the Aadhaar number itself is never stored. Null for staff registered before it was captured.</summary>
        [MaxLength(64)]
        [Column("AadhaarHash")]
        public string? AadhaarHash { get; set; }

        [MaxLength(4)]
        [Column("AadhaarLast4")]
        public string? AadhaarLast4 { get; set; }

        [Required]
        [Column("DateOfBirth")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Column("Gender")]
        public string Gender { get; set; } = "Female";

        [Required]
        [Column("DepartmentId")]
        public Guid DepartmentId { get; set; }

        [Required]
        [Column("Designation")]
        public string Designation { get; set; } = string.Empty;

        /// <summary>Retired free-text column, kept so existing data is not dropped. "Staff Category" on screen is the department.</summary>
        [Required]
        [MaxLength(100)]
        [Column("StaffCategory")]
        public string StaffCategory { get; set; } = "General";

        [Column("MonthlySalary", TypeName = "decimal(18,2)")]
        public decimal MonthlySalary { get; set; }

        [Required]
        [Column("JoinDate")]
        public DateTime JoinDate { get; set; }

        [Required]
        [Column("Shift")]
        public string Shift { get; set; } = "morning";

        [Column("Status")]
        public string Status { get; set; } = "ACTIVE";

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [Column("CreatedBy")]
        public string? CreatedBy { get; set; }

        [Column("UpdatedBy")]
        public string? UpdatedBy { get; set; }
        public string? MaskedAadhaar => AadhaarLast4 == null ? null : "XXXX-XXXX-" + AadhaarLast4;
        public bool IsActive => Status == "ACTIVE";
        public bool IsOnLeave => Status == "ON_LEAVE";
        public DateTime? ContractEndDate { get; set; } // Optional field for contract end date
    }
}
