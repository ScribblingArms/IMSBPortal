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

/// <summary>Shared fixtures for the join date, salary, annual quota and photo tests.</summary>
internal static class StaffExtras
{
    public static readonly string[] LeaveTypeNames = { "Casual", "Earned", "Sick" };

    /// <summary>A context with the SERP masters and the Casual / Earned / Sick leave types (in-memory databases skip HasData).</summary>
    public static IMSBContext Context()
    {
        var ctx = TestHelpers.CreateStaffContext();
        SeedLeaveTypes(ctx);
        ctx.SaveChanges();
        return ctx;
    }

    public static void SeedLeaveTypes(IMSBContext ctx) =>
        ctx.LeaveTypes.AddRange(LeaveTypeNames.Select(n => new LeaveType { Name = n }));

    // smallest byte strings the server takes for a JPEG / PNG (it checks the signature and the size)
    public static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
    public static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };

    public static string JpegDataUrl => "data:image/jpeg;base64," + Convert.ToBase64String(Jpeg);

    public static List<LeaveQuotaDto> Quotas(decimal? casual, decimal? earned, decimal? sick)
    {
        var list = new List<LeaveQuotaDto>();
        if (casual != null) list.Add(new LeaveQuotaDto { LeaveType = "Casual", AnnualQuota = casual });
        if (earned != null) list.Add(new LeaveQuotaDto { LeaveType = "Earned", AnnualQuota = earned });
        if (sick != null) list.Add(new LeaveQuotaDto { LeaveType = "Sick", AnnualQuota = sick });
        return list;
    }

    public static ApiStaffController Api(IMSBContext ctx, string role = "Admin")
    {
        var services = new ServiceCollection().AddSingleton<IStringLocalizer>(TestHelpers.SharedLocalizer()).BuildServiceProvider();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "api-tester"), new Claim(ClaimTypes.Role, role) }, "Test"));
        return new ApiStaffController(new StaffDetails(ctx), new Leaves(ctx))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user, RequestServices = services } }
        };
    }

    public static UpdateStaffDto DtoFor(Employee e) => new()
    {
        FullName = e.EmpName!, Mobile = e.Mobile!, Gender = e.Gender!, EmployeeTypeId = e.EmpTypeId?.ToString() ?? "",
        DepartmentId = e.DepartmentId?.ToString(), AgencyId = e.AgencyId?.ToString(), ShiftId = e.ShiftId?.ToString(),
        StallNumber = e.StallNum, JoinDate = e.JoinDate?.ToString("yyyy-MM-dd")
    };
}

