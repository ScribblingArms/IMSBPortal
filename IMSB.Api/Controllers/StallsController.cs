using System.Text.RegularExpressions;
using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>Stalls: the list and map, allocation to artisans, vacating, and reallocation requests.</summary>
    [Authorize(Roles = "Admin")]
    public class StallsController : ApiControllerBase
    {
        private const int RecentReallocations = 50;
        private static readonly Regex StallCodePattern = new("^[A-Z0-9-]{1,20}$", RegexOptions.Compiled);

        private readonly IStallsInterface _stalls;

        public StallsController(IStallsInterface stalls) => _stalls = stalls;

        /// <summary>Stats, the filtered list, the full map, pickers and reallocation requests.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId, [FromQuery] string? block)
        {
            var all = await _stalls.AllAsync();
            var filtered = await _stalls.FilteredIdsAsync(q, status, categoryId, block);
            var pending = await _stalls.PendingReallocationStallIdsAsync();
            var productCounts = await _stalls.ProductCountsAsync();

            return Ok(new
            {
                stats = await _stalls.StatsAsync(),
                stalls = all.Where(s => filtered.Contains(s.Id)).Select(s => s.ToListItem(pending.Contains(s.Id), productCounts)),
                map = all.Select(s => s.ToMapItem(pending.Contains(s.Id))),
                blocks = all.Select(s => s.Block).Distinct().OrderBy(b => b),
                categories = (await _stalls.CategoriesAsync()).ToOptions(),
                freeArtisans = (await _stalls.FreeArtisansAsync()).Select(a => a.ToFreeOption()),
                reallocations = (await _stalls.RecentReallocationsAsync(RecentReallocations)).Select(r => r.ToItem())
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] StallDto dto)
        {
            var errors = new Dictionary<string, string>();
            var code = dto.StallCode?.Trim().ToUpperInvariant() ?? "";
            if (!StallCodePattern.IsMatch(code)) errors["stall_code"] = L["ValStallCode"];
            else if (await _stalls.StallCodeExistsAsync(code)) errors["stall_code"] = L["ValStallCodeExists"];
            var status = dto.Status?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(status)) status = StallStatuses.Vacant;
            if (!StallStatuses.Settable.Contains(status)) errors["status"] = L["ValStallStatus"];
            var categoryId = await ValidateCategoryAsync(dto.CategoryId, errors);
            if (errors.Count > 0) return Invalid(errors);

            var stall = new Stall { StallCode = code, Status = status, CreatedBy = UserName };
            ApplyDetails(stall, dto, categoryId);
            await _stalls.AddAsync(stall);
            return Ok(new { code = stall.StallCode });
        }

        /// <summary>Edits the details; the code is fixed, and an occupied stall stays OCCUPIED (vacate or reallocate instead).</summary>
        [HttpPut("{code}")]
        public async Task<IActionResult> Update(string code, [FromBody] StallDto dto)
        {
            var stall = await _stalls.FindAsync(code);
            if (stall == null) return NotFound();

            var errors = new Dictionary<string, string>();
            var status = dto.Status?.Trim().ToUpperInvariant();
            if (stall.ArtisanId != null)
            {
                if (!string.IsNullOrEmpty(status) && status != StallStatuses.Occupied) errors["status"] = L["ValStallOccupiedStatus"];
                status = StallStatuses.Occupied;
            }
            else
            {
                if (string.IsNullOrEmpty(status)) status = stall.Status == StallStatuses.Occupied ? StallStatuses.Vacant : stall.Status;
                if (!StallStatuses.Settable.Contains(status)) errors["status"] = L["ValStallStatus"];
            }
            var categoryId = await ValidateCategoryAsync(dto.CategoryId, errors);
            if (errors.Count > 0) return Invalid(errors);

            stall.Status = status;
            ApplyDetails(stall, dto, categoryId);
            Touch(stall);
            await _stalls.UpdateAsync(stall);
            return Ok(new { code = stall.StallCode });
        }

        /// <summary>Only a vacant stall without products can be deleted; its reallocation requests go with it.</summary>
        [HttpDelete("{code}")]
        public async Task<IActionResult> Delete(string code)
        {
            var stall = await _stalls.FindAsync(code);
            if (stall == null) return NotFound();
            if (stall.ArtisanId != null) return Invalid("stall", "ValStallOccupiedDelete");
            var products = await _stalls.ProductCountAsync(stall.StallCode);
            if (products > 0) return Invalid("stall", "ValStallHasProducts", products);

            await _stalls.RemoveAsync(stall);
            return Ok(new { code });
        }

        /// <summary>Allots a vacant stall to an artisan who holds none.</summary>
        [HttpPost("allocate")]
        public async Task<IActionResult> Allocate([FromBody] AllocateStallDto dto)
        {
            var errors = new Dictionary<string, string>();
            var stall = string.IsNullOrWhiteSpace(dto.StallCode) ? null : await _stalls.FindAsync(dto.StallCode.Trim().ToUpper());
            if (stall == null) errors["stall_code"] = L["ValSelectStall"];
            else if (stall.Status != StallStatuses.Vacant) errors["stall_code"] = L["ValStallNotVacant"];

            var artisan = await FindArtisanAsync(dto.ArtisanCode);
            if (artisan == null) errors["artisan_code"] = L["ValSelectArtisan"];
            else if (!ArtisanStatuses.CanHoldStall(artisan.Status)) errors["artisan_code"] = L["ValArtisanInactive"];
            else if (await _stalls.ArtisanHasStallAsync(artisan.Id)) errors["artisan_code"] = L["ValArtisanHasStall"];

            var categoryId = await ValidateCategoryAsync(dto.CategoryId, errors);

            var allottedValid = TryParseDate(dto.AllottedDate, out var allotted);
            if (!allottedValid) errors["allotted_date"] = L["ValDateInvalid"];
            DateTime? agreementEnd = null;
            if (!string.IsNullOrWhiteSpace(dto.AgreementEndDate))
            {
                if (!TryParseDate(dto.AgreementEndDate, out var end)) errors["agreement_end_date"] = L["ValDateInvalid"];
                else if (allottedValid && end <= allotted) errors["agreement_end_date"] = L["ValAgreementEnd"];
                else agreementEnd = end;
            }
            if (errors.Count > 0) return Invalid(errors);

            AssignArtisan(stall!, artisan!, allotted, agreementEnd, categoryId ?? artisan!.CategoryId);
            stall!.MonthlyRent = dto.MonthlyRent ?? stall.MonthlyRent;
            stall.Remarks = Clean(dto.Remarks) ?? stall.Remarks;
            await _stalls.UpdateAsync(stall);
            return Ok(new { code = stall.StallCode, artisanCode = artisan!.ArtisanCode });
        }

        /// <summary>Frees an occupied stall; any open reallocation request for it is closed.</summary>
        [HttpPost("{code}/vacate")]
        public async Task<IActionResult> Vacate(string code)
        {
            var stall = await _stalls.FindAsync(code);
            if (stall == null) return NotFound();
            if (stall.ArtisanId == null) return Invalid("stall", "ValStallNotOccupied");

            ClearArtisan(stall);
            foreach (var r in await _stalls.OpenReallocationsAsync(stall.Id))
                Decide(r, ReallocationStatuses.Rejected, L["ClosedStallVacated"]);
            await _stalls.UpdateAsync(stall);
            return Ok(new { code = stall.StallCode });
        }

        /// <summary>Asks for an occupied stall to move to another artisan (or to be vacated when none is named).</summary>
        [HttpPost("reallocations")]
        public async Task<IActionResult> RequestReallocation([FromBody] ReallocationDto dto)
        {
            var errors = new Dictionary<string, string>();
            var stall = string.IsNullOrWhiteSpace(dto.StallCode) ? null : await _stalls.FindAsync(dto.StallCode.Trim().ToUpper(), includeArtisan: true);
            if (stall == null) errors["stall_code"] = L["ValSelectStall"];
            else if (stall.ArtisanId == null) errors["stall_code"] = L["ValStallNotOccupied"];
            else if (await _stalls.HasPendingReallocationAsync(stall.Id)) errors["stall_code"] = L["ValReallocationPending"];

            var reason = dto.Reason?.Trim().ToUpperInvariant() ?? "";
            if (!ReallocationReasons.All.Contains(reason)) errors["reason"] = L["ValReason"];

            Artisan? target = null;
            if (!string.IsNullOrWhiteSpace(dto.NewArtisanCode))
            {
                target = await FindArtisanAsync(dto.NewArtisanCode);
                if (target == null) errors["new_artisan_code"] = L["ValSelectArtisan"];
                else if (stall?.ArtisanId == target.Id) errors["new_artisan_code"] = L["ValSameArtisan"];
                else if (!ArtisanStatuses.CanHoldStall(target.Status)) errors["new_artisan_code"] = L["ValArtisanInactive"];
                else if (await _stalls.ArtisanHasStallAsync(target.Id)) errors["new_artisan_code"] = L["ValArtisanHasStall"];
            }

            if (!TryParseDate(dto.RequestedDate, out var requested)) errors["requested_date"] = L["ValDateInvalid"];
            else if (requested > DateTime.Today) errors["requested_date"] = L["ValDateFuture"];
            if (errors.Count > 0) return Invalid(errors);

            var request = new StallReallocation
            {
                RequestNo = NextCode(await _stalls.ReallocationNumbersAsync(), "REA-", 4),
                StallId = stall!.Id,
                StallCode = stall.StallCode,
                CurrentArtisanId = stall.ArtisanId,
                CurrentArtisanName = stall.Artisan?.FullName,
                NewArtisanId = target?.Id,
                NewArtisanName = target?.FullName,
                Reason = reason,
                Remarks = Clean(dto.Remarks),
                RequestedDate = requested,
                CreatedBy = UserName
            };
            await _stalls.AddReallocationAsync(request);
            return Ok(new { id = request.RequestNo });
        }

        /// <summary>Approving moves the stall to the requested artisan, or vacates it when none was named.</summary>
        [HttpPost("reallocations/{id}/approve")]
        public async Task<IActionResult> ApproveReallocation(string id, [FromBody] DecisionDto? dto)
        {
            var request = await _stalls.FindReallocationAsync(id);
            if (request == null) return NotFound();
            if (request.Status != ReallocationStatuses.Pending) return Invalid("request", "ValReallocationDecided");

            var stall = await _stalls.FindByIdAsync(request.StallId);
            // the stall changed hands since the request was made
            if (stall == null || stall.ArtisanId != request.CurrentArtisanId) return Invalid("request", "ValReallocationStale");

            if (request.NewArtisanId != null)
            {
                var target = await _stalls.FindArtisanByIdAsync(request.NewArtisanId.Value);
                if (target == null || !ArtisanStatuses.CanHoldStall(target.Status) || await _stalls.ArtisanHasStallAsync(target.Id))
                    return Invalid("request", "ValReallocationTargetUnavailable");
                AssignArtisan(stall, target, DateTime.Today, null, target.CategoryId);
            }
            else
            {
                ClearArtisan(stall);
            }
            Decide(request, ReallocationStatuses.Approved, dto?.Remarks);
            await _stalls.DecideReallocationAsync(request);
            return Ok(new { id = request.RequestNo, code = stall.StallCode });
        }

        [HttpPost("reallocations/{id}/reject")]
        public async Task<IActionResult> RejectReallocation(string id, [FromBody] DecisionDto? dto)
        {
            var request = await _stalls.FindReallocationAsync(id);
            if (request == null) return NotFound();
            if (request.Status != ReallocationStatuses.Pending) return Invalid("request", "ValReallocationDecided");
            Decide(request, ReallocationStatuses.Rejected, dto?.Remarks);
            await _stalls.DecideReallocationAsync(request);
            return Ok(new { id = request.RequestNo });
        }

        private static void ApplyDetails(Stall stall, StallDto dto, Guid? categoryId)
        {
            stall.Name = Clean(dto.Name)!;
            stall.Block = dto.Block.Trim().ToUpperInvariant();
            stall.RowLabel = Clean(dto.RowLabel);
            stall.Location = Clean(dto.Location);
            stall.CategoryId = categoryId;
            stall.MonthlyRent = dto.MonthlyRent;
            stall.Remarks = Clean(dto.Remarks);
        }

        private async Task<Guid?> ValidateCategoryAsync(string? categoryId, Dictionary<string, string> errors)
        {
            if (string.IsNullOrWhiteSpace(categoryId)) return null;
            if (Guid.TryParse(categoryId, out var id) && await _stalls.CategoryExistsAsync(id)) return id;
            errors.TryAdd("category_id", L["ValCategory"]);
            return null;
        }

        private void AssignArtisan(Stall stall, Artisan artisan, DateTime allotted, DateTime? agreementEnd, Guid? categoryId)
        {
            stall.ArtisanId = artisan.Id;
            stall.Status = StallStatuses.Occupied;
            stall.AllottedDate = allotted;
            stall.AgreementEndDate = agreementEnd;
            stall.CategoryId = categoryId;
            Touch(stall);
        }

        private void ClearArtisan(Stall stall)
        {
            stall.ArtisanId = null;
            stall.Status = StallStatuses.Vacant;
            stall.AllottedDate = null;
            stall.AgreementEndDate = null;
            Touch(stall);
        }

        private void Touch(Stall stall)
        {
            stall.UpdatedAt = DateTime.UtcNow;
            stall.UpdatedBy = UserName;
        }

        private void Decide(StallReallocation r, string status, string? remarks)
        {
            r.Status = status;
            r.DecidedAt = DateTime.UtcNow;
            r.DecidedBy = UserName;
            r.DecisionRemarks = Clean(remarks);
        }

        private async Task<Artisan?> FindArtisanAsync(string? code) =>
            string.IsNullOrWhiteSpace(code) ? null : await _stalls.FindArtisanAsync(code.Trim().ToUpper());
    }
}
