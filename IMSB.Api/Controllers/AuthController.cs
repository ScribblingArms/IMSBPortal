using System.ComponentModel.DataAnnotations;
using IMSB.Api.Auth;
using IMSB.Backend;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>Sign-in for the mobile app: a bearer token per device, backed by a server-side session.</summary>
    public class AuthController : ApiControllerBase
    {
        private readonly IAccountInterface _account;
        private readonly SessionService _sessions;
        private readonly JwtTokens _tokens;

        public AuthController(IAccountInterface account, SessionService sessions, JwtTokens tokens)
        {
            _account = account;
            _sessions = sessions;
            _tokens = tokens;
        }

        public class LoginRequest
        {
            [Required(ErrorMessage = "ValUsernameRequired")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "ValPasswordRequired")]
            public string Password { get; set; } = string.Empty;

            /// <summary>admin, staff or inventory: the role the account signs in with.</summary>
            public string Role { get; set; } = "admin";

            /// <summary>true: the token lasts Session:RememberMeDays; false: Session:AbsoluteTimeoutHours, ended early by inactivity.</summary>
            public bool RememberMe { get; set; }
        }

        public record LoginResponse(string Token, DateTime ExpiresAt, bool Persistent, int IdleTimeoutMinutes, UserInfo User);

        public record UserInfo(Guid Id, string Username, string Role);

        /// <summary>Signs in and returns the bearer token to send as "Authorization: Bearer {token}".</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var (login, role, error) = await _account.CheckSignInAsync(request.Username, request.Password, request.Role);
            if (login == null) return Invalid("login", error!);

            var session = await _sessions.StartAsync(login, role, request.RememberMe,
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            await _account.RecordLoginAsync(login);

            return Ok(new LoginResponse(_tokens.Issue(login, session), Utc(session.ExpiresAt), session.IsPersistent,
                _sessions.Options.IdleTimeoutMinutes, new UserInfo(login.Id, login.Username, role)));
        }

        /// <summary>Ends this device's session; the token stops working.</summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (CurrentSessionId() is Guid sid) await _sessions.RevokeAsync(sid, SessionEndReasons.SignedOut);
            return NoContent();
        }

        /// <summary>The signed-in user and how long the session has left.</summary>
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (CurrentSessionId() is not Guid sid) return Unauthorized();
            var (result, session) = await _sessions.ValidateAsync(sid, touch: false);
            if (result != SessionCheck.Valid || session == null) return Unauthorized();
            return Ok(new
            {
                user = new UserInfo(session.LoginId, UserName ?? "", session.Role),
                secondsLeft = _sessions.SecondsLeft(session),
                persistent = session.IsPersistent
            });
        }

        /// <summary>"Stay signed in": counts as activity and returns how long the session now has left.</summary>
        [HttpPost("keepalive")]
        public async Task<IActionResult> KeepAlive()
        {
            if (CurrentSessionId() is not Guid sid) return Unauthorized();
            var (result, session) = await _sessions.ValidateAsync(sid, touch: false);
            if (result != SessionCheck.Valid || session == null) return Unauthorized();
            await _sessions.TouchAsync(session);
            return Ok(new { secondsLeft = _sessions.SecondsLeft(session), persistent = session.IsPersistent });
        }

        public record SessionItem(Guid Id, string Username, string Role, DateTime CreatedAt, DateTime LastSeenAt, DateTime ExpiresAt,
            bool IsPersistent, string? IpAddress, string? UserAgent, string? EndedReason, bool IsActive, bool IsCurrent);

        /// <summary>The user's own sessions; an admin also gets everyone's active sessions.</summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> Sessions()
        {
            var current = CurrentSessionId();
            var active = await _sessions.ActiveIdsAsync();
            var mine = (await _account.UserSessions(CurrentLoginId() ?? Guid.Empty))
                .Select(s => new SessionItem(s.Id, UserName ?? "", s.Role, Utc(s.CreatedAt), Utc(s.LastSeenAt), Utc(s.ExpiresAt),
                    s.IsPersistent, s.IpAddress, s.UserAgent, s.RevokedReason, active.Contains(s.Id), s.Id == current));
            var everyone = User.IsInRole("Admin")
                ? (await _sessions.ActiveWithLoginAsync()).Select(s => new SessionItem(s.Id, s.Login?.Username ?? "", s.Role,
                    Utc(s.CreatedAt), Utc(s.LastSeenAt), Utc(s.ExpiresAt), s.IsPersistent, s.IpAddress, s.UserAgent, null, true, s.Id == current))
                : null;
            return Ok(new { mine, everyone, idleTimeoutMinutes = _sessions.Options.IdleTimeoutMinutes });
        }

        /// <summary>Ends one session: any of your own, or (admins) anyone's.</summary>
        [HttpDelete("sessions/{id:guid}")]
        public async Task<IActionResult> RevokeSession(Guid id)
        {
            var session = await _account.GetUserSession(id);
            if (session == null) return NotFound();
            if (session.LoginId != CurrentLoginId() && !User.IsInRole("Admin")) return Forbid();
            await _sessions.RevokeAsync(id, SessionEndReasons.Revoked);
            return NoContent();
        }

        /// <summary>Signs out every other device and browser where this account is signed in.</summary>
        [HttpPost("sessions/revoke-others")]
        public async Task<IActionResult> RevokeOtherSessions()
        {
            if (CurrentLoginId() is not Guid loginId) return Unauthorized();
            var revoked = await _sessions.RevokeAllAsync(loginId, SessionEndReasons.Revoked, except: CurrentSessionId());
            return Ok(new { revoked });
        }

        public class ForgotRequest
        {
            [Required(ErrorMessage = "ValUsernameOrEmail")]
            public string UsernameOrEmail { get; set; } = string.Empty;
        }

        /// <summary>Starts a password reset. The answer is the same whether or not the account exists.</summary>
        [HttpPost("forgot")]
        [AllowAnonymous]
        public async Task<IActionResult> Forgot([FromBody] ForgotRequest request)
        {
            var login = await _account.ValidateLogincredentials(request.UsernameOrEmail.Trim().ToLower());
            // only the newest link is valid; TODO: send it by email (it is never returned, or anyone could reset any account)
            if (login is { IsActive: true }) await _account.ValidatePasswordResetTokens(login.Id);
            return Ok(new { message = L["ResetLinkSentMsg"].Value });
        }

        /// <summary>Sets a new password with the emailed token; every session of the account is signed out.</summary>
        [HttpPost("reset")]
        [AllowAnonymous]
        public async Task<IActionResult> Reset([FromBody] ResetPasswordViewModel request)
        {
            var (loginId, error) = await _account.ResetPasswordAsync(request.Token, request.NewPassword);
            if (error != null) return Invalid("token", error);
            await _sessions.RevokeAllAsync(loginId!.Value, SessionEndReasons.PasswordReset);
            return Ok(new { message = L["PasswordResetDone"].Value });
        }

        private Guid? CurrentSessionId() =>
            Guid.TryParse(User.FindFirst(ApiClaims.SessionId)?.Value, out var id) ? id : null;

        private Guid? CurrentLoginId() =>
            Guid.TryParse(User.FindFirst(ApiClaims.LoginId)?.Value, out var id) ? id : null;
    }
}
