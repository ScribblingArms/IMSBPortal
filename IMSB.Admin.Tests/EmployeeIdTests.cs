extern alias ImsbApi;

using System.Security.Claims;
using IMSB.Backend;
using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ApiStaffController = ImsbApi::IMSB.Api.Controllers.StaffController;

namespace IMSB.Admin.Tests;

/// <summary>
/// Employee codes, SERP.tbl_Employee.EmployeeId ("EMP001", "EMP002", ...): given once when an employee is registered,
/// kept when they are edited, never handed out again after a delete, and shown everywhere the employee id is shown.
/// (On SQL Server the numbers come from the SERP.EmployeeIdSeq sequence; the in-memory database here counts on from the
/// highest code on record.)
/// </summary>
public class EmployeeIdFormatTests
{
    [Theory]
    [InlineData(1, "EMP001")]
    [InlineData(42, "EMP042")]
    [InlineData(999, "EMP999")]
    [InlineData(1000, "EMP1000")]
    [InlineData(12345, "EMP12345")]
    public void Codes_are_EMP_and_at_least_three_digits(long number, string code) =>
        Assert.Equal(code, EmployeeIds.Format(number));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Numbers_start_at_one(long number) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EmployeeIds.Format(number));

    [Theory]
    [InlineData("EMP001", 1L)]
    [InlineData("EMP012", 12L)]
    [InlineData("emp7", 7L)]
    [InlineData(" EMP1000 ", 1000L)]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("EMP", null)]
    [InlineData("EMP000", null)]
    [InlineData("EMP-1", null)]
    [InlineData("EMPX1", null)]
    [InlineData("1012", null)]
    [InlineData("STF001", null)]
    public void Only_EMP_codes_are_read_as_numbers(string? code, long? number) =>
        Assert.Equal(number, EmployeeIds.Number(code));

    [Fact]
    public void Next_follows_the_highest_code_and_ignores_anything_else()
    {
        Assert.Equal("EMP001", EmployeeIds.Next(Array.Empty<string?>()));
        Assert.Equal("EMP001", EmployeeIds.Next(new string?[] { null, "", "1012" }));
        Assert.Equal("EMP010", EmployeeIds.Next(new string?[] { "EMP001", "EMP009", "EMP003", null, "junk" }));
        Assert.Equal("EMP1000", EmployeeIds.Next(new string?[] { "EMP999" }));
    }

    [Fact]
    public void Finder_takes_the_code_in_any_case_or_the_emp_id()
    {
        var meena = new Staff { Id = 501, EmployeeId = "EMP001", FullName = "Meena" };
        var legacy = new Staff { Id = 502, EmployeeId = "502", FullName = "No code yet" };   // the view shows the EMP_ID
        var find = EmployeeIds.Finder(new[] { meena, legacy });

        Assert.Same(meena, find("EMP001"));
        Assert.Same(meena, find(" emp001 "));
        Assert.Same(meena, find("501"));
        Assert.Same(legacy, find("502"));
        Assert.Null(find("EMP999"));
        Assert.Null(find("999"));
        Assert.Null(find(""));
        Assert.Null(find(null));
    }
}

