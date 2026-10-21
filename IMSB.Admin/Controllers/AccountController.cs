using IMSB.Dto.Models;
using IMSB.Admin.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Security.Claims;
using IMSB.Interfaces;

namespace IMSB.Admin.Controllers
{
    public class AccountController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer _L;
        private readonly SessionService _sessions;
        private readonly IAccountInterface _account;

        public AccountController(IWebHostEnvironment env, IStringLocalizerFactory localizerFactory, SessionService sessions, IAccountInterface account)
        {
            _env = env;
            _account = account;
            _sessions = sessions;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(AccountController).Assembly.GetName().Name!);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Forgot()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Forgot(string usernameOrEmail)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail))
            {
                ModelState.AddModelError(string.Empty, _L["ValUsernameOrEmail"]);
                return View();
            }
            // Same message whether or not the account exists, so accounts cannot be enumerated
            ViewBag.Message = _L["ResetLinkSentMsg"].Value;

            var lookup = usernameOrEmail.Trim().ToLower();
            var login = await _account.ValidateLogincredentials(lookup);

            if (login == null || !login.IsActive)
            {
                return View();
            }
            // Only the newest link is valid
            var token = await _account.ValidatePasswordResetTokens(login.Id);
            // TODO: send the link by email. Until then it is shown on screen in Development only;
            // showing it in any other environment would let anyone reset any account's password.
            if (_env.IsDevelopment())
            {
                ViewBag.ResetUrl = Url.Action("Reset", "Account", new { token }, Request.Scheme);
            }
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Reset(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login");
            var prt = await _account.ValidatePasswordResetTokens(token);
            if (prt == null || prt.ExpiresAt < DateTime.UtcNow)
            {
                ViewBag.Error = _L["InvalidOrExpiredToken"].Value;
                return View(new ResetPasswordViewModel());
            }
            var model = new ResetPasswordViewModel { Token = token, Username = prt.Login?.Username };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var prt = await _account.ValidatePasswordResetTokens(model.Token);
            if (prt == null || prt.ExpiresAt < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, _L["InvalidOrExpiredToken"]);
                return View(model);
            }
            var login = prt.Login;
            if (login == null)
            {
                ModelState.AddModelError(string.Empty, _L["AccountNotFound"]);
                return View(model);
            }
            var hasher = new PasswordHasher<Login>();
            login.PasswordHash = hasher.HashPassword(login, model.NewPassword);
            // remove every outstanding token for this account (this also saves the new password hash)
            await _account.PasswordResetTokensID(login.Id);
            // a new password signs the account out everywhere
            await _sessions.RevokeAllAsync(login.Id, SessionEndReasons.PasswordReset);
            TempData["Message"] = _L["PasswordResetDone"].Value;
            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null, string? reason = null)
        {
            // why the user is back at the login page (set by SessionCookieEvents / Logout)
            ViewBag.Notice = reason switch
            {
                "expired" => _L["SessionExpiredMsg"].Value,
                "signedout" => _L["SessionEndedMsg"].Value,
                "disabled" => _L["AccountDisabled"].Value,
                _ => null
            };
            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            if (User.Identity?.IsAuthenticated == true && currentRole is "Admin" or "Staff" or "Inventory")
            {
                return RedirectToRoleHome(currentRole);
            }

            // Demo credentials are only exposed in Development (they are seeded at startup in Program.cs)
            if (_env.IsDevelopment())
            {
                ViewBag.DemoUsers = DemoUsers.All;
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (_env.IsDevelopment())
            {
                ViewBag.DemoUsers = DemoUsers.All;
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Lookup user by username or email
            var lookup = model.Username.Trim().ToLower();
            var login = await _account.ValidateLogincredentials(lookup);
            if (login == null)
            {
                ModelState.AddModelError(string.Empty, _L["InvalidCredentials"]);
                return View(model);
            }

            // verify password before revealing anything else about the account
            var hasher = new PasswordHasher<Login>();
            var verify = hasher.VerifyHashedPassword(login, login.PasswordHash, model.Password);
            if (verify == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, _L["InvalidCredentials"]);
                return View(model);
            }

            if (!login.IsActive)
            {
                ModelState.AddModelError(string.Empty, _L["AccountDisabled"]);
                return View(model);
            }

            // validate selected role
            var selected = (model.SelectedRole ?? string.Empty).Trim().ToLowerInvariant();
            string normalizedSelected = selected switch
            {
                "admin" => "Admin",
                "staff" => "Staff",
                "inventory" => "Inventory",
                _ => string.Empty
            };
            if (string.IsNullOrEmpty(normalizedSelected))
            {
                ModelState.AddModelError(string.Empty, _L["InvalidRoleSelected"]);
                return View(model);
            }

            if (!string.Equals(login.Role, normalizedSelected, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, _L["RoleNotAuthorized"]);
                return View(model);
            }

            if (verify == PasswordVerificationResult.SuccessRehashNeeded)
            {
                login.PasswordHash = hasher.HashPassword(login, model.Password);
            }

            var session = await _sessions.StartAsync(login, normalizedSelected, model.RememberMe,
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

            var claims = new List<Claim>
            {
                new Claim(SessionService.ClaimType, session.Id.ToString()),
                // tells the page whether to run the idle timer ("Remember me" sessions do not time out when idle)
                new Claim(SessionService.PersistentClaimType, session.IsPersistent ? "1" : "0"),
                new Claim(ClaimTypes.NameIdentifier, login.Id.ToString()),
                new Claim(ClaimTypes.Name, login.Username),
                new Claim("FullName", login.Username),
                new Claim(ClaimTypes.Role, normalizedSelected)
            };

            var claimsIdentity = new ClaimsIdentity(claims, "IMSB.Identity");
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = session.ExpiresAt
            };

            await HttpContext.SignInAsync("IMSB.Cookie", claimsPrincipal, authProperties);

            // update last login
            login.LastLogin = DateTime.UtcNow;
            await _account.SaveChangesAsync();

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }
            return RedirectToRoleHome(normalizedSelected);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string? reason = null)
        {
            // reason=expired: the page's idle timer ran out (main.js initSession)
            var idle = reason == "expired";
            if (CurrentSessionId() is Guid sid) await _sessions.RevokeAsync(sid, idle ? SessionEndReasons.IdleTimeout : SessionEndReasons.SignedOut);
            await HttpContext.SignOutAsync("IMSB.Cookie");
            return RedirectToAction("Login", "Account", idle ? new { reason = "expired" } : null);
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            ViewData["Title"] = _L["AccessDenied"].Value;
            return View();
        }

        // ── Session management ───────────────────────────────────────

        private Guid? CurrentSessionId() =>
            Guid.TryParse(User.FindFirstValue(SessionService.ClaimType), out var id) ? id : null;

        private Guid? CurrentLoginId() =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        /// <summary>"Stay signed in": the request itself is the activity; returns how long the session now has left.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KeepAlive()
        {
            if (CurrentSessionId() is not Guid sid) return Unauthorized();
            var (result, session) = await _sessions.ValidateAsync(sid, touch: false);
            if (result != SessionCheck.Valid || session == null) return Unauthorized();
            await _sessions.TouchAsync(session);
            return Ok(new { secondsLeft = _sessions.SecondsLeft(session), persistent = session.IsPersistent });
        }

        /// <summary>The user's own sessions; an admin also sees everyone's active sessions.</summary>
        [HttpGet]
        public async Task<IActionResult> Sessions()
        {
            ViewData["Title"] = _L["ActiveSessions"].Value;
            var loginId = CurrentLoginId();
            var mine = await _account.UserSessions(loginId ?? Guid.Empty);
            var active = await _sessions.ActiveIdsAsync();

            List<SessionRow>? everyone = null;
            if (User.IsInRole("Admin"))
            {
                everyone = (await _sessions.ActiveWithLoginAsync())
                    .Select(s => new SessionRow(s.Id, s.Login?.Username ?? "", s.Role, s.CreatedAt, s.LastSeenAt, s.ExpiresAt, s.IsPersistent, s.IpAddress, s.UserAgent, null, true))
                    .ToList();
            }

            ViewBag.CurrentSessionId = CurrentSessionId();
            ViewBag.Everyone = everyone;
            ViewBag.IdleMinutes = _sessions.Options.IdleTimeoutMinutes;
            return View(mine.Select(s => new SessionRow(s.Id, User.Identity?.Name ?? "", s.Role, s.CreatedAt, s.LastSeenAt, s.ExpiresAt,
                s.IsPersistent, s.IpAddress, s.UserAgent, s.RevokedReason, active.Contains(s.Id))).ToList());
        }

        public record SessionRow(Guid Id, string Username, string Role, DateTime CreatedAt, DateTime LastSeenAt, DateTime ExpiresAt,
            bool IsPersistent, string? IpAddress, string? UserAgent, string? EndedReason, bool IsActive);

        /// <summary>Ends one session: any of your own, or (admins) anyone's.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeSession(Guid id)
        {
            var session = await _account.GetUserSession(id);
            if (session == null) return NotFound();
            if (session.LoginId != CurrentLoginId() && !User.IsInRole("Admin")) return Forbid();
            await _sessions.RevokeAsync(id, SessionEndReasons.Revoked);
            TempData["SessionsMessage"] = _L["SessionRevokedMsg"].Value;
            // ending your own current session is a sign-out
            if (id == CurrentSessionId())
            {
                await HttpContext.SignOutAsync("IMSB.Cookie");
                return RedirectToAction("Login", new { reason = "signedout" });
            }
            return RedirectToAction(nameof(Sessions));
        }

        /// <summary>Signs out every other browser where this account is signed in.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeOtherSessions()
        {
            if (CurrentLoginId() is not Guid loginId) return Unauthorized();
            var n = await _sessions.RevokeAllAsync(loginId, SessionEndReasons.Revoked, except: CurrentSessionId());
            TempData["SessionsMessage"] = string.Format(_L["OtherSessionsRevokedMsg"].Value, n);
            return RedirectToAction(nameof(Sessions));
        }

        private IActionResult RedirectToRoleHome(string? role) => role switch
        {
            "Admin" => RedirectToAction("Index", "Dashboard"),
            "Staff" => RedirectToAction("Index", "Staff"),
            "Inventory" => RedirectToAction("Index", "Inventory"),
            _ => RedirectToAction("Login", "Account")
        };
    }
}
