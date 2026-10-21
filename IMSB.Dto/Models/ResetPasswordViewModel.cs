using System.ComponentModel.DataAnnotations;

namespace IMSB.Dto.Models
{
    // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; } = "";

        public string? Username { get; set; }

        [Required(ErrorMessage = "ValPasswordRequired"), StringLength(100, MinimumLength = 8, ErrorMessage = "ValPasswordLength")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "ValPasswordRequired"), Compare("NewPassword", ErrorMessage = "ValPasswordsDoNotMatch")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
