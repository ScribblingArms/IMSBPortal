using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>
    /// Admin portal "SHG Applications": read-only view of the stall applications the website saves in SHGDetails.
    /// Every query takes the district the user may see: null means every district.
    /// </summary>
    public interface IShgApplicationsInterface
    {
        /// <summary>Applications, newest first, with their district and product category; only <paramref name="districtId"/> when given.</summary>
        Task<List<ShgDetail>> ListAsync(int? districtId);

        /// <summary>One application by its public id, or null when missing or outside <paramref name="districtId"/>.</summary>
        Task<ShgDetail?> FindAsync(Guid publicId, int? districtId);

        /// <summary>The Districts table, by name.</summary>
        Task<List<District>> DistrictsAsync();

        /// <summary>The district stored on a login (officers), or null.</summary>
        Task<string?> LoginDistrictAsync(int loginId);

        /// <summary>
        /// Rejects the application with this row id (stores the reason, the officer and the time). False when the row is
        /// missing or no longer rejectable (already rejected, or holding a stall). Incomplete applications can be rejected.
        /// </summary>
        Task<bool> RejectAsync(int shgDetailId, string reason, string? rejectedBy, DateTime rejectedAt);

        /// <summary>Deletes the application with this row id. False when the row is missing or holds a stall (vacate it first).</summary>
        Task<bool> DeleteAsync(int shgDetailId);

        // ── Stall allotment (stalls.IsArtisan = false, stalls.ShgDetailId = the application) ──

        /// <summary>The stall held by each application that holds one: SHGDetails.Id → stall.</summary>
        Task<Dictionary<int, Stall>> AllottedStallsAsync();

        /// <summary>The stall an application holds, or null.</summary>
        Task<Stall?> StallOfAsync(int shgDetailId);

        /// <summary>VACANT stalls by block and code, for the Allocate Stall popup.</summary>
        Task<List<Stall>> VacantStallsAsync();

        /// <summary>A stall to change (tracked), by code.</summary>
        Task<Stall?> FindStallAsync(string stallCode);

        /// <summary>Saves changes made to a tracked stall.</summary>
        Task SaveStallAsync(Stall stall);
    }
}
