using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    /// <summary>
    /// A registered artisan. The Aadhaar number itself is never stored: only a SHA-256 hash (to stop duplicate
    /// registrations) and the last four digits (to show XXXX-XXXX-1234).
    /// </summary>
    [Table("artisans")]
    public class Artisan
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("ArtisanCode")]
        [MaxLength(20)]
        public string ArtisanCode { get; set; } = string.Empty;   // ART-0001

        [Required]
        [Column("FullName")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Column("DateOfBirth", TypeName = "date")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Column("Gender")]
        [MaxLength(10)]
        public string Gender { get; set; } = "Female";

        [Required]
        [Column("Mobile")]
        [MaxLength(15)]
        public string Mobile { get; set; } = string.Empty;

        [Column("Email")]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Required]
        [Column("Address")]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [Column("AadhaarHash")]
        [MaxLength(64)]
        public string AadhaarHash { get; set; } = string.Empty;

        [Required]
        [Column("AadhaarLast4")]
        [MaxLength(4)]
        public string AadhaarLast4 { get; set; } = string.Empty;

        [Required]
        [Column("District")]
        [MaxLength(60)]
        public string District { get; set; } = string.Empty;

        [Column("CategoryId")]
        public Guid CategoryId { get; set; }

        public ProductCategory? Category { get; set; }

        [Column("YearsOfExperience")]
        public int YearsOfExperience { get; set; }

        [Column("Skills")]
        [MaxLength(1000)]
        public string? Skills { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = ArtisanStatuses.Active;

        [Column("JoinedDate", TypeName = "date")]
        public DateTime JoinedDate { get; set; }

        /// <summary>Optional period entered on the registration / edit form.</summary>
        [Column("FromDate", TypeName = "date")]
        public DateTime? FromDate { get; set; }

        [Column("ToDate", TypeName = "date")]
        public DateTime? ToDate { get; set; }

        [Column("Remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("CreatedBy")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [Column("UpdatedBy")]
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        [NotMapped]
        public string MaskedAadhaar => "XXXX-XXXX-" + AadhaarLast4;
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
