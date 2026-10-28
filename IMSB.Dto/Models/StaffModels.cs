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
        public string StaffCategory { get; set; } = "General";
        public string JoinDate { get; set; } = "";
        public string Shift { get; set; } = "morning";
        public string Status { get; set; } = "ACTIVE";
        public bool IsActive => Status == "ACTIVE";
        public bool IsOnLeave => Status == "ON_LEAVE";
        public DateTime? ContractEndDate { get; set; } // Optional field for contract end date

    }

    public class CreateStaffDto
    {
        // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)
        [Required(ErrorMessage = "ValFullName"), StringLength(100, MinimumLength = 2, ErrorMessage = "ValFullName")]
        public string FullName { get; set; } = "";
        [Required(ErrorMessage = "ValMobile")]
        [RegularExpression("^[0-9]{9,12}$", ErrorMessage = "ValMobile")]
        public string Mobile { get; set; } = "";
        [EmailAddress(ErrorMessage = "ValEmail"), StringLength(200, ErrorMessage = "ValEmail")]
        public string? Email { get; set; }
        [Required(ErrorMessage = "ValDobRequired")]
        public string DateOfBirth { get; set; } = "";
        [Required, RegularExpression("^(Female|Male|Other)$")]
        public string Gender { get; set; } = "Female";
        [Required(ErrorMessage = "ValDepartment")]
        public string DepartmentId { get; set; } = "";
        [Required(ErrorMessage = "ValDesignation"), StringLength(100, ErrorMessage = "ValDesignation")]
        public string Designation { get; set; } = "";
        [Required(ErrorMessage = "ValStaffCategory"), StringLength(100, ErrorMessage = "ValStaffCategory")]
        public string StaffCategory { get; set; } = "";
        [Range(typeof(decimal), "0", "999999999.99", ErrorMessage = "ValMonthlySalary")]
        public decimal MonthlySalary { get; set; }
        [Required(ErrorMessage = "ValJoinRequired")]
        public string JoinDate { get; set; } = "";
        public string Shift { get; set; } = "";
    }

    public class UpdateStaffDto : CreateStaffDto
    {
        // client must not set Id or EmployeeId
        [RegularExpression("^(ACTIVE|ON_LEAVE|INACTIVE)$", ErrorMessage = "ValStatus")]
        public string? Status { get; set; }
    }
}
