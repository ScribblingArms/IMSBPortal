using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

/// <summary>Separate app instance (own in-memory database) for the inventory HTTP tests.</summary>
public class InventoryAppFactory : StaffAppFactory { }

public class InventoryIntegrationTests : IClassFixture<InventoryAppFactory>
{
    private readonly InventoryAppFactory _factory;
    private static bool _seeded;
    private static readonly object SeedLock = new();

    public InventoryIntegrationTests(InventoryAppFactory factory)
    {
        _factory = factory;
        lock (SeedLock)
        {
            if (_seeded) return;
            factory.Seed(ctx =>
            {
                ctx.ProductCategories.AddRange(IMSBContext.SeedCategories.Select((c, i) => new ProductCategory { Id = c.Id, Name = c.Name, SortOrder = i + 1 }));
                ctx.Products.Add(new Product { ProductCode = "PRD001", Name = "Seeded Saree", CategoryId = InventoryTestHelpers.Textiles, StallCode = "A-01", Quantity = 5, MinStock = 10, UnitPrice = 100 });
            });
            _seeded = true;
        }
    }

    private static async Task<string> Token(HttpClient client)
    {
        var html = await client.GetStringAsync("/Inventory/Index");
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
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("Inventory", HttpStatusCode.OK)]
    [InlineData("Staff", HttpStatusCode.Forbidden)]
    public async Task Inventory_is_for_inventory_and_admin_roles(string? role, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync("/Inventory/Data")).StatusCode);
    }

    [Fact]
    public async Task Page_renders_real_stats_and_no_sample_data()
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Inventory").GetStringAsync("/Inventory/Index"));
        Assert.Matches(@"id=""statTotalProducts"">\d+<", html);
        Assert.DoesNotContain(">487<", html);                  // prototype's hard-coded figures are gone
        Assert.Contains("window.InventoryI18n", html);
        Assert.Contains("id=\"stockUpdateForm\"", html);
        Assert.DoesNotContain("data-validate", html);          // main.js's fake "saved" toasts are not wired
    }

    [Theory]
    [InlineData("en-US", "Inventory Management", "Stock Level by Category")]
    [InlineData("ta-IN", "சரக்கு இருப்பு மேலாண்மை", "வகை வாரியான இருப்பு நிலை")]
    [InlineData("te-IN", "ఇన్వెంటరీ నిర్వహణ", "వర్గాల వారీ స్టాక్ స్థాయి")]
    [InlineData("hi-IN", "इन्वेंटरी प्रबंधन", "श्रेणी के अनुसार स्टॉक स्तर")]
    public async Task Page_is_localized(string culture, string title, string chartTitle)
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Inventory", culture).GetStringAsync("/Inventory/Index"));
        Assert.Contains(title, html);
        Assert.Contains(chartTitle, html);
    }

    [Fact]
    public async Task Mutations_require_antiforgery_token()
    {
        var client = _factory.ClientAs("Inventory");
        await Token(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/Inventory/Create", InventoryTestHelpers.ValidCreate(name: "No Token"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/Inventory/UpdateStock", InventoryTestHelpers.Stock("PRD001", "ADD", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Inventory/Delete/PRD001", null)).StatusCode);
    }

    [Fact]
    public async Task Product_and_stock_round_trip_over_http()
    {
        var client = _factory.ClientAs("Inventory");
        var token = await Token(client);

        // create
        var create = await client.SendAsync(Post("/Inventory/Create", token, InventoryTestHelpers.ValidCreate(name: "Round Trip Lamp", stall: "R-01", qty: 10)));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var code = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

        // listed with status
        var data = await client.GetFromJsonAsync<JsonElement>("/Inventory/Data?q=Round%20Trip");
        var row = Assert.Single(data.GetProperty("products").EnumerateArray());
        Assert.Equal("IN_STOCK", row.GetProperty("status").GetString());

        // reduce below minimum -> appears in low stock alerts
        var reduce = await client.SendAsync(Post("/Inventory/UpdateStock", token, InventoryTestHelpers.Stock(code, "REDUCE", 8, remarks: "sold")));
        Assert.Equal(HttpStatusCode.OK, reduce.StatusCode);
        data = await client.GetFromJsonAsync<JsonElement>("/Inventory/Data");
        Assert.Contains(data.GetProperty("lowStock").EnumerateArray(), p => p.GetProperty("code").GetString() == code);

        // over-reduce is rejected with a field error
        var over = await client.SendAsync(Post("/Inventory/UpdateStock", token, InventoryTestHelpers.Stock(code, "REDUCE", 3)));
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Equal("Not enough stock: only 2 available", (await over.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("quantity").GetString());

        // edit details
        var upd = new UpdateProductDto { Name = "Round Trip Lamp XL", CategoryId = InventoryTestHelpers.Pottery.ToString(), StallCode = "R-02", MinStock = 1, UnitPrice = 50 };
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Inventory/Update/{code}", token, upd))).StatusCode);

        // export reflects the edit and new quantity
        var csv = await client.GetStringAsync("/Inventory/Export?q=Round%20Trip");
        Assert.Contains($"{code},Round Trip Lamp XL,Pottery,R-02,2,1,50.00,In Stock", csv);

        // history: INITIAL + REDUCE
        data = await client.GetFromJsonAsync<JsonElement>("/Inventory/Data");
        var history = data.GetProperty("movements").EnumerateArray().Where(m => m.GetProperty("code").GetString() == code).Select(m => m.GetProperty("type").GetString()).ToList();
        Assert.Equal(new[] { "REDUCE", "INITIAL" }, history);

        // delete
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Inventory/Delete/{code}", token))).StatusCode);
        data = await client.GetFromJsonAsync<JsonElement>("/Inventory/Data?q=Round%20Trip");
        Assert.Empty(data.GetProperty("products").EnumerateArray());
    }

    [Fact]
    public async Task Validation_messages_follow_culture_cookie()
    {
        var client = _factory.ClientAs("Inventory", "ta-IN");
        var token = await Token(client);
        var res = await client.SendAsync(Post("/Inventory/Create", token, InventoryTestHelpers.ValidCreate(name: "X")));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("பொருள் பெயர் தேவை (2-200 எழுத்துகள்)", errors.GetProperty("name").GetString());
    }
}
