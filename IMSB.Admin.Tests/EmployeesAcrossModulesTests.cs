using IMSB.Backend;
using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace IMSB.Admin.Tests;

/// <summary>
/// The attendance, leave, OT, payroll, dashboard and search pages read their employees from SERP.tbl_Employee
/// (through the Staff view), not from dbo.staff: someone registered on the Staff page shows up everywhere under their EMP_ID.
/// </summary>
public class EmployeesAcrossModulesTests
{
    /// <summary>A context with one employee registered through the Staff page: Sales, "Clerk" at 30,000, morning shift.</summary>
    private static (IMSBContext Ctx, int Id) Registered(bool active = true)
    {
        var ctx = StaffExtras.Context();
        var dto = TestHelpers.ValidCreateDto();
        dto.Designation = "Clerk";
        dto.MonthlySalary = 30000m;
        dto.LeaveQuotas = StaffExtras.Quotas(12, null, null);
        var id = TestHelpers.OkJson(TestHelpers.CreateController(ctx).Create(dto).GetAwaiter().GetResult()).GetProperty("employeeId").GetInt32();
        if (!active)
        {
            ctx.Employees.Single(e => e.EmpId == id).IsActive = 0;
            ctx.SaveChanges();
        }
        return (ctx, id);
    }

    [Fact]
    public async Task The_attendance_sheet_lists_tbl_employee_people()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;

        var row = Assert.Single(await new Attendance(ctx).SheetAsync(DateTime.Today));