/// <summary>The Staff page (IMSB.Admin StaffController) gives, keeps and reports the employee code.</summary>
public class EmployeeIdStaffPageTests
{
    private static async Task<IMSBContext> ContextWith(params Employee[] employees)
    {
        var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.AddRange(employees);
        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static UpdateStaffDto DtoFor(Employee e) => new()
    {
        FullName = e.EmpName!, Mobile = e.Mobile!, Gender = e.Gender!, EmployeeTypeId = e.EmpTypeId?.ToString() ?? "",
        DepartmentId = e.DepartmentId?.ToString(), AgencyId = e.AgencyId?.ToString(), ShiftId = e.ShiftId?.ToString(), StallNumber = e.StallNum
    };

    private static Employee WithCode(Employee e, string code)
    {
        e.EmployeeId = code;
        return e;
    }

    [Fact]
    public async Task Registering_gives_EMP001_then_EMP002()
    {
        using var ctx = TestHelpers.CreateStaffContext();

        var first = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto("9876500001")));
        var second = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto("9876500002")));

        Assert.Equal("EMP001", first.GetProperty("employeeCode").GetString());
        Assert.Equal("EMP002", second.GetProperty("employeeCode").GetString());
        var saved = await ctx.Employees.AsNoTracking().OrderBy(e => e.EmpId).Select(e => new { e.EmpId, e.EmployeeId }).ToListAsync();
        Assert.Equal(new[] { "EMP001", "EMP002" }, saved.Select(e => e.EmployeeId));
        // the EMP_ID stays the record's number (URLs), and is what employeeId still reports
        Assert.Equal(saved[0].EmpId, first.GetProperty("employeeId").GetInt32());
    }

    [Fact]
    public async Task Registering_continues_after_the_highest_code_on_record()
    {
        using var ctx = await ContextWith(
            WithCode(TestHelpers.ExistingEmployee(501, "First", "9876500001"), "EMP007"),
            WithCode(TestHelpers.ExistingEmployee(502, "Second", "9876500002"), "EMP003"));

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto("9876500003")));

        Assert.Equal("EMP008", json.GetProperty("employeeCode").GetString());
    }

    [Fact]
    public async Task The_other_pages_see_the_code_as_the_employee_id()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto()));

        // dbo.vw_StaffEmployees: attendance, leaves, OT, payroll, search and the dashboard read Staff.EmployeeId
        var viewed = Assert.Single(ctx.Staff.AsNoTracking());
        Assert.Equal(json.GetProperty("employeeId").GetInt32(), viewed.Id);
        Assert.Equal("EMP001", viewed.EmployeeId);
    }

    [Fact]
    public async Task Editing_keeps_the_code()
    {
        using var ctx = await ContextWith(WithCode(TestHelpers.ExistingEmployee(501, "Old Name", "9876500001"), "EMP004"));
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.FullName = "New Name";

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Update(501, dto));

        var saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Equal(("New Name", "EMP004"), (saved.EmpName, saved.EmployeeId));
        Assert.Equal("EMP004", json.GetProperty("employeeCode").GetString());
        Assert.Equal("EMP004", (await ctx.Staff.AsNoTracking().SingleAsync()).EmployeeId);
    }

    [Fact]
    public async Task Editing_an_employee_saved_before_codes_existed_gives_them_the_next_code()
    {
        using var ctx = await ContextWith(
            WithCode(TestHelpers.ExistingEmployee(501, "Has Code", "9876500001"), "EMP002"),
            TestHelpers.ExistingEmployee(502, "No Code", "9876500002"));
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == 502));

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Update(502, dto));

        Assert.Equal("EMP003", json.GetProperty("employeeCode").GetString());
        Assert.Equal("EMP003", (await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == 502)).EmployeeId);
        Assert.Equal("EMP002", (await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == 501)).EmployeeId);   // untouched
    }

    [Fact]
    public async Task A_failed_edit_does_not_give_a_code()
    {
        using var ctx = await ContextWith(TestHelpers.ExistingEmployee(501, "No Code", "9876500001"));
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.Aadhaar = "12";   // refused by StaffValidation

        Assert.True(TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto)).ContainsKey("aadhaar"));

        Assert.Null((await ctx.Employees.AsNoTracking().SingleAsync()).EmployeeId);
    }

    [Fact]
    public async Task Deleting_reports_the_code_and_leaves_the_others_alone()
    {
        using var ctx = await ContextWith(
            WithCode(TestHelpers.ExistingEmployee(501, "First", "9876500001"), "EMP001"),
            WithCode(TestHelpers.ExistingEmployee(502, "Second", "9876500002"), "EMP002"),
            WithCode(TestHelpers.ExistingEmployee(503, "Third", "9876500003"), "EMP003"));

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Delete(502));

        Assert.Equal((502, "EMP002"), (json.GetProperty("employeeId").GetInt32(), json.GetProperty("employeeCode").GetString()));
        Assert.Equal(new[] { "EMP001", "EMP003" },
            await ctx.Employees.AsNoTracking().OrderBy(e => e.EmpId).Select(e => e.EmployeeId).ToListAsync());
        Assert.DoesNotContain(ctx.Staff.AsNoTracking(), s => s.EmployeeId == "EMP002");
    }

    [Fact]
    public async Task Registering_after_a_delete_does_not_repeat_a_code_still_in_use()
    {
        using var ctx = await ContextWith(
            WithCode(TestHelpers.ExistingEmployee(501, "First", "9876500001"), "EMP001"),
            WithCode(TestHelpers.ExistingEmployee(502, "Second", "9876500002"), "EMP002"),
            WithCode(TestHelpers.ExistingEmployee(503, "Third", "9876500003"), "EMP003"));
        await TestHelpers.CreateController(ctx).Delete(502);

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto("9876500004")));

        var code = json.GetProperty("employeeCode").GetString();
        Assert.Equal("EMP004", code);   // after the highest, not the gap left by EMP002
        Assert.Equal(3, await ctx.Employees.Select(e => e.EmployeeId).Distinct().CountAsync());
    }

    [Fact]
    public async Task A_code_set_by_the_caller_is_ignored()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var employee = new Employee { EmployeeId = "EMP999", IsActive = 1, CreateOn = DateTime.Now };
        TestHelpers.ValidCreateDto().ApplyFormTo(employee);

        await new StaffDetails(ctx).AddAsync(employee);

        Assert.Equal("EMP001", (await ctx.Employees.AsNoTracking().SingleAsync()).EmployeeId);
    }

    [Theory]
    [InlineData("EMP002")]
    [InlineData("emp002")]
    [InlineData("002")]
    public async Task The_staff_search_finds_an_employee_by_code(string q)
    {
        using var ctx = await ContextWith(
            WithCode(TestHelpers.ExistingEmployee(501, "First", "9876500001"), "EMP001"),
            WithCode(TestHelpers.ExistingEmployee(502, "Second", "9876500002"), "EMP002"));

        var found = await new StaffDetails(ctx).FilterAsync(q, null, null);

        Assert.Equal(502, Assert.Single(found).EmpId);
    }

    [Fact]
    public async Task The_staff_list_and_export_show_the_code()
    {
        using var ctx = await ContextWith(WithCode(TestHelpers.ExistingEmployee(501, "First", "9876500001"), "EMP005"));
        var staff = new StaffDetails(ctx);

        var row = (await staff.FilterAsync(null, null, null)).Single().ToListItem(await staff.LookupsAsync());
        Assert.Equal((501, "EMP005"), (row.EmployeeId, row.EmployeeCode));

        var export = await StaffValidation.ExportRowsAsync(staff, null, null, null, null);
        Assert.Equal("EMP005", export[1][Array.IndexOf(StaffValidation.ExportHeader, "EmployeeId")]);
    }

    [Fact]
    public async Task An_employee_without_a_code_is_still_shown_under_the_EMP_ID()
    {
        using var ctx = await ContextWith(TestHelpers.ExistingEmployee(501, "Legacy", "9876500001"));
        var staff = new StaffDetails(ctx);

        var row = (await staff.FilterAsync(null, null, null)).Single().ToListItem(await staff.LookupsAsync());

        Assert.Null(row.EmployeeCode);
        Assert.Equal("501", (await StaffValidation.ExportRowsAsync(staff, null, null, null, null))[1][0]);
        Assert.Equal("501", (await ctx.Staff.AsNoTracking().SingleAsync()).EmployeeId);
    }
}

