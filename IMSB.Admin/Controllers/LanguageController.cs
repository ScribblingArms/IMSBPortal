using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Controllers
{
    [AllowAnonymous]
    public class LanguageController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            if (string.IsNullOrEmpty(culture))
            {
                return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/"));
            }

            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, cookieValue, new Microsoft.AspNetCore.Http.CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                Path = "/"
            });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return LocalRedirect("/");
        }
    }
}
