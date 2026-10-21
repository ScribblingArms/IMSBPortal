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

        [EmailAddress(ErrorMessage = "ValEmail"), StringLength(200, ErrorMessage = "ValEmail")]
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
        /// <summary>"+91 98765 43210", "098765 43210" and "98765-43210" all become 9876543210.</summary>
        public static string Normalize(string? value)
        {
            var mobile = Regex.Replace(value ?? "", @"[\s-]", "");
            if (mobile.StartsWith("+91")) mobile = mobile[3..];
            else if (mobile.Length == 11 && mobile.StartsWith("0")) mobile = mobile[1..];
            return mobile;
        }

        /// <summary>10-digit Indian mobile number.</summary>
        public static bool IsValid(string normalized) => Regex.IsMatch(normalized, "^[6-9][0-9]{9}$");
    }
}
