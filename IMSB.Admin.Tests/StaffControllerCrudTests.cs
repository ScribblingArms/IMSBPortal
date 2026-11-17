using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

/// <summary>Registering staff on the Staff page saves a SERP.tbl_Employee row.</summary>
public class StaffControllerCreateTests
{
    [Fact]
    public async Task Create_valid_staff_persists_an_employee()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var controller = TestHelpers.CreateController(ctx);
        var dto = TestHelpers.ValidCreateDto();
        dto.StallNumber = 12;
        TestHelpers.BindModelState(controller, dto);

        var json = TestHelpers.OkJson(await controller.Create(dto));

        var saved = await ctx.Employees.SingleAsync();
        Assert.Equal(json.GetProperty("employeeId").GetInt32(), saved.EmpId);
        Assert.Equal("Meena Kumari", saved.EmpName);
        Assert.Equal("9876500001", saved.Mobile);
        Assert.Equal("Female", saved.Gender);
        Assert.Equal(TestHelpers.SalesDept, saved.DepartmentId);
        Assert.Equal(TestHelpers.OfficeType, saved.EmpTypeId);   // the chosen tbl_EMP_Types row
        Assert.Equal(TestHelpers.AgencyA, saved.AgencyId);
        Assert.Equal(TestHelpers.MorningShift, saved.ShiftId);
        Assert.Equal(12, saved.StallNum);
        Assert.Equal(1, saved.IsActive);
        Assert.NotNull(saved.CreateOn);
        Assert.Null(saved.ModifiedOn);
        // the other pages see the employee through the Staff view, under the EMP_ID
        var viewed = Assert.Single(ctx.Staff.AsNoTracking());
        Assert.Equal((saved.EmpId, saved.EmpId.ToString(), "Meena Kumari", "Sales Executive", "Sales", "ACTIVE"),
            (viewed.Id, viewed.EmployeeId, viewed.FullName, viewed.Designation, viewed.StaffCategory, viewed.Status));
    }

    [Fact]
    public async Task Create_trims_the_name_and_leaves_the_optional_fields_empty()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.FullName = "  Meena Kumari  ";
        dto.AgencyId = "";
        dto.ShiftId = null;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));

        var saved = await ctx.Employees.SingleAsync();
        Assert.Equal("Meena Kumari", saved.EmpName);
        Assert.Null(saved.AgencyId);
        Assert.Null(saved.ShiftId);
        Assert.Null(saved.StallNum);
    }

    [Fact]
    public async Task Consecutive_creates_get_different_ids()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var a = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto("9876500001")));
        var b = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto("9876500002")));

        Assert.NotEqual(a.GetProperty("employeeId").GetInt32(), b.GetProperty("employeeId").GetInt32());
        Assert.Equal(2, await ctx.Employees.CountAsync());
    }

    [Fact]
    public async Task Null_body_is_bad_request()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        Assert.IsType<BadRequestResult>(await TestHelpers.CreateController(ctx).Create(null));
    }

    [Fact]
    public async Task Model_state_errors_are_returned_with_form_field_names()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var controller = TestHelpers.CreateController(ctx);
        var dto = TestHelpers.ValidCreateDto();
        dto.FullName = "A";
        dto.StallNumber = 0;
        TestHelpers.BindModelState(controller, dto);

        var errors = TestHelpers.Errors(await controller.Create(dto));

        // outside MVC the DataAnnotations localizer does not run, so the resource keys come back as they are
        Assert.Equal("ValFullName", errors["full_name"]);
        Assert.Equal("ValStallNumber", errors["stall_number"]);
        Assert.Empty(ctx.Employees);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123456")]   // not a tbl_Designation row
    public async Task Unknown_department_is_rejected(string departmentId)
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DepartmentId = departmentId;

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Staff category is required", errors["department_id"]);
    }

    [Fact]
    public async Task Unknown_agency_is_rejected()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.AgencyId = "99";

        Assert.Equal("Select a valid agency", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["agency_id"]);
    }

    [Fact]
    public async Task Shift_must_be_an_offered_dbo_shift()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();

        dto.ShiftId = TestHelpers.HousekeepingShift.ToString();   // a SERP.tbl_Shifts id, not a dbo.Shift row
        Assert.Equal("Invalid shift", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["shift"]);

        dto.ShiftId = TestHelpers.RetiredShift.ToString();        // a dbo.Shift row no longer offered
        Assert.Equal("Invalid shift", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["shift"]);

        dto.ShiftId = TestHelpers.EveningShift.ToString();
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));
        Assert.Equal(TestHelpers.EveningShift, (await ctx.Employees.SingleAsync()).ShiftId);   // tbl_Employee.ShiftID = dbo.Shift.Id
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("77")]   // not a tbl_EMP_Types row
    public async Task Unknown_employee_type_is_rejected(string typeId)
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.EmployeeTypeId = typeId;

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Select a valid employee type", errors["employee_type_id"]);
        Assert.False(errors.ContainsKey("department_id"));   // the type error is enough
        Assert.False(errors.ContainsKey("shift"));
        Assert.Empty(ctx.Employees);
    }

    [Fact]
    public async Task Staff_category_must_be_a_dbo_departments_row()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.EmployeeDesignations.Add(new EmployeeDesignation { DesigId = 9100, Name = "Only In SERP" });   // tbl_Designation is not used
        await ctx.SaveChangesAsync();
        var dto = TestHelpers.ValidCreateDto();

        dto.DepartmentId = "9100";
        Assert.Equal("Staff category is required", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["department_id"]);

        dto.DepartmentId = TestHelpers.HousekeepingDept.ToString();
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));
        Assert.Equal(TestHelpers.HousekeepingDept, (await ctx.Employees.SingleAsync()).DepartmentId);   // tbl_Employee.DepartmentID = departments.Id
    }

    [Fact]
    public async Task Any_staff_category_and_shift_go_with_any_employee_type()
    {
        // dbo.departments and dbo.Shift have no employee type, so nothing ties them to tbl_EMP_Types
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.EmployeeTypeId = TestHelpers.HousekeepingType.ToString();
        dto.DepartmentId = TestHelpers.SalesDept.ToString();
        dto.ShiftId = TestHelpers.MorningShift.ToString();

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));
        var saved = await ctx.Employees.SingleAsync();
        Assert.Equal(TestHelpers.HousekeepingType, saved.EmpTypeId);
        Assert.Equal(TestHelpers.SalesDept, saved.DepartmentId);
        Assert.Equal(TestHelpers.MorningShift, saved.ShiftId);
    }

    [Theory]
    [InlineData("Female")]
    [InlineData("Male")]
    [InlineData("Other")]
    public async Task Known_genders_are_accepted(string gender)
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.Gender = gender;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(dto));
        Assert.Equal(gender, (await ctx.Employees.SingleAsync()).Gender);
    }

    [Fact]
    public async Task Unknown_gender_is_rejected()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.Gender = "Unknown";

        Assert.Equal("Select a gender", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["gender"]);
    }

    [Fact]
    public async Task Duplicate_mobile_is_rejected()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));
        await ctx.SaveChangesAsync();

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto("9876500001")));

        Assert.Equal("Mobile already registered", errors["mobile"]);
    }

    [Fact]
    public async Task Create_saves_the_aadhaar_digits_that_serp_expects()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.Aadhaar = "2341-2341-2346";

        await TestHelpers.CreateController(ctx).Create(dto);

        Assert.Equal("234123412346", (await ctx.Employees.SingleAsync()).Aadhar);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]
    public async Task Create_needs_a_12_digit_aadhaar(string? aadhaar)
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.Aadhaar = aadhaar;

        Assert.Equal("Enter a valid 12-digit Aadhaar number", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["aadhaar"]);
        Assert.Empty(ctx.Employees);
    }

    [Fact]
    public async Task Create_rejects_an_aadhaar_already_on_another_employee()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Someone", "9876500009", aadhaar: "234123412346"));
        await ctx.SaveChangesAsync();
        var dto = TestHelpers.ValidCreateDto();
        dto.Aadhaar = "2341 2341 2346";

        Assert.Equal("This Aadhaar number is already registered", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["aadhaar"]);
    }
}

