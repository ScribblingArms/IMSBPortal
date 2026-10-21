using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

/// <summary>Separate app instance (own in-memory database) for the sales and dashboard HTTP tests.</summary>
public class SalesAppFactory : StaffAppFactory { }

public class SalesDashboardIntegrationTests : IClassFixture<SalesAppFactory>
{
    private readonly SalesAppFactory _factory;
    private static bool _seeded;
    private static readonly object SeedLock = new();

    public SalesDashboardIntegrationTests(SalesAppFactory factory)
    {
        _factory = factory;
        lock (SeedLock)
        {
            if (_seeded) return;
            factory.Seed(ctx =>
            {
                ctx.ProductCategories.AddRange(IMSBContext.SeedCategories.Select((c, i) => new ProductCategory { Id = c.Id, Name = c.Name, SortOrder = i + 1 }));
                ctx.Products.Add(new Product { ProductCode = "PRD001", Name = "Http Saree", CategoryId = InventoryTestHelpers.Textiles, StallCode = "H-01", Quantity = 100, MinStock = 5, UnitPrice = 1000 });
                ctx.Products.Add(new Product { ProductCode = "PRD002", Name = "Http Pot", CategoryId = InventoryTestHelpers.Pottery, StallCode = "H-02", Quantity = 2, MinStock = 10, UnitPrice = 300 });
            });
            _seeded = true;
        }
    }

    private static async Task<string> Token(HttpClient client)
    {
        var html = await client.GetStringAsync("/Sales/Index");
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

    private static object Sale(string code, int qty, decimal price, string? date = null) => new
    {
        date = date ?? DateTime.Today.ToString("yyyy-MM-dd"),
        time = "00:00",
        items = new[] { new { productCode = code, quantity = qty, unitPrice = price } }
    };

    // ── Access ────────────────────────────────────────────────────

    [Theory]
    [InlineData("/Sales/Data", null, HttpStatusCode.Unauthorized)]
    [InlineData("/Sales/Data", "Admin", HttpStatusCode.OK)]
    [InlineData("/Sales/Data", "Staff", HttpStatusCode.Forbidden)]
    [InlineData("/Sales/Data", "Inventory", HttpStatusCode.Forbidden)]
    [InlineData("/Sales/Export", "Inventory", HttpStatusCode.Forbidden)]
    [InlineData("/Dashboard/Data", null, HttpStatusCode.Unauthorized)]
    [InlineData("/Dashboard/Data", "Admin", HttpStatusCode.OK)]
    [InlineData("/Dashboard/Data", "Staff", HttpStatusCode.Forbidden)]
    [InlineData("/Dashboard/Export", "Inventory", HttpStatusCode.Forbidden)]
    public async Task Sales_and_dashboard_are_admin_only(string url, string? role, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Mutations_require_antiforgery_token()
    {
        var client = _factory.ClientAs("Admin");
        await Token(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/Sales/Create", Sale("PRD001", 1, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/Sales/Update/SAL-000001", Sale("PRD001", 1, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Sales/Delete/SAL-000001", null)).StatusCode);
    }

    // ── Pages ─────────────────────────────────────────────────────

    [Fact]
    public async Task Sales_page_has_no_prototype_data_or_fake_export()
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin").GetStringAsync("/Sales/Index"));
        Assert.DoesNotContain("1,42,850", html);           // prototype figures are gone
        Assert.DoesNotContain("42.8L", html);
        Assert.DoesNotContain("data-export", html);         // main.js's fake "Export Ready" is not wired here
        Assert.Contains("window.SalesI18n", html);
        Assert.Contains("id=\"salesEntryForm\"", html);
        Assert.Contains("id=\"productSalesTable\"", html);
        Assert.Contains("id=\"hourlySalesChart\"", html);
        Assert.Contains("id=\"stockDeductList\"", html);
    }

    [Fact]
    public async Task Dashboard_page_has_real_cards_and_real_export()
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin").GetStringAsync("/Dashboard/Index"));
        Assert.Matches(@"id=""stat-stalls"">\d+<", html);
        Assert.Matches(@"id=""stat-occupied"">\d+<", html);
        Assert.Contains("id=\"stat-artisans\">0<", html);    // counted from the artisans table
        Assert.DoesNotContain("+3 this month", html);
        Assert.DoesNotContain("data-export", html);
        Assert.Contains("id=\"dashboardExportBtn\"", html);
        Assert.Contains("href=\"/Sales/Index\"", html);      // New Sales Entry
        Assert.Contains("window.DashboardI18n", html);
    }

    [Theory]   // the dashboard's quick actions link to bare controller URLs, which must open the Index page
    [InlineData("/Inventory")]
    [InlineData("/Stalls")]
    [InlineData("/Sales")]
    [InlineData("/Artisans")]
    [InlineData("/Attendance")]
    [InlineData("/Reports")]
    public async Task Bare_controller_urls_open_the_index_page(string url)
    {
        var res = await _factory.ClientAs("Admin").GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Theory]
    [InlineData("en-US", "Sales Management", "Product-wise Sales Report", "Auto Stock Deduction Log")]
    [InlineData("ta-IN", "விற்பனை மேலாண்மை", "பொருள் வாரியான விற்பனை அறிக்கை", "தானியங்கு இருப்புக் கழிவுப் பதிவேடு")]
    [InlineData("te-IN", "అమ్మకాల నిర్వహణ", "ఉత్పత్తి వారీ అమ్మకాల నివేదిక", "ఆటో స్టాక్ తగ్గింపు లాగ్")]
    [InlineData("hi-IN", "बिक्री प्रबंधन", "उत्पाद-वार बिक्री रिपोर्ट", "स्वचालित स्टॉक कटौती लॉग")]
    public async Task Sales_page_is_localized(string culture, params string[] texts)
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin", culture).GetStringAsync("/Sales/Index"));
        foreach (var text in texts) Assert.Contains(text, html);
    }

    [Theory]
    [InlineData("en-US", "Operations Dashboard", "Best Performing Stalls", "Quick Actions")]
    [InlineData("ta-IN", "சிறப்பாகச் செயல்படும் ஸ்டால்கள்", "விரைவுச் செயல்கள்", "அறிக்கையை ஏற்றுமதி செய்")]
    [InlineData("te-IN", "ఉత్తమ పనితీరు స్టాల్‌లు", "త్వరిత చర్యలు", "నివేదికను ఎగుమతి చేయండి")]
    [InlineData("hi-IN", "सर्वश्रेष्ठ प्रदर्शन वाले स्टॉल", "त्वरित कार्य", "रिपोर्ट निर्यात करें")]
    public async Task Dashboard_page_is_localized(string culture, params string[] texts)
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin", culture).GetStringAsync("/Dashboard/Index"));
        foreach (var text in texts) Assert.Contains(text, html);
    }

