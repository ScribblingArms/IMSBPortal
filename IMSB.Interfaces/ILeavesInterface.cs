using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>
    /// Leave management for the current year: per-employee entitlements (one of them is the attendance leave policy that
    /// Leave / Half Day attendance draws on) and leave taken. Refusals carry a shared resource key.
    /// </summary>
    public interface ILeavesInterface
    {
        /// <summary>Every staff member by category and name.</summary>
        Task<List<LeaveStaffOption>> StaffAsync();

        /// <summary>Staff who are not inactive, by name.</summary>
        Task<List<LeaveStaffOption>> ActiveStaffAsync();

        /// <summary>Names from the leave type table, in alphabetical order.</summary>
        Task<List<string>> LeaveTypesAsync();

        /// <summary>The year's entitlements with their staff member, by name and leave type.</summary>
        Task<List<LeaveEntitlement>> EntitlementsWithStaffAsync(int year);

        /// <summary>The year's entitlements by staff and leave type.</summary>
        Task<List<LeaveEntitlement>> EntitlementsAsync(int year);

        /// <summary>The year's leave entries (with staff), latest first.</summary>
        Task<List<StaffLeaveEntry>> EntriesAsync(int year);

        /// <summary>Entitlement, taken and balance per employee and leave type, optionally for one employee / leave type.</summary>
        Task<List<LeaveBalanceRow>> BalancesAsync(int year, string? employeeId = null, string? leaveType = null);

        /// <summary>Adds or changes an employee's quota for a leave type; the first type configured becomes the attendance policy.</summary>
        Task<BackendResult> SaveEntitlementAsync(Guid staffId, string leaveType, decimal annualQuota);

        /// <summary>Removes an entitlement that no leave has been taken against.</summary>
        Task<BackendResult> DeleteEntitlementAsync(Guid id);

        /// <summary>The first quota change that cannot be made for this employee, or null when all can.</summary>
        Task<LeaveQuotaProblem?> LeaveQuotaProblemAsync(Guid staffId, IReadOnlyCollection<LeaveQuotaDto>? changes);

        /// <summary>Adds, changes or (null days) removes the listed quotas, keeping an attendance policy on a type with days.</summary>
        Task<BackendResult> SaveQuotasAsync(Guid staffId, IReadOnlyCollection<LeaveQuotaDto> changes);

        /// <summary>Records leave on any date this year from the join date on (from/to null = not a valid date).</summary>
        Task<BackendResult> RecordEntryAsync(Guid staffId, string leaveType, DateTime? from, DateTime? to, string? remarks, string? user);

        Task<BackendResult> UpdateEntryAsync(Guid id, DateTime? from, DateTime? to, string? remarks, string? user);

        Task<BackendResult> DeleteEntryAsync(Guid id);
    }
}
