using IMSB.Backend;
using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

/// <summary>The SERP.tbl_Employee entity's computed values.</summary>
public class EmployeeEntityTests
{
    [Theory]
    [InlineData(1, true, "ACTIVE")]
    [InlineData(0, false, "INACTIVE")]
    [InlineData(null, false, "INACTIVE")]   // SERP rows may leave IsActive empty
    [InlineData(2, false, "INACTIVE")]
    public void Status_comes_from_is_active(int? isActive, bool active, string status)
    {
        var e = new Employee { IsActive = isActive };
        Assert.Equal(active, e.Active);
        Assert.Equal(status, e.Status);
    }

    [Theory]
    [InlineData("234123412346", "XXXX-XXXX-2346")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("123", null)]
    public void Aadhaar_is_shown_masked(string? aadhar, string? masked) =>
        Assert.Equal(masked, new Employee { Aadhar = aadhar }.MaskedAadhaar);

    /// <summary>
    /// The SQL Server design-time model: the in-memory provider drops relational settings (column names, migrations).
    /// Building it needs no database, so the connection string is never used.
    /// </summary>
    private static Microsoft.EntityFrameworkCore.Metadata.IModel SqlServerModel()
    {
        using var ctx = new IMSBContext(new DbContextOptionsBuilder<IMSBContext>().UseSqlServer("Server=unused;Database=unused").Options);
        return Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(ctx).Model;
    }

    [Fact]
    public void Entities_map_to_the_serp_tables_and_are_left_out_of_migrations()
    {
        var model = SqlServerModel();
        var expected = new Dictionary<Type, string>
        {
            [typeof(Employee)] = "tbl_Employee",
            [typeof(EmployeeDesignation)] = "tbl_Designation",
            [typeof(ArtisansTypeAgency)] = "tbl_ArtisansTypeAgency",
            [typeof(EmployeeTypeMaster)] = "tbl_EMP_Types",
            [typeof(EmployeeShift)] = "tbl_Shifts",
        };
        foreach (var (type, table) in expected)
        {
            var entity = model.FindEntityType(type)!;
            Assert.Equal(table, entity.GetTableName());
            Assert.Equal("SERP", entity.GetSchema());
            Assert.True(entity.IsTableExcludedFromMigrations(), $"{table} must not be created by EF");
        }
    }

    [Fact]
    public void Employee_columns_keep_their_serp_names()
    {
        var entity = SqlServerModel().FindEntityType(typeof(Employee))!;
        var columns = entity.GetProperties().ToDictionary(p => p.Name, p => p.GetColumnName());

        Assert.Equal("EMP_ID", columns[nameof(Employee.EmpId)]);
        Assert.Equal("EMP_Name", columns[nameof(Employee.EmpName)]);
        Assert.Equal("Aadhar", columns[nameof(Employee.Aadhar)]);
        Assert.Equal("Desig", columns[nameof(Employee.DesigId)]);
        Assert.Equal("AgencyID", columns[nameof(Employee.AgencyId)]);
        Assert.Equal("ShiftID", columns[nameof(Employee.ShiftId)]);
        Assert.Equal("StallNum", columns[nameof(Employee.StallNum)]);
        Assert.DoesNotContain(nameof(Employee.Status), columns.Keys);        // computed, not stored
        Assert.DoesNotContain(nameof(Employee.MaskedAadhaar), columns.Keys);
    }
}

