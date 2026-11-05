using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace IMSB.Dto.Models
{
    // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)

    /// <summary>New application and edit. On edit an empty Aadhaar keeps the registered number.</summary>
    public class ArtisanDto
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

        /// <summary>Defaults to ACTIVE for a new application.</summary>
        public string? Status { get; set; }

        /// <summary>Defaults to today.</summary>
        public string? JoinedDate { get; set; }

        /// <summary>Optional, yyyy-MM-dd; To may not be before From.</summary>
        public string? FromDate { get; set; }

        public string? ToDate { get; set; }

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    public record ArtisanStats(int Total, int Active, int OnLeave, int RenewalPending, int Inactive, int WithStall);

    /// <summary>Aadhaar handling: the length check, and the hash/last-4 that are stored instead of the number.</summary>
    public static class Aadhaar
    {
        /// <summary>Digits only ("1234 5678 9012" -> "123456789012").</summary>
        public static string Normalize(string? value) => new((value ?? "").Where(char.IsDigit).ToArray());

        /// <summary>
        /// Exactly 12 digits; hyphens and spaces between them are ignored. The digits themselves are not checked.
        /// main.js IMSBValidate.aadhaar is the browser copy.
        /// </summary>
        public static bool IsValid(string? value) =>
            System.Text.RegularExpressions.Regex.IsMatch(
                System.Text.RegularExpressions.Regex.Replace(value ?? "", @"[-\s]", ""), @"^[0-9]{12}\z");

        public static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("IMSB-Aadhaar:" + Normalize(value))));

        public static string Last4(string value) => Normalize(value)[^4..];
    }
}
