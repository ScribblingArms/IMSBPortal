using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    /// <summary>
    /// The public stall application form (IMSB website), saved step by step:
    /// step 1 creates the row (SHG details), step 2 adds the representative, step 3 the product details,
    /// step 4 the stall requirements, step 5 the documents and declaration; submitting sets the reference number.
    /// The website updates the same row with <see cref="PublicId"/> (the int key is never sent to the browser),
    /// so the step 2-5 columns are nullable until those steps are saved.
    /// </summary>
    [Table("SHGDetails")]
    public class ShgDetail
    {
        public const int ShgIdMinLength = 6;
        public const int ShgIdMaxLength = 24;
        public const int MandalMaxLength = 100;
        public const int MinYearFormed = 1990;
        public const int MinMembers = 5;
        public const int MaxMembers = 25;
        public const int MinRepAge = 18;
        public const int MaxRepAge = 75;
        public const int MinDescriptionLength = 20;
        public const int MaxProductPhotos = 5;

        [Key]
        [Column("Id")]
        public int Id { get; set; }

        /// <summary>Handle the website uses to update this row from the same application session.</summary>
        [Column("PublicId")]
        public Guid PublicId { get; set; } = Guid.NewGuid();

        // ---------------- Step 1: SHG details ----------------

        [Required]
        [Column("ShgName")]
        [MaxLength(150)]
        public string ShgName { get; set; } = string.Empty;

        /// <summary>SHG ID as on the passbook / SERP records (letters, digits, / and -), stored upper-case.</summary>
        [Required]
        [Column("ShgId")]
        [MaxLength(ShgIdMaxLength)]
        public string ShgId { get; set; } = string.Empty;

        [Required]
        [Column("VillageOrganisation")]
        [MaxLength(150)]
        public string VillageOrganisation { get; set; } = string.Empty;

        [Required]
        [Column("MandalSamakhya")]
        [MaxLength(150)]
        public string MandalSamakhya { get; set; } = string.Empty;

        [Column("DistrictId")]
        public int DistrictId { get; set; }

        public District? District { get; set; }

        /// <summary>Mandal name as typed by the applicant.</summary>
        [Required]
        [Column("Mandal")]
        [MaxLength(MandalMaxLength)]
        public string Mandal { get; set; } = string.Empty;

        [Required]
        [Column("Village")]
        [MaxLength(150)]
        public string Village { get; set; } = string.Empty;

        [Column("YearFormed")]
        public int? YearFormed { get; set; }

        [Column("MemberCount")]
        public int MemberCount { get; set; }

        // ---------------- Step 2: representative and contact details ----------------

        [Column("RepName")]
        [MaxLength(100)]
        public string? RepName { get; set; }

        /// <summary>President, Secretary, Treasurer, Book keeper or Member.</summary>
        [Column("RepRole")]
        [MaxLength(30)]
        public string? RepRole { get; set; }

        [Column("RepAge")]
        public int? RepAge { get; set; }

        /// <summary>Father's or husband's name (optional).</summary>
        [Column("RepGuardianName")]
        [MaxLength(100)]
        public string? RepGuardianName { get; set; }

        /// <summary>10-digit Indian mobile number, without +91.</summary>
        [Column("RepMobile")]
        [MaxLength(10)]
        public string? RepMobile { get; set; }

        [Column("RepAltMobile")]
        [MaxLength(10)]
        public string? RepAltMobile { get; set; }

        [Column("RepEmail")]
        [MaxLength(200)]
        public string? RepEmail { get; set; }

        [Column("RepAddress")]
        [MaxLength(500)]
        public string? RepAddress { get; set; }

        [Column("RepPinCode")]
        [MaxLength(6)]
        public string? RepPinCode { get; set; }

        [Column("RepSavedAt")]
        public DateTime? RepSavedAt { get; set; }

        // ---------------- Step 3: product details ----------------

        /// <summary>Category from product_categories.</summary>
        [Column("ProductCategoryId")]
        public int? ProductCategoryId { get; set; }

        public ProductCategory? ProductCategory { get; set; }

        [Column("ProductType")]
        [MaxLength(150)]
        public string? ProductType { get; set; }

        [Column("ProductDescription")]
        [MaxLength(2000)]
        public string? ProductDescription { get; set; }

        [Column("PriceMin", TypeName = "decimal(12,2)")]
        public decimal? PriceMin { get; set; }

        [Column("PriceMax", TypeName = "decimal(12,2)")]
        public decimal? PriceMax { get; set; }

        [Column("ProductionCapacity")]
        [MaxLength(150)]
        public string? ProductionCapacity { get; set; }

        /// <summary>
        /// Stored product photo file names, separated by '|' (up to <see cref="MaxProductPhotos"/>).
        /// The files live in the website's upload folder under {PublicId}/.
        /// </summary>
        [Column("ProductPhotos")]
        [MaxLength(1000)]
        public string? ProductPhotos { get; set; }

        [Column("ProductSavedAt")]
        public DateTime? ProductSavedAt { get; set; }

        // ---------------- Step 4: stall requirements ----------------

        /// <summary>One of <see cref="StallOptions.Durations"/>.</summary>
        [Column("StallDuration")]
        [MaxLength(20)]
        public string? StallDuration { get; set; }

        /// <summary>Preferred start month, stored as the 1st of that month.</summary>
        [Column("StallStartMonth", TypeName = "date")]
        public DateTime? StallStartMonth { get; set; }

        /// <summary>Facilities needed (from <see cref="StallOptions.Facilities"/>), separated by '|'; null when none.</summary>
        [Column("StallFacilities")]
        [MaxLength(200)]
        public string? StallFacilities { get; set; }

        [Column("PreviousExhibition")]
        public bool? PreviousExhibition { get; set; }

        /// <summary>Which exhibitions and when; only kept when <see cref="PreviousExhibition"/> is true.</summary>
        [Column("PreviousExhibitionDetails")]
        [MaxLength(1000)]
        public string? PreviousExhibitionDetails { get; set; }

        [Column("StallSavedAt")]
        public DateTime? StallSavedAt { get; set; }

        // ---------------- Step 5: documents and declaration ----------------

        /// <summary>Stored file name of the SHG passbook page / certificate (optional), in the upload folder under {PublicId}/.</summary>
        [Column("ShgDocument")]
        [MaxLength(100)]
        public string? ShgDocument { get; set; }

        /// <summary>Stored file name of the representative's identity proof (optional).</summary>
        [Column("IdProofDocument")]
        [MaxLength(100)]
        public string? IdProofDocument { get; set; }

        [Column("DeclarationTrue")]
        public bool DeclarationTrue { get; set; }

        [Column("DeclarationConsent")]
        public bool DeclarationConsent { get; set; }

        /// <summary>When both declarations were confirmed.</summary>
        [Column("DeclarationSavedAt")]
        public DateTime? DeclarationSavedAt { get; set; }

        // ---------------- Submission ----------------

        /// <summary>Reference shown to the applicant, e.g. IMSB-2026-00042; set on submit.</summary>
        [Column("ApplicationRef")]
        [MaxLength(20)]
        public string? ApplicationRef { get; set; }

        /// <summary>Set when the application is submitted; after that the website cannot change the row.</summary>
        [Column("SubmittedAt")]
        public DateTime? SubmittedAt { get; set; }

        // ---------------- Review (admin portal, SHG Applications) ----------------

        public const int RejectionReasonMaxLength = 500;

        /// <summary>
        /// Set when an officer rejects the application (submitted or still incomplete); it then can no longer be given a
        /// stall, and the website can no longer change or submit it.
        /// </summary>
        [Column("RejectedAt")]
        public DateTime? RejectedAt { get; set; }

        [Column("RejectedBy")]
        [MaxLength(100)]
        public string? RejectedBy { get; set; }

        [Column("RejectionReason")]
        [MaxLength(RejectionReasonMaxLength)]
        public string? RejectionReason { get; set; }

        // ---------------- Audit ----------------

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        public IReadOnlyList<string> ProductPhotoFiles =>
            string.IsNullOrEmpty(ProductPhotos) ? Array.Empty<string>() : ProductPhotos.Split('|', StringSplitOptions.RemoveEmptyEntries);

        [NotMapped]
        public bool IsSubmitted => SubmittedAt.HasValue;

        [NotMapped]
        public bool IsRejected => RejectedAt.HasValue;

        /// <summary>Submitted or rejected: the website can no longer change the row.</summary>
        [NotMapped]
        public bool IsClosed => IsSubmitted || IsRejected;

        /// <summary>Marks the application (submitted or incomplete) as rejected by an officer, with the reason shown on the details.</summary>
        public void Reject(string reason, string? by, DateTime at)
        {
            if (IsRejected) throw new InvalidOperationException("The application has already been rejected.");
            RejectedAt = at;
            RejectedBy = by;
            RejectionReason = reason;
            UpdatedAt = at;
        }

        /// <summary>Steps 2-5 that have not been saved yet (step 1 always is, since it creates the row).</summary>
        [NotMapped]
        public IReadOnlyList<int> MissingSteps
        {
            get
            {
                var missing = new List<int>();
                if (RepSavedAt is null) missing.Add(2);
                if (ProductSavedAt is null) missing.Add(3);
                if (StallSavedAt is null) missing.Add(4);
                if (DeclarationSavedAt is null || !DeclarationTrue || !DeclarationConsent) missing.Add(5);
                return missing;
            }
        }
    }

    /// <summary>Choices offered in step 4 of the stall application.</summary>
    public static class StallOptions
    {
        public static readonly string[] Durations = { "15 days", "1 month", "3 months" };
        public static readonly string[] Facilities = { "Electricity", "Display table or rack", "Storage space", "Water connection (food stalls)" };

        /// <summary>Latest preferred start month that can be chosen, counted from next month.</summary>
        public const int MaxMonthsAhead = 12;
    }

    /// <summary>Roles a stall representative can hold in the SHG.</summary>
    public static class ShgRepresentativeRoles
    {
        public static readonly string[] All = { "President", "Secretary", "Treasurer", "Book keeper", "Member" };
    }
}