// ── Join date (tbl_Employee.JoinDate) ──
public class StaffJoinDateAndSalaryTests
{
    [Fact]
    public async Task Create_saves_the_join_date()
    {
        using var ctx = StaffExtras.Context();

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto()));

        Assert.Equal(new DateTime(2020, 1, 15), (await ctx.Employees.SingleAsync()).JoinDate);
    }

    [Fact]
    public async Task Salary_is_listed_for_admins_only()
    {
        using var ctx = StaffExtras.Context();
        ctx.EmployeeDesignations.Single(d => d.DesigId == TestHelpers.SalesDesignation).BaseSalary = 27000m;
        var e = TestHelpers.ExistingEmployee(501, "Paid", "9876500001");
        e.JoinDate = new DateTime(2019, 4, 1);
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));
        var staff = TestHelpers.OkJson(await TestHelpers.CreateController(ctx, role: "Staff").IndexData(null, null, null));

        Assert.Equal(27000m, admin.GetProperty("staff")[0].GetProperty("monthlySalary").GetDecimal());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, staff.GetProperty("staff")[0].GetProperty("monthlySalary").ValueKind);
        Assert.Equal("2019-04-01", staff.GetProperty("staff")[0].GetProperty("joinDate").GetString());   // the join date is for everyone
        // the designation suggestions carry the salary for admins only, too
        Assert.Contains(admin.GetProperty("designations").EnumerateArray(), d => d.GetProperty("salary").ValueKind == System.Text.Json.JsonValueKind.Number);
        Assert.All(staff.GetProperty("designations").EnumerateArray(), d => Assert.Equal(System.Text.Json.JsonValueKind.Null, d.GetProperty("salary").ValueKind));
    }

    [Theory]
    [InlineData(null, "Join date is required")]
    [InlineData("", "Join date is required")]
    [InlineData("15-01-2020", "Invalid join date")]
    [InlineData("2020-02-30", "Invalid join date")]
    public async Task Registering_needs_a_valid_join_date(string? joinDate, string message)
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.JoinDate = joinDate;

        Assert.Equal(message, TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["join_date"]);
        Assert.Empty(ctx.Employees);
    }

    [Fact]
    public async Task Join_date_cannot_be_in_the_future_but_today_is_fine()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.JoinDate = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        Assert.Equal("Join date cannot be in the future", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["join_date"]);

        dto.JoinDate = DateTime.Today.ToString("yyyy-MM-dd");
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));
    }

    [Fact]
    public async Task A_serp_record_without_a_join_date_can_be_edited_but_a_join_date_on_record_cannot_be_cleared()
    {
        using var ctx = StaffExtras.Context();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "From SERP", "9876500001"));   // no JoinDate
        await ctx.SaveChangesAsync();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        dto.JoinDate = "2018-07-01";
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        Assert.Equal(new DateTime(2018, 7, 1), (await ctx.Employees.AsNoTracking().SingleAsync()).JoinDate);

        dto.JoinDate = "";
        Assert.Equal("Join date is required", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto))["join_date"]);
    }

    // tbl_Designation.BaseSalary is decimal(10,2)
    [Theory]
    [InlineData(-0.01, false)]
    [InlineData(0, true)]
    [InlineData(99999999.99, true)]
    [InlineData(100000000, false)]
    public void Salary_range_is_checked_on_the_form(decimal salary, bool valid)
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.MonthlySalary = salary;
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(dto, new(dto), results, true);
        Assert.Equal(valid, !results.Any(r => r.MemberNames.Contains(nameof(CreateStaffDto.MonthlySalary))));
    }
}

