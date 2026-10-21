using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("password_reset_tokens")]
    public class PasswordResetToken
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("login_id")]
        public Guid LoginId { get; set; }

        [Required]
        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("LoginId")]
        public Login? Login { get; set; }
    }
}
