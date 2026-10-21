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

        /// <summary>Defaults to ACTIVE for a new application.</summary>
        public string? Status { get; set; }

        /// <summary>Defaults to today.</summary>
        public string? JoinedDate { get; set; }

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    public record ArtisanStats(int Total, int Active, int OnLeave, int RenewalPending, int Inactive, int WithStall);

    /// <summary>Aadhaar handling: format + Verhoeff checksum validation, and the hash/last-4 that are stored instead of the number.</summary>
    public static class Aadhaar
    {
        private static readonly int[,] D =
        {
            {0,1,2,3,4,5,6,7,8,9},{1,2,3,4,0,6,7,8,9,5},{2,3,4,0,1,7,8,9,5,6},{3,4,0,1,2,8,9,5,6,7},{4,0,1,2,3,9,5,6,7,8},
            {5,9,8,7,6,0,4,3,2,1},{6,5,9,8,7,1,0,4,3,2},{7,6,5,9,8,2,1,0,4,3},{8,7,6,5,9,3,2,1,0,4},{9,8,7,6,5,4,3,2,1,0}
        };
        private static readonly int[,] P =
        {
            {0,1,2,3,4,5,6,7,8,9},{1,5,7,6,2,8,3,0,9,4},{5,8,0,3,7,9,6,1,4,2},{8,9,1,6,0,4,3,5,2,7},
            {9,4,5,3,1,2,6,8,7,0},{4,2,8,6,5,7,3,9,0,1},{2,7,9,3,8,0,6,4,1,5},{7,0,4,6,9,1,3,2,5,8}
        };
        private static readonly int[] Inv = { 0, 4, 3, 2, 1, 5, 6, 7, 8, 9 };

        /// <summary>Digits only ("1234 5678 9012" -> "123456789012").</summary>
        public static string Normalize(string? value) => new((value ?? "").Where(char.IsDigit).ToArray());

        /// <summary>12 digits, not starting with 0 or 1, with a valid Verhoeff check digit (as issued by UIDAI).</summary>
        public static bool IsValid(string? value)
        {
            var digits = Normalize(value);
            if (digits.Length != 12 || digits[0] == '0' || digits[0] == '1') return false;
            if ((value ?? "").Any(c => !char.IsDigit(c) && c != ' ' && c != '-')) return false;
            var c = 0;
            for (int i = 0; i < digits.Length; i++)
                c = D[c, P[i % 8, digits[digits.Length - 1 - i] - '0']];
            return c == 0;
        }

        /// <summary>Check digit to append to 11 digits (used to build valid test/demo numbers).</summary>
        public static char CheckDigit(string elevenDigits)
        {
            var c = 0;
            for (int i = 0; i < elevenDigits.Length; i++)
                c = D[c, P[(i + 1) % 8, elevenDigits[elevenDigits.Length - 1 - i] - '0']];
            return (char)('0' + Inv[c]);
        }

        public static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("IMSB-Aadhaar:" + Normalize(value))));

        public static string Last4(string value) => Normalize(value)[^4..];
    }
}
