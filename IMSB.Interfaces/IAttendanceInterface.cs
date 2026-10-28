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
    }
}
