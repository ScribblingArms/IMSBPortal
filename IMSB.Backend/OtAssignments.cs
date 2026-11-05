using System.Globalization;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class OtAssignments : BackendBase, IOtAssignmentsInterface
    {
        public OtAssignments(IMSBContext context) : base(context) { }

        public async Task<List<OtAssignmentRow>> ListAsync()
        {
            var categories = await _context.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name);
            var assignments = await _context.OtAssignments.AsNoTracking().Include(a => a.Staff).ToListAsync();
            return assignments.Where(a => a.Staff != null)
                .Select(a => new OtAssignmentRow(a.Id, a.StaffId, a.Staff!.EmployeeId, a.Staff.FullName,
                    categories.GetValueOrDefault(a.Staff.DepartmentId) ?? string.Empty, a.Staff.Designation,
                    Stamp(a.FromDateTime), Stamp(a.ToDateTime), a.UpdatedBy ?? a.CreatedBy))
                .OrderBy(row => row.StaffName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.EmployeeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public Task<List<OtStaffOption>> UnassignedStaffAsync() =>
            _context.Staff.AsNoTracking()
                .Where(s => s.Status.ToUpper() != "INACTIVE" && !_context.OtAssignments.Any(a => a.StaffId == s.Id))
                .OrderBy(s => s.FullName)
                .Select(s => new OtStaffOption(s.Id, s.EmployeeId, s.FullName))
                .ToListAsync();

        public async Task<BackendResult> AddAsync(Guid staffId, DateTime from, DateTime to, string? user)
        {
            if (!await _context.Staff.AnyAsync(s => s.Id == staffId)) return BackendResult.Error("ValStaffNotFound");
            if (await _context.OtAssignments.AnyAsync(a => a.StaffId == staffId)) return BackendResult.Error("ValOtAlreadyAssigned");

            _context.OtAssignments.Add(new OtAssignment { StaffId = staffId, FromDateTime = from, ToDateTime = to, CreatedBy = user });
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        public async Task<BackendResult> UpdateAsync(Guid id, DateTime from, DateTime to, string? user)
        {
            var assignment = await _context.OtAssignments.FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null) return BackendResult.Missing;

            assignment.FromDateTime = from;
            assignment.ToDateTime = to;
            assignment.UpdatedAt = DateTime.UtcNow;
            assignment.UpdatedBy = user;
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        public async Task<BackendResult> DeleteAsync(Guid id)
        {
            var assignment = await _context.OtAssignments.FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null) return BackendResult.Missing;

            _context.OtAssignments.Remove(assignment);
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        private static string Stamp(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
    }
}
