using IMSB.Admin.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace IMSB.Admin.Tests;

public class LanguageControllerTests
{
    private static LanguageController Controller(out DefaultHttpContext http)
    {
        http = new DefaultHttpContext();
        var controller = new LanguageController { ControllerContext = new ControllerContext { HttpContext = http } };
        controller.Url = new UrlHelper(new ActionContext(http, new RouteData(), new ActionDescriptor()));
        return controller;
    }

    private static string? CultureCookie(HttpContext http) =>
        http.Response.Headers.SetCookie.FirstOrDefault(c => c!.StartsWith(CookieRequestCultureProvider.DefaultCookieName + "="));

    [Theory]
    [InlineData("ta-IN")]
    [InlineData("hi-IN")]
    public void Sets_the_culture_cookie_and_returns_to_the_page(string culture)
    {
        var result = Controller(out var http).SetLanguage(culture, "/Stalls/Index");
        Assert.Equal("/Stalls/Index", Assert.IsType<LocalRedirectResult>(result).Url);
        var cookie = CultureCookie(http);
        Assert.NotNull(cookie);
        Assert.Contains(Uri.EscapeDataString($"c={culture}|uic={culture}"), cookie);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://evil.example.com/")]
    [InlineData("//evil.example.com")]
    [InlineData(null)]
    public void Never_redirects_off_site(string? returnUrl)
    {
        var result = Controller(out _).SetLanguage("te-IN", returnUrl!);
        Assert.Equal("/", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Fact]
    public void Missing_culture_leaves_the_cookie_alone()
    {
        var result = Controller(out var http).SetLanguage("", "/Dashboard/Index");
        Assert.Equal("/Dashboard/Index", Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Null(CultureCookie(http));
    }
}
