using System.Text.RegularExpressions;

namespace IMSB.Dto.Models
{
    /// <summary>User types the User Management screen gives; stored in logins.Role (the resource key of each is its label).</summary>
    public static class UserTypes
    {
        public const string DistrictOfficer = "DistrictOfficer";
        public const string StateAdminOfficer = "StateAdminOfficer";

        public static readonly string[] All = { DistrictOfficer, StateAdminOfficer };
    }

    /// <summary>The roles a login can sign in as, and the permissions each one carries.</summary>
    public static class UserRoles
    {
        public const string Admin = "Admin", Staff = "Staff", Inventory = "Inventory";

        /// <summary>The sign-in page's role choices: (value posted by the page, stored role, label resource key), in display order.</summary>
        public static readonly (string Value, string Role, string LabelKey)[] SignInChoices =
        {
            ("admin", Admin, "AdminRole"),
            ("staff", Staff, "StaffHR"),
            ("inventory", Inventory, "Inventory"),
            ("districtofficer", UserTypes.DistrictOfficer, UserTypes.DistrictOfficer),
            ("stateadminofficer", UserTypes.StateAdminOfficer, UserTypes.StateAdminOfficer),
        };

        /// <summary>The stored role for a choice posted by the sign-in page (case-insensitive), or null for an unknown one.</summary>
        public static string? FromSignInChoice(string? value) =>
            SignInChoices.Where(c => string.Equals(c.Value, value?.Trim(), StringComparison.OrdinalIgnoreCase)).Select(c => c.Role).FirstOrDefault();

        /// <summary>
        /// Roles that may open Stall Management, for [Authorize(Roles = ...)]: Admin and the two officer roles.
        /// Stall Management and SHG Applications are the only pages District Officers and State Admin Officers can open.
        /// </summary>
        public const string StallsAccess = Admin + "," + UserTypes.DistrictOfficer + "," + UserTypes.StateAdminOfficer;

        /// <summary>
        /// Roles that may open SHG Applications (the website's stall applications, SHGDetails): Admin and the two officer
        /// roles. A District Officer sees only their own district; Admin and State Admin Officers see every district.
        /// </summary>
        public const string ShgApplicationsAccess = Admin + "," + UserTypes.DistrictOfficer + "," + UserTypes.StateAdminOfficer;

        public static bool IsOfficer(string? role) => UserTypes.All.Contains(role);

        /// <summary>The page a role lands on after signing in: Stall Management for officers.</summary>
        public static string HomeController(string? role) => role switch
        {
            Staff => "Staff",
            Inventory => "Inventory",
            UserTypes.DistrictOfficer or UserTypes.StateAdminOfficer => "Stalls",
            _ => "Dashboard"
        };
    }

    /// <summary>
    /// Lookups over <see cref="TelanganaDistricts.All"/> for User Management: the stored (English) name and its
    /// "District_{letters of the name}" resource key, e.g. "Medchal-Malkajgiri" → "District_MedchalMalkajgiri".
    /// </summary>
    public static class DistrictNames
    {
        /// <summary>The stored name for a district (case-insensitive), or null when it is not a Telangana district.</summary>
        public static string? Find(string? name) =>
            TelanganaDistricts.All.FirstOrDefault(n => string.Equals(n, name?.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>The resource key of a stored district name, or null for an unknown name.</summary>
        public static string? ResourceKey(string? name) =>
            TelanganaDistricts.All.Contains(name) ? "District_" + Regex.Replace(name!, "[^A-Za-z]", "") : null;

        /// <summary>
        /// Whether two spellings name the same district, comparing letters only (case-insensitive): the officers' list
        /// (<see cref="TelanganaDistricts.All"/>) and the Districts table differ in places, e.g. "Medchal-Malkajgiri" /
        /// "Medchal–Malkajgiri" and "Rangareddy" / "Ranga Reddy".
        /// </summary>
        public static bool Same(string? a, string? b)
        {
            var x = Letters(a);
            return x.Length > 0 && x == Letters(b);
        }

        private static string Letters(string? name) => Regex.Replace(name ?? "", "[^A-Za-z]", "").ToLowerInvariant();

        /// <summary>The resource key for any spelling of a district (e.g. a Districts table name), or null.</summary>
        public static string? ResourceKeyFor(string? name) =>
            ResourceKey(TelanganaDistricts.All.FirstOrDefault(d => Same(d, name)));
    }

    /// <summary>The User Management form. On update an empty password keeps the current one.</summary>
    public class UserFormDto
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public string? Mobile { get; set; }
        public string? UserType { get; set; }
        public string? District { get; set; }

        public const int PasswordMin = 8, PasswordMax = 100, UserNameMin = 3, UserNameMax = 100, EmailMax = 200;

        private static readonly Regex UserNamePattern = new(@"^[A-Za-z0-9._@-]+\z", RegexOptions.Compiled);

        /// <summary>At least one upper-case letter, one lower-case letter, one digit and one other character (no spaces).</summary>
        public static bool IsStrongPassword(string password) =>
            password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit)
            && password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)) && !password.Any(char.IsWhiteSpace);

        /// <summary>
        /// Field → SharedResource key of the first problem with each field (snake_case fields, as the form's err_* elements);
        /// empty when valid. user-management.js holds the browser copy of these rules.
        /// </summary>
        public Dictionary<string, string> Validate(bool isNew)
        {
            var errors = new Dictionary<string, string>();
            var userName = UserName?.Trim() ?? "";
            if (userName.Length < UserNameMin || userName.Length > UserNameMax || !UserNamePattern.IsMatch(userName))
                errors["user_name"] = "ValUserName";

            var email = Email?.Trim() ?? "";
            if (email.Length == 0) errors["email"] = "ValEmailRequired";
            else if (email.Length > EmailMax || !Regex.IsMatch(email, EmailAddresses.Pattern)) errors["email"] = "ValEmail";

            // on update both may be left empty to keep the current password
            var password = Password ?? "";
            var confirm = ConfirmPassword ?? "";
            if (password.Length == 0)
            {
                if (isNew || confirm.Length > 0) errors["password"] = "ValPasswordRequired";
            }
            else if (password.Length < PasswordMin || password.Length > PasswordMax) errors["password"] = "ValPasswordLength";
            else if (!IsStrongPassword(password)) errors["password"] = "ValPasswordStrength";

            if (password.Length > 0 && confirm.Length == 0) errors["confirm_password"] = "ValConfirmPasswordRequired";
            else if (password.Length > 0 && password != confirm) errors["confirm_password"] = "ValPasswordsDoNotMatch";

            if (!MobileNumbers.IsValid(MobileNumbers.Normalize(Mobile))) errors["mobile"] = "ValMobile";
            if (!UserTypes.All.Contains(UserType?.Trim())) errors["user_type"] = "ValUserType";
            if (DistrictNames.Find(District) == null) errors["district"] = "ValDistrict";
            return errors;
        }
    }

    /// <summary>A website user as the User Management table and view show it (no password).</summary>
    public record ManagedUserRow(int Id, string UserName, string? Email, string? Mobile, string UserType, string? District, bool IsActive,
        DateTime CreatedAt, DateTime? LastLogin);
}
