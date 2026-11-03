using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>One staff line of a day's attendance sheet.</summary>
    public record SheetRow(Staff Staff, string DepartmentName, string? ShiftName, AttendanceRecord? Record);

    /// <summary>An attendance record's date, status and the department of its staff member.</summary>
    public record AttendanceStatusRow(DateTime Date, string Status, Guid DepartmentId);

    /// <summary>A dashboard activity feed entry (sale, stock change or staff registration).</summary>
    public record DashboardActivity(string Type, DateTime At, string Subject, string? Reference, decimal? Amount, int? Quantity, string? By);

    /// <summary>
    /// A header-bell alert. Id changes whenever the count does, so the page shows a changed alert as unread again.
    /// Severity: danger / warning / info.
    /// </summary>
    public record Notification(string Id, string Type, string Severity, int Count, string Url);

    /// <summary>One row per product per day of the Product-wise Sales Report.</summary>
    public record ProductReportRow(string Code, string Name, string CategoryName, string StallCode, int Quantity,
        decimal UnitPrice, decimal Revenue, int? StockLeft, int? MinStock, string Date);

    /// <summary>A sale line with its entry's number, time and author, for the sales CSV.</summary>
    public record SaleExportRow(string EntryNo, DateTime SoldAt, string? CreatedBy, string ProductCode, string ProductName,
        string CategoryName, string StallCode, int Quantity, decimal UnitPrice, decimal LineTotal);

    /// <summary>A sale line reduced to what the reports' sales tables need.</summary>
    public record SaleLine(Guid SalesEntryId, DateTime SoldAt, string StallCode, int Quantity, decimal LineTotal);

    /// <summary>Applications submitted in a report period, the statuses decided in it and how many are still open.</summary>
    public record ApplicationReportData(List<ArtisanApplication> Submitted, List<string> DecidedStatuses, int StillOpen);

    /// <summary>
    /// Outcome of a backend change: done, not found, or refused with the shared resource key of the reason
    /// (the caller localizes it).
    /// </summary>
    public record BackendResult(bool Found, string? ErrorKey)
    {
        public static readonly BackendResult Done = new(true, null);
        public static readonly BackendResult Missing = new(false, null);
        public static BackendResult Error(string key) => new(true, key);
        public bool Succeeded => Found && ErrorKey == null;
    }

    /// <summary>One employee line of a month's pay bill (saved figures when generated, otherwise worked out from attendance).</summary>
    public sealed record PayBillRow(
        Guid StaffId, string EmployeeId, string EmployeeName, string Department, string Designation,
        int MonthDays, decimal PaidDays, decimal MonthlySalary, decimal BasicPay, decimal Allowances,
        decimal OvertimeHours, decimal OvertimeAmount, decimal Deductions, decimal NetPay);

    /// <summary>A month of an employee's saved pay bill history.</summary>
    public record PayBillHistoryRow(int Year, int Month, decimal PaidDays, decimal BasicPay, decimal Allowances,
        decimal OvertimeAmount, decimal Deductions, decimal NetPay);

    /// <summary>One staff line of the monthly attendance calendar: the status of every day (null = not marked).</summary>
    public record MonthlyCalendarRow(string EmployeeId, string Name, string Type, List<string?> Days);
    public record MonthlyCalendar(string Month, List<int> Days, List<string> Types, List<MonthlyCalendarRow> Rows);

    /// <summary>
    /// One staff line of the monthly report. Week offs are the month's Sundays and working days = total days - week offs.
    /// Present counts Present and Half Day records; OT hours is the month's overtime from attendance.
    /// </summary>
    public record MonthlyAttendanceRow(string EmployeeId, string Name, string Designation, int TotalDaysInMonth, int TotalWorkingDays,
        int WeekOffs, int Present, int Absent, int Leaves, decimal OtHours);
    public record MonthlyAttendanceData(int Year, int Month, int TotalDaysInMonth, List<string> Types, List<MonthlyAttendanceRow> Rows);

    public record SalaryReportRow(string EmployeeId, string Name, string Department, string Designation,
        int MonthDays, decimal WorkedDays, decimal TotalSalary);
    public record SalaryReportData(int Year, int Month, int MonthDays, List<string> Types, List<SalaryReportRow> Rows, decimal GrandTotalSalary);

    public record AbstractSection(string Key, string Name, int Total, int? Attended, int? NotAttended);
    public record AbstractEmployee(string SectionKey, string EmployeeId, string Name, string EmployeeType, string Designation,
        string Gender, string Mobile, bool? Attended, string? AttendanceStatus);
    public record AbstractReportData(string Date, List<AbstractSection> Sections, List<AbstractEmployee> Employees);
}
