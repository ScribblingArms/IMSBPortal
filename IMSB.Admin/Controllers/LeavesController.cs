using System.Globalization;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace IMSB.Admin.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class LeavesController : Controller
{
    private readonly IMSBContext _context;
    private readonly IStringLocalizer _L;

    public LeavesController(IMSBContext context, IStringLocalizerFactory localizerFactory)
    {
        _context = context;
        _L = localizerFactory.Create("SharedResource", typeof(LeavesController).Assembly.GetName().Name!);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var year = DateTime.Today.Year;
        var staff = await _context.Staff.AsNoTracking().OrderBy(s => s.StaffCategory).ThenBy(s => s.FullName).ToListAsync();
        var entitlements = await _context.LeaveEntitlements.Include(e => e.Staff).AsNoTracking().Where(e => e.Year == year)
            .OrderBy(e => e.Staff!.FullName).ThenBy(e => e.LeaveType).ToListAsync();

        ViewData["Title"] = _L["LeaveManagement"].Value;
        return View(new LeaveManagementViewModel
        {
            Year = year,
            Categories = staff.Select(s => s.StaffCategory.Trim()).Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList(),
            Staff = staff.Select(s => new LeaveStaffOption(s.Id, s.EmployeeId, s.FullName, s.StaffCategory)).ToList(),
            Entitlements = entitlements,
            Balances = await CurrentYearBalancesAsync(year)
        });
    }

    [HttpGet]
    public async Task<IActionResult> CurrentBalances(string? employeeId, string? leaveType)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        return Ok(new { rows = await CurrentYearBalancesAsync(DateTime.Today.Year, employeeId, leaveType) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEntitlement(LeaveEntitlementForm form)
    {
        if (!ModelState.IsValid || form.StaffId == Guid.Empty || string.IsNullOrWhiteSpace(form.LeaveType)
            || form.AnnualQuota % 0.5m != 0m)
            return LeaveIndexError(_L["ValLeaveEntitlement"].Value);

        var staff = await _context.Staff.FirstOrDefaultAsync(person => person.Id == form.StaffId);
        if (staff == null) return LeaveIndexError(_L["ValStaffNotFound"].Value);
        var type = form.LeaveType.Trim();
        var year = DateTime.Today.Year;
        var existing = await _context.LeaveEntitlements.FirstOrDefaultAsync(e => e.Year == year
            && e.StaffId == staff.Id && e.LeaveType.ToUpper() == type.ToUpper());
        var wasAttendancePolicy = existing?.IsAttendanceLeaveType == true;

        var manualEntries = await _context.StaffLeaveEntries.AsNoTracking()
            .Where(entry => entry.Year == year && entry.StaffId == staff.Id).ToListAsync();
        var attendance = await _context.AttendanceRecords.AsNoTracking()
            .Where(record => record.AttendanceDate.Year == year && record.StaffId == staff.Id).ToListAsync();
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
        if (form.AnnualQuota < maximumTaken)
            return LeaveIndexError(_L["ValLeaveQuotaBelowTaken"].Value);

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
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
                AnnualQuota = form.AnnualQuota,
                IsAttendanceLeaveType = !alreadyHasAttendancePolicy
            };
            _context.LeaveEntitlements.Add(entitlement);
        }
        else
        {
            existing.AnnualQuota = form.AnnualQuota;
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
        TempData["LeaveMessage"] = _L["LeaveEntitlementSaved"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntitlement(Guid id)
    {
        var entitlement = await _context.LeaveEntitlements.FirstOrDefaultAsync(e => e.Id == id && e.Year == DateTime.Today.Year);
        if (entitlement == null) return NotFound();

        var staffId = entitlement.StaffId;
        if (staffId.HasValue && await _context.StaffLeaveEntries.AnyAsync(e => e.Year == entitlement.Year && e.StaffId == staffId
                && e.LeaveType.ToUpper() == entitlement.LeaveType.ToUpper()))
            return LeaveIndexError(_L["ValLeaveTypeInUse"].Value);
        if (entitlement.IsAttendanceLeaveType && await _context.AttendanceRecords.AnyAsync(r =>
                r.StaffId == staffId && r.AttendanceDate.Year == entitlement.Year
                && (r.Status == AttendanceStatuses.Leave || r.Status == AttendanceStatuses.HalfDay)))
            return LeaveIndexError(_L["ValLeaveTypeInUse"].Value);

        _context.LeaveEntitlements.Remove(entitlement);
        if (staffId.HasValue && entitlement.IsAttendanceLeaveType)
            await ReconcileAttendanceLeavePolicyAsync(staffId.Value, entitlement.Year, null);
        await _context.SaveChangesAsync();
        TempData["LeaveMessage"] = _L["LeaveEntitlementDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Add()
    {
        var year = DateTime.Today.Year;
        var today = DateTime.Today;
        var endOfYear = new DateTime(year, 12, 31);
        var firstFutureDate = today.AddDays(1);
        var staff = await _context.Staff.AsNoTracking().Where(s => s.Status.ToUpper() != "INACTIVE")
            .OrderBy(s => s.FullName).ToListAsync();
        var entries = await _context.StaffLeaveEntries.Include(e => e.Staff).AsNoTracking().Where(e => e.Year == year)
            .OrderByDescending(e => e.FromDate).ThenBy(e => e.Staff!.FullName).ToListAsync();
        ViewData["Title"] = _L["AddLeaveTaken"].Value;
        return View(new LeaveEntryViewModel
        {
            Year = year,
            Form = new LeaveEntryForm
            {
                FromDate = firstFutureDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ToDate = firstFutureDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            },
            Staff = staff.Select(s => new LeaveStaffOption(s.Id, s.EmployeeId, s.FullName, s.StaffCategory)).ToList(),
            Entitlements = await _context.LeaveEntitlements.AsNoTracking().Where(e => e.Year == year)
                .OrderBy(e => e.StaffId).ThenBy(e => e.LeaveType).ToListAsync(),
            Entries = entries
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Record(LeaveEntryForm form)
    {
        if (!ModelState.IsValid) return LeaveAddError(_L["ValLeaveEntry"].Value);
        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.Id == form.StaffId);
        if (staff == null) return LeaveAddError(_L["ValStaffNotFound"].Value);
        if (!TryDates(form, out var from, out var to)) return LeaveAddError(_L["ValLeaveDates"].Value);
        var invalid = await ValidateEntryAsync(staff, form.LeaveType, from, to, null);
        if (invalid != null) return LeaveAddError(invalid);

        _context.StaffLeaveEntries.Add(new StaffLeaveEntry
        {
            StaffId = staff.Id,
            LeaveType = form.LeaveType.Trim(),
            Year = DateTime.Today.Year,
            FromDate = from,
            ToDate = to,
            Days = CalendarDays(from, to),
            Remarks = string.IsNullOrWhiteSpace(form.Remarks) ? null : form.Remarks.Trim(),
            CreatedBy = User.Identity?.Name
        });
        await _context.SaveChangesAsync();
        TempData["LeaveMessage"] = _L["LeaveEntrySaved"].Value;
        return RedirectToAction(nameof(Add));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEntry(Guid id, string fromDate, string toDate, string? remarks)
    {
        var entry = await _context.StaffLeaveEntries.Include(e => e.Staff)
            .FirstOrDefaultAsync(e => e.Id == id && e.Year == DateTime.Today.Year);
        if (entry?.Staff == null) return NotFound();
        var form = new LeaveEntryForm { FromDate = fromDate, ToDate = toDate };
        if (!TryDates(form, out var from, out var to))
            return LeaveAddError(_L["ValLeaveDates"].Value);
        if (remarks?.Length > 500) return LeaveAddError(_L["ValRemarks"].Value);
        var invalid = await ValidateEntryAsync(entry.Staff, entry.LeaveType, from, to, entry.Id);
        if (invalid != null) return LeaveAddError(invalid);

        entry.FromDate = from;
        entry.ToDate = to;
        entry.Days = CalendarDays(from, to);
        entry.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        entry.UpdatedAt = DateTime.UtcNow;
        entry.UpdatedBy = User.Identity?.Name;
        await _context.SaveChangesAsync();
        TempData["LeaveMessage"] = _L["LeaveEntrySaved"].Value;
        return RedirectToAction(nameof(Add));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(Guid id)
    {
        var entry = await _context.StaffLeaveEntries.FirstOrDefaultAsync(e => e.Id == id && e.Year == DateTime.Today.Year);
        if (entry == null) return NotFound();
        _context.StaffLeaveEntries.Remove(entry);
        await _context.SaveChangesAsync();
        TempData["LeaveMessage"] = _L["LeaveEntryDeleted"].Value;
        return RedirectToAction(nameof(Add));
    }

    private async Task<string?> ValidateEntryAsync(Staff staff, string leaveType, DateTime from, DateTime to,
        Guid? excludingEntryId)
    {
        var year = DateTime.Today.Year;
        if (from.Year != year || to.Year != year || to < from)
            return _L["ValLeaveDates"].Value;
        if (from.Date <= DateTime.Today || to.Date <= DateTime.Today)
            return _L["ValLeaveFutureDate"].Value;

        var entitlement = await _context.LeaveEntitlements.AsNoTracking().FirstOrDefaultAsync(e => e.Year == year
            && e.StaffId == staff.Id
            && e.LeaveType.ToUpper() == leaveType.Trim().ToUpper());
        if (entitlement == null) return _L["ValLeaveTypeNotConfigured"].Value;

        var existing = await _context.StaffLeaveEntries.AsNoTracking().Where(e => e.StaffId == staff.Id && e.Year == year
            && (!excludingEntryId.HasValue || e.Id != excludingEntryId.Value)).ToListAsync();
        if (existing.Any(e => e.FromDate <= to && e.ToDate >= from))
            return _L["ValLeaveDatesOverlap"].Value;
        var attendance = await _context.AttendanceRecords.AsNoTracking()
            .Where(r => r.StaffId == staff.Id && r.AttendanceDate.Year == year).ToListAsync();
        var taken = LeaveBalanceRules.Taken(staff.Id, year, entitlement.LeaveType,
            entitlement.IsAttendanceLeaveType, existing, attendance);
        if (taken + CalendarDays(from, to) > entitlement.AnnualQuota)
            return _L["ValInsufficientLeaveBalance"].Value;
        return null;
    }

    private static decimal CalendarDays(DateTime from, DateTime to) => (to.Date - from.Date).Days + 1;

    private async Task<List<LeaveBalanceRow>> CurrentYearBalancesAsync(
        int year, string? employeeId = null, string? leaveType = null)
    {
        var staffQuery = _context.Staff.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId))
            staffQuery = staffQuery.Where(person => person.EmployeeId == employeeId);
        var staff = await staffQuery
            .OrderBy(person => person.StaffCategory).ThenBy(person => person.FullName).ToListAsync();
        var entitlements = await _context.LeaveEntitlements.AsNoTracking().Where(item => item.Year == year)
            .OrderBy(item => item.StaffId).ThenBy(item => item.LeaveType).ToListAsync();
        if (!string.IsNullOrWhiteSpace(leaveType))
            entitlements = entitlements.Where(item =>
                string.Equals(item.LeaveType, leaveType, StringComparison.OrdinalIgnoreCase)).ToList();
        var entries = await _context.StaffLeaveEntries.AsNoTracking().Where(entry => entry.Year == year).ToListAsync();
        var attendance = await _context.AttendanceRecords.AsNoTracking()
            .Where(record => record.AttendanceDate.Year == year).ToListAsync();
        var balances = new List<LeaveBalanceRow>();

        foreach (var employee in staff)
        {
            foreach (var entitlement in entitlements.Where(item => item.StaffId == employee.Id))
            {
                var taken = LeaveBalanceRules.Taken(employee.Id, year, entitlement.LeaveType,
                    entitlement.IsAttendanceLeaveType, entries, attendance);
                balances.Add(new LeaveBalanceRow(employee.EmployeeId, employee.FullName, employee.StaffCategory,
                    entitlement.LeaveType, entitlement.AnnualQuota, taken, entitlement.AnnualQuota - taken));
            }
        }
        return balances;
    }

    private async Task ReconcileAttendanceLeavePolicyAsync(Guid staffId, int year, LeaveEntitlement? policy)
    {
        var leaveEntries = await _context.StaffLeaveEntries.AsNoTracking()
            .Where(entry => entry.Year == year && entry.StaffId == staffId).ToListAsync();
        var attendance = await _context.AttendanceRecords
            .Where(record => record.AttendanceDate.Year == year && record.StaffId == staffId)
            .OrderBy(record => record.AttendanceDate).ThenBy(record => record.Id).ToListAsync();

        var remaining = policy == null ? 0m : policy.AnnualQuota - leaveEntries
            .Where(entry => entry.LeaveType.ToUpper() == policy.LeaveType.ToUpper())
            .Sum(entry => entry.Days);
        foreach (var record in attendance)
        {
            var days = LeaveBalanceRules.AttendanceDays(record);
            if (days == 0m)
            {
                record.LeaveWithoutPay = false;
                continue;
            }
            if (LeaveBalanceRules.IsCoveredByManualLeave(record, leaveEntries))
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

    private IActionResult LeaveIndexError(string message)
    {
        TempData["LeaveError"] = message;
        return RedirectToAction(nameof(Index));
    }

    private IActionResult LeaveAddError(string message)
    {
        TempData["LeaveError"] = message;
        return RedirectToAction(nameof(Add));
    }

    private static bool TryDates(LeaveEntryForm form, out DateTime from, out DateTime to)
    {
        var parsedFrom = DateTime.TryParseExact(form.FromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out from);
        var parsedTo = DateTime.TryParseExact(form.ToDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out to);
        return parsedFrom && parsedTo;
    }
}