// ── Annual leave quotas: the Leaves page's leave_entitlements, keyed by EMP_ID ──
public class StaffLeaveQuotaTests
{
    private static async Task<IMSBContext> ContextWithEmployee()
    {
        var ctx = StaffExtras.Context();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));
        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static Dictionary<string, decimal> OnRecord(IMSBContext ctx, int empId = 501) =>
        ctx.LeaveEntitlements.AsNoTracking().Where(q => q.StaffId == empId && q.Year == DateTime.Today.Year)
            .ToDictionary(q => q.LeaveType, q => q.AnnualQuota);

    [Fact]
    public async Task IndexData_offers_the_leave_types()
    {
        using var ctx = StaffExtras.Context();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));
        Assert.Equal(StaffExtras.LeaveTypeNames, json.GetProperty("leaveTypes").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task Registering_saves_casual_earned_and_sick_as_leave_entitlements()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = StaffExtras.Quotas(12, 15, 7.5m);

        var id = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(dto)).GetProperty("employeeId").GetInt32();

        var rows = await ctx.LeaveEntitlements.AsNoTracking().OrderBy(q => q.LeaveType).ToListAsync();
        Assert.Equal(new[] { "Casual", "Earned", "Sick" }, rows.Select(r => r.LeaveType));
        Assert.Equal(new[] { 12m, 15m, 7.5m }, rows.Select(r => r.AnnualQuota));
        Assert.All(rows, r =>
        {
            Assert.Equal(id, r.StaffId);                 // the EMP_ID
            Assert.Equal(DateTime.Today.Year, r.Year);
            Assert.Equal("Sales", r.StaffCategory);      // the Staff Category name
        });
        // leave marked on attendance draws on the first type (by name) with days
        Assert.Equal(new[] { "Casual" }, rows.Where(r => r.IsAttendanceLeaveType).Select(r => r.LeaveType));
    }

    [Fact]
    public async Task A_type_with_no_days_does_not_take_the_attendance_policy()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = StaffExtras.Quotas(0, 10, null);

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Earned", (await ctx.LeaveEntitlements.SingleAsync(e => e.IsAttendanceLeaveType)).LeaveType);
    }

    [Fact]
    public async Task Registered_quotas_appear_in_the_leaves_page_balances()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = StaffExtras.Quotas(12, null, null);
        var id = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(dto)).GetProperty("employeeId").GetInt32();

        var balance = Assert.Single(await new IMSB.Backend.Leaves(ctx).BalancesAsync(DateTime.Today.Year, id.ToString()));

        Assert.Equal(("Meena Kumari", "Casual", 12m, 0m, 12m), (balance.StaffName, balance.LeaveType, balance.Entitlement, balance.Taken, balance.Balance));
    }

    [Fact]
    public async Task A_type_left_blank_is_simply_not_saved()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = StaffExtras.Quotas(12, null, null);

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));
        Assert.Equal(new[] { "Casual" }, OnRecord(ctx, (await ctx.Employees.SingleAsync()).EmpId).Keys);
    }

    [Fact]
    public async Task Registering_without_quotas_saves_none()
    {
        using var ctx = StaffExtras.Context();
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto()));
        Assert.Empty(ctx.LeaveEntitlements);
    }

    [Theory]
    [InlineData("Study", 5, null)]          // not a leave type
    [InlineData("Casual", -1, null)]
    [InlineData("Casual", 367, null)]
    [InlineData("Casual", 2.25, null)]      // half-days only
    [InlineData("Casual", 5, "casual")]     // the same type twice
    public async Task Invalid_quotas_refuse_the_registration_and_save_nothing(string type, decimal days, string? duplicate)
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = new() { new LeaveQuotaDto { LeaveType = type, AnnualQuota = days } };
        if (duplicate != null) dto.LeaveQuotas.Add(new LeaveQuotaDto { LeaveType = duplicate, AnnualQuota = 1 });

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Enter leave days from 0 to 366, in half-day steps (for example 12 or 7.5).", errors["leave_quotas"]);
        Assert.Empty(ctx.Employees);
        Assert.Empty(ctx.LeaveEntitlements);
    }

    [Fact]
    public async Task Registering_cannot_send_a_removal()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = new() { new LeaveQuotaDto { LeaveType = "Casual", AnnualQuota = null } };

        Assert.True(TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto)).ContainsKey("leave_quotas"));
    }

    [Fact]
    public async Task Editing_adds_changes_and_removes_only_the_listed_types()
    {
        using var ctx = await ContextWithEmployee();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.LeaveQuotas = StaffExtras.Quotas(12, 15, 6);
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        dto.LeaveQuotas = new()
        {
            new LeaveQuotaDto { LeaveType = "Casual", AnnualQuota = 10 },   // changed
            new LeaveQuotaDto { LeaveType = "Sick", AnnualQuota = null },    // removed
        };
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        Assert.Equal(new Dictionary<string, decimal> { ["Casual"] = 10, ["Earned"] = 15 }, OnRecord(ctx));   // Earned untouched
    }

    [Fact]
    public async Task Editing_without_quotas_leaves_them_alone()
    {
        using var ctx = await ContextWithEmployee();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.LeaveQuotas = StaffExtras.Quotas(12, null, null);
        await TestHelpers.CreateController(ctx).Update(501, dto);

        dto.LeaveQuotas = null;
        dto.FullName = "Renamed";
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        Assert.Equal(new Dictionary<string, decimal> { ["Casual"] = 12 }, OnRecord(ctx));
    }

    [Fact]
    public async Task A_quota_below_the_leave_already_taken_is_refused_with_its_type()
    {
        using var ctx = await ContextWithEmployee();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.LeaveQuotas = StaffExtras.Quotas(12, null, null);
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        var year = DateTime.Today.Year;
        ctx.StaffLeaveEntries.Add(new StaffLeaveEntry
        {
            StaffId = 501, LeaveType = "Casual", Year = year, FromDate = new DateTime(year, 1, 5), ToDate = new DateTime(year, 1, 9), Days = 5
        });
        await ctx.SaveChangesAsync();

        dto.LeaveQuotas = StaffExtras.Quotas(3, null, null);
        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto));

        Assert.StartsWith("Casual: ", errors["leave_quotas"]);
        Assert.Equal(12m, OnRecord(ctx)["Casual"]);
    }

    [Fact]
    public async Task An_invalid_quota_saves_none_of_the_edit()
    {
        using var ctx = await ContextWithEmployee();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.FullName = "Should Not Save";
        dto.LeaveQuotas = new() { new LeaveQuotaDto { LeaveType = "Casual", AnnualQuota = 400 } };

        Assert.True(TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto)).ContainsKey("leave_quotas"));
        Assert.Equal("Someone", (await ctx.Employees.AsNoTracking().SingleAsync()).EmpName);
        Assert.Empty(ctx.LeaveEntitlements);
    }

    [Fact]
    public async Task LeaveQuotas_endpoint_returns_this_years_quotas_with_taken_and_balance()
    {
        using var ctx = await ContextWithEmployee();
        var year = DateTime.Today.Year;
        ctx.LeaveEntitlements.AddRange(
            new LeaveEntitlement { StaffId = 501, StaffCategory = "Sales", LeaveType = "Sick", Year = year, AnnualQuota = 6 },
            new LeaveEntitlement { StaffId = 501, StaffCategory = "Sales", LeaveType = "Casual", Year = year, AnnualQuota = 12, IsAttendanceLeaveType = true },
            new LeaveEntitlement { StaffId = 501, StaffCategory = "Sales", LeaveType = "Earned", Year = year - 1, AnnualQuota = 99 });
        ctx.StaffLeaveEntries.Add(new StaffLeaveEntry
        {
            StaffId = 501, LeaveType = "Casual", Year = year, FromDate = new DateTime(year, 1, 5), ToDate = new DateTime(year, 1, 6), Days = 2
        });
        await ctx.SaveChangesAsync();

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).LeaveQuotas(501));

        Assert.Equal(year, json.GetProperty("year").GetInt32());
        var rows = json.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(new[] { "Casual", "Sick" }, rows.Select(r => r.GetProperty("leaveType").GetString()));
        Assert.Equal(new[] { 12m, 6m }, rows.Select(r => r.GetProperty("annualQuota").GetDecimal()));
        Assert.Equal(new[] { 2m, 0m }, rows.Select(r => r.GetProperty("taken").GetDecimal()));
        Assert.Equal(new[] { 10m, 6m }, rows.Select(r => r.GetProperty("balance").GetDecimal()));
    }

    [Fact]
    public async Task LeaveQuotas_of_an_unknown_employee_is_404()
    {
        using var ctx = StaffExtras.Context();
        Assert.IsType<NotFoundResult>(await TestHelpers.CreateController(ctx).LeaveQuotas(999));
    }

    [Fact]
    public async Task Deleting_the_employee_deletes_their_quotas_leave_attendance_and_ot()
    {
        using var ctx = await ContextWithEmployee();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(502, "Other", "9876500002"));
        var year = DateTime.Today.Year;
        foreach (var id in new[] { 501, 502 })
        {
            ctx.LeaveEntitlements.Add(new LeaveEntitlement { StaffId = id, StaffCategory = "Sales", LeaveType = "Casual", Year = year, AnnualQuota = 12 });
            ctx.StaffLeaveEntries.Add(new StaffLeaveEntry { StaffId = id, LeaveType = "Casual", Year = year, FromDate = new DateTime(year, 1, 5), ToDate = new DateTime(year, 1, 5), Days = 1 });
            ctx.AttendanceRecords.Add(new AttendanceRecord { StaffId = id, AttendanceDate = new DateTime(year, 1, 2) });
            ctx.OtAssignments.Add(new OtAssignment { StaffId = id, FromDateTime = new DateTime(year, 1, 2, 18, 0, 0), ToDateTime = new DateTime(year, 1, 2, 20, 0, 0) });
        }
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Delete(501));

        Assert.Equal(new int?[] { 502 }, ctx.LeaveEntitlements.AsNoTracking().Select(q => q.StaffId));
        Assert.Equal(new[] { 502 }, ctx.StaffLeaveEntries.AsNoTracking().Select(q => q.StaffId));
        Assert.Equal(new[] { 502 }, ctx.AttendanceRecords.AsNoTracking().Select(q => q.StaffId));
        Assert.Equal(new[] { 502 }, ctx.OtAssignments.AsNoTracking().Select(q => q.StaffId));
        Assert.Null(await ctx.Staff.FindAsync(501));   // gone from the Staff view too
    }
}

