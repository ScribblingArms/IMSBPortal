using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("logins")]
    public class Login
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("Username")]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Column("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("Email")]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("Role")]
        [MaxLength(50)]
        public string Role { get; set; } = "Admin"; // Admin, Staff, Inventory

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("LastLogin")]
        public DateTime? LastLogin { get; set; }
    }
   
}