/// <summary>StaffMapper: form to employee, and employee to directory row.</summary>
public class StaffMapperTests
{
    private static EmployeeLookups Lookups()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        return new StaffDetails(ctx).LookupsAsync().GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData(" 7 ", 7)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("abc", null)]
    public void ParseId_reads_an_int_or_nothing(string? value, int? expected) => Assert.Equal(expected, StaffMapper.ParseId(value));

    [Fact]
    public void ApplyTo_copies_the_form_onto_the_employee()
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.FullName = "  Meena Kumari ";
        dto.Mobile = " 9876500001 ";
        dto.Aadhaar = "2341-2341-2346";
        dto.Gender = " Male ";
        dto.StallNumber = 4;
        var e = new Employee();

        dto.ApplyTo(e);

        Assert.Equal("Meena Kumari", e.EmpName);
        Assert.Equal("9876500001", e.Mobile);
        Assert.Equal("234123412346", e.Aadhar);
        Assert.Equal("Male", e.Gender);
        Assert.Equal(TestHelpers.OfficeType, e.EmpTypeId);   // tbl_EMP_Types
        Assert.Equal(TestHelpers.SalesDept, e.DepartmentId);
        Assert.Equal(TestHelpers.AgencyA, e.AgencyId);
        Assert.Equal(TestHelpers.MorningShift, e.ShiftId);
        Assert.Equal(4, e.StallNum);
    }

    [Fact]
    public void ApplyTo_keeps_the_aadhaar_on_record_when_none_is_typed_and_clears_empty_choices()
    {
        var e = new Employee { Aadhar = "234123412346", AgencyId = TestHelpers.AgencyB, ShiftId = TestHelpers.EveningShift, StallNum = 9 };
        var dto = TestHelpers.ValidCreateDto();
        dto.Aadhaar = "  ";
        dto.AgencyId = "";
        dto.ShiftId = null;
        dto.StallNumber = null;

        dto.ApplyTo(e);

        Assert.Equal("234123412346", e.Aadhar);
        Assert.Null(e.AgencyId);
        Assert.Null(e.ShiftId);
        Assert.Null(e.StallNum);
    }

    [Fact]
    public void ApplyTo_leaves_status_and_biometrics_alone()
    {
        var e = new Employee { IsActive = 0, ImageBase64 = "photo", Template = "finger", Remarks = "kept" };
        TestHelpers.ValidCreateDto().ApplyTo(e);

        Assert.Equal(0, e.IsActive);
        Assert.Equal("photo", e.ImageBase64);
        Assert.Equal("finger", e.Template);
        Assert.Equal("kept", e.Remarks);
    }

    [Fact]
    public void ToListItem_resolves_the_master_names()
    {
        var e = TestHelpers.ExistingEmployee(501, "Lakshmi Devi", "9876543212", dept: TestHelpers.HousekeepingDept,
            shift: TestHelpers.RetiredShift, agency: TestHelpers.AgencyB, aadhaar: "234123412346");
        e.StallNum = 3;
        e.Remarks = "note";

        var row = e.ToListItem(Lookups());

        // the category name comes from dbo.departments, the shift name from dbo.Shift (even one no longer offered)

        Assert.Equal(501, row.EmployeeId);
        Assert.Equal("Lakshmi Devi", row.FullName);
        Assert.Equal(TestHelpers.HousekeepingDept.ToString(), row.DepartmentId);
        Assert.Equal("Cleaner", row.DepartmentName);
        Assert.Equal(TestHelpers.HousekeepingType.ToString(), row.EmployeeTypeId);
        Assert.Equal("Housekeeping", row.EmployeeTypeName);
        Assert.Equal(TestHelpers.AgencyB.ToString(), row.AgencyId);
        Assert.Equal("Agency B", row.AgencyName);
        Assert.Equal(TestHelpers.RetiredShift.ToString(), row.ShiftId);
        Assert.Equal("10:00 PM to 06:00 AM", row.ShiftName);
        Assert.Equal(3, row.StallNumber);
        Assert.Equal("ACTIVE", row.Status);
        Assert.Equal("XXXX-XXXX-2346", row.AadhaarMasked);
        Assert.Equal("note", row.Remarks);
    }

    [Fact]
    public void ToListItem_copes_with_ids_missing_from_the_masters_and_empty_columns()
    {
        // e.g. a SERP record whose Desig / ShiftID are tbl_Designation / tbl_Shifts ids, unknown to dbo.departments / dbo.Shift
        var e = new Employee { EmpId = 9, DepartmentId = 4242, DesigId = 4243, AgencyId = 77, ShiftId = TestHelpers.HousekeepingShift, EmpTypeId = 66 };

        var row = e.ToListItem(Lookups());

        Assert.Equal("", row.FullName);
        Assert.Equal("", row.Mobile);
        Assert.Equal("", row.Gender);
        Assert.Equal("", row.DepartmentName);
        Assert.Null(row.EmployeeTypeName);
        Assert.Null(row.AgencyName);
        Assert.Null(row.ShiftName);
        Assert.Equal("INACTIVE", row.Status);
        Assert.Null(row.AadhaarMasked);
    }

    [Fact]
    public void Shift_option_is_the_dbo_shift_id_and_name()
    {
        Assert.Equal(new StaffShiftOption("11", "Morning (8AM-4PM)"), new Shift { Id = 11, Code = "MORNING", Name = "Morning (8AM-4PM)" }.ToStaffOption());
        Assert.Equal("NIGHT", new Shift { Id = 14, Code = "NIGHT", Name = "" }.ToStaffOption().Name);   // no name: the code is shown
    }

    [Fact]
    public void Lookups_resolve_names_from_dbo_departments_and_dbo_shift()
    {
        var lookups = Lookups();

        Assert.Equal("Sales", lookups.DepartmentName(TestHelpers.SalesDept));
        Assert.Equal("Cleaner", lookups.DepartmentName(TestHelpers.HousekeepingDept));
        Assert.Equal("Morning (8AM-4PM)", lookups.ShiftName(TestHelpers.MorningShift));
        Assert.Equal("10:00 PM to 06:00 AM", lookups.ShiftName(TestHelpers.RetiredShift));   // retired, but still named
        Assert.Null(lookups.ShiftName(TestHelpers.HousekeepingShift));                     // a SERP tbl_Shifts id
        Assert.Equal(new[] { TestHelpers.MorningShift, TestHelpers.EveningShift }, lookups.OfferedShifts.Select(s => s.Id));
    }
}

