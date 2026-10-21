using System.ComponentModel.DataAnnotations;

namespace IMSB.Dto.Models
{
    // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)
    public class LoginViewModel
    {
        [Required(ErrorMessage = "ValUsernameRequired")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "ValPasswordRequired")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }

        public string SelectedRole { get; set; } = "admin";

        public string? ReturnUrl { get; set; }
    }
}
