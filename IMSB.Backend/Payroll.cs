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

        public async Task<List<string>> DepartmentsAsync() =>
            (await _context.Departments.AsNoTracking().Select(department => department.Name).Distinct().ToListAsync())
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();

        public async Task<List<PayBillRow>> RowsAsync(DateTime monthStart, string? query = null, string? employeeId = null)
        {
            var saved = await _context.PayBillEntries.AsNoTracking()
                .Where(entry => entry.Year == monthStart.Year && entry.Month == monthStart.Month)
                .ToListAsync();
            var savedByStaff = saved.ToDictionary(entry => entry.StaffId);
            var (staff, attendance, lopLeave) = await PayableStaffAsync(monthStart, savedByStaff.Keys.ToList(), tracking: false);
            var departments = await DepartmentNamesAsync();
            var days = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var overtime = await OvertimePay.ForMonthAsync(_context, staff, departments, monthStart);

            var rows = staff.Select(employee =>
            {
                // a generated month keeps its saved figures; otherwise they are worked out from attendance and OT assignments
                if (savedByStaff.TryGetValue(employee.Id, out var entry))
                    return SavedRow(entry) with
                    {
                        // current details of staff still on the books
                        EmployeeId = employee.EmployeeId,
                        EmployeeName = employee.FullName,
                        Department = departments.GetValueOrDefault(employee.DepartmentId) ?? string.Empty,
                        Designation = employee.Designation
                    };
                var (paidDays, basicPay, ot) = Fresh(employee, monthStart, attendance, lopLeave, overtime);
                return new PayBillRow(
                    employee.Id,
                    employee.EmployeeId,
                    employee.FullName,
                    departments.GetValueOrDefault(employee.DepartmentId) ?? string.Empty,
                    employee.Designation,
                    days,
                    paidDays,
                    employee.MonthlySalary,
                    basicPay,
                    0m,
                    ot.Hours,
                    ot.Amount,
                    0m,
                    NetPay(employee.MonthlySalary, ot.Amount, basicPay));
            }).ToList();

            // saved rows of staff who have since been removed
            var currentIds = rows.Select(row => row.StaffId).ToHashSet();
            rows.AddRange(saved.Where(entry => !currentIds.Contains(entry.StaffId)).Select(entry => SavedRow(entry)));

            var search = query?.Trim();
            var exactId = employeeId?.Trim();
            return rows.Where(row => MatchesStaffFilter(row, search)
                    && (string.IsNullOrEmpty(exactId) || string.Equals(row.EmployeeId, exactId, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(row => row.Department, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.EmployeeId, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<bool> GenerateAsync(DateTime monthStart, IReadOnlyCollection<PayBillAdjustmentDto> rows)
        {
            var existing = await _context.PayBillEntries
                .Where(entry => entry.Year == monthStart.Year && entry.Month == monthStart.Month)
                .ToDictionaryAsync(entry => entry.StaffId);
            var (staff, attendance, lopLeave) = await PayableStaffAsync(monthStart, existing.Keys.ToList(), tracking: true);
            var staffIds = staff.Select(employee => employee.Id).ToHashSet();
            if (rows.Any(row => !staffIds.Contains(row.StaffId))) return false;

            var departments = await DepartmentNamesAsync();
            var adjustments = rows.ToDictionary(row => row.StaffId);
            var monthDays = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var overtime = await OvertimePay.ForMonthAsync(_context, staff, departments, monthStart);

            foreach (var employee in staff)
            {
                existing.TryGetValue(employee.Id, out var entry);
                adjustments.TryGetValue(employee.Id, out var adjustment);
                var (paidDays, basicPay, ot) = Fresh(employee, monthStart, attendance, lopLeave, overtime);
                var allowances = adjustment?.Allowances ?? entry?.Allowances ?? 0m;
                // OT is worked out again from the OT assignments on every generate, unless an amount is given for this employee
                var overtimeAmount = adjustment?.OvertimeAmount ?? ot.Amount;
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
                entry.OvertimeHours = ot.Hours;
                entry.OvertimeAmount = overtimeAmount;
                entry.Deductions = deductions;
                entry.NetPay = NetPay(employee.MonthlySalary, overtimeAmount, basicPay);
                entry.GeneratedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public Task<List<PayBillHistoryRow>> HistoryAsync(int staffId, DateTime monthStart, int take) =>
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
        /// month), by department and name, with their attendance records and LOP leave entries in the month.
        /// </summary>
        private async Task<(List<Staff> Staff, ILookup<int, AttendanceRecord> Attendance, ILookup<int, StaffLeaveEntry> LopLeave)> PayableStaffAsync(
            DateTime monthStart, List<int> savedStaffIds, bool tracking)
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
            var lop = LeaveBalanceRules.LopLeaveType;
            var lopLeave = await _context.StaffLeaveEntries.AsNoTracking()
                .Where(entry => staffIds.Contains(entry.StaffId) && entry.LeaveType.ToUpper() == lop
                    && entry.FromDate <= end && entry.ToDate >= monthStart)
                .ToListAsync();
            return (staff, attendance.ToLookup(record => record.StaffId), lopLeave.ToLookup(entry => entry.StaffId));
        }

        private Task<Dictionary<int, string>> DepartmentNamesAsync() =>
            _context.Departments.AsNoTracking().ToDictionaryAsync(department => department.Id, department => department.Name);

        /// <summary>
        /// Payable days (month days − LOP days), the salary for them and OT, worked out from the month's attendance,
        /// LOP leave entries, join date and OT assignments.
        /// </summary>
        private static (decimal PayableDays, decimal BasicPay, OvertimeResult Ot) Fresh(Staff employee, DateTime monthStart,
            ILookup<int, AttendanceRecord> attendance, ILookup<int, StaffLeaveEntry> lopLeave, IReadOnlyDictionary<int, OvertimeResult> overtime)
        {
            var monthDays = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var payableDays = monthDays - LopDays(monthStart, employee.JoinDate, attendance[employee.Id], lopLeave[employee.Id]);
            return (payableDays, ProratedPay(employee.MonthlySalary, payableDays, monthDays),
                overtime.GetValueOrDefault(employee.Id) ?? OvertimeResult.None);
        }

        /// <summary>A pay bill row from the saved figures, with Net Pay recomputed from them.</summary>
        private static PayBillRow SavedRow(PayBillEntry entry) => new(
            entry.StaffId, entry.EmployeeId, entry.EmployeeName, entry.Department, entry.Designation, entry.MonthDays,
            entry.PaidDays, entry.MonthlySalary, entry.BasicPay, entry.Allowances, entry.OvertimeHours,
            entry.OvertimeAmount, entry.Deductions, NetPay(entry.MonthlySalary, entry.OvertimeAmount, entry.BasicPay));

        /// <summary>query is already trimmed: matches name or employee id (contains).</summary>
        private static bool MatchesStaffFilter(PayBillRow row, string? query) =>
            string.IsNullOrEmpty(query)
            || row.EmployeeName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.EmployeeId.Contains(query, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The month's loss-of-pay days, day by day: Absent = 1, Leave without pay = 1, Half Day without pay = 0.5,
        /// a day under an LOP leave entry with no attendance mark = 1, a day before joining = 1. Every other day,
        /// marked or not, is payable.
        /// </summary>
        public static decimal LopDays(DateTime monthStart, DateTime joinDate,
            IEnumerable<AttendanceRecord> attendance, IEnumerable<StaffLeaveEntry> lopLeave)
        {
            var marks = attendance.GroupBy(record => record.AttendanceDate.Date).ToDictionary(day => day.Key, day => day.First());
            var lopEntries = lopLeave.Where(entry => LeaveBalanceRules.IsLop(entry.LeaveType)).ToList();
            var lopDays = 0m;
            for (var day = monthStart.Date; day.Month == monthStart.Month; day = day.AddDays(1))
            {
                if (day < joinDate.Date) { lopDays += 1m; continue; }
                lopDays += marks.TryGetValue(day, out var record)
                    ? record.Status switch
                    {
                        AttendanceStatuses.Absent => 1m,
                        AttendanceStatuses.Leave when record.LeaveWithoutPay => 1m,
                        AttendanceStatuses.HalfDay when record.LeaveWithoutPay => 0.5m,
                        _ => 0m
                    }
                    : lopEntries.Any(entry => entry.FromDate.Date <= day && entry.ToDate.Date >= day) ? 1m : 0m;
            }
            return lopDays;
        }

        public static decimal ProratedPay(decimal monthlySalary, decimal paidDays, int monthDays) =>
            Math.Round(monthlySalary * paidDays / monthDays, 2, MidpointRounding.AwayFromZero);

        /// <summary>LOP Deduction: the salary not earned for LOP days (Monthly Salary − pay for the payable days).</summary>
        public static decimal LopDeduction(decimal monthlySalary, decimal basicPay) => Math.Max(0m, monthlySalary - basicPay);

        /// <summary>
        /// Net Pay = Monthly Salary + Overtime Amount − LOP Deduction, the three amounts on the Employee-wise Salary
        /// Statement. Allowances and other deductions are kept on the bill but are not part of Net Pay.
        /// </summary>
        public static decimal NetPay(decimal monthlySalary, decimal overtimeAmount, decimal basicPay) =>
            Math.Max(0m, monthlySalary + overtimeAmount - LopDeduction(monthlySalary, basicPay));
    }
}
