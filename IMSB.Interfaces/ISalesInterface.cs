using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface ISalesInterface
    {
        Task<int> TotalStallsAsync(BazaarOptions bazaar);
        Task<SalesSummary> SummaryAsync(DateTime day, int totalStalls);
        Task<List<(string Category, decimal Revenue, int Quantity)>> ByCategoryAsync(DateTime from, DateTime to);
        Task<List<(int Hour, decimal Revenue)>> HourlyAsync(DateTime day);

        /// <summary>The day's entries (with items), newest first; stall (upper-case) keeps entries with a line at that stall.</summary>
        Task<List<SalesEntry>> EntriesOnAsync(DateTime day, string? stall);

        /// <summary>Read-only entry with items.</summary>
        Task<SalesEntry?> GetEntryAsync(string entryNo);

        /// <summary>Tracked entry with items.</summary>
        Task<SalesEntry?> FindEntryAsync(string entryNo);

        /// <summary>Every product (with category) by name, read-only.</summary>
        Task<List<Product>> ProductsAsync();

        /// <summary>Tracked products (with category) by code.</summary>
        Task<Dictionary<string, Product>> ProductsByCodeAsync(List<string> codes);

        /// <summary>Tracked products by id.</summary>
        Task<Dictionary<Guid, Product>> ProductsByIdAsync(IEnumerable<Guid> ids);

        Task<List<ProductCategory>> CategoriesAsync();

        /// <summary>Stall codes that appear on any sale line.</summary>
        Task<List<string>> SaleStallsAsync();

        Task<List<ProductReportRow>> ProductReportRowsAsync(string? q, string? categoryId, string? stall, DateTime? fromDay, DateTime? toDay);

        /// <summary>Stock movements written by sales (deductions and reversals), newest first, with the products they still belong to.</summary>
        Task<(List<StockMovement> Movements, Dictionary<Guid, Product> Products)> DeductionLogAsync(DateTime? day, int take);

        /// <summary>Sale lines sold in [from, to), optionally for one stall.</summary>
        Task<List<SaleExportRow>> ExportRowsAsync(DateTime from, DateTime to, string? stall);

        /// <summary>Next SAL-000001 style entry number.</summary>
        Task<string> NextEntryNoAsync();

        void Add(SalesEntry entry);
        void Remove(SalesEntry entry);
        void AddItems(IEnumerable<SaleItem> items);
        void RemoveItems(IEnumerable<SaleItem> items);
        void AddMovement(StockMovement movement);
        Task SaveChangesAsync();
    }
}
