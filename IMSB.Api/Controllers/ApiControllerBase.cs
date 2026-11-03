using IMSB.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace IMSB.Api.Controllers
{
    /// <summary>
    /// JSON endpoints for the mobile app. Errors use the admin portal's shape: 400 { errors: { field_name: message } },
    /// with messages in the request's language (Accept-Language: en-US, ta-IN, te-IN, hi-IN).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public abstract class ApiControllerBase : ControllerBase
    {
        private IStringLocalizer? _l;

        /// <summary>The shared translations (IMSB.Admin/Resources).</summary>
        protected IStringLocalizer L => _l ??= HttpContext.RequestServices.GetRequiredService<IStringLocalizer>();

        protected string? UserName => User.Identity?.Name;

        protected static BadRequestObjectResult Invalid(Dictionary<string, string> errors) => new(new { errors });

        protected BadRequestObjectResult Invalid(string key, string resourceKey, params object[] args) =>
            Invalid(new Dictionary<string, string> { [key] = L[resourceKey, args].Value });

        /// <summary>404 when not found, 400 { errors: { key: reason } } when refused, otherwise the success result.</summary>
        protected IActionResult FromResult(BackendResult result, string errorKey, Func<IActionResult> onSuccess) =>
            !result.Found ? NotFound()
            : result.ErrorKey != null ? Invalid(errorKey, result.ErrorKey)
            : onSuccess();
    }
}
