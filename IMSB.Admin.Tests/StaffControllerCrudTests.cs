using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

public class StaffControllerCreateTests
{
    private static string Iso(DateTime d) => d.ToString("yyyy-MM-dd");

    [Fact]
    public async Task Create_valid_staff_persists_record()
    {
        using var ctx = TestHelpers.CreateContext();
        var controller = TestHelpers.CreateController(ctx, userName: "hr@imsb");
        var dto = TestHelpers.ValidCreateDto();
        TestHelpers.BindModelState(controller, dto);

        var json = TestHelpers.OkJson(await controller.Create(dto));

        var saved = await ctx.Staff.SingleAsync();
        Assert.Equal(json.GetProperty("employeeId").GetString(), saved.EmployeeId);
        Assert.Equal("Meena Kumari", saved.FullName);
        Assert.Equal("9876500001", saved.Mobile);
        Assert.Equal("new.staff@example.com", saved.Email);
        Assert.Equal(new DateTime(1990, 5, 10), saved.DateOfBirth);
        Assert.Equal(new DateTime(2020, 1, 15), saved.JoinDate);
        Assert.Equal(TestHelpers.SalesDept, saved.DepartmentId);
        Assert.Equal("MORNING", saved.Shift);
        Assert.Equal("ACTIVE", saved.Status);
        Assert.Equal("hr@imsb", saved.CreatedBy);
    }

    [Fact]
    public async Task Create_trims_text_and_stores_blank_email_as_null()
    {
        using var ctx = TestHelpers.CreateContext();
        var controller = TestHelpers.CreateController(ctx);
        var dto = TestHelpers.ValidCreateDto(email: "   ");
        dto.FullName = "  Meena Kumari  ";
        dto.Designation = "  Clerk ";

        await controller.Create(dto);

        var saved = await ctx.Staff.SingleAsync();
        Assert.Equal("Meena Kumari", saved.FullName);
        Assert.Equal("Clerk", saved.Designation);
        Assert.Null(saved.Email);
    }

