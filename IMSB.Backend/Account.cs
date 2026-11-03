using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace IMSB.Backend
{
    public class Account : BackendBase, IAccountInterface
    {
        public Account(IMSBContext context) : base(context) { }

        public Task<Login?> ValidateLogincredentials(string lookup) =>
            _context.Logins.FirstOrDefaultAsync(l => l.Username.ToLower() == lookup || (l.Email != null && l.Email.ToLower() == lookup));

        public async Task<string> ValidatePasswordResetTokens(Guid loginid)
        {
            // only the newest link is valid
            var oldTokens = await _context.PasswordResetTokens.Where(p => p.LoginId == loginid).ToListAsync();
            _context.PasswordResetTokens.RemoveRange(oldTokens);
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                LoginId = loginid,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return token;
        }

        public Task<PasswordResetToken?> ValidatePasswordResetTokens(string token) =>
            _context.PasswordResetTokens.Include(p => p.Login).FirstOrDefaultAsync(p => p.Token == token);

        public async Task<bool> PasswordResetTokensID(Guid loginId)
        {
            var tokens = await _context.PasswordResetTokens.Where(p => p.LoginId == loginId).ToListAsync();
            _context.PasswordResetTokens.RemoveRange(tokens);
            await _context.SaveChangesAsync();
            return true;
        }

        public Task<List<UserSession>> UserSessions(Guid loginId) =>
            _context.UserSessions.AsNoTracking()
                .Where(s => s.LoginId == loginId)
                .OrderByDescending(s => s.LastSeenAt).Take(50).ToListAsync();

        public Task<UserSession?> GetUserSession(Guid sessionId) =>
            _context.UserSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);

        public async Task RecordLoginAsync(Login login)
        {
            login.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<(Login? Login, string Role, string? ErrorKey)> CheckSignInAsync(string usernameOrEmail, string password, string? selectedRole)
        {
            var login = await ValidateLogincredentials(usernameOrEmail.Trim().ToLower());
            if (login == null) return (null, "", "InvalidCredentials");

            // verify the password before revealing anything else about the account
            var hasher = new PasswordHasher<Login>();
            var verify = hasher.VerifyHashedPassword(login, login.PasswordHash, password);
            if (verify == PasswordVerificationResult.Failed) return (null, "", "InvalidCredentials");
            if (!login.IsActive) return (null, "", "AccountDisabled");

            var role = (selectedRole ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "admin" => "Admin",
                "staff" => "Staff",
                "inventory" => "Inventory",
                _ => string.Empty
            };
            if (role.Length == 0) return (null, "", "InvalidRoleSelected");
            if (!string.Equals(login.Role, role, StringComparison.OrdinalIgnoreCase)) return (null, "", "RoleNotAuthorized");

            if (verify == PasswordVerificationResult.SuccessRehashNeeded)
                login.PasswordHash = hasher.HashPassword(login, password);
            return (login, role, null);
        }

        public async Task<(Guid? LoginId, string? ErrorKey)> ResetPasswordAsync(string token, string newPassword)
        {
            var prt = await ValidatePasswordResetTokens(token);
            if (prt == null || prt.ExpiresAt < DateTime.UtcNow) return (null, "InvalidOrExpiredToken");
            var login = prt.Login;
            if (login == null) return (null, "AccountNotFound");

            login.PasswordHash = new PasswordHasher<Login>().HashPassword(login, newPassword);
            // remove every outstanding token for this account (this also saves the new password hash)
            await PasswordResetTokensID(login.Id);
            return (login.Id, null);
        }
    }
}
