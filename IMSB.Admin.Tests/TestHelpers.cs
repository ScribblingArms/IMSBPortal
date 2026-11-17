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
    public const int SalesDept = 9001;
    public const int AdminDept = 9002;

    // Masters of the Staff page: employee types (SERP.tbl_EMP_Types), staff categories (dbo.departments: Sales, Admin, Cleaner),
    // shifts (dbo.Shift: Morning, Evening, and a retired Night shift no longer offered), agencies (SERP.tbl_ArtisansTypeAgency)
    public const int OfficeType = 2, HousekeepingType = 3;
    public const int HousekeepingDept = 9003;
    public const int MorningShift = 11, EveningShift = 12, RetiredShift = 13;
    /// <summary>A SERP.tbl_Shifts id (not a dbo.Shift row), as on employees registered by the SERP app.</summary>
    public const int HousekeepingShift = 113;
    public const int AgencyA = 1, AgencyB = 2;

    /// <summary>The dbo rows the Staff form adds to the base seed: the Cleaner category and the retired Night shift.</summary>
    public static void SeedStaffMasters(IMSBContext ctx)
    {
        ctx.Departments.Add(new Department { Id = HousekeepingDept, Name = "Cleaner" });
        ctx.Shifts.Add(new Shift { Id = RetiredShift, Code = "NIGHT", Name = "10:00 PM to 06:00 AM", DisplayOrder = 0 });
    }

    public static IMSBContext CreateContext(bool seedReferenceData = true)
    {
        var options = new DbContextOptionsBuilder<IMSBContext>()
            .UseInMemoryDatabase("staff-tests-" + Guid.NewGuid())
            .Options;
        // keeps the Staff set (a SQL view in production) in step with tbl_Employee
        var ctx = new StaffViewTestContext(options);
        if (seedReferenceData)
        {
            ctx.Departments.AddRange(
                new Department { Id = SalesDept, Name = "Sales" },
                new Department { Id = AdminDept, Name = "Admin" });
            ctx.Shifts.AddRange(
                new Shift { Id = MorningShift, Code = "MORNING", Name = "Morning (8AM-4PM)", DisplayOrder = 1 },
                new Shift { Id = EveningShift, Code = "EVENING", Name = "Evening (4PM-10PM)", DisplayOrder = 2 });
            SeedSerpMasters(ctx);
            ctx.SaveChanges();
        }
        return ctx;
    }

    /// <summary>
    /// <see cref="CreateContext"/> plus the Staff form's extra dbo rows (the Cleaner category and the retired Night shift).
    /// Kept out of the base seed, which the attendance and payroll tests count on.
    /// </summary>
    public static IMSBContext CreateStaffContext()
    {
        var ctx = CreateContext();
        SeedStaffMasters(ctx);
        ctx.SaveChanges();
        return ctx;
    }

    /// <summary>The SERP master rows the Staff page reads (not saved; the caller saves).</summary>
    public static void SeedSerpMasters(IMSBContext ctx)
    {
        ctx.EmployeeTypeMasters.AddRange(
            new EmployeeTypeMaster { EmpTypeId = OfficeType, Name = "Office" },
            new EmployeeTypeMaster { EmpTypeId = HousekeepingType, Name = "Housekeeping" });
        ctx.EmployeeDesignations.AddRange(
            new EmployeeDesignation { DesigId = SalesDept, Name = "Sales", EmpTypeId = OfficeType },
            new EmployeeDesignation { DesigId = AdminDept, Name = "Admin", EmpTypeId = OfficeType },
            new EmployeeDesignation { DesigId = HousekeepingDept, Name = "Cleaner", EmpTypeId = HousekeepingType });
        ctx.EmployeeShifts.AddRange(
            new EmployeeShift { ShiftId = MorningShift, ShiftTime = "Morning (8AM-4PM)", EmpTypeId = OfficeType, InTime = new TimeSpan(8, 0, 0) },
            new EmployeeShift { ShiftId = EveningShift, ShiftTime = "Evening (4PM-10PM)", EmpTypeId = OfficeType, InTime = new TimeSpan(16, 0, 0) },
            new EmployeeShift { ShiftId = HousekeepingShift, ShiftTime = "06:00 AM to 02:00 PM", EmpTypeId = HousekeepingType, InTime = new TimeSpan(6, 0, 0) });
        ctx.ArtisansTypeAgencies.AddRange(
            new ArtisansTypeAgency { AgencyId = AgencyA, Name = "Agency A" },
            new ArtisansTypeAgency { AgencyId = AgencyB, Name = "Agency B" });
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
        return new StaffController(new IMSB.Backend.StaffDetails(ctx), new IMSB.Backend.Leaves(ctx), LocalizerFactory())
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

    /// <summary>A 12-digit Aadhaar number derived from the seed, so different seeds give different numbers.</summary>
    public static string ValidAadhaar(string seed) =>
        "23" + new string(seed.Where(char.IsDigit).ToArray()).PadLeft(10, '0')[^10..];

    /// <summary>A valid Staff form: a Sales (Office) employee from Agency A on the morning shift.</summary>
    public static CreateStaffDto ValidCreateDto(string mobile = "9876500001") => new()
    {
        FullName = "Meena Kumari",
        Mobile = mobile,
        Aadhaar = ValidAadhaar(mobile),
        Gender = "Female",
        EmployeeTypeId = OfficeType.ToString(),
        DepartmentId = SalesDept.ToString(),
        Designation = "Sales Executive",
        AgencyId = AgencyA.ToString(),
        ShiftId = MorningShift.ToString(),
        StallNumber = null,
        JoinDate = "2020-01-15"
    };

    /// <summary>The same form as an edit; the Aadhaar is left blank, which keeps the number on record.</summary>
    public static UpdateStaffDto ToUpdate(CreateStaffDto c, string? status = null, string? remarks = null) => new()
    {
        FullName = c.FullName, Mobile = c.Mobile, Gender = c.Gender, EmployeeTypeId = c.EmployeeTypeId, DepartmentId = c.DepartmentId,
        Designation = c.Designation, AgencyId = c.AgencyId,
        ShiftId = c.ShiftId, StallNumber = c.StallNumber, JoinDate = c.JoinDate, MonthlySalary = c.MonthlySalary,
        Status = status, Remarks = remarks
    };

    /// <summary>The tbl_Designation row ExistingEmployee gives everyone unless told otherwise ("Sales", seeded by SeedSerpMasters).</summary>
    public const int SalesDesignation = SalesDept;

    /// <summary>
    /// A SERP.tbl_Employee row (EMP_ID is given so tests can address it): staff category dept (dbo.departments, Sales by default),
    /// designation desig (tbl_Designation, "Sales" by default). The type follows the category unless given.
    /// </summary>
    public static Employee ExistingEmployee(int id, string name, string mobile, bool active = true, int? dept = null, int? shift = null,
        int? agency = null, string? aadhaar = null, DateTime? createdOn = null, string gender = "Female", int? type = null,
        int? desig = SalesDesignation) => new()
    {
        EmpId = id,
        EmpName = name,
        Mobile = mobile,
        Aadhar = aadhaar,
        Gender = gender,
        DepartmentId = dept ?? SalesDept,
        DesigId = desig,
        EmpTypeId = type ?? (dept == HousekeepingDept ? HousekeepingType : OfficeType),
        ShiftId = shift ?? MorningShift,
        AgencyId = agency,
        IsActive = active ? 1 : 0,
        CreateOn = createdOn ?? DateTime.Now
    };

    /// <summary>
    /// Saves people described in the Staff shape as what they now are: SERP.tbl_Employee rows, each with a Staff Category in
    /// dbo.departments, a shift in dbo.Shift and their own tbl_Designation row holding the designation and monthly salary.
    /// The EMP_ID is the spec's Id, or the number in its EmployeeId ("1" → 1); the spec's Id and EmployeeId are then set to it,
    /// so a test can refer to the person as before. The Staff rows the pages read appear on SaveChanges (StaffViewTestContext).
    /// "ON_LEAVE" is saved as active: tbl_Employee only knows active or not.
    /// </summary>
    public static void AddEmployees(this IMSBContext ctx, params Staff[] people)
    {
        foreach (var spec in people)
        {
            if (spec.Id == 0 && int.TryParse(spec.EmployeeId, out var fromCode)) spec.Id = fromCode;

            if (!ctx.Departments.Local.Any(d => d.Id == spec.DepartmentId) && !ctx.Departments.AsNoTracking().Any(d => d.Id == spec.DepartmentId))
                ctx.Departments.Add(new Department { Id = spec.DepartmentId, Name = $"Department {spec.DepartmentId}" });

            var code = spec.Shift.Trim().ToUpperInvariant();
            var shift = ctx.Shifts.Local.FirstOrDefault(s => s.Code.ToUpper() == code)
                ?? ctx.Shifts.FirstOrDefault(s => s.Code.ToUpper() == code);
            if (shift == null && code.Length > 0)
            {
                shift = new Shift { Code = code, Name = spec.Shift, DisplayOrder = 0 };
                ctx.Shifts.Add(shift);
            }

            var employee = new Employee
            {
                EmpId = spec.Id,
                EmpName = spec.FullName,
                Mobile = spec.Mobile,
                Gender = spec.Gender,
                EmpTypeId = OfficeType,
                DepartmentId = spec.DepartmentId,
                ShiftId = shift?.Id,
                JoinDate = spec.JoinDate,
                CreateOn = spec.CreatedAt,
                IsActive = string.Equals(spec.Status?.Trim(), "INACTIVE", StringComparison.OrdinalIgnoreCase) ? 0 : 1
            };
            ctx.Employees.Add(employee);   // the in-memory store numbers it here when no EMP_ID was given

            var nextDesig = Math.Max(
                ctx.EmployeeDesignations.Local.Select(d => d.DesigId).DefaultIfEmpty(0).Max(),
                ctx.EmployeeDesignations.AsNoTracking().Select(d => (int?)d.DesigId).Max() ?? 0) + 1;
            ctx.EmployeeDesignations.Add(new EmployeeDesignation
            {
                DesigId = nextDesig, Name = spec.Designation, BaseSalary = spec.MonthlySalary, EmpTypeId = OfficeType, EmpId = employee.EmpId
            });
            employee.DesigId = nextDesig;

            spec.Id = employee.EmpId;
            spec.EmployeeId = employee.EmpId.ToString();
        }
    }

    public static Staff ExistingStaff(string employeeId, string fullName, string mobile, string status = "ACTIVE",
        int? dept = null, string shift = "MORNING", string? email = null, DateTime? createdAt = null) => new()
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
