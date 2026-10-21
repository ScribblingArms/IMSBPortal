using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface IDashboardInterface
    {
        /// <summary>Header cards. Pending applications = new + under review.</summary>
        Task<DashboardStats> StatsAsync(BazaarOptions bazaar);

        Task<ApplicationStats> ApplicationStatsAsync();
        Task<List<(DateTime Day, decimal Revenue)>> DailyTrendAsync(DateTime lastDay, int days);
        Task<List<(string Category, decimal Revenue, int Quantity)>> ByCategoryAsync(DateTime from, DateTime to);
        Task<List<SalesAnalytics.StallPerformance>> StallPerformanceAsync(DateTime from, DateTime to);

        /// <summary>Products (with category) below their minimum or out of stock, by name.</summary>
        Task<List<Product>> LowStockAsync();

        /// <summary>Latest sales, manual stock changes and staff registrations, newest first.</summary>
        Task<List<DashboardActivity>> RecentActivityAsync(int take);
    }
}
