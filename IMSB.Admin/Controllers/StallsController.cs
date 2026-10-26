using IMSB.Admin.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StallsController : Controller
    {
        private const int RecentReallocations = 50;
        private static readonly Regex StallCodePattern = new("^[A-Z0-9-]{1,20}$", RegexOptions.Compiled);

        private readonly IStallsInterface _stalls;
        private readonly IStringLocalizer _L;

        public StallsController(IStallsInterface stalls, IStringLocalizerFactory localizerFactory)
        {
            _stalls = stalls;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(StallsController).Assembly.GetName().Name!);
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = _L["StallManagement"].Value;
            ViewBag.Stats = await ComputeStatsAsync();
            return View();
        }

        internal Task<StallStats> ComputeStatsAsync() => _stalls.StatsAsync();

        /// <summary>Everything the page needs: stats, filtered list, the full map, pickers and reallocation requests.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId, [FromQuery] string? block)
        {
            var all = await _stalls.AllAsync();
            var filtered = await _stalls.FilteredIdsAsync(q, status, categoryId, block);
            var pending = await _stalls.PendingReallocationStallIdsAsync();
            var productCounts = await _stalls.ProductCountsAsync();

            return Ok(new
            {
                stats = await ComputeStatsAsync(),
                stalls = all.Where(s => filtered.Contains(s.Id)).Select(s => s.ToListItem(pending.Contains(s.Id), productCounts)),
                map = all.Select(s => s.ToMapItem(pending.Contains(s.Id))),
                blocks = all.Select(s => s.Block).Distinct().OrderBy(b => b),
                categories = (await _stalls.CategoriesAsync()).ToOptions(),
                freeArtisans = (await _stalls.FreeArtisansAsync()).Select(a => a.ToFreeOption()),
                reallocations = (await _stalls.RecentReallocationsAsync(RecentReallocations)).Select(r => r.ToItem())
            });
        }

        // ── Create / edit / delete ────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] StallDto? dto)
        {
            if (dto == null) return BadRequest();
            var errors = ModelStateErrors(ModelState);
            var code = dto.StallCode?.Trim().ToUpperInvariant() ?? "";
            if (!errors.ContainsKey("stall_code"))
            {
                if (!StallCodePattern.IsMatch(code)) errors["stall_code"] = _L["ValStallCode"];
                else if (await _stalls.StallCodeExistsAsync(code)) errors["stall_code"] = _L["ValStallCodeExists"];
            }
            var status = dto.Status?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(status)) status = StallStatuses.Vacant;
            if (!StallStatuses.Settable.Contains(status)) errors.TryAdd("status", _L["ValStallStatus"]);
            var categoryId = await ValidateCategoryAsync(dto.CategoryId, errors);
            if (errors.Count > 0) return BadRequest(new { errors });

            var stall = new Stall
            {
                StallCode = code,
                Status = status,
                CreatedBy = User.Identity?.Name
            };
            ApplyDetails(stall, dto, categoryId);
            await _stalls.AddAsync(stall);
            return Ok(new { code = stall.StallCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string id, [FromBody] StallDto? dto)
        {
            if (dto == null) return BadRequest();
            var stall = await _stalls.FindAsync(id);
            if (stall == null) return NotFound();

            var errors = ModelStateErrors(ModelState);
            errors.Remove("stall_code");   // the code is fixed after creation
            var status = dto.Status?.Trim().ToUpperInvariant();
            if (stall.ArtisanId != null)
            {
                // occupancy comes from allocation: vacate or reallocate instead
                if (!string.IsNullOrEmpty(status) && status != StallStatuses.Occupied) errors.TryAdd("status", _L["ValStallOccupiedStatus"]);
                status = StallStatuses.Occupied;
            }
            else
            {
                if (string.IsNullOrEmpty(status)) status = stall.Status == StallStatuses.Occupied ? StallStatuses.Vacant : stall.Status;
                if (!StallStatuses.Settable.Contains(status)) errors.TryAdd("status", _L["ValStallStatus"]);
            }
            var categoryId = await ValidateCategoryAsync(dto.CategoryId, errors);
            if (errors.Count > 0) return BadRequest(new { errors });

            stall.Status = status;
            ApplyDetails(stall, dto, categoryId);
            Touch(stall);
            await _stalls.UpdateAsync(stall);
            return Ok(new { code = stall.StallCode });
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
            errors.TryAdd("category_id", _L["ValCategory"]);
            return null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var stall = await _stalls.FindAsync(id);
            if (stall == null) return NotFound();
            if (stall.ArtisanId != null) return ErrorResult("stall", _L["ValStallOccupiedDelete"]);
            var products = await _stalls.ProductCountAsync(stall.StallCode);
            if (products > 0) return ErrorResult("stall", string.Format(_L["ValStallHasProducts"], products));

            await _stalls.RemoveAsync(stall);   // its reallocation requests go with it
            return Ok(new { code = id });
        }

        // ── Allocation ────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Allocate([FromBody] AllocateStallDto? dto)
        {
            if (dto == null) return BadRequest();
            var errors = ModelStateErrors(ModelState);

            var stall = string.IsNullOrWhiteSpace(dto.StallCode) ? null : await _stalls.FindAsync(dto.StallCode.Trim().ToUpper());
            if (stall == null) errors.TryAdd("stall_code", _L["ValSelectStall"]);
            else if (stall.Status != StallStatuses.Vacant) errors.TryAdd("stall_code", _L["ValStallNotVacant"]);

            var artisan = await FindArtisanAsync(dto.ArtisanCode);
            if (artisan == null) errors.TryAdd("artisan_code", _L["ValSelectArtisan"]);
            else if (!ArtisanStatuses.CanHoldStall(artisan.Status)) errors.TryAdd("artisan_code", _L["ValArtisanInactive"]);
            else if (await _stalls.ArtisanHasStallAsync(artisan.Id)) errors.TryAdd("artisan_code", _L["ValArtisanHasStall"]);

            var categoryId = await ValidateCategoryAsync(dto.CategoryId, errors);

            DateTime allotted = default;
            if (!errors.ContainsKey("allotted_date") && !TryParseDate(dto.AllottedDate, out allotted)) errors["allotted_date"] = _L["ValDateInvalid"];
            DateTime? agreementEnd = null;
            if (!string.IsNullOrWhiteSpace(dto.AgreementEndDate))
            {
                if (!TryParseDate(dto.AgreementEndDate, out var end)) errors["agreement_end_date"] = _L["ValDateInvalid"];
                else if (!errors.ContainsKey("allotted_date") && end <= allotted) errors["agreement_end_date"] = _L["ValAgreementEnd"];
                else agreementEnd = end;
            }
            if (errors.Count > 0) return BadRequest(new { errors });

            AssignArtisan(stall!, artisan!, allotted, agreementEnd, categoryId ?? artisan!.CategoryId);
            stall!.MonthlyRent = dto.MonthlyRent ?? stall.MonthlyRent;
            stall.Remarks = Clean(dto.Remarks) ?? stall.Remarks;
            await _stalls.UpdateAsync(stall);
            return Ok(new { code = stall.StallCode, artisanCode = artisan!.ArtisanCode });
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
            stall.UpdatedBy = User.Identity?.Name;
        }

        private async Task<Artisan?> FindArtisanAsync(string? code) =>
            string.IsNullOrWhiteSpace(code) ? null : await _stalls.FindArtisanAsync(code.Trim().ToUpper());

        /// <summary>Frees an occupied stall; any open reallocation request for it is closed.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Vacate(string id)
        {
            var stall = await _stalls.FindAsync(id);
            if (stall == null) return NotFound();
            if (stall.ArtisanId == null) return ErrorResult("stall", _L["ValStallNotOccupied"]);

            ClearArtisan(stall);
            foreach (var r in await _stalls.OpenReallocationsAsync(stall.Id))
                Decide(r, ReallocationStatuses.Rejected, _L["ClosedStallVacated"]);
            await _stalls.UpdateAsync(stall);
            return Ok(new { code = stall.StallCode });
        }

        private void Decide(StallReallocation r, string status, string? remarks)
        {
            r.Status = status;
            r.DecidedAt = DateTime.UtcNow;
            r.DecidedBy = User.Identity?.Name;
            r.DecisionRemarks = Clean(remarks);
        }

        // ── Reallocation ──────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestReallocation([FromBody] ReallocationDto? dto)
        {
            if (dto == null) return BadRequest();
            var errors = ModelStateErrors(ModelState);

            var stall = string.IsNullOrWhiteSpace(dto.StallCode) ? null : await _stalls.FindAsync(dto.StallCode.Trim().ToUpper(), includeArtisan: true);
            if (stall == null) errors.TryAdd("stall_code", _L["ValSelectStall"]);
            else if (stall.ArtisanId == null) errors.TryAdd("stall_code", _L["ValStallNotOccupied"]);
            else if (await _stalls.HasPendingReallocationAsync(stall.Id)) errors.TryAdd("stall_code", _L["ValReallocationPending"]);

            var reason = dto.Reason?.Trim().ToUpperInvariant() ?? "";
            if (!errors.ContainsKey("reason") && !ReallocationReasons.All.Contains(reason)) errors["reason"] = _L["ValReason"];

            Artisan? target = null;
            if (!string.IsNullOrWhiteSpace(dto.NewArtisanCode))
            {
                target = await FindArtisanAsync(dto.NewArtisanCode);
                if (target == null) errors["new_artisan_code"] = _L["ValSelectArtisan"];
                else if (stall?.ArtisanId == target.Id) errors["new_artisan_code"] = _L["ValSameArtisan"];
                else if (!ArtisanStatuses.CanHoldStall(target.Status)) errors["new_artisan_code"] = _L["ValArtisanInactive"];
                else if (await _stalls.ArtisanHasStallAsync(target.Id)) errors["new_artisan_code"] = _L["ValArtisanHasStall"];
            }

            DateTime requested = default;
            if (!errors.ContainsKey("requested_date"))
            {
                if (!TryParseDate(dto.RequestedDate, out requested)) errors["requested_date"] = _L["ValDateInvalid"];
                else if (requested > DateTime.Today) errors["requested_date"] = _L["ValDateFuture"];
            }
            if (errors.Count > 0) return BadRequest(new { errors });

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
                CreatedBy = User.Identity?.Name
            };
            await _stalls.AddReallocationAsync(request);
            return Ok(new { id = request.RequestNo });
        }

        /// <summary>Approving moves the stall to the requested artisan, or vacates it when none was named.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveReallocation(string id, [FromBody] DecisionDto? dto)
        {
            var request = await _stalls.FindReallocationAsync(id);
            if (request == null) return NotFound();
            if (request.Status != ReallocationStatuses.Pending) return ErrorResult("request", _L["ValReallocationDecided"]);

            var stall = await _stalls.FindByIdAsync(request.StallId);
            // the stall changed hands since the request was made
            if (stall == null || stall.ArtisanId != request.CurrentArtisanId) return ErrorResult("request", _L["ValReallocationStale"]);

            if (request.NewArtisanId != null)
            {
                var target = await _stalls.FindArtisanByIdAsync(request.NewArtisanId.Value);
                if (target == null || !ArtisanStatuses.CanHoldStall(target.Status) || await _stalls.ArtisanHasStallAsync(target.Id))
                    return ErrorResult("request", _L["ValReallocationTargetUnavailable"]);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectReallocation(string id, [FromBody] DecisionDto? dto)
        {
            var request = await _stalls.FindReallocationAsync(id);
            if (request == null) return NotFound();
            if (request.Status != ReallocationStatuses.Pending) return ErrorResult("request", _L["ValReallocationDecided"]);
            Decide(request, ReallocationStatuses.Rejected, dto?.Remarks);
            await _stalls.DecideReallocationAsync(request);
            return Ok(new { id = request.RequestNo });
        }

        // ── Export ────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? categoryId, [FromQuery] string? block)
        {
            var stalls = await _stalls.FilterAsync(q, status, categoryId, block);

            var rows = new List<string?[]>
            {
                new string?[] { _L["StallID"], _L["StallName"], _L["BlockZone"], _L["Row"], _L["Location"], _L["Status"], _L["Category"],
                    _L["ArtisanID"], _L["Artisan"], _L["AllottedDate"], _L["AgreementEndDate"], _L["MonthlyRent"], _L["Remarks"] }
            };
            foreach (var s in stalls)
            {
                rows.Add(new string?[] { s.StallCode, s.Name, s.Block, s.RowLabel, s.Location, StatusLabels.Stall(_L, s.Status), s.Category?.Name,
                    s.Artisan?.ArtisanCode, s.Artisan?.FullName, Date(s.AllottedDate), Date(s.AgreementEndDate),
                    s.MonthlyRent?.ToString("0.00", CultureInfo.InvariantCulture), s.Remarks });
            }
            return ExportCsv($"stalls_{DateTime.Now:yyyyMMddHHmmss}.csv", rows);
        }
    }
}
