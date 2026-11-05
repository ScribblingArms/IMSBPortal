using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

/// <summary>Separate app instance (own in-memory database) for the OT Assignment HTTP tests.</summary>
public class OtAssignmentsAppFactory : StaffAppFactory { }

public class OtAssignmentsIntegrationTests : IClassFixture<OtAssignmentsAppFactory>
{
    private readonly OtAssignmentsAppFactory _factory;
    private static bool _seeded;
    private static readonly object SeedLock = new();
    private static readonly Guid StaffId = Guid.NewGuid();

    public OtAssignmentsIntegrationTests(OtAssignmentsAppFactory factory)
    {
        _factory = factory;
        lock (SeedLock)
        {
            if (_seeded) return;
            factory.Seed(ctx =>
            {
                ctx.Departments.Add(new Department { Id = TestHelpers.SalesDept, Name = "Sales" });
                var staff = TestHelpers.ExistingStaff("EMP801", "Http Ot Tester", "900000801");
                staff.Id = StaffId;
                ctx.Staff.Add(staff);
            });
            _seeded = true;
        }
    }

    private static async Task<string> Token(HttpClient client)
    {
        var html = await client.GetStringAsync("/OtAssignments/Index");
        var match = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        Assert.True(match.Success, "antiforgery token not found on the OT Assignment page");
        return match.Groups[1].Value;
    }

    private static HttpRequestMessage Post(string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("RequestVerificationToken", token);
        if (body != null) request.Content = JsonContent.Create(body);
        return request;
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Inventory", HttpStatusCode.Forbidden)]
    [InlineData("Staff", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task Screen_and_data_are_admin_only(string? role, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/OtAssignments/Index")).StatusCode);
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/OtAssignments/Data")).StatusCode);
    }

    [Fact]
    public async Task Page_renders_the_table_form_and_script()
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin").GetStringAsync("/OtAssignments/Index"));

        Assert.Contains("id=\"otTable\"", html);
        Assert.Contains("id=\"otFormModal\"", html);
        Assert.Contains("id=\"otViewModal\"", html);
        Assert.Contains("id=\"otFrom\"", html);
        Assert.Contains("id=\"otTo\"", html);
        Assert.Equal(2, Regex.Matches(html, "type=\"datetime-local\"").Count);
        Assert.Contains("window.OtAssignmentI18n", html);
        Assert.Contains("/js/ot-assignments.js", html);
        var headers = Regex.Matches(html, "<th>([^<]*)</th>").Select(m => m.Groups[1].Value.Trim()).ToList();
        Assert.Equal(new[] { "Staff ID", "Name", "Staff Category", "Designation", "OT From", "OT To", "Actions" }, headers);
    }

    [Fact]
    public async Task Menu_item_sits_under_the_hr_module_for_admins_only()
    {
        var admin = await _factory.ClientAs("Admin").GetStringAsync("/Staff/Index");
        var submenu = admin.IndexOf("id=\"hrSubmenu\"", StringComparison.Ordinal);
        Assert.True(submenu >= 0 && admin.IndexOf("data-page=\"/OtAssignments/Index\"", submenu, StringComparison.Ordinal) > submenu);

        var staff = await _factory.ClientAs("Staff").GetStringAsync("/Staff/Index");
        Assert.DoesNotContain("/OtAssignments/Index", staff);
    }

    [Theory]
    [InlineData("hi-IN", "ओटी असाइनमेंट")]
    [InlineData("te-IN", "ఓటీ కేటాయింపు")]
    [InlineData("ta-IN", "மிகைநேர ஒதுக்கீடு")]
    public async Task Page_is_localized(string culture, string title)
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin", culture).GetStringAsync("/OtAssignments/Index"));
        Assert.Contains(title, html);
    }

    [Fact]
    public async Task Add_view_update_delete_round_trip_over_http()
    {
        var client = _factory.ClientAs("Admin");
        var token = await Token(client);

        // add
        var created = await client.SendAsync(Post("/OtAssignments/Create", token, new { staffId = StaffId, fromDateTime = "2026-11-05T18:00", toDateTime = "2026-11-05T20:00" }));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        // view
        var data = await client.GetFromJsonAsync<JsonElement>("/OtAssignments/Data");
        var row = Assert.Single(data.GetProperty("rows").EnumerateArray());
        Assert.Equal("Http Ot Tester", row.GetProperty("staffName").GetString());
        Assert.Equal("2026-11-05T18:00", row.GetProperty("fromDateTime").GetString());
        Assert.Empty(data.GetProperty("staff").EnumerateArray());
        var id = row.GetProperty("id").GetString();

        // a second one for the same person is refused
        var duplicate = await client.SendAsync(Post("/OtAssignments/Create", token, new { staffId = StaffId, fromDateTime = "2026-11-06T06:00", toDateTime = "2026-11-06T07:00" }));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        // update
        var updated = await client.SendAsync(Post($"/OtAssignments/Update/{id}", token, new { fromDateTime = "2026-11-07T19:00", toDateTime = "2026-11-07T22:30" }));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        data = await client.GetFromJsonAsync<JsonElement>("/OtAssignments/Data");
        row = Assert.Single(data.GetProperty("rows").EnumerateArray());
        Assert.Equal(("2026-11-07T19:00", "2026-11-07T22:30"), (row.GetProperty("fromDateTime").GetString(), row.GetProperty("toDateTime").GetString()));

        // invalid update is refused with the field's error
        var invalid = await client.SendAsync(Post($"/OtAssignments/Update/{id}", token, new { fromDateTime = "2026-11-07T22:00", toDateTime = "2026-11-07T21:00" }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var errors = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OT To time must be later than OT From time", errors.GetProperty("errors").GetProperty("to_datetime").GetString());

        // delete
        var deleted = await client.SendAsync(Post($"/OtAssignments/Delete/{id}", token));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        data = await client.GetFromJsonAsync<JsonElement>("/OtAssignments/Data");
        Assert.Empty(data.GetProperty("rows").EnumerateArray());
        Assert.Single(data.GetProperty("staff").EnumerateArray());

        // and it is gone
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Post($"/OtAssignments/Delete/{id}", token))).StatusCode);
    }

    [Theory]
    [InlineData("/OtAssignments/Create")]
    [InlineData("/OtAssignments/Update/00000000-0000-0000-0000-000000000001")]
    [InlineData("/OtAssignments/Delete/00000000-0000-0000-0000-000000000001")]
    public async Task Changes_without_antiforgery_token_are_rejected(string url)
    {
        var client = _factory.ClientAs("Admin");
        await Token(client);   // gets the cookie, but the header is missing below

        var response = await client.PostAsJsonAsync(url, new { staffId = StaffId, fromDateTime = "2026-11-05T18:00", toDateTime = "2026-11-05T20:00" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Staff_role_cannot_change_assignments()
    {
        var response = await _factory.ClientAs("Staff").PostAsJsonAsync("/OtAssignments/Create", new { staffId = StaffId, fromDateTime = "2026-11-05T18:00", toDateTime = "2026-11-05T20:00" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
