using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface IAttendanceInterface
    {
        /// <summary>Departments by name.</summary>
        Task<List<Department>> DepartmentsAsync();
        Task<Dictionary<Guid, string>> DepartmentNamesAsync();

        /// <summary>Shift names by upper-cased code (staff rows may hold "morning" or "MORNING").</summary>
        Task<Dictionary<string, string>> ShiftNamesAsync();

        /// <summary>
        /// The day's sheet: staff who were employed and not inactive that day, plus anyone already marked,
        /// by department, name and employee id.
        /// </summary>
        Task<List<SheetRow>> SheetAsync(DateTime day);

        /// <summary>Status and department of every record dated from..to (inclusive).</summary>
        Task<List<AttendanceStatusRow>> StatusesBetweenAsync(DateTime from, DateTime to);

        /// <summary>Staff employed by the end of the period, including inactive staff marked during it.</summary>
        Task<List<Staff>> MonthlyStaffAsync(DateTime from, DateTime to);

        /// <summary>Tracked staff by employee id.</summary>
        Task<List<Staff>> StaffByEmployeeIdsAsync(List<string> employeeIds);

        /// <summary>Tracked records of the day for the given staff, by staff id.</summary>
        Task<Dictionary<Guid, AttendanceRecord>> RecordsOnAsync(DateTime day, List<Guid> staffIds);

        /// <summary>Record with its staff member.</summary>
        Task<AttendanceRecord?> FindAsync(Guid id, bool tracking);

        /// <summary>Records (with staff) in the period, newest first, filtered by department, status and name / employee id.</summary>
        Task<List<AttendanceRecord>> HistoryAsync(string? q, string? departmentId, string? status, DateTime from, DateTime to);

        /// <summary>Saves a marked sheet: inserts the new records and the changes made to the tracked existing ones.</summary>
        Task SaveSheetAsync(IEnumerable<AttendanceRecord> newRecords);

        /// <summary>Saves the changes made to a tracked record.</summary>
        Task UpdateAsync(AttendanceRecord record);

        Task RemoveAsync(AttendanceRecord record);

        /// <summary>
        /// Marks each Leave / Half Day record as loss of pay when it is not covered by recorded leave and the staff
        /// member's attendance leave policy has no balance left. Returns how many records are loss of pay.
        /// </summary>
        Task<int> ApplyLeavePolicyAsync(IReadOnlyCollection<AttendanceRecord> records);

        /// <summary>The month's calendar; type = designation, status = a status code or NOT_MARKED (rows with such a day).</summary>
        Task<MonthlyCalendar> MonthlyCalendarAsync(DateTime monthStart, string? type, string? q, string? status);

        /// <summary>Days worked (present or half day) per staff member in the month; type = designation.</summary>
        Task<MonthlyAttendanceData> MonthlyAttendanceReportAsync(DateTime monthStart, string? type);

        /// <summary>Salary prorated by days worked; type = department name, q matches id, name, department or designation.</summary>
        Task<SalaryReportData> SalaryReportAsync(DateTime monthStart, string? type, string? q);

        /// <summary>The day's head count: artisans, then staff per department with who attended.</summary>
        Task<AbstractReportData> AbstractReportAsync(DateTime date, string staffFallbackSection, string artisansLabel);
    }
}
