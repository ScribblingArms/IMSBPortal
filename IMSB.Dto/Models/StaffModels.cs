using System.ComponentModel.DataAnnotations;

namespace IMSB.Dto.Models
{
    public class DepartmentDto { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
    public class ShiftDto { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }

    public class StaffDto
    {
        public string Id { get; set; } = ""; // EMP-0001
        public string FullName { get; set; } = "";
        public string Mobile { get; set; } = "";
        public string? Email { get; set; }
        public string DateOfBirth { get; set; } = ""; // ISO yyyy-MM-dd
        public string Gender { get; set; }= "";
        public string DepartmentId { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public string Designation { get; set; } = "";
        public string JoinDate { get; set; } = "";
        public string Shift { get; set; } = "morning";
        public string Status { get; set; } = "ACTIVE";
        public bool IsActive => Status == "ACTIVE";
        public bool IsOnLeave => Status == "ON_LEAVE";
        public DateTime? ContractEndDate { get; set; } // Optional field for contract end date

    }

    /// <summary>The values of the staff form's Employee Type dropdown.</summary>
    public static class EmployeeTypes
    {
        public const string Department = "Department";
        public const string Agency = "Agency";
        public const string Pattern = "^(Department|Agency)$";
    }

    /// <summary>Annual days of one leave type. Null removes the quota, which only editing a staff member allows.</summary>
    public class LeaveQuotaDto
    {
        public string LeaveType { get; set; } = "";
        public decimal? AnnualQuota { get; set; }
    }

    public class CreateStaffDto
    {
        // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)
        [Required(ErrorMessage = "ValFullName"), StringLength(100, MinimumLength = 2, ErrorMessage = "ValFullName")]
        public string FullName { get; set; } = "";
        /// <summary>The format is checked by the controllers, which let an older record keep its unchanged number.</summary>
        [Required(ErrorMessage = "ValMobile")]
        public string Mobile { get; set; } = "";
        [RegularExpression(EmailAddresses.Pattern, ErrorMessage = "ValEmail"), StringLength(200, ErrorMessage = "ValEmail")]
        public string? Email { get; set; }
        /// <summary>Required when registering; on edit an empty value keeps the number on record. Checked by the controllers.</summary>
        public string? Aadhaar { get; set; }
        [Required(ErrorMessage = "ValDobRequired")]
        public string DateOfBirth { get; set; } = "";
        [Required, RegularExpression("^(Female|Male|Other)$")]
        public string Gender { get; set; } = "Female";
        [Required(ErrorMessage = "ValDepartment")]
        public string DepartmentId { get; set; } = "";
        [RegularExpression(EmployeeTypes.Pattern, ErrorMessage = "ValEmployeeType")]
        public string? EmployeeType { get; set; }
        [Required(ErrorMessage = "ValDesignation"), StringLength(100, ErrorMessage = "ValDesignation")]
        public string Designation { get; set; } = "";
        [Range(typeof(decimal), "0", "999999999.99", ErrorMessage = "ValMonthlySalary")]
        public decimal MonthlySalary { get; set; }
        [Required(ErrorMessage = "ValJoinRequired")]
        public string JoinDate { get; set; } = "";
        public string Shift { get; set; } = "";
        /// <summary>This year's leave quotas. On edit only the listed types change; the controllers check them.</summary>
        public List<LeaveQuotaDto>? LeaveQuotas { get; set; }
    }

    public class UpdateStaffDto : CreateStaffDto
    {
        // client must not set Id or EmployeeId
        [RegularExpression("^(ACTIVE|ON_LEAVE|INACTIVE)$", ErrorMessage = "ValStatus")]
        public string? Status { get; set; }
    }
}
