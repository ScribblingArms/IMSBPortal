using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace IMSB.Admin.Tests;

public class PortalAppFactory : StaffAppFactory { }

/// <summary>
/// The public website (landing page and stall application, moved from the former IMSB project) is served by IMSB.Admin
/// under /Portal: open to anonymous visitors, in its own layout and static files, translated from PortalResource.
/// </summary>
public class PortalIntegrationTests : IClassFixture<PortalAppFactory>
{
    private readonly PortalAppFactory _factory;

    public PortalIntegrationTests(PortalAppFactory factory) => _factory = factory;

    private static string Translation(string language, string english)
    {
        var adminDir = typeof(PortalIntegrationTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "AdminProjectDir").Value!;
        return XDocument.Load(Path.Combine(adminDir, "Resources", $"PortalResource.{language}.resx")).Root!.Elements("data")
            .Single(d => (string)d.Attribute("name")! == english).Element("value")!.Value;
    }

    [Theory]
    [InlineData("/Portal")]
    [InlineData("/Portal/Home/StallApply")]
    [InlineData("/Portal/Home/StallApplyForm")]
    [InlineData("/Portal/Home/Privacy")]
    public async Task Website_pages_are_open_to_visitors(string url)
    {
        var res = await _factory.ClientAs(null).GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
        Assert.Contains("Apply for a Stall", html);                       // portal layout, not the admin one
        Assert.Matches(@"href=""/portal/css/site\.css\?v=", html);         // portal static files, version-stamped (so they exist)
        Assert.Contains("href=\"/Portal/Home/StallApply\"", html);         // links stay in the area
    }

    [Fact]
    public async Task Admin_pages_still_require_sign_in()
    {
        var res = await _factory.ClientAs(null).GetAsync("/Dashboard");
        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
    }

    [Theory]
    [InlineData("te-IN", "te")]
    [InlineData("hi-IN", "hi")]
    public async Task Website_is_shown_in_the_chosen_language(string culture, string language)
    {
        var html = await _factory.ClientAs(null, culture).GetStringAsync("/Portal/Home/StallApply");
        // unescaped thanks to the encoder settings, and looked up in PortalResource.<language>.resx
        Assert.Contains(Translation(language, "Apply for a Stall"), html);
        Assert.Contains($"<html lang=\"{language}\">", html);
    }

    [Fact]
    public async Task Language_buttons_save_the_choice_and_return_to_the_page()
    {
        var res = await _factory.ClientAs(null).GetAsync("/Portal/Home/SetLanguage?culture=te&returnUrl=%2FPortal%2FHome%2FStallApply");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/Portal/Home/StallApply", res.Headers.Location!.OriginalString);
        Assert.Contains(res.Headers.GetValues("Set-Cookie"), c => c.Contains("te-IN"));

        var english = await _factory.ClientAs(null).GetAsync("/Portal/Home/SetLanguage?culture=en&returnUrl=https%3A%2F%2Fevil.example%2F");
        Assert.Equal("/Portal", english.Headers.Location!.OriginalString);   // never off the site
        Assert.Contains(english.Headers.GetValues("Set-Cookie"), c => c.Contains("en-US"));
    }

    [Theory]
    [InlineData(null, "Enter the name of your SHG")]
    [InlineData("te-IN", null)]
    public async Task Stall_form_step_1_validates_in_the_visitor_language(string? culture, string? expectedEnglish)
    {
        var client = _factory.ClientAs(null);
        var cultureCookie = culture == null ? "" : $".AspNetCore.Culture=c={culture}|uic={culture}; ";

        using var get = new HttpRequestMessage(HttpMethod.Get, "/Portal/Home/StallApplyForm");
        if (culture != null) get.Headers.Add("Cookie", cultureCookie.TrimEnd(' ', ';'));
        var formPage = await client.SendAsync(get);
        var token = Regex.Match(await formPage.Content.ReadAsStringAsync(),
            @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
        Assert.NotEmpty(token);
        var antiforgeryCookie = formPage.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Antiforgery")).Split(';')[0];

        using var post = new HttpRequestMessage(HttpMethod.Post, "/Portal/Home/SaveShgDetails")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["shg-members"] = "abc",
            })
        };
        post.Headers.Add("Cookie", cultureCookie + antiforgeryCookie);
        var res = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");
        var nameError = errors.GetProperty("shg-name").GetString();
        Assert.Equal(expectedEnglish ?? Translation("te", "Enter the name of your SHG"), nameError);
        // a value that is not a number gets the website's own message (the admin pages keep the framework's)
        Assert.Equal(culture == null ? "Enter a whole number" : Translation("te", "Enter a whole number"),
            errors.GetProperty("shg-members").GetString());
    }
}