        Assert.Equal(id, row.Staff.Id);
        Assert.Equal("EMP001", row.Staff.EmployeeId);   // the employee code
        Assert.Equal("Meena Kumari", row.Staff.FullName);
        Assert.Equal("Sales", row.DepartmentName);
        Assert.Equal("Clerk", row.Staff.Designation);
        Assert.Equal("Morning (8AM-4PM)", row.ShiftName);   // tbl_Employee.ShiftID -> dbo.Shift
    }

    [Fact]
    public async Task An_inactive_employee_is_left_off_the_attendance_sheet()
    {
        var (ctx, _) = Registered(active: false);
        using var __ = ctx;

        Assert.Empty(await new Attendance(ctx).SheetAsync(DateTime.Today));
    }

    [Fact]
    public async Task Attendance_saved_for_an_employee_is_keyed_by_their_emp_id()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        var attendance = new Attendance(ctx);
        var person = Assert.Single(await attendance.StaffByEmployeeIdsAsync(new List<string> { id.ToString() }));

        await attendance.SaveSheetAsync(new[] { new AttendanceRecord { StaffId = person.Id, AttendanceDate = DateTime.Today } });

        Assert.Equal(id, (await ctx.AttendanceRecords.SingleAsync()).StaffId);
        Assert.Equal("Meena Kumari", (await attendance.FindAsync((await ctx.AttendanceRecords.SingleAsync()).Id, false))!.Staff!.FullName);
    }

    [Fact]
    public async Task The_leaves_page_offers_tbl_employee_people_with_their_category()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;

        var option = Assert.Single(await new Leaves(ctx).ActiveStaffAsync());

        Assert.Equal((id, "EMP001", "Meena Kumari", "Sales"), (option.Id, option.EmployeeId, option.FullName, option.StaffCategory));
    }

    [Fact]
    public async Task Leave_can_be_recorded_for_an_employee_against_the_quota_set_on_the_staff_page()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        var year = DateTime.Today.Year;
        var from = new DateTime(year, 1, 5);
        var leaves = new Leaves(ctx);

        var result = await leaves.RecordEntryAsync(id, "Casual", from, from.AddDays(1), null, "tester");

        Assert.Null(result.ErrorKey);
        var balance = Assert.Single(await leaves.BalancesAsync(year, id.ToString()));
        Assert.Equal((12m, 2m, 10m), (balance.Entitlement, balance.Taken, balance.Balance));
    }

    [Fact]
    public async Task The_pay_bill_uses_the_employees_designation_and_salary()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;

        var row = Assert.Single(await new Payroll(ctx).RowsAsync(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)));

        Assert.Equal((id, "EMP001", "Meena Kumari", "Sales", "Clerk", 30000m),
            (row.StaffId, row.EmployeeId, row.EmployeeName, row.Department, row.Designation, row.MonthlySalary));
    }

    [Fact]
    public async Task A_salary_change_on_the_staff_page_reaches_the_pay_bill()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        var dto = StaffExtras.DtoFor(await ctx.Employees.AsNoTracking().SingleAsync());
        dto.Designation = "Senior Clerk";
        dto.MonthlySalary = 35000m;
        await TestHelpers.CreateController(ctx).Update(id, dto);

        var row = Assert.Single(await new Payroll(ctx).RowsAsync(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)));

        Assert.Equal(("Senior Clerk", 35000m), (row.Designation, row.MonthlySalary));
    }

    [Fact]
    public async Task Ot_assignment_offers_tbl_employee_people()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;

        var option = Assert.Single(await new OtAssignments(ctx).UnassignedStaffAsync());

        Assert.Equal(id, option.Id);
    }

    [Fact]
    public async Task Header_search_finds_an_employee_by_name_mobile_or_emp_id()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        var search = new Search(ctx);

        Assert.Equal(id, Assert.Single(await search.StaffAsync("meena", "", 5)).Id);
        Assert.Equal(id, Assert.Single(await search.StaffAsync("9876500001", "9876500001", 5)).Id);
        Assert.Equal(id, Assert.Single(await search.StaffAsync(id.ToString(), id.ToString(), 5)).Id);
    }

    [Fact]
    public async Task The_dashboard_counts_tbl_employee_people()
    {
        var (ctx, _) = Registered();
        using var __ = ctx;
        ctx.Employees.Add(TestHelpers.ExistingEmployee(9001, "Gone", "9876500009", active: false));
        await ctx.SaveChangesAsync();

        var stats = await new Dashboard(ctx).StatsAsync(new BazaarOptions { TotalStalls = 1 });

        Assert.Equal((2, 1), (stats.TotalStaff, stats.ActiveStaff));
    }

    [Fact]
    public async Task An_employee_deleted_on_the_staff_page_disappears_from_the_other_pages()
    {
        var (ctx, id) = Registered();
        using var _ = ctx;
        ctx.ChangeTracker.Clear();

        await TestHelpers.CreateController(ctx).Delete(id);

        Assert.Empty(await new Attendance(ctx).SheetAsync(DateTime.Today));
        Assert.Empty(await new Leaves(ctx).StaffAsync());
        Assert.Empty(ctx.LeaveEntitlements);
    }
}

/// <summary>The Staff model is the view over tbl_Employee, and the EmployeesReplaceStaff migration builds it.</summary>
public class StaffViewMappingTests
{
    private static Microsoft.EntityFrameworkCore.Metadata.IModel SqlServerModel()
    {
        using var ctx = new IMSBContext(new DbContextOptionsBuilder<IMSBContext>().UseSqlServer("Server=unused;Database=unused").Options);
        return Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(ctx).Model;
    }

    private static string UpSql() => string.Join("\n",
        new IMSB.Dto.Migrations.EmployeesReplaceStaff().UpOperations.OfType<SqlOperation>().Select(o => o.Sql));

    [Fact]
    public void Staff_reads_the_view_and_is_no_longer_the_staff_table()
    {
        var staff = SqlServerModel().FindEntityType(typeof(Staff))!;

        Assert.Null(staff.GetTableName());
        Assert.Equal(IMSBContext.StaffViewName, staff.GetViewName());
        Assert.Equal("dbo", staff.GetViewSchema());
    }

