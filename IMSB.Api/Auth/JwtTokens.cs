using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IMSB.Backend;
using IMSB.Dto.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IMSB.Api.Auth
{
    public class JwtOptions
    {
        public const string Section = "Jwt";

        public string Issuer { get; set; } = "IMSB.Api";
        public string Audience { get; set; } = "IMSB.Mobile";

        /// <summary>HMAC-SHA256 key, at least 32 characters. Set it outside source control (user secrets / environment).</summary>
        public string SigningKey { get; set; } = string.Empty;

        public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
    }

    /// <summary>Claim types in the bearer token (inbound claim mapping is off, so they arrive as written).</summary>
    public static class ApiClaims
    {
        public const string LoginId = JwtRegisteredClaimNames.Sub;
        public const string Name = "name";
        public const string Role = "role";
        public const string SessionId = SessionService.ClaimType;
    }

    /// <summary>
    /// Issues bearer tokens for the mobile app. Each token carries a server-side session id (<see cref="SessionService"/>),
    /// so signing out, revoking a session or disabling the account ends the token before it expires.
    /// </summary>
    public class JwtTokens
    {
        private readonly JwtOptions _options;

        public JwtTokens(IOptions<JwtOptions> options) => _options = options.Value;

        public string Issue(Login login, UserSession session)
        {
            var claims = new[]
            {
                new Claim(ApiClaims.SessionId, session.Id.ToString()),
                new Claim(ApiClaims.LoginId, login.Id.ToString()),
                new Claim(ApiClaims.Name, login.Username),
                new Claim(ApiClaims.Role, session.Role)
            };
            var token = new JwtSecurityToken(
                _options.Issuer,
                _options.Audience,
                claims,
                notBefore: session.CreatedAt,
                expires: session.ExpiresAt,
                signingCredentials: new SigningCredentials(_options.Key(), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
