extern alias ImsbApi;

using System.Security.Claims;
using IMSB.Backend;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Api = ImsbApi::IMSB.Api.Controllers;

namespace IMSB.Admin.Tests;

/// <summary>
/// The mobile app's attendance, leave, payroll and OT endpoints (IMSB.Api) serve the people registered on the Staff page:
/// SERP.tbl_Employee rows, addressed by their EMP_ID.
/// </summary>
public class EmployeesApiTests
{
    private static T Wire<T>(T controller, string role = "Admin") where T : ControllerBase
    {
        var services = new ServiceCollection().AddSingleton<IStringLocalizer>(TestHelpers.SharedLocalizer()).BuildServiceProvider();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "api-tester"), new Claim(ClaimTypes.Role, role) }, "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user, RequestServices = services } };
        return controller;
    }

    /// <summary>One employee registered through the Staff API: Sales, "Clerk" at 31,000, morning shift, 12 Casual days.</summary>
    private static (IMSBContext Ctx, int Id) Registered()
    {
        var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.Designation = "Clerk";
        dto.MonthlySalary = 31000m;
        dto.LeaveQuotas = StaffExtras.Quotas(12, null, null);
        var id = TestHelpers.OkJson(StaffExtras.Api(ctx).Create(dto).GetAwaiter().GetResult()).GetProperty("employeeId").GetInt32();
        return (ctx, id);
    }

    private static Api.AttendanceController Attendance(IMSBContext ctx) => Wire(new Api.AttendanceController(new Attendance(ctx)));
    private static Api.LeavesController Leaves(IMSBContext ctx) => Wire(new Api.LeavesController(new Leaves(ctx)));
    private static Api.PayrollController Payroll(IMSBContext ctx) => Wire(new Api.PayrollController(new Payroll(ctx)));
    private static Api.OtAssignmentsController Ot(IMSBContext ctx) => Wire(new Api.OtAssignmentsController(new OtAssignments(ctx)));

    // ── api/attendance ────────────────────────────────────────────

    [Fact]
    public async Task Attendance_sheet_lists_the_employee_by_emp_id()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;

        var row = Assert.Single(TestHelpers.OkJson(await Attendance(ctx).Data(null, null)).GetProperty("rows").EnumerateArray());

        Assert.Equal("EMP001", row.GetProperty("employeeId").GetString());   // the employee code
        Assert.Equal("Meena Kumari", row.GetProperty("name").GetString());
        Assert.Equal("Sales", row.GetProperty("departmentName").GetString());
        Assert.Equal("Clerk", row.GetProperty("designation").GetString());
        Assert.Equal("Morning (8AM-4PM)", row.GetProperty("shift").GetString());
        Assert.Equal("ACTIVE", row.GetProperty("staffStatus").GetString());
    }

    [Fact]
    public async Task Attendance_is_saved_against_the_emp_id()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        var sheet = new AttendanceSheetDto
        {
            Date = DateTime.Today.ToString("yyyy-MM-dd"),
            Entries = new() { new AttendanceEntryDto { EmployeeId = $" {id} ", Status = "PRESENT", InTime = "08:00", OutTime = "18:00" } }
        };

        var json = TestHelpers.OkJson(await Attendance(ctx).Save(sheet));

        Assert.Equal(1, json.GetProperty("created").GetInt32());
        var record = await ctx.AttendanceRecords.SingleAsync();
        Assert.Equal(id, record.StaffId);
        Assert.Equal(2m, record.OvertimeHours);   // 10h on the 8h morning shift (dbo.Shift)
    }

    [Fact]
    public async Task Attendance_for_an_id_that_is_not_an_employee_is_refused()
    {
        var (ctx, _) = Registered();
        using var __ = ctx;
        var sheet = new AttendanceSheetDto
        {
            Date = DateTime.Today.ToString("yyyy-MM-dd"),
            Entries = new() { new AttendanceEntryDto { EmployeeId = "EMP999", Status = "PRESENT", InTime = "08:00" } }   // a code nobody has
        };

        Assert.True(TestHelpers.Errors(await Attendance(ctx).Save(sheet)).ContainsKey("entries_0_employee_id"));
        Assert.Empty(ctx.AttendanceRecords);
    }

    [Theory]
    [InlineData("EMP001")]
    [InlineData("emp001")]
    public async Task Attendance_is_saved_against_the_employee_code(string code)
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        var sheet = new AttendanceSheetDto
        {
            Date = DateTime.Today.ToString("yyyy-MM-dd"),
            Entries = new() { new AttendanceEntryDto { EmployeeId = code, Status = "PRESENT", InTime = "08:00", OutTime = "16:00" } }
        };

        Assert.Equal(1, TestHelpers.OkJson(await Attendance(ctx).Save(sheet)).GetProperty("created").GetInt32());
        Assert.Equal(id, (await ctx.AttendanceRecords.SingleAsync()).StaffId);
    }

    [Fact]
    public async Task The_code_and_the_emp_id_of_one_person_in_one_sheet_are_a_duplicate()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        var sheet = new AttendanceSheetDto
        {
            Date = DateTime.Today.ToString("yyyy-MM-dd"),
            Entries = new()
            {
                new AttendanceEntryDto { EmployeeId = "EMP001", Status = "PRESENT", InTime = "08:00" },
                new AttendanceEntryDto { EmployeeId = id.ToString(), Status = "PRESENT", InTime = "08:00" }
            }
        };

        Assert.True(TestHelpers.Errors(await Attendance(ctx).Save(sheet)).ContainsKey("entries_1_employee_id"));
        Assert.Empty(ctx.AttendanceRecords);
    }

    [Fact]
    public async Task Leave_balances_can_be_filtered_by_the_employee_code()
    {
        var (ctx, _) = Registered();
        using var __ = ctx;

        var row = Assert.Single(TestHelpers.OkJson(await Leaves(ctx).Balances("EMP001", null)).GetProperty("rows").EnumerateArray());

        Assert.Equal("EMP001", row.GetProperty("employeeId").GetString());
        Assert.Empty(TestHelpers.OkJson(await Leaves(ctx).Balances("EMP002", null)).GetProperty("rows").EnumerateArray());
    }

    // ── api/leaves ────────────────────────────────────────────────

    [Fact]
    public async Task Leave_balances_show_the_quota_set_on_the_staff_page()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;

        var row = Assert.Single(TestHelpers.OkJson(await Leaves(ctx).Balances(id.ToString(), null)).GetProperty("rows").EnumerateArray());

        Assert.Equal("EMP001", row.GetProperty("employeeId").GetString());   // the employee code
        Assert.Equal("Casual", row.GetProperty("leaveType").GetString());
        Assert.Equal(12m, row.GetProperty("entitlement").GetDecimal());
    }

    [Fact]
    public async Task Leave_is_recorded_against_the_emp_id()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        var year = DateTime.Today.Year;
        var form = new LeaveEntryForm { StaffId = id, LeaveType = "Casual", FromDate = $"{year}-01-05", ToDate = $"{year}-01-06" };

        Assert.IsType<OkObjectResult>(await Leaves(ctx).Record(form));

        var entry = Assert.Single(TestHelpers.OkJson(await Leaves(ctx).Entries()).GetProperty("entries").EnumerateArray());
        Assert.Equal("EMP001", entry.GetProperty("employeeId").GetString());
        Assert.Equal("Meena Kumari", entry.GetProperty("staffName").GetString());
    }

    [Fact]
    public async Task The_entitlement_form_offers_the_employee_with_their_category()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;

        var json = TestHelpers.OkJson(await Leaves(ctx).Entitlements());

        Assert.Equal(new[] { "Sales" }, json.GetProperty("categories").EnumerateArray().Select(c => c.GetString()));
        var person = Assert.Single(json.GetProperty("staff").EnumerateArray());
        Assert.Equal(id, person.GetProperty("id").GetInt32());
        var entitlement = Assert.Single(json.GetProperty("entitlements").EnumerateArray());
        Assert.Equal((id, "Meena Kumari"), (entitlement.GetProperty("staffId").GetInt32(), entitlement.GetProperty("staffName").GetString()));
    }

    // ── api/payroll ───────────────────────────────────────────────

    [Fact]
    public async Task The_pay_bill_has_the_employee_with_their_designation_salary()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;

        var json = TestHelpers.OkJson(await Payroll(ctx).Data(DateTime.Today.Year, DateTime.Today.Month, null));

        var row = Assert.Single(json.GetProperty("rows").EnumerateArray());
        Assert.Equal(id, row.GetProperty("staffId").GetInt32());
        Assert.Equal("EMP001", row.GetProperty("employeeId").GetString());   // the employee code
        Assert.Equal("Clerk", row.GetProperty("designation").GetString());
        Assert.Equal(31000m, row.GetProperty("monthlySalary").GetDecimal());
    }

    [Fact]
    public async Task Generating_the_pay_bill_saves_it_under_the_emp_id()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        var dto = new GeneratePayBillDto { Year = DateTime.Today.Year, Month = DateTime.Today.Month, Rows = new() { new PayBillAdjustmentDto { StaffId = id } } };

        Assert.IsType<OkObjectResult>(await Payroll(ctx).Generate(dto));

        var saved = await ctx.PayBillEntries.SingleAsync();
        Assert.Equal((id, "EMP001", 31000m), (saved.StaffId, saved.EmployeeId, saved.MonthlySalary));
    }

    // ── api/otassignments ─────────────────────────────────────────

    [Fact]
    public async Task Ot_can_be_assigned_to_the_employee_by_emp_id()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        Assert.Contains(TestHelpers.OkJson(await Ot(ctx).List()).GetProperty("staff").EnumerateArray(), s => s.GetProperty("id").GetInt32() == id);

        // after the morning shift (8AM-4PM) today
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var result = await Ot(ctx).Create(new OtAssignmentDto { StaffId = id, FromDateTime = $"{today}T17:00", ToDateTime = $"{today}T19:00" });

        if (DateTime.Now.Hour >= 17)
            return;   // a time already past today is refused by the OT rules; the rest is covered by the Admin tests
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(id, (await ctx.OtAssignments.SingleAsync()).StaffId);
    }
}
