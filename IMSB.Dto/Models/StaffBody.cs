using System;

namespace IMSB.Dto.Models
{
    // View model for rendering staff rows in the Staff Directory
    public class StaffBody
    {
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; }
        public string Status { get; set; } = string.Empty;

        public string JoinDateDisplay => JoinDate == default ? "—" : JoinDate.ToString("dd-MM-yyyy");
    }
}
