using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IMSB.Admin.Tests;

/// <summary>Shared fixtures: an in-memory IMSBContext with reference data, and a StaffController wired like MVC would.</summary>
internal static class TestHelpers
{
    public static readonly Guid SalesDept = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AdminDept = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static IMSBContext CreateContext(bool seedReferenceData = true)
    {
        var options = new DbContextOptionsBuilder<IMSBContext>()
            .UseInMemoryDatabase("staff-tests-" + Guid.NewGuid())
            .Options;
        var ctx = new IMSBContext(options);
        if (seedReferenceData)
        {
            ctx.Departments.AddRange(
                new Department { Id = SalesDept, Name = "Sales" },
                new Department { Id = AdminDept, Name = "Admin" });
            ctx.Shifts.AddRange(
                new Shift { Code = "MORNING", Name = "Morning (8AM-4PM)" },
                new Shift { Code = "EVENING", Name = "Evening (4PM-10PM)" });
            ctx.SaveChanges();
        }
        return ctx;
    }

    /// <summary>The real resource-backed localizer factory, reading IMSB.Admin's SharedResource.*.resx.</summary>
    public static IStringLocalizerFactory LocalizerFactory() =>
        new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }),
            NullLoggerFactory.Instance);

    public static IStringLocalizer SharedLocalizer() =>
        LocalizerFactory().Create("SharedResource", typeof(StaffController).Assembly.GetName().Name!);

    public static StaffController CreateController(IMSBContext ctx, string userName = "admin@imsb.tn.gov.in", string role = "Admin")
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Role, role)
        }, "Test");
        return new StaffController(new IMSB.Backend.StaffDetails(ctx), LocalizerFactory())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    /// <summary>Runs DataAnnotations on the DTO and copies failures into ModelState, as model binding would.</summary>
    public static void BindModelState(Controller controller, object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        foreach (var r in results)
            foreach (var member in r.MemberNames.DefaultIfEmpty(string.Empty))
                controller.ModelState.AddModelError(member, r.ErrorMessage ?? "invalid");
    }

    public static CreateStaffDto ValidCreateDto(string mobile = "9876500001", string? email = "new.staff@example.com") => new()
    {
        FullName = "Meena Kumari",
        Mobile = mobile,
        Email = email,
        DateOfBirth = "1990-05-10",
        Gender = "Female",
        DepartmentId = SalesDept.ToString(),
        Designation = "Sales Executive",
        StaffCategory = "Executive",
        MonthlySalary = 24000m,
        JoinDate = "2020-01-15",
        Shift = "MORNING"
    };

    public static UpdateStaffDto ToUpdate(CreateStaffDto c, string? status = null) => new()
    {
        FullName = c.FullName, Mobile = c.Mobile, Email = c.Email, DateOfBirth = c.DateOfBirth, Gender = c.Gender,
        DepartmentId = c.DepartmentId, Designation = c.Designation, StaffCategory = c.StaffCategory, MonthlySalary = c.MonthlySalary,
        JoinDate = c.JoinDate, Shift = c.Shift, Status = status
    };

    public static Staff ExistingStaff(string employeeId, string fullName, string mobile, string status = "ACTIVE",
        Guid? dept = null, string shift = "MORNING", string? email = null, DateTime? createdAt = null) => new()
    {
        EmployeeId = employeeId,
        FullName = fullName,
        Mobile = mobile,
        Email = email,
        DateOfBirth = new DateTime(1988, 3, 3),
        Gender = "Female",
        DepartmentId = dept ?? SalesDept,
        Designation = "Executive",
        StaffCategory = "Executive",
        JoinDate = new DateTime(2015, 6, 1),
        Shift = shift,
        Status = status,
        CreatedAt = createdAt ?? DateTime.UtcNow
    };

    /// <summary>Errors dictionary from a BadRequest({ errors }) result.</summary>
    public static Dictionary<string, string> Errors(IActionResult result)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var json = JsonSerializer.Serialize(bad.Value);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    /// <summary>Serialises an Ok(...) value the way MVC does (camelCase, JsonSerializerDefaults.Web) so tests read what the browser gets.</summary>
    public static JsonElement OkJson(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement.Clone();
    }

    /// <summary>Temporarily switches the UI culture (for localization assertions).</summary>
    public sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture, _uiCulture;
        public CultureScope(string name)
        {
            _culture = CultureInfo.CurrentCulture; _uiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);
        }
        public void Dispose() { CultureInfo.CurrentCulture = _culture; CultureInfo.CurrentUICulture = _uiCulture; }
    }
}
