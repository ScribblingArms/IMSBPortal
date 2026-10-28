using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

/// <summary>Separate app instance (own in-memory database) for the attendance HTTP tests.</summary>
public class AttendanceAppFactory : StaffAppFactory { }

public class AttendanceIntegrationTests : IClassFixture<AttendanceAppFactory>
{
    private readonly AttendanceAppFactory _factory;
    private static bool _seeded;
    private static readonly object SeedLock = new();
    private static readonly string Day = AttendanceTestHelpers.Day.ToString("yyyy-MM-dd");

    public AttendanceIntegrationTests(AttendanceAppFactory factory)
    {
        _factory = factory;
        lock (SeedLock)
        {
            if (_seeded) return;
            factory.Seed(ctx =>
            {
                ctx.Departments.AddRange(new Department { Id = TestHelpers.SalesDept, Name = "Sales" }, new Department { Id = TestHelpers.AdminDept, Name = "Admin" });
                ctx.Shifts.Add(new Shift { Code = "MORNING", Name = "Morning (8AM-4PM)" });
                var meena = TestHelpers.ExistingStaff("EMP101", "Http Meena", "900000101", dept: TestHelpers.SalesDept);
                meena.StaffCategory = "Operators";
                ctx.Staff.Add(meena);
                ctx.Staff.Add(TestHelpers.ExistingStaff("EMP102", "Http Ravi", "900000102", dept: TestHelpers.AdminDept));
                ctx.LeaveEntitlements.Add(new LeaveEntitlement
                {
                    StaffId = meena.Id,
                    StaffCategory = "Operators",
                    LeaveType = "Casual",
                    Year = DateTime.Today.Year,
                    AnnualQuota = 5m,
                    IsAttendanceLeaveType = true
                });
            });
            _seeded = true;
        }
    }

    private static async Task<string> Token(HttpClient client)
    {
        var html = await client.GetStringAsync("/Attendance/Index");
        var m = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        Assert.True(m.Success, "antiforgery token not found on the Attendance page");
        return m.Groups[1].Value;
    }

    private static HttpRequestMessage Post(string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("RequestVerificationToken", token);
        if (body != null) req.Content = JsonContent.Create(body);
        return req;
    }

    [Theory]
    [InlineData("/Attendance")]
    [InlineData("/Attendance/Index")]
    [InlineData("/Attendance/Data")]
    [InlineData("/Attendance/History")]
    [InlineData("/Attendance/Export")]
    [InlineData("/Attendance/MonthlyCalendar")]
    [InlineData("/Attendance/Monthly")]
    [InlineData("/Attendance/MonthlyExport")]
    public async Task Anonymous_users_are_challenged(string url)
    {
        var res = await _factory.ClientAs(null).GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("Staff", HttpStatusCode.OK)]
    [InlineData("Inventory", HttpStatusCode.Forbidden)]
    public async Task Only_staff_and_admin_roles_can_use_attendance(string role, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/Attendance/Index")).StatusCode);
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/Attendance/MonthlyCalendar")).StatusCode);
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/Attendance/Data")).StatusCode);
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/Attendance/Monthly")).StatusCode);
    }

    [Fact]
    public async Task Page_lists_real_departments_and_no_sample_data()
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin").GetStringAsync("/Attendance/Index"));

