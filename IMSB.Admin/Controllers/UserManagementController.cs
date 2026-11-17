using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers;

/// <summary>User Management: add, view, update and delete District Officer and State Admin Officer logins.</summary>
[Authorize(Roles = "Admin")]
public class UserManagementController : Controller
{
    private readonly IUserManagementInterface _users;
    private readonly IStringLocalizer _L;

    public UserManagementController(IUserManagementInterface users, IStringLocalizerFactory localizerFactory)
    {
        _users = users;
        _L = localizerFactory.Create("SharedResource", typeof(UserManagementController).Assembly.GetName().Name!);
    }

    public IActionResult Index()
    {
        ViewData["Title"] = _L["UserManagement"].Value;
        return View();
    }

    /// <summary>The users plus the user type and district pickers, with localized labels.</summary>
    [HttpGet]
    public async Task<IActionResult> Data()
    {
        var rows = (await _users.ListAsync()).Select(u => new
        {
            u.Id,
            u.UserName,
            u.Email,
            u.Mobile,
            u.UserType,
            UserTypeLabel = _L[u.UserType].Value,
            u.District,
            DistrictLabel = DistrictLabel(u.District),
            u.IsActive,
            CreatedAt = Utc(u.CreatedAt),
            LastLogin = u.LastLogin == null ? (DateTime?)null : Utc(u.LastLogin.Value)
        });
        return Ok(new
        {
            rows,
            userTypes = UserTypes.All.Select(t => new { value = t, label = _L[t].Value }),
            districts = TelanganaDistricts.All.Select(d => new { value = d, label = DistrictLabel(d) })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] UserFormDto? dto)
    {
        if (dto == null) return ErrorResult("request", _L["CheckForm"].Value);
        var errors = dto.Validate(isNew: true);
        if (errors.Count > 0) return Invalid(errors);
        return FromResult(await _users.AddAsync(dto), "UserSaved");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] UserFormDto? dto)
    {
        if (dto == null) return ErrorResult("request", _L["CheckForm"].Value);
        var errors = dto.Validate(isNew: false);
        if (errors.Count > 0) return Invalid(errors);
        return FromResult(await _users.UpdateAsync(id, dto), "UserUpdated");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id) =>
        FromResult(await _users.DeleteAsync(id), "UserDeleted");

    private string? DistrictLabel(string? district) =>
        DistrictNames.ResourceKey(district) is { } key ? _L[key].Value : district;

    private BadRequestObjectResult Invalid(Dictionary<string, string> errorKeys) =>
        BadRequest(new { errors = errorKeys.ToDictionary(e => e.Key, e => _L[e.Value].Value) });

    private IActionResult FromResult(BackendResult result, string successKey)
    {
        if (!result.Found) return NotFound();
        // refusals: a user name or email address already in use
        if (result.ErrorKey != null) return ErrorResult(result.ErrorKey == "ValEmailExists" ? "email" : "user_name", _L[result.ErrorKey].Value);
        return Ok(new { message = _L[successKey].Value });
    }
}
