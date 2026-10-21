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
    [Authorize(Roles = "Admin")]
    public class ArtisansController : Controller
    {
        public static readonly string[] Genders = { "Female", "Male", "Other" };

        private readonly IArtisansInterface _artisans;
        private readonly IStringLocalizer _L;

        public ArtisansController(IArtisansInterface artisans, IStringLocalizerFactory localizerFactory)
        {
            _artisans = artisans;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(ArtisansController).Assembly.GetName().Name!);
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = _L["Artisans"].Value;
            ViewBag.Stats = await ComputeStatsAsync();
            return View();
        }

        internal Task<ArtisanStats> ComputeStatsAsync() => _artisans.StatsAsync();

        /// <summary>Stats, status chart, the filtered directory and the form pickers.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId,
            [FromQuery] string? district, [FromQuery] string? stall)
        {
            var stats = await ComputeStatsAsync();
            var stallByArtisan = await _artisans.StallsByArtisanAsync();
            var artisans = await _artisans.FilterAsync(q, status, categoryId, district, stall);

            return Ok(new
            {
                stats,
                chart = new
                {
                    statuses = new[] { ArtisanStatuses.Active, ArtisanStatuses.OnLeave, ArtisanStatuses.RenewalDue, ArtisanStatuses.Inactive },
                    values = new[] { stats.Active, stats.OnLeave, stats.RenewalPending, stats.Inactive }
                },
                artisans = artisans.Select(a => ListDto(a, stallByArtisan)),
                categories = (await _artisans.CategoriesAsync()).ToOptions(),
                districts = TelanganaDistricts.All
            });
        }

        private static object ListDto(Artisan a, Dictionary<Guid, string> stalls) => new
        {
            code = a.ArtisanCode,
            fullName = a.FullName,
            mobile = a.Mobile,
            district = a.District,
            categoryId = a.CategoryId.ToString(),
            categoryName = a.Category?.Name,
            status = a.Status,
            stallCode = stalls.GetValueOrDefault(a.Id),
            joinedDate = Date(a.JoinedDate)
        };

        /// <summary>Profile: details (Aadhaar masked), stall, and this month's sales at that stall.</summary>
        [HttpGet]
        public async Task<IActionResult> Get(string id)
        {
            var a = await _artisans.GetDetailsAsync(id);
            if (a == null) return NotFound();
            var stall = await _artisans.StallOfAsync(a.Id);

            decimal monthSales = 0;
            int products = 0;
            if (stall != null) (monthSales, products) = await _artisans.StallFiguresAsync(stall.StallCode);

            return Ok(new
            {
                code = a.ArtisanCode,
                fullName = a.FullName,
                dateOfBirth = Date(a.DateOfBirth),
                gender = a.Gender,
                mobile = a.Mobile,
                email = a.Email,
                address = a.Address,
                aadhaarMasked = a.MaskedAadhaar,
                district = a.District,
                categoryId = a.CategoryId.ToString(),
                categoryName = a.Category?.Name,
                yearsOfExperience = a.YearsOfExperience,
                skills = a.Skills,
                status = a.Status,
                joinedDate = Date(a.JoinedDate),
                remarks = a.Remarks,
                stallCode = stall?.StallCode,
                allottedDate = stall?.AllottedDate == null ? null : Date(stall.AllottedDate),
                monthSales,
                productsListed = products
            });
        }

        // ── Create / edit / delete ────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] ArtisanDto? dto)
        {
            if (dto == null) return BadRequest();
            var (errors, v) = await ValidateAsync(dto, null);
            if (errors.Count > 0) return BadRequest(new { errors });

            var artisan = new Artisan
            {
                ArtisanCode = NextCode(await _artisans.ArtisanCodesAsync(), "ART-", 4),
                CreatedBy = User.Identity?.Name
            };
            Apply(artisan, dto, v);
            _artisans.Add(artisan);
            await _artisans.SaveChangesAsync();
            return Ok(new { code = artisan.ArtisanCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string id, [FromBody] ArtisanDto? dto)
        {
            if (dto == null) return BadRequest();
            var artisan = await _artisans.FindAsync(id);
            if (artisan == null) return NotFound();
            var (errors, v) = await ValidateAsync(dto, artisan);
            if (errors.Count > 0) return BadRequest(new { errors });

            Apply(artisan, dto, v);
            artisan.UpdatedAt = DateTime.UtcNow;
            artisan.UpdatedBy = User.Identity?.Name;
            await _artisans.SaveChangesAsync();
            return Ok(new { code = artisan.ArtisanCode });
        }

        /// <summary>An artisan holding a stall cannot be deleted; her open reallocation requests are closed and history keeps the name.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var artisan = await _artisans.FindAsync(id);
            if (artisan == null) return NotFound();
            var stall = await _artisans.StallOfAsync(artisan.Id);
            if (stall != null)
                return BadRequest(new { errors = new Dictionary<string, string> { ["artisan"] = string.Format(_L["ValArtisanDeleteHasStall"], stall.StallCode) } });

            await _artisans.DeleteAsync(artisan, User.Identity?.Name, _L["ClosedArtisanDeleted"]);
            return Ok(new { code = id });
        }

        internal record Validated(DateTime DateOfBirth, string Mobile, string? Aadhaar, Guid CategoryId, string Status, DateTime JoinedDate, string District, string Gender);

        private async Task<(Dictionary<string, string> Errors, Validated V)> ValidateAsync(ArtisanDto dto, Artisan? existing)
        {
            var errors = ModelStateErrors(ModelState);

            DateTime dob = default;
            if (!errors.ContainsKey("date_of_birth"))
            {
                if (!TryParseDate(dto.DateOfBirth, out dob)) errors["date_of_birth"] = _L["ValDobInvalid"];
                else if (dob > DateTime.Today) errors["date_of_birth"] = _L["ValDobFuture"];
                else if (dob > DateTime.Today.AddYears(-18)) errors["date_of_birth"] = _L["ValArtisanMinAge"];
                else if (dob < DateTime.Today.AddYears(-100)) errors["date_of_birth"] = _L["ValDobInvalid"];
            }

            var gender = Genders.FirstOrDefault(g => g.Equals(dto.Gender?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (gender == null) errors.TryAdd("gender", _L["ValGender"]);

            // 10-digit Indian mobile; "+91 98765 43210" and "098765..." are accepted and stored as 9876543210
            var mobile = MobileNumbers.Normalize(dto.Mobile);
            if (!errors.ContainsKey("mobile"))
            {
                if (!MobileNumbers.IsValid(mobile)) errors["mobile"] = _L["ValArtisanMobile"];
                else if (await _artisans.MobileExistsAsync(mobile, existing?.Id ?? Guid.Empty))
                    errors["mobile"] = _L["ValMobileExists"];
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
                    if (await _artisans.AadhaarExistsAsync(hash, existing?.Id ?? Guid.Empty))
                        errors["aadhaar"] = _L["ValAadhaarExists"];
                    else aadhaar = dto.Aadhaar;
                }
            }

            var district = TelanganaDistricts.All.FirstOrDefault(d => d.Equals(dto.District?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (district == null) errors.TryAdd("district", _L["ValDistrict"]);

            Guid categoryId = default;
            if (!errors.ContainsKey("category_id") && (!Guid.TryParse(dto.CategoryId, out categoryId) || !await _artisans.CategoryExistsAsync(categoryId)))
                errors["category_id"] = _L["ValCraftCategory"];

            var status = string.IsNullOrWhiteSpace(dto.Status) ? existing?.Status ?? ArtisanStatuses.Active : dto.Status.Trim().ToUpperInvariant();
            if (!ArtisanStatuses.All.Contains(status)) errors["status"] = _L["ValStatus"];
            else if (existing != null && !ArtisanStatuses.CanHoldStall(status) && await _artisans.HasStallAsync(existing.Id))
                errors["status"] = _L["ValInactiveWithStall"];

            var joined = existing?.JoinedDate ?? DateTime.Today;
            if (!string.IsNullOrWhiteSpace(dto.JoinedDate))
            {
                if (!TryParseDate(dto.JoinedDate, out joined)) errors["joined_date"] = _L["ValJoinInvalid"];
                else if (joined > DateTime.Today) errors["joined_date"] = _L["ValJoinFuture"];
                else if (!errors.ContainsKey("date_of_birth") && joined < dob) errors["joined_date"] = _L["ValJoinInvalid"];
            }

            return (errors, new Validated(dob, mobile, aadhaar, categoryId, status, joined, district ?? "", gender ?? ""));
        }

        private static void Apply(Artisan a, ArtisanDto dto, Validated v)
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
            a.Status = v.Status;
            a.JoinedDate = v.JoinedDate;
            a.Remarks = Clean(dto.Remarks);
        }

        // ── Export ────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId,
            [FromQuery] string? district, [FromQuery] string? stall)
        {
            var stalls = await _artisans.StallsByArtisanAsync();
            var artisans = await _artisans.FilterAsync(q, status, categoryId, district, stall);

            var sb = new StringBuilder();
            sb.AppendLine(Row(_L["ArtisanID"], _L["FullName"], _L["Gender"], _L["DateOfBirth"], _L["Mobile"], _L["Email"], _L["AadhaarNumber"],
                _L["District"], _L["CraftCategory"], _L["YearsOfExperience"], _L["Stall"], _L["Status"], _L["Joined"]));
            foreach (var a in artisans)
            {
                sb.AppendLine(Row(a.ArtisanCode, a.FullName, a.Gender, Date(a.DateOfBirth), a.Mobile, a.Email, a.MaskedAadhaar,
                    a.District, a.Category?.Name, Int(a.YearsOfExperience),
                    stalls.GetValueOrDefault(a.Id), StatusLabels.Artisan(_L, a.Status), Date(a.JoinedDate)));
            }
            return Csv(sb, $"artisans_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }
    }
}
