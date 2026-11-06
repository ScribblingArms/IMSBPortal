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
        Assert.Equal(24000m, saved.MonthlySalary);
        Assert.Equal("hr@imsb", saved.CreatedBy);
    }

    [Fact]
    public async Task Staff_role_cannot_set_monthly_salary_on_create()
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.MonthlySalary = 99999m;

        var result = await TestHelpers.CreateController(ctx, userName: "staff@imsb", role: "Staff").Create(dto);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0m, (await ctx.Staff.SingleAsync()).MonthlySalary);
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
        await controller.Create(TestHelpers.ValidCreateDto(mobile: "9000000011", email: null));
        await controller.Create(TestHelpers.ValidCreateDto(mobile: "9000000012", email: null));

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
        var dto = TestHelpers.ValidCreateDto(mobile: "");
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

        Assert.Equal("Staff category is required", errors["department_id"]);
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

    [Fact]
    public async Task Create_keeps_only_the_hash_and_last_four_of_the_aadhaar()
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        var number = dto.Aadhaar!;
        dto.Aadhaar = $"{number[..4]}-{number[4..8]}-{number[8..]}";   // as typed in the form

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        var saved = await ctx.Staff.SingleAsync();
        Assert.Equal(Aadhaar.Hash(number), saved.AadhaarHash);
        Assert.Equal(number[^4..], saved.AadhaarLast4);
        Assert.Equal("XXXX-XXXX-" + number[^4..], saved.MaskedAadhaar);
        Assert.DoesNotContain(number, System.Text.Json.JsonSerializer.Serialize(saved));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("12345678901")]     // 11 digits
    [InlineData("1234567890123")]   // 13 digits
    [InlineData("abcd-efgh-ijkl")]
    public async Task Create_needs_a_12_digit_aadhaar(string? aadhaar)
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.Aadhaar = aadhaar;

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Enter a valid 12-digit Aadhaar number", errors["aadhaar"]);
        Assert.Empty(ctx.Staff);
    }

    [Theory]
    [InlineData("1111-1111-1111")]
    [InlineData("0123-4567-8901")]
    [InlineData("234567890123")]
    public async Task Create_accepts_any_12_digit_aadhaar(string aadhaar)
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.Aadhaar = aadhaar;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        var saved = await ctx.Staff.SingleAsync();
        Assert.Equal(Aadhaar.Hash(aadhaar), saved.AadhaarHash);
        Assert.Equal(aadhaar[^4..], saved.AadhaarLast4);
    }

    [Fact]
    public async Task Create_rejects_an_aadhaar_already_on_another_staff_member()
    {
        using var ctx = TestHelpers.CreateContext();
        var first = TestHelpers.ValidCreateDto(mobile: "9876500001", email: null);
        await TestHelpers.CreateController(ctx).Create(first);
        var second = TestHelpers.ValidCreateDto(mobile: "9876500002", email: null);
        second.Aadhaar = first.Aadhaar;

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(second));

        Assert.Equal("This Aadhaar number is already registered", errors["aadhaar"]);
        Assert.Single(ctx.Staff);
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
    public async Task Editing_staff_without_an_aadhaar_on_record_does_not_demand_one()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", DtoFor(staff)));

        Assert.Null((await ctx.Staff.SingleAsync()).AadhaarHash);
    }

    [Fact]
    public async Task Editing_with_a_blank_aadhaar_keeps_the_one_on_record()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        var number = TestHelpers.ValidAadhaar("1");
        staff.AadhaarHash = Aadhaar.Hash(number);
        staff.AadhaarLast4 = Aadhaar.Last4(number);
        await ctx.SaveChangesAsync();

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", DtoFor(staff)));

        var saved = await ctx.Staff.SingleAsync();
        Assert.Equal(Aadhaar.Hash(number), saved.AadhaarHash);
        Assert.Equal(number[^4..], saved.AadhaarLast4);
    }

    [Fact]
    public async Task Editing_can_add_or_replace_the_aadhaar_and_resubmit_its_own()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        var dto = DtoFor(staff);
        dto.Aadhaar = TestHelpers.ValidAadhaar("2");

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
        Assert.Equal(dto.Aadhaar[^4..], (await ctx.Staff.SingleAsync()).AadhaarLast4);

        // the same person sending their own number again is not a duplicate
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
    }

    [Fact]
    public async Task Editing_rejects_an_invalid_aadhaar_or_another_staff_members()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        var taken = TestHelpers.ValidAadhaar("3");
        var other = TestHelpers.ExistingStaff("EMP002", "Other", "900000002");
        other.AadhaarHash = Aadhaar.Hash(taken);
        other.AadhaarLast4 = Aadhaar.Last4(taken);
        ctx.Staff.Add(other);
        await ctx.SaveChangesAsync();
        var dto = DtoFor(staff);

        dto.Aadhaar = "12345";
        Assert.Equal("Enter a valid 12-digit Aadhaar number", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update("EMP001", dto))["aadhaar"]);

        dto.Aadhaar = taken;
        Assert.Equal("This Aadhaar number is already registered", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update("EMP001", dto))["aadhaar"]);
    }

    [Fact]
    public async Task Staff_list_shows_the_aadhaar_masked_and_never_the_hash()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        var number = TestHelpers.ValidAadhaar("4");
        staff.AadhaarHash = Aadhaar.Hash(number);
        staff.AadhaarLast4 = Aadhaar.Last4(number);
        await ctx.SaveChangesAsync();

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));

        var row = Assert.Single(json.GetProperty("staff").EnumerateArray());
        Assert.Equal("XXXX-XXXX-" + number[^4..], row.GetProperty("aadhaarMasked").GetString());
        Assert.DoesNotContain(staff.AadhaarHash!, json.GetRawText());
    }

    [Fact]
    public async Task Editing_to_an_under_18_dob_is_rejected()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        var original = staff.DateOfBirth;
        var dto = DtoFor(staff);
        dto.DateOfBirth = DateTime.Today.AddYears(-17).ToString("yyyy-MM-dd");

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update("EMP001", dto));

        Assert.Equal("Staff must be at least 18 years old", errors["date_of_birth"]);
        Assert.Equal(original, (await ctx.Staff.SingleAsync()).DateOfBirth);
    }

    [Fact]
    public async Task Update_changes_fields_and_audit_columns()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        var dto = DtoFor(staff, "ON_LEAVE");
        dto.Designation = "Senior Executive";
        dto.DepartmentId = TestHelpers.AdminDept.ToString();
        dto.Shift = "EVENING";
        dto.MonthlySalary = 31500m;

        var result = await TestHelpers.CreateController(ctx, userName: "editor@imsb").Update("EMP001", dto);

        Assert.Equal("EMP001", TestHelpers.OkJson(result).GetProperty("employeeId").GetString());
        var saved = await ctx.Staff.SingleAsync();
        Assert.Equal("Senior Executive", saved.Designation);
        Assert.Equal(TestHelpers.AdminDept, saved.DepartmentId);
        Assert.Equal("EVENING", saved.Shift);
        Assert.Equal(31500m, saved.MonthlySalary);
        Assert.Equal("ON_LEAVE", saved.Status);
        Assert.Equal("editor@imsb", saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
        Assert.Equal("EMP001", saved.EmployeeId);
    }

    [Fact]
    public async Task Staff_role_cannot_set_or_change_monthly_salary()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        staff.MonthlySalary = 18000m;
        await ctx.SaveChangesAsync();

        var dto = DtoFor(staff);
        dto.MonthlySalary = 99999m;
        var update = await TestHelpers.CreateController(ctx, userName: "staff@imsb", role: "Staff").Update("EMP001", dto);

        Assert.IsType<OkObjectResult>(update);
        Assert.Equal(18000m, (await ctx.Staff.SingleAsync()).MonthlySalary);
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
        ctx.Staff.Add(TestHelpers.ExistingStaff("EMP002", "Other", "9000000002"));
        await ctx.SaveChangesAsync();
        var dto = DtoFor(staff);
        dto.Mobile = "9000000002";

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

    // ── Shift timings ─────────────────────────────────────────────

    private static readonly string[] NewShifts =
    {
        "06:00 AM to 02:00 PM", "02:00 PM to 10:00 PM", "10:00 PM to 06:00 AM", "08:00 AM to 04:00 PM", "01:00 PM to 09:00 PM",
        "10:00 AM to 05:00 PM", "10:00 AM to 06:00 PM"
    };

    private static async Task<IMSB.Dto.Models.IMSBContext> ContextWithNewShifts()
    {
        var ctx = TestHelpers.CreateContext();
        foreach (var old in ctx.Shifts) old.DisplayOrder = 0;
        ctx.Shifts.AddRange(NewShifts.Reverse().Select((name, i) => new Shift { Code = name, Name = name, DisplayOrder = NewShifts.Length - i }));
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task Dropdown_offers_the_new_shifts_in_order_and_not_the_old_ones()
    {
        using var ctx = await ContextWithNewShifts();

        var shifts = await new IMSB.Backend.StaffDetails(ctx).ShiftsAsync();

        Assert.Equal(NewShifts, shifts.Select(s => s.Name));
    }

    [Theory]
    [InlineData("06:00 AM to 02:00 PM")]
    [InlineData("10:00 PM to 06:00 AM")]
    [InlineData("10:00 AM to 05:00 PM")]
    [InlineData("10:00 AM to 06:00 PM")]
    public async Task New_staff_can_take_a_new_shift(string shift)
    {
        using var ctx = await ContextWithNewShifts();
        var dto = TestHelpers.ValidCreateDto();
        dto.Shift = shift;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal(shift, (await ctx.Staff.SingleAsync()).Shift);
    }

    [Fact]
    public async Task New_staff_cannot_take_a_shift_no_longer_offered()
    {
        using var ctx = await ContextWithNewShifts();
        var dto = TestHelpers.ValidCreateDto();
        dto.Shift = "MORNING";

        Assert.Equal("Invalid shift", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["shift"]);
    }

    [Fact]
    public async Task Staff_on_an_old_shift_keep_it_when_edited_and_can_move_to_a_new_one()
    {
        using var ctx = await ContextWithNewShifts();
        var staff = TestHelpers.ExistingStaff("EMP001", "Older Record", "9876500009", shift: "MORNING");
        ctx.Staff.Add(staff);
        await ctx.SaveChangesAsync();
        var dto = DtoFor(staff);
        dto.Designation = "Guard";

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
        Assert.Equal("MORNING", (await ctx.Staff.AsNoTracking().SingleAsync()).Shift);

        dto.Shift = "02:00 PM to 10:00 PM";
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
        Assert.Equal("02:00 PM to 10:00 PM", (await ctx.Staff.AsNoTracking().SingleAsync()).Shift);
    }

    [Fact]
    public async Task Attendance_still_knows_the_length_of_a_shift_no_longer_offered()
    {
        using var ctx = await ContextWithNewShifts();

        var names = await new IMSB.Backend.Attendance(ctx).ShiftNamesAsync();

        Assert.Equal(6m, AttendanceRules.ShiftHours(names["EVENING"]));
        Assert.Equal(8m, AttendanceRules.ShiftHours(names["10:00 PM TO 06:00 AM"]));
    }

    // ── Staff category (the department's name) ────────────────────

    [Fact]
    public async Task Staff_category_is_the_name_of_the_chosen_department()
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DepartmentId = TestHelpers.AdminDept.ToString();

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Admin", (await ctx.Staff.SingleAsync()).StaffCategory);
    }

    [Fact]
    public async Task Changing_the_department_changes_the_staff_category()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        Assert.NotEqual("Admin", staff.StaffCategory);
        var dto = DtoFor(staff);

        dto.DepartmentId = TestHelpers.AdminDept.ToString();
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
        Assert.Equal("Admin", (await ctx.Staff.AsNoTracking().SingleAsync()).StaffCategory);

        dto.DepartmentId = TestHelpers.SalesDept.ToString();
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
        Assert.Equal("Sales", (await ctx.Staff.AsNoTracking().SingleAsync()).StaffCategory);
    }

    [Fact]
    public async Task Leave_quota_saves_the_department_as_the_category()
    {
        using var ctx = TestHelpers.CreateContext();
        var staff = TestHelpers.ExistingStaff("EMP001", "Older Record", "9876500009", dept: TestHelpers.AdminDept);
        staff.StaffCategory = "General";
        ctx.Staff.Add(staff);
        await ctx.SaveChangesAsync();
        var leaves = new IMSB.Backend.Leaves(ctx);

        await leaves.SaveEntitlementAsync(staff.Id, "Casual", 5m);
        Assert.Equal("Admin", (await ctx.LeaveEntitlements.SingleAsync()).StaffCategory);

        var stale = await ctx.LeaveEntitlements.SingleAsync();
        stale.StaffCategory = "Operators";
        await ctx.SaveChangesAsync();
        await leaves.SaveEntitlementAsync(staff.Id, "Casual", 6m);
        Assert.Equal("Admin", (await ctx.LeaveEntitlements.SingleAsync()).StaffCategory);
    }

    // ── Leave quotas captured when registering ────────────────────

    private static IMSB.Dto.Models.IMSBContext ContextWithLeaveTypes()
    {
        var ctx = TestHelpers.CreateContext();
        ctx.LeaveTypes.AddRange(new LeaveType { Name = "Casual" }, new LeaveType { Name = "Earned" }, new LeaveType { Name = "Sick" });
        ctx.SaveChanges();
        return ctx;
    }

    private static LeaveQuotaDto Quota(string type, decimal? days) => new() { LeaveType = type, AnnualQuota = days };

    private static async Task<(IMSB.Dto.Models.IMSBContext Ctx, Staff Staff)> EditableWithLeaveTypes()
    {
        var ctx = ContextWithLeaveTypes();
        var staff = TestHelpers.ExistingStaff("EMP001", "Older Record", "9876500009", dept: TestHelpers.AdminDept);
        ctx.Staff.Add(staff);
        await ctx.SaveChangesAsync();
        return (ctx, staff);
    }

    private static LeaveEntitlement StoredQuota(Staff staff, string type, decimal days, bool attendance = false) => new()
    {
        StaffId = staff.Id, StaffCategory = "Admin", LeaveType = type, Year = DateTime.Today.Year, AnnualQuota = days, IsAttendanceLeaveType = attendance
    };

    private static StaffLeaveEntry TakenLeave(Staff staff, string type, decimal days) => new()
    {
        StaffId = staff.Id, LeaveType = type, Year = DateTime.Today.Year, FromDate = DateTime.Today, ToDate = DateTime.Today, Days = days
    };

    [Fact]
    public async Task Registering_saves_each_leave_quota_with_all_its_details()
    {
        using var ctx = ContextWithLeaveTypes();
        var dto = TestHelpers.ValidCreateDto();
        dto.DepartmentId = TestHelpers.AdminDept.ToString();
        dto.LeaveQuotas = new() { Quota("Sick", 7.5m), Quota(" Casual ", 12m) };

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        var staff = await ctx.Staff.SingleAsync();
        var saved = await ctx.LeaveEntitlements.OrderBy(e => e.LeaveType).ToListAsync();
        Assert.Equal(new[] { "Casual", "Sick" }, saved.Select(e => e.LeaveType));
        Assert.Equal(new[] { 12m, 7.5m }, saved.Select(e => e.AnnualQuota));
        Assert.All(saved, e =>
        {
            Assert.Equal(staff.Id, e.StaffId);
            Assert.Equal("Admin", e.StaffCategory);
            Assert.Equal(DateTime.Today.Year, e.Year);
        });
    }

    [Fact]
    public async Task Registered_quotas_appear_in_the_leave_balances()
    {
        using var ctx = ContextWithLeaveTypes();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = new() { Quota("Casual", 12m) };

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        var row = Assert.Single(await new IMSB.Backend.Leaves(ctx).BalancesAsync(DateTime.Today.Year));
        Assert.Equal("Casual", row.LeaveType);
        Assert.Equal(12m, row.Entitlement);
        Assert.Equal(12m, row.Balance);
    }

    [Fact]
    public async Task Attendance_leave_draws_on_the_first_type_with_days()
    {
        using var ctx = ContextWithLeaveTypes();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = new() { Quota("Sick", 6m), Quota("Casual", 0m), Quota("Earned", 10m) };

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Earned", (await ctx.LeaveEntitlements.SingleAsync(e => e.IsAttendanceLeaveType)).LeaveType);
    }

    [Fact]
    public async Task Registering_without_quotas_saves_none()
    {
        using var ctx = ContextWithLeaveTypes();

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto()));

        Assert.Single(ctx.Staff);
        Assert.Empty(ctx.LeaveEntitlements);
    }

    public static TheoryData<string, decimal, string?> InvalidQuotas => new()
    {
        { "Casual", -1m, null },
        { "Casual", 366.5m, null },
        { "Casual", 1.25m, null },
        { "Sabbatical", 5m, null },
        { "Casual", 5m, "casual" },
    };

    [Theory]
    [MemberData(nameof(InvalidQuotas))]
    public async Task Invalid_quota_rejects_the_registration_and_saves_nothing(string type, decimal days, string? duplicate)
    {
        using var ctx = ContextWithLeaveTypes();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = new() { Quota(type, days) };
        if (duplicate != null) dto.LeaveQuotas.Add(Quota(duplicate, days));

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.True(errors.ContainsKey("leave_quotas"));
        Assert.Empty(ctx.Staff);
        Assert.Empty(ctx.LeaveEntitlements);
    }

    [Fact]
    public async Task Registering_cannot_remove_a_quota()
    {
        using var ctx = ContextWithLeaveTypes();
        var dto = TestHelpers.ValidCreateDto();
        dto.LeaveQuotas = new() { Quota("Casual", null) };

        Assert.True(TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto)).ContainsKey("leave_quotas"));
        Assert.Empty(ctx.Staff);
    }

    [Fact]
    public async Task Editing_adds_quotas_missed_at_registration_with_all_their_details()
    {
        var (ctx, staff) = await EditableWithLeaveTypes();
        using var _ = ctx;
        var dto = DtoFor(staff);
        dto.LeaveQuotas = new() { Quota("Casual", 0m), Quota("Sick", 7.5m) };

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));

        var saved = await ctx.LeaveEntitlements.OrderBy(e => e.LeaveType).ToListAsync();
        Assert.Equal(new[] { "Casual", "Sick" }, saved.Select(e => e.LeaveType));
        Assert.Equal(new[] { 0m, 7.5m }, saved.Select(e => e.AnnualQuota));
        Assert.All(saved, e =>
        {
            Assert.Equal(staff.Id, e.StaffId);
            Assert.Equal("Admin", e.StaffCategory);
            Assert.Equal(DateTime.Today.Year, e.Year);
        });
        Assert.Equal("Sick", saved.Single(e => e.IsAttendanceLeaveType).LeaveType);   // the one with days to draw on
    }

    [Fact]
    public async Task Editing_only_changes_the_listed_quotas()
    {
        var (ctx, staff) = await EditableWithLeaveTypes();
        using var _ = ctx;
        ctx.LeaveEntitlements.AddRange(StoredQuota(staff, "Casual", 5m, attendance: true), StoredQuota(staff, "Sick", 6m));
        await ctx.SaveChangesAsync();
        var dto = DtoFor(staff);
        dto.LeaveQuotas = new() { Quota("Sick", 8m) };

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));

        var saved = await ctx.LeaveEntitlements.AsNoTracking().OrderBy(e => e.LeaveType).ToListAsync();
        Assert.Equal(new[] { 5m, 8m }, saved.Select(e => e.AnnualQuota));
        Assert.True(saved[0].IsAttendanceLeaveType);
    }

    [Fact]
    public async Task Editing_with_a_quota_below_leave_taken_saves_nothing_and_names_the_type()
    {
        var (ctx, staff) = await EditableWithLeaveTypes();
        using var _ = ctx;
        ctx.LeaveEntitlements.Add(StoredQuota(staff, "Casual", 5m, attendance: true));
        ctx.StaffLeaveEntries.Add(TakenLeave(staff, "Casual", 3m));
        await ctx.SaveChangesAsync();
        var dto = DtoFor(staff);
        dto.FullName = "Changed Name";
        dto.LeaveQuotas = new() { Quota("Casual", 2m) };

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update("EMP001", dto));

        Assert.StartsWith("Casual: ", errors["leave_quotas"]);
        Assert.Equal(5m, (await ctx.LeaveEntitlements.AsNoTracking().SingleAsync()).AnnualQuota);
        Assert.Equal("Older Record", (await ctx.Staff.AsNoTracking().SingleAsync()).FullName);
    }

    [Fact]
    public async Task Editing_rejects_an_unknown_leave_type()
    {
        var (ctx, staff) = await EditableWithLeaveTypes();
        using var _ = ctx;
        var dto = DtoFor(staff);
        dto.LeaveQuotas = new() { Quota("Sabbatical", 5m) };

        Assert.True(TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update("EMP001", dto)).ContainsKey("leave_quotas"));
        Assert.Empty(ctx.LeaveEntitlements);
    }

    [Fact]
    public async Task Clearing_an_unused_quota_removes_it_and_moves_the_attendance_policy()
    {
        var (ctx, staff) = await EditableWithLeaveTypes();
        using var _ = ctx;
        ctx.LeaveEntitlements.AddRange(StoredQuota(staff, "Casual", 5m, attendance: true), StoredQuota(staff, "Sick", 6m));
        await ctx.SaveChangesAsync();
        var dto = DtoFor(staff);
        dto.LeaveQuotas = new() { Quota("Casual", null) };

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));

        var left = await ctx.LeaveEntitlements.AsNoTracking().SingleAsync();
        Assert.Equal("Sick", left.LeaveType);
        Assert.True(left.IsAttendanceLeaveType);
    }

    [Fact]
    public async Task Clearing_a_quota_with_leave_taken_is_refused()
    {
        var (ctx, staff) = await EditableWithLeaveTypes();
        using var _ = ctx;
        ctx.LeaveEntitlements.Add(StoredQuota(staff, "Casual", 5m, attendance: true));
        ctx.StaffLeaveEntries.Add(TakenLeave(staff, "Casual", 1m));
        await ctx.SaveChangesAsync();
        var dto = DtoFor(staff);
        dto.LeaveQuotas = new() { Quota("Casual", null) };

        Assert.StartsWith("Casual: ", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update("EMP001", dto))["leave_quotas"]);
        Assert.Single(ctx.LeaveEntitlements);
    }

    [Fact]
    public async Task Giving_days_to_another_type_moves_the_policy_off_a_type_with_none()
    {
        var (ctx, staff) = await EditableWithLeaveTypes();
        using var _ = ctx;
        ctx.LeaveEntitlements.Add(StoredQuota(staff, "Casual", 0m, attendance: true));
        var unpaid = new AttendanceRecord { StaffId = staff.Id, AttendanceDate = DateTime.Today, Status = AttendanceStatuses.Leave, LeaveWithoutPay = true };
        ctx.AttendanceRecords.Add(unpaid);
        await ctx.SaveChangesAsync();
        var dto = DtoFor(staff);
        dto.LeaveQuotas = new() { Quota("Earned", 10m) };

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));

        Assert.Equal("Earned", (await ctx.LeaveEntitlements.AsNoTracking().SingleAsync(e => e.IsAttendanceLeaveType)).LeaveType);
        Assert.False((await ctx.AttendanceRecords.AsNoTracking().SingleAsync()).LeaveWithoutPay);
    }

    [Fact]
    public async Task Edit_form_reads_quota_taken_and_balance()
    {
        var (ctx, staff) = await EditableWithLeaveTypes();
        using var _ = ctx;
        ctx.LeaveEntitlements.Add(StoredQuota(staff, "Casual", 5m, attendance: true));
        ctx.StaffLeaveEntries.Add(TakenLeave(staff, "Casual", 2m));
        await ctx.SaveChangesAsync();
        var controller = TestHelpers.CreateController(ctx);

        var row = TestHelpers.OkJson(await controller.LeaveQuotas("EMP001")).GetProperty("rows")[0];

        Assert.Equal("Casual", row.GetProperty("leaveType").GetString());
        Assert.Equal(5m, row.GetProperty("entitlement").GetDecimal());
        Assert.Equal(2m, row.GetProperty("taken").GetDecimal());
        Assert.Equal(3m, row.GetProperty("balance").GetDecimal());
        Assert.IsType<NotFoundResult>(await controller.LeaveQuotas("EMP999"));
    }

    // ── Employee type (separate from the staff category) ──────────

    private static string ColumnOf(string property) =>
        typeof(Staff).GetProperty(property)!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.ColumnAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>().Single().Name!;

    [Fact]
    public void Staff_table_keeps_DepartmentId_and_has_its_own_EmployeeType_column()
    {
        Assert.Equal("DepartmentId", ColumnOf(nameof(Staff.DepartmentId)));
        Assert.Equal("EmployeeType", ColumnOf(nameof(Staff.EmployeeType)));
    }

    [Theory]
    [InlineData("Department", "Department")]
    [InlineData("Agency", "Agency")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public async Task Employee_type_is_optional_and_saved_beside_the_category(string? sent, string? saved)
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.EmployeeType = sent;

        var controller = TestHelpers.CreateController(ctx);
        TestHelpers.BindModelState(controller, dto);
        Assert.IsType<OkObjectResult>(await controller.Create(dto));

        var staff = await ctx.Staff.SingleAsync();
        Assert.Equal(saved, staff.EmployeeType);
        Assert.Equal(Guid.Parse(dto.DepartmentId), staff.DepartmentId);
    }

    [Fact]
    public async Task Unknown_employee_type_is_refused()
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.EmployeeType = "Permanent";

        var controller = TestHelpers.CreateController(ctx);
        TestHelpers.BindModelState(controller, dto);
        var errors = TestHelpers.Errors(await controller.Create(dto));

        Assert.Equal("ValEmployeeType", errors["employee_type"]);
        Assert.Empty(ctx.Staff);
    }

    [Fact]
    public async Task Employee_type_can_be_changed_and_cleared_and_is_returned_to_the_form()
    {
        var (ctx, staff) = await SeedOne();
        using var _ = ctx;
        var dto = DtoFor(staff);

        dto.EmployeeType = "Agency";
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
        Assert.Equal("Agency", (await ctx.Staff.AsNoTracking().SingleAsync()).EmployeeType);
        var row = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null)).GetProperty("staff")[0];
        Assert.Equal("Agency", row.GetProperty("employeeType").GetString());

        dto.EmployeeType = null;
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
        Assert.Null((await ctx.Staff.AsNoTracking().SingleAsync()).EmployeeType);
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
