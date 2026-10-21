using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    /// <summary>
    /// One signed-in browser. The auth cookie only carries this row's id ("sid" claim); every request checks the row,
    /// so signing out, revoking, a password reset or disabling the account ends the session even if the cookie is kept.
    /// </summary>
    [Table("user_sessions")]
    public class UserSession
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("login_id")]
        public Guid LoginId { get; set; }

        public Login? Login { get; set; }

        [Required]
        [Column("role")]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("last_seen_at")]
        public DateTime LastSeenAt { get; set; }

        /// <summary>Absolute end of the session, however active it is.</summary>
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>"Remember me" sessions are not ended by inactivity, only by ExpiresAt.</summary>
        [Column("is_persistent")]
        public bool IsPersistent { get; set; }

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        [Column("revoked_reason")]
        [MaxLength(30)]
        public string? RevokedReason { get; set; }

        [Column("ip_address")]
        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [Column("user_agent")]
        [MaxLength(300)]
        public string? UserAgent { get; set; }
    }

    public static class SessionEndReasons
    {
        public const string SignedOut = "SIGNED_OUT";
        public const string Revoked = "REVOKED";
        public const string PasswordReset = "PASSWORD_RESET";
        public const string AccountDisabled = "ACCOUNT_DISABLED";
        public const string IdleTimeout = "IDLE_TIMEOUT";
        public const string Expired = "EXPIRED";
    }

    /// <summary>"Session" section of appsettings.json.</summary>
    public class UserSessionOptions
    {
        public const string Section = "Session";

        /// <summary>Signed out after this long without a request (normal sign-ins).</summary>
        public int IdleTimeoutMinutes { get; set; } = 30;

        /// <summary>Longest a normal sign-in lasts, active or not.</summary>
        public int AbsoluteTimeoutHours { get; set; } = 8;

        /// <summary>Lifetime of a "Remember me" sign-in.</summary>
        public int RememberMeDays { get; set; } = 7;

        /// <summary>How long before the idle timeout the page warns the user.</summary>
        public int WarningSeconds { get; set; } = 120;
    }
}
