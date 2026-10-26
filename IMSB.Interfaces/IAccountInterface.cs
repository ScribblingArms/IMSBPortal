using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    public interface IAccountInterface
    {
        /// <summary>Tracked login whose username or email matches (lookup is already trimmed and lower-cased).</summary>
        Task<Login?> ValidateLogincredentials(string usernameOrEmail);

        /// <summary>Replaces the login's reset tokens with a new one-hour token and returns it.</summary>
        Task<string> ValidatePasswordResetTokens(Guid loginid);

        /// <summary>The reset token with its (tracked) login, or null.</summary>
        Task<PasswordResetToken?> ValidatePasswordResetTokens(string token);

        /// <summary>Removes every outstanding reset token of the login and saves pending changes.</summary>
        Task<bool> PasswordResetTokensID(Guid loginId);

        /// <summary>The login's 50 most recently used sessions.</summary>
        Task<List<UserSession>> UserSessions(Guid loginId);

        Task<UserSession?> GetUserSession(Guid sessionId);

        /// <summary>Stamps the tracked login's last sign-in and saves it (with a rehashed password, if any).</summary>
        Task RecordLoginAsync(Login login);
    }
}
