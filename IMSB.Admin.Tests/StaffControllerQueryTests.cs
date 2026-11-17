using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

/// <summary>The Staff directory reads SERP.tbl_Employee with its departments (tbl_Designation) and agencies (tbl_ArtisansTypeAgency).</summary>
public class StaffControllerQueryTests
{
    private static IMSB.Dto.Models.IMSBContext SeededContext()
    {
        var ctx = TestHelpers.CreateStaffContext();
        var t0 = new DateTime(2026, 1, 1);
        ctx.Employees.AddRange(
            TestHelpers.ExistingEmployee(501, "Ramesh Kumar", "9876543210", createdOn: t0, agency: TestHelpers.AgencyA),
            TestHelpers.ExistingEmployee(502, "Suresh Reddy", "9876543211", createdOn: t0.AddDays(1)),
            TestHelpers.ExistingEmployee(503, "Lakshmi Devi", "9876543212", dept: TestHelpers.AdminDept, createdOn: t0.AddDays(2), agency: TestHelpers.AgencyB, aadhaar: "234123412346"),
            TestHelpers.ExistingEmployee(504, "Kiran Kumar", "9876543213", active: false, dept: TestHelpers.AdminDept, createdOn: t0.AddDays(3)),
            TestHelpers.ExistingEmployee(505, "Padma Rani", "9876543214", dept: TestHelpers.HousekeepingDept, shift: TestHelpers.RetiredShift, createdOn: t0.AddDays(4)));
        ctx.SaveChanges();
        return ctx;
    }

    private static List<int> Ids(System.Text.Json.JsonElement json) =>
        json.GetProperty("staff").EnumerateArray().Select(s => s.GetProperty("employeeId").GetInt32()).ToList();

