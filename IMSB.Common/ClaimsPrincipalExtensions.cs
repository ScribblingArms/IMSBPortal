using System.Security.Claims;

namespace IMSB.Common
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>A Guid claim of the signed-in user (session id, login id), or null when missing or not a Guid.</summary>
        public static Guid? GuidClaim(this ClaimsPrincipal user, string claimType) =>
            Guid.TryParse(user.FindFirst(claimType)?.Value, out var id) ? id : null;

        /// <summary>An int claim of the signed-in user (login id), or null when missing or not a number.</summary>
        public static int? IntClaim(this ClaimsPrincipal user, string claimType) =>
            int.TryParse(user.FindFirst(claimType)?.Value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : null;
    }
}
