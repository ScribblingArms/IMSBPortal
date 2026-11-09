using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>OT Assignment screen: the overtime window of each staff member. Refusals carry a shared resource key.</summary>
    public interface IOtAssignmentsInterface
    {
        /// <summary>Every assignment with its staff member, latest OT date first, then by staff name.</summary>
        Task<List<OtAssignmentRow>> ListAsync();

        /// <summary>Staff who are not inactive and have no assignment for today or later, by name.</summary>
        Task<List<OtStaffOption>> UnassignedStaffAsync();

        /// <summary>
        /// Refused while the staff member already has an assignment for today or later (past ones are history), and when the
        /// window falls inside their regular shift ("ValOtBeforeShiftEnd" / "ValOtIntoNextShift"); the same shift rule applies to updates.
        /// </summary>
        Task<BackendResult> AddAsync(Guid staffId, DateTime from, DateTime to, string? user);

        /// <summary>Refused ("ValOtPastLocked") once the assignment's OT date is before today.</summary>
        Task<BackendResult> UpdateAsync(Guid id, DateTime from, DateTime to, string? user);

        Task<BackendResult> DeleteAsync(Guid id);
    }
}
