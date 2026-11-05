using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    /// <summary>Backend of the Staff page (named so it does not clash with the <see cref="Staff"/> entity).</summary>
    public class StaffDetails : BackendBase, IStaffInterface
    {
        public StaffDetails(IMSBContext context) : base(context) { }

        public Task<List<Staff>> AllAsync() => _context.Staff.AsNoTracking().ToListAsync();

        public Task<List<Staff>> FilterAsync(string? q, string? departmentId, string? status) =>
            ApplyFilters(_context.Staff.AsNoTracking(), q, departmentId, status).OrderByDescending(s => s.CreatedAt).ToListAsync();

        // Comparisons are explicitly case-insensitive so they don't depend on the database collation
        // (existing rows hold both "Active" and "ACTIVE").
        private static IQueryable<Staff> ApplyFilters(IQueryable<Staff> query, string? q, string? departmentId, string? status)
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(s => s.FullName.ToLower().Contains(term) || s.Mobile.Contains(term) || s.EmployeeId.ToLower().Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(departmentId) && Guid.TryParse(departmentId, out var deptGuid))
            {
                query = query.Where(s => s.DepartmentId == deptGuid);
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                var wanted = status.Trim().ToUpper();
                query = query.Where(s => s.Status.ToUpper() == wanted);
            }
            return query;
        }

        public Task<List<Department>> DepartmentsAsync() => _context.Departments.AsNoTracking().ToListAsync();

        public Task<Dictionary<Guid, string>> DepartmentNamesAsync() =>
            _context.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name);

        public Task<List<Shift>> ShiftsAsync() =>
            _context.Shifts.AsNoTracking().Where(s => s.DisplayOrder > 0).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name).ToListAsync();

        public Task<List<string>> EmployeeIdsAsync() => _context.Staff.AsNoTracking().Select(s => s.EmployeeId).ToListAsync();

        public Task<Staff?> FindAsync(string employeeId) => _context.Staff.FirstOrDefaultAsync(s => s.EmployeeId == employeeId);

        public Task<bool> DepartmentExistsAsync(Guid departmentId) => _context.Departments.AnyAsync(d => d.Id == departmentId);

        public Task<bool> AnyShiftsAsync() => _context.Shifts.AnyAsync();

        public Task<bool> ShiftExistsAsync(string code) => _context.Shifts.AnyAsync(s => s.Code == code && s.DisplayOrder > 0);

        public Task<bool> MobileExistsAsync(string mobile, Guid? exceptId) =>
            _context.Staff.AnyAsync(s => s.Mobile == mobile && s.Id != exceptId);

        public Task<bool> AadhaarExistsAsync(string aadhaarHash, Guid? exceptId) =>
            _context.Staff.AnyAsync(s => s.AadhaarHash == aadhaarHash && s.Id != exceptId);

        public Task<bool> EmailExistsAsync(string email, Guid? exceptId) =>
            _context.Staff.AnyAsync(s => s.Email != null && s.Email.ToLower() == email && s.Id != exceptId);

        public async Task AddAsync(Staff staff)
        {
            await CopyCategoryFromDepartmentAsync(staff);
            _context.Staff.Add(staff);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Staff staff)
        {
            await CopyCategoryFromDepartmentAsync(staff);
            await _context.SaveChangesAsync();
        }

        /// <summary>The Staff Category column holds the name of the staff member's department.</summary>
        private async Task CopyCategoryFromDepartmentAsync(Staff staff)
        {
            var name = await _context.Departments.AsNoTracking().Where(d => d.Id == staff.DepartmentId).Select(d => d.Name).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(name)) staff.StaffCategory = name.Length > 100 ? name[..100] : name;
        }

        public async Task RemoveAsync(Staff staff)
        {
            _context.Staff.Remove(staff);
            await _context.SaveChangesAsync();
        }
    }
}
