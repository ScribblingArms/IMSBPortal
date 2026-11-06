using System.Globalization;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace IMSB.Admin.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class LeavesController : Controller
{
    private readonly ILeavesInterface _leaves;
    private readonly IStringLocalizer _L;

    public LeavesController(ILeavesInterface leaves, IStringLocalizerFactory localizerFactory)
    {
        _leaves = leaves;
        _L = localizerFactory.Create("SharedResource", typeof(LeavesController).Assembly.GetName().Name!);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var year = DateTime.Today.Year;
        var staff = await _leaves.StaffAsync();

        ViewData["Title"] = _L["LeaveManagement"].Value;
        return View(new LeaveManagementViewModel
        {
            Year = year,
            Categories = staff.Select(s => s.StaffCategory.Trim()).Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList(),
            Staff = staff,
            Entitlements = await _leaves.EntitlementsWithStaffAsync(year),
            Balances = await _leaves.BalancesAsync(year)
        });
    }

    [HttpGet]
    public async Task<IActionResult> CurrentBalances(string? employeeId, string? leaveType)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        return Ok(new { rows = await _leaves.BalancesAsync(DateTime.Today.Year, employeeId, leaveType) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEntitlement(LeaveEntitlementForm form)
    {
        if (!ModelState.IsValid || form.StaffId == Guid.Empty || string.IsNullOrWhiteSpace(form.LeaveType)
            || form.AnnualQuota % 0.5m != 0m)
            return LeaveIndexError(_L["ValLeaveEntitlement"].Value);

        var result = await _leaves.SaveEntitlementAsync(form.StaffId, form.LeaveType, form.AnnualQuota);
        if (result.ErrorKey != null) return LeaveIndexError(_L[result.ErrorKey].Value);
        TempData["LeaveMessage"] = _L["LeaveEntitlementSaved"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntitlement(Guid id)
    {
        var result = await _leaves.DeleteEntitlementAsync(id);
        if (!result.Found) return NotFound();
        if (result.ErrorKey != null) return LeaveIndexError(_L[result.ErrorKey].Value);
        TempData["LeaveMessage"] = _L["LeaveEntitlementDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Add()
    {
        var year = DateTime.Today.Year;
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ViewData["Title"] = _L["AddLeaveTaken"].Value;
        return View(new LeaveEntryViewModel
        {
            Year = year,
            Form = new LeaveEntryForm { FromDate = today, ToDate = today },
            Staff = await _leaves.ActiveStaffAsync(),
            LeaveTypes = await _leaves.LeaveTypesAsync(),
            Entitlements = await _leaves.EntitlementsAsync(year),
            Entries = await _leaves.EntriesAsync(year)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Record(LeaveEntryForm form)
    {
        if (!ModelState.IsValid) return LeaveAddError(_L["ValLeaveEntry"].Value);
        var result = await _leaves.RecordEntryAsync(form.StaffId, form.LeaveType, ParseDate(form.FromDate), ParseDate(form.ToDate),
            form.Remarks, User.Identity?.Name);
        if (result.ErrorKey != null) return LeaveAddError(_L[result.ErrorKey].Value);
        TempData["LeaveMessage"] = _L["LeaveEntrySaved"].Value;
        return RedirectToAction(nameof(Add));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEntry(Guid id, string fromDate, string toDate, string? remarks)
    {
        var result = await _leaves.UpdateEntryAsync(id, ParseDate(fromDate), ParseDate(toDate), remarks, User.Identity?.Name);
        if (!result.Found) return NotFound();
        if (result.ErrorKey != null) return LeaveAddError(_L[result.ErrorKey].Value);
        TempData["LeaveMessage"] = _L["LeaveEntrySaved"].Value;
        return RedirectToAction(nameof(Add));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(Guid id)
    {
        if (!(await _leaves.DeleteEntryAsync(id)).Found) return NotFound();
        TempData["LeaveMessage"] = _L["LeaveEntryDeleted"].Value;
        return RedirectToAction(nameof(Add));
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

    /// <summary>yyyy-MM-dd, or null when the value is not a date.</summary>
    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
