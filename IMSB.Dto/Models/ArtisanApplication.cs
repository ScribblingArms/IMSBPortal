using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    /// <summary>
    /// A request to register as an IMSB artisan. It moves PENDING (new) -> UNDER_REVIEW (processed by an admin)
    /// -> APPROVED (an Artisan is created, optionally with a stall) or REJECTED. Like Artisan, the Aadhaar number
    /// itself is never stored: only its hash and last four digits.
    /// </summary>
    [Table("artisan_applications")]
    public class ArtisanApplication
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("ApplicationNo")]
        [MaxLength(20)]
        public string ApplicationNo { get; set; } = string.Empty;   // APP-0001

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

        /// <summary>Notes entered with the application.</summary>
        [Column("Remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = ApplicationStatuses.Pending;

        [Column("SubmittedDate", TypeName = "date")]
        public DateTime SubmittedDate { get; set; }

        /// <summary>Optional period entered on the form; carried over to the artisan on approval.</summary>
        [Column("FromDate", TypeName = "date")]
        public DateTime? FromDate { get; set; }

        [Column("ToDate", TypeName = "date")]
        public DateTime? ToDate { get; set; }

        /// <summary>The admin who processed it (moved it to UNDER_REVIEW).</summary>
        [Column("Reviewer")]
        [MaxLength(100)]
        public string? Reviewer { get; set; }

        [Column("ReviewStartedAt")]
        public DateTime? ReviewStartedAt { get; set; }

        [Column("DecidedAt")]
        public DateTime? DecidedAt { get; set; }

        [Column("DecidedBy")]
        [MaxLength(100)]
        public string? DecidedBy { get; set; }

        [Column("DecisionRemarks")]
        [MaxLength(500)]
        public string? DecisionRemarks { get; set; }

        /// <summary>The artisan registered on approval (null once that artisan is deleted; ArtisanCode keeps the reference).</summary>
        [Column("ArtisanId")]
        public Guid? ArtisanId { get; set; }

        public Artisan? Artisan { get; set; }

        [Column("ArtisanCode")]
        [MaxLength(20)]
        public string? ArtisanCode { get; set; }

        /// <summary>Stall allotted on approval (snapshot).</summary>
        [Column("StallCode")]
        [MaxLength(20)]
        public string? StallCode { get; set; }

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

    public static class ApplicationStatuses
    {
        public const string Pending = "PENDING";          // new, not yet processed
        public const string UnderReview = "UNDER_REVIEW";
        public const string Approved = "APPROVED";
        public const string Rejected = "REJECTED";

        public static readonly string[] All = { Pending, UnderReview, Approved, Rejected };

        /// <summary>Still waiting for a decision: can be edited, approved or rejected.</summary>
        public static bool IsOpen(string status) => status == Pending || status == UnderReview;
    }
}
