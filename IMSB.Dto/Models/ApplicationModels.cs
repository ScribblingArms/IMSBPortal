using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace IMSB.Dto.Models
{
    // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)

    /// <summary>New application and edit. On edit an empty Aadhaar keeps the number already on the application.</summary>
    public class ApplicationDto
    {
        [Required(ErrorMessage = "ValFullName"), StringLength(100, MinimumLength = 2, ErrorMessage = "ValFullName")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "ValDobRequired")]
        public string DateOfBirth { get; set; } = "";   // yyyy-MM-dd

        [Required(ErrorMessage = "ValGender")]
        public string Gender { get; set; } = "Female";

        [Required(ErrorMessage = "ValArtisanMobile")]
        public string Mobile { get; set; } = "";

        [RegularExpression(EmailAddresses.Pattern, ErrorMessage = "ValEmail"), StringLength(200, ErrorMessage = "ValEmail")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "ValAddress"), StringLength(500, MinimumLength = 5, ErrorMessage = "ValAddress")]
        public string Address { get; set; } = "";

        public string? Aadhaar { get; set; }

        [Required(ErrorMessage = "ValDistrict")]
        public string District { get; set; } = "";

        [Required(ErrorMessage = "ValCraftCategory")]
        public string CategoryId { get; set; } = "";

        [Range(0, 80, ErrorMessage = "ValExperience")]
        public int YearsOfExperience { get; set; }

        [StringLength(1000, ErrorMessage = "ValSkills")]
        public string? Skills { get; set; }

        /// <summary>Defaults to today.</summary>
        public string? SubmittedDate { get; set; }

        /// <summary>Optional, yyyy-MM-dd; To may not be before From.</summary>
        public string? FromDate { get; set; }

        public string? ToDate { get; set; }

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    /// <summary>Approval: optionally allot a vacant stall to the new artisan.</summary>
    public class ApproveApplicationDto
    {
        public string? StallCode { get; set; }

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    public record ApplicationStats(int Total, int Approved, int UnderReview, int Pending, int Rejected, int ApprovedThisMonth);

    public static class MobileNumbers
    {
        /// <summary>Only surrounding whitespace is dropped: "+91" prefixes, leading zeros and separators stay and fail <see cref="IsValid"/>.</summary>
        public static string Normalize(string? value) => (value ?? "").Trim();

        /// <summary>Exactly 10 digits, as an Indian mobile number (starts with 6-9). main.js IMSBValidate.mobile is the browser copy.</summary>
        public static bool IsValid(string normalized) => Regex.IsMatch(normalized, @"^[6-9][0-9]{9}\z");
    }

    public static class EmailAddresses
    {
        /// <summary>
        /// name@domain.tld: the name and each domain label may not be empty, a domain label may not start or end with a hyphen,
        /// and the last label is at least 2 letters. Written without backslashes so main.js can hold the identical text.
        /// </summary>
        public const string Pattern = "^[A-Za-z0-9_%+-]+([.][A-Za-z0-9_%+-]+)*@[A-Za-z0-9]+(-+[A-Za-z0-9]+)*([.][A-Za-z0-9]+(-+[A-Za-z0-9]+)*)*[.][A-Za-z]{2,}$";
    }
}
