using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

/// <summary>Separate app instance (own in-memory database) for the applications, reports and header HTTP tests.</summary>
public class ApplicationsAppFactory : StaffAppFactory { }

public class ApplicationsReportsIntegrationTests : IClassFixture<ApplicationsAppFactory>
{
    private readonly ApplicationsAppFactory _factory;
    private static bool _seeded;
    private static readonly object SeedLock = new();

    public ApplicationsReportsIntegrationTests(ApplicationsAppFactory factory)
    {
        _factory = factory;
        lock (SeedLock)
        {
            if (_seeded) return;
            factory.Seed(ctx =>
            {
                ctx.ProductCategories.AddRange(IMSBContext.SeedCategories.Select((c, i) => new ProductCategory { Id = c.Id, Name = c.Name, SortOrder = i + 1 }));
                ctx.Stalls.Add(new Stall { StallCode = "Q-01", Name = "Stall Q-01", Block = "Q", Status = StallStatuses.Vacant });
            });
            _seeded = true;
        }
    }

    private static async Task<string> Token(HttpClient client, string page)
    {
        var html = await client.GetStringAsync(page);
        var m = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        Assert.True(m.Success);
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
    [InlineData("/Applications/Data", null, HttpStatusCode.Unauthorized)]
    [InlineData("/Applications/Data", "Admin", HttpStatusCode.OK)]
    [InlineData("/Applications/Export", "Staff", HttpStatusCode.Forbidden)]
    [InlineData("/Reports/Data", "Admin", HttpStatusCode.OK)]
    [InlineData("/Reports/Data", "Inventory", HttpStatusCode.Forbidden)]
    [InlineData("/Reports/Export/kpis", "Staff", HttpStatusCode.Forbidden)]
    [InlineData("/Reports/Export/nothing", "Admin", HttpStatusCode.NotFound)]
    [InlineData("/Search/Query?q=ab", null, HttpStatusCode.Unauthorized)]
    [InlineData("/Search/Query?q=ab", "Staff", HttpStatusCode.OK)]
    [InlineData("/Notifications/Data", "Inventory", HttpStatusCode.OK)]
    public async Task Access_rules(string url, string? role, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Mutations_require_antiforgery_token()
    {
        var client = _factory.ClientAs("Admin");
        foreach (var url in new[] { "/Applications/Create", "/Applications/Process/APP-0001", "/Applications/Approve/APP-0001",
                                    "/Applications/Reject/APP-0001", "/Applications/Delete/APP-0001", "/Applications/Update/APP-0001" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { })).StatusCode);
    }

    [Fact]
    public async Task Header_tools_are_hidden_only_on_the_artisans_page()
    {
        var client = _factory.ClientAs("Admin");
        var artisans = await client.GetStringAsync("/Artisans/Index");
        Assert.DoesNotContain("id=\"headerSearchBtn\"", artisans);
        Assert.DoesNotContain("id=\"headerNotifBtn\"", artisans);
        Assert.Contains("id=\"darkModeToggle\"", artisans);   // the rest of the header stays

        foreach (var page in new[] { "/Applications/Index", "/Reports/Index", "/Stalls/Index", "/Dashboard/Index" })
        {
            var html = await client.GetStringAsync(page);
            Assert.Contains("id=\"headerSearchBtn\"", html);
            Assert.Contains("id=\"headerNotifBtn\"", html);
            Assert.Contains("window.IMSBHeaderI18n", html);
        }
    }

    [Fact]
    public async Task Pages_render_without_prototype_data()
    {
        var client = _factory.ClientAs("Admin");
        var apps = WebUtility.HtmlDecode(await client.GetStringAsync("/Applications/Index"));
        Assert.Matches(@"id=""statTotal"">\d+<", apps);
        Assert.DoesNotContain(">44<", apps);
        Assert.Contains("window.ApplicationsI18n", apps);

        var reports = WebUtility.HtmlDecode(await client.GetStringAsync("/Reports/Index"));
        Assert.DoesNotContain("₹42.8L", reports);
        Assert.DoesNotContain("April 2025", reports);
        Assert.Contains("window.ReportsI18n", reports);
    }

    [Theory]
    [InlineData("ta-IN", "RptApplications")]
    [InlineData("te-IN", "ExportApproved")]
    [InlineData("hi-IN", "SearchPending")]
    public async Task Pages_are_localized(string culture, string key)
    {
        string text;
        using (new TestHelpers.CultureScope(culture)) text = TestHelpers.SharedLocalizer()[key].Value;
        var client = _factory.ClientAs("Admin", culture);
        var page = key == "RptApplications" ? "/Reports/Index" : "/Applications/Index";
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(page));
        // the report library labels are rendered by reports.js from ReportsI18n (JSON-escaped)
        Assert.True(html.Contains(text) || html.Contains(JsonSerializer.Serialize(text).Trim('"')), $"{key} not localized on {page}");
    }

