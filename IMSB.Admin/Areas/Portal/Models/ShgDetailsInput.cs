using System.ComponentModel.DataAnnotations;
using System.Globalization;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Areas.Portal.Models
{
    // Steps 1-5 of the stall application form, as posted by stall-form.js. Field names match the form's input
    // names (shg-name, rep-mobile, ...) so validation errors map straight back onto the inputs. Rules and
    // messages match the browser checks in StallApplyForm.cshtml. Checks that need the database (district,
    // category) or compare two fields are done in HomeController.

    /// <summary>Step 1: SHG details.</summary>
    public class ShgDetailsInput
    {
        /// <summary>Set after the first save; sending it back updates the same row.</summary>
        [FromForm(Name = "shg-public-id")]
        public Guid? PublicId { get; set; }

        [FromForm(Name = "shg-name")]
        [Required(ErrorMessage = "Enter the name of your SHG")]
        [StringLength(150, ErrorMessage = "SHG name must be 150 characters or fewer")]
        public string? ShgName { get; set; }

        [FromForm(Name = "shg-id")]
        [Required(ErrorMessage = "Enter your SHG ID")]
        [RegularExpression(@"^[A-Za-z0-9/\-]{6,24}$", ErrorMessage = "Enter a valid SHG ID (6 to 24 letters, numbers, / or –)")]
        public string? ShgId { get; set; }

        [FromForm(Name = "vo-name")]
        [Required(ErrorMessage = "Enter the name of your Village Organisation")]
        [StringLength(150, ErrorMessage = "Village Organisation must be 150 characters or fewer")]
        public string? VillageOrganisation { get; set; }

        [FromForm(Name = "ms-name")]
        [Required(ErrorMessage = "Enter the name of your Mandal Samakhya")]
        [StringLength(150, ErrorMessage = "Mandal Samakhya must be 150 characters or fewer")]
        public string? MandalSamakhya { get; set; }

        [FromForm(Name = "district")]
        [Required(ErrorMessage = "Select your district")]
        public int? DistrictId { get; set; }

        [FromForm(Name = "mandal")]
        [Required(ErrorMessage = "Enter your mandal")]
        [StringLength(ShgDetail.MandalMaxLength, ErrorMessage = "Mandal must be 100 characters or fewer")]
        // starts with a letter (English, Telugu or Hindi); the same pattern is on the input in the view
        [RegularExpression(@"^\s*\p{L}[\p{L}\p{M}\p{Nd} .,'\(\)\-]*\s*$", ErrorMessage = "Enter a valid mandal name")]
        public string? Mandal { get; set; }

        [FromForm(Name = "village")]
        [Required(ErrorMessage = "Enter your village or habitation")]
        [StringLength(150, ErrorMessage = "Village must be 150 characters or fewer")]
        public string? Village { get; set; }

        /// <summary>Optional; between 1990 and the current year.</summary>
        [FromForm(Name = "shg-year")]
        [YearUpToCurrent(ShgDetail.MinYearFormed)]
        public int? YearFormed { get; set; }

        [FromForm(Name = "shg-members")]
        [Required(ErrorMessage = "Enter the number of members")]
        [Range(ShgDetail.MinMembers, ShgDetail.MaxMembers, ErrorMessage = "Enter a number between 5 and 25")]
        public int? MemberCount { get; set; }

        public void ApplyTo(ShgDetail entity)
        {
            entity.ShgName = ShgName!.Trim();
            entity.ShgId = ShgId!.Trim().ToUpperInvariant();
            entity.VillageOrganisation = VillageOrganisation!.Trim();
            entity.MandalSamakhya = MandalSamakhya!.Trim();
            entity.DistrictId = DistrictId!.Value;
            entity.Mandal = string.Join(' ', Mandal!.Split(' ', StringSplitOptions.RemoveEmptyEntries)); // trim + single spaces
            entity.Village = Village!.Trim();
            entity.YearFormed = YearFormed;
            entity.MemberCount = MemberCount!.Value;
        }
    }

    /// <summary>Step 2: representative and contact details.</summary>
    public class RepresentativeInput
    {
        [FromForm(Name = "shg-public-id")]
        public Guid? PublicId { get; set; }

        [FromForm(Name = "rep-name")]
        [Required(ErrorMessage = "Enter the representative's full name")]
        [StringLength(100, ErrorMessage = "Full name must be 100 characters or fewer")]
        public string? Name { get; set; }

        [FromForm(Name = "rep-role")]
        [Required(ErrorMessage = "Select the representative's role")]
        [OneOf(nameof(ShgRepresentativeRoles), ErrorMessage = "Select the representative's role")]
        public string? Role { get; set; }

        [FromForm(Name = "rep-age")]
        [Required(ErrorMessage = "Enter the representative's age")]
        [Range(ShgDetail.MinRepAge, ShgDetail.MaxRepAge, ErrorMessage = "Age must be between 18 and 75")]
        public int? Age { get; set; }

        [FromForm(Name = "rep-guardian")]
        [StringLength(100, ErrorMessage = "Father's or husband's name must be 100 characters or fewer")]
        public string? GuardianName { get; set; }

        [FromForm(Name = "rep-mobile")]
        [Required(ErrorMessage = "Enter a mobile number")]
        [RegularExpression("^[6-9][0-9]{9}$", ErrorMessage = "Enter a 10-digit mobile number starting with 6, 7, 8 or 9")]
        public string? Mobile { get; set; }

        [FromForm(Name = "rep-mobile2")]
        [RegularExpression("^[6-9][0-9]{9}$", ErrorMessage = "Enter a 10-digit mobile number starting with 6, 7, 8 or 9")]
        public string? AltMobile { get; set; }

        [FromForm(Name = "rep-email")]
        [EmailAddress(ErrorMessage = "Enter an email address like name@example.com")]
        [StringLength(200, ErrorMessage = "Email must be 200 characters or fewer")]
        public string? Email { get; set; }

        [FromForm(Name = "rep-address")]
        [Required(ErrorMessage = "Enter the address")]
        [StringLength(500, ErrorMessage = "Address must be 500 characters or fewer")]
        public string? Address { get; set; }

        [FromForm(Name = "rep-pin")]
        [Required(ErrorMessage = "Enter the PIN code")]
        [RegularExpression("^[1-9][0-9]{5}$", ErrorMessage = "Enter a 6-digit PIN code")]
        public string? PinCode { get; set; }

        public void ApplyTo(ShgDetail entity)
        {
            entity.RepName = Name!.Trim();
            entity.RepRole = ShgRepresentativeRoles.All.First(r => string.Equals(r, Role!.Trim(), StringComparison.OrdinalIgnoreCase));
            entity.RepAge = Age;
            entity.RepGuardianName = Blank(GuardianName);
            entity.RepMobile = Mobile!.Trim();
            entity.RepAltMobile = Blank(AltMobile);
            entity.RepEmail = Blank(Email)?.ToLowerInvariant();
            entity.RepAddress = Address!.Trim();
            entity.RepPinCode = PinCode!.Trim();
            entity.RepSavedAt = DateTime.UtcNow;
        }

        private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    /// <summary>Step 3: product details (multipart, with up to 5 photos).</summary>
    public class ProductDetailsInput
    {
        [FromForm(Name = "shg-public-id")]
        public Guid? PublicId { get; set; }

        [FromForm(Name = "prod-cat")]
        [Required(ErrorMessage = "Select a product category")]
        public int? CategoryId { get; set; }

        [FromForm(Name = "prod-type")]
        [Required(ErrorMessage = "Enter the type of product")]
        [StringLength(150, ErrorMessage = "Product type must be 150 characters or fewer")]
        public string? ProductType { get; set; }

        [FromForm(Name = "prod-desc")]
        [Required(ErrorMessage = "Describe your products")]
        [MinLength(ShgDetail.MinDescriptionLength, ErrorMessage = "Write at least 20 characters about your products")]
        [StringLength(2000, ErrorMessage = "Description must be 2,000 characters or fewer")]
        public string? Description { get; set; }

        public const string PriceRangeMessage = "Enter a price between ₹1 and ₹1,00,00,000";

        [FromForm(Name = "price-min")]
        [Required(ErrorMessage = "Enter the lowest price")]
        [Range(typeof(decimal), "1", "10000000", ErrorMessage = PriceRangeMessage)]
        public decimal? PriceMin { get; set; }

        [FromForm(Name = "price-max")]
        [Required(ErrorMessage = "Enter the highest price")]
        [Range(typeof(decimal), "1", "10000000", ErrorMessage = PriceRangeMessage)]
        public decimal? PriceMax { get; set; }

        [FromForm(Name = "prod-capacity")]
        [StringLength(150, ErrorMessage = "Production capacity must be 150 characters or fewer")]
        public string? ProductionCapacity { get; set; }

        /// <summary>New photos; when none are sent, photos saved earlier for this application are kept.</summary>
        [FromForm(Name = "prod-images")]
        public List<IFormFile> Photos { get; set; } = new();

        public void ApplyTo(ShgDetail entity)
        {
            entity.ProductCategoryId = CategoryId;
            entity.ProductType = ProductType!.Trim();
            entity.ProductDescription = Description!.Trim();
            entity.PriceMin = PriceMin;
            entity.PriceMax = PriceMax;
            entity.ProductionCapacity = string.IsNullOrWhiteSpace(ProductionCapacity) ? null : ProductionCapacity.Trim();
            entity.ProductSavedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Step 4: stall requirements.</summary>
    public class StallRequirementsInput
    {
        [FromForm(Name = "shg-public-id")]
        public Guid? PublicId { get; set; }

        [FromForm(Name = "stall-duration")]
        [Required(ErrorMessage = "Select how long you want the stall")]
        [OneOf(nameof(StallOptions.Durations), ErrorMessage = "Select how long you want the stall")]
        public string? Duration { get; set; }

        /// <summary>"yyyy-MM", as sent by the month input.</summary>
        [FromForm(Name = "stall-start")]
        [Required(ErrorMessage = "Select a preferred start month")]
        public string? StartMonth { get; set; }

        [FromForm(Name = "stall-facilities")]
        public List<string> Facilities { get; set; } = new();

        [FromForm(Name = "prev-exhibit")]
        [Required(ErrorMessage = "Select yes or no")]
        [RegularExpression("^(Yes|No)$", ErrorMessage = "Select yes or no")]
        public string? PreviousExhibition { get; set; }

        [FromForm(Name = "prev-exhibit-text")]
        [StringLength(1000, ErrorMessage = "Exhibition details must be 1,000 characters or fewer")]
        public string? PreviousExhibitionDetails { get; set; }

        /// <summary>Earliest and latest start months allowed on <paramref name="today"/>: next month to 12 months ahead.</summary>
        public static (DateTime First, DateTime Last) StartMonthRange(DateTime today)
        {
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            return (thisMonth.AddMonths(1), thisMonth.AddMonths(StallOptions.MaxMonthsAhead));
        }

        /// <summary>
        /// Checks that need more than one field or today's date. Returns (field name, message) pairs, in the visitor's
        /// language when a localizer is given (month names follow the current culture).
        /// </summary>
        public IEnumerable<(string Field, string Message)> Check(DateTime today, Microsoft.Extensions.Localization.IStringLocalizer? L = null)
        {
            string T(string text, params object[] args) => L == null ? string.Format(text, args) : L[text, args].Value;

            if (!string.IsNullOrWhiteSpace(StartMonth))
            {
                var (first, last) = StartMonthRange(today);
                if (!DateTime.TryParseExact(StartMonth.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
                    yield return ("stall-start", T("Select a preferred start month"));
                else if (month < first || month > last)
                    yield return ("stall-start", T("Choose a month between {0} and {1}", first.ToString("MMMM yyyy"), last.ToString("MMMM yyyy")));
            }

            var chosen = Facilities.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToList();
            if (chosen.Any(f => !StallOptions.Facilities.Contains(f, StringComparer.OrdinalIgnoreCase)) ||
                chosen.Distinct(StringComparer.OrdinalIgnoreCase).Count() != chosen.Count)
                yield return ("stall-facilities", T("Choose facilities from the list"));

            if (PreviousExhibition == "Yes" && string.IsNullOrWhiteSpace(PreviousExhibitionDetails))
                yield return ("prev-exhibit-text", T("Tell us which exhibitions you took part in"));
        }

        public void ApplyTo(ShgDetail entity)
        {
            entity.StallDuration = StallOptions.Durations.First(d => string.Equals(d, Duration!.Trim(), StringComparison.OrdinalIgnoreCase));
            entity.StallStartMonth = DateTime.ParseExact(StartMonth!.Trim(), "yyyy-MM", CultureInfo.InvariantCulture);
            // stored in the order of the list, whatever order they were sent in
            var chosen = StallOptions.Facilities
                .Where(f => Facilities.Any(c => string.Equals(c?.Trim(), f, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            entity.StallFacilities = chosen.Count == 0 ? null : string.Join('|', chosen);
            entity.PreviousExhibition = PreviousExhibition == "Yes";
            // the details box is hidden (and ignored) when the answer is No
            entity.PreviousExhibitionDetails = entity.PreviousExhibition == true ? PreviousExhibitionDetails!.Trim() : null;
            entity.StallSavedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Step 5: optional documents (multipart) and the two required declarations.</summary>
    public class DeclarationInput
    {
        [FromForm(Name = "shg-public-id")]
        public Guid? PublicId { get; set; }

        /// <summary>SHG passbook first page or certificate; when none is sent, one saved earlier is kept.</summary>
        [FromForm(Name = "doc-shg")]
        public IFormFile? ShgDocument { get; set; }

        /// <summary>Representative's identity proof; when none is sent, one saved earlier is kept.</summary>
        [FromForm(Name = "doc-id")]
        public IFormFile? IdProof { get; set; }

        /// <summary>The checkbox posts "true" when ticked and nothing when not.</summary>
        [FromForm(Name = "decl-true")]
        public bool DeclarationTrue { get; set; }

        [FromForm(Name = "decl-consent")]
        public bool DeclarationConsent { get; set; }

        /// <summary>Declarations not confirmed. Returns (field name, message) pairs.</summary>
        public IEnumerable<(string Field, string Message)> Check()
        {
            if (!DeclarationTrue) yield return ("decl-true", "Confirm that the details are true");
            if (!DeclarationConsent) yield return ("decl-consent", "Agree to the use of these details for your application");
        }

        public void ApplyTo(ShgDetail entity)
        {
            entity.DeclarationTrue = DeclarationTrue;
            entity.DeclarationConsent = DeclarationConsent;
            entity.DeclarationSavedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Optional year between a minimum and the current year (empty is valid).</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class YearUpToCurrentAttribute : ValidationAttribute
    {
        public int Minimum { get; }

        public YearUpToCurrentAttribute(int minimum) => Minimum = minimum;

        public override bool IsValid(object? value) =>
            value is not int year || (year >= Minimum && year <= DateTime.Now.Year);

        public override string FormatErrorMessage(string name) =>
            string.Format(Template, Minimum, DateTime.Now.Year);

        private const string Template = "Enter a year between {0} and {1}";

        /// <summary>The message in the visitor's language (the website's PortalResource; the English text is the key).</summary>
        protected override ValidationResult? IsValid(object? value, ValidationContext context)
        {
            if (IsValid(value)) return ValidationResult.Success;
            var localizer = context.GetService(typeof(Microsoft.Extensions.Localization.IStringLocalizer<PortalResource>))
                as Microsoft.Extensions.Localization.IStringLocalizer;
            var message = localizer == null ? FormatErrorMessage(context.DisplayName) : localizer[Template, Minimum, DateTime.Now.Year].Value;
            return new ValidationResult(message, context.MemberName == null ? null : new[] { context.MemberName });
        }
    }

    /// <summary>Value must be one of a known list (case-insensitive); empty is left to [Required].</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class OneOfAttribute : ValidationAttribute
    {
        private readonly string[] _values;

        public OneOfAttribute(string list) => _values = list switch
        {
            nameof(ShgRepresentativeRoles) => ShgRepresentativeRoles.All,
            nameof(StallOptions.Durations) => StallOptions.Durations,
            _ => throw new ArgumentException($"Unknown list {list}", nameof(list))
        };

        public override bool IsValid(object? value) =>
            value is not string s || string.IsNullOrWhiteSpace(s) || _values.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
