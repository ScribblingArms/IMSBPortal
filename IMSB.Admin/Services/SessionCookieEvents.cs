using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IMSB.Admin.Services
{
    /// <summary>
    /// Ties the auth cookie to a server-side session: every request re-checks the session, and requests made by page
    /// scripts get 401/403 instead of an HTML redirect so the page can react (show "session expired").
    /// </summary>
    public class SessionCookieEvents : CookieAuthenticationEvents
    {
        public const string EndedItemKey = "IMSB.SessionEnded";
        public const string EndedHeader = "X-Session-Ended";

        private readonly SessionService _sessions;

        public SessionCookieEvents(SessionService sessions) => _sessions = sessions;

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var sid = context.Principal?.FindFirst(SessionService.ClaimType)?.Value;
            var result = Guid.TryParse(sid, out var id) ? (await _sessions.ValidateAsync(id)).Result : SessionCheck.NotFound;
            if (result == SessionCheck.Valid) return;

            context.RejectPrincipal();
            context.HttpContext.Items[EndedItemKey] = result;
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
        }

        public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
        {
            var ended = context.HttpContext.Items.TryGetValue(EndedItemKey, out var r) ? (SessionCheck?)r : null;
            if (IsScriptRequest(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                if (ended != null) context.Response.Headers[EndedHeader] = ended.ToString();
                return Task.CompletedTask;
            }
            var uri = context.RedirectUri;
            if (ended != null) uri += (uri.Contains('?') ? "&" : "?") + "reason=" + ReasonFor(ended.Value);
            context.Response.Redirect(uri);
            return Task.CompletedTask;
        }

        public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
        {
            if (IsScriptRequest(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            return base.RedirectToAccessDenied(context);
        }

        /// <summary>Browser navigations ask for HTML; fetch() calls and form-less posts from scripts do not.</summary>
        public static bool IsScriptRequest(HttpRequest request) =>
            !HttpMethods.IsGet(request.Method) && !request.HasFormContentType
            || !request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);

        public static string ReasonFor(SessionCheck check) => check switch
        {
            SessionCheck.IdleTimeout or SessionCheck.Expired => "expired",
            SessionCheck.AccountDisabled => "disabled",
            _ => "signedout"
        };
    }
}