public class StaffControllerUpdateTests
{
    private static async Task<IMSBContext> ContextWith(Employee employee)
    {
        var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static UpdateStaffDto DtoFor(Employee e, string? status = null) => new()
    {
        FullName = e.EmpName!, Mobile = e.Mobile!, Gender = e.Gender!, EmployeeTypeId = e.EmpTypeId?.ToString() ?? "",
        DepartmentId = e.DepartmentId?.ToString(),
        AgencyId = e.AgencyId?.ToString(), ShiftId = e.ShiftId?.ToString(), StallNumber = e.StallNum, Status = status
    };

    [Fact]
    public async Task Update_changes_fields_and_modified_on()
    {
        using var ctx = await ContextWith(TestHelpers.ExistingEmployee(501, "Old Name", "9876500001", aadhaar: "234123412346"));
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.FullName = "New Name";
        dto.DepartmentId = TestHelpers.AdminDept.ToString();
        dto.AgencyId = TestHelpers.AgencyB.ToString();
        dto.ShiftId = TestHelpers.EveningShift.ToString();
        dto.StallNumber = 7;

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Update(501, dto));

        Assert.Equal(501, json.GetProperty("employeeId").GetInt32());
        var saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Equal("New Name", saved.EmpName);
        Assert.Equal(TestHelpers.AdminDept, saved.DepartmentId);
        Assert.Equal(TestHelpers.AgencyB, saved.AgencyId);
        Assert.Equal(TestHelpers.EveningShift, saved.ShiftId);
        Assert.Equal(7, saved.StallNum);
        Assert.NotNull(saved.ModifiedOn);
        Assert.Equal("234123412346", saved.Aadhar);   // a blank Aadhaar keeps the one on record
    }

