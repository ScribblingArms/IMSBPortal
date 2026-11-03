using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Payroll : BackendBase, IPayrollInterface
    {
        public Payroll(IMSBContext context) : base(context) { }

        public Task<bool> IsGeneratedAsync(DateTime monthStart) =>
            _context.PayBillEntries.AnyAsync(e => e.Year == monthStart.Year && e.Month == monthStart.Month);

        public async Task<List<PayBillRow>> RowsAsync(DateTime monthStart, string? query = null)
        {
            var saved = await _context.PayBillEntries.AsNoTracking()
                .Where(entry => entry.Year == monthStart.Year && entry.Month == monthStart.Month)
                .ToListAsync();
            var savedByStaff = saved.ToDictionary(entry => entry.StaffId);
            var (staff, attendance) = await PayableStaffAsync(monthStart, savedByStaff.Keys.ToList(), tracking: false);
            var departments = await DepartmentNamesAsync();
            var days = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

            var rows = staff.Select(employee =>
            {
                savedByStaff.TryGetValue(employee.Id, out var entry);
                var records = attendance[employee.Id].Where(record => record.AttendanceDate.Date >= employee.JoinDate.Date).ToList();
                var paidDays = entry?.PaidDays ?? PaidDays(records);
                var basicPay = entry?.BasicPay ?? ProratedPay(employee.MonthlySalary, paidDays, days);
                var allowances = entry?.Allowances ?? 0m;
                var overtimeAmount = entry?.OvertimeAmount ?? 0m;
                var deductions = entry?.Deductions ?? 0m;
                return new PayBillRow(
                    employee.Id,
                    employee.EmployeeId,
                    employee.FullName,
                    departments.GetValueOrDefault(employee.DepartmentId) ?? string.Empty,
                    employee.Designation,
                    entry?.MonthDays ?? days,
                    paidDays,
                    entry?.MonthlySalary ?? employee.MonthlySalary,
                    basicPay,
                    allowances,
                    entry?.OvertimeHours ?? records.Sum(record => record.OvertimeHours),
                    overtimeAmount,
                    deductions,
                    entry?.NetPay ?? NetPay(basicPay, allowances, overtimeAmount, deductions));
            }).ToList();

            // saved rows of staff who have since been removed
            var currentIds = rows.Select(row => row.StaffId).ToHashSet();
            rows.AddRange(saved.Where(entry => !currentIds.Contains(entry.StaffId)).Select(entry => new PayBillRow(
                entry.StaffId, entry.EmployeeId, entry.EmployeeName, entry.Department, entry.Designation, entry.MonthDays,
                entry.PaidDays, entry.MonthlySalary, entry.BasicPay, entry.Allowances, entry.OvertimeHours,
                entry.OvertimeAmount, entry.Deductions, entry.NetPay)));
            return rows.Where(row => MatchesStaffFilter(row, query))
                .OrderBy(row => row.Department, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.EmployeeId, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<bool> GenerateAsync(DateTime monthStart, IReadOnlyCollection<PayBillAdjustmentDto> rows)
        {
            var existing = await _context.PayBillEntries
                .Where(entry => entry.Year == monthStart.Year && entry.Month == monthStart.Month)
                .ToDictionaryAsync(entry => entry.StaffId);
            var (staff, attendance) = await PayableStaffAsync(monthStart, existing.Keys.ToList(), tracking: true);
            var staffIds = staff.Select(employee => employee.Id).ToHashSet();
            if (rows.Any(row => !staffIds.Contains(row.StaffId))) return false;

            var departments = await DepartmentNamesAsync();
            var adjustments = rows.ToDictionary(row => row.StaffId);
            var monthDays = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

            foreach (var employee in staff)
            {
                existing.TryGetValue(employee.Id, out var entry);
                adjustments.TryGetValue(employee.Id, out var adjustment);
                var attendanceRows = attendance[employee.Id].Where(record => record.AttendanceDate.Date >= employee.JoinDate.Date).ToList();
                var paidDays = PaidDays(attendanceRows);
                var basicPay = ProratedPay(employee.MonthlySalary, paidDays, monthDays);
                var allowances = adjustment?.Allowances ?? entry?.Allowances ?? 0m;
                var overtimeAmount = adjustment?.OvertimeAmount ?? entry?.OvertimeAmount ?? 0m;
                var deductions = adjustment?.Deductions ?? entry?.Deductions ?? 0m;

                if (entry == null)
                {
                    entry = new PayBillEntry { StaffId = employee.Id, Year = monthStart.Year, Month = monthStart.Month };
                    _context.PayBillEntries.Add(entry);
                }

                entry.EmployeeId = employee.EmployeeId;
                entry.EmployeeName = employee.FullName;
                entry.Department = departments.GetValueOrDefault(employee.DepartmentId) ?? string.Empty;
                entry.Designation = employee.Designation;
                entry.MonthDays = monthDays;
                entry.PaidDays = paidDays;
                entry.MonthlySalary = employee.MonthlySalary;
                entry.BasicPay = basicPay;
                entry.Allowances = allowances;
                entry.OvertimeHours = attendanceRows.Sum(record => record.OvertimeHours);
                entry.OvertimeAmount = overtimeAmount;
                entry.Deductions = deductions;
                entry.NetPay = NetPay(basicPay, allowances, overtimeAmount, deductions);
                entry.GeneratedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public Task<List<PayBillHistoryRow>> HistoryAsync(Guid staffId, DateTime monthStart, int take) =>
            _context.PayBillEntries.AsNoTracking()
                .Where(entry => entry.StaffId == staffId
                    && (entry.Year < monthStart.Year || (entry.Year == monthStart.Year && entry.Month < monthStart.Month)))
                .OrderByDescending(entry => entry.Year).ThenByDescending(entry => entry.Month)
                .Take(take)
                .Select(entry => new PayBillHistoryRow(entry.Year, entry.Month, entry.PaidDays, entry.BasicPay,
                    entry.Allowances, entry.OvertimeAmount, entry.Deductions, entry.NetPay))
                .ToListAsync();

        /// <summary>
        /// Staff on the month's pay bill (joined by its end and active, already on the saved bill, or marked during the
        /// month), by department and name, with their attendance records in the month.
        /// </summary>
        private async Task<(List<Staff> Staff, ILookup<Guid, AttendanceRecord> Attendance)> PayableStaffAsync(
            DateTime monthStart, List<Guid> savedStaffIds, bool tracking)
        {
            var end = monthStart.AddMonths(1).AddDays(-1);
            var endExclusive = end.AddDays(1);
            var markedStaff = _context.AttendanceRecords
                .Where(record => record.AttendanceDate >= monthStart && record.AttendanceDate <= end)
                .Select(record => record.StaffId);
            var query = tracking ? _context.Staff : _context.Staff.AsNoTracking();
            var staff = await query
                .Where(employee => employee.JoinDate < endExclusive
                    && (employee.Status.ToUpper() != "INACTIVE" || savedStaffIds.Contains(employee.Id) || markedStaff.Contains(employee.Id)))
                .OrderBy(employee => employee.DepartmentId).ThenBy(employee => employee.FullName)
                .ToListAsync();
            var staffIds = staff.Select(employee => employee.Id).ToList();
            var attendance = await _context.AttendanceRecords.AsNoTracking()
                .Where(record => staffIds.Contains(record.StaffId)
                    && record.AttendanceDate >= monthStart && record.AttendanceDate <= end)
                .ToListAsync();
            return (staff, attendance.ToLookup(record => record.StaffId));
        }

        private Task<Dictionary<Guid, string>> DepartmentNamesAsync() =>
            _context.Departments.AsNoTracking().ToDictionaryAsync(department => department.Id, department => department.Name);

        private static bool MatchesStaffFilter(PayBillRow row, string? query) =>
            string.IsNullOrWhiteSpace(query)
            || row.EmployeeName.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
            || row.EmployeeId.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>Present = 1, Half Day = 0.5, paid leave = 1; absent and loss-of-pay leave are unpaid.</summary>
        public static decimal PaidDays(IEnumerable<AttendanceRecord> records) =>
            records.Sum(record => record.Status switch
            {
                AttendanceStatuses.Present => 1m,
                AttendanceStatuses.HalfDay => 0.5m,
                AttendanceStatuses.Leave when !record.LeaveWithoutPay => 1m,
                _ => 0m
            });

        public static decimal ProratedPay(decimal monthlySalary, decimal paidDays, int monthDays) =>
            Math.Round(monthlySalary * paidDays / monthDays, 2, MidpointRounding.AwayFromZero);

        private static decimal NetPay(decimal basicPay, decimal allowances, decimal overtimeAmount, decimal deductions) =>
            Math.Max(0m, basicPay + allowances + overtimeAmount - deductions);
    }
}