    [Fact]
    public async Task Application_round_trip_over_http()
    {
        var client = _factory.ClientAs("Admin");
        var token = await Token(client, "/Applications/Index");

        var create = await client.SendAsync(Post("/Applications/Create", token, ApplicationTestHelpers.NewApplication(mobile: "9000022222", aadhaarSeed: 9101, name: "Http Applicant")));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Applications/Process/{id}", token))).StatusCode);
        var review = await client.GetFromJsonAsync<JsonElement>("/Applications/Data?status=UNDER_REVIEW&q=http");
        Assert.Equal("tester", review.GetProperty("applications").EnumerateArray().Single().GetProperty("reviewer").GetString());

        var approve = await client.SendAsync(Post($"/Applications/Approve/{id}", token, new { stallCode = "Q-01", remarks = "ok" }));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var artisan = (await approve.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("artisanCode").GetString();

        var artisans = await client.GetFromJsonAsync<JsonElement>("/Artisans/Data?q=http");
        var row = artisans.GetProperty("artisans").EnumerateArray().Single();
        Assert.Equal((artisan, "Q-01"), (row.GetProperty("code").GetString(), row.GetProperty("stallCode").GetString()));

        // the header search finds both
        var search = await client.GetFromJsonAsync<JsonElement>("/Search/Query?q=http%20appl");
        Assert.Contains(search.GetProperty("groups").EnumerateArray(), g => g.GetProperty("type").GetString() == "APPLICATIONS");

        var csv = await client.GetAsync("/Applications/Export?status=APPROVED&q=http");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains($"{id},Http Applicant,", text);
        Assert.DoesNotContain(BazaarTestHelpers.ValidAadhaar(9101), text);

        // an approved application stays on record
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Post($"/Applications/Delete/{id}", token))).StatusCode);
    }

    [Fact]
    public async Task Report_exports_over_http()
    {
        var client = _factory.ClientAs("Admin");
        var csv = await client.GetAsync("/Reports/Export/all?format=csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("Reports & Analytics,", (await csv.Content.ReadAsStringAsync()).TrimStart('﻿'));

        var pdf = await client.GetAsync("/Reports/Export/stalls?format=pdf&status=VACANT");
        Assert.Equal("text/html", pdf.Content.Headers.ContentType!.MediaType);
        var html = WebUtility.HtmlDecode(await pdf.Content.ReadAsStringAsync());
        Assert.Contains("<h1>Stall Occupancy Report</h1>", html);
        Assert.Contains("window.print()", html);
        Assert.DoesNotContain("class=\"sidebar\"", html);   // standalone page, no layout

        var data = await client.GetFromJsonAsync<JsonElement>("/Reports/Data");
        Assert.Equal(12, data.GetProperty("salesTrend").GetProperty("labels").GetArrayLength());
    }

    [Fact]
    public async Task Search_result_links_prefill_the_page_search()
    {
        var html = await _factory.ClientAs("Admin").GetStringAsync("/Stalls/Index?q=Q-01");
        Assert.Contains("id=\"stallSearch\" data-url-search", html);
    }
}