    [Fact]
    public void The_view_gives_every_column_the_staff_model_reads()
    {
        var staff = SqlServerModel().FindEntityType(typeof(Staff))!;
        var view = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.View(IMSBContext.StaffViewName, "dbo");
        var sql = UpSql();

        Assert.Contains($"CREATE OR ALTER VIEW [dbo].[{IMSBContext.StaffViewName}] AS", sql);
        foreach (var property in staff.GetProperties())
            Assert.Contains($"AS [{property.GetColumnName(view)}]", sql);
    }

    [Fact]
    public void The_view_is_built_from_tbl_employee_and_its_masters()
    {
        var sql = UpSql();

        Assert.Contains("e.[EMP_ID] AS [Id]", sql);
        Assert.Contains("FROM [SERP].[tbl_Employee] e", sql);
        Assert.Contains("FROM [SERP].[tbl_Designation] x", sql);                 // designation and salary
        Assert.Contains("ORDER BY CASE WHEN x.[EmpId] = e.[EMP_ID] THEN 0 ELSE 1 END", sql);   // the employee's own row first
        Assert.Contains("LEFT JOIN [dbo].[departments] dep ON dep.[Id] = e.[DepartmentID]", sql);
        Assert.Contains("LEFT JOIN [dbo].[Shift] sh ON sh.[Id] = e.[ShiftID]", sql);
        Assert.Contains("CASE WHEN e.[IsActive] = 1 THEN N'ACTIVE' ELSE N'INACTIVE' END", sql);
    }

    [Theory]
    [InlineData("attendance_records")]
    [InlineData("ot_assignments")]
    [InlineData("leave_entitlements")]
    [InlineData("staff_leave_entries")]
    public void History_tables_point_at_tbl_employee_and_start_afresh(string table)
    {
        var migration = new IMSB.Dto.Migrations.EmployeesReplaceStaff();
        var sql = UpSql();

        Assert.Contains(migration.UpOperations.OfType<DropForeignKeyOperation>(), op => op.Table == table && op.Name == $"FK_{table}_staff_StaffId");
        Assert.Contains($"DELETE FROM [dbo].[{table}]", sql);
        Assert.Contains($"ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [FK_{table}_tbl_Employee_StaffId]", sql);
        Assert.Contains("REFERENCES [SERP].[tbl_Employee] ([EMP_ID]) ON DELETE CASCADE", sql);
    }

    [Fact]
    public void Pay_bills_start_afresh_and_dbo_staff_is_kept()
    {
        var migration = new IMSB.Dto.Migrations.EmployeesReplaceStaff();

        Assert.Contains("DELETE FROM [dbo].[pay_bill_entries];", UpSql());
        Assert.Contains("DELETE FROM [dbo].[leave_entitlements] WHERE [StaffId] IS NOT NULL;", UpSql());   // category-wide rows stay
        Assert.DoesNotContain(migration.UpOperations.OfType<DropTableOperation>(), op => op.Name == "staff");
        Assert.Contains(migration.UpOperations.OfType<DropTableOperation>(), op => op.Name == "employee_leave_quotas");
    }

    [Fact]
    public void Tbl_designation_gets_an_emp_id_column()
    {
        Assert.Contains("IF COL_LENGTH(N'[SERP].[tbl_Designation]', N'EmpId') IS NULL", UpSql());
        Assert.Contains("ALTER TABLE [SERP].[tbl_Designation] ADD [EmpId] int NULL;", UpSql());

        var designation = SqlServerModel().FindEntityType(typeof(EmployeeDesignation))!;
        Assert.Equal("EmpId", designation.FindProperty(nameof(EmployeeDesignation.EmpId))!.GetColumnName());
    }

    [Fact]
    public void Down_restores_the_links_to_dbo_staff()
    {
        var migration = new IMSB.Dto.Migrations.EmployeesReplaceStaff();
        var down = string.Join("\n", migration.DownOperations.OfType<SqlOperation>().Select(o => o.Sql));

        Assert.Contains("DROP VIEW IF EXISTS [dbo].[vw_StaffEmployees];", down);
        Assert.Equal(4, migration.DownOperations.OfType<AddForeignKeyOperation>().Count(op => op.PrincipalTable == "staff"));
    }
}
