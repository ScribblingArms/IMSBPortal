using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface IStaffInterface
    {
        /// <summary>Every staff member, read-only.</summary>
        Task<List<Staff>> AllAsync();

        /// <summary>Filtered staff, newest first. Comparisons are case-insensitive whatever the database collation.</summary>
        Task<List<Staff>> FilterAsync(string? q, string? departmentId, string? status);

        Task<List<Department>> DepartmentsAsync();
        Task<Dictionary<Guid, string>> DepartmentNamesAsync();
        Task<List<Shift>> ShiftsAsync();

        Task<List<string>> EmployeeIdsAsync();

        /// <summary>Tracked staff member by employee id.</summary>
        Task<Staff?> FindAsync(string employeeId);

        Task<bool> DepartmentExistsAsync(Guid departmentId);
        Task<bool> AnyShiftsAsync();
        Task<bool> ShiftExistsAsync(string code);
        Task<bool> MobileExistsAsync(string mobile, Guid? exceptId);
        Task<bool> AadhaarExistsAsync(string aadhaarHash, Guid? exceptId);

        /// <summary>email is lower-case.</summary>
        Task<bool> EmailExistsAsync(string email, Guid? exceptId);

        /// <summary>The leave types offered when registering, by name.</summary>
        Task<List<string>> LeaveTypesAsync();

        /// <summary>False unless each quota is for a different known leave type, in half-days from 0 to 366 (or null, when removal is allowed).</summary>
        Task<bool> LeaveQuotasValidAsync(IReadOnlyCollection<LeaveQuotaDto>? quotas, bool allowRemoval = false);

        /// <summary>Saves a new staff member together with their leave quotas for this year.</summary>
        Task AddAsync(Staff staff, IReadOnlyCollection<LeaveQuotaDto>? quotas = null);

        /// <summary>Saves the changes made to a tracked staff member.</summary>
        Task UpdateAsync(Staff staff);

        Task RemoveAsync(Staff staff);
    }
}
