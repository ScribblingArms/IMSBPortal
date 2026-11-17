using IMSB.Backend;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

/// <summary>
/// The Designation box: every employee has their own SERP.tbl_Designation row (EmpId = their EMP_ID), holding the designation
/// name and their monthly salary (BaseSalary); tbl_Employee.Desig points at it. The Staff Category is tbl_Employee.DepartmentID.
/// </summary>
public class StaffDesignationTests
{
    private static IMSBContext Context() => StaffExtras.Context();

    private static EmployeeDesignation DesignationOf(IMSBContext ctx, int empId)
    {
        var desig = ctx.Employees.AsNoTracking().Single(e => e.EmpId == empId).DesigId;
        return ctx.EmployeeDesignations.AsNoTracking().Single(d => d.DesigId == desig);
    }

    private static int Register(IMSBContext ctx, string mobile, string designation, decimal? salary = null, string role = "Admin",
        int type = TestHelpers.OfficeType)
    {
        var dto = TestHelpers.ValidCreateDto(mobile);
        dto.Designation = designation;
        dto.MonthlySalary = salary;
        dto.EmployeeTypeId = type.ToString();
        var result = TestHelpers.CreateController(ctx, role: role).Create(dto).GetAwaiter().GetResult();
        return TestHelpers.OkJson(result).GetProperty("employeeId").GetInt32();
    }

    // ── registering ───────────────────────────────────────────────

    [Fact]
    public async Task Registering_adds_the_employees_own_designation_row_with_their_emp_id()
    {
        using var ctx = Context();
        var before = await ctx.EmployeeDesignations.MaxAsync(d => d.DesigId);

        var id = Register(ctx, "9876500001", "  Store Keeper ", 18500m);

        var row = DesignationOf(ctx, id);
        Assert.Equal(id, row.EmpId);                            // tbl_Designation.EmpId = tbl_Employee.EMP_ID
        Assert.Equal("Store Keeper", row.Name);                 // trimmed
        Assert.Equal(before + 1, row.DesigId);                  // no identity column: the next id
        Assert.Equal(TestHelpers.OfficeType, row.EmpTypeId);    // the employee's type
        Assert.Equal(18500m, row.BaseSalary);
        var saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Equal(row.DesigId, saved.DesigId);
        Assert.Equal(TestHelpers.SalesDept, saved.DepartmentId); // the staff category has its own column
    }

    [Fact]
    public async Task Employees_with_the_same_designation_each_get_their_own_row_and_salary()
    {
        using var ctx = Context();
        var count = await ctx.EmployeeDesignations.CountAsync();

        var a = Register(ctx, "9876500001", "Clerk", 20000m);
        var b = Register(ctx, "9876500002", "CLERK", 21000m);

        Assert.Equal(count + 2, await ctx.EmployeeDesignations.CountAsync());
        Assert.NotEqual(DesignationOf(ctx, a).DesigId, DesignationOf(ctx, b).DesigId);
        Assert.Equal((a, 20000m), (DesignationOf(ctx, a).EmpId!.Value, DesignationOf(ctx, a).BaseSalary!.Value));
        Assert.Equal((b, 21000m), (DesignationOf(ctx, b).EmpId!.Value, DesignationOf(ctx, b).BaseSalary!.Value));
    }

    [Fact]
    public async Task A_serp_designation_with_the_same_name_is_left_alone()
    {
        using var ctx = Context();
        var id = Register(ctx, "9876500001", "Sales", 9000m);   // "Sales" is also a SERP tbl_Designation row (no EmpId)

        Assert.NotEqual(TestHelpers.SalesDesignation, DesignationOf(ctx, id).DesigId);
        var serp = await ctx.EmployeeDesignations.AsNoTracking().SingleAsync(d => d.DesigId == TestHelpers.SalesDesignation);
        Assert.Null(serp.EmpId);
        Assert.Null(serp.BaseSalary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Registering_needs_a_designation(string? designation)
    {
        using var ctx = Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.Designation = designation;

        Assert.Equal("Designation is required", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["designation"]);
        Assert.Empty(ctx.Employees);
    }

    [Fact]
    public async Task A_designation_longer_than_the_column_is_refused()
    {
        using var ctx = Context();
        var count = await ctx.EmployeeDesignations.CountAsync();
        var dto = TestHelpers.ValidCreateDto();
        dto.Designation = new string('d', EmployeeDesignation.NameMaxLength + 1);

        Assert.True(TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto)).ContainsKey("designation"));
        Assert.Equal(count, await ctx.EmployeeDesignations.CountAsync());   // nothing added
    }

