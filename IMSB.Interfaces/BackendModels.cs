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
}
