using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Attendance : BackendBase, IAttendanceInterface
    {
        public Attendance(IMSBContext context) : base(context) { }

        public Task<List<Department>> DepartmentsAsync() => _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();

        public Task<Dictionary<Guid, string>> DepartmentNamesAsync() =>
            _context.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name);

        public async Task<Dictionary<string, string>> ShiftNamesAsync() =>
            (await _context.Shifts.AsNoTracking().ToListAsync())
                .GroupBy(s => s.Code.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Name);

        public async Task<List<SheetRow>> SheetAsync(DateTime day)
        {
            var marked = _context.AttendanceRecords.Where(r => r.AttendanceDate == day).Select(r => r.StaffId);
            var staff = await _context.Staff.AsNoTracking()
                .Where(s => (s.Status.ToUpper() != "INACTIVE" && s.JoinDate <= day) || marked.Contains(s.Id))
                .ToListAsync();
            var records = await _context.AttendanceRecords.AsNoTracking().Where(r => r.AttendanceDate == day).ToDictionaryAsync(r => r.StaffId);
            var depts = await DepartmentNamesAsync();
            var shifts = await ShiftNamesAsync();

            return staff
                .Select(s => new SheetRow(s, depts.GetValueOrDefault(s.DepartmentId) ?? string.Empty,
                    shifts.GetValueOrDefault(s.Shift.Trim().ToUpperInvariant()), records.GetValueOrDefault(s.Id)))
                .OrderBy(r => r.DepartmentName).ThenBy(r => r.Staff.FullName).ThenBy(r => r.Staff.EmployeeId)
                .ToList();
        }

        public async Task<List<AttendanceStatusRow>> StatusesBetweenAsync(DateTime from, DateTime to) =>
            (await _context.AttendanceRecords.AsNoTracking()
                .Where(r => r.AttendanceDate >= from && r.AttendanceDate <= to)
                .Select(r => new { r.AttendanceDate, r.Status, r.Staff!.DepartmentId }).ToListAsync())
            .Select(r => new AttendanceStatusRow(r.AttendanceDate, r.Status, r.DepartmentId)).ToList();

        public Task<List<Staff>> StaffByEmployeeIdsAsync(List<string> employeeIds) =>
            _context.Staff.Where(s => employeeIds.Contains(s.EmployeeId)).ToListAsync();

        public Task<Dictionary<Guid, AttendanceRecord>> RecordsOnAsync(DateTime day, List<Guid> staffIds) =>
            _context.AttendanceRecords.Where(r => r.AttendanceDate == day && staffIds.Contains(r.StaffId)).ToDictionaryAsync(r => r.StaffId);

        public Task<AttendanceRecord?> FindAsync(Guid id, bool tracking)
        {
            var query = _context.AttendanceRecords.Include(r => r.Staff).AsQueryable();
            if (!tracking) query = query.AsNoTracking();
            return query.FirstOrDefaultAsync(r => r.Id == id);
        }

        public Task<List<AttendanceRecord>> HistoryAsync(string? q, string? departmentId, string? status, DateTime from, DateTime to)
        {
            var query = _context.AttendanceRecords.AsNoTracking().Include(r => r.Staff)
                .Where(r => r.AttendanceDate >= from && r.AttendanceDate <= to);
            if (Guid.TryParse(departmentId, out var deptId)) query = query.Where(r => r.Staff!.DepartmentId == deptId);
            var st = AttendanceStatuses.Normalize(status);
            if (st != null) query = query.Where(r => r.Status == st);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(r => r.Staff!.FullName.ToLower().Contains(term) || r.Staff.EmployeeId.ToLower().Contains(term));
            }
            return query.OrderByDescending(r => r.AttendanceDate).ThenBy(r => r.Staff!.FullName).ToListAsync();
        }

        public void Add(AttendanceRecord record) => _context.AttendanceRecords.Add(record);

        public void Remove(AttendanceRecord record) => _context.AttendanceRecords.Remove(record);
    }
}
