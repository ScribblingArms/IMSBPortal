using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

/// <summary>Separate app instance (own in-memory database) for the stall and artisan HTTP tests.</summary>
public class BazaarAppFactory : StaffAppFactory { }

public class StallsArtisansIntegrationTests : IClassFixture<BazaarAppFactory>
{
    private readonly BazaarAppFactory _factory;
    private static bool _seeded;
    private static readonly object SeedLock = new();

    public StallsArtisansIntegrationTests(BazaarAppFactory factory)
    {
        _factory = factory;
        lock (SeedLock)
        {
            if (_seeded) return;
            factory.Seed(ctx => ctx.ProductCategories.AddRange(IMSBContext.SeedCategories.Select((c, i) => new ProductCategory { Id = c.Id, Name = c.Name, SortOrder = i + 1 })));
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
    [InlineData("/Stalls/Data", null, HttpStatusCode.Unauthorized)]
    [InlineData("/Stalls/Data", "Admin", HttpStatusCode.OK)]
    [InlineData("/Stalls/Data", "Staff", HttpStatusCode.Forbidden)]
    [InlineData("/Stalls/Export", "Inventory", HttpStatusCode.Forbidden)]
    [InlineData("/Artisans/Data", null, HttpStatusCode.Unauthorized)]
    [InlineData("/Artisans/Data", "Admin", HttpStatusCode.OK)]
    [InlineData("/Artisans/Data", "Staff", HttpStatusCode.Forbidden)]
    [InlineData("/Artisans/Export", "Inventory", HttpStatusCode.Forbidden)]
    public async Task Stalls_and_artisans_are_admin_only(string url, string? role, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await _factory.ClientAs(role).GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Mutations_require_antiforgery_token()
    {
        var client = _factory.ClientAs("Admin");
        foreach (var url in new[] { "/Stalls/Create", "/Stalls/Allocate", "/Stalls/RequestReallocation", "/Stalls/Delete/A-01", "/Stalls/Vacate/A-01",
                                    "/Artisans/Create", "/Artisans/Delete/ART-0001" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { })).StatusCode);
    }

    [Fact]
    public async Task Pages_render_real_stats_without_prototype_data()
    {
        var client = _factory.ClientAs("Admin");
        var stalls = WebUtility.HtmlDecode(await client.GetStringAsync("/Stalls/Index"));
        Assert.Matches(@"id=""statTotal"">\d+<", stalls);
        Assert.DoesNotContain(">120<", stalls);
        Assert.DoesNotContain("Meena Selvam", stalls);
        Assert.DoesNotContain("data-validate", stalls);
        Assert.Contains("window.StallsI18n", stalls);

        var artisans = WebUtility.HtmlDecode(await client.GetStringAsync("/Artisans/Index"));
        Assert.Matches(@"id=""statTotal"">\d+<", artisans);
        Assert.DoesNotContain(">89<", artisans);
        Assert.DoesNotContain("Chennai", artisans);   // Telangana districts come from the server
        Assert.Contains("window.ArtisansI18n", artisans);
    }

    [Theory]
    [InlineData("en-US", "Stall Management", "Allocate Stall to Artisan", "Artisan Directory")]
    [InlineData("ta-IN", "ஸ்டால் மேலாண்மை", "கலைஞருக்கு ஸ்டாலை ஒதுக்கு", "மறுஒதுக்கீடு கோரு")]
    [InlineData("te-IN", "స్టాల్ నిర్వహణ", "కళాకారునికి స్టాల్ కేటాయించండి", "పునః కేటాయింపు అభ్యర్థించండి")]
    [InlineData("hi-IN", "स्टॉल प्रबंधन", "कारीगर को स्टॉल आवंटित करें", "पुनः आवंटन का अनुरोध करें")]
    public async Task Stalls_page_is_localized(string culture, string title, string allocate, string third)
    {
        var html = WebUtility.HtmlDecode(await _factory.ClientAs("Admin", culture).GetStringAsync("/Stalls/Index"));
        Assert.Contains(title, html);
        Assert.Contains(allocate, html);
        if (culture != "en-US") Assert.Contains(third, html);
    }

    [Theory]
    [InlineData("ta-IN", "நிலுவையிலுள்ள மறுஒதுக்கீடு", "ஸ்டால் இல்லாதவர்கள்")]
    [InlineData("te-IN", "పెండింగ్ పునః కేటాయింపు", "స్టాల్ లేనివారు")]
    [InlineData("hi-IN", "लंबित पुनः आवंटन", "बिना स्टॉल")]
    public async Task Stat_labels_and_filters_are_localized(string culture, string pendingLabel, string withoutStall)
    {
        var client = _factory.ClientAs("Admin", culture);
        Assert.Contains(pendingLabel, WebUtility.HtmlDecode(await client.GetStringAsync("/Stalls/Index")));
        Assert.Contains(withoutStall, WebUtility.HtmlDecode(await client.GetStringAsync("/Artisans/Index")));
    }

    [Fact]
    public async Task Artisan_and_stall_round_trip_over_http()
    {
        var client = _factory.ClientAs("Admin");
        var token = await Token(client, "/Artisans/Index");

        // new application
        var create = await client.SendAsync(Post("/Artisans/Create", token, BazaarTestHelpers.NewArtisan(mobile: "9000011111", aadhaarSeed: 9001, name: "Http Artisan")));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var artisan = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

        // create a stall and allocate it
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post("/Stalls/Create", token, BazaarTestHelpers.NewStall(code: "H-01", name: "Http Stall", block: "H")))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post("/Stalls/Allocate", token, BazaarTestHelpers.Allocation(stall: "H-01", artisan: artisan)))).StatusCode);

        var stalls = await client.GetFromJsonAsync<JsonElement>("/Stalls/Data?q=H-01");
        var row = stalls.GetProperty("stalls").EnumerateArray().Single();
        Assert.Equal(("OCCUPIED", "Http Artisan"), (row.GetProperty("status").GetString(), row.GetProperty("artisanName").GetString()));

        var artisans = await client.GetFromJsonAsync<JsonElement>("/Artisans/Data?stall=assigned&q=http");
        Assert.Equal("H-01", artisans.GetProperty("artisans").EnumerateArray().Single().GetProperty("stallCode").GetString());

        // the artisan cannot be deleted while holding the stall
        var blocked = await client.SendAsync(Post($"/Artisans/Delete/{artisan}", token));
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);

        // vacate-type reallocation, approved
        var req = await client.SendAsync(Post("/Stalls/RequestReallocation", token, BazaarTestHelpers.Reallocation(stall: "H-01", reason: "CLOSURE", newArtisan: null)));
        Assert.Equal(HttpStatusCode.OK, req.StatusCode);
        var id = (await req.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Stalls/ApproveReallocation/{id}", token, new { remarks = "closing" }))).StatusCode);

        // exports
        var csv = await client.GetAsync("/Stalls/Export?block=H");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.Contains("H-01,Http Stall,H,", await csv.Content.ReadAsStringAsync());
        var artisanCsv = await (await client.GetAsync("/Artisans/Export?q=http")).Content.ReadAsStringAsync();
        Assert.Contains("XXXX XXXX", artisanCsv);
        Assert.DoesNotContain(BazaarTestHelpers.ValidAadhaar(9001), artisanCsv);

        // now both can be deleted
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/Artisans/Delete/{artisan}", token))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post("/Stalls/Delete/H-01", token))).StatusCode);
    }

    [Fact]
    public async Task Binding_errors_are_localized()
    {
        var client = _factory.ClientAs("Admin", "hi-IN");
        var token = await Token(client, "/Artisans/Index");
        var res = await client.SendAsync(Post("/Artisans/Create", token, new { fullName = "", dateOfBirth = "", gender = "Female", mobile = "", address = "", district = "", categoryId = "" }));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("ज़िला चुनें", errors.GetProperty("district").GetString());
        Assert.Equal("मान्य 10 अंकों का मोबाइल नंबर दर्ज करें", errors.GetProperty("mobile").GetString());
    }
}
