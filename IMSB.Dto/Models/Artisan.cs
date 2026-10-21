using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    /// <summary>
    /// A registered artisan. The Aadhaar number itself is never stored: only a SHA-256 hash (to stop duplicate
    /// registrations) and the last four digits (to show XXXX XXXX 1234).
    /// </summary>
    [Table("artisans")]
    public class Artisan
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("artisan_code")]
        [MaxLength(20)]
        public string ArtisanCode { get; set; } = string.Empty;   // ART-0001

        [Required]
        [Column("full_name")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Column("date_of_birth", TypeName = "date")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Column("gender")]
        [MaxLength(10)]
        public string Gender { get; set; } = "Female";

        [Required]
        [Column("mobile")]
        [MaxLength(15)]
        public string Mobile { get; set; } = string.Empty;

        [Column("email")]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Required]
        [Column("address")]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [Column("aadhaar_hash")]
        [MaxLength(64)]
        public string AadhaarHash { get; set; } = string.Empty;

        [Required]
        [Column("aadhaar_last4")]
        [MaxLength(4)]
        public string AadhaarLast4 { get; set; } = string.Empty;

        [Required]
        [Column("district")]
        [MaxLength(60)]
        public string District { get; set; } = string.Empty;

        [Column("category_id")]
        public Guid CategoryId { get; set; }

        public ProductCategory? Category { get; set; }

        [Column("years_of_experience")]
        public int YearsOfExperience { get; set; }

        [Column("skills")]
        [MaxLength(1000)]
        public string? Skills { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = ArtisanStatuses.Active;

        [Column("joined_date", TypeName = "date")]
        public DateTime JoinedDate { get; set; }

        [Column("remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("updated_by")]
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        [NotMapped]
        public string MaskedAadhaar => "XXXX XXXX " + AadhaarLast4;
    }

    public static class ArtisanStatuses
    {
        public const string Active = "ACTIVE";
        public const string OnLeave = "ON_LEAVE";
        public const string RenewalDue = "RENEWAL_DUE";   // "Renewal Pending"
        public const string Inactive = "INACTIVE";

        public static readonly string[] All = { Active, OnLeave, RenewalDue, Inactive };

        /// <summary>Artisans who can be given a stall.</summary>
        public static bool CanHoldStall(string status) => status != Inactive;
    }

    /// <summary>Districts of Telangana, used by the artisan registration form and its validation.</summary>
    public static class TelanganaDistricts
    {
        public static readonly string[] All =
        {
            "Adilabad", "Bhadradri Kothagudem", "Hanumakonda", "Hyderabad", "Jagtial", "Jangaon", "Jayashankar Bhupalpally",
            "Jogulamba Gadwal", "Kamareddy", "Karimnagar", "Khammam", "Kumuram Bheem Asifabad", "Mahabubabad", "Mahabubnagar",
            "Mancherial", "Medak", "Medchal-Malkajgiri", "Mulugu", "Nagarkurnool", "Nalgonda", "Narayanpet", "Nirmal",
            "Nizamabad", "Peddapalli", "Rajanna Sircilla", "Rangareddy", "Sangareddy", "Siddipet", "Suryapet", "Vikarabad",
            "Wanaparthy", "Warangal", "Yadadri Bhuvanagiri"
        };
    }
}
