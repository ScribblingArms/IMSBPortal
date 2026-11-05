using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class OtAssignmentsController : Controller
{
    private readonly IOtAssignmentsInterface _assignments;
    private readonly IStringLocalizer _L;

    public OtAssignmentsController(IOtAssignmentsInterface assignments, IStringLocalizerFactory localizerFactory)
    {
        _assignments = assignments;
        _L = localizerFactory.Create("SharedResource", typeof(OtAssignmentsController).Assembly.GetName().Name!);
    }

    public IActionResult Index()
    {
        ViewData["Title"] = _L["OTAssignment"].Value;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Data() =>
        Ok(new { rows = await _assignments.ListAsync(), staff = await _assignments.UnassignedStaffAsync() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] OtAssignmentDto? dto)
    {
        if (dto == null) return ErrorResult("request", _L["CheckForm"].Value);

        var errors = TimeErrors(dto);
        if (dto.StaffId is null || dto.StaffId == Guid.Empty) errors["staff_id"] = _L["ValOtSelectStaff"].Value;
        if (errors.Count > 0) return BadRequest(new { errors });

        var (from, to) = dto.Times();
        return FromResult(await _assignments.AddAsync(dto.StaffId!.Value, from, to, User.Identity?.Name), "staff_id", "OTAssignmentSaved");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, [FromBody] OtAssignmentDto? dto)
    {
        if (dto == null) return ErrorResult("request", _L["CheckForm"].Value);

        var errors = TimeErrors(dto);
        if (errors.Count > 0) return BadRequest(new { errors });

        var (from, to) = dto.Times();
        return FromResult(await _assignments.UpdateAsync(id, from, to, User.Identity?.Name), "staff_id", "OTAssignmentSaved");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id) =>
        FromResult(await _assignments.DeleteAsync(id), "request", "OTAssignmentDeleted");

    private Dictionary<string, string> TimeErrors(OtAssignmentDto dto)
    {
        var errors = new Dictionary<string, string>();
        if (dto.ValidateTimes() is { } invalid) errors[invalid.Field] = _L[invalid.Key].Value;
        return errors;
    }

    private IActionResult FromResult(BackendResult result, string errorField, string successKey)
    {
        if (!result.Found) return NotFound();
        if (result.ErrorKey != null) return ErrorResult(errorField, _L[result.ErrorKey].Value);
        return Ok(new { message = _L[successKey].Value });
    }
}