    [Fact]
    public async Task IndexData_returns_all_employees_newest_first_with_the_masters()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));

        Assert.Equal(new[] { 505, 504, 503, 502, 501 }, Ids(json));
        // staff categories: dbo.departments by name
        Assert.Equal(new[] { "Admin", "Cleaner", "Sales" }, json.GetProperty("depts").EnumerateArray().Select(d => d.GetProperty("name").GetString()));
        Assert.Equal(new[] { "Agency A", "Agency B" }, json.GetProperty("agencies").EnumerateArray().Select(a => a.GetProperty("name").GetString()));
        // shifts: the offered dbo.Shift rows by display order (the retired Night shift is left out)
        var shifts = json.GetProperty("shifts").EnumerateArray().ToList();
        Assert.Equal(new[] { "11", "12" }, shifts.Select(s => s.GetProperty("id").GetString()));
        Assert.Equal(new[] { "Morning (8AM-4PM)", "Evening (4PM-10PM)" }, shifts.Select(s => s.GetProperty("name").GetString()));
        Assert.False(shifts[0].TryGetProperty("employeeTypeId", out _));
    }

    [Fact]
    public async Task Staff_rows_have_display_names_and_a_masked_aadhaar()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData("503", null, null));
        var row = Assert.Single(json.GetProperty("staff").EnumerateArray());

        Assert.Equal("Lakshmi Devi", row.GetProperty("fullName").GetString());
        Assert.Equal(TestHelpers.AdminDept.ToString(), row.GetProperty("departmentId").GetString());
        Assert.Equal("Admin", row.GetProperty("departmentName").GetString());
        Assert.Equal("Office", row.GetProperty("employeeTypeName").GetString());
        Assert.Equal("Agency B", row.GetProperty("agencyName").GetString());
        Assert.Equal("Morning (8AM-4PM)", row.GetProperty("shiftName").GetString());
        Assert.Equal("ACTIVE", row.GetProperty("status").GetString());
        Assert.Equal("XXXX-XXXX-2346", row.GetProperty("aadhaarMasked").GetString());
        Assert.DoesNotContain("234123412346", row.GetRawText());
    }

    [Theory]
    [InlineData("kumar", new[] { 504, 501 })]       // name, case-insensitive
    [InlineData("RAMESH", new[] { 501 })]
    [InlineData("9876543212", new[] { 503 })]       // mobile
    [InlineData("502", new[] { 502 })]              // employee id
    [InlineData("  Suresh  ", new[] { 502 })]       // trimmed
    [InlineData("nobody", new int[0])]
    public async Task Search_matches_name_mobile_or_employee_id(string q, int[] expected)
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(q, null, null));
        Assert.Equal(expected, Ids(json));
    }

    [Fact]
    public async Task Department_filter_returns_only_that_designation()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, TestHelpers.AdminDept.ToString(), null));
        Assert.Equal(new[] { 504, 503 }, Ids(json));
    }

    [Fact]
    public async Task Invalid_department_filter_is_ignored()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, "not-a-number", null));
        Assert.Equal(5, Ids(json).Count);
    }

    [Theory]
    [InlineData("ACTIVE", new[] { 505, 503, 502, 501 })]
    [InlineData("active", new[] { 505, 503, 502, 501 })]
    [InlineData("INACTIVE", new[] { 504 })]
    public async Task Status_filter_reads_is_active(string status, int[] expected)
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, status));
        Assert.Equal(expected, Ids(json));
    }

    [Fact]
    public async Task Filters_combine()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData("kumar", TestHelpers.AdminDept.ToString(), "inactive"));
        Assert.Equal(new[] { 504 }, Ids(json));
    }

    [Fact]
    public async Task Stats_and_department_counts_cover_all_employees_regardless_of_filters()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData("nobody", null, null));
        var stats = json.GetProperty("stats");

        Assert.Equal(5, stats.GetProperty("total").GetInt32());
        Assert.Equal(4, stats.GetProperty("active").GetInt32());
        Assert.Equal(1, stats.GetProperty("inactive").GetInt32());
        Assert.Equal(3, stats.GetProperty("departments").GetInt32());
        Assert.Equal(2, stats.GetProperty("agencyStaff").GetInt32());
        var counts = json.GetProperty("depts").EnumerateArray().ToDictionary(d => d.GetProperty("name").GetString()!, d => d.GetProperty("count").GetInt32());
        Assert.Equal(2, counts["Sales"]);
        Assert.Equal(2, counts["Admin"]);
        Assert.Equal(1, counts["Cleaner"]);
    }

    [Fact]
    public async Task Index_view_gets_the_same_stats()
    {
        using var ctx = SeededContext();
        var view = Assert.IsType<ViewResult>(await TestHelpers.CreateController(ctx).Index());

        Assert.Equal(5, view.ViewData["TotalStaff"]);
        Assert.Equal(4, view.ViewData["ActiveStaff"]);
        Assert.Equal(1, view.ViewData["InactiveStaff"]);
        Assert.Equal(3, view.ViewData["Departments"]);
        Assert.Equal(2, view.ViewData["AgencyStaff"]);
    }

    [Fact]
    public async Task Export_returns_csv_with_header_and_filtered_rows()
    {
        using var ctx = SeededContext();
        var file = Assert.IsType<FileContentResult>(await TestHelpers.CreateController(ctx).Export(null, TestHelpers.AdminDept.ToString(), null));

        Assert.Equal("application/vnd.ms-excel", file.ContentType);
        Assert.EndsWith(".xls", file.FileDownloadName);
        var lines = ExcelExportTestHelpers.CsvText(file).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\n', '\r')).ToArray();
        Assert.Equal("EmployeeId,FullName,EmployeeType,Department,Designation,Agency,Mobile,Gender,Shift,StallNumber,JoinDate,Status", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.Equal("504,Kiran Kumar,Office,Admin,Sales,,9876543213,Female,Morning (8AM-4PM),,,INACTIVE", lines[1]);
    }

    [Fact]
    public async Task Export_follows_the_employee_type_filter()
    {
        using var ctx = SeededContext();
        var file = Assert.IsType<FileContentResult>(await TestHelpers.CreateController(ctx).Export(null, null, null, TestHelpers.HousekeepingType.ToString()));

        var lines = ExcelExportTestHelpers.CsvText(file).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("505,Padma Rani,Housekeeping,Cleaner,", lines[1]);
    }

    // ── tbl_EMP_Types and tbl_Shifts ──────────────────────────────

    [Fact]
    public async Task IndexData_lists_the_employee_types_with_their_counts()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));

        var types = json.GetProperty("employeeTypes").EnumerateArray().ToList();
        Assert.Equal(new[] { "Housekeeping", "Office" }, types.Select(t => t.GetProperty("name").GetString()));
        Assert.Equal(new[] { TestHelpers.HousekeepingType.ToString(), TestHelpers.OfficeType.ToString() }, types.Select(t => t.GetProperty("id").GetString()));
        Assert.Equal(new[] { 1, 4 }, types.Select(t => t.GetProperty("count").GetInt32()));
        Assert.Equal(2, json.GetProperty("stats").GetProperty("employeeTypes").GetInt32());
    }

    [Fact]
    public async Task Staff_rows_carry_their_employee_type_and_shift_ids()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData("505", null, null));
        var row = Assert.Single(json.GetProperty("staff").EnumerateArray());

        Assert.Equal(TestHelpers.HousekeepingType.ToString(), row.GetProperty("employeeTypeId").GetString());
        Assert.Equal("Housekeeping", row.GetProperty("employeeTypeName").GetString());
        Assert.Equal(TestHelpers.HousekeepingDept.ToString(), row.GetProperty("departmentId").GetString());
        Assert.Equal("Cleaner", row.GetProperty("departmentName").GetString());   // from dbo.departments
        Assert.Equal(TestHelpers.RetiredShift.ToString(), row.GetProperty("shiftId").GetString());
        Assert.Equal("10:00 PM to 06:00 AM", row.GetProperty("shiftName").GetString());   // from dbo.Shift
    }

    [Theory]
    [InlineData(TestHelpers.OfficeType, new[] { 504, 503, 502, 501 })]
    [InlineData(TestHelpers.HousekeepingType, new[] { 505 })]
    [InlineData(77, new int[0])]
    public async Task Employee_type_filter_returns_only_that_type(int type, int[] expected)
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null, type.ToString()));
        Assert.Equal(expected, Ids(json));
        Assert.Equal(5, json.GetProperty("stats").GetProperty("total").GetInt32());   // the totals still cover everyone
    }

    // ── dbo.departments and dbo.Shift ─────────────────────────────

    [Fact]
    public async Task Staff_categories_come_from_dbo_departments()
    {
        using var ctx = SeededContext();
        ctx.Departments.Add(new IMSB.Dto.Models.Department { Id = 9050, Name = "Accounts" });
        ctx.EmployeeDesignations.Add(new IMSB.Dto.Models.EmployeeDesignation { DesigId = 9060, Name = "Only In SERP" });
        ctx.SaveChanges();

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));

        var depts = json.GetProperty("depts").EnumerateArray().ToList();
        Assert.Equal(new[] { "Accounts", "Admin", "Cleaner", "Sales" }, depts.Select(d => d.GetProperty("name").GetString()));   // by name
        Assert.Equal("9050", depts[0].GetProperty("id").GetString());
        Assert.DoesNotContain(depts, d => d.GetProperty("name").GetString() == "Only In SERP");   // tbl_Designation is not used
    }

    [Fact]
    public async Task Shifts_come_from_dbo_shift_in_display_order_without_the_retired_ones()
    {
        using var ctx = SeededContext();
        ctx.Shifts.Add(new IMSB.Dto.Models.Shift { Id = 20, Code = "EARLY", Name = "Early (6AM-2PM)", DisplayOrder = 0 });   // no longer offered
        ctx.Shifts.Add(new IMSB.Dto.Models.Shift { Id = 21, Code = "FIRST", Name = "First", DisplayOrder = 1 });
        ctx.EmployeeShifts.Add(new IMSB.Dto.Models.EmployeeShift { ShiftId = 99, ShiftTime = "Only In SERP", EmpTypeId = TestHelpers.OfficeType });
        ctx.SaveChanges();
        foreach (var s in ctx.Shifts.Where(s => s.Id == TestHelpers.MorningShift || s.Id == TestHelpers.EveningShift)) s.DisplayOrder += 1;
        ctx.SaveChanges();

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));

        var shifts = json.GetProperty("shifts").EnumerateArray().ToList();
        Assert.Equal(new[] { "21", TestHelpers.MorningShift.ToString(), TestHelpers.EveningShift.ToString() }, shifts.Select(s => s.GetProperty("id").GetString()));
        Assert.Equal(new[] { "First", "Morning (8AM-4PM)", "Evening (4PM-10PM)" }, shifts.Select(s => s.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task A_shift_no_longer_offered_still_shows_its_name_on_the_staff_row()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData("505", null, null));
        var row = Assert.Single(json.GetProperty("staff").EnumerateArray());

        Assert.Equal(TestHelpers.RetiredShift.ToString(), row.GetProperty("shiftId").GetString());
        Assert.Equal("10:00 PM to 06:00 AM", row.GetProperty("shiftName").GetString());
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("=SUM(A1:A9)", "'=SUM(A1:A9)")]       // CSV/formula injection neutralised
    [InlineData("+911234", "'+911234")]
    [InlineData("-5", "'-5")]
    [InlineData("@cmd", "'@cmd")]
    public void CsvEscape_quotes_and_neutralises_formulas(string? input, string expected)
    {
        Assert.Equal(expected, IMSB.Common.CommonHelpers.CsvEscape(input));
    }
}
