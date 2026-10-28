using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

public class LeavesAppFactory : StaffAppFactory { }

public class LeavesIntegrationTests : IClassFixture<LeavesAppFactory>
{
    private readonly LeavesAppFactory _factory;
    private static bool _seeded;
    private static readonly object SeedLock = new();

    public LeavesIntegrationTests(LeavesAppFactory factory)
    {
        _factory = factory;
        lock (SeedLock)
        {
            if (_seeded) return;
            factory.Seed(context =>
            {
                var staff = TestHelpers.ExistingStaff("EMP951", "Leave Page Tester", "900000951");
                staff.StaffCategory = "Operators";
                context.Staff.Add(staff);
                context.LeaveEntitlements.Add(new LeaveEntitlement
                {
                    StaffId = staff.Id,
                    StaffCategory = "Operators",
                    LeaveType = "Casual",
                    Year = DateTime.Today.Year,
                    AnnualQuota = 5m
                });
            });
            _seeded = true;
        }
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Inventory", HttpStatusCode.Forbidden)]
    [InlineData("Staff", HttpStatusCode.OK)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task Leave_screens_require_staff_or_admin(string? role, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/Leaves/Index")).StatusCode);
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/Leaves/Add")).StatusCode);
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/Leaves/CurrentBalances")).StatusCode);
    }

    [Fact]
    public async Task Pages_render_balance_search_and_leave_entry_form()
    {
        var index = await _factory.ClientAs("Admin").GetStringAsync("/Leaves/Index");
        Assert.DoesNotContain("Leave Entitlements", index);
        Assert.Contains("Current Leave Balances", index);
        Assert.Contains("balanceStaffFilter", index);
        Assert.Contains("balanceLeaveTypeFilter", index);
        Assert.Contains("id=\"searchLeaveBalances\"", index);
        Assert.DoesNotContain("balanceEmployeeTypeFilter", index);
        Assert.DoesNotContain("attendance_leave_type", index);
        Assert.DoesNotContain("Use for attendance Leave and Half Day deductions", index);
        Assert.Contains("/Leaves/CurrentBalances", index);
        Assert.Contains("Operators", index);
        Assert.Contains("Casual", index);
        Assert.Contains("/Leaves/Add", index);

        var add = await _factory.ClientAs("Staff").GetStringAsync("/Leaves/Add");
        Assert.Contains("Record Leave Taken", add);
        Assert.Contains("Leave Page Tester", add);
        Assert.Contains("id=\"leaveStaff\"", add);
        Assert.Contains("id=\"leaveType\"", add);
        Assert.Contains("id=\"leaveFrom\"", add);
        var futureDate = DateTime.Today.AddDays(1);
        Assert.Contains($"value=\"{futureDate:yyyy-MM-dd}\"", add);
        Assert.Contains($"value=\"{futureDate:yyyy-MM-dd}\"", add);
        Assert.Contains("id=\"leaveDays\" name=\"Days\" type=\"number\"", add);
        Assert.Contains("readonly", add);
        Assert.Contains("btn btn-primary btn-sm", add);
        Assert.Contains("register=True", index);

        var registerStaff = await _factory.ClientAs("Admin").GetStringAsync("/Staff?register=True");
        Assert.Contains("StaffOpenCreate = true", registerStaff);
    }

    [Fact]
    public async Task Current_balances_can_be_filtered_by_selected_employee()
    {
        var result = await _factory.ClientAs("Admin")
            .GetFromJsonAsync<System.Text.Json.JsonElement>("/Leaves/CurrentBalances?employeeId=EMP951");
        var rows = result.GetProperty("rows").EnumerateArray().ToList();

        var row = Assert.Single(rows);
        Assert.Equal("EMP951", row.GetProperty("employeeId").GetString());
    }

    [Fact]
    public async Task Current_balances_can_be_filtered_by_leave_type()
    {
        var result = await _factory.ClientAs("Admin")
            .GetFromJsonAsync<System.Text.Json.JsonElement>("/Leaves/CurrentBalances?leaveType=Casual");
        var rows = result.GetProperty("rows").EnumerateArray().ToList();

        var row = Assert.Single(rows);
        Assert.Equal("Casual", row.GetProperty("leaveType").GetString());
    }

    [Fact]
    public async Task Staff_can_record_leave_through_antiforgery_protected_form()
    {
        var client = _factory.ClientAs("Admin");
        var html = await client.GetStringAsync("/Leaves/Add");
        var match = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        Assert.True(match.Success, "antiforgery token not found on the leave page");

        var response = await client.PostAsync("/Leaves/Record", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = match.Groups[1].Value,
            ["StaffId"] = ReadStaffId(html),
            ["LeaveType"] = "Casual",
            ["FromDate"] = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"),
            ["ToDate"] = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd"),
            ["Days"] = "2",
            ["Remarks"] = "integration test"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var balance = await client.GetStringAsync("/Leaves/Index");
        Assert.Contains("Leave entry saved.", balance);
        Assert.Contains(">3<", balance);
    }

    private static string ReadStaffId(string html)
    {
        var match = Regex.Match(html, @"<option value=""([^""]+)"" data-category=""Operators"">EMP951");
        Assert.True(match.Success, "staff option was not rendered");
        return match.Groups[1].Value;
    }
}
