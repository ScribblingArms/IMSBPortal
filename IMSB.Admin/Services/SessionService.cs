using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IMSB.Admin.Services
{
    public enum SessionCheck { Valid, NotFound, Revoked, IdleTimeout, Expired, AccountDisabled }

    /// <summary>Starts, checks, refreshes and ends server-side sign-in sessions (see <see cref="UserSession"/>).</summary>
    public class SessionService
    {
        /// <summary>Claim on the auth cookie that holds the session id.</summary>
        public const string ClaimType = "sid";
        public const string PersistentClaimType = "sid_persistent";

        /// <summary>LastSeenAt is written at most this often, so ordinary page use does not write on every request.</summary>
        public static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

        /// <summary>Ended sessions are kept this long for the history list, then pruned at the user's next sign-in.</summary>
        public static readonly TimeSpan KeepEndedFor = TimeSpan.FromDays(30);

        private readonly IMSBContext _context;
        private readonly UserSessionOptions _options;
        private readonly TimeProvider _clock;

        public SessionService(IMSBContext context, IOptions<UserSessionOptions> options, TimeProvider clock)
        {
            _context = context;
            _options = options.Value;
            _clock = clock;
        }

        public DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;
        public TimeSpan IdleTimeout => TimeSpan.FromMinutes(_options.IdleTimeoutMinutes);
        public UserSessionOptions Options => _options;

        public async Task<UserSession> StartAsync(Login login, string role, bool rememberMe, string? ipAddress, string? userAgent)
        {
            var now = UtcNow;
            var cutoff = now - KeepEndedFor;
            var stale = await _context.UserSessions
                .Where(s => s.LoginId == login.Id && ((s.RevokedAt != null && s.RevokedAt < cutoff) || s.ExpiresAt < cutoff))
                .ToListAsync();
            _context.UserSessions.RemoveRange(stale);

            var session = new UserSession
            {
                LoginId = login.Id,
                Role = role,
                CreatedAt = now,
                LastSeenAt = now,
                ExpiresAt = rememberMe ? now.AddDays(_options.RememberMeDays) : now.AddHours(_options.AbsoluteTimeoutHours),
                IsPersistent = rememberMe,
                IpAddress = Truncate(ipAddress, 64),
                UserAgent = Truncate(userAgent, 300)
            };
            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();
            return session;
        }

        /// <summary>Checks a session for the current request. A session that has run out is closed on the spot; a valid one is refreshed.</summary>
        public async Task<(SessionCheck Result, UserSession? Session)> ValidateAsync(Guid sessionId, bool touch = true)
        {
            var session = await _context.UserSessions.Include(s => s.Login).FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null) return (SessionCheck.NotFound, null);
            if (session.RevokedAt != null) return (SessionCheck.Revoked, session);

            var now = UtcNow;
            var result =
                session.Login == null || !session.Login.IsActive ? SessionCheck.AccountDisabled
                : now >= session.ExpiresAt ? SessionCheck.Expired
                : !session.IsPersistent && now - session.LastSeenAt >= IdleTimeout ? SessionCheck.IdleTimeout
                : SessionCheck.Valid;

            if (result != SessionCheck.Valid)
            {
                End(session, result switch
                {
                    SessionCheck.AccountDisabled => SessionEndReasons.AccountDisabled,
                    SessionCheck.Expired => SessionEndReasons.Expired,
                    _ => SessionEndReasons.IdleTimeout
                }, now);
                await _context.SaveChangesAsync();
                return (result, session);
            }

            if (touch && now - session.LastSeenAt >= TouchInterval)
            {
                session.LastSeenAt = now;
                await _context.SaveChangesAsync();
            }
            return (SessionCheck.Valid, session);
        }

        /// <summary>Seconds until the session ends if nothing else happens (idle limit or absolute end, whichever is first).</summary>
        public int SecondsLeft(UserSession session)
        {
            var end = session.IsPersistent ? session.ExpiresAt : Min(session.ExpiresAt, session.LastSeenAt + IdleTimeout);
            return Math.Max(0, (int)(end - UtcNow).TotalSeconds);
        }

        /// <summary>Marks the session as seen now (the "Stay signed in" button).</summary>
        public async Task TouchAsync(UserSession session)
        {
            session.LastSeenAt = UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> RevokeAsync(Guid sessionId, string reason)
        {
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null || session.RevokedAt != null) return false;
            End(session, reason, UtcNow);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>Ends every open session of a login (optionally keeping one). Returns how many were ended.</summary>
        public async Task<int> RevokeAllAsync(Guid loginId, string reason, Guid? except = null)
        {
            var now = UtcNow;
            var open = await _context.UserSessions
                .Where(s => s.LoginId == loginId && s.RevokedAt == null && s.ExpiresAt > now && s.Id != except)
                .ToListAsync();
            open.ForEach(s => End(s, reason, now));
            await _context.SaveChangesAsync();
            return open.Count;
        }

        /// <summary>Sessions that would still pass validation now.</summary>
        public IQueryable<UserSession> Active()
        {
            var now = UtcNow;
            var idleCutoff = now - IdleTimeout;
            return _context.UserSessions.Where(s => s.RevokedAt == null && s.ExpiresAt > now && (s.IsPersistent || s.LastSeenAt > idleCutoff));
        }

        public async Task<HashSet<Guid>> ActiveIdsAsync() => (await Active().Select(s => s.Id).ToListAsync()).ToHashSet();

        /// <summary>Every active session with its login, most recently used first (the admin's view).</summary>
        public Task<List<UserSession>> ActiveWithLoginAsync() =>
            Active().AsNoTracking().Include(s => s.Login).OrderByDescending(s => s.LastSeenAt).ToListAsync();

        private static void End(UserSession session, string reason, DateTime now)
        {
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }

        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
        private static string? Truncate(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
    }
}
