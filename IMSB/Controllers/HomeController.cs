using System.Diagnostics;
using IMSB.Interfaces;
using IMSB.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace IMSB.Controllers
{
    /// <summary>
    /// Website pages and the stall application form. Only the posted fields are checked here; saving, uploads, logging
    /// and the submission are done by the IMSB.Backend behind <see cref="IShgDetailsInterface"/>.
    /// </summary>
    public class HomeController : Controller
    {
        private const long ProductRequestLimit = 12 * 1024 * 1024;
        private const long DocumentsRequestLimit = 6 * 1024 * 1024;
        private const string PriceFormatMessage = "Enter the price in rupees and paise, like 250 or 250.50";

        private readonly IShgDetailsInterface _shgDetails;
        private readonly IStringLocalizer<SharedResource> L;

        public HomeController(IShgDetailsInterface shgDetails, IStringLocalizer<SharedResource> localizer)
        {
            _shgDetails = shgDetails;
            L = localizer;
        }

        public IActionResult Index() => View();

        public IActionResult StallApply() => View();

        public IActionResult Privacy() => View();

        /// <summary>The English / తెలుగు / हिन्दी buttons: remembers the choice in the culture cookie for a year, then returns to the page.</summary>
        [HttpGet]
        public IActionResult SetLanguage(string? culture, string? returnUrl)
        {
            var chosen = culture?.Trim().ToLowerInvariant() switch { "te" => "te-IN", "hi" => "hi-IN", _ => "en-IN" };
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(chosen)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax
                });
            // only back to a page of this site
            return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }

        public async Task<IActionResult> StallApplyForm()
        {
            var lookups = await _shgDetails.FormLookupsAsync();
            return View(new StallApplyFormViewModel { Districts = lookups.Districts, Categories = lookups.Categories });
        }

        /// <summary>
        /// Step 1 ("Save and continue"): the first save creates the row and returns its public id; posting that id back
        /// updates the same row. Returns 200 { publicId } or 400 { errors: { "field-name": "message" } } / { message }.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveShgDetails(ShgDetailsInput input)
        {
            if (input.DistrictId is int districtId && !await _shgDetails.DistrictExistsAsync(districtId))
                ModelState.AddModelError("district", L["Select your district"]);
            if (!ModelState.IsValid) return ValidationErrors();

            return FormResult(await _shgDetails.SaveShgDetailsAsync(input.PublicId, input.ApplyTo));
        }

        /// <summary>Step 2: representative and contact details.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRepresentative(RepresentativeInput input)
        {
            if (!string.IsNullOrWhiteSpace(input.AltMobile) && input.AltMobile.Trim() == input.Mobile?.Trim())
                ModelState.AddModelError("rep-mobile2", L["Alternate mobile number must be different from the mobile number"]);
            if (!ModelState.IsValid) return ValidationErrors();

            return FormResult(await _shgDetails.SaveStepAsync(input.PublicId, "Representative", input.ApplyTo));
        }

        /// <summary>Step 3: product details and photos (1 to 5 JPG/PNG, 2 MB each).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(ProductRequestLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = ProductRequestLimit)]
        public async Task<IActionResult> SaveProductDetails(ProductDetailsInput input)
        {
            if (input.CategoryId is int categoryId && !await _shgDetails.CategoryExistsAsync(categoryId))
                ModelState.AddModelError("prod-cat", L["Select a product category"]);
            // the columns keep 2 decimals; more would be rounded silently
            if (input.PriceMin is decimal pmin && decimal.Round(pmin, 2) != pmin)
                ModelState.AddModelError("price-min", L[PriceFormatMessage]);
            if (input.PriceMax is decimal pmax && decimal.Round(pmax, 2) != pmax)
                ModelState.AddModelError("price-max", L[PriceFormatMessage]);
            if (input.PriceMin is decimal min && input.PriceMax is decimal max && max < min)
                ModelState.AddModelError("price-max", L["Highest price must be the same as or more than the lowest price"]);

            var photos = input.Photos.Select(Uploaded).OfType<UploadedFile>().ToList();
            if (photos.Count > Dto.Models.ShgDetail.MaxProductPhotos)
                ModelState.AddModelError("prod-images", L["Upload no more than 5 files"]);
            else if (photos.Count == 0 && await _shgDetails.SavedPhotoCountAsync(input.PublicId) == 0)
                ModelState.AddModelError("prod-images", L["Upload at least one product photo"]);
            else
                await CheckUploadsAsync("prod-images", photos, allowPdf: false);
            if (!ModelState.IsValid) return ValidationErrors();

            var result = await _shgDetails.SaveProductDetailsAsync(input.PublicId, photos, input.ApplyTo);
            return result.Succeeded ? Ok(new { publicId = result.PublicId, photos = result.Files }) : FormResult(result);
        }

        /// <summary>Step 4: stall period, start month, facilities and previous exhibitions.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStallRequirements(StallRequirementsInput input)
        {
            foreach (var (field, message) in input.Check(_shgDetails.IndiaNow(), L))
                if (!ModelState.TryGetValue(field, out var entry) || entry.Errors.Count == 0)
                    ModelState.AddModelError(field, message);
            if (!ModelState.IsValid) return ValidationErrors();

            return FormResult(await _shgDetails.SaveStepAsync(input.PublicId, "Stall requirements", input.ApplyTo));
        }

        /// <summary>Step 5: optional documents (JPG, PNG or PDF, 2 MB each) and the two declarations.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(DocumentsRequestLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = DocumentsRequestLimit)]
        public async Task<IActionResult> SaveDeclaration(DeclarationInput input)
        {
            foreach (var (field, message) in input.Check())
                ModelState.AddModelError(field, L[message]);

            var shgDoc = Uploaded(input.ShgDocument);
            var idProof = Uploaded(input.IdProof);
            if (shgDoc is not null) await CheckUploadsAsync("doc-shg", new[] { shgDoc }, allowPdf: true);
            if (idProof is not null) await CheckUploadsAsync("doc-id", new[] { idProof }, allowPdf: true);
            if (!ModelState.IsValid) return ValidationErrors();

            return FormResult(await _shgDetails.SaveDeclarationAsync(input.PublicId, shgDoc, idProof, input.ApplyTo));
        }

        /// <summary>
        /// Final "Submit application": steps 1-5 must all be saved; gives the application its reference number and locks it.
        /// Returns 200 { reference } or 400 { message, step } (the first step still to be saved).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitApplication([FromForm(Name = "shg-public-id")] Guid? publicId)
        {
            var result = await _shgDetails.SubmitAsync(publicId);
            return result.Status switch
            {
                ShgFormStatus.Saved => Ok(new { reference = result.Reference }),
                ShgFormStatus.Incomplete => BadRequest(new
                {
                    message = L["Step {0} has not been saved yet. Go to step {0} and select Save and continue.", result.MissingStep!].Value,
                    step = result.MissingStep
                }),
                ShgFormStatus.Failed => StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = L["We could not submit your application. Please try again in a few minutes."].Value }),
                _ => FormResult(result)
            };
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        // ───────────── helpers ─────────────

        /// <summary>200 { publicId }, or the visitor-facing message for a step that could not be saved.</summary>
        private IActionResult FormResult(ShgFormResult result) => result.Status switch
        {
            ShgFormStatus.Saved => Ok(new { publicId = result.PublicId }),
            ShgFormStatus.NotFound => BadRequest(new
            {
                message = L["Your session for this application has expired. Go back to step 1 and select Save and continue."].Value
            }),
            ShgFormStatus.AlreadySubmitted => BadRequest(new
            {
                message = L["This application has already been submitted and can no longer be changed."].Value
            }),
            ShgFormStatus.Rejected => BadRequest(new
            {
                message = L["This application has been rejected by IMSB and can no longer be changed or submitted."].Value
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError,
                new { message = L["We could not save your details. Please try again in a few minutes."].Value })
        };

        /// <summary>Adds the first upload problem of <paramref name="files"/> to the field, in the visitor's language.</summary>
        private async Task CheckUploadsAsync(string field, IEnumerable<UploadedFile> files, bool allowPdf)
        {
            foreach (var file in files)
            {
                if (await _shgDetails.CheckUploadAsync(file, allowPdf) is not string error) continue;
                ModelState.AddModelError(field, L[error, Path.GetFileName(file.FileName)]);
                return;
            }
        }

        /// <summary>A file part with no name and no bytes is an empty file input, not an upload.</summary>
        private static UploadedFile? Uploaded(IFormFile? file) =>
            file is null || (file.Length == 0 && string.IsNullOrEmpty(file.FileName))
                ? null
                : new UploadedFile(file.FileName, file.Length, file.OpenReadStream);

        /// <summary>400 { errors: { "field-name": "first message" } } from ModelState.</summary>
        private BadRequestObjectResult ValidationErrors() =>
            BadRequest(new
            {
                errors = ModelState
                    .Where(kv => kv.Value?.Errors.Count > 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors[0].ErrorMessage)
            });
    }
}
