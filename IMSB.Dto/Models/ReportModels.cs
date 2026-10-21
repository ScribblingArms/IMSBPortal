namespace IMSB.Dto.Models
{
    /// <summary>
    /// A report as a list of titled tables. The same document is written out as CSV or rendered as a printable
    /// page (Views/Reports/Print.cshtml, saved as PDF from the browser's print dialog).
    /// </summary>
    public record ReportDocument(string Title, string Period, string GeneratedBy, DateTime GeneratedAt, List<ReportTable> Tables);

    public record ReportTable(string Title, string[] Headers, List<string?[]> Rows, string? Note = null)
    {
        /// <summary>Column indexes holding numbers (right-aligned in print).</summary>
        public HashSet<int> NumericColumns { get; init; } = new();
    }

    /// <summary>Headline figures for one month (and optionally one craft category).</summary>
    public record ReportKpis(
        string Month,
        decimal TotalSales,
        decimal? SalesChangePct,
        int OccupiedStalls,
        int TotalStalls,
        int OccupancyPct,
        int ActiveArtisans,
        decimal? AvgAttendancePct,   // no attendance table yet: always null ("—")
        int ApplicationsApproved,
        int LowStockItems);

    public record StallReportRow(
        string StallCode, string Block, string? ArtisanCode, string? ArtisanName, string? Category,
        decimal MonthlySales, int Products, int StockUnits, int OccupancyDays, int DaysInPeriod, string Status);

    public record ArtisanReportRow(
        string ArtisanCode, string Name, string District, string? Category, string? StallCode,
        decimal MonthlySales, int UnitsSold, int Products, decimal SharePct, int Rating, string Status);

    /// <summary>The reporting window for a month: [From, To) with To capped at tomorrow for the current month.</summary>
    public record ReportPeriod(DateTime MonthStart, DateTime From, DateTime To)
    {
        public DateTime LastDay => To.AddDays(-1);
        public int Days => (To - From).Days;
        public string Key => MonthStart.ToString("yyyy-MM");

        /// <summary>"yyyy-MM"; anything unreadable, in the future or older than ten years means the current month.</summary>
        public static ReportPeriod ForMonth(string? month, DateTime today)
        {
            var current = new DateTime(today.Year, today.Month, 1);
            var start = current;
            if (DateTime.TryParseExact(month?.Trim(), "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed) && parsed <= current && parsed >= current.AddYears(-10))
                start = parsed;
            var end = start.AddMonths(1);
            return new ReportPeriod(start, start, end > today.AddDays(1) ? today.AddDays(1) : end);
        }
    }

    /// <summary>Report ids accepted by /Reports/Export/{id}.</summary>
    public static class ReportIds
    {
        public const string Kpis = "kpis";
        public const string SalesTrend = "sales-trend";
        public const string CategoryRevenue = "category-revenue";
        public const string OccupancyTrend = "occupancy-trend";
        public const string Stalls = "stalls";
        public const string Artisans = "artisans";
        public const string Applications = "applications";
        public const string Inventory = "inventory";
        public const string Sales = "sales";
        public const string All = "all";

        public static readonly string[] Every = { Kpis, SalesTrend, CategoryRevenue, OccupancyTrend, Stalls, Artisans, Applications, Inventory, Sales, All };
    }
}
