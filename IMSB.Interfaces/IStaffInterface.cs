using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>
    /// The master lists of the Staff form: employee types (SERP.tbl_EMP_Types), staff categories (dbo.departments),
    /// agencies (SERP.tbl_ArtisansTypeAgency) and shifts (dbo.Shift; every shift, including ones no longer offered,
    /// so names on record still resolve).
    /// </summary>
    public record EmployeeLookups(
        List<EmployeeTypeMaster> EmployeeTypes,
        List<Department> Departments,
        List<ArtisansTypeAgency> Agencies,
        List<Shift> Shifts,
        List<EmployeeDesignation> Designations)
    {
        public EmployeeDesignation? Designation(int? id) => id == null ? null : Designations.FirstOrDefault(d => d.DesigId == id);
        public string? DepartmentName(int? id) => id == null ? null : Departments.FirstOrDefault(d => d.Id == id)?.Name;
        public string? AgencyName(int? id) => id == null ? null : Agencies.FirstOrDefault(a => a.AgencyId == id)?.Name;
        public string? ShiftName(int? id) => id == null ? null : Shifts.FirstOrDefault(s => s.Id == id)?.Name;
        public string? EmployeeTypeName(int? id) => id == null ? null : EmployeeTypes.FirstOrDefault(t => t.EmpTypeId == id)?.Name;

        /// <summary>The shifts the form offers (DisplayOrder &gt; 0), in display order.</summary>
        public IEnumerable<Shift> OfferedShifts => Shifts.Where(s => s.DisplayOrder > 0).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name);
    }

    /// <summary>
    /// The designation typed on the Staff form, saved in the employee's own SERP.tbl_Designation row (EmpId = EMP_ID).
    /// Salary (BaseSalary) is only written when SetSalary (admins); null then clears it.
    /// </summary>
    public record DesignationInput(string Name, decimal? Salary, bool SetSalary);

    /// <summary>
    /// The Staff page: employees in SERP.tbl_Employee. Their EmpTypeID is a SERP.tbl_EMP_Types row, Desig a SERP.tbl_Designation
    /// row (the designation, which holds the salary), DepartmentID a dbo.departments row (the staff category), ShiftID a dbo.Shift
    /// row and AgencyID a SERP.tbl_ArtisansTypeAgency row.
    /// </summary>
    public interface IStaffInterface
    {
        /// <summary>Every employee, read-only.</summary>
        Task<List<Employee>> AllAsync();

        /// <summary>
        /// Filtered employees, newest first. q matches name, mobile or employee id; status is ACTIVE or INACTIVE;
        /// employeeTypeId and departmentId are ids (anything else is ignored).
        /// </summary>
        Task<List<Employee>> FilterAsync(string? q, string? departmentId, string? status, string? employeeTypeId = null);

        Task<EmployeeLookups> LookupsAsync();

        /// <summary>Tracked employee by EMP_ID.</summary>
        Task<Employee?> FindAsync(int employeeId);

        Task<bool> EmployeeTypeExistsAsync(int employeeTypeId);
        /// <summary>True for a dbo.departments row (a staff category).</summary>
        Task<bool> DepartmentExistsAsync(int departmentId);

        /// <summary>True for a dbo.Shift row the form offers (DisplayOrder &gt; 0).</summary>
        Task<bool> ShiftOfferedAsync(int shiftId);

        Task<bool> AgencyExistsAsync(int agencyId);

        Task<bool> MobileExistsAsync(string mobile, int? exceptId);

        /// <summary>aadhaar is the 12 digits.</summary>
        Task<bool> AadhaarExistsAsync(string aadhaar, int? exceptId);

        /// <summary>The leave types (leave_types) a quota can be given for, by name.</summary>
        Task<List<string>> LeaveTypesAsync();

        /// <summary>The photo saved in tbl_Employee.ImageBase64, or null.</summary>
        Task<string?> PhotoAsync(int employeeId);

        /// <summary>
        /// Saves a new employee with this year's leave quotas (leave_entitlements; null days are skipped). A designation,
        /// when given, gets the employee's own tbl_Designation row, which becomes tbl_Employee.Desig.
        /// </summary>
        Task AddAsync(Employee employee, IReadOnlyCollection<LeaveQuotaDto>? quotas = null, DesignationInput? designation = null);

        /// <summary>
        /// Saves the changes made to a tracked employee. A designation, when given, is written to the employee's own
        /// tbl_Designation row (added if they have none yet); null keeps the one on record. Quotas are saved through ILeavesInterface.
        /// </summary>
        Task UpdateAsync(Employee employee, DesignationInput? designation = null);

        /// <summary>
        /// Deletes the employee with their own tbl_Designation rows and their attendance, OT and leave rows.
        /// False when the database refuses, e.g. the SERP app has attendance for them.
        /// </summary>
        Task<bool> RemoveAsync(Employee employee);
    }
}
