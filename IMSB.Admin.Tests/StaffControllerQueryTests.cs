using System.Text;
using IMSB.Admin.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

public class StaffControllerQueryTests
{
    private static IMSB.Dto.Models.IMSBContext SeededContext()
    {
        var ctx = TestHelpers.CreateContext();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.Staff.AddRange(
            TestHelpers.ExistingStaff("EMP001", "Ramesh Kumar", "9876543210", status: "ON_LEAVE", createdAt: t0),
            TestHelpers.ExistingStaff("EMP002", "Suresh Reddy", "9876543211", status: "Active", createdAt: t0.AddDays(1)),   // legacy mixed case
            TestHelpers.ExistingStaff("EMP003", "Lakshmi Devi", "9876543212", status: "ACTIVE", dept: TestHelpers.AdminDept, createdAt: t0.AddDays(2)),
            TestHelpers.ExistingStaff("EMP004", "Kiran Kumar", "9876543213", status: "Inactive", dept: TestHelpers.AdminDept, createdAt: t0.AddDays(3)));
        var expiring = TestHelpers.ExistingStaff("EMP005", "Contract Person", "9876543214", createdAt: t0.AddDays(4));
        expiring.ContractEndDate = DateTime.Today.AddDays(10);
        ctx.Staff.Add(expiring);
        ctx.SaveChanges();
        return ctx;
    }

    private static List<string> Ids(System.Text.Json.JsonElement json) =>
        json.GetProperty("staff").EnumerateArray().Select(s => s.GetProperty("employeeId").GetString()!).ToList();

    [Fact]
    public async Task IndexData_returns_all_staff_newest_first_with_reference_data()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, null, null));

        Assert.Equal(new[] { "EMP005", "EMP004", "EMP003", "EMP002", "EMP001" }, Ids(json));
        Assert.Equal(2, json.GetProperty("depts").GetArrayLength());
        Assert.Equal(new[] { "MORNING", "EVENING" }, json.GetProperty("shifts").EnumerateArray().Select(s => s.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task IndexData_staff_rows_have_display_fields()
    {
        using var ctx = SeededContext();
        ctx.Staff.Single(s => s.EmployeeId == "EMP003").MonthlySalary = 27000m;
        await ctx.SaveChangesAsync();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData("EMP003", null, null));
        var row = json.GetProperty("staff")[0];

        Assert.Equal("Lakshmi Devi", row.GetProperty("fullName").GetString());
        Assert.Equal("Admin", row.GetProperty("departmentName").GetString());
        Assert.Equal("1988-03-03", row.GetProperty("dateOfBirth").GetString());
        Assert.Equal("2015-06-01", row.GetProperty("joinDate").GetString());
        Assert.Equal(27000m, row.GetProperty("monthlySalary").GetDecimal());
    }

    [Fact]
    public async Task IndexData_does_not_expose_salary_to_staff_role()
    {
        using var ctx = SeededContext();
        ctx.Staff.Single(s => s.EmployeeId == "EMP003").MonthlySalary = 27000m;
        await ctx.SaveChangesAsync();

        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx, role: "Staff").IndexData(null, null, null));

        Assert.All(json.GetProperty("staff").EnumerateArray(), row =>
            Assert.Equal(System.Text.Json.JsonValueKind.Null, row.GetProperty("monthlySalary").ValueKind));
    }

    [Theory]
    [InlineData("kumar", new[] { "EMP004", "EMP001" })]       // name, case-insensitive
    [InlineData("RAMESH", new[] { "EMP001" })]
    [InlineData("9876543212", new[] { "EMP003" })]            // mobile
    [InlineData("emp002", new[] { "EMP002" })]                // employee id
    [InlineData("  Suresh  ", new[] { "EMP002" })]            // trimmed
    [InlineData("nobody", new string[0])]
    public async Task Search_matches_name_mobile_or_employee_id(string q, string[] expected)
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(q, null, null));
        Assert.Equal(expected, Ids(json));
    }

    [Fact]
    public async Task Department_filter_returns_only_that_department()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, TestHelpers.AdminDept.ToString(), null));
        Assert.Equal(new[] { "EMP004", "EMP003" }, Ids(json));
    }

    [Fact]
    public async Task Invalid_department_filter_is_ignored()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData(null, "not-a-guid", null));
        Assert.Equal(5, Ids(json).Count);
    }

    [Theory]
    [InlineData("ACTIVE", new[] { "EMP005", "EMP003", "EMP002" })]   // includes legacy "Active"
    [InlineData("active", new[] { "EMP005", "EMP003", "EMP002" })]
    [InlineData("ON_LEAVE", new[] { "EMP001" })]
    [InlineData("INACTIVE", new[] { "EMP004" })]                     // legacy "Inactive"
    public async Task Status_filter_is_case_insensitive(string status, string[] expected)
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
        Assert.Equal(new[] { "EMP004" }, Ids(json));
    }

    [Fact]
    public async Task Stats_and_department_counts_cover_all_staff_regardless_of_filters()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await TestHelpers.CreateController(ctx).IndexData("nobody", null, null));
        var stats = json.GetProperty("stats");

        Assert.Equal(5, stats.GetProperty("total").GetInt32());
        Assert.Equal(3, stats.GetProperty("active").GetInt32());
        Assert.Equal(1, stats.GetProperty("onLeave").GetInt32());
        Assert.Equal(2, stats.GetProperty("departments").GetInt32());
        Assert.Equal(1, stats.GetProperty("contractExpiring").GetInt32());
        var counts = json.GetProperty("depts").EnumerateArray().ToDictionary(d => d.GetProperty("name").GetString()!, d => d.GetProperty("count").GetInt32());
        Assert.Equal(3, counts["Sales"]);
        Assert.Equal(2, counts["Admin"]);
    }

    [Fact]
    public async Task Index_view_gets_the_same_stats()
    {
        using var ctx = SeededContext();
        var view = Assert.IsType<ViewResult>(await TestHelpers.CreateController(ctx).Index());

        Assert.Equal(5, view.ViewData["TotalStaff"]);
        Assert.Equal(3, view.ViewData["ActiveStaff"]);
        Assert.Equal(1, view.ViewData["OnLeave"]);
        Assert.Equal(2, view.ViewData["Departments"]);
        Assert.Equal(1, view.ViewData["ContractExpiring"]);
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
        Assert.Equal("EmployeeId,FullName,Department,Designation,StaffCategory,Mobile,Email,DateOfBirth,Gender,JoinDate,Shift,Status", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("EMP004,Kiran Kumar,Admin,", lines[1]);
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
