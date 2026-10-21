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
        builder.UseEnvironment("Testing");   // skips the Development-only demo-user seeding
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<IMSBContext>>();
            services.AddDbContext<IMSBContext>(o => o.UseInMemoryDatabase(_dbName));

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
                ctx.Shifts.Add(new Shift { Code = "MORNING", Name = "Morning (8AM-4PM)" });
                ctx.Staff.Add(TestHelpers.ExistingStaff("EMP001", "Seeded Person", "900000001"));
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
    public async Task Staff_page_uses_prototype_styles_without_bootstrap()
    {
        var html = await _factory.ClientAs("Admin").GetStringAsync("/Staff/Index");

        Assert.Contains("/css/style.css", html);
        Assert.Contains("/css/forms.css", html);
        Assert.DoesNotContain("bootstrap.min.css", html);
        Assert.Contains("class=\"form-grid col-2\"", html);
        Assert.Contains("id=\"addStaffModal\"", html);
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
        var res = await client.PostAsJsonAsync("/Staff/Create", TestHelpers.ValidCreateDto(mobile: "900000777", email: null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Staff/Delete/EMP001", null)).StatusCode);
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
        var create = await client.SendAsync(Post("/Staff/Create", token, TestHelpers.ValidCreateDto(mobile: "900000555", email: "roundtrip@example.com")));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeId").GetString()!;
        Assert.Matches(@"^EMP\d{3,}$", id);

        // listed
        var list = await client.GetFromJsonAsync<JsonElement>("/Staff/IndexData?q=900000555");
        var row = Assert.Single(list.GetProperty("staff").EnumerateArray());
        Assert.Equal(id, row.GetProperty("employeeId").GetString());

        // validation error comes back keyed by form field
        var dup = await client.SendAsync(Post("/Staff/Create", token, TestHelpers.ValidCreateDto(mobile: "900000555", email: null)));
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        Assert.Equal("Mobile already registered", (await dup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("mobile").GetString());

        // update
        var upd = TestHelpers.ToUpdate(TestHelpers.ValidCreateDto(mobile: "900000555", email: "roundtrip@example.com"), "ON_LEAVE");
        upd.Designation = "Floor Manager";
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Staff/Update/{id}", token, upd))).StatusCode);
        list = await client.GetFromJsonAsync<JsonElement>("/Staff/IndexData?status=ON_LEAVE&q=900000555");
        row = Assert.Single(list.GetProperty("staff").EnumerateArray());
        Assert.Equal("Floor Manager", row.GetProperty("designation").GetString());

        // csv export includes it
        var csv = await client.GetStringAsync("/Staff/Export?q=900000555");
        Assert.Contains($"{id},Meena Kumari,Sales,Floor Manager,900000555", csv);

        // delete
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Staff/Delete/{id}", token))).StatusCode);
        list = await client.GetFromJsonAsync<JsonElement>("/Staff/IndexData?q=900000555");
        Assert.Empty(list.GetProperty("staff").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Post($"/Staff/Delete/{id}", token))).StatusCode);
    }

    [Fact]
    public async Task Server_validation_messages_follow_the_culture_cookie()
    {
        var client = _factory.ClientAs("Admin", "hi-IN");
        var token = await AntiforgeryToken(client);
        var dto = TestHelpers.ValidCreateDto(mobile: "12", email: null);   // fails [RegularExpression] on the DTO

        var res = await client.SendAsync(Post("/Staff/Create", token, dto));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("मोबाइल नंबर 9-12 अंकों का होना चाहिए", errors.GetProperty("mobile").GetString());
    }
}
