using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    // The SERP tables below are created by the AddGmcDbObjects migration (Migrations/Scripts/GmcDb/Tables.sql)
    // and shared with the SERP attendance system. IMSBContext maps them with ExcludeFromMigrations(),
    // so EF reads and writes them but never creates or alters them.

    /// <summary>An employee of the Staff page: SERP.tbl_Employee.</summary>
    [Table("tbl_Employee", Schema = "SERP")]
    public class Employee
    {
        [Key]
        [Column("EMP_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmpId { get; set; }

        [Column("EMP_Name")]
        [MaxLength(100)]
        public string? EmpName { get; set; }

        /// <summary>
        /// The 12-digit Aadhaar number in plain digits, as the SERP procedures and attendance devices expect
        /// (USP_EMP_Registration checks duplicates on it). It is never sent to the browser; pages show <see cref="MaskedAadhaar"/>.
        /// </summary>
        [Column("Aadhar")]
        [MaxLength(12)]
        public string? Aadhar { get; set; }

        [Column("Mobile")]
        [MaxLength(10)]
        public string? Mobile { get; set; }

        /// <summary>SERP.tbl_EMP_Types; copied from the designation whenever the employee is saved.</summary>
        [Column("EmpTypeID")]
        public int? EmpTypeId { get; set; }

        /// <summary>SERP.tbl_Designation.DesigID: the employee's designation; the Staff form gives each employee their own row.</summary>
        [Column("Desig")]
        public int? DesigId { get; set; }

        /// <summary>dbo.departments.Id: the Staff Category. Added to tbl_Employee by the AddEmployeeDepartmentIdDropMonthlySalary migration.</summary>
        [Column("DepartmentID")]
        public int? DepartmentId { get; set; }

        /// <summary>SERP.tbl_Shifts.ShiftID.</summary>
        [Column("ShiftID")]
        public int? ShiftId { get; set; }

        /// <summary>Photo and fingerprint template captured by the SERP attendance app; the Staff page leaves them alone.</summary>
        [Column("ImageBase64")]
        public string? ImageBase64 { get; set; }

        [Column("Template")]
        public string? Template { get; set; }

        /// <summary>1 = active, 0 = inactive.</summary>
        [Column("IsActive")]
        public int? IsActive { get; set; }

        [Column("CreateOn")]
        public DateTime? CreateOn { get; set; }

        [Column("ModifiedOn")]
        public DateTime? ModifiedOn { get; set; }

        [Column("ImagePath")]
        [MaxLength(200)]
        public string? ImagePath { get; set; }

        [Column("Gender")]
        [MaxLength(10)]
        public string? Gender { get; set; }

        [Column("StallNum")]
        public int? StallNum { get; set; }

        /// <summary>SERP.tbl_ArtisansTypeAgency.AgencyID.</summary>
        [Column("AgencyID")]
        public int? AgencyId { get; set; }

        [Column("Remarks")]
        [MaxLength(200)]
        public string? Remarks { get; set; }

        /// <summary>Added to tbl_Employee by the AddEmployeeJoinDateSalaryAndLeaveQuotas migration.</summary>
        [Column("JoinDate", TypeName = "date")]
        public DateTime? JoinDate { get; set; }

        private bool? _hasPhoto;

        /// <summary>
        /// Whether ImageBase64 holds a photo. List queries leave the (large) photo out and set this instead;
        /// otherwise it is read from ImageBase64.
        /// </summary>
        [NotMapped]
        public bool HasPhoto
        {
            get => _hasPhoto ?? !string.IsNullOrEmpty(ImageBase64);
            set => _hasPhoto = value;
        }

        [NotMapped]
        public bool Active => IsActive == 1;

        [NotMapped]
        public string Status => Active ? EmployeeStatuses.Active : EmployeeStatuses.Inactive;

        [NotMapped]
        public string? MaskedAadhaar => string.IsNullOrEmpty(Aadhar) || Aadhar.Length < 4 ? null : "XXXX-XXXX-" + Aadhar[^4..];
    }

    /// <summary>The employee photo saved in tbl_Employee.ImageBase64: plain base64 of a JPEG or PNG.</summary>
    public static class EmployeePhotos
    {
        public const int MaxBytes = 500 * 1024;

        /// <summary>The image bytes of a data URL ("data:image/jpeg;base64,...") or plain base64; null if it is neither.</summary>
        public static byte[]? Decode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var s = value.Trim();
            if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = s.IndexOf(',');
                if (comma < 0 || !s[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
                s = s[(comma + 1)..];
            }
            var buffer = new byte[s.Length];
            return Convert.TryFromBase64String(s, buffer, out var written) ? buffer[..written] : null;
        }

        /// <summary>"image/jpeg" or "image/png" from the file's first bytes; null for anything else.</summary>
        public static string? ContentType(byte[] bytes) =>
            bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF ? "image/jpeg"
            : bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A ? "image/png"
            : null;

        /// <summary>True for a JPEG or PNG of at most <see cref="MaxBytes"/>.</summary>
        public static bool IsValid(string? value) => Decode(value) is { Length: > 0 and <= MaxBytes } bytes && ContentType(bytes) != null;
    }

    /// <summary>Statuses of the Staff page, stored as tbl_Employee.IsActive.</summary>
    public static class EmployeeStatuses
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Pattern = "^(ACTIVE|INACTIVE)$";
    }

    /// <summary>
    /// A designation: SERP.tbl_Designation. The Staff form gives every employee their own row (<see cref="EmpId"/>),
    /// holding their designation name and monthly salary (<see cref="BaseSalary"/>). Rows from the SERP app have no EmpId.
    /// </summary>
    [Table("tbl_Designation", Schema = "SERP")]
    public class EmployeeDesignation
    {
        [Key]
        [Column("DesigID")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int DesigId { get; set; }

        [Column("Designation")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("EmpTypeID")]
        public int? EmpTypeId { get; set; }

        /// <summary>The monthly salary of this designation (decimal(10,2): at most <see cref="MaxSalary"/>).</summary>
        [Column("BaseSalary", TypeName = "decimal(10,2)")]
        public decimal? BaseSalary { get; set; }

        public const decimal MaxSalary = 99999999.99m;
        public const int NameMaxLength = 50;

        [Column("ServicePct", TypeName = "decimal(5,2)")]
        public decimal? ServicePct { get; set; }

        [Column("GSTPercent", TypeName = "decimal(5,2)")]
        public decimal? GstPercent { get; set; }

        /// <summary>SERP.tbl_Employee.EMP_ID of the employee this row belongs to. Added by the EmployeesReplaceStaff migration.</summary>
        [Column("EmpId")]
        public int? EmpId { get; set; }
    }

    /// <summary>SERP.tbl_ArtisansTypeAgency: the agencies an employee can come through (tbl_Employee.AgencyID).</summary>
    [Table("tbl_ArtisansTypeAgency", Schema = "SERP")]
    public class ArtisansTypeAgency
    {
        [Key]
        [Column("AgencyID")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int AgencyId { get; set; }

        [Column("Agency")]
        [MaxLength(50)]
        public string? Name { get; set; }
    }

    /// <summary>SERP.tbl_EMP_Types (tbl_Employee.EmpTypeID).</summary>
    [Table("tbl_EMP_Types", Schema = "SERP")]
    public class EmployeeTypeMaster
    {
        [Key]
        [Column("EmpTypeID")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int EmpTypeId { get; set; }

        [Column("EmpType")]
        [MaxLength(50)]
        public string? Name { get; set; }
    }

    /// <summary>SERP.tbl_Shifts (tbl_Employee.ShiftID); each shift belongs to an employee type.</summary>
    [Table("tbl_Shifts", Schema = "SERP")]
    public class EmployeeShift
    {
        [Key]
        [Column("ShiftID")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ShiftId { get; set; }

        [Column("ShiftTime")]
        [MaxLength(50)]
        public string? ShiftTime { get; set; }

        [Column("EmpTypeID")]
        public int? EmpTypeId { get; set; }

        [Column("InTime")]
        public TimeSpan? InTime { get; set; }

        [Column("Outtime")]
        public TimeSpan? OutTime { get; set; }
    }
}
