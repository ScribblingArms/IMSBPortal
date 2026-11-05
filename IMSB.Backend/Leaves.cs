using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Leaves : BackendBase, ILeavesInterface
    {
        public Leaves(IMSBContext context) : base(context) { }

        private static int CurrentYear => DateTime.Today.Year;

        public async Task<List<LeaveStaffOption>> StaffAsync() =>
            (await OptionsAsync(_context.Staff.AsNoTracking()))
                .OrderBy(s => s.StaffCategory).ThenBy(s => s.FullName).ToList();

        public Task<List<LeaveStaffOption>> ActiveStaffAsync() =>
            OptionsAsync(_context.Staff.AsNoTracking().Where(s => s.Status.ToUpper() != "INACTIVE").OrderBy(s => s.FullName));

        private async Task<List<LeaveStaffOption>> OptionsAsync(IQueryable<Staff> staff)
        {
            var categories = await StaffCategoryNamesAsync();
            return (await staff.ToListAsync())
                .Select(s => new LeaveStaffOption(s.Id, s.EmployeeId, s.FullName, categories.GetValueOrDefault(s.DepartmentId) ?? string.Empty))
                .ToList();
        }

        // A staff member's category is their department
        private Task<Dictionary<Guid, string>> StaffCategoryNamesAsync() =>
            _context.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name);

        public Task<List<string>> LeaveTypesAsync() =>
            _context.LeaveTypes.AsNoTracking().OrderBy(t => t.Name).Select(t => t.Name).ToListAsync();

        public Task<List<LeaveEntitlement>> EntitlementsWithStaffAsync(int year) =>
            _context.LeaveEntitlements.Include(e => e.Staff).AsNoTracking().Where(e => e.Year == year)
                .OrderBy(e => e.Staff!.FullName).ThenBy(e => e.LeaveType).ToListAsync();

        public Task<List<LeaveEntitlement>> EntitlementsAsync(int year) =>
            _context.LeaveEntitlements.AsNoTracking().Where(e => e.Year == year)
                .OrderBy(e => e.StaffId).ThenBy(e => e.LeaveType).ToListAsync();

        public Task<List<StaffLeaveEntry>> EntriesAsync(int year) =>
            _context.StaffLeaveEntries.Include(e => e.Staff).AsNoTracking().Where(e => e.Year == year)
                .OrderByDescending(e => e.FromDate).ThenBy(e => e.Staff!.FullName).ToListAsync();

        public async Task<List<LeaveBalanceRow>> BalancesAsync(int year, string? employeeId = null, string? leaveType = null)
        {
            var staffQuery = _context.Staff.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(employeeId))
                staffQuery = staffQuery.Where(person => person.EmployeeId == employeeId);
            var categories = await StaffCategoryNamesAsync();
            string CategoryOf(Staff person) => categories.GetValueOrDefault(person.DepartmentId) ?? string.Empty;
            var staff = (await staffQuery.ToListAsync())
                .OrderBy(CategoryOf).ThenBy(person => person.FullName).ToList();
            var entitlements = await EntitlementsAsync(year);
            if (!string.IsNullOrWhiteSpace(leaveType))
                entitlements = entitlements.Where(item =>
                    string.Equals(item.LeaveType, leaveType, StringComparison.OrdinalIgnoreCase)).ToList();
            var entries = await _context.StaffLeaveEntries.AsNoTracking().Where(entry => entry.Year == year).ToListAsync();
            var attendance = await AttendanceInYear(year).ToListAsync();
            var byStaff = entitlements.ToLookup(item => item.StaffId);
            // per-staff slices, so each balance only scans that person's rows
            var entriesByStaff = entries.ToLookup(entry => entry.StaffId);
            var attendanceByStaff = attendance.ToLookup(record => record.StaffId);

            return staff.SelectMany(employee => byStaff[employee.Id].Select(entitlement =>
            {
                var taken = LeaveBalanceRules.Taken(employee.Id, year, entitlement.LeaveType,
                    entitlement.IsAttendanceLeaveType, entriesByStaff[employee.Id].ToList(), attendanceByStaff[employee.Id]);
                return new LeaveBalanceRow(employee.EmployeeId, employee.FullName, CategoryOf(employee),
                    entitlement.LeaveType, entitlement.AnnualQuota, taken, entitlement.AnnualQuota - taken);
            })).ToList();
        }

        public async Task<BackendResult> SaveEntitlementAsync(Guid staffId, string leaveType, decimal annualQuota)
        {
            var staff = await _context.Staff.FirstOrDefaultAsync(person => person.Id == staffId);
            if (staff == null) return BackendResult.Error("ValStaffNotFound");
            var type = leaveType.Trim();
            var year = CurrentYear;
            var existing = await _context.LeaveEntitlements.FirstOrDefaultAsync(e => e.Year == year
                && e.StaffId == staff.Id && e.LeaveType.ToUpper() == type.ToUpper());
            var wasAttendancePolicy = existing?.IsAttendanceLeaveType == true;

            var manualEntries = await _context.StaffLeaveEntries.AsNoTracking()
                .Where(entry => entry.Year == year && entry.StaffId == staff.Id).ToListAsync();
            var attendance = await AttendanceInYear(year).Where(record => record.StaffId == staff.Id).ToListAsync();
            var appliedType = existing?.IsAttendanceLeaveType == true
                ? type
                : await _context.LeaveEntitlements.Where(item => item.StaffId == staff.Id
                        && item.Year == year && item.IsAttendanceLeaveType)
                    .Select(item => item.LeaveType).FirstOrDefaultAsync();
            var maximumTaken = manualEntries.Where(entry => entry.LeaveType.ToUpper() == type.ToUpper()).Sum(entry => entry.Days)
                + (string.Equals(appliedType, type, StringComparison.OrdinalIgnoreCase)
                    ? attendance.Where(record => !record.LeaveWithoutPay && !LeaveBalanceRules.IsCoveredByManualLeave(record, manualEntries))
                        .Sum(LeaveBalanceRules.AttendanceDays)
                    : 0m);
            if (annualQuota < maximumTaken) return BackendResult.Error("ValLeaveQuotaBelowTaken");

            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;
            // a quota for a new type must still be selectable when applying for leave
            if (!await _context.LeaveTypes.AnyAsync(t => t.Name.ToUpper() == type.ToUpper()))
                _context.LeaveTypes.Add(new LeaveType { Name = type });

            LeaveEntitlement entitlement;
            if (existing == null)
            {
                var alreadyHasAttendancePolicy = await _context.LeaveEntitlements.AnyAsync(item =>
                    item.StaffId == staff.Id && item.Year == year && item.IsAttendanceLeaveType);
                entitlement = new LeaveEntitlement
                {
                    StaffId = staff.Id,
                    StaffCategory = staff.StaffCategory,
                    LeaveType = type,
                    Year = year,
                    AnnualQuota = annualQuota,
                    IsAttendanceLeaveType = !alreadyHasAttendancePolicy
                };
                _context.LeaveEntitlements.Add(entitlement);
            }
            else
            {
                existing.AnnualQuota = annualQuota;
                existing.StaffCategory = staff.StaffCategory;
                entitlement = existing;
            }
            var attendancePolicy = wasAttendancePolicy || entitlement.IsAttendanceLeaveType
                ? entitlement
                : await _context.LeaveEntitlements.AsNoTracking().FirstOrDefaultAsync(e => e.Year == year
                    && e.StaffId == staff.Id && e.IsAttendanceLeaveType);
            await ReconcileAttendanceLeavePolicyAsync(staff.Id, year, attendancePolicy);
            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
            return BackendResult.Done;
        }

        public async Task<BackendResult> DeleteEntitlementAsync(Guid id)
        {
            var entitlement = await _context.LeaveEntitlements.FirstOrDefaultAsync(e => e.Id == id && e.Year == CurrentYear);
            if (entitlement == null) return BackendResult.Missing;

            var staffId = entitlement.StaffId;
            if (staffId.HasValue && await _context.StaffLeaveEntries.AnyAsync(e => e.Year == entitlement.Year && e.StaffId == staffId
                    && e.LeaveType.ToUpper() == entitlement.LeaveType.ToUpper()))
                return BackendResult.Error("ValLeaveTypeInUse");
            if (entitlement.IsAttendanceLeaveType && await AttendanceInYear(entitlement.Year).AnyAsync(r =>
                    r.StaffId == staffId
                    && (r.Status == AttendanceStatuses.Leave || r.Status == AttendanceStatuses.HalfDay)))
                return BackendResult.Error("ValLeaveTypeInUse");

            _context.LeaveEntitlements.Remove(entitlement);
            if (staffId.HasValue && entitlement.IsAttendanceLeaveType)
                await ReconcileAttendanceLeavePolicyAsync(staffId.Value, entitlement.Year, null);
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        public async Task<BackendResult> RecordEntryAsync(Guid staffId, string leaveType, DateTime? from, DateTime? to, string? remarks, string? user)
        {
            var staff = await _context.Staff.FirstOrDefaultAsync(s => s.Id == staffId);
            if (staff == null) return BackendResult.Error("ValStaffNotFound");
            if (from == null || to == null) return BackendResult.Error("ValLeaveDates");
            var invalid = await ValidateEntryAsync(staff, leaveType, from.Value, to.Value, null);
            if (invalid != null) return BackendResult.Error(invalid);

            _context.StaffLeaveEntries.Add(new StaffLeaveEntry
            {
                StaffId = staff.Id,
                LeaveType = leaveType.Trim(),
                Year = CurrentYear,
                FromDate = from.Value,
                ToDate = to.Value,
                Days = CalendarDays(from.Value, to.Value),
                Remarks = CleanRemarks(remarks),
                CreatedBy = user
            });
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        public async Task<BackendResult> UpdateEntryAsync(Guid id, DateTime? from, DateTime? to, string? remarks, string? user)
        {
            var entry = await _context.StaffLeaveEntries.Include(e => e.Staff)
                .FirstOrDefaultAsync(e => e.Id == id && e.Year == CurrentYear);
            if (entry?.Staff == null) return BackendResult.Missing;
            if (from == null || to == null) return BackendResult.Error("ValLeaveDates");
            if (remarks?.Length > 500) return BackendResult.Error("ValRemarks");
            var invalid = await ValidateEntryAsync(entry.Staff, entry.LeaveType, from.Value, to.Value, entry.Id);
            if (invalid != null) return BackendResult.Error(invalid);

            entry.FromDate = from.Value;
            entry.ToDate = to.Value;
            entry.Days = CalendarDays(from.Value, to.Value);
            entry.Remarks = CleanRemarks(remarks);
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = user;
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        public async Task<BackendResult> DeleteEntryAsync(Guid id)
        {
            var entry = await _context.StaffLeaveEntries.FirstOrDefaultAsync(e => e.Id == id && e.Year == CurrentYear);
            if (entry == null) return BackendResult.Missing;
            _context.StaffLeaveEntries.Remove(entry);
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        /// <summary>Resource key of the reason the leave cannot be recorded, or null.</summary>
        private async Task<string?> ValidateEntryAsync(Staff staff, string leaveType, DateTime from, DateTime to, Guid? excludingEntryId)
        {
            var year = CurrentYear;
            if (from.Year != year || to.Year != year || to < from) return "ValLeaveDates";
            if (from.Date <= DateTime.Today || to.Date <= DateTime.Today) return "ValLeaveFutureDate";

            var entitlement = await _context.LeaveEntitlements.AsNoTracking().FirstOrDefaultAsync(e => e.Year == year
                && e.StaffId == staff.Id
                && e.LeaveType.ToUpper() == leaveType.Trim().ToUpper());
            if (entitlement == null) return "ValLeaveTypeNotConfigured";

            var existing = await _context.StaffLeaveEntries.AsNoTracking().Where(e => e.StaffId == staff.Id && e.Year == year
                && (!excludingEntryId.HasValue || e.Id != excludingEntryId.Value)).ToListAsync();
            if (existing.Any(e => e.FromDate <= to && e.ToDate >= from)) return "ValLeaveDatesOverlap";
            var attendance = await AttendanceInYear(year).Where(r => r.StaffId == staff.Id).ToListAsync();
            var taken = LeaveBalanceRules.Taken(staff.Id, year, entitlement.LeaveType,
                entitlement.IsAttendanceLeaveType, existing, attendance);
            return taken + CalendarDays(from, to) > entitlement.AnnualQuota ? "ValInsufficientLeaveBalance" : null;
        }

        /// <summary>Re-applies the attendance leave policy to the year's Leave / Half Day records, oldest first.</summary>
        private async Task ReconcileAttendanceLeavePolicyAsync(Guid staffId, int year, LeaveEntitlement? policy)
        {
            var leaveEntries = await _context.StaffLeaveEntries.AsNoTracking()
                .Where(entry => entry.Year == year && entry.StaffId == staffId).ToListAsync();
            var attendance = await AttendanceInYear(year, tracking: true)
                .Where(record => record.StaffId == staffId)
                .OrderBy(record => record.AttendanceDate).ThenBy(record => record.Id).ToListAsync();

            var remaining = policy == null ? 0m : policy.AnnualQuota - leaveEntries
                .Where(entry => entry.LeaveType.ToUpper() == policy.LeaveType.ToUpper())
                .Sum(entry => entry.Days);
            foreach (var record in attendance)
            {
                var days = LeaveBalanceRules.AttendanceDays(record);
                if (days == 0m || LeaveBalanceRules.IsCoveredByManualLeave(record, leaveEntries))
                {
                    record.LeaveWithoutPay = false;
                    continue;
                }
                if (policy == null || remaining < days)
                {
                    record.LeaveWithoutPay = true;
                    continue;
                }
                record.LeaveWithoutPay = false;
                remaining -= days;
            }
        }

        private static decimal CalendarDays(DateTime from, DateTime to) => (to.Date - from.Date).Days + 1;

        private static string? CleanRemarks(string? remarks) => string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
    }
}