    [Fact]
    public async Task First_employee_id_is_EMP001()
    {
        using var ctx = TestHelpers.CreateContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto()));
        Assert.Equal("EMP001", json.GetProperty("employeeId").GetString());
    }

    [Fact]
    public async Task Employee_id_continues_after_highest_existing_number_in_any_format()
    {
        using var ctx = TestHelpers.CreateContext();
        ctx.Staff.AddRange(
            TestHelpers.ExistingStaff("EMP002", "A", "900000001"),
            TestHelpers.ExistingStaff("EMP010", "B", "900000002"),
            TestHelpers.ExistingStaff("EMP-0007", "C", "900000003"),
            TestHelpers.ExistingStaff("LEGACY", "D", "900000004"));
        await ctx.SaveChangesAsync();

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto()));

        Assert.Equal("EMP011", json.GetProperty("employeeId").GetString());
    }

    [Fact]
    public async Task Consecutive_creates_get_unique_sequential_ids()
    {
        using var ctx = TestHelpers.CreateContext();
        var controller = TestHelpers.CreateController(ctx);
        await controller.Create(TestHelpers.ValidCreateDto(mobile: "900000011", email: null));
        await controller.Create(TestHelpers.ValidCreateDto(mobile: "900000012", email: null));

        var ids = await ctx.Staff.Select(s => s.EmployeeId).OrderBy(x => x).ToListAsync();
        Assert.Equal(new[] { "EMP001", "EMP002" }, ids);
    }

    [Fact]
    public async Task Null_body_is_bad_request()
    {
        using var ctx = TestHelpers.CreateContext();
        Assert.IsType<BadRequestResult>(await TestHelpers.CreateController(ctx).Create(null));
    }

    [Fact]
    public async Task Model_state_errors_are_returned_with_form_field_names()
    {
        using var ctx = TestHelpers.CreateContext();
        var controller = TestHelpers.CreateController(ctx);
        var dto = TestHelpers.ValidCreateDto(mobile: "12");
        dto.FullName = "A";
        dto.DepartmentId = "";
        TestHelpers.BindModelState(controller, dto);

        var errors = TestHelpers.Errors(await controller.Create(dto));

        Assert.Contains("full_name", errors.Keys);
        Assert.Contains("mobile", errors.Keys);
        Assert.Contains("department_id", errors.Keys);
        Assert.Empty(ctx.Staff);
    }

    [Fact]
    public async Task Under_18_is_rejected()
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DateOfBirth = Iso(DateTime.Today.AddYears(-18).AddDays(1));
        dto.JoinDate = Iso(DateTime.Today);

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Staff must be at least 18 years old", errors["date_of_birth"]);
    }

    [Fact]
    public async Task Exactly_18_today_is_accepted()
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DateOfBirth = Iso(DateTime.Today.AddYears(-18));
        dto.JoinDate = Iso(DateTime.Today);

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));
    }

    [Fact]
    public async Task Future_dates_are_rejected()
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DateOfBirth = Iso(DateTime.Today.AddDays(1));
        dto.JoinDate = Iso(DateTime.Today.AddDays(1));

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("DOB cannot be in the future", errors["date_of_birth"]);
        Assert.Equal("Join date cannot be in the future", errors["join_date"]);
    }

    [Fact]
    public async Task Join_date_before_18th_birthday_is_rejected()
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DateOfBirth = "1990-05-10";
        dto.JoinDate = "2008-05-09";

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Join date must be at least 18 years after DOB", errors["join_date"]);
    }

    [Theory]
    [InlineData("10-05-1990")]
    [InlineData("1990/05/10")]
    [InlineData("not a date")]
    public async Task Dates_must_be_iso_format(string dob)
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DateOfBirth = dob;

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Invalid date of birth", errors["date_of_birth"]);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("99999999-9999-9999-9999-999999999999")]
    public async Task Unknown_department_is_rejected(string departmentId)
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DepartmentId = departmentId;

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Department is required", errors["department_id"]);
    }

    [Fact]
    public async Task Shift_not_in_shift_table_is_rejected()
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.Shift = "NIGHT";

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Invalid shift", errors["shift"]);
    }

    [Fact]
    public async Task Any_shift_is_accepted_when_no_shifts_are_configured()
    {
        using var ctx = TestHelpers.CreateContext(seedReferenceData: false);
        ctx.Departments.Add(new Department { Id = TestHelpers.SalesDept, Name = "Sales" });
        await ctx.SaveChangesAsync();
        var dto = TestHelpers.ValidCreateDto();
        dto.Shift = "ANYTHING";

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));
    }

    [Fact]
    public async Task Duplicate_mobile_is_rejected()
    {
        using var ctx = TestHelpers.CreateContext();
        ctx.Staff.Add(TestHelpers.ExistingStaff("EMP001", "Existing", "9876500001"));
        await ctx.SaveChangesAsync();

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto(mobile: "9876500001")));

        Assert.Equal("Mobile already registered", errors["mobile"]);
        Assert.Single(ctx.Staff);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_case_insensitively()
    {
        using var ctx = TestHelpers.CreateContext();
        ctx.Staff.Add(TestHelpers.ExistingStaff("EMP001", "Existing", "900000099", email: "Taken@Example.com"));
        await ctx.SaveChangesAsync();

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto(email: "taken@example.com")));

        Assert.Equal("Email already registered", errors["email"]);
    }
}

public class StaffControllerUpdateTests
{
    private static async Task<(IMSB.Dto.Models.IMSBContext ctx, Staff staff)> SeedOne(string shift = "MORNING", string status = "ACTIVE")
    {
        var ctx = TestHelpers.CreateContext();
        var staff = TestHelpers.ExistingStaff("EMP001", "Meena Kumari", "9876500001", status: status, shift: shift, email: "meena@example.com");
        ctx.Staff.Add(staff);
        await ctx.SaveChangesAsync();
        return (ctx, staff);
    }

    private static UpdateStaffDto DtoFor(Staff s, string? status = null) => new()
    {
        FullName = s.FullName, Mobile = s.Mobile, Email = s.Email, DateOfBirth = s.DateOfBirth.ToString("yyyy-MM-dd"),
        Gender = s.Gender, DepartmentId = s.DepartmentId.ToString(), Designation = s.Designation,
        JoinDate = s.JoinDate.ToString("yyyy-MM-dd"), Shift = s.Shift, Status = status
    };

    [Fact]
    public async Task Update_changes_fields_and_audit_columns()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        var dto = DtoFor(staff, "ON_LEAVE");
        dto.Designation = "Senior Executive";
        dto.DepartmentId = TestHelpers.AdminDept.ToString();
        dto.Shift = "EVENING";

