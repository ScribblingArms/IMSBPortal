using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>
    /// Artisan applications: new (PENDING) -> process (UNDER_REVIEW) -> approve (registers the artisan, optionally
    /// allotting a vacant stall) or reject. Open applications can be edited; any application can be deleted.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class ApplicationsController : ApiControllerBase
    {
        private readonly IApplicationsInterface _applications;

        public ApplicationsController(IApplicationsInterface applications) => _applications = applications;

        /// <summary>Stats, the filtered applications (all, or one status) and the form pickers.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId, [FromQuery] string? district) =>
            Ok(new
            {
                stats = await _applications.StatsAsync(),
                applications = (await _applications.FilterAsync(q, status, categoryId, district)).Select(a => a.ToListItem()),
                categories = (await _applications.CategoriesAsync()).ToOptions(),
                districts = TelanganaDistricts.All,
                vacantStalls = (await _applications.VacantStallsAsync()).Select(s => s.ToVacantOption())
            });

        /// <summary>One application with every field (Aadhaar masked).</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var a = await _applications.GetDetailsAsync(id);
            return a == null ? NotFound() : Ok(a.ToDetails());
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ApplicationDto dto)
        {
            var (errors, v) = await ValidateAsync(dto, null);
            if (errors.Count > 0) return Invalid(errors);

            var app = new ArtisanApplication
            {
                ApplicationNo = NextCode(await _applications.ApplicationNumbersAsync(), "APP-", 4),
                Status = ApplicationStatuses.Pending,
                CreatedBy = UserName
            };
            Apply(app, dto, v);
            await _applications.AddAsync(app);
            return Ok(new { id = app.ApplicationNo });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ApplicationDto dto)
        {
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            if (!ApplicationStatuses.IsOpen(app.Status)) return Invalid("application", "ValApplicationDecided");
            var (errors, v) = await ValidateAsync(dto, app);
            if (errors.Count > 0) return Invalid(errors);

            Apply(app, dto, v);
            Touch(app);
            await _applications.UpdateAsync(app);
            return Ok(new { id = app.ApplicationNo });
        }

        /// <summary>Removes the application only: an artisan registered from it, and their stall, are not touched.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            await _applications.RemoveAsync(app);
            return Ok(new { id });
        }

        /// <summary>Takes a new application up for review by the current admin.</summary>
        [HttpPost("{id}/process")]
        public async Task<IActionResult> Process(string id)
        {
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            if (app.Status != ApplicationStatuses.Pending) return Invalid("application", "ValApplicationNotPending");
            app.Status = ApplicationStatuses.UnderReview;
            app.Reviewer = UserName;
            app.ReviewStartedAt = DateTime.UtcNow;
            Touch(app);
            await _applications.UpdateAsync(app);
            return Ok(new { id = app.ApplicationNo });
        }

        /// <summary>Registers the applicant as an ACTIVE artisan and, when a vacant stall is chosen, allots it.</summary>
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> Approve(string id, [FromBody] ApproveApplicationDto? dto)
        {
            dto ??= new ApproveApplicationDto();
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            if (!ApplicationStatuses.IsOpen(app.Status)) return Invalid("application", "ValApplicationDecided");

            var errors = new Dictionary<string, string>();
            // someone with the same mobile or Aadhaar may have been registered since the application came in
            if (await _applications.ArtisanExistsAsync(app.Mobile, app.AadhaarHash)) errors["application"] = L["ValApplicantAlreadyArtisan"];
            if (!await _applications.CategoryExistsAsync(app.CategoryId)) errors["application"] = L["ValCraftCategory"];

            Stall? stall = null;
            if (!string.IsNullOrWhiteSpace(dto.StallCode))
            {
                stall = await _applications.FindStallAsync(dto.StallCode.Trim().ToUpperInvariant());
                if (stall == null) errors["stall_code"] = L["ValSelectStall"];
                else if (stall.Status != StallStatuses.Vacant || stall.ArtisanId != null) errors["stall_code"] = L["ValStallNotVacant"];
            }
            if (errors.Count > 0) return Invalid(errors);

            var artisan = app.ToArtisan(NextCode(await _applications.ArtisanCodesAsync(), "ART-", 4),
                L["RegisteredFromApplication", app.ApplicationNo], UserName);
            if (stall != null)
            {
                stall.ArtisanId = artisan.Id;
                stall.Status = StallStatuses.Occupied;
                stall.AllottedDate = DateTime.Today;
                stall.CategoryId = artisan.CategoryId;
                stall.UpdatedAt = DateTime.UtcNow;
                stall.UpdatedBy = UserName;
            }

            app.Status = ApplicationStatuses.Approved;
            app.Reviewer ??= UserName;
            app.ArtisanId = artisan.Id;
            app.ArtisanCode = artisan.ArtisanCode;
            app.StallCode = stall?.StallCode;
            Decide(app, dto.Remarks);
            await _applications.ApproveAsync(app, artisan);
            return Ok(new { id = app.ApplicationNo, artisanCode = artisan.ArtisanCode, stallCode = stall?.StallCode });
        }

        /// <summary>Rejects an open application; remarks are required.</summary>
        [HttpPost("{id}/reject")]
        public async Task<IActionResult> Reject(string id, [FromBody] DecisionDto? dto)
        {
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            if (!ApplicationStatuses.IsOpen(app.Status)) return Invalid("application", "ValApplicationDecided");
            if (string.IsNullOrWhiteSpace(dto?.Remarks)) return Invalid("remarks", "ValRejectRemarks");

            app.Status = ApplicationStatuses.Rejected;
            app.Reviewer ??= UserName;
            Decide(app, dto.Remarks);
            await _applications.UpdateAsync(app);
            return Ok(new { id = app.ApplicationNo });
        }

        private void Decide(ArtisanApplication app, string? remarks)
        {
            app.DecidedAt = DateTime.UtcNow;
            app.DecidedBy = UserName;
            app.DecisionRemarks = Clean(remarks);
            Touch(app);
        }

        private void Touch(ArtisanApplication app)
        {
            app.UpdatedAt = DateTime.UtcNow;
            app.UpdatedBy = UserName;
        }

        private record Validated(DateTime DateOfBirth, string Mobile, string? Aadhaar, Guid CategoryId, DateTime SubmittedDate, string District, string Gender,
            DateTime? FromDate, DateTime? ToDate);

        /// <summary>
        /// Same rules as an artisan registration. The mobile and Aadhaar must not belong to a registered artisan or to
        /// another open application (a rejected applicant may apply again).
        /// </summary>
        private async Task<(Dictionary<string, string> Errors, Validated V)> ValidateAsync(ApplicationDto dto, ArtisanApplication? existing)
        {
            var errors = new Dictionary<string, string>();
            var selfId = existing?.Id ?? Guid.Empty;

            if (!TryParseDate(dto.DateOfBirth, out var dob)) errors["date_of_birth"] = L["ValDobInvalid"];
            else if (dob > DateTime.Today) errors["date_of_birth"] = L["ValDobFuture"];
            else if (dob > DateTime.Today.AddYears(-18)) errors["date_of_birth"] = L["ValArtisanMinAge"];
            else if (dob < DateTime.Today.AddYears(-100)) errors["date_of_birth"] = L["ValDobInvalid"];

            var gender = ArtisansController.Genders.FirstOrDefault(g => g.Equals(dto.Gender?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (gender == null) errors["gender"] = L["ValGender"];

            var mobile = MobileNumbers.Normalize(dto.Mobile);
            if (!MobileNumbers.IsValid(mobile)) errors["mobile"] = L["ValArtisanMobile"];
            else if (await _applications.ArtisanMobileExistsAsync(mobile)) errors["mobile"] = L["ValMobileExists"];
            else if (await _applications.OpenApplicationMobileExistsAsync(mobile, selfId)) errors["mobile"] = L["ValMobileApplicationExists"];

            string? aadhaar = null;
            var aadhaarGiven = !string.IsNullOrWhiteSpace(dto.Aadhaar);
            if (existing == null && !aadhaarGiven) errors["aadhaar"] = L["ValAadhaar"];
            else if (aadhaarGiven)
            {
                var hash = Aadhaar.IsValid(dto.Aadhaar) ? Aadhaar.Hash(dto.Aadhaar!) : null;
                if (hash == null) errors["aadhaar"] = L["ValAadhaar"];
                else if (await _applications.ArtisanAadhaarExistsAsync(hash)) errors["aadhaar"] = L["ValAadhaarExists"];
                else if (await _applications.OpenApplicationAadhaarExistsAsync(hash, selfId)) errors["aadhaar"] = L["ValAadhaarApplicationExists"];
                else aadhaar = dto.Aadhaar;
            }

            var district = TelanganaDistricts.All.FirstOrDefault(d => d.Equals(dto.District?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (district == null) errors["district"] = L["ValDistrict"];

            if (!Guid.TryParse(dto.CategoryId, out var categoryId) || !await _applications.CategoryExistsAsync(categoryId))
                errors["category_id"] = L["ValCraftCategory"];

            var submitted = existing?.SubmittedDate ?? DateTime.Today;
            if (!string.IsNullOrWhiteSpace(dto.SubmittedDate))
            {
                if (!TryParseDate(dto.SubmittedDate, out submitted)) errors["submitted_date"] = L["ValDateInvalid"];
                else if (submitted > DateTime.Today) errors["submitted_date"] = L["ValDateFuture"];
            }

            if (ParsePeriod(dto.FromDate, dto.ToDate, out var from, out var to) is { } period) errors[period.Field] = L[period.Key];

            return (errors, new Validated(dob, mobile, aadhaar, categoryId, submitted, district ?? "", gender ?? "", from, to));
        }

        private static void Apply(ArtisanApplication a, ApplicationDto dto, Validated v)
        {
            a.FullName = Clean(dto.FullName)!;
            a.DateOfBirth = v.DateOfBirth;
            a.Gender = v.Gender;
            a.Mobile = v.Mobile;
            a.Email = Clean(dto.Email)?.ToLowerInvariant();
            a.Address = Clean(dto.Address)!;
            if (v.Aadhaar != null)
            {
                a.AadhaarHash = Aadhaar.Hash(v.Aadhaar);
                a.AadhaarLast4 = Aadhaar.Last4(v.Aadhaar);
            }
            a.District = v.District;
            a.CategoryId = v.CategoryId;
            a.YearsOfExperience = dto.YearsOfExperience;
            a.Skills = Clean(dto.Skills);
            a.SubmittedDate = v.SubmittedDate;
            a.FromDate = v.FromDate;
            a.ToDate = v.ToDate;
            a.Remarks = Clean(dto.Remarks);
        }
    }
}
