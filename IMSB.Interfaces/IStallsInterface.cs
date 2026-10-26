using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface IStallsInterface
    {
        Task<StallStats> StatsAsync();

        /// <summary>Every stall (with category and artisan) by block and code.</summary>
        Task<List<Stall>> AllAsync();

        /// <summary>Ids of the stalls matching the filters. q matches code, name, location and the artisan's name or code.</summary>
        Task<HashSet<Guid>> FilteredIdsAsync(string? q, string? status, string? categoryId, string? block);

        /// <summary>Filtered stalls (with category and artisan) by block and code.</summary>
        Task<List<Stall>> FilterAsync(string? q, string? status, string? categoryId, string? block);

        Task<HashSet<Guid>> PendingReallocationStallIdsAsync();

        /// <summary>Number of products per upper-cased stall code.</summary>
        Task<Dictionary<string, int>> ProductCountsAsync();

        /// <summary>Products listed at a stall.</summary>
        Task<int> ProductCountAsync(string stallCode);

        /// <summary>Artisans (with category) who are not inactive and hold no stall, by name.</summary>
        Task<List<Artisan>> FreeArtisansAsync();

        /// <summary>Pending requests first, then the newest.</summary>
        Task<List<StallReallocation>> RecentReallocationsAsync(int take);

        Task<List<ProductCategory>> CategoriesAsync();
        Task<bool> CategoryExistsAsync(Guid categoryId);

        Task<bool> StallCodeExistsAsync(string stallCode);

        /// <summary>Tracked stall by code (with its artisan when includeArtisan).</summary>
        Task<Stall?> FindAsync(string stallCode, bool includeArtisan = false);

        /// <summary>Tracked stall by id.</summary>
        Task<Stall?> FindByIdAsync(Guid id);

        /// <summary>Tracked artisan by code.</summary>
        Task<Artisan?> FindArtisanAsync(string artisanCode);

        /// <summary>Tracked artisan by id.</summary>
        Task<Artisan?> FindArtisanByIdAsync(Guid id);

        Task<bool> ArtisanHasStallAsync(Guid artisanId);

        Task<bool> HasPendingReallocationAsync(Guid stallId);

        /// <summary>Tracked pending reallocation requests for a stall.</summary>
        Task<List<StallReallocation>> OpenReallocationsAsync(Guid stallId);

        /// <summary>Tracked reallocation request by number.</summary>
        Task<StallReallocation?> FindReallocationAsync(string requestNo);

        Task<List<string>> ReallocationNumbersAsync();

        Task AddAsync(Stall stall);

        /// <summary>Saves the changes made to a tracked stall (details, allocation or vacating, with any requests closed by it).</summary>
        Task UpdateAsync(Stall stall);

        /// <summary>Deletes the stall; its reallocation requests go with it.</summary>
        Task RemoveAsync(Stall stall);

        Task AddReallocationAsync(StallReallocation request);

        /// <summary>Saves the decided request together with the stall change it made, if any.</summary>
        Task DecideReallocationAsync(StallReallocation request);
    }
}
