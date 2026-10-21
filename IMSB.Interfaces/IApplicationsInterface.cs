using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface IApplicationsInterface
    {
        Task<ApplicationStats> StatsAsync();

        /// <summary>Filtered applications (with category), most recently decided first. q matches name, application no, mobile, district, artisan id and stall.</summary>
        Task<List<ArtisanApplication>> FilterAsync(string? q, string? status, string? categoryId, string? district);

        /// <summary>Same filters as <see cref="FilterAsync"/>, ordered by application no.</summary>
        Task<List<ArtisanApplication>> ExportAsync(string? q, string? status, string? categoryId, string? district);

        Task<List<ProductCategory>> CategoriesAsync();
        Task<bool> CategoryExistsAsync(Guid categoryId);

        /// <summary>Vacant stalls (with category) by block and code.</summary>
        Task<List<Stall>> VacantStallsAsync();

        /// <summary>Read-only application with its category.</summary>
        Task<ArtisanApplication?> GetDetailsAsync(string applicationNo);

        /// <summary>Tracked application for changes.</summary>
        Task<ArtisanApplication?> FindAsync(string applicationNo);

        Task<List<string>> ApplicationNumbersAsync();
        Task<List<string>> ArtisanCodesAsync();

        Task<bool> ArtisanMobileExistsAsync(string mobile);
        Task<bool> ArtisanAadhaarExistsAsync(string aadhaarHash);
        Task<bool> ArtisanExistsAsync(string mobile, string aadhaarHash);

        /// <summary>Whether another pending / under-review application uses the mobile or Aadhaar.</summary>
        Task<bool> OpenApplicationMobileExistsAsync(string mobile, Guid exceptId);
        Task<bool> OpenApplicationAadhaarExistsAsync(string aadhaarHash, Guid exceptId);

        /// <summary>Tracked stall by code.</summary>
        Task<Stall?> FindStallAsync(string stallCode);

        void Add(ArtisanApplication application);
        void Remove(ArtisanApplication application);
        void AddArtisan(Artisan artisan);
        Task SaveChangesAsync();
    }
}