// ── Photo (tbl_Employee.ImageBase64) ──
public class StaffPhotoTests
{
    [Theory]
    [InlineData("data:image/jpeg;base64,/9j/4AAQ", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 })]
    [InlineData("/9j/4AAQ", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 })]   // plain base64, as SERP stores it
    [InlineData("  /9j/4AAQ  ", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 })]
    public void Decode_reads_a_data_url_or_plain_base64(string value, byte[] expected) =>
        Assert.Equal(expected, EmployeePhotos.Decode(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("data:image/jpeg,/9j/4AAQ")]   // a data URL that is not base64
    public void Decode_refuses_anything_else(string? value) => Assert.Null(EmployeePhotos.Decode(value));

    [Fact]
    public void Only_jpeg_and_png_up_to_500_kb_are_photos()
    {
        Assert.Equal("image/jpeg", EmployeePhotos.ContentType(StaffExtras.Jpeg));
        Assert.Equal("image/png", EmployeePhotos.ContentType(StaffExtras.Png));
        Assert.Null(EmployeePhotos.ContentType("GIF89a"u8.ToArray()));
        Assert.Null(EmployeePhotos.ContentType(Array.Empty<byte>()));

        Assert.True(EmployeePhotos.IsValid(StaffExtras.JpegDataUrl));
        Assert.True(EmployeePhotos.IsValid(Convert.ToBase64String(StaffExtras.Png)));
        Assert.False(EmployeePhotos.IsValid(Convert.ToBase64String("GIF89a"u8.ToArray())));
        var big = new byte[EmployeePhotos.MaxBytes + 1];
        StaffExtras.Jpeg.CopyTo(big, 0);
        Assert.False(EmployeePhotos.IsValid(Convert.ToBase64String(big)));
        big = big[..EmployeePhotos.MaxBytes];
        Assert.True(EmployeePhotos.IsValid(Convert.ToBase64String(big)));
    }

    [Fact]
    public async Task Registering_with_a_photo_saves_plain_base64()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.Photo = StaffExtras.JpegDataUrl;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal(Convert.ToBase64String(StaffExtras.Jpeg), (await ctx.Employees.SingleAsync()).ImageBase64);   // no "data:" prefix
    }

    [Fact]
    public async Task A_file_that_is_not_a_jpeg_or_png_is_refused()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.Photo = "data:image/gif;base64," + Convert.ToBase64String("GIF89a"u8.ToArray());

        Assert.Equal("The photo must be a JPG or PNG image of at most 500 KB", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["photo"]);
        Assert.Empty(ctx.Employees);
    }

    [Fact]
    public async Task Editing_keeps_replaces_or_removes_the_photo()
    {
        using var ctx = StaffExtras.Context();
        var e = TestHelpers.ExistingEmployee(501, "Someone", "9876500001");
        e.ImageBase64 = Convert.ToBase64String(StaffExtras.Png);
        e.Template = "finger";
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        string? Saved() => ctx.Employees.AsNoTracking().Single().ImageBase64;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));   // no photo sent: kept
        Assert.Equal(Convert.ToBase64String(StaffExtras.Png), Saved());

        dto.Photo = StaffExtras.JpegDataUrl;
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        Assert.Equal(Convert.ToBase64String(StaffExtras.Jpeg), Saved());

        dto.Photo = null;
        dto.RemovePhoto = true;
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        Assert.Null(Saved());
        Assert.Equal("finger", ctx.Employees.AsNoTracking().Single().Template);   // the fingerprint is never touched
    }

    [Fact]
    public async Task Photo_endpoint_serves_the_image_with_its_type()
    {
        using var ctx = StaffExtras.Context();
        var jpeg = TestHelpers.ExistingEmployee(501, "Jpeg", "9876500001");
        jpeg.ImageBase64 = Convert.ToBase64String(StaffExtras.Jpeg);
        var png = TestHelpers.ExistingEmployee(502, "Png", "9876500002");
        png.ImageBase64 = Convert.ToBase64String(StaffExtras.Png);
        ctx.Employees.AddRange(jpeg, png, TestHelpers.ExistingEmployee(503, "None", "9876500003"));
        await ctx.SaveChangesAsync();
        var controller = TestHelpers.CreateController(ctx);

        var file = Assert.IsType<FileContentResult>(await controller.Photo(501));
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Equal(StaffExtras.Jpeg, file.FileContents);
        Assert.Equal("image/png", Assert.IsType<FileContentResult>(await controller.Photo(502)).ContentType);
        Assert.IsType<NotFoundResult>(await controller.Photo(503));   // no photo
        Assert.IsType<NotFoundResult>(await controller.Photo(999));   // no employee
    }

    [Fact]
    public async Task A_photo_saved_by_serp_that_is_not_an_image_is_not_served()
    {
        using var ctx = StaffExtras.Context();
        var e = TestHelpers.ExistingEmployee(501, "Odd", "9876500001");
        e.ImageBase64 = "not base64 at all";
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await TestHelpers.CreateController(ctx).Photo(501));
    }

    [Fact]
    public async Task Lists_say_who_has_a_photo_without_sending_or_loading_it()
    {
        using var ctx = StaffExtras.Context();
        var e = TestHelpers.ExistingEmployee(501, "Pictured", "9876500001");
        e.ImageBase64 = Convert.ToBase64String(StaffExtras.Jpeg);
        e.Template = "finger";
        ctx.Employees.AddRange(e, TestHelpers.ExistingEmployee(502, "Plain", "9876500002"));
        await ctx.SaveChangesAsync();

        var all = await new StaffDetails(ctx).AllAsync();
        Assert.All(all, x => { Assert.Null(x.ImageBase64); Assert.Null(x.Template); });
        Assert.True(all.Single(x => x.EmpId == 501).HasPhoto);
        Assert.False(all.Single(x => x.EmpId == 502).HasPhoto);

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));
        var rows = json.GetProperty("staff").EnumerateArray().ToDictionary(r => r.GetProperty("employeeId").GetInt32());
        Assert.True(rows[501].GetProperty("hasPhoto").GetBoolean());
        Assert.False(rows[502].GetProperty("hasPhoto").GetBoolean());
        Assert.DoesNotContain(Convert.ToBase64String(StaffExtras.Jpeg), json.GetRawText());
    }
}

