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

        void Add(Product product);
        void AddMovement(StockMovement movement);

        /// <summary>Deletes the product; its stock history and sale lines keep their snapshot of it.</summary>
        Task DeleteAsync(Product product);

        Task SaveChangesAsync();
    }
}
