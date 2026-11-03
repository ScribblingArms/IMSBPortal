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

        public Task<List<Staff>> MonthlyStaffAsync(DateTime from, DateTime to)
        {
            var endExclusive = to.Date.AddDays(1);
            var marked = _context.AttendanceRecords
                .Where(r => r.AttendanceDate >= from.Date && r.AttendanceDate < endExclusive)
                .Select(r => r.StaffId);

            return _context.Staff.AsNoTracking()
                .Where(s => s.JoinDate < endExclusive && (s.Status.ToUpper() != "INACTIVE" || marked.Contains(s.Id)))
                .OrderBy(s => s.DepartmentId).ThenBy(s => s.FullName).ThenBy(s => s.EmployeeId)
                .ToListAsync();
        }

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

        public async Task SaveSheetAsync(IEnumerable<AttendanceRecord> newRecords)
        {
            _context.AttendanceRecords.AddRange(newRecords);
            await _context.SaveChangesAsync();
        }

        public Task UpdateAsync(AttendanceRecord record) => _context.SaveChangesAsync();

        public async Task RemoveAsync(AttendanceRecord record)
        {
            _context.AttendanceRecords.Remove(record);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ApplyLeavePolicyAsync(IReadOnlyCollection<AttendanceRecord> records)
        {
            var lopCount = 0;
            foreach (var year in records.GroupBy(r => r.AttendanceDate.Year))
            {
                var staffIds = year.Select(r => r.StaffId).Distinct().ToList();
                // saved values: a record being changed is left out of its own balance below
                var entries = await _context.StaffLeaveEntries.AsNoTracking()
                    .Where(e => e.Year == year.Key && staffIds.Contains(e.StaffId)).ToListAsync();
                var attendance = await AttendanceInYear(year.Key).Where(r => staffIds.Contains(r.StaffId)).ToListAsync();
                var policies = await _context.LeaveEntitlements.AsNoTracking()
                    .Where(e => e.Year == year.Key && e.IsAttendanceLeaveType && e.StaffId.HasValue && staffIds.Contains(e.StaffId.Value))
                    .ToListAsync();
                // per-staff slices: each record only scans its own person's leave and attendance
                var entriesByStaff = entries.ToLookup(e => e.StaffId);
                var attendanceByStaff = attendance.ToLookup(r => r.StaffId);
                var policyByStaff = policies.GroupBy(p => p.StaffId!.Value).ToDictionary(g => g.Key, g => g.First());

                foreach (var record in year)
                {
                    ApplyLeavePolicy(record, policyByStaff.GetValueOrDefault(record.StaffId),
                        entriesByStaff[record.StaffId].ToList(), attendanceByStaff[record.StaffId]);
                    if (record.LeaveWithoutPay) lopCount++;
                }
            }
            return lopCount;
        }

        private static void ApplyLeavePolicy(AttendanceRecord record, LeaveEntitlement? policy,
            List<StaffLeaveEntry> entries, IEnumerable<AttendanceRecord> attendance)
        {
            var days = LeaveBalanceRules.AttendanceDays(record);
            if (days == 0m || entries.Any(e => e.StaffId == record.StaffId
                    && e.FromDate.Date <= record.AttendanceDate.Date && e.ToDate.Date >= record.AttendanceDate.Date))
            {
                record.LeaveWithoutPay = false;
                return;
            }
            if (policy == null)
            {
                record.LeaveWithoutPay = true;
                return;
            }
            var taken = LeaveBalanceRules.Taken(record.StaffId, record.AttendanceDate.Year, policy.LeaveType,
                true, entries, attendance.Where(other => other.Id != record.Id));
            record.LeaveWithoutPay = taken + days > policy.AnnualQuota;
        }

        // ── Monthly calendar and reports ──────────────────────────────

        /// <summary>The month's records reduced to what the reports read (no staff join, no full rows).</summary>
        private record MonthRecord(Guid StaffId, DateTime AttendanceDate, string Status, decimal OvertimeHours);

        private Task<List<MonthRecord>> MonthRecordsAsync(DateTime from, DateTime to) =>
            _context.AttendanceRecords.AsNoTracking()
                .Where(r => r.AttendanceDate >= from && r.AttendanceDate <= to)
                .Select(r => new MonthRecord(r.StaffId, r.AttendanceDate, r.Status, r.OvertimeHours))
                .ToListAsync();

        public async Task<MonthlyCalendar> MonthlyCalendarAsync(DateTime monthStart, string? type, string? q, string? status)
        {
            var end = monthStart.AddMonths(1).AddDays(-1);
            var staff = await MonthlyStaffAsync(monthStart, end);
            var records = await MonthRecordsAsync(monthStart, end);
            var types = DistinctDesignations(staff);

            staff = ByDesignation(staff, type);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim();
                staff = staff.Where(s =>
                    s.EmployeeId.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || s.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || s.Designation.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var staffIds = staff.Select(s => s.Id).ToHashSet();
            var recordsByStaffAndDay = records.Where(r => staffIds.Contains(r.StaffId))
                .ToLookup(r => (r.StaffId, r.AttendanceDate.Day));
            var days = Enumerable.Range(1, DateTime.DaysInMonth(monthStart.Year, monthStart.Month)).ToList();
            var rows = staff.Select(s => new MonthlyCalendarRow(s.EmployeeId, s.FullName, s.Designation,
                    days.Select(day => recordsByStaffAndDay[(s.Id, day)].Select(r => r.Status).FirstOrDefault()).ToList()))
                .Where(row => status == null || row.Days.Any(day => status == "NOT_MARKED" ? day == null : day == status))
                .ToList();

            return new MonthlyCalendar(monthStart.ToString("yyyy-MM"), days, types, rows);
        }

        public async Task<MonthlyAttendanceData> MonthlyAttendanceReportAsync(DateTime monthStart, string? type)
        {
            var end = monthStart.AddMonths(1).AddDays(-1);
            var staff = await MonthlyStaffAsync(monthStart, end);
            var types = DistinctDesignations(staff);
            staff = ByDesignation(staff, type);

            var recordsByStaff = (await MonthRecordsAsync(monthStart, end)).ToLookup(r => r.StaffId);
            var monthDays = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var weekOffs = Enumerable.Range(0, monthDays).Count(d => monthStart.AddDays(d).DayOfWeek == DayOfWeek.Sunday);
            var rows = staff.Select(s =>
                {
                    var records = recordsByStaff[s.Id].ToList();
                    return new MonthlyAttendanceRow(s.EmployeeId, s.FullName, s.Designation, monthDays, monthDays - weekOffs, weekOffs,
                        records.Count(r => AttendanceStatuses.Worked(r.Status)),
                        records.Count(r => r.Status == AttendanceStatuses.Absent),
                        records.Count(r => r.Status == AttendanceStatuses.Leave),
                        records.Sum(r => r.OvertimeHours));
                })
                .ToList();

            return new MonthlyAttendanceData(monthStart.Year, monthStart.Month, monthDays, types, rows);
        }

        public async Task<SalaryReportData> SalaryReportAsync(DateTime monthStart, string? type, string? q)
        {
            var end = monthStart.AddMonths(1).AddDays(-1);
            var staff = await MonthlyStaffAsync(monthStart, end);
            var departments = await DepartmentNamesAsync();
            var types = departments.Values.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

            if (!string.IsNullOrWhiteSpace(type))
                staff = staff.Where(s => string.Equals(
                    departments.GetValueOrDefault(s.DepartmentId), type.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            var recordsByStaff = (await MonthRecordsAsync(monthStart, end)).ToLookup(r => r.StaffId);
            var monthDays = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var rows = staff.Select(s =>
            {
                var workedDays = recordsByStaff[s.Id].Sum(r => r.Status switch
                {
                    AttendanceStatuses.Present => 1m,
                    AttendanceStatuses.HalfDay => 0.5m,
                    _ => 0m
                });
                var totalSalary = Math.Round(s.MonthlySalary * workedDays / monthDays, 2, MidpointRounding.AwayFromZero);
                return new SalaryReportRow(s.EmployeeId, s.FullName, departments.GetValueOrDefault(s.DepartmentId) ?? string.Empty,
                    s.Designation, monthDays, workedDays, totalSalary);
            }).Where(row => MatchesSalaryQuery(row, q))
              .OrderBy(row => row.Department, StringComparer.OrdinalIgnoreCase)
              .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
              .ThenBy(row => row.EmployeeId, StringComparer.OrdinalIgnoreCase)
              .ToList();

            return new SalaryReportData(monthStart.Year, monthStart.Month, monthDays, types, rows, rows.Sum(row => row.TotalSalary));
        }

        public async Task<AbstractReportData> AbstractReportAsync(DateTime date, string staffFallbackSection, string artisansLabel)
        {
            var staff = await SheetAsync(date);
            var artisans = await _context.Artisans.AsNoTracking()
                .Select(a => new { a.ArtisanCode, a.FullName, a.Gender, a.Mobile }).ToListAsync();
            var sections = new List<AbstractSection>();
            var employees = new List<AbstractEmployee>();

            foreach (var group in staff
                .GroupBy(row => string.IsNullOrWhiteSpace(row.DepartmentName) ? staffFallbackSection : row.DepartmentName.Trim())
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                var key = "staff:" + group.Key;
                var rows = group.OrderBy(row => row.Staff.FullName, StringComparer.OrdinalIgnoreCase).ToList();
                bool Attended(SheetRow row) => row.Record?.Status is AttendanceStatuses.Present or AttendanceStatuses.HalfDay;
                var attended = rows.Count(Attended);
                sections.Add(new AbstractSection(key, group.Key, rows.Count, attended, rows.Count - attended));
                employees.AddRange(rows.Select(row => new AbstractEmployee(key, row.Staff.EmployeeId, row.Staff.FullName, group.Key,
                    row.Staff.Designation, row.Staff.Gender, row.Staff.Mobile, Attended(row), row.Record?.Status ?? "NOT_MARKED")));
            }

            const string artisanSectionKey = "artisans";
            if (artisans.Count > 0)
            {
                sections.Insert(0, new AbstractSection(artisanSectionKey, artisansLabel, artisans.Count, null, null));
                employees.InsertRange(0, artisans.OrderBy(artisan => artisan.FullName, StringComparer.OrdinalIgnoreCase)
                    .Select(artisan => new AbstractEmployee(artisanSectionKey, artisan.ArtisanCode, artisan.FullName, artisansLabel,
                        artisansLabel, artisan.Gender, artisan.Mobile, null, null)));
            }

            return new AbstractReportData(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sections, employees);
        }

        private static List<string> DistinctDesignations(IEnumerable<Staff> staff) =>
            staff.Select(s => s.Designation.Trim()).Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();

        private static List<Staff> ByDesignation(List<Staff> staff, string? type) =>
            string.IsNullOrWhiteSpace(type)
                ? staff
                : staff.Where(s => string.Equals(s.Designation.Trim(), type.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        private static bool MatchesSalaryQuery(SalaryReportRow row, string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            var value = query.Trim();
            return row.EmployeeId.Contains(value, StringComparison.OrdinalIgnoreCase)
                || row.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
                || row.Department.Contains(value, StringComparison.OrdinalIgnoreCase)
                || row.Designation.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
