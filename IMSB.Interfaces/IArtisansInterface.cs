using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface IArtisansInterface
    {
        Task<ArtisanStats> StatsAsync();

        /// <summary>Stall code held by each artisan.</summary>
        Task<Dictionary<Guid, string>> StallsByArtisanAsync();

        /// <summary>Filtered artisans (with category) by name. stall = "assigned" / "unassigned".</summary>
        Task<List<Artisan>> FilterAsync(string? q, string? status, string? categoryId, string? district, string? stall);

        Task<List<ProductCategory>> CategoriesAsync();
        Task<bool> CategoryExistsAsync(Guid categoryId);

        /// <summary>Read-only artisan with category.</summary>
        Task<Artisan?> GetDetailsAsync(string artisanCode);

        /// <summary>Tracked artisan for changes.</summary>
        Task<Artisan?> FindAsync(string artisanCode);

        /// <summary>Read-only stall held by the artisan, if any.</summary>
        Task<Stall?> StallOfAsync(Guid artisanId);

        /// <summary>This month's sales at the stall and the number of products listed there.</summary>
        Task<(decimal MonthSales, int Products)> StallFiguresAsync(string stallCode);

        Task<List<string>> ArtisanCodesAsync();
        Task<bool> MobileExistsAsync(string mobile, Guid exceptId);
        Task<bool> AadhaarExistsAsync(string aadhaarHash, Guid exceptId);
        Task<bool> HasStallAsync(Guid artisanId);

        Task AddAsync(Artisan artisan);

        /// <summary>Saves the changes made to a tracked artisan.</summary>
        Task UpdateAsync(Artisan artisan);

        /// <summary>
        /// Deletes the artisan: pending reallocation requests naming her as the new holder are closed with the given
        /// remarks, other requests and applications lose the reference (their snapshots keep the name).
        /// </summary>
        Task DeleteAsync(Artisan artisan, string? user, string closedRemarks);
    }
}
