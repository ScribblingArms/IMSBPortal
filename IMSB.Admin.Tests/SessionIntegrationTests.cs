using System.Net;
using System.Text.RegularExpressions;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IMSB.Admin.Tests;

/// <summary>
/// The real app with its real cookie sign-in (no test auth handler), an in-memory database with one admin login,
/// and a hand-moved clock so idle timeouts can be tested.
/// </summary>
public class SessionAppFactory : WebApplicationFactory<Program>
{
    public const string User = "session.admin@imsb";
    public const string Password = "Session@2026";
    // starts at the real time: the HttpClient cookie jar expires persistent cookies by the real clock, so a fixed
    // past date would drop "remember me" cookies once that date is more than RememberMeDays ago
    public ManualClock Clock { get; } = new() { Now = DateTimeOffset.UtcNow };
    private readonly string _dbName = "session-integration-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<IMSBContext>>();
            services.AddDbContext<IMSBContext>(o => o.UseInMemoryDatabase(_dbName));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public void EnsureLogin(string username = User, string role = "Admin")
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IMSBContext>();
        if (ctx.Logins.Any(l => l.Username == username)) return;
        var login = new Login { Username = username, Email = username, Role = role, IsActive = true };
        login.PasswordHash = new PasswordHasher<Login>().HashPassword(login, Password);
        ctx.Logins.Add(login);
        ctx.SaveChanges();
    }

    public void WithDb(Action<IMSBContext> action)
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IMSBContext>();
        action(ctx);
        ctx.SaveChanges();
    }

    /// <summary>A browser-like client (keeps cookies, does not follow redirects).</summary>
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
}

public class SessionIntegrationTests : IClassFixture<SessionAppFactory>
{
    private readonly SessionAppFactory _factory;

    public SessionIntegrationTests(SessionAppFactory factory)
    {
        _factory = factory;
        factory.EnsureLogin();
    }

    private static string Token(string html) =>
        Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;

    private static HttpRequestMessage Html(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("Accept", "text/html,application/xhtml+xml");
        return req;
    }

    private static async Task<HttpResponseMessage> SignIn(HttpClient client, bool rememberMe = false)
    {
        var page = await client.SendAsync(Html(HttpMethod.Get, "/Account/Login"));
        var form = new Dictionary<string, string>
        {
            ["Username"] = SessionAppFactory.User, ["Password"] = SessionAppFactory.Password, ["SelectedRole"] = "admin",
            ["RememberMe"] = rememberMe ? "true" : "false",
            ["__RequestVerificationToken"] = Token(await page.Content.ReadAsStringAsync())
        };
        var req = Html(HttpMethod.Post, "/Account/Login");
        req.Content = new FormUrlEncodedContent(form);
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/Dashboard", res.Headers.Location?.OriginalString);
        return res;
    }

