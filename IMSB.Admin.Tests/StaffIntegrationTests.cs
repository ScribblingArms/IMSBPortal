using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IMSB.Admin.Tests;

/// <summary>
/// Hosts the real app (routing, auth filters, antiforgery, views, localization) with an in-memory database
/// and a header-driven test login, so the Staff page and endpoints are exercised over HTTP.
/// </summary>
public class StaffAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "staff-integration-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("SeedDemoData", "false");   // tests seed their own data
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<IMSBContext>>();
            services.AddDbContext<IMSBContext>(o => o.UseInMemoryDatabase(_dbName));
            // the Staff set is a SQL view over tbl_Employee in production; this context plays the view in memory
            services.RemoveAll<IMSBContext>();
            services.AddScoped<IMSBContext>(sp => new StaffViewTestContext(sp.GetRequiredService<DbContextOptions<IMSBContext>>()));

            services.AddAuthentication(o =>
            {
                o.DefaultScheme = TestAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public void Seed(Action<IMSBContext> seed)
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IMSBContext>();
        seed(ctx);
        ctx.SaveChanges();
    }

    public HttpClient ClientAs(string? role, string? culture = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role != null) client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        if (culture != null) client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Culture=c={culture}|uic={culture}");
        return client;
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList()) services.Remove(d);
    }
}