// ── The same over IMSB.Api ──
public class StaffApiExtrasTests
{
    [Fact]
    public async Task Api_create_saves_join_date_salary_quotas_and_photo()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.MonthlySalary = 21000m;
        dto.LeaveQuotas = StaffExtras.Quotas(12, 15, 6);
        dto.Photo = StaffExtras.JpegDataUrl;

        var id = TestHelpers.OkJson(await StaffExtras.Api(ctx).Create(dto)).GetProperty("employeeId").GetInt32();

        var saved = await ctx.Employees.SingleAsync();
        Assert.Equal(new DateTime(2020, 1, 15), saved.JoinDate);
        Assert.Equal(21000m, (await ctx.EmployeeDesignations.SingleAsync(d => d.DesigId == saved.DesigId)).BaseSalary);   // on the designation
        Assert.Equal(Convert.ToBase64String(StaffExtras.Jpeg), saved.ImageBase64);
        Assert.Equal(3, ctx.LeaveEntitlements.Count(q => q.StaffId == id));
    }

    [Fact]
    public async Task Api_staff_role_cannot_set_the_salary_and_does_not_see_it()
    {
        using var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.MonthlySalary = 21000m;

        var id = TestHelpers.OkJson(await StaffExtras.Api(ctx, "Staff").Create(dto)).GetProperty("employeeId").GetInt32();
        var designation = await ctx.EmployeeDesignations.SingleAsync(d => d.DesigId == ctx.Employees.Single().DesigId);
        Assert.Null(designation.BaseSalary);

        designation.BaseSalary = 5000m;
        await ctx.SaveChangesAsync();
        Assert.Equal(System.Text.Json.JsonValueKind.Null, TestHelpers.OkJson(await StaffExtras.Api(ctx, "Staff").Get(id)).GetProperty("monthlySalary").ValueKind);
        Assert.Equal(5000m, TestHelpers.OkJson(await StaffExtras.Api(ctx).Get(id)).GetProperty("monthlySalary").GetDecimal());
    }

    [Fact]
    public async Task Api_update_changes_the_quotas()
    {
        using var ctx = StaffExtras.Context();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));
        await ctx.SaveChangesAsync();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.LeaveQuotas = StaffExtras.Quotas(null, 20, null);

        Assert.IsType<OkObjectResult>(await StaffExtras.Api(ctx).Update(501, dto));

        var json = TestHelpers.OkJson(await StaffExtras.Api(ctx).LeaveQuotas(501));
        var row = Assert.Single(json.GetProperty("rows").EnumerateArray());
        Assert.Equal("Earned", row.GetProperty("leaveType").GetString());
        Assert.Equal(20m, row.GetProperty("annualQuota").GetDecimal());
        Assert.IsType<NotFoundResult>(await StaffExtras.Api(ctx).LeaveQuotas(999));
    }

    [Fact]
    public async Task Api_photo_endpoint_serves_the_image()
    {
        using var ctx = StaffExtras.Context();
        var e = TestHelpers.ExistingEmployee(501, "Pictured", "9876500001");
        e.ImageBase64 = Convert.ToBase64String(StaffExtras.Png);
        ctx.Employees.AddRange(e, TestHelpers.ExistingEmployee(502, "Plain", "9876500002"));
        await ctx.SaveChangesAsync();

        var file = Assert.IsType<FileContentResult>(await StaffExtras.Api(ctx).Photo(501));
        Assert.Equal("image/png", file.ContentType);
        Assert.IsType<NotFoundResult>(await StaffExtras.Api(ctx).Photo(502));
    }

    [Fact]
    public async Task Api_list_has_leave_types_join_dates_and_photo_flags()
    {
        using var ctx = StaffExtras.Context();
        var e = TestHelpers.ExistingEmployee(501, "Pictured", "9876500001");
        e.ImageBase64 = Convert.ToBase64String(StaffExtras.Jpeg);
        e.JoinDate = new DateTime(2021, 3, 1);
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();

        var json = TestHelpers.OkJson(await StaffExtras.Api(ctx).List(null, null, null));

        Assert.Equal(StaffExtras.LeaveTypeNames, json.GetProperty("leaveTypes").EnumerateArray().Select(t => t.GetString()));
        var row = json.GetProperty("staff")[0];
        Assert.Equal("2021-03-01", row.GetProperty("joinDate").GetString());
        Assert.True(row.GetProperty("hasPhoto").GetBoolean());
    }

    [Theory]
    [InlineData(nameof(ApiStaffController.LeaveQuotas), "{id:int}/leave-quotas")]
    [InlineData(nameof(ApiStaffController.Photo), "{id:int}/photo")]
    public void New_api_endpoints_are_gets_on_their_routes(string action, string template)
    {
        var attr = typeof(ApiStaffController).GetMethod(action)!.GetCustomAttributes(true)
            .OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single();
        Assert.Equal(new[] { "GET" }, attr.HttpMethods);
        Assert.Equal(template, attr.Template);
    }
}

