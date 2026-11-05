using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>OT Assignment screen: the overtime window of each staff member. Refusals carry a shared resource key.</summary>
    public interface IOtAssignmentsInterface
    {
        /// <summary>Every assignment with its staff member, by staff name.</summary>
        Task<List<OtAssignmentRow>> ListAsync();

        /// <summary>Staff who are not inactive and have no assignment yet, by name.</summary>
        Task<List<OtStaffOption>> UnassignedStaffAsync();

        Task<BackendResult> AddAsync(Guid staffId, DateTime from, DateTime to, string? user);

        Task<BackendResult> UpdateAsync(Guid id, DateTime from, DateTime to, string? user);

        Task<BackendResult> DeleteAsync(Guid id);
    }
}
