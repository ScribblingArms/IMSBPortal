using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface IReportsInterface
    {
        Task<List<ProductCategory>> CategoriesAsync();
        Task<ProductCategory?> CategoryAsync(Guid categoryId);

        /// <summary>Distinct stall blocks.</summary>
        Task<List<string>> BlocksAsync();

        Task<ReportKpis> KpisAsync(ReportPeriod period, ProductCategory? category);

        /// <summary>Revenue for the months ending with the selected one, and for the same months a year earlier.</summary>
        Task<List<(DateTime Month, decimal Current, decimal Previous)>> SalesTrendAsync(ReportPeriod period, string? categoryName, int months);

        Task<List<(string Category, decimal Revenue, int Quantity)>> ByCategoryAsync(DateTime from, DateTime to);

        Task<List<(DateTime Month, int Occupied, int Total)>> OccupancyTrendAsync(ReportPeriod period, Guid? categoryId, int months);

        Task<List<StallReportRow>> StallRowsAsync(ReportPeriod period, string? q, string? status, string? block, string? categoryId);

        /// <summary>Sales at each artisan's stall in the period, with share of all sales and a 1-5 rating against the best seller.</summary>
        Task<List<ArtisanReportRow>> ArtisanRowsAsync(ReportPeriod period, string? q, string? status, string? categoryId);

        Task<ApplicationReportData> ApplicationsAsync(ReportPeriod period, Guid? categoryId);

        /// <summary>Products (with category) by stall and name.</summary>
        Task<List<Product>> InventoryAsync(Guid? categoryId);

        Task<List<SaleLine>> SaleLinesAsync(ReportPeriod period, string? categoryName);
    }
}