    [Fact]
    public async Task Employee_type_staff_category_and_shift_can_each_be_changed()
    {
        using var ctx = await ContextWith(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());

        dto.EmployeeTypeId = TestHelpers.HousekeepingType.ToString();   // the category and shift may stay as they are
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        dto.DepartmentId = TestHelpers.HousekeepingDept.ToString();
        dto.ShiftId = TestHelpers.EveningShift.ToString();
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        var saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Equal(TestHelpers.HousekeepingType, saved.EmpTypeId);
        Assert.Equal(TestHelpers.HousekeepingDept, saved.DepartmentId);
        Assert.Equal(TestHelpers.EveningShift, saved.ShiftId);
    }

    [Fact]
    public async Task A_serp_record_with_tbl_designation_and_tbl_shifts_ids_can_be_saved_unchanged()
    {
        // registered by the SERP app: Desig / ShiftID hold SERP ids that dbo.departments / dbo.Shift do not have,
        // and the type is no longer in tbl_EMP_Types
        var legacy = TestHelpers.ExistingEmployee(501, "Older Record", "9876500001", dept: 9200, shift: TestHelpers.HousekeepingShift, type: 99);
        using var ctx = await ContextWith(legacy);
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.FullName = "Renamed";

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        var saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Equal((99, 9200, TestHelpers.HousekeepingShift), (saved.EmpTypeId!.Value, saved.DepartmentId!.Value, saved.ShiftId!.Value));

        // but it cannot be moved to another id that is not a dbo row
        dto.DepartmentId = "9201";
        Assert.Equal("Staff category is required", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto))["department_id"]);
    }

    [Fact]
    public async Task An_employee_on_a_retired_shift_keeps_it_and_can_move_to_an_offered_one()
    {
        using var ctx = await ContextWith(TestHelpers.ExistingEmployee(501, "Night Worker", "9876500001", shift: TestHelpers.RetiredShift));
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        Assert.Equal(TestHelpers.RetiredShift, (await ctx.Employees.AsNoTracking().SingleAsync()).ShiftId);

        dto.ShiftId = TestHelpers.MorningShift.ToString();
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        Assert.Equal(TestHelpers.MorningShift, (await ctx.Employees.AsNoTracking().SingleAsync()).ShiftId);

        dto.ShiftId = TestHelpers.RetiredShift.ToString();   // once moved off, it cannot go back
        Assert.Equal("Invalid shift", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto))["shift"]);
    }

    [Fact]
    public async Task Status_inactive_with_remarks_is_saved_and_can_be_reversed()
    {
        using var ctx = await ContextWith(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync(), EmployeeStatuses.Inactive);
        dto.Remarks = "  Left the agency  ";

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        var saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Equal(0, saved.IsActive);
        Assert.Equal("Left the agency", saved.Remarks);

        dto.Status = EmployeeStatuses.Active;
        dto.Remarks = "";
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Equal(1, saved.IsActive);
        Assert.Null(saved.Remarks);
    }

    [Fact]
    public async Task Update_without_status_keeps_current_status()
    {
        using var ctx = await ContextWith(TestHelpers.ExistingEmployee(501, "Someone", "9876500001", active: false));

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, DtoFor(await ctx.Employees.AsNoTracking().SingleAsync())));

        Assert.Equal(0, (await ctx.Employees.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Editing_can_replace_the_aadhaar_but_not_with_another_employees()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.AddRange(
            TestHelpers.ExistingEmployee(501, "First", "9876500001", aadhaar: "234123412346"),
            TestHelpers.ExistingEmployee(502, "Second", "9876500002", aadhaar: "999988887777"));
        await ctx.SaveChangesAsync();
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == 501));

        dto.Aadhaar = "9999-8888-7777";
        Assert.Equal("This Aadhaar number is already registered", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto))["aadhaar"]);

        dto.Aadhaar = "234123412346";   // its own number is not a duplicate
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        dto.Aadhaar = "1111-2222-3333";
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        Assert.Equal("111122223333", (await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == 501)).Aadhar);
    }

    [Fact]
    public async Task Own_mobile_is_not_a_duplicate_but_another_employees_is()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.AddRange(
            TestHelpers.ExistingEmployee(501, "First", "9876500001"),
            TestHelpers.ExistingEmployee(502, "Second", "9876500002"));
        await ctx.SaveChangesAsync();
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == 501));

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        dto.Mobile = "9876500002";
        Assert.Equal("Mobile already registered", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto))["mobile"]);
    }

    [Fact]
    public async Task Values_saved_by_the_serp_app_can_be_kept_unchanged()
    {
        // e.g. a SERP tbl_Shifts id and a short gender code, as registered by the SERP attendance app
        var legacy = TestHelpers.ExistingEmployee(501, "Older Record", "900000001", shift: TestHelpers.HousekeepingShift, gender: "F");
        using var ctx = await ContextWith(legacy);
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        dto.ShiftId = TestHelpers.HousekeepingShift.ToString();
        dto.Gender = "X";
        dto.Mobile = "900000002";
        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto));
        Assert.Equal("Select a gender", errors["gender"]);
        Assert.Equal("Enter a valid 10-digit mobile number", errors["mobile"]);
    }

    [Fact]
    public async Task Changing_to_a_shift_that_is_not_in_dbo_shift_is_rejected()
    {
        using var ctx = await ContextWith(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));
        var dto = DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.ShiftId = TestHelpers.HousekeepingShift.ToString();

        Assert.Equal("Invalid shift", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto))["shift"]);
    }

    [Fact]
    public async Task Biometric_data_is_left_alone()
    {
        var e = TestHelpers.ExistingEmployee(501, "Someone", "9876500001");
        e.ImageBase64 = "photo";
        e.Template = "finger";
        using var ctx = await ContextWith(e);

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, DtoFor(await ctx.Employees.AsNoTracking().SingleAsync())));

        var saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Equal("photo", saved.ImageBase64);
        Assert.Equal("finger", saved.Template);
    }

    [Fact]
    public async Task Update_unknown_employee_returns_404()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        Assert.IsType<NotFoundResult>(await TestHelpers.CreateController(ctx).Update(999, TestHelpers.ToUpdate(TestHelpers.ValidCreateDto())));
    }

    [Fact]
    public async Task Update_null_body_is_bad_request()
    {
        using var ctx = await ContextWith(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));
        Assert.IsType<BadRequestResult>(await TestHelpers.CreateController(ctx).Update(501, null));
    }
}