// ── The migration adding JoinDate / MonthlySalary to tbl_Employee ──
public class EmployeeColumnsMigrationTests
{
    private static string Sql(IEnumerable<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> ops) =>
        string.Join("\n", ops.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(o => o.Sql));

    [Fact]
    public void Up_adds_both_columns_only_when_missing_and_creates_the_quota_table()
    {
        var migration = new IMSB.Dto.Migrations.AddEmployeeJoinDateSalaryAndLeaveQuotas();
        var sql = Sql(migration.UpOperations);

        Assert.Contains("IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'JoinDate') IS NULL", sql);
        Assert.Contains("ADD [JoinDate] date NULL", sql);
        Assert.Contains("IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'MonthlySalary') IS NULL", sql);
        Assert.Contains("ADD [MonthlySalary] decimal(18,2) NULL", sql);
        var table = Assert.Single(migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>());
        Assert.Equal("employee_leave_quotas", table.Name);
        Assert.Equal("tbl_Employee", Assert.Single(table.ForeignKeys).PrincipalTable);
    }

    [Fact]
    public void Down_removes_them_again()
    {
        var migration = new IMSB.Dto.Migrations.AddEmployeeJoinDateSalaryAndLeaveQuotas();
        var sql = Sql(migration.DownOperations);

        Assert.Contains("DROP COLUMN [JoinDate]", sql);
        Assert.Contains("DROP COLUMN [MonthlySalary]", sql);
        Assert.Contains(migration.DownOperations, o => o is Microsoft.EntityFrameworkCore.Migrations.Operations.DropTableOperation d && d.Name == "employee_leave_quotas");
    }