        var result = await TestHelpers.CreateController(ctx, userName: "editor@imsb").Update("EMP001", dto);

        Assert.Equal("EMP001", TestHelpers.OkJson(result).GetProperty("employeeId").GetString());
        var saved = await ctx.Staff.SingleAsync();
        Assert.Equal("Senior Executive", saved.Designation);
        Assert.Equal(TestHelpers.AdminDept, saved.DepartmentId);
        Assert.Equal("EVENING", saved.Shift);
        Assert.Equal("ON_LEAVE", saved.Status);
        Assert.Equal("editor@imsb", saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
        Assert.Equal("EMP001", saved.EmployeeId);
    }

    [Fact]
    public async Task Update_without_status_keeps_current_status()
    {
        var (ctx, staff) = await SeedOne(status: "ON_LEAVE");
        using var _ = ctx;

        await TestHelpers.CreateController(ctx).Update("EMP001", DtoFor(staff, status: null));

        Assert.Equal("ON_LEAVE", (await ctx.Staff.SingleAsync()).Status);
    }

    [Fact]
    public async Task Own_mobile_and_email_are_not_duplicates()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", DtoFor(staff)));
    }

    [Fact]
    public async Task Changing_to_another_staffs_mobile_is_rejected()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        ctx.Staff.Add(TestHelpers.ExistingStaff("EMP002", "Other", "900000002"));
        await ctx.SaveChangesAsync();
        var dto = DtoFor(staff);
        dto.Mobile = "900000002";

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update("EMP001", dto));

        Assert.Equal("Mobile already registered", errors["mobile"]);
        Assert.Equal("9876500001", (await ctx.Staff.SingleAsync(s => s.EmployeeId == "EMP001")).Mobile);
    }

    [Fact]
    public async Task Legacy_shift_not_in_table_can_be_saved_unchanged()
    {
        var (ctx, staff) = await SeedOne(shift: "Night");
        using var _ = ctx;
        var dto = DtoFor(staff);
        dto.Designation = "Guard";

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
        Assert.Equal("Night", (await ctx.Staff.SingleAsync()).Shift);
    }

    [Fact]
    public async Task Changing_to_an_unknown_shift_is_rejected()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        var dto = DtoFor(staff);
        dto.Shift = "Night";

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update("EMP001", dto));

        Assert.Equal("Invalid shift", errors["shift"]);
    }

    [Fact]
    public async Task Update_unknown_employee_returns_404()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        Assert.IsType<NotFoundResult>(await TestHelpers.CreateController(ctx).Update("EMP999", DtoFor(staff)));
    }

    [Fact]
    public async Task Update_null_body_is_bad_request()
    {
        var (ctx, _) = await SeedOne();
        using var __ = ctx;
        Assert.IsType<BadRequestResult>(await TestHelpers.CreateController(ctx).Update("EMP001", null));
    }
}

public class StaffControllerDeleteTests
{
    [Fact]
    public async Task Delete_removes_only_that_staff()
    {
        using var ctx = TestHelpers.CreateContext();
        ctx.Staff.AddRange(
            TestHelpers.ExistingStaff("EMP001", "Keep", "900000001"),
            TestHelpers.ExistingStaff("EMP002", "Remove", "900000002"));
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateController(ctx).Delete("EMP002");

        Assert.Equal("EMP002", TestHelpers.OkJson(result).GetProperty("employeeId").GetString());
        Assert.Equal("EMP001", (await ctx.Staff.SingleAsync()).EmployeeId);
    }

    [Fact]
    public async Task Delete_unknown_employee_returns_404()
    {
        using var ctx = TestHelpers.CreateContext();
        Assert.IsType<NotFoundResult>(await TestHelpers.CreateController(ctx).Delete("EMP999"));
    }

    [Fact]
    public async Task Deleted_mobile_can_be_registered_again()
    {
        using var ctx = TestHelpers.CreateContext();
        ctx.Staff.Add(TestHelpers.ExistingStaff("EMP001", "Old", "9876500001"));
        await ctx.SaveChangesAsync();
        var controller = TestHelpers.CreateController(ctx);

        await controller.Delete("EMP001");

        Assert.IsType<OkObjectResult>(await controller.Create(TestHelpers.ValidCreateDto(mobile: "9876500001")));
    }
}