/// <summary>Authenticates a request as "tester" in the role given by the X-Test-Role header.</summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var role)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tester"), new Claim(ClaimTypes.Role, role.ToString()) }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public class StaffIntegrationTests : IClassFixture<StaffAppFactory>
{
    private readonly StaffAppFactory _factory;
    private static bool _seeded;
    private static readonly object SeedLock = new();

    public StaffIntegrationTests(StaffAppFactory factory)
    {
        _factory = factory;
        lock (SeedLock)
        {
            if (_seeded) return;
            factory.Seed(ctx =>
            {
                ctx.Departments.Add(new Department { Id = TestHelpers.SalesDept, Name = "Sales" });
                ctx.Departments.Add(new Department { Id = TestHelpers.AdminDept, Name = "Admin" });
                ctx.Shifts.Add(new Shift { Id = TestHelpers.MorningShift, Code = "MORNING", Name = "Morning (8AM-4PM)", DisplayOrder = 1 });
                ctx.Shifts.Add(new Shift { Id = TestHelpers.EveningShift, Code = "EVENING", Name = "Evening (4PM-10PM)", DisplayOrder = 2 });
                TestHelpers.SeedStaffMasters(ctx);
                TestHelpers.SeedSerpMasters(ctx);
                StaffExtras.SeedLeaveTypes(ctx);
                var seeded = TestHelpers.ExistingEmployee(501, "Seeded Employee", "9000000001");
                seeded.ImageBase64 = Convert.ToBase64String(StaffExtras.Jpeg);
                ctx.Employees.Add(seeded);
            });
            _seeded = true;
        }
    }

    private static async Task<string> AntiforgeryToken(HttpClient client)
    {
        var html = await client.GetStringAsync("/Staff/Index");
        var m = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        Assert.True(m.Success, "antiforgery token not found on the Staff page");
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
    [InlineData("/Staff/Index")]
    [InlineData("/Staff/IndexData")]
    [InlineData("/Staff/Export")]
    public async Task Anonymous_users_are_challenged(string url)
    {
        var res = await _factory.ClientAs(null).GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("Staff", HttpStatusCode.OK)]
    [InlineData("Inventory", HttpStatusCode.Forbidden)]
    public async Task Only_staff_and_admin_roles_can_open_the_staff_page(string role, HttpStatusCode expected)
    {
        var res = await _factory.ClientAs(role).GetAsync("/Staff/IndexData");
        Assert.Equal(expected, res.StatusCode);
    }

    [Fact]
    public async Task Staff_add_form_has_the_tbl_employee_fields()
    {
        var html = System.Net.WebUtility.HtmlDecode(await _factory.ClientAs("Admin").GetStringAsync("/Staff/Index"));

        // employee type (tbl_EMP_Types) and department (tbl_Designation) are required dropdowns; shift (tbl_Shifts) is optional
        Assert.Matches(@"for=""staffEmployeeType"">Employee Type\s*<span class=""req"">", html);
        Assert.Matches(@"<select id=""staffEmployeeType"" name=""employee_type_id"" class=""form-control"" required>", html);
        Assert.Matches(@"for=""staffDepartment"">Staff Category\s*<span class=""req""", html);
        Assert.DoesNotContain("required", Regex.Match(html, @"<select id=""staffShift""[^>]*>").Value);
        Assert.Contains("id=\"staffFilterEmployeeType\"", html);
        // the type comes before the department, which follows it
        Assert.True(html.IndexOf("id=\"staffEmployeeType\"", StringComparison.Ordinal) < html.IndexOf("id=\"staffDepartment\"", StringComparison.Ordinal));
        // agency (tbl_ArtisansTypeAgency) is optional
        Assert.Matches(@"for=""staffAgency"">Agency</label>", html);
        Assert.DoesNotContain("required", Regex.Match(html, @"<select id=""staffAgency""[^>]*>").Value);
        Assert.Contains("id=\"staffRemarks\"", html);
        // tbl_Employee has none of these
        foreach (var gone in new[] { "staffEmail", "staffDOB" })
            Assert.DoesNotContain($"id=\"{gone}\"", html);
        Assert.DoesNotContain("value=\"ON_LEAVE\"", html);
    }

    [Fact]
    public async Task Staff_page_uses_prototype_styles_without_bootstrap()
    {
        var html = await _factory.ClientAs("Admin").GetStringAsync("/Staff/Index");
        var staffHtml = await _factory.ClientAs("Staff").GetStringAsync("/Staff/Index");

        Assert.Contains("/css/style.css", html);
        Assert.Contains("/css/forms.css", html);
        Assert.DoesNotContain("bootstrap.min.css", html);
        Assert.Contains("class=\"form-grid col-2\"", html);
        Assert.Contains("id=\"addStaffModal\"", html);
        // Add Staff sits in the Staff Directory card header; Export stays in the markup but hidden
        var addStaffBtn = html.IndexOf("class=\"btn staff-add-btn\" data-modal-open=\"addStaffModal\"", StringComparison.Ordinal);
        Assert.True(html.IndexOf("Staff Directory", StringComparison.Ordinal) < addStaffBtn);
        Assert.True(addStaffBtn < html.IndexOf("id=\"staffTable\"", StringComparison.Ordinal));
        Assert.Contains("id=\"staffExportBtn\" style=\"display:none\"", html);
        Assert.Contains("id=\"staffProfileModal\"", html);
        Assert.Contains("id=\"addStaffModal\"", staffHtml);
        // Department field is labelled Staff Category; the old free-text field is gone
        Assert.DoesNotContain("id=\"staffCategory\"", html);
        Assert.Matches(@"for=""staffDepartment"">Staff Category ", html);
        // OT timings have their own screen
        Assert.DoesNotContain("id=\"staffOtFrom\"", html);
        Assert.Single(Regex.Matches(html, "id=\"toast-container\""));
        Assert.Single(Regex.Matches(html, "/js/main\\.js"));
    }

    [Theory]
    [InlineData("en-US", "Staff / HR Management", "Add New Staff Member")]
    [InlineData("ta-IN", "பணியாளர் / HR மேலாண்மை", "புதிய பணியாளரைச் சேர்")]
    [InlineData("te-IN", "సిబ్బంది / HR నిర్వహణ", "కొత్త సిబ్బంది సభ్యుడిని జోడించండి")]
    [InlineData("hi-IN", "स्टाफ / एचआर प्रबंधन", "नया स्टाफ सदस्य जोड़ें")]
    public async Task Staff_page_is_localized(string culture, string title, string modalTitle)
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin", culture).GetStringAsync("/Staff/Index"));
        Assert.Contains(title, html);
        Assert.Contains(modalTitle, html);
        Assert.Contains("window.StaffI18n", html);
    }

    [Fact]
    public async Task Mutations_without_antiforgery_token_are_rejected()
    {
        var client = _factory.ClientAs("Admin");
        await AntiforgeryToken(client);   // gets the cookie, but the header is missing below
        var res = await client.PostAsJsonAsync("/Staff/Create", TestHelpers.ValidCreateDto(mobile: "9000007771"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Staff/Delete/501", null)).StatusCode);
    }

    [Fact]
    public async Task Inventory_role_cannot_create_staff()
    {
        var res = await _factory.ClientAs("Inventory").PostAsJsonAsync("/Staff/Create", TestHelpers.ValidCreateDto());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Create_update_delete_round_trip_over_http()
    {
        var client = _factory.ClientAs("Staff");
        var token = await AntiforgeryToken(client);

        // create
        var create = await client.SendAsync(Post("/Staff/Create", token, TestHelpers.ValidCreateDto(mobile: "9000005551")));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeId").GetInt32();

        // listed
        var list = await client.GetFromJsonAsync<JsonElement>("/Staff/IndexData?q=9000005551");
        var row = Assert.Single(list.GetProperty("staff").EnumerateArray());
        Assert.Equal(id, row.GetProperty("employeeId").GetInt32());
        Assert.Equal("Sales", row.GetProperty("departmentName").GetString());
        Assert.Equal("Agency A", row.GetProperty("agencyName").GetString());

        // validation error comes back keyed by form field
        var dup = await client.SendAsync(Post("/Staff/Create", token, TestHelpers.ValidCreateDto(mobile: "9000005551")));
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        Assert.Equal("Mobile already registered", (await dup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("mobile").GetString());

        // update: another department and agency, set inactive
        var upd = TestHelpers.ToUpdate(TestHelpers.ValidCreateDto(mobile: "9000005551"), "INACTIVE", "Moved out");
        upd.DepartmentId = TestHelpers.AdminDept.ToString();
        upd.AgencyId = TestHelpers.AgencyB.ToString();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Staff/Update/{id}", token, upd))).StatusCode);
        list = await client.GetFromJsonAsync<JsonElement>("/Staff/IndexData?status=INACTIVE&q=9000005551");
        row = Assert.Single(list.GetProperty("staff").EnumerateArray());
        Assert.Equal("Admin", row.GetProperty("departmentName").GetString());
        Assert.Equal("Agency B", row.GetProperty("agencyName").GetString());
        Assert.Equal("Moved out", row.GetProperty("remarks").GetString());

        // csv export includes it
        var csv = ExcelExportTestHelpers.CsvText(await client.GetStringAsync("/Staff/Export?q=9000005551"));
        Assert.Contains($"{id},Meena Kumari,Office,Admin,Sales Executive,Agency B,9000005551,Female,Morning (8AM-4PM),,2020-01-15,INACTIVE", csv);

        // delete
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Staff/Delete/{id}", token))).StatusCode);
        list = await client.GetFromJsonAsync<JsonElement>("/Staff/IndexData?q=9000005551");
        Assert.Empty(list.GetProperty("staff").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Post($"/Staff/Delete/{id}", token))).StatusCode);
    }

    [Fact]
    public async Task Popup_hides_agency_and_stall_number_and_has_photo_join_date_salary_and_quotas()
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin").GetStringAsync("/Staff/Index"));

        // hidden, though still in the form so editing keeps the values on record
        Assert.Contains("id=\"staffAgencyGroup\" style=\"display:none\"", html);
        Assert.Contains("id=\"staffStallNumberGroup\" style=\"display:none\"", html);
        // photo
        Assert.Matches(@"<input id=""staffPhoto"" name=""photo"" type=""file"" accept=""image/jpeg,image/png""", html);
        Assert.Contains("id=\"staffPhotoPreview\"", html);
        Assert.Contains("Upload Photo", html);
        // designation: a text box suggesting the tbl_Designation names, after the staff category
        Assert.Matches(@"for=""staffDesignation"">Designation <span class=""req""", html);
        Assert.Contains("<input id=\"staffDesignation\" name=\"designation\" class=\"form-control\" maxlength=\"50\" list=\"staffDesignationList\"", html);
        Assert.Contains("<datalist id=\"staffDesignationList\"></datalist>", html);
        Assert.True(html.IndexOf("id=\"staffDepartment\"", StringComparison.Ordinal) < html.IndexOf("id=\"staffDesignation\"", StringComparison.Ordinal));
        Assert.Contains("max=\"99999999.99\"", html);
        // join date and salary
        Assert.Matches(@"for=""staffJoinDate"">Join Date <span class=""req""", html);
        Assert.Contains("<input id=\"staffJoinDate\" name=\"join_date\" type=\"date\"", html);
        Assert.Contains("Monthly Salary (₹)", html);
        Assert.Contains("id=\"staffMonthlySalary\"", html);
        Assert.Contains("StaffCanViewSalary = true", html);
        // annual quota section
        Assert.Contains($"Annual Quota ({DateTime.Today.Year})", html);
        Assert.Contains("Days allowed this year. Leave a type blank if it does not apply to this employee.", html);
        Assert.Contains("id=\"staffLeaveQuotaFields\"", html);
    }

    [Fact]
    public async Task Staff_role_popup_has_no_salary_field()
    {
        var html = await _factory.ClientAs("Staff").GetStringAsync("/Staff/Index");
        Assert.DoesNotContain("id=\"staffMonthlySalary\"", html);
        Assert.Contains("StaffCanViewSalary = false", html);
        Assert.Contains("id=\"staffJoinDate\"", html);
    }

    [Fact]
    public async Task Photo_and_quotas_over_http()
    {
        var client = _factory.ClientAs("Staff");

        var photo = await client.GetAsync("/Staff/Photo/501");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType!.MediaType);
        Assert.Equal(StaffExtras.Jpeg, await photo.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Staff/Photo/99999")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.ClientAs(null).GetAsync("/Staff/Photo/501")).StatusCode);

        var quotas = await client.GetFromJsonAsync<JsonElement>("/Staff/LeaveQuotas/501");
        Assert.Equal(DateTime.Today.Year, quotas.GetProperty("year").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Staff/LeaveQuotas/99999")).StatusCode);
    }

    [Fact]
    public async Task Register_with_quotas_and_photo_over_http()
    {
        var client = _factory.ClientAs("Admin");
        var token = await AntiforgeryToken(client);
        var dto = TestHelpers.ValidCreateDto(mobile: "9000005581");
        dto.MonthlySalary = 18000m;
        dto.LeaveQuotas = StaffExtras.Quotas(12, 15, 6);
        dto.Photo = StaffExtras.JpegDataUrl;

        var res = await client.SendAsync(Post("/Staff/Create", token, dto));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var id = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeId").GetInt32();

        var quotas = (await client.GetFromJsonAsync<JsonElement>($"/Staff/LeaveQuotas/{id}")).GetProperty("rows").EnumerateArray()
            .ToDictionary(r => r.GetProperty("leaveType").GetString()!, r => r.GetProperty("annualQuota").GetDecimal());
        Assert.Equal(new Dictionary<string, decimal> { ["Casual"] = 12, ["Earned"] = 15, ["Sick"] = 6 }, quotas);
        Assert.Equal(StaffExtras.Jpeg, await client.GetByteArrayAsync($"/Staff/Photo/{id}"));
        var row = Assert.Single((await client.GetFromJsonAsync<JsonElement>($"/Staff/IndexData?q={id}")).GetProperty("staff").EnumerateArray());
        Assert.Equal(18000m, row.GetProperty("monthlySalary").GetDecimal());
        Assert.Equal("2020-01-15", row.GetProperty("joinDate").GetString());

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Staff/Delete/{id}", token))).StatusCode);
    }

    [Theory]
    [InlineData("/Staff/Masters")]
    public async Task The_old_masters_endpoint_is_gone(string url) =>
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.ClientAs("Admin").GetAsync(url)).StatusCode);

    [Fact]
    public async Task Registering_with_a_category_that_is_not_in_dbo_departments_is_refused_over_http()
    {
        var client = _factory.ClientAs("Admin");
        var token = await AntiforgeryToken(client);
        var dto = TestHelpers.ValidCreateDto(mobile: "9000005571");
        dto.DepartmentId = "424242";

        var res = await client.SendAsync(Post("/Staff/Create", token, dto));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Staff category is required", errors.GetProperty("department_id").GetString());
    }

    [Fact]
    public async Task IndexData_lists_the_masters()
    {
        var json = await _factory.ClientAs("Admin").GetFromJsonAsync<JsonElement>("/Staff/IndexData");

        Assert.Equal(2, json.GetProperty("employeeTypes").GetArrayLength());   // SERP.tbl_EMP_Types
        // staff categories: dbo.departments
        Assert.Equal(new[] { "Admin", "Cleaner", "Sales" }, json.GetProperty("depts").EnumerateArray().Select(d => d.GetProperty("name").GetString()));
        Assert.Equal(2, json.GetProperty("agencies").GetArrayLength());
        // shifts: the offered dbo.Shift rows (the retired Night shift is left out)
        Assert.Equal(new[] { "Morning (8AM-4PM)", "Evening (4PM-10PM)" }, json.GetProperty("shifts").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        Assert.Contains(json.GetProperty("staff").EnumerateArray(), s => s.GetProperty("employeeId").GetInt32() == 501);
    }

    [Fact]
    public async Task Server_validation_messages_follow_the_culture_cookie()
    {
        var client = _factory.ClientAs("Admin", "hi-IN");
        var token = await AntiforgeryToken(client);
        var dto = TestHelpers.ValidCreateDto(mobile: "12");   // fails the mobile rule

        var res = await client.SendAsync(Post("/Staff/Create", token, dto));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("मान्य 10 अंकों का मोबाइल नंबर दर्ज करें", errors.GetProperty("mobile").GetString());
    }
}
