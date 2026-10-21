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

        /// <summary>email is lower-case.</summary>
        Task<bool> EmailExistsAsync(string email, Guid? exceptId);

        void Add(Staff staff);
        void Remove(Staff staff);
        Task SaveChangesAsync();
    }
}