public class StaffControllerDeleteTests
{
    [Fact]
    public async Task Delete_removes_only_that_employee()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.AddRange(
            TestHelpers.ExistingEmployee(501, "First", "9876500001"),
            TestHelpers.ExistingEmployee(502, "Second", "9876500002"));
        await ctx.SaveChangesAsync();

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).Delete(501));

        Assert.Equal(501, json.GetProperty("employeeId").GetInt32());
        Assert.Equal(502, (await ctx.Employees.SingleAsync()).EmpId);
    }

    [Fact]
    public async Task Delete_unknown_employee_returns_404()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        Assert.IsType<NotFoundResult>(await TestHelpers.CreateController(ctx).Delete(999));
    }

    [Fact]
    public async Task Deleted_mobile_can_be_registered_again()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "First", "9876500001"));
        await ctx.SaveChangesAsync();

        await TestHelpers.CreateController(ctx).Delete(501);

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Create(TestHelpers.ValidCreateDto("9876500001")));
    }
}

public class ShiftLengthTests
{
    [Fact]
    public async Task Attendance_still_knows_the_length_of_a_shift_no_longer_offered()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        foreach (var old in ctx.Shifts) old.DisplayOrder = 0;
        ctx.Shifts.Add(new Shift { Code = "10:00 PM to 06:00 AM", Name = "10:00 PM to 06:00 AM", DisplayOrder = 1 });
        await ctx.SaveChangesAsync();

        var names = await new IMSB.Backend.Attendance(ctx).ShiftNamesAsync();

        Assert.Equal(6m, AttendanceRules.ShiftHours(names["EVENING"]));
        Assert.Equal(8m, AttendanceRules.ShiftHours(names["10:00 PM TO 06:00 AM"]));
    }
}
