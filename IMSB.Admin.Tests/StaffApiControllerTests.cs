extern alias ImsbApi;

using System.Security.Claims;
using IMSB.Backend;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ApiStaffController = ImsbApi::IMSB.Api.Controllers.StaffController;

namespace IMSB.Admin.Tests;

/// <summary>
/// The mobile app's staff endpoints (IMSB.Api, api/staff): the same SERP.tbl_Employee data and rules as the Staff page.
/// EmpTypeID is a tbl_EMP_Types row, Desig a tbl_Designation row and ShiftID a tbl_Shifts row of that type.
/// </summary>
public class StaffApiControllerTests
{
    private static ApiStaffController Controller(IMSBContext ctx, string role = "Admin")
    {
        // ApiControllerBase reads its IStringLocalizer from the request services, as IMSB.Api's Program registers it
        var services = new ServiceCollection()
            .AddSingleton<IStringLocalizer>(TestHelpers.SharedLocalizer())
            .BuildServiceProvider();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "api-tester"), new Claim(ClaimTypes.Role, role) }, "Test"));
        return new ApiStaffController(new StaffDetails(ctx), new Leaves(ctx))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user, RequestServices = services } }
        };
    }

    private static IMSBContext Seeded()
    {
        var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.AddRange(
            TestHelpers.ExistingEmployee(501, "Ramesh Kumar", "9876543210", agency: TestHelpers.AgencyA, createdOn: new DateTime(2026, 1, 1)),
            TestHelpers.ExistingEmployee(502, "Padma Rani", "9876543211", dept: TestHelpers.HousekeepingDept, shift: TestHelpers.EveningShift,
                createdOn: new DateTime(2026, 1, 2)));
        ctx.SaveChanges();
        return ctx;
    }

    private static UpdateStaffDto DtoFor(Employee e, string? status = null) => new()
    {
        FullName = e.EmpName!, Mobile = e.Mobile!, Gender = e.Gender!, EmployeeTypeId = e.EmpTypeId?.ToString() ?? "",
        DepartmentId = e.DepartmentId?.ToString(), AgencyId = e.AgencyId?.ToString(), ShiftId = e.ShiftId?.ToString(),
        StallNumber = e.StallNum, Status = status
    };

    private static List<int> Ids(System.Text.Json.JsonElement json) =>
        json.GetProperty("staff").EnumerateArray().Select(s => s.GetProperty("employeeId").GetInt32()).ToList();

    // ── GET api/staff ─────────────────────────────────────────────

    [Fact]
    public async Task List_returns_the_masters_the_list_and_the_totals()
    {
        using var ctx = Seeded();
        var json = TestHelpers.OkJson(await Controller(ctx).List(null, null, null));

        Assert.Equal(new[] { 502, 501 }, Ids(json));
        Assert.Equal(new[] { "Housekeeping", "Office" }, json.GetProperty("employeeTypes").EnumerateArray().Select(t => t.GetProperty("name").GetString()));
        Assert.Equal(3, json.GetProperty("depts").GetArrayLength());     // dbo.departments
        Assert.Equal(2, json.GetProperty("agencies").GetArrayLength());
        Assert.Equal(2, json.GetProperty("shifts").GetArrayLength());    // offered dbo.Shift rows
        var stats = json.GetProperty("stats");
        Assert.Equal(2, stats.GetProperty("total").GetInt32());
        Assert.Equal(2, stats.GetProperty("employeeTypes").GetInt32());
        Assert.Equal(1, stats.GetProperty("agencyStaff").GetInt32());
    }

    [Fact]
    public async Task List_is_the_same_as_the_staff_page_data()
    {
        using var ctx = Seeded();
        var api = TestHelpers.OkJson(await Controller(ctx).List("kumar", null, "ACTIVE", TestHelpers.OfficeType.ToString()));
        var page = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData("kumar", null, "ACTIVE", TestHelpers.OfficeType.ToString()));

        Assert.Equal(page.GetRawText(), api.GetRawText());
    }

    [Theory]
    [InlineData("2", new[] { 501 })]
    [InlineData("3", new[] { 502 })]
    [InlineData("77", new int[0])]
    [InlineData("x", new[] { 502, 501 })]   // not an id: ignored
    public async Task List_filters_by_employee_type(string type, int[] expected)
    {
        using var ctx = Seeded();
        Assert.Equal(expected, Ids(TestHelpers.OkJson(await Controller(ctx).List(null, null, null, type))));
    }

    [Fact]
    public async Task List_filters_by_search_department_and_status()
    {
        using var ctx = Seeded();
        Assert.Equal(new[] { 502 }, Ids(TestHelpers.OkJson(await Controller(ctx).List("padma", null, null))));
        Assert.Equal(new[] { 501 }, Ids(TestHelpers.OkJson(await Controller(ctx).List(null, TestHelpers.SalesDept.ToString(), null))));
        Assert.Empty(Ids(TestHelpers.OkJson(await Controller(ctx).List(null, null, "INACTIVE"))));
    }

    [Fact]
    public async Task List_offers_dbo_departments_and_dbo_shifts()
    {
        using var ctx = Seeded();
        var json = TestHelpers.OkJson(await Controller(ctx).List(null, null, null));

        Assert.Equal(new[] { "Admin", "Cleaner", "Sales" }, json.GetProperty("depts").EnumerateArray().Select(d => d.GetProperty("name").GetString()));
        Assert.Equal(new[] { TestHelpers.MorningShift.ToString(), TestHelpers.EveningShift.ToString() },
            json.GetProperty("shifts").EnumerateArray().Select(s => s.GetProperty("id").GetString()));   // the retired shift is not offered
    }

    // ── GET api/staff/export ──────────────────────────────────────

    [Fact]
    public async Task Export_returns_the_filtered_rows()
    {
        using var ctx = Seeded();
        var file = Assert.IsType<FileContentResult>(await Controller(ctx).Export(null, null, null, TestHelpers.OfficeType.ToString()));

        Assert.Equal("application/vnd.ms-excel", file.ContentType);
        var lines = ExcelExportTestHelpers.CsvText(file).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("EmployeeId,FullName,EmployeeType,Department,Designation,Agency,Mobile,Gender,Shift,StallNumber,JoinDate,Status", lines[0]);
        Assert.Equal("501,Ramesh Kumar,Office,Sales,Sales,Agency A,9876543210,Female,Morning (8AM-4PM),,,ACTIVE", lines[1]);
        Assert.Equal(2, lines.Length);
    }

    // ── GET api/staff/{id} ────────────────────────────────────────

    [Fact]
    public async Task Get_returns_one_employee_with_names_and_a_masked_aadhaar()
    {
        using var ctx = Seeded();
        ctx.Employees.Single(e => e.EmpId == 502).Aadhar = "234123412346";
        await ctx.SaveChangesAsync();

        var json = TestHelpers.OkJson(await Controller(ctx).Get(502));

        Assert.Equal("Padma Rani", json.GetProperty("fullName").GetString());
        Assert.Equal("Housekeeping", json.GetProperty("employeeTypeName").GetString());
        Assert.Equal("Cleaner", json.GetProperty("departmentName").GetString());
        Assert.Equal("Evening (4PM-10PM)", json.GetProperty("shiftName").GetString());
        Assert.Equal("XXXX-XXXX-2346", json.GetProperty("aadhaarMasked").GetString());
        Assert.DoesNotContain("234123412346", json.GetRawText());
    }

    [Fact]
    public async Task Get_unknown_employee_is_404()
    {
        using var ctx = Seeded();
        Assert.IsType<NotFoundResult>(await Controller(ctx).Get(999));
    }

    // ── POST api/staff ────────────────────────────────────────────

    [Fact]
    public async Task Create_saves_the_type_department_and_shift()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.EmployeeTypeId = TestHelpers.HousekeepingType.ToString();
        dto.DepartmentId = TestHelpers.HousekeepingDept.ToString();
        dto.ShiftId = TestHelpers.EveningShift.ToString();

        var json = TestHelpers.OkJson(await Controller(ctx).Create(dto));

        var saved = await ctx.Employees.SingleAsync();
        Assert.Equal(json.GetProperty("employeeId").GetInt32(), saved.EmpId);
        Assert.Equal(TestHelpers.HousekeepingType, saved.EmpTypeId);    // tbl_EMP_Types
        Assert.Equal(TestHelpers.HousekeepingDept, saved.DepartmentId);      // dbo.departments.Id
        Assert.Equal(TestHelpers.EveningShift, saved.ShiftId);          // dbo.Shift.Id
        Assert.Equal(1, saved.IsActive);
        Assert.Equal(TestHelpers.ValidAadhaar(dto.Mobile), saved.Aadhar);
    }

    [Fact]
    public async Task Create_refuses_a_category_or_shift_that_is_not_a_dbo_row()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DepartmentId = "9200";                                // not in dbo.departments
        dto.ShiftId = TestHelpers.HousekeepingShift.ToString();   // a SERP tbl_Shifts id, not in dbo.Shift

        var errors = TestHelpers.Errors(await Controller(ctx).Create(dto));

        Assert.Equal("Staff category is required", errors["department_id"]);
        Assert.Equal("Invalid shift", errors["shift"]);
        Assert.Empty(ctx.Employees);
    }

    [Fact]
    public async Task Create_refuses_a_retired_shift()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.ShiftId = TestHelpers.RetiredShift.ToString();

        Assert.Equal("Invalid shift", TestHelpers.Errors(await Controller(ctx).Create(dto))["shift"]);
    }

    [Fact]
    public async Task Create_refuses_an_unknown_employee_type()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.EmployeeTypeId = "77";

        Assert.Equal("Select a valid employee type", TestHelpers.Errors(await Controller(ctx).Create(dto))["employee_type_id"]);
    }

    [Fact]
    public async Task Create_messages_follow_the_request_culture()
    {
        using var scope = new TestHelpers.CultureScope("hi-IN");
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.EmployeeTypeId = "77";

        Assert.Equal("मान्य कर्मचारी प्रकार चुनें", TestHelpers.Errors(await Controller(ctx).Create(dto))["employee_type_id"]);
    }

    // ── PUT api/staff/{id} ────────────────────────────────────────

    [Fact]
    public async Task Update_changes_the_type_category_and_shift()
    {
        using var ctx = Seeded();
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == 501), EmployeeStatuses.Inactive);
        dto.EmployeeTypeId = TestHelpers.HousekeepingType.ToString();
        dto.DepartmentId = TestHelpers.HousekeepingDept.ToString();
        dto.ShiftId = TestHelpers.EveningShift.ToString();
        dto.Remarks = "Moved";

        Assert.IsType<OkObjectResult>(await Controller(ctx).Update(501, dto));

        var saved = await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == 501);
        Assert.Equal(TestHelpers.HousekeepingType, saved.EmpTypeId);
        Assert.Equal(TestHelpers.HousekeepingDept, saved.DepartmentId);
        Assert.Equal(TestHelpers.EveningShift, saved.ShiftId);
        Assert.Equal(0, saved.IsActive);
        Assert.Equal("Moved", saved.Remarks);
        Assert.NotNull(saved.ModifiedOn);
    }

    [Fact]
    public async Task Update_keeps_an_unchanged_category_and_shift_that_are_not_offered()
    {
        using var ctx = Seeded();
        var legacy = ctx.Employees.Single(e => e.EmpId == 501);
        legacy.DepartmentId = 9200;                    // not a dbo.departments id
        legacy.ShiftId = TestHelpers.RetiredShift;     // retired dbo.Shift
        await ctx.SaveChangesAsync();
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == 501));

        Assert.IsType<OkObjectResult>(await Controller(ctx).Update(501, dto));

        dto.ShiftId = TestHelpers.HousekeepingShift.ToString();
        Assert.Equal("Invalid shift", TestHelpers.Errors(await Controller(ctx).Update(501, dto))["shift"]);
    }

    [Fact]
    public async Task Update_unknown_employee_is_404()
    {
        using var ctx = Seeded();
        Assert.IsType<NotFoundResult>(await Controller(ctx).Update(999, TestHelpers.ToUpdate(TestHelpers.ValidCreateDto())));
    }

    // ── DELETE api/staff/{id} ─────────────────────────────────────

    [Fact]
    public async Task Delete_removes_the_employee_and_then_answers_404()
    {
        using var ctx = Seeded();

        var json = TestHelpers.OkJson(await Controller(ctx).Delete(501));

        Assert.Equal(501, json.GetProperty("employeeId").GetInt32());
        Assert.Equal(new[] { 502 }, ctx.Employees.Select(e => e.EmpId));
        Assert.IsType<NotFoundResult>(await Controller(ctx).Delete(501));
    }

    // ── routes and roles ──────────────────────────────────────────

    [Theory]
    [InlineData(nameof(ApiStaffController.List), "GET", null)]
    [InlineData(nameof(ApiStaffController.Export), "GET", "export")]
    [InlineData(nameof(ApiStaffController.Get), "GET", "{id:int}")]
    [InlineData(nameof(ApiStaffController.Create), "POST", null)]
    [InlineData(nameof(ApiStaffController.Update), "PUT", "{id:int}")]
    [InlineData(nameof(ApiStaffController.Delete), "DELETE", "{id:int}")]
    public void Every_endpoint_has_its_verb_and_route(string action, string verb, string? template)
    {
        var method = typeof(ApiStaffController).GetMethod(action)!;
        var attr = method.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single();
        Assert.Equal(new[] { verb }, attr.HttpMethods);
        Assert.Equal(template, attr.Template);
    }

    [Fact]
    public void Staff_endpoints_are_for_staff_and_admin_roles()
    {
        var authorize = typeof(ApiStaffController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single();
        Assert.Equal("Staff,Admin", authorize.Roles);
    }
}
