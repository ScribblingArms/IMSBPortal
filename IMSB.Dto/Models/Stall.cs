using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    /// <summary>
    /// A physical market stall. It is occupied exactly when it has a holder: a registered artisan (<see cref="ArtisanId"/>),
    /// (<see cref="IsArtisan"/> true) or an SHG application from the website (<see cref="IsArtisan"/> false,
    /// <see cref="ShgDetailId"/> set), never both.
    /// </summary>
    [Table("stalls")]
    public class Stall
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        /// <summary>A-01. Fixed once created: products and sales refer to stalls by this code.</summary>
        [Required]
        [Column("StallCode")]
        [MaxLength(20)]
        public string StallCode { get; set; } = string.Empty;

        [Required]
        [Column("Name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("Block")]
        [MaxLength(20)]
        public string Block { get; set; } = string.Empty;   // "A" (Block A)

        [Column("RowLabel")]
        [MaxLength(20)]
        public string? RowLabel { get; set; }

        [Column("Location")]
        [MaxLength(500)]
        public string? Location { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = StallStatuses.Vacant;

        [Column("CategoryId")]
        public int? CategoryId { get; set; }

        public ProductCategory? Category { get; set; }

        [Column("ArtisanId")]
        public int? ArtisanId { get; set; }

        public Artisan? Artisan { get; set; }

        /// <summary>
        /// True when the stall is allotted to a registered artisan (Artisan / Stall management, or an approved artisan
        /// application). False when it is vacant, or allotted to an SHG application from the website (SHG Applications
        /// page): then <see cref="ArtisanId"/> is null and the holder and product category come from SHGDetails.
        /// </summary>
        [Column("IsArtisan")]
        public bool IsArtisan { get; set; }

        /// <summary>The SHG application holding the stall (then <see cref="IsArtisan"/> is false).</summary>
        [Column("ShgDetailId")]
        public int? ShgDetailId { get; set; }

        public ShgDetail? ShgDetail { get; set; }

        [Column("AllottedDate", TypeName = "date")]
        public DateTime? AllottedDate { get; set; }

        [Column("AgreementEndDate", TypeName = "date")]
        public DateTime? AgreementEndDate { get; set; }

        [Column("MonthlyRent", TypeName = "decimal(18,2)")]
        public decimal? MonthlyRent { get; set; }

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

        /// <summary>Held by an artisan or by an SHG application.</summary>
        [NotMapped]
        public bool IsHeld => ArtisanId != null || ShgDetailId != null;

        /// <summary>Name shown for the holder: the artisan's, or the SHG's (needs Artisan / ShgDetail loaded).</summary>
        [NotMapped]
        public string? HolderName => Artisan?.FullName ?? ShgDetail?.ShgName;

        /// <summary>Code shown for the holder: ART-0001, or the SHG application's reference number.</summary>
        [NotMapped]
        public string? HolderCode => Artisan?.ArtisanCode ?? ShgDetail?.ApplicationRef;

        /// <summary>Gives the stall to an artisan (any SHG holder is removed).</summary>
        public void AssignTo(Artisan artisan, DateTime allotted, DateTime? agreementEnd, int? categoryId)
        {
            ArtisanId = artisan.Id;
            IsArtisan = true;
            ShgDetailId = null;
            Occupy(allotted, agreementEnd, categoryId);
        }

        /// <summary>Gives the stall to an SHG application; its product category comes from the application.</summary>
        public void AssignTo(ShgDetail shg, DateTime allotted, DateTime? agreementEnd)
        {
            ArtisanId = null;
            IsArtisan = false;
            ShgDetailId = shg.Id;
            Occupy(allotted, agreementEnd, shg.ProductCategoryId);
        }

        /// <summary>Frees the stall from whoever holds it.</summary>
        public void ClearHolder()
        {
            ArtisanId = null;
            IsArtisan = false;
            ShgDetailId = null;
            Status = StallStatuses.Vacant;
            AllottedDate = null;
            AgreementEndDate = null;
        }

        private void Occupy(DateTime allotted, DateTime? agreementEnd, int? categoryId)
        {
            Status = StallStatuses.Occupied;
            AllottedDate = allotted;
            AgreementEndDate = agreementEnd;
            CategoryId = categoryId;
        }
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
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [Column("RequestNo")]
        [MaxLength(20)]
        public string RequestNo { get; set; } = string.Empty;   // REA-0001

        [Column("StallId")]
        public int StallId { get; set; }

        public Stall? Stall { get; set; }

        [Required]
        [Column("StallCode")]
        [MaxLength(20)]
        public string StallCode { get; set; } = string.Empty;

        [Column("CurrentArtisanId")]
        public int? CurrentArtisanId { get; set; }

        [Column("CurrentArtisanName")]
        [MaxLength(100)]
        public string? CurrentArtisanName { get; set; }

        /// <summary>The artisan who gets the stall on approval; null = the stall is vacated.</summary>
        [Column("NewArtisanId")]
        public int? NewArtisanId { get; set; }

        [Column("NewArtisanName")]
        [MaxLength(100)]
        public string? NewArtisanName { get; set; }

        [Required]
        [Column("Reason")]
        [MaxLength(30)]
        public string Reason { get; set; } = ReallocationReasons.Other;

        [Column("Remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Column("RequestedDate", TypeName = "date")]
        public DateTime RequestedDate { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = ReallocationStatuses.Pending;

        [Column("DecidedAt")]
        public DateTime? DecidedAt { get; set; }

        [Column("DecidedBy")]
        [MaxLength(100)]
        public string? DecidedBy { get; set; }

        [Column("DecisionRemarks")]
        [MaxLength(500)]
        public string? DecisionRemarks { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("CreatedBy")]
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
