using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    /// <summary>A physical market stall. It is occupied exactly when an artisan is allocated to it.</summary>
    [Table("stalls")]
    public class Stall
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>A-01. Fixed once created: products and sales refer to stalls by this code.</summary>
        [Required]
        [Column("stall_code")]
        [MaxLength(20)]
        public string StallCode { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("block")]
        [MaxLength(20)]
        public string Block { get; set; } = string.Empty;   // "A" (Block A)

        [Column("row_label")]
        [MaxLength(20)]
        public string? RowLabel { get; set; }

        [Column("location")]
        [MaxLength(500)]
        public string? Location { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = StallStatuses.Vacant;

        [Column("category_id")]
        public Guid? CategoryId { get; set; }

        public ProductCategory? Category { get; set; }

        [Column("artisan_id")]
        public Guid? ArtisanId { get; set; }

        public Artisan? Artisan { get; set; }

        [Column("allotted_date", TypeName = "date")]
        public DateTime? AllottedDate { get; set; }

        [Column("agreement_end_date", TypeName = "date")]
        public DateTime? AgreementEndDate { get; set; }

        [Column("monthly_rent", TypeName = "decimal(18,2)")]
        public decimal? MonthlyRent { get; set; }

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
    }

    public static class StallStatuses
    {
        public const string Vacant = "VACANT";
        public const string Occupied = "OCCUPIED";
        public const string Reserved = "RESERVED";       // held back, not allocatable until released
        public const string Maintenance = "MAINTENANCE";

        public static readonly string[] All = { Vacant, Occupied, Reserved, Maintenance };

        /// <summary>Statuses an admin can set on a stall without an artisan (OCCUPIED comes only from an allocation).</summary>
        public static readonly string[] Settable = { Vacant, Reserved, Maintenance };
    }

    /// <summary>A request to move a stall to another artisan, or to vacate it. Snapshots keep the history readable after deletes.</summary>
    [Table("stall_reallocations")]
    public class StallReallocation
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("request_no")]
        [MaxLength(20)]
        public string RequestNo { get; set; } = string.Empty;   // REA-0001

        [Column("stall_id")]
        public Guid StallId { get; set; }

        public Stall? Stall { get; set; }

        [Required]
        [Column("stall_code")]
        [MaxLength(20)]
        public string StallCode { get; set; } = string.Empty;

        [Column("current_artisan_id")]
        public Guid? CurrentArtisanId { get; set; }

        [Column("current_artisan_name")]
        [MaxLength(100)]
        public string? CurrentArtisanName { get; set; }

        /// <summary>The artisan who gets the stall on approval; null = the stall is vacated.</summary>
        [Column("new_artisan_id")]
        public Guid? NewArtisanId { get; set; }

        [Column("new_artisan_name")]
        [MaxLength(100)]
        public string? NewArtisanName { get; set; }

        [Required]
        [Column("reason")]
        [MaxLength(30)]
        public string Reason { get; set; } = ReallocationReasons.Other;

        [Column("remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Column("requested_date", TypeName = "date")]
        public DateTime RequestedDate { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = ReallocationStatuses.Pending;

        [Column("decided_at")]
        public DateTime? DecidedAt { get; set; }

        [Column("decided_by")]
        [MaxLength(100)]
        public string? DecidedBy { get; set; }

        [Column("decision_remarks")]
        [MaxLength(500)]
        public string? DecisionRemarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
    }

    public static class ReallocationStatuses
    {
        public const string Pending = "PENDING";
        public const string Approved = "APPROVED";
        public const string Rejected = "REJECTED";
    }

    public static class ReallocationReasons
    {
        public const string Closure = "CLOSURE";
        public const string CategoryChange = "CATEGORY_CHANGE";
        public const string ArtisanRequest = "ARTISAN_REQUEST";
        public const string NonPerformance = "NON_PERFORMANCE";
        public const string AgreementExpired = "AGREEMENT_EXPIRED";
        public const string Other = "OTHER";

        public static readonly string[] All = { Closure, CategoryChange, ArtisanRequest, NonPerformance, AgreementExpired, Other };
    }
}