        Assert.Contains($"<option value=\"{TestHelpers.SalesDept}\">Sales</option>", html);
        Assert.Contains($"<option value=\"{TestHelpers.AdminDept}\">Admin</option>", html);
        Assert.DoesNotContain("Housekeeping", html);          // was a hard-coded sample department
        Assert.DoesNotContain("5 – 11 May 2025", html);       // was a hard-coded sample week
        Assert.Contains("/js/attendance.js", html);
        Assert.Contains("/css/attendance.css", html);
        Assert.Contains("id=\"attEditModal\"", html);
        Assert.DoesNotContain("data-tab=\"monthly\"", html);
        Assert.DoesNotContain("id=\"monthlyMonth\"", html);
        Assert.Contains("data-page=\"/Attendance/MonthlyCalendar\"", html);
    }

    [Fact]
    public async Task Monthly_calendar_is_linked_under_reports()
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin").GetStringAsync("/Attendance/MonthlyCalendar"));

        Assert.Contains("Monthly Attendance Calendar", html);
        Assert.Contains("id=\"monthlyMonth\"", html);
        Assert.Contains("id=\"monthlyYear\"", html);
        Assert.Contains("id=\"monthlyType\"", html);
        Assert.Contains("id=\"monthlySearch\"", html);
        Assert.Contains("id=\"monthlyStatus\"", html);
        Assert.Contains("id=\"monthlyResetBtn\"", html);
        Assert.Contains("id=\"monthlyCount\"", html);
        Assert.Contains("att-monthly-report-header", html);
        Assert.Contains("att-monthly-tools", html);
        Assert.Contains("id=\"monthlyExportBtn\"", html);
        Assert.DoesNotContain("id=\"monthlyPdfBtn\"", html);
        Assert.Contains("/js/attendance-monthly.js", html);

        var abstractReportLink = html.IndexOf("data-page=\"/Reports/AbstractReport\"", StringComparison.Ordinal);
        var monthlyLink = html.IndexOf("data-page=\"/Attendance/MonthlyCalendar\"", StringComparison.Ordinal);
        Assert.True(abstractReportLink >= 0 && monthlyLink > abstractReportLink);
    }

    [Fact]
    public async Task Staff_can_find_monthly_calendar_under_reports_without_admin_report_links()
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Staff").GetStringAsync("/Attendance/MonthlyCalendar"));

        Assert.Contains("data-page=\"/Attendance/MonthlyCalendar\"", html);
        Assert.DoesNotContain("data-page=\"/Reports/Index\"", html);
    }

    [Fact]
    public async Task Monthly_calendar_shows_unmarked_days_for_a_month_without_attendance()
    {
        const string month = "2099-02";
        var json = await _factory.ClientAs("Admin").GetFromJsonAsync<JsonElement>($"/Attendance/Monthly?month={month}");

        Assert.Equal(month, json.GetProperty("month").GetString());
        Assert.Equal(28, json.GetProperty("days").GetArrayLength());
        var rows = json.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.All(row.GetProperty("days").EnumerateArray(),
            status => Assert.Equal(JsonValueKind.Null, status.ValueKind)));
    }

    [Theory]
    [InlineData("en-US", "Attendance Management", "Mark All Present", "Half Day")]
    [InlineData("ta-IN", "வருகை மேலாண்மை", "அனைவரையும் வருகை எனக் குறி", "அரை நாள்")]
    [InlineData("te-IN", "హాజరుకాల నిర్వహణ", "అందరినీ హాజరుగా గుర్తించు", "అర్ధ రోజు")]
    [InlineData("hi-IN", "उपस्थिति प्रबंधन", "सभी को उपस्थित चिह्नित करें", "आधा दिन")]
    public async Task Page_is_localized(string culture, string title, string markAll, string halfDay)
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin", culture).GetStringAsync("/Attendance/Index"));
        Assert.Contains(title, html);
        Assert.Contains(markAll, html);
        Assert.Contains(halfDay, html);
        Assert.Contains("window.AttendanceI18n", html);
    }

    [Fact]
    public async Task Mutations_without_antiforgery_token_are_rejected()
    {
        var client = _factory.ClientAs("Admin");
        await Token(client);   // gets the cookie, but the header is missing below
        var res = await client.PostAsJsonAsync("/Attendance/Save", AttendanceTestHelpers.Sheet(AttendanceTestHelpers.Day, AttendanceTestHelpers.E("EMP101", "ABSENT")));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/Attendance/Delete/{Guid.NewGuid()}", null)).StatusCode);
    }

    [Fact]
    public async Task Inventory_role_cannot_save_attendance()
    {
        var res = await _factory.ClientAs("Inventory").PostAsJsonAsync("/Attendance/Save", AttendanceTestHelpers.Sheet(AttendanceTestHelpers.Day));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Save_view_edit_export_delete_round_trip_over_http()
    {
        var client = _factory.ClientAs("Staff");
        var token = await Token(client);

        // save the sheet (Ravi has no out-time yet)
        var save = await client.SendAsync(Post("/Attendance/Save", token, AttendanceTestHelpers.Sheet(AttendanceTestHelpers.Day,
            AttendanceTestHelpers.E("EMP101", "PRESENT", "08:00", "17:00"),
            AttendanceTestHelpers.E("EMP102", "PRESENT", "09:15"))));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        // the day's sheet shows it
        var data = await client.GetFromJsonAsync<JsonElement>($"/Attendance/Data?date={Day}");
        var meena = data.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeeId").GetString() == "EMP101").GetProperty("record");
        Assert.Equal(9m, meena.GetProperty("workedHours").GetDecimal());
        Assert.Equal(1m, meena.GetProperty("overtimeHours").GetDecimal());
        var raviId = data.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeeId").GetString() == "EMP102")
            .GetProperty("record").GetProperty("id").GetString()!;

        // the department filter narrows the sheet
        var admin = await client.GetFromJsonAsync<JsonElement>($"/Attendance/Data?date={Day}&departmentId={TestHelpers.AdminDept}");
        Assert.Equal("EMP102", Assert.Single(admin.GetProperty("rows").EnumerateArray()).GetProperty("employeeId").GetString());

        // validation errors come back keyed by row and field
        var bad = await client.SendAsync(Post($"/Attendance/Update/{raviId}", token, new AttendanceUpdateDto { Status = "PRESENT", InTime = "09:15", OutTime = "08:00" }));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("Out-time must be after the in-time", (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("out_time").GetString());

        // edit one record
        var upd = await client.SendAsync(Post($"/Attendance/Update/{raviId}", token, new AttendanceUpdateDto { Status = "PRESENT", InTime = "09:15", OutTime = "18:45", Remarks = "audit" }));
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);
        var view = await client.GetFromJsonAsync<JsonElement>($"/Attendance/Get/{raviId}");
        Assert.Equal(9.5m, view.GetProperty("workedHours").GetDecimal());
        Assert.Equal("audit", view.GetProperty("remarks").GetString());

        // history with filters
        var hist = await client.GetFromJsonAsync<JsonElement>($"/Attendance/History?from={Day}&to={Day}&q=ravi&status=PRESENT");
        Assert.Equal("EMP102", Assert.Single(hist.GetProperty("rows").EnumerateArray()).GetProperty("employeeId").GetString());

        // csv export
        var csv = ExcelExportTestHelpers.CsvText(await client.GetStringAsync($"/Attendance/Export?from={Day}&to={Day}&departmentId={TestHelpers.AdminDept}"));
        Assert.Contains($"{Day},EMP102,Http Ravi,Admin,Executive,09:15,18:45,9.5,1.5,Present,audit,tester", csv);
        Assert.DoesNotContain("EMP101", csv);

        // delete
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Attendance/Delete/{raviId}", token))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Attendance/Get/{raviId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Post($"/Attendance/Delete/{raviId}", token))).StatusCode);
    }

    [Fact]
    public async Task Leave_balance_endpoint_reflects_attendance_add_and_status_updates_immediately()
    {
        var client = _factory.ClientAs("Staff");
        var token = await Token(client);
        var save = await client.SendAsync(Post("/Attendance/Save", token,
            AttendanceTestHelpers.Sheet(DateTime.Today, AttendanceTestHelpers.E("EMP101", "LEAVE"))));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var data = await client.GetFromJsonAsync<JsonElement>($"/Attendance/Data?date={DateTime.Today:yyyy-MM-dd}");
        var record = data.GetProperty("rows").EnumerateArray()
            .Single(row => row.GetProperty("employeeId").GetString() == "EMP101").GetProperty("record");
        var id = record.GetProperty("id").GetString();
        await AssertBalance(client, 1m, 4m);

        var present = await client.SendAsync(Post($"/Attendance/Update/{id}", token,
            new AttendanceUpdateDto { Status = "PRESENT", InTime = "08:00" }));
        Assert.Equal(HttpStatusCode.OK, present.StatusCode);
        await AssertBalance(client, 0m, 5m);

        var halfDay = await client.SendAsync(Post($"/Attendance/Update/{id}", token,
            new AttendanceUpdateDto { Status = "HALF_DAY", InTime = "08:00", OutTime = "12:00" }));
        Assert.Equal(HttpStatusCode.OK, halfDay.StatusCode);
        await AssertBalance(client, 0.5m, 4.5m);
    }

    private static async Task AssertBalance(HttpClient client, decimal taken, decimal balance)
    {
        var result = await client.GetFromJsonAsync<JsonElement>("/Leaves/CurrentBalances");
        var row = result.GetProperty("rows").EnumerateArray()
            .Single(item => item.GetProperty("employeeId").GetString() == "EMP101");
        Assert.Equal(5m, row.GetProperty("entitlement").GetDecimal());
        Assert.Equal(taken, row.GetProperty("taken").GetDecimal());
        Assert.Equal(balance, row.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Server_validation_messages_follow_the_culture_cookie()
    {
        var client = _factory.ClientAs("Admin", "ta-IN");
        var token = await Token(client);

        var res = await client.SendAsync(Post("/Attendance/Save", token, AttendanceTestHelpers.Sheet(AttendanceTestHelpers.Day,
            AttendanceTestHelpers.E("EMP101", "PRESENT", "8:00"))));   // fails [RegularExpression] on the DTO

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("சரியான நேரத்தை உள்ளிடவும் (HH:mm)", errors.GetProperty("entries_0_in_time").GetString());
    }

    [Fact]
    public async Task Malformed_json_is_a_bad_request()
    {
        var client = _factory.ClientAs("Admin");
        var token = await Token(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/Attendance/Save") { Content = new StringContent("{ \"date\": ", System.Text.Encoding.UTF8, "application/json") };
        req.Headers.Add("RequestVerificationToken", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(req)).StatusCode);
    }
}