    [Fact]
    public async Task A_refused_registration_adds_no_designation()
    {
        using var ctx = Context();
        var count = await ctx.EmployeeDesignations.CountAsync();
        var dto = TestHelpers.ValidCreateDto();
        dto.Designation = "Brand New";
        dto.Mobile = "12";

        Assert.True(TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto)).ContainsKey("mobile"));
        Assert.Equal(count, await ctx.EmployeeDesignations.CountAsync());
    }

    // ── the salary, the employee's own ────────────────────────────

    [Fact]
    public async Task Changing_one_employees_salary_leaves_the_others()
    {
        using var ctx = Context();
        var a = Register(ctx, "9876500001", "Clerk", 20000m);
        var b = Register(ctx, "9876500002", "Clerk", 20000m);

        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == b));
        dto.Designation = "Clerk";
        dto.MonthlySalary = 22000m;
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(b, dto));

        var rows = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null)).GetProperty("staff").EnumerateArray()
            .ToDictionary(r => r.GetProperty("employeeId").GetInt32(), r => r.GetProperty("monthlySalary").GetDecimal());
        Assert.Equal(20000m, rows[a]);
        Assert.Equal(22000m, rows[b]);
    }

    [Fact]
    public async Task Editing_updates_the_employees_own_row_in_place()
    {
        using var ctx = Context();
        var id = Register(ctx, "9876500001", "Clerk", 20000m);
        var row = DesignationOf(ctx, id).DesigId;
        var count = await ctx.EmployeeDesignations.CountAsync();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.Designation = "Senior Clerk";
        dto.MonthlySalary = 25000m;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(id, dto));

        Assert.Equal(count, await ctx.EmployeeDesignations.CountAsync());
        Assert.Equal((row, "Senior Clerk", 25000m), (DesignationOf(ctx, id).DesigId, DesignationOf(ctx, id).Name, DesignationOf(ctx, id).BaseSalary!.Value));
    }

    [Fact]
    public async Task An_admin_can_clear_the_salary()
    {
        using var ctx = Context();
        var id = Register(ctx, "9876500001", "Clerk", 20000m);
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.Designation = "Clerk";
        dto.MonthlySalary = null;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(id, dto));

        Assert.Null(DesignationOf(ctx, id).BaseSalary);
    }

    [Fact]
    public async Task Staff_role_cannot_set_or_change_the_salary()
    {
        using var ctx = Context();
        var other = Register(ctx, "9876500002", "Clerk", 1m, role: "Staff");
        Assert.Null(DesignationOf(ctx, other).BaseSalary);

        var id = Register(ctx, "9876500001", "Clerk", 20000m);
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync(e => e.EmpId == id));
        dto.Designation = "Head Clerk";
        dto.MonthlySalary = 2m;
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx, role: "Staff").Update(id, dto));

        Assert.Equal(("Head Clerk", 20000m), (DesignationOf(ctx, id).Name, DesignationOf(ctx, id).BaseSalary!.Value));   // the name changes, the salary not
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000000)]   // over decimal(10,2)
    public async Task A_salary_outside_base_salarys_range_is_refused(decimal salary)
    {
        using var ctx = Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.MonthlySalary = salary;

        Assert.Equal("Enter a monthly salary from 0 to 99,999,999.99.", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto))["monthly_salary"]);
    }

    // ── editing ───────────────────────────────────────────────────

    [Fact]
    public async Task Editing_with_a_blank_designation_keeps_the_one_on_record()
    {
        using var ctx = Context();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));
        await ctx.SaveChangesAsync();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());   // Designation not sent

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        Assert.Equal(TestHelpers.SalesDesignation, (await ctx.Employees.AsNoTracking().SingleAsync()).DesigId);
    }

    [Fact]
    public async Task A_serp_employee_on_a_shared_designation_gets_their_own_row_when_edited()
    {
        using var ctx = Context();
        ctx.Employees.Add(TestHelpers.ExistingEmployee(501, "Someone", "9876500001"));   // Desig = the SERP "Sales" row
        await ctx.SaveChangesAsync();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.Designation = "Accountant";
        dto.MonthlySalary = 30000m;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));

        var own = DesignationOf(ctx, 501);
        Assert.Equal((501, "Accountant", 30000m), (own.EmpId!.Value, own.Name, own.BaseSalary!.Value));
        var serp = await ctx.EmployeeDesignations.AsNoTracking().SingleAsync(d => d.DesigId == TestHelpers.SalesDesignation);
        Assert.Equal(("Sales", null), (serp.Name, serp.BaseSalary));   // the shared SERP row is never changed
    }

    [Fact]
    public async Task Deleting_an_employee_deletes_their_own_designation_row_only()
    {
        using var ctx = Context();
        var id = Register(ctx, "9876500001", "Clerk", 20000m);
        var own = DesignationOf(ctx, id).DesigId;
        ctx.ChangeTracker.Clear();

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Delete(id));

        Assert.False(await ctx.EmployeeDesignations.AnyAsync(d => d.DesigId == own));
        Assert.True(await ctx.EmployeeDesignations.AnyAsync(d => d.DesigId == TestHelpers.SalesDesignation));
    }

    [Fact]
    public async Task A_serp_record_without_a_designation_or_category_can_be_edited_until_given_one()
    {
        using var ctx = Context();
        var e = TestHelpers.ExistingEmployee(501, "From SERP", "9876500001", desig: null);
        e.DepartmentId = null;
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.DepartmentId = null;

        Assert.Equal("Designation is required", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto))["designation"]);

        dto.Designation = "Helper";
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));   // the category may stay empty
        var saved = await ctx.Employees.AsNoTracking().SingleAsync();
        Assert.Null(saved.DepartmentId);
        Assert.Equal("Helper", DesignationOf(ctx, 501).Name);

        dto.DepartmentId = TestHelpers.AdminDept.ToString();
        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update(501, dto));
        dto.DepartmentId = "";
        Assert.Equal("Staff category is required", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update(501, dto))["department_id"]);
    }

    // ── what the page and API get ─────────────────────────────────

    [Fact]
    public async Task Staff_rows_carry_the_designation_and_the_category_separately()
    {
        using var ctx = Context();
        var id = Register(ctx, "9876500001", "Clerk", 20000m);

        var row = Assert.Single(TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null)).GetProperty("staff").EnumerateArray());

        Assert.Equal(DesignationOf(ctx, id).DesigId.ToString(), row.GetProperty("designationId").GetString());
        Assert.Equal("Clerk", row.GetProperty("designationName").GetString());
        Assert.Equal(TestHelpers.SalesDept.ToString(), row.GetProperty("departmentId").GetString());
        Assert.Equal("Sales", row.GetProperty("departmentName").GetString());
        Assert.Equal(20000m, row.GetProperty("monthlySalary").GetDecimal());
    }

    [Fact]
    public async Task The_designation_suggestions_are_the_tbl_designation_rows_with_a_name()
    {
        using var ctx = Context();
        ctx.EmployeeDesignations.Add(new EmployeeDesignation { DesigId = 9600, Name = null });
        await ctx.SaveChangesAsync();

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));

        var names = json.GetProperty("designations").EnumerateArray().Select(d => d.GetProperty("name").GetString()).ToList();
        Assert.Equal(new[] { "Admin", "Cleaner", "Sales" }, names);   // the seeded rows, by name; the nameless one is left out
    }

    [Fact]
    public async Task The_category_filter_and_counts_read_department_id()
    {
        using var ctx = Context();
        ctx.Employees.AddRange(
            TestHelpers.ExistingEmployee(501, "Sales One", "9876500001", dept: TestHelpers.SalesDept),
            TestHelpers.ExistingEmployee(502, "Admin One", "9876500002", dept: TestHelpers.AdminDept, desig: TestHelpers.SalesDesignation));
        await ctx.SaveChangesAsync();

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, TestHelpers.AdminDept.ToString(), null));

        Assert.Equal(new[] { 502 }, json.GetProperty("staff").EnumerateArray().Select(s => s.GetProperty("employeeId").GetInt32()));
        var counts = json.GetProperty("depts").EnumerateArray().ToDictionary(d => d.GetProperty("name").GetString()!, d => d.GetProperty("count").GetInt32());
        Assert.Equal(1, counts["Sales"]);
        Assert.Equal(1, counts["Admin"]);
        Assert.Equal(2, json.GetProperty("stats").GetProperty("departments").GetInt32());
    }

    [Fact]
    public async Task The_export_has_a_designation_column_and_no_salary()
    {
        using var ctx = Context();
        var id = Register(ctx, "9876500001", "Clerk", 20000m);

        var file = Assert.IsType<FileContentResult>(await TestHelpers.CreateController(ctx).Export(null, null, null));

        var lines = ExcelExportTestHelpers.CsvText(file).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("EmployeeId,FullName,EmployeeType,Department,Designation,Agency,Mobile,Gender,Shift,StallNumber,JoinDate,Status", lines[0]);
        Assert.StartsWith($"{id},Meena Kumari,Office,Sales,Clerk,Agency A,", lines[1]);
        Assert.DoesNotContain("20000", lines[1]);
    }

    [Fact]
    public async Task The_api_saves_the_designation_the_same_way()
    {
        using var ctx = Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.Designation = "Clerk";
        dto.MonthlySalary = 19000m;

        var id = TestHelpers.OkJson(await StaffExtras.Api(ctx).Create(dto)).GetProperty("employeeId").GetInt32();

        Assert.Equal("Clerk", DesignationOf(ctx, id).Name);
        Assert.Equal(19000m, DesignationOf(ctx, id).BaseSalary);
        var json = TestHelpers.OkJson(await StaffExtras.Api(ctx).Get(id));
        Assert.Equal("Clerk", json.GetProperty("designationName").GetString());
        Assert.Equal(19000m, json.GetProperty("monthlySalary").GetDecimal());
    }

    [Fact]
    public async Task Backend_add_without_a_designation_keeps_what_the_employee_has()
    {
        using var ctx = Context();
        var e = new Employee { EmpName = "Direct", DesigId = TestHelpers.SalesDesignation, IsActive = 1 };

        await new StaffDetails(ctx).AddAsync(e, null, null);

        Assert.Equal(TestHelpers.SalesDesignation, (await ctx.Employees.AsNoTracking().SingleAsync()).DesigId);
    }
}
