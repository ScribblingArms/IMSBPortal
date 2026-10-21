namespace IMSB.Dto.Models
{
    /// <summary>Dashboard header cards. Null = the module behind it (artisans, applications) has no data yet.</summary>
    public record DashboardStats(
        int TotalStalls,
        int OccupiedStalls,
        int VacantStalls,
        int OccupancyPct,
        int? TotalArtisans,
        decimal TodaySales,
        decimal? SalesChangePct,
        int LowStockItems,
        int OutOfStockItems,
        int? PendingApplications,
        int TotalStaff,
        int ActiveStaff);
}
