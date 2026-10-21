using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>Header search. term is lower-case; digits (when 3 or more) also match mobile numbers.</summary>
    public interface ISearchInterface
    {
        Task<List<Artisan>> ArtisansAsync(string term, string digits, int take);
        Task<List<ArtisanApplication>> ApplicationsAsync(string term, string digits, int take);

        /// <summary>Stalls (with artisan) by code, name or artisan name.</summary>
        Task<List<Stall>> StallsAsync(string term, int take);

        Task<List<Staff>> StaffAsync(string term, string digits, int take);
        Task<List<Product>> ProductsAsync(string term, int take);
    }
}