    [Theory]
    [InlineData("/Stalls/Index", "स्टॉल प्रबंधन", "Create, allocate")]
    [InlineData("/Reports/Index", "रिपोर्ट और विश्लेषण", "Generate and export reports")]
    [InlineData("/Artisans/Index", "नया कारीगर आवेदन", "Personal Information")]
    [InlineData("/Attendance/Index", "उपस्थिति इतिहास", "Attendance History")]
    [InlineData("/Applications/Index", "हस्तशिल्प आवेदन", "Days Pending<")]
    public async Task Other_admin_pages_are_localized(string url, string hindi, string english)
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin", "hi-IN").GetStringAsync(url));
        Assert.Contains(hindi, html);
        Assert.DoesNotContain(english, html);
        Assert.Contains("window.IMSBI18n", html);   // common strings for main.js and the page scripts
    }

    [Theory]
    [InlineData("en-US", "Welcome Back", "Sign In to Portal")]
    [InlineData("ta-IN", "மீண்டும் வரவேற்கிறோம்", "தளத்தில் உள்நுழை")]
    [InlineData("te-IN", "తిరిగి స్వాగతం", "పోర్టల్‌లో సైన్ ఇన్ చేయండి")]
    [InlineData("hi-IN", "फिर से स्वागत है", "पोर्टल में साइन इन करें")]
    public async Task Login_page_is_localized(string culture, string heading, string button)
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs(null, culture).GetStringAsync("/Account/Login"));
        Assert.Contains(heading, html);
        Assert.Contains(button, html);
        Assert.Contains("name=\"culture\"", html);   // language can be chosen before signing in
    }

    [Fact]
    public async Task Forgot_password_page_is_localized()
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs(null, "te-IN").GetStringAsync("/Account/Forgot"));
        Assert.Contains("పాస్‌వర్డ్ మర్చిపోయారా", html);
        Assert.Contains("లాగిన్‌కు తిరిగి వెళ్ళండి", html);
    }

    // ── Round trip ────────────────────────────────────────────────

    [Fact]
    public async Task Sale_round_trip_over_http()
    {
        var client = _factory.ClientAs("Admin");
        var token = await Token(client);

        // create: stock is deducted
        var create = await client.SendAsync(Post("/Sales/Create", token, Sale("PRD001", 3, 950)));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var entryNo = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("entryNo").GetString()!;

        var data = await client.GetFromJsonAsync<JsonElement>("/Sales/Data");
        Assert.Contains(data.GetProperty("entries").EnumerateArray(), e => e.GetProperty("entryNo").GetString() == entryNo);
        Assert.Equal(97, data.GetProperty("productOptions").EnumerateArray().Single(p => p.GetProperty("code").GetString() == "PRD001").GetProperty("quantity").GetInt32());

        // shows up in the report, the deduction log, the dashboard and the exports
        var report = await client.GetFromJsonAsync<JsonElement>("/Sales/ProductReport?q=http%20saree");
        Assert.Contains(report.GetProperty("rows").EnumerateArray(), r => r.GetProperty("quantity").GetInt32() >= 3);
        var log = await client.GetFromJsonAsync<JsonElement>("/Sales/DeductionLog");
        Assert.Contains(log.EnumerateArray(), m => m.GetProperty("entryNo").GetString() == entryNo && m.GetProperty("type").GetString() == "SALE");
        var dash = await client.GetFromJsonAsync<JsonElement>("/Dashboard/Data?days=90");
        Assert.True(dash.GetProperty("stats").GetProperty("todaySales").GetDecimal() >= 2850m);
        Assert.Contains(dash.GetProperty("topStalls").EnumerateArray(), s => s.GetProperty("stallCode").GetString() == "H-01");

        var csv = await client.GetAsync("/Sales/Export");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.Contains($"{entryNo},", await csv.Content.ReadAsStringAsync());
        Assert.Equal("text/csv", (await client.GetAsync("/Sales/ExportProductReport?stall=H-01")).Content.Headers.ContentType!.MediaType);
        var dashCsv = await client.GetAsync("/Dashboard/Export?days=40");
        Assert.Equal("text/csv", dashCsv.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Low Stock Alerts", await dashCsv.Content.ReadAsStringAsync());

        // edit down to 1: 2 go back to stock
        var update = await client.SendAsync(Post($"/Sales/Update/{entryNo}", token, Sale("PRD001", 1, 950)));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        data = await client.GetFromJsonAsync<JsonElement>("/Sales/Data");
        Assert.Equal(99, data.GetProperty("productOptions").EnumerateArray().Single(p => p.GetProperty("code").GetString() == "PRD001").GetProperty("quantity").GetInt32());

        // delete: all back
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Sales/Delete/{entryNo}", token))).StatusCode);
        data = await client.GetFromJsonAsync<JsonElement>("/Sales/Data");
        Assert.DoesNotContain(data.GetProperty("entries").EnumerateArray(), e => e.GetProperty("entryNo").GetString() == entryNo);
        Assert.Equal(100, data.GetProperty("productOptions").EnumerateArray().Single(p => p.GetProperty("code").GetString() == "PRD001").GetProperty("quantity").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Post($"/Sales/Delete/{entryNo}", token))).StatusCode);
    }

    [Fact]
    public async Task Overselling_over_http_returns_a_field_error()
    {
        var client = _factory.ClientAs("Admin");
        var token = await Token(client);
        var res = await client.SendAsync(Post("/Sales/Create", token, Sale("PRD002", 5, 300)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Not enough stock: only 2 available", errors.GetProperty("items_0_quantity").GetString());
    }

    [Fact]
    public async Task Binding_errors_keep_the_row_and_follow_the_culture()
    {
        var client = _factory.ClientAs("Admin", "ta-IN");
        var token = await Token(client);
        var body = new { date = "", time = "99:99", items = new[] { new { productCode = "PRD001", quantity = 1, unitPrice = 10m }, new { productCode = "PRD002", quantity = 0, unitPrice = 10m } } };
        var res = await client.SendAsync(Post("/Sales/Create", token, body));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("அளவு குறைந்தது 1 ஆக இருக்க வேண்டும்", errors.GetProperty("items_1_quantity").GetString());
        Assert.True(errors.TryGetProperty("date", out _));
        Assert.True(errors.TryGetProperty("time", out _));
    }
}
