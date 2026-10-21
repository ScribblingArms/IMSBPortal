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
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("application_no")]
        [MaxLength(20)]
        public string ApplicationNo { get; set; } = string.Empty;   // APP-0001

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

        /// <summary>Notes entered with the application.</summary>
        [Column("remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = ApplicationStatuses.Pending;

        [Column("submitted_date", TypeName = "date")]
        public DateTime SubmittedDate { get; set; }

        /// <summary>The admin who processed it (moved it to UNDER_REVIEW).</summary>
        [Column("reviewer")]
        [MaxLength(100)]
        public string? Reviewer { get; set; }

        [Column("review_started_at")]
        public DateTime? ReviewStartedAt { get; set; }

        [Column("decided_at")]
        public DateTime? DecidedAt { get; set; }

        [Column("decided_by")]
        [MaxLength(100)]
        public string? DecidedBy { get; set; }

        [Column("decision_remarks")]
        [MaxLength(500)]
        public string? DecisionRemarks { get; set; }

        /// <summary>The artisan registered on approval (null once that artisan is deleted; ArtisanCode keeps the reference).</summary>
        [Column("artisan_id")]
        public Guid? ArtisanId { get; set; }

        public Artisan? Artisan { get; set; }

        [Column("artisan_code")]
        [MaxLength(20)]
        public string? ArtisanCode { get; set; }

        /// <summary>Stall allotted on approval (snapshot).</summary>
        [Column("stall_code")]
        [MaxLength(20)]
        public string? StallCode { get; set; }

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
