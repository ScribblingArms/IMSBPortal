namespace IMSB.Dto.Models
{
    /// <summary>
    /// Dashboard figures. Null = the module behind it (artisans, applications) has no data yet.
    /// The header cards show the staff attendance summary for today: present (incl. half day), absent and on leave
    /// (a LEAVE record, or a leave entry covering today with nothing marked).
    /// </summary>
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
        int ActiveStaff,
        int PresentToday = 0,
        int AbsentToday = 0,
        int OnLeaveToday = 0);
}
