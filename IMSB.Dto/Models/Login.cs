using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("logins")]
    public class Login
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("username")]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("email")]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("role")]
        [MaxLength(50)]
        public string Role { get; set; } = "Admin"; // Admin, Staff, Inventory

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }
    }
   
}
