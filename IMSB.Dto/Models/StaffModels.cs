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

    /// <summary>The Staff form, saved to SERP.tbl_Employee.</summary>
    public class CreateStaffDto
    {
        // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)
        [Required(ErrorMessage = "ValFullName"), StringLength(100, MinimumLength = 2, ErrorMessage = "ValFullName")]
        public string FullName { get; set; } = "";
        /// <summary>The format is checked by the controllers, which let an older record keep its unchanged number.</summary>
        [Required(ErrorMessage = "ValMobile")]
        public string Mobile { get; set; } = "";
        /// <summary>Required when registering; on edit an empty value keeps the number on record. Checked by the controllers.</summary>
        public string? Aadhaar { get; set; }
        /// <summary>Female, Male or Other; checked by the controllers, which let an older record keep its unchanged value.</summary>
        public string Gender { get; set; } = "Female";
        /// <summary>SERP.tbl_EMP_Types.EmpTypeID, saved as tbl_Employee.EmpTypeID. The department and shift must be of this type.</summary>
        [Required(ErrorMessage = "ValEmployeeType")]
        public string EmployeeTypeId { get; set; } = "";
        /// <summary>dbo.departments.Id (the Staff Category), saved as tbl_Employee.DepartmentID. Checked by the controllers.</summary>
        public string? DepartmentId { get; set; }
        /// <summary>
        /// The designation's name: the SERP.tbl_Designation row of that name is used (or added), and its DesigID saved as
        /// tbl_Employee.Desig. Required when registering; checked by the controllers.
        /// </summary>
        [StringLength(EmployeeDesignation.NameMaxLength, ErrorMessage = "ValDesignation")]
        public string? Designation { get; set; }
        /// <summary>Optional SERP.tbl_ArtisansTypeAgency.AgencyID.</summary>
        public string? AgencyId { get; set; }
        /// <summary>Optional SERP.tbl_Shifts.ShiftID, saved as tbl_Employee.ShiftID; one of the shifts of the employee type.</summary>
        public string? ShiftId { get; set; }
        [Range(1, 99999, ErrorMessage = "ValStallNumber")]
        public int? StallNumber { get; set; }
        /// <summary>yyyy-MM-dd, tbl_Employee.JoinDate. Required when registering; checked by the controllers.</summary>
        public string? JoinDate { get; set; }
        /// <summary>
        /// The designation's monthly salary, saved in tbl_Designation.BaseSalary (so it is the same for everyone with that designation).
        /// Only an admin's value is saved; null leaves the designation's salary as it is.
        /// </summary>
        [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "ValDesignationSalary")]
        public decimal? MonthlySalary { get; set; }
        /// <summary>This year's quotas (employee_leave_quotas). On edit only the listed types change; null days removes one.</summary>
        public List<LeaveQuotaDto>? LeaveQuotas { get; set; }
        /// <summary>A new photo for tbl_Employee.ImageBase64: a JPEG/PNG data URL or plain base64. Null keeps the photo on record.</summary>
        public string? Photo { get; set; }
        /// <summary>True clears the photo on record (ignored when a new one is sent).</summary>
        public bool RemovePhoto { get; set; }
    }

    public class UpdateStaffDto : CreateStaffDto
    {
        // client must not set the employee id
        [RegularExpression(EmployeeStatuses.Pattern, ErrorMessage = "ValStatus")]
        public string? Status { get; set; }
        [StringLength(200, ErrorMessage = "ValEmployeeRemarks")]
        public string? Remarks { get; set; }
    }
}
