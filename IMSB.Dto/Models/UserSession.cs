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
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("LoginId")]
        public Guid LoginId { get; set; }

        public Login? Login { get; set; }

        [Required]
        [Column("Role")]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column("LastSeenAt")]
        public DateTime LastSeenAt { get; set; }

        /// <summary>Absolute end of the session, however active it is.</summary>
        [Column("ExpiresAt")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>"Remember me" sessions are not ended by inactivity, only by ExpiresAt.</summary>
        [Column("IsPersistent")]
        public bool IsPersistent { get; set; }

        [Column("RevokedAt")]
        public DateTime? RevokedAt { get; set; }

        [Column("RevokedReason")]
        [MaxLength(30)]
        public string? RevokedReason { get; set; }

        [Column("IpAddress")]
        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [Column("UserAgent")]
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
