using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("staff")]
    public class Staff
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("employee_id")]
        public string EmployeeId { get; set; } = string.Empty; // EMP-0001

        [Required]
        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Column("mobile")]
        public string Mobile { get; set; } = string.Empty;

        [Column("email")]
        public string? Email { get; set; }

        [Required]
        [Column("date_of_birth")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Column("gender")]
        public string Gender { get; set; } = "Female";

        [Required]
        [Column("department_id")]
        public Guid DepartmentId { get; set; }

        [Required]
        [Column("designation")]
        public string Designation { get; set; } = string.Empty;

        [Required]
        [Column("join_date")]
        public DateTime JoinDate { get; set; }

        [Required]
        [Column("shift")]
        public string Shift { get; set; } = "morning";

        [Column("status")]
        public string Status { get; set; } = "ACTIVE";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("created_by")]
        public string? CreatedBy { get; set; }

        [Column("updated_by")]
        public string? UpdatedBy { get; set; }
        public bool IsActive => Status == "ACTIVE";
        public bool IsOnLeave => Status == "ON_LEAVE";
        public DateTime? ContractEndDate { get; set; } // Optional field for contract end date
    }
}
