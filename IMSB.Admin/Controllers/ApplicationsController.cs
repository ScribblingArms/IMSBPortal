using IMSB.Admin.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Text;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers
{
    /// <summary>
    /// Artisan applications: new (PENDING) -> Process (UNDER_REVIEW) -> Approve (registers the artisan, optionally
    /// allotting a vacant stall) or Reject. Open applications can be edited; any but approved ones deleted.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class ApplicationsController : Controller
    {
        private readonly IApplicationsInterface _applications;
        private readonly IStringLocalizer _L;

        public ApplicationsController(IApplicationsInterface applications, IStringLocalizerFactory localizerFactory)
        {
            _applications = applications;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(ApplicationsController).Assembly.GetName().Name!);
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = _L["Applications"].Value;
            ViewBag.Stats = await _applications.StatsAsync();
            return View();
        }

        /// <summary>Stats, the filtered applications (all tabs, or one status), and the form pickers.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId, [FromQuery] string? district)
        {
            var apps = await _applications.FilterAsync(q, status, categoryId, district);

            return Ok(new
            {
                stats = await _applications.StatsAsync(),
                applications = apps.Select(a => a.ToListItem()),
                categories = (await _applications.CategoriesAsync()).ToOptions(),
                districts = TelanganaDistricts.All,
                vacantStalls = (await _applications.VacantStallsAsync()).Select(s => s.ToVacantOption())
            });
        }

        /// <summary>One application with every field (Aadhaar masked).</summary>
        [HttpGet]
        public async Task<IActionResult> Get(string id)
        {
            var a = await _applications.GetDetailsAsync(id);
            return a == null ? NotFound() : Ok(a.ToDetails());
        }

        // ── Create / edit / delete ────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] ApplicationDto? dto)
        {
            if (dto == null) return BadRequest();
            var (errors, v) = await ValidateAsync(dto, null);
            if (errors.Count > 0) return BadRequest(new { errors });

            var app = new ArtisanApplication
            {
                ApplicationNo = NextCode(await _applications.ApplicationNumbersAsync(), "APP-", 4),
                Status = ApplicationStatuses.Pending,
                CreatedBy = User.Identity?.Name
            };
            Apply(app, dto, v);
            await _applications.AddAsync(app);
            return Ok(new { id = app.ApplicationNo });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string id, [FromBody] ApplicationDto? dto)
        {
            if (dto == null) return BadRequest();
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            if (!ApplicationStatuses.IsOpen(app.Status)) return ErrorResult("application", _L["ValApplicationDecided"]);
            var (errors, v) = await ValidateAsync(dto, app);
            if (errors.Count > 0) return BadRequest(new { errors });

            Apply(app, dto, v);
            Touch(app);
            await _applications.UpdateAsync(app);
            return Ok(new { id = app.ApplicationNo });
        }

        /// <summary>Approved applications stay as the artisan's registration record; the rest can be deleted.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            if (app.Status == ApplicationStatuses.Approved) return ErrorResult("application", _L["ValApplicationApprovedDelete"]);
            await _applications.RemoveAsync(app);
            return Ok(new { id });
        }

        // ── Workflow ──────────────────────────────────────────────────

        /// <summary>Takes a new application up for review by the current admin.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(string id)
        {
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            if (app.Status != ApplicationStatuses.Pending) return ErrorResult("application", _L["ValApplicationNotPending"]);
            app.Status = ApplicationStatuses.UnderReview;
            app.Reviewer = User.Identity?.Name;
            app.ReviewStartedAt = DateTime.UtcNow;
            Touch(app);
            await _applications.UpdateAsync(app);
            return Ok(new { id = app.ApplicationNo });
        }

        /// <summary>Registers the applicant as an ACTIVE artisan and, when a vacant stall is chosen, allots it.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string id, [FromBody] ApproveApplicationDto? dto)
        {
            dto ??= new ApproveApplicationDto();
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            if (!ApplicationStatuses.IsOpen(app.Status)) return ErrorResult("application", _L["ValApplicationDecided"]);

            var errors = ModelStateErrors(ModelState);
            // someone with the same mobile or Aadhaar may have been registered since the application came in
            if (await _applications.ArtisanExistsAsync(app.Mobile, app.AadhaarHash))
                errors["application"] = _L["ValApplicantAlreadyArtisan"];
            if (!await _applications.CategoryExistsAsync(app.CategoryId))
                errors["application"] = _L["ValCraftCategory"];

            Stall? stall = null;
            if (!string.IsNullOrWhiteSpace(dto.StallCode))
            {
                stall = await _applications.FindStallAsync(dto.StallCode.Trim().ToUpperInvariant());
                if (stall == null) errors["stall_code"] = _L["ValSelectStall"];
                else if (stall.Status != StallStatuses.Vacant || stall.ArtisanId != null) errors["stall_code"] = _L["ValStallNotVacant"];
            }
            if (errors.Count > 0) return BadRequest(new { errors });

            var user = User.Identity?.Name;
            var artisan = app.ToArtisan(NextCode(await _applications.ArtisanCodesAsync(), "ART-", 4),
                string.Format(_L["RegisteredFromApplication"], app.ApplicationNo), user);

            if (stall != null)
            {
                stall.ArtisanId = artisan.Id;
                stall.Status = StallStatuses.Occupied;
                stall.AllottedDate = DateTime.Today;
                stall.CategoryId = artisan.CategoryId;
                stall.UpdatedAt = DateTime.UtcNow;
                stall.UpdatedBy = user;
            }

            app.Status = ApplicationStatuses.Approved;
            app.Reviewer ??= user;
            app.ArtisanId = artisan.Id;
            app.ArtisanCode = artisan.ArtisanCode;
            app.StallCode = stall?.StallCode;
            Decide(app, dto.Remarks);
            await _applications.ApproveAsync(app, artisan);
            return Ok(new { id = app.ApplicationNo, artisanCode = artisan.ArtisanCode, stallCode = stall?.StallCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id, [FromBody] DecisionDto? dto)
        {
            var app = await _applications.FindAsync(id);
            if (app == null) return NotFound();
            if (!ApplicationStatuses.IsOpen(app.Status)) return ErrorResult("application", _L["ValApplicationDecided"]);
            var errors = ModelStateErrors(ModelState);
            if (string.IsNullOrWhiteSpace(dto?.Remarks)) errors.TryAdd("remarks", _L["ValRejectRemarks"]);
            if (errors.Count > 0) return BadRequest(new { errors });

            app.Status = ApplicationStatuses.Rejected;
            app.Reviewer ??= User.Identity?.Name;
            Decide(app, dto!.Remarks);
            await _applications.UpdateAsync(app);
            return Ok(new { id = app.ApplicationNo });
        }

        private void Decide(ArtisanApplication app, string? remarks)
        {
            app.DecidedAt = DateTime.UtcNow;
            app.DecidedBy = User.Identity?.Name;
            app.DecisionRemarks = Clean(remarks);
            Touch(app);
        }

        private void Touch(ArtisanApplication app)
        {
            app.UpdatedAt = DateTime.UtcNow;
            app.UpdatedBy = User.Identity?.Name;
        }

        // ── Validation ────────────────────────────────────────────────

        internal record Validated(DateTime DateOfBirth, string Mobile, string? Aadhaar, Guid CategoryId, DateTime SubmittedDate, string District, string Gender);

        /// <summary>
        /// Same rules as an artisan registration. The mobile and Aadhaar must not belong to a registered artisan or to
        /// another open application (a rejected applicant may apply again).
        /// </summary>
        private async Task<(Dictionary<string, string> Errors, Validated V)> ValidateAsync(ApplicationDto dto, ArtisanApplication? existing)
        {
            var errors = ModelStateErrors(ModelState);
            var selfId = existing?.Id ?? Guid.Empty;

            DateTime dob = default;
            if (!errors.ContainsKey("date_of_birth"))
            {
                if (!TryParseDate(dto.DateOfBirth, out dob)) errors["date_of_birth"] = _L["ValDobInvalid"];
                else if (dob > DateTime.Today) errors["date_of_birth"] = _L["ValDobFuture"];
                else if (dob > DateTime.Today.AddYears(-18)) errors["date_of_birth"] = _L["ValArtisanMinAge"];
                else if (dob < DateTime.Today.AddYears(-100)) errors["date_of_birth"] = _L["ValDobInvalid"];
            }

            var gender = ArtisansController.Genders.FirstOrDefault(g => g.Equals(dto.Gender?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (gender == null) errors.TryAdd("gender", _L["ValGender"]);

            var mobile = MobileNumbers.Normalize(dto.Mobile);
            if (!errors.ContainsKey("mobile"))
            {
                if (!MobileNumbers.IsValid(mobile)) errors["mobile"] = _L["ValArtisanMobile"];
                else if (await _applications.ArtisanMobileExistsAsync(mobile)) errors["mobile"] = _L["ValMobileExists"];
                else if (await _applications.OpenApplicationMobileExistsAsync(mobile, selfId)) errors["mobile"] = _L["ValMobileApplicationExists"];
            }

            string? aadhaar = null;
            var aadhaarGiven = !string.IsNullOrWhiteSpace(dto.Aadhaar);
            if (existing == null && !aadhaarGiven) errors["aadhaar"] = _L["ValAadhaar"];
            else if (aadhaarGiven)
            {
                if (!Aadhaar.IsValid(dto.Aadhaar)) errors["aadhaar"] = _L["ValAadhaar"];
                else
                {
                    var hash = Aadhaar.Hash(dto.Aadhaar!);
                    if (await _applications.ArtisanAadhaarExistsAsync(hash)) errors["aadhaar"] = _L["ValAadhaarExists"];
                    else if (await _applications.OpenApplicationAadhaarExistsAsync(hash, selfId)) errors["aadhaar"] = _L["ValAadhaarApplicationExists"];
                    else aadhaar = dto.Aadhaar;
                }
            }

            var district = TelanganaDistricts.All.FirstOrDefault(d => d.Equals(dto.District?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (district == null) errors.TryAdd("district", _L["ValDistrict"]);

            Guid categoryId = default;
            if (!errors.ContainsKey("category_id") && (!Guid.TryParse(dto.CategoryId, out categoryId) || !await _applications.CategoryExistsAsync(categoryId)))
                errors["category_id"] = _L["ValCraftCategory"];

            var submitted = existing?.SubmittedDate ?? DateTime.Today;
            if (!string.IsNullOrWhiteSpace(dto.SubmittedDate))
            {
                if (!TryParseDate(dto.SubmittedDate, out submitted)) errors["submitted_date"] = _L["ValDateInvalid"];
                else if (submitted > DateTime.Today) errors["submitted_date"] = _L["ValDateFuture"];
            }

            return (errors, new Validated(dob, mobile, aadhaar, categoryId, submitted, district ?? "", gender ?? ""));
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
            a.Remarks = Clean(dto.Remarks);
        }

        // ── Export ────────────────────────────────────────────────────

        /// <summary>CSV of the filtered applications (a whole tab, or everything when status is empty).</summary>
        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId, [FromQuery] string? district)
        {
            var apps = await _applications.ExportAsync(q, status, categoryId, district);

            var rows = new List<string?[]>
            {
                new string?[] { _L["AppID"], _L["Applicant"], _L["Gender"], _L["DateOfBirth"], _L["Mobile"], _L["Email"], _L["AadhaarNumber"],
                    _L["District"], _L["CraftCategory"], _L["YearsOfExperience"], _L["Status"], _L["Submitted"], _L["DaysWaiting"], _L["Reviewer"],
                    _L["DecidedOn"], _L["DecidedBy"], _L["DecisionRemarks"], _L["ArtisanID"], _L["StallAssigned"] }
            };
            foreach (var a in apps)
            {
                var waiting = a.DaysWaiting();
                rows.Add(new string?[] { a.ApplicationNo, a.FullName, a.Gender, Date(a.DateOfBirth), a.Mobile, a.Email, a.MaskedAadhaar,
                    a.District, a.Category?.Name, Int(a.YearsOfExperience), StatusLabels.Application(_L, a.Status), Date(a.SubmittedDate),
                    waiting == null ? null : Int(waiting.Value),
                    a.Reviewer, LocalDate(a.DecidedAt), a.DecidedBy, a.DecisionRemarks, a.ArtisanCode, a.StallCode });
            }
            var st = status?.Trim().ToUpperInvariant();
            var suffix = !string.IsNullOrEmpty(st) && ApplicationStatuses.All.Contains(st) ? "_" + st.ToLowerInvariant() : "";
            return ExportCsv($"applications{suffix}_{DateTime.Now:yyyyMMddHHmmss}.csv", rows);
        }
    }
}