/// <summary>StaffValidation, the rules shared by IMSB.Admin and IMSB.Api.</summary>
public class StaffValidationTests
{
    private static Task<Dictionary<string, string>> Validate(IMSBContext ctx, CreateStaffDto dto, Employee? existing = null) =>
        StaffValidation.ValidateAsync(new StaffDetails(ctx), dto, existing, TestHelpers.SharedLocalizer());

    [Fact]
    public async Task Valid_form_has_no_errors()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        Assert.Empty(await Validate(ctx, TestHelpers.ValidCreateDto()));
    }

    [Fact]
    public async Task Every_failing_field_is_reported_at_once()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = new CreateStaffDto
        {
            FullName = "Some One", Mobile = "123", Aadhaar = "12", Gender = "?", DepartmentId = "0", AgencyId = "x", ShiftId = "999"
        };

        var errors = await Validate(ctx, dto);

        Assert.Equal(new[] { "aadhaar", "agency_id", "department_id", "designation", "employee_type_id", "gender", "join_date", "mobile", "shift" }, errors.Keys.OrderBy(k => k));
    }

    [Fact]
    public async Task Employee_type_category_and_shift_are_checked_separately()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.EmployeeTypeId = "";
        dto.DepartmentId = TestHelpers.HousekeepingDept.ToString();
        dto.ShiftId = TestHelpers.EveningShift.ToString();

        Assert.Equal(new[] { "employee_type_id" }, (await Validate(ctx, dto)).Keys);
    }

    // any staff category and any offered shift go with any employee type: dbo.departments and dbo.Shift have no type
    [Theory]
    [InlineData(TestHelpers.OfficeType, TestHelpers.SalesDept, TestHelpers.MorningShift, true)]
    [InlineData(TestHelpers.OfficeType, TestHelpers.HousekeepingDept, TestHelpers.EveningShift, true)]
    [InlineData(TestHelpers.HousekeepingType, TestHelpers.AdminDept, TestHelpers.MorningShift, true)]
    [InlineData(TestHelpers.OfficeType, TestHelpers.SalesDept, TestHelpers.RetiredShift, false)]          // dbo.Shift with DisplayOrder 0
    [InlineData(TestHelpers.OfficeType, TestHelpers.SalesDept, TestHelpers.HousekeepingShift, false)]     // a SERP tbl_Shifts id
    [InlineData(TestHelpers.OfficeType, 9060, TestHelpers.MorningShift, false)]                           // a category not in dbo.departments
    public async Task Category_must_be_a_dbo_department_and_shift_an_offered_dbo_shift(int type, int dept, int shift, bool valid)
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.EmployeeDesignations.Add(new EmployeeDesignation { DesigId = 9060, Name = "Only In SERP" });   // tbl_Designation is not used
        ctx.SaveChanges();
        var dto = TestHelpers.ValidCreateDto();
        dto.EmployeeTypeId = type.ToString();
        dto.DepartmentId = dept.ToString();
        dto.ShiftId = shift.ToString();

        Assert.Equal(valid, (await Validate(ctx, dto)).Count == 0);
    }

    [Fact]
    public async Task Editing_does_not_demand_an_aadhaar_but_registering_does()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var existing = TestHelpers.ExistingEmployee(501, "Someone", "9876500001");
        ctx.Employees.Add(existing);
        await ctx.SaveChangesAsync();
        var dto = TestHelpers.ValidCreateDto("9876500001");
        dto.Aadhaar = null;

        Assert.False((await Validate(ctx, dto, existing)).ContainsKey("aadhaar"));

        var register = TestHelpers.ValidCreateDto("9876500002");
        register.Aadhaar = null;
        Assert.True((await Validate(ctx, register)).ContainsKey("aadhaar"));
    }

    [Fact]
    public async Task An_empty_mobile_is_never_taken_as_unchanged()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var existing = TestHelpers.ExistingEmployee(501, "Someone", "");
        var dto = TestHelpers.ValidCreateDto("");

        Assert.Equal("Enter a valid 10-digit mobile number", (await Validate(ctx, dto, existing))["mobile"]);
    }

    [Theory]
    [InlineData("ACTIVE", null, 1)]
    [InlineData("INACTIVE", 1, 0)]
    [InlineData("inactive", 1, 0)]
    [InlineData(null, 0, 0)]    // no status sent: unchanged
    [InlineData("", 1, 1)]
    public void ApplyStatus_sets_is_active(string? status, int? before, int? after)
    {
        var e = new Employee { IsActive = before };
        StaffValidation.ApplyStatus(new UpdateStaffDto { Status = status }, e);
        Assert.Equal(after, e.IsActive);
    }

    [Theory]
    [InlineData(null, "old", "old")]        // not sent: kept
    [InlineData("", "old", null)]           // cleared
    [InlineData("   ", "old", null)]
    [InlineData("  new note ", null, "new note")]
    public void ApplyStatus_sets_or_clears_remarks(string? sent, string? before, string? after)
    {
        var e = new Employee { Remarks = before };
        StaffValidation.ApplyStatus(new UpdateStaffDto { Remarks = sent }, e);
        Assert.Equal(after, e.Remarks);
    }

    [Fact]
    public void ComputeStats_counts_status_departments_and_agency_staff()
    {
        var all = new[]
        {
            new Employee { IsActive = 1, DepartmentId = 1, AgencyId = 1 },
            new Employee { IsActive = 1, DepartmentId = 1 },
            new Employee { IsActive = 0, DepartmentId = 2, AgencyId = 2 },
            new Employee { IsActive = null, DepartmentId = null },
        };

        all[0].EmpTypeId = 2;
        all[1].EmpTypeId = 2;
        all[2].EmpTypeId = 3;

        Assert.Equal(new StaffValidation.Stats(4, 2, 2, 2, 2, 2), StaffValidation.ComputeStats(all));
        Assert.Equal(new StaffValidation.Stats(0, 0, 0, 0, 0, 0), StaffValidation.ComputeStats(Array.Empty<Employee>()));
    }

    [Fact]
    public async Task DepartmentItems_lists_every_designation_with_its_count_and_type()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var lookups = await new StaffDetails(ctx).LookupsAsync();
        var all = new[]
        {
            new Employee { DepartmentId = TestHelpers.SalesDept },
            new Employee { DepartmentId = TestHelpers.SalesDept },
            new Employee { DepartmentId = null },
        };

        var items = StaffValidation.DepartmentItems(all, lookups).ToDictionary(d => d.Name);

        Assert.Equal(3, items.Count);
        Assert.Equal(2, items["Sales"].Count);
        Assert.Equal(0, items["Admin"].Count);   // unused categories are still offered in the form
        Assert.Equal(TestHelpers.HousekeepingDept.ToString(), items["Cleaner"].Id);
    }

    [Fact]
    public void Export_row_follows_the_header()
    {
        var row = new StaffListItem(7, "Name", "2", "Office", "1", "Sales", "1", "Agency A", "9876500001", "Female", "11", "Morning", 5, "ACTIVE",
            "XXXX-XXXX-1234", null, JoinDate: "2020-01-15", MonthlySalary: 24000m, HasPhoto: true, DesignationId: "40", DesignationName: "Clerk");

        Assert.Equal(StaffValidation.ExportHeader.Length, StaffValidation.ExportRow(row).Length);
        Assert.Equal(new[] { "7", "Name", "Office", "Sales", "Clerk", "Agency A", "9876500001", "Female", "Morning", "5", "2020-01-15", "ACTIVE" }, StaffValidation.ExportRow(row));
        Assert.DoesNotContain(StaffValidation.ExportRow(row), v => v != null && v.Contains("XXXX"));   // no Aadhaar in exports
        Assert.DoesNotContain(StaffValidation.ExportRow(row), v => v != null && v.Contains("24000"));  // nor salary
    }
}

