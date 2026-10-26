using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface IInventoryInterface
    {
        /// <summary>Every product, read-only.</summary>
        Task<List<Product>> ProductsAsync();

        /// <summary>Filtered products by name; status is derived from quantity / min stock.</summary>
        Task<List<Product>> FilterAsync(string? q, string? categoryId, string? status);

        Task<List<ProductCategory>> CategoriesAsync();
        Task<int> CategoryCountAsync();
        Task<Dictionary<Guid, string>> CategoryNamesAsync();
        Task<bool> CategoryExistsAsync(Guid categoryId);

        Task<List<StockMovement>> RecentMovementsAsync(int take);

        Task<List<string>> ProductCodesAsync();

        /// <summary>Whether another product in the stall has this name (case-insensitive).</summary>
        Task<bool> ProductExistsAsync(string name, string stallCode, Guid? exceptId);

        /// <summary>Tracked product by code.</summary>
        Task<Product?> FindAsync(string productCode);

        /// <summary>Inserts the product with its opening stock movement.</summary>
        Task AddAsync(Product product, StockMovement opening);

        /// <summary>Saves the changes made to a tracked product.</summary>
        Task UpdateAsync(Product product);

        /// <summary>Records the stock movement and saves the product's new quantity.</summary>
        Task UpdateStockAsync(Product product, StockMovement movement);

        /// <summary>Deletes the product; its stock history and sale lines keep their snapshot of it.</summary>
        Task DeleteAsync(Product product);
    }
}