    [Fact]
    public void The_next_migration_adds_department_id_and_drops_the_employee_salary()
    {
        var migration = new IMSB.Dto.Migrations.AddEmployeeDepartmentIdDropMonthlySalary();

        var up = Sql(migration.UpOperations);
        Assert.Contains("IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'DepartmentID') IS NULL", up);
        Assert.Contains("ADD [DepartmentID] int NULL", up);
        Assert.Contains("IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'MonthlySalary') IS NOT NULL", up);
        Assert.Contains("DROP COLUMN [MonthlySalary]", up);
        Assert.DoesNotContain(migration.UpOperations, o => o is not Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation);   // tbl_Designation is left as it is

        var down = Sql(migration.DownOperations);
        Assert.Contains("ADD [MonthlySalary] decimal(18,2) NULL", down);
        Assert.Contains("DROP COLUMN [DepartmentID]", down);
    }

    [Fact]
    public void Employee_maps_the_columns_and_the_salary_lives_on_the_designation()
    {
        using var ctx = new IMSBContext(new DbContextOptionsBuilder<IMSBContext>().UseSqlServer("Server=unused;Database=unused").Options);
        var model = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(ctx).Model;
        var employee = model.FindEntityType(typeof(Employee))!;

        Assert.Equal("date", employee.FindProperty(nameof(Employee.JoinDate))!.GetColumnType());
        Assert.Equal("DepartmentID", employee.FindProperty(nameof(Employee.DepartmentId))!.GetColumnName());
        Assert.Equal("Desig", employee.FindProperty(nameof(Employee.DesigId))!.GetColumnName());
        Assert.DoesNotContain(employee.GetProperties(), p => p.GetColumnName() == "MonthlySalary");
        Assert.Null(employee.FindProperty(nameof(Employee.HasPhoto)));   // computed, not stored

        var salary = model.FindEntityType(typeof(EmployeeDesignation))!.FindProperty(nameof(EmployeeDesignation.BaseSalary))!;
        Assert.Equal("BaseSalary", salary.GetColumnName());
        Assert.Equal("decimal(10,2)", salary.GetColumnType());
    }
}