    private static string AuthCookie(HttpResponseMessage signIn) =>
        signIn.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("IMSB.Auth=")).Split(';')[0];

    private async Task<HttpResponseMessage> PostForm(HttpClient client, string pageUrl, string postUrl)
    {
        var page = await (await client.SendAsync(Html(HttpMethod.Get, pageUrl))).Content.ReadAsStringAsync();
        var req = Html(HttpMethod.Post, postUrl);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(page) });
        return await client.SendAsync(req);
    }

    [Fact]
    public async Task Sign_in_creates_a_session_and_sign_out_ends_it_even_for_a_copied_cookie()
    {
        var client = _factory.Browser();
        var cookie = AuthCookie(await SignIn(client));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"))).StatusCode);

        var logout = await PostForm(client, "/Dashboard/Index", "/Account/Logout");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        // the old cookie no longer works anywhere
        var replay = _factory.Browser();
        var page = Html(HttpMethod.Get, "/Dashboard/Index");
        page.Headers.Add("Cookie", cookie);
        var res = await replay.SendAsync(page);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("/Account/Login?", res.Headers.Location!.OriginalString);
        Assert.Contains("reason=signedout", res.Headers.Location.OriginalString);

        var api = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/Data");
        api.Headers.Add("Cookie", cookie);
        var apiRes = await _factory.Browser().SendAsync(api);
        Assert.Equal(HttpStatusCode.Unauthorized, apiRes.StatusCode);
        Assert.Equal("Revoked", apiRes.Headers.GetValues("X-Session-Ended").Single());
    }

    [Fact]
    public async Task Idle_session_expires_and_the_login_page_says_so()
    {
        var client = _factory.Browser();
        await SignIn(client);
        _factory.Clock.Advance(TimeSpan.FromMinutes(31));

        var res = await client.SendAsync(Html(HttpMethod.Get, "/Sales/Index"));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        var location = res.Headers.Location!.OriginalString;
        Assert.Contains("reason=expired", location);

        var login = await client.SendAsync(Html(HttpMethod.Get, location));
        Assert.Contains("Your session has expired. Please sign in again.", WebUtility.HtmlDecode(await login.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Requests_keep_the_session_alive()
    {
        var client = _factory.Browser();
        await SignIn(client);
        for (int i = 0; i < 4; i++)
        {
            _factory.Clock.Advance(TimeSpan.FromMinutes(20));
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"))).StatusCode);
        }
    }

    [Fact]
    public async Task Keep_alive_reports_time_left()
    {
        var client = _factory.Browser();
        await SignIn(client);
        var page = await (await client.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"))).Content.ReadAsStringAsync();
        Assert.Contains("data-session-idle=\"1800\"", page);   // the page's idle timer gets the server's limit
        var req = new HttpRequestMessage(HttpMethod.Post, "/Account/KeepAlive");
        req.Headers.Add("RequestVerificationToken", Token(page));
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("\"secondsLeft\":1800", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Remember_me_has_no_idle_timer()
    {
        var client = _factory.Browser();
        await SignIn(client, rememberMe: true);
        var page = await (await client.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"))).Content.ReadAsStringAsync();
        Assert.Contains("data-session-idle=\"0\"", page);
        _factory.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"))).StatusCode);
    }

    [Fact]
    public async Task Signing_out_other_sessions_ends_them()
    {
        var phone = _factory.Browser();
        await SignIn(phone);
        var laptop = _factory.Browser();
        await SignIn(laptop);

        var sessions = WebUtility.HtmlDecode(await (await laptop.SendAsync(Html(HttpMethod.Get, "/Account/Sessions"))).Content.ReadAsStringAsync());
        Assert.Contains("This device", sessions);
        Assert.Contains("All Active Sessions", sessions);   // admins see everyone's

        var res = await PostForm(laptop, "/Account/Sessions", "/Account/RevokeOtherSessions");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await laptop.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await phone.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"))).StatusCode);
    }

    [Fact]
    public async Task Disabling_the_account_signs_it_out()
    {
        _factory.EnsureLogin("to.disable@imsb");
        var client = _factory.Browser();
        var page = await client.SendAsync(Html(HttpMethod.Get, "/Account/Login"));
        var req = Html(HttpMethod.Post, "/Account/Login");
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "to.disable@imsb", ["Password"] = SessionAppFactory.Password, ["SelectedRole"] = "admin",
            ["__RequestVerificationToken"] = Token(await page.Content.ReadAsStringAsync())
        });
        Assert.Equal(HttpStatusCode.Redirect, (await client.SendAsync(req)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"))).StatusCode);

        _factory.WithDb(ctx => ctx.Logins.Single(l => l.Username == "to.disable@imsb").IsActive = false);
        var res = await client.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"));
        Assert.Contains("reason=disabled", res.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task The_login_page_offers_the_five_roles_in_a_dropdown()
    {
        var html = await (await _factory.Browser().SendAsync(Html(HttpMethod.Get, "/Account/Login"))).Content.ReadAsStringAsync();

        var select = Regex.Match(html, @"<select id=""selectedRole"" name=""SelectedRole""[^>]*>(.*?)</select>", RegexOptions.Singleline);
        Assert.True(select.Success, "role dropdown not found");
        var options = Regex.Matches(select.Groups[1].Value, @"<option value=""(\w+)""[^>]*>([^<]+)</option>")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value.Trim())).ToList();
        Assert.Equal(new[]
        {
            ("admin", "Admin"), ("staff", "Staff"), ("inventory", "Inventory"),
            ("districtofficer", "District Officer"), ("stateadminofficer", "State Admin Officer")
        }, options);
        Assert.DoesNotContain("role-tab", html);
    }

    [Theory]
    [InlineData("district.officer@imsb", "DistrictOfficer", "districtofficer")]
    [InlineData("state.officer@imsb", "StateAdminOfficer", "stateadminofficer")]
    public async Task An_officer_signs_in_to_stall_management_and_can_open_only_it_and_shg_applications(string user, string role, string choice)
    {
        _factory.EnsureLogin(user, role);
        var client = _factory.Browser();
        var page = await client.SendAsync(Html(HttpMethod.Get, "/Account/Login"));
        var req = Html(HttpMethod.Post, "/Account/Login");
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = user, ["Password"] = SessionAppFactory.Password, ["SelectedRole"] = choice,
            ["__RequestVerificationToken"] = Token(await page.Content.ReadAsStringAsync())
        });

        var signIn = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.Equal("/Stalls", signIn.Headers.Location?.OriginalString);

        var stalls = await client.SendAsync(Html(HttpMethod.Get, "/Stalls/Index"));
        Assert.Equal(HttpStatusCode.OK, stalls.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Html(HttpMethod.Get, "/Stalls/Data"))).StatusCode);

        // the sidebar offers Stall Management and SHG Applications and nothing else
        var html = await stalls.Content.ReadAsStringAsync();
        var sidebar = Regex.Match(html, @"<nav class=""sidebar-nav"">(.*?)</nav>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(new[] { "/Stalls/Index", "/ShgApplications/Index" }, Regex.Matches(sidebar, @"data-page=""([^""]+)""").Select(m => m.Groups[1].Value));

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Html(HttpMethod.Get, "/ShgApplications/Index"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Html(HttpMethod.Get, "/ShgApplications/Data"))).StatusCode);

        foreach (var url in new[] { "/Dashboard/Index", "/Inventory/Index", "/Sales/Index", "/Artisans/Index", "/Applications/Index",
                     "/Staff/Index", "/Attendance/Index", "/Reports/Index", "/UserManagement/Index", "/OtAssignments/Index" })
        {
            var res = await client.SendAsync(Html(HttpMethod.Get, url));
            Assert.True(res.StatusCode != HttpStatusCode.OK, $"{role} could open {url}");
            Assert.Contains("AccessDenied", res.Headers.Location?.OriginalString ?? "");
        }
    }

    [Fact]
    public async Task An_officer_cannot_sign_in_as_admin()
    {
        _factory.EnsureLogin("district.only@imsb", "DistrictOfficer");
        var client = _factory.Browser();
        var page = await client.SendAsync(Html(HttpMethod.Get, "/Account/Login"));
        var req = Html(HttpMethod.Post, "/Account/Login");
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "district.only@imsb", ["Password"] = SessionAppFactory.Password, ["SelectedRole"] = "admin",
            ["__RequestVerificationToken"] = Token(await page.Content.ReadAsStringAsync())
        });

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);   // the login page again, with the error
        Assert.Equal(HttpStatusCode.Redirect, (await client.SendAsync(Html(HttpMethod.Get, "/Dashboard/Index"))).StatusCode);
    }
}
