using System.Globalization;
using IMSB.Backend.Mappers;
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
            var shifts = await ShiftNamesAsync();
            var assignments = await _context.OtAssignments.AsNoTracking().Include(a => a.Staff).ToListAsync();
            return assignments.Where(a => a.Staff != null)
                .Select(a =>
                {
                    var (shiftName, start, end) = ShiftOf(a.Staff!, shifts);
                    return new OtAssignmentRow(a.Id, a.StaffId, a.Staff!.EmployeeId, a.Staff.FullName,
                        categories.GetValueOrDefault(a.Staff.DepartmentId) ?? string.Empty, a.Staff.Designation,
                        Stamp(a.FromDateTime), Stamp(a.ToDateTime), a.UpdatedBy ?? a.CreatedBy, shiftName, start, end, a.OtHours);
                })
                // latest OT date first, so the upcoming ones lead and past ones follow
                .OrderByDescending(row => row.FromDateTime, StringComparer.Ordinal)
                .ThenBy(row => row.StaffName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.EmployeeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<OtStaffOption>> UnassignedStaffAsync()
        {
            var today = DateTime.Today;
            var shifts = await ShiftNamesAsync();
            var staff = await _context.Staff.AsNoTracking()
                .Where(s => s.Status.ToUpper() != "INACTIVE" && !_context.OtAssignments.Any(a => a.StaffId == s.Id && a.FromDateTime >= today))
                .OrderBy(s => s.FullName)
                .ToListAsync();
            return staff.Select(s =>
            {
                var (shiftName, start, end) = ShiftOf(s, shifts);
                return new OtStaffOption(s.Id, s.EmployeeId, s.FullName, shiftName, start, end);
            }).ToList();
        }

        public async Task<BackendResult> AddAsync(Guid staffId, DateTime from, DateTime to, string? user)
        {
            var today = DateTime.Today;
            var staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId);
            if (staff == null) return BackendResult.Error("ValStaffNotFound");
            // past assignments are kept as history and do not count; only one for today or later is allowed
            if (await _context.OtAssignments.AnyAsync(a => a.StaffId == staffId && a.FromDateTime >= today)) return BackendResult.Error("ValOtAlreadyAssigned");
            if (await ShiftConflictAsync(staff, from, to) is { } conflict) return BackendResult.Error(conflict);

            _context.OtAssignments.Add(new OtAssignment
            {
                StaffId = staffId, FromDateTime = from, ToDateTime = to, OtHours = OtAssignment.HoursBetween(from, to), CreatedBy = user
            });
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        public async Task<BackendResult> UpdateAsync(Guid id, DateTime from, DateTime to, string? user)
        {
            var assignment = await _context.OtAssignments.Include(a => a.Staff).FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null) return BackendResult.Missing;
            if (assignment.FromDateTime.Date < DateTime.Today) return BackendResult.Error("ValOtPastLocked");
            if (assignment.Staff != null && await ShiftConflictAsync(assignment.Staff, from, to) is { } conflict) return BackendResult.Error(conflict);

            assignment.FromDateTime = from;
            assignment.ToDateTime = to;
            assignment.OtHours = OtAssignment.HoursBetween(from, to);
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

        /// <summary>Shift names by upper-cased code; staff hold the code (older records may hold a name such as "Night").</summary>
        private async Task<Dictionary<string, string>> ShiftNamesAsync() =>
            (await _context.Shifts.AsNoTracking().ToListAsync())
                .GroupBy(s => s.Code.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Name);

        private static string? ShiftNameOf(Staff staff, Dictionary<string, string> shifts) =>
            string.IsNullOrWhiteSpace(staff.Shift) ? null : shifts.GetValueOrDefault(staff.Shift.Trim().ToUpperInvariant()) ?? staff.Shift.Trim();

        private static (string? Name, string? Start, string? End) ShiftOf(Staff staff, Dictionary<string, string> shifts)
        {
            var name = ShiftNameOf(staff, shifts);
            var times = AttendanceRules.ShiftTimes(name);
            return (name, times == null ? null : Clock(times.Value.Start), times == null ? null : Clock(times.Value.End));
        }

        private async Task<string?> ShiftConflictAsync(Staff staff, DateTime from, DateTime to) =>
            OtAssignmentMapper.ShiftConflict(from, to, AttendanceRules.ShiftTimes(ShiftNameOf(staff, await ShiftNamesAsync())));

        private static string Clock(TimeSpan value) => value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        private static string Stamp(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
    }
}