/// <summary>StaffDetails, the backend over the SERP tables.</summary>
public class StaffDetailsTests
{
    [Fact]
    public async Task Lookups_read_the_four_master_tables_in_order()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var lookups = await new StaffDetails(ctx).LookupsAsync();

        Assert.Equal(new[] { "Housekeeping", "Office" }, lookups.EmployeeTypes.Select(t => t.Name));   // tbl_EMP_Types by name
        Assert.Equal(new[] { "Admin", "Cleaner", "Sales" }, lookups.Departments.Select(d => d.Name));   // dbo.departments by name
        Assert.Equal(new[] { TestHelpers.AgencyA, TestHelpers.AgencyB }, lookups.Agencies.Select(a => a.AgencyId));
        // dbo.Shift, all of them (names on record still resolve), by display order
        Assert.Equal(new[] { TestHelpers.RetiredShift, TestHelpers.MorningShift, TestHelpers.EveningShift }, lookups.Shifts.Select(s => s.Id));
        Assert.Equal("Office", lookups.EmployeeTypeName(TestHelpers.OfficeType));
        Assert.Null(lookups.EmployeeTypeName(null));
        Assert.Null(lookups.DepartmentName(123456));
    }

    [Fact]
    public async Task ShiftOffered_reads_dbo_shift_display_order()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var staff = new StaffDetails(ctx);

        Assert.True(await staff.ShiftOfferedAsync(TestHelpers.MorningShift));
        Assert.True(await staff.ShiftOfferedAsync(TestHelpers.EveningShift));
        Assert.False(await staff.ShiftOfferedAsync(TestHelpers.RetiredShift));        // DisplayOrder 0
        Assert.False(await staff.ShiftOfferedAsync(TestHelpers.HousekeepingShift));   // only in SERP.tbl_Shifts
    }

    [Fact]
    public async Task DepartmentExists_reads_dbo_departments_not_tbl_designation()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.EmployeeDesignations.Add(new EmployeeDesignation { DesigId = 9060, Name = "Only In SERP" });
        ctx.SaveChanges();
        var staff = new StaffDetails(ctx);

        Assert.True(await staff.DepartmentExistsAsync(TestHelpers.SalesDept));
        Assert.True(await staff.DepartmentExistsAsync(TestHelpers.HousekeepingDept));
        Assert.False(await staff.DepartmentExistsAsync(9060));
    }

    [Fact]
    public async Task Duplicate_checks_exclude_the_employee_being_edited()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Someone", "9876500001", aadhaar: "234123412346"));
        await ctx.SaveChangesAsync();
        var staff = new StaffDetails(ctx);

        Assert.True(await staff.MobileExistsAsync("9876500001", null));
        Assert.False(await staff.MobileExistsAsync("9876500001", 501));
        Assert.True(await staff.AadhaarExistsAsync("234123412346", 502));
        Assert.False(await staff.AadhaarExistsAsync("234123412346", 501));
        Assert.False(await staff.AadhaarExistsAsync("999999999999", null));
    }

    [Fact]
    public async Task Filter_treats_an_empty_is_active_as_inactive()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var noFlag = TestHelpers.ExistingEmployee(501, "No Flag", "9876500001");
        noFlag.IsActive = null;
        ctx.Employees.AddRange(noFlag, TestHelpers.ExistingEmployee(502, "Active", "9876500002"));
        await ctx.SaveChangesAsync();
        var staff = new StaffDetails(ctx);

        Assert.Equal(new[] { 501 }, (await staff.FilterAsync(null, null, "INACTIVE")).Select(e => e.EmpId));
        Assert.Equal(new[] { 502 }, (await staff.FilterAsync(null, null, "ACTIVE")).Select(e => e.EmpId));
        Assert.Equal(2, (await staff.FilterAsync(null, null, "UNKNOWN")).Count);   // unknown status: no filter
    }

    [Fact]
    public async Task Filter_skips_rows_without_a_name_or_mobile_when_searching()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var blank = TestHelpers.ExistingEmployee(501, "x", "9876500001");
        blank.EmpName = null;
        blank.Mobile = null;
        ctx.Employees.AddRange(blank, TestHelpers.ExistingEmployee(502, "Meena", "9876500002"));
        await ctx.SaveChangesAsync();

        Assert.Equal(new[] { 502 }, (await new StaffDetails(ctx).FilterAsync("meena", null, null)).Select(e => e.EmpId));
        Assert.Equal(new[] { 501 }, (await new StaffDetails(ctx).FilterAsync("501", null, null)).Select(e => e.EmpId));
    }

    [Fact]
    public async Task Add_and_update_save_the_employee_type_shift_and_department_as_given()
    {
        // the backend stores the ids; matching them is StaffValidation's job
        using var ctx = TestHelpers.CreateStaffContext();
        var staff = new StaffDetails(ctx);
        var e = new Employee { EmpName = "New", EmpTypeId = TestHelpers.OfficeType, DepartmentId = TestHelpers.SalesDept, ShiftId = TestHelpers.MorningShift, IsActive = 1 };

        await staff.AddAsync(e);
        Assert.True(e.EmpId > 0);

        e.EmpTypeId = TestHelpers.HousekeepingType;
        e.DepartmentId = TestHelpers.HousekeepingDept;
        e.ShiftId = TestHelpers.HousekeepingShift;
        await staff.UpdateAsync(e);

        var saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Equal(TestHelpers.HousekeepingType, saved.EmpTypeId);
        Assert.Equal(TestHelpers.HousekeepingDept, saved.DepartmentId);
        Assert.Equal(TestHelpers.HousekeepingShift, saved.ShiftId);
    }

    [Fact]
    public async Task Employee_type_exists_reads_tbl_emp_types()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        var staff = new StaffDetails(ctx);

        Assert.True(await staff.EmployeeTypeExistsAsync(TestHelpers.OfficeType));
        Assert.True(await staff.EmployeeTypeExistsAsync(TestHelpers.HousekeepingType));
        Assert.False(await staff.EmployeeTypeExistsAsync(77));
    }

    [Fact]
    public async Task Filter_by_employee_type_reads_emp_type_id()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.AddRange(
            TestHelpers.ExistingEmployee(501, "Office One", "9876500001"),
            TestHelpers.ExistingEmployee(502, "Cleaner One", "9876500002", dept: TestHelpers.HousekeepingDept, shift: TestHelpers.HousekeepingShift),
            TestHelpers.ExistingEmployee(503, "Office Two", "9876500003", active: false));
        await ctx.SaveChangesAsync();
        var staff = new StaffDetails(ctx);

        Assert.Equal(new[] { 502 }, (await staff.FilterAsync(null, null, null, TestHelpers.HousekeepingType.ToString())).Select(e => e.EmpId));
        Assert.Equal(new[] { 503, 501 }, (await staff.FilterAsync(null, null, null, TestHelpers.OfficeType.ToString())).Select(e => e.EmpId));
        Assert.Equal(new[] { 503 }, (await staff.FilterAsync(null, null, "INACTIVE", TestHelpers.OfficeType.ToString())).Select(e => e.EmpId));
        Assert.Equal(3, (await staff.FilterAsync(null, null, null, "not-a-number")).Count);   // ignored
    }

    [Fact]
    public async Task Find_returns_a_tracked_employee()
    {
        using var ctx = TestHelpers.CreateStaffContext();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var staff = new StaffDetails(ctx);

        var found = await staff.FindAsync(501);

        Assert.NotNull(found);
        Assert.Equal(EntityState.Unchanged, ctx.Entry(found!).State);
        Assert.Null(await staff.FindAsync(999));
    }

    [Fact]
    public async Task Remove_reports_a_refused_delete_and_keeps_the_employee()
    {
        using var ctx = RefusingDeleteContext.Create();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Has Attendance", "9876500001"));
        await ctx.SaveChangesAsync();
        ctx.RefuseDeletes = true;
        var staff = new StaffDetails(ctx);

        Assert.False(await staff.RemoveAsync((await staff.FindAsync(501))!));

        Assert.Equal(EntityState.Unchanged, ctx.Entry(ctx.Employees.Local.Single()).State);
        ctx.RefuseDeletes = false;
        await ctx.SaveChangesAsync();   // nothing left pending
        Assert.Single(ctx.Employees);
    }

    [Fact]
    public async Task Controller_answers_409_with_a_translated_message_when_the_delete_is_refused()
    {
        using var ctx = RefusingDeleteContext.Create();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Has Attendance", "9876500001"));
        await ctx.SaveChangesAsync();
        ctx.RefuseDeletes = true;

        var conflict = Assert.IsType<ConflictObjectResult>(await TestHelpers.CreateController(ctx).Delete(501));

        var json = System.Text.Json.JsonSerializer.Serialize(conflict.Value);
        Assert.Contains("attendance records and cannot be deleted", json);
    }

    /// <summary>
    /// The in-memory provider has no foreign keys, so this context fails deletes the way SQL Server does when
    /// FK_tbl_EMPDailyAttendance_EMP_ID_tbl_Employee still points at the employee.
    /// </summary>
    private sealed class RefusingDeleteContext : IMSBContext
    {
        public bool RefuseDeletes { get; set; }

        private RefusingDeleteContext(DbContextOptions<IMSBContext> options) : base(options) { }

        public static RefusingDeleteContext Create()
        {
            var ctx = new RefusingDeleteContext(new DbContextOptionsBuilder<IMSBContext>()
                .UseInMemoryDatabase("staff-refuse-" + Guid.NewGuid()).Options);
            TestHelpers.SeedSerpMasters(ctx);
            ctx.SaveChanges();
            return ctx;
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (RefuseDeletes && ChangeTracker.Entries<Employee>().Any(e => e.State == EntityState.Deleted))
                throw new DbUpdateException("The DELETE statement conflicted with the REFERENCE constraint \"FK_tbl_EMPDailyAttendance_EMP_ID_tbl_Employee\".");
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
