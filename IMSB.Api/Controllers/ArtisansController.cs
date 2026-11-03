using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>The artisan directory and profiles; the Aadhaar number is never returned (masked).</summary>
    [Authorize(Roles = "Admin")]
    public class ArtisansController : ApiControllerBase
    {
        public static readonly string[] Genders = { "Female", "Male", "Other" };

        private readonly IArtisansInterface _artisans;

        public ArtisansController(IArtisansInterface artisans) => _artisans = artisans;

        /// <summary>Stats, the status chart, the filtered directory and the form pickers. stall = assigned / unassigned.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId,
            [FromQuery] string? district, [FromQuery] string? stall)
        {
            var stats = await _artisans.StatsAsync();
            var stallByArtisan = await _artisans.StallsByArtisanAsync();

            return Ok(new
            {
                stats,
                chart = new
                {
                    statuses = new[] { ArtisanStatuses.Active, ArtisanStatuses.OnLeave, ArtisanStatuses.RenewalDue, ArtisanStatuses.Inactive },
                    values = new[] { stats.Active, stats.OnLeave, stats.RenewalPending, stats.Inactive }
                },
                artisans = (await _artisans.FilterAsync(q, status, categoryId, district, stall)).Select(a => new
                {
                    code = a.ArtisanCode,
                    fullName = a.FullName,
                    mobile = a.Mobile,
                    district = a.District,
                    categoryId = a.CategoryId.ToString(),
                    categoryName = a.Category?.Name,
                    status = a.Status,
                    stallCode = stallByArtisan.GetValueOrDefault(a.Id),
                    joinedDate = Date(a.JoinedDate)
                }),
                categories = (await _artisans.CategoriesAsync()).ToOptions(),
                districts = TelanganaDistricts.All
            });
        }

        /// <summary>Profile: details, stall, and this month's sales at that stall.</summary>
        [HttpGet("{code}")]
        public async Task<IActionResult> Get(string code)
        {
            var a = await _artisans.GetDetailsAsync(code);
            if (a == null) return NotFound();
            var stall = await _artisans.StallOfAsync(a.Id);
            var (monthSales, products) = stall == null ? (0m, 0) : await _artisans.StallFiguresAsync(stall.StallCode);

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
                fromDate = Date(a.FromDate),
                toDate = Date(a.ToDate),
                remarks = a.Remarks,
                stallCode = stall?.StallCode,
                allottedDate = stall?.AllottedDate == null ? null : Date(stall.AllottedDate),
                monthSales,
                productsListed = products
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ArtisanDto dto)
        {
            var (errors, v) = await ValidateAsync(dto, null);
            if (errors.Count > 0) return Invalid(errors);

            var artisan = new Artisan { ArtisanCode = NextCode(await _artisans.ArtisanCodesAsync(), "ART-", 4), CreatedBy = UserName };
            Apply(artisan, dto, v);
            await _artisans.AddAsync(artisan);
            return Ok(new { code = artisan.ArtisanCode });
        }

        [HttpPut("{code}")]
        public async Task<IActionResult> Update(string code, [FromBody] ArtisanDto dto)
        {
            var artisan = await _artisans.FindAsync(code);
            if (artisan == null) return NotFound();
            var (errors, v) = await ValidateAsync(dto, artisan);
            if (errors.Count > 0) return Invalid(errors);

            Apply(artisan, dto, v);
            artisan.UpdatedAt = DateTime.UtcNow;
            artisan.UpdatedBy = UserName;
            await _artisans.UpdateAsync(artisan);
            return Ok(new { code = artisan.ArtisanCode });
        }

        /// <summary>An artisan holding a stall cannot be deleted; open reallocation requests naming them are closed.</summary>
        [HttpDelete("{code}")]
        public async Task<IActionResult> Delete(string code)
        {
            var artisan = await _artisans.FindAsync(code);
            if (artisan == null) return NotFound();
            var stall = await _artisans.StallOfAsync(artisan.Id);
            if (stall != null) return Invalid("artisan", "ValArtisanDeleteHasStall", stall.StallCode);

            await _artisans.DeleteAsync(artisan, UserName, L["ClosedArtisanDeleted"]);
            return Ok(new { code });
        }

        private record Validated(DateTime DateOfBirth, string Mobile, string? Aadhaar, Guid CategoryId, string Status, DateTime JoinedDate, string District, string Gender,
            DateTime? FromDate, DateTime? ToDate);

        private async Task<(Dictionary<string, string> Errors, Validated V)> ValidateAsync(ArtisanDto dto, Artisan? existing)
        {
            var errors = new Dictionary<string, string>();
            var selfId = existing?.Id ?? Guid.Empty;

            if (!TryParseDate(dto.DateOfBirth, out var dob)) errors["date_of_birth"] = L["ValDobInvalid"];
            else if (dob > DateTime.Today) errors["date_of_birth"] = L["ValDobFuture"];
            else if (dob > DateTime.Today.AddYears(-18)) errors["date_of_birth"] = L["ValArtisanMinAge"];
            else if (dob < DateTime.Today.AddYears(-100)) errors["date_of_birth"] = L["ValDobInvalid"];

            var gender = Genders.FirstOrDefault(g => g.Equals(dto.Gender?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (gender == null) errors["gender"] = L["ValGender"];

            // 10-digit Indian mobile; "+91 98765 43210" and "098765..." are accepted and stored as 9876543210
            var mobile = MobileNumbers.Normalize(dto.Mobile);
            if (!MobileNumbers.IsValid(mobile)) errors["mobile"] = L["ValArtisanMobile"];
            else if (await _artisans.MobileExistsAsync(mobile, selfId)) errors["mobile"] = L["ValMobileExists"];

            string? aadhaar = null;
            var aadhaarGiven = !string.IsNullOrWhiteSpace(dto.Aadhaar);
            if (existing == null && !aadhaarGiven) errors["aadhaar"] = L["ValAadhaar"];
            else if (aadhaarGiven)
            {
                if (!Aadhaar.IsValid(dto.Aadhaar)) errors["aadhaar"] = L["ValAadhaar"];
                else if (await _artisans.AadhaarExistsAsync(Aadhaar.Hash(dto.Aadhaar!), selfId)) errors["aadhaar"] = L["ValAadhaarExists"];
                else aadhaar = dto.Aadhaar;
            }

            var district = TelanganaDistricts.All.FirstOrDefault(d => d.Equals(dto.District?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (district == null) errors["district"] = L["ValDistrict"];

            if (!Guid.TryParse(dto.CategoryId, out var categoryId) || !await _artisans.CategoryExistsAsync(categoryId))
                errors["category_id"] = L["ValCraftCategory"];

            var status = string.IsNullOrWhiteSpace(dto.Status) ? existing?.Status ?? ArtisanStatuses.Active : dto.Status.Trim().ToUpperInvariant();
            if (!ArtisanStatuses.All.Contains(status)) errors["status"] = L["ValStatus"];
            else if (existing != null && !ArtisanStatuses.CanHoldStall(status) && await _artisans.HasStallAsync(existing.Id))
                errors["status"] = L["ValInactiveWithStall"];

            var joined = existing?.JoinedDate ?? DateTime.Today;
            if (!string.IsNullOrWhiteSpace(dto.JoinedDate))
            {
                if (!TryParseDate(dto.JoinedDate, out joined)) errors["joined_date"] = L["ValJoinInvalid"];
                else if (joined > DateTime.Today) errors["joined_date"] = L["ValJoinFuture"];
                else if (!errors.ContainsKey("date_of_birth") && joined < dob) errors["joined_date"] = L["ValJoinInvalid"];
            }

            if (ParsePeriod(dto.FromDate, dto.ToDate, out var from, out var to) is { } period) errors[period.Field] = L[period.Key];

            return (errors, new Validated(dob, mobile, aadhaar, categoryId, status, joined, district ?? "", gender ?? "", from, to));
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
            a.FromDate = v.FromDate;
            a.ToDate = v.ToDate;
            a.Remarks = Clean(dto.Remarks);
        }
    }
}