/// <summary>The mobile app's staff endpoints (IMSB.Api, api/staff) give, keep and report the same code.</summary>
public class EmployeeIdApiTests
{
    private static ApiStaffController Controller(IMSBContext ctx)
    {
        var services = new ServiceCollection().AddSingleton<IStringLocalizer>(TestHelpers.SharedLocalizer()).BuildServiceProvider();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "api-tester"), new Claim(ClaimTypes.Role, "Admin") }, "Test"));
        return new ApiStaffController(new StaffDetails(ctx), new Leaves(ctx))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user, RequestServices = services } }
        };
    }

    [Fact]
    public async Task Create_update_get_and_delete_report_the_code()
    {
        using var ctx = TestHelpers.CreateStaffContext();

        var created = TestHelpers.OkJson(await Controller(ctx).Create(TestHelpers.ValidCreateDto("9876500001")));
        var id = created.GetProperty("employeeId").GetInt32();
        Assert.Equal("EMP001", created.GetProperty("employeeCode").GetString());

        var dto = TestHelpers.ToUpdate(TestHelpers.ValidCreateDto("9876500001"));
        dto.FullName = "Renamed";
        dto.Aadhaar = null;   // keep the one on record
        var updated = TestHelpers.OkJson(await Controller(ctx).Update(id, dto));
        Assert.Equal("EMP001", updated.GetProperty("employeeCode").GetString());

        var got = Assert.IsType<OkObjectResult>(await Controller(ctx).Get(id)).Value as StaffListItem;
        Assert.Equal(("Renamed", "EMP001"), (got!.FullName, got.EmployeeCode));

        var deleted = TestHelpers.OkJson(await Controller(ctx).Delete(id));
        Assert.Equal((id, "EMP001"), (deleted.GetProperty("employeeId").GetInt32(), deleted.GetProperty("employeeCode").GetString()));
        Assert.Empty(ctx.Employees);
    }

    [Fact]
    public async Task The_list_includes_the_code()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        await Controller(ctx).Create(TestHelpers.ValidCreateDto("9876500001"));
        await Controller(ctx).Create(TestHelpers.ValidCreateDto("9876500002"));

        var json = TestHelpers.OkJson(await Controller(ctx).List(null, null, null));

        Assert.Equal(new[] { "EMP001", "EMP002" },
            json.GetProperty("staff").EnumerateArray().Select(s => s.GetProperty("employeeCode").GetString()).OrderBy(c => c));
    }
}
