using System.Security.Claims;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers;

/// <summary>
/// SHG Applications: the stall applications Self-Help Groups make on the IMSB website (SHGDetails). Officers view them,
/// allot a stall to a submitted one, or reject it with a reason.
/// District Officers see only their own district (the district on their login); Admin and State Admin Officers see all.
/// </summary>
[Authorize(Roles = UserRoles.ShgApplicationsAccess)]
public class ShgApplicationsController : Controller
{
    private readonly IShgApplicationsInterface _shg;
    private readonly IStringLocalizer _L;
    private readonly string _uploadRoot;

    public ShgApplicationsController(IShgApplicationsInterface shg, IStringLocalizerFactory localizerFactory,
        IConfiguration configuration, IWebHostEnvironment env)
    {
        _shg = shg;
        _L = localizerFactory.Create("SharedResource", typeof(ShgApplicationsController).Assembly.GetName().Name!);
        // the website saves photos and documents here; both apps must point at the same folder (Uploads:ShgPhotosPath)
        var configured = configuration["Uploads:ShgPhotosPath"];
        _uploadRoot = string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "IMSB", "App_Data", "shg-photos"))
            : Path.GetFullPath(configured, env.ContentRootPath);
    }

    /// <summary>What the signed-in user may see: every district, or one (none when an officer's district is not set or unknown).</summary>
    internal record Scope(bool AllDistricts, int? DistrictId, string? DistrictName)
    {
        public bool SeesNothing => !AllDistricts && DistrictId == null;
        public int? Filter => AllDistricts ? null : DistrictId;
    }

    internal async Task<Scope> ScopeAsync()
    {
        if (!User.IsInRole(UserTypes.DistrictOfficer)) return new Scope(true, null, null);
        // read from the login on every request, so a district changed in User Management applies at once
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var loginId)) return new Scope(false, null, null);
        var name = await _shg.LoginDistrictAsync(loginId);
        var district = (await _shg.DistrictsAsync()).FirstOrDefault(d => DistrictNames.Same(d.Name, name));
        return new Scope(false, district?.Id, district?.Name);
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = _L["ShgApplications"].Value;
        var scope = await ScopeAsync();
        ViewBag.ScopeText = scope.AllDistricts ? _L["ShgScopeAll"].Value
            : scope.SeesNothing ? _L["ShgNoDistrictAssigned"].Value
            : string.Format(_L["ShgScopeDistrict"].Value, DistrictLabel(scope.DistrictName));
        ViewBag.SeesNothing = scope.SeesNothing;
        return View();
    }

    /// <summary>The applications the user may see, the counts, and (for all-district users) the district filter.</summary>
    [HttpGet]
    public async Task<IActionResult> Data()
    {
        var scope = await ScopeAsync();
        var apps = scope.SeesNothing ? new List<ShgDetail>() : await _shg.ListAsync(scope.Filter);
        var stalls = await _shg.AllottedStallsAsync();
        var districts = scope.AllDistricts
            ? (await _shg.DistrictsAsync()).Select(d => new { value = d.Id, label = DistrictLabel(d.Name) }).OrderBy(d => d.label).ToList()
            : new();

        return Ok(new
        {
            scope = new { allDistricts = scope.AllDistricts, seesNothing = scope.SeesNothing, districtLabel = DistrictLabel(scope.DistrictName) },
            stats = new
            {
                total = apps.Count,
                inProcess = apps.Count(a => Status(a, stalls.ContainsKey(a.Id)) == ShgStatus.InProcess),
                submitted = apps.Count(a => Status(a, stalls.ContainsKey(a.Id)) == ShgStatus.Submitted),
                incomplete = apps.Count(a => Status(a, stalls.ContainsKey(a.Id)) == ShgStatus.Incomplete),
                rejected = apps.Count(a => Status(a, stalls.ContainsKey(a.Id)) == ShgStatus.Rejected)
            },
            districts,
            rows = apps.Select(a => new
            {
                id = a.PublicId,
                reference = a.ApplicationRef,
                a.ShgName,
                a.ShgId,
                a.DistrictId,
                districtLabel = DistrictLabel(a.District?.Name),
                a.Mandal,
                a.Village,
                a.RepName,
                a.RepMobile,
                category = a.ProductCategory?.Name?.Trim(),
                submitted = a.IsSubmitted,
                status = Status(a, stalls.ContainsKey(a.Id)),
                statusLabel = StatusLabel(a, stalls.ContainsKey(a.Id)),
                stallCode = stalls.TryGetValue(a.Id, out var stall) ? stall.StallCode : null,
                rejectionReason = a.RejectionReason,
                createdAt = Utc(a.CreatedAt),
                submittedAt = a.SubmittedAt == null ? (DateTime?)null : Utc(a.SubmittedAt.Value)
            })
        });
    }

    /// <summary>Every saved field of one application, with links to its photos and documents.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid id)
    {
        var a = await FindInScopeAsync(id);
        if (a == null) return NotFound();
        var held = await _shg.StallOfAsync(a.Id);

        object FileLink(string name) => new
        {
            name,
            url = $"/ShgApplications/File/{a.PublicId}?name={Uri.EscapeDataString(name)}",
            isPdf = name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
        };

        return Ok(new
        {
            id = a.PublicId,
            reference = a.ApplicationRef,
            submitted = a.IsSubmitted,
            status = Status(a, held != null),
            statusLabel = StatusLabel(a, held != null),
            createdAt = Utc(a.CreatedAt),
            submittedAt = a.SubmittedAt == null ? (DateTime?)null : Utc(a.SubmittedAt.Value),
            missingSteps = a.MissingSteps,
            // Allocate Stall: in process only. Reject: in process or incomplete (never once rejected or holding a stall)
            canAllocate = CanAllocate(a, held != null),
            canReject = CanReject(a, held != null),
            rejection = !a.IsRejected ? null : new
            {
                reason = a.RejectionReason,
                rejectedBy = a.RejectedBy,
                rejectedAt = Utc(a.RejectedAt!.Value)
            },
            category = a.ProductCategory?.Name?.Trim(),
            allottedStall = held == null ? null : new
            {
                code = held.StallCode, held.Name, held.Block, allottedDate = Date(held.AllottedDate),
                agreementEndDate = held.AgreementEndDate == null ? null : Date(held.AgreementEndDate), held.MonthlyRent, held.Remarks
            },
            shg = new
            {
                a.ShgName, a.ShgId, a.VillageOrganisation, a.MandalSamakhya, districtLabel = DistrictLabel(a.District?.Name),
                a.Mandal, a.Village, a.YearFormed, a.MemberCount
            },
            representative = a.RepSavedAt == null ? null : new
            {
                name = a.RepName, role = a.RepRole, age = a.RepAge, guardianName = a.RepGuardianName, mobile = a.RepMobile,
                altMobile = a.RepAltMobile, email = a.RepEmail, address = a.RepAddress, pinCode = a.RepPinCode
            },
            product = a.ProductSavedAt == null ? null : new
            {
                category = a.ProductCategory?.Name?.Trim(), type = a.ProductType, description = a.ProductDescription,
                priceMin = a.PriceMin, priceMax = a.PriceMax, capacity = a.ProductionCapacity,
                photos = a.ProductPhotoFiles.Select(FileLink).ToList()
            },
            stall = a.StallSavedAt == null ? null : new
            {
                duration = a.StallDuration,
                startMonth = a.StallStartMonth?.ToString("yyyy-MM"),
                facilities = string.IsNullOrEmpty(a.StallFacilities) ? Array.Empty<string>() : a.StallFacilities.Split('|'),
                previousExhibition = a.PreviousExhibition,
                previousExhibitionDetails = a.PreviousExhibitionDetails
            },
            declaration = a.DeclarationSavedAt == null ? null : new
            {
                detailsTrue = a.DeclarationTrue,
                consent = a.DeclarationConsent,
                shgDocument = a.ShgDocument == null ? null : FileLink(a.ShgDocument),
                idProof = a.IdProofDocument == null ? null : FileLink(a.IdProofDocument)
            }
        });
    }

    /// <summary>
    /// A product photo or document of an application the user may see, shown in the browser. Only names stored on the
    /// row are served, so no other file can be reached.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> File(Guid id, string? name)
    {
        var a = await FindInScopeAsync(id);
        if (a == null || string.IsNullOrEmpty(name)) return NotFound();
        var stored = a.ProductPhotoFiles.Append(a.ShgDocument).Append(a.IdProofDocument);
        if (!stored.Contains(name, StringComparer.Ordinal)) return NotFound();

        var path = Path.Combine(_uploadRoot, a.PublicId.ToString("N"), Path.GetFileName(name));
        if (!System.IO.File.Exists(path)) return NotFound();
        var type = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            _ => null
        };
        if (type == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, type);
    }

    /// <summary>Excel file of the applications the user may see.</summary>
    [HttpGet]
    public async Task<IActionResult> Export()
    {
        var scope = await ScopeAsync();
        var apps = scope.SeesNothing ? new List<ShgDetail>() : await _shg.ListAsync(scope.Filter);
        var stalls = await _shg.AllottedStallsAsync();
        var rows = new List<string?[]>
        {
            new string?[] { _L["ShgReference"], _L["Status"], _L["Stall"], _L["ShgName"], _L["ShgIdLabel"], _L["ShgVillageOrganisation"], _L["ShgMandalSamakhya"],
                _L["District"], _L["ShgMandal"], _L["ShgVillage"], _L["ShgMembers"], _L["ShgRepresentative"], _L["ShgRepRole"], _L["Mobile"],
                _L["Email"], _L["ShgProductCategory"], _L["ShgProductType"], _L["ShgPriceMin"], _L["ShgPriceMax"], _L["ShgDuration"],
                _L["ShgStartMonth"], _L["ShgFacilities"], _L["ShgCreatedOn"], _L["ShgSubmittedOn"], _L["ShgRejectionReason"] }
        };
        foreach (var a in apps)
            rows.Add(new string?[] { a.ApplicationRef, StatusLabel(a, stalls.ContainsKey(a.Id)), stalls.TryGetValue(a.Id, out var st) ? st.StallCode : null, a.ShgName, a.ShgId, a.VillageOrganisation, a.MandalSamakhya,
                DistrictLabel(a.District?.Name), a.Mandal, a.Village, Int(a.MemberCount), a.RepName, a.RepRole, a.RepMobile,
                a.RepEmail, a.ProductCategory?.Name?.Trim(), a.ProductType,
                a.PriceMin == null ? null : Money(a.PriceMin.Value), a.PriceMax == null ? null : Money(a.PriceMax.Value),
                a.StallDuration, a.StallStartMonth?.ToString("yyyy-MM"), a.StallFacilities?.Replace("|", ", "),
                LocalDate(a.CreatedAt), LocalDate(a.SubmittedAt), a.RejectionReason });
        return ExportExcel($"shg_applications_{DateTime.Now:yyyyMMddHHmmss}.xls", rows);
    }

    // ── Allocate Stall ────────────────────────────────────────────

    /// <summary>The vacant stalls for the Allocate Stall popup.</summary>
    [HttpGet]
    public async Task<IActionResult> VacantStalls() =>
        Ok((await _shg.VacantStallsAsync()).Select(s => new { code = s.StallCode, s.Name, s.Block, s.MonthlyRent }));

    /// <summary>
    /// Allots a vacant stall to a submitted SHG application: stalls.IsArtisan = false and stalls.ShgDetailId = the application,
    /// with no artisan; the product category comes from the application. A District Officer can only allot to
    /// applications of their own district. Same date and rent rules as Stall Management's Allocate.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Allocate(Guid id, [FromBody] ShgAllocateStallDto? dto)
    {
        if (dto == null) return ErrorResult("request", _L["CheckForm"].Value);
        var app = await FindInScopeAsync(id);
        if (app == null) return NotFound();
        if (!app.IsSubmitted) return ErrorResult("request", _L["ShgValNotSubmitted"].Value);
        if (app.IsRejected) return ErrorResult("request", _L["ShgValRejected"].Value);
        if (await _shg.StallOfAsync(app.Id) is { } held) return ErrorResult("request", string.Format(_L["ShgValHasStall"].Value, held.StallCode));

        var errors = ModelStateErrors(ModelState);
        var stall = string.IsNullOrWhiteSpace(dto.StallCode) ? null : await _shg.FindStallAsync(dto.StallCode.Trim().ToUpperInvariant());
        if (stall == null) errors.TryAdd("stall_code", _L["ValSelectStall"]);
        else if (stall.Status != StallStatuses.Vacant || stall.IsHeld) errors.TryAdd("stall_code", _L["ValStallNotVacant"]);

        DateTime allotted = default;
        if (!errors.ContainsKey("allotted_date") && !TryParseDate(dto.AllottedDate, out allotted)) errors["allotted_date"] = _L["ValDateInvalid"];
        DateTime? agreementEnd = null;
        if (!string.IsNullOrWhiteSpace(dto.AgreementEndDate))
        {
            if (!TryParseDate(dto.AgreementEndDate, out var end)) errors["agreement_end_date"] = _L["ValDateInvalid"];
            else if (!errors.ContainsKey("allotted_date") && end <= allotted) errors["agreement_end_date"] = _L["ValAgreementEnd"];
            else agreementEnd = end;
        }
        if (dto.MonthlyRent is < 0 or > 10000000) errors.TryAdd("monthly_rent", _L["ValRent"]);
        if (dto.Remarks?.Length > 500) errors.TryAdd("remarks", _L["ValRemarks"]);
        if (errors.Count > 0) return BadRequest(new { errors });

        stall!.AssignTo(app, allotted, agreementEnd);
        stall.MonthlyRent = dto.MonthlyRent ?? stall.MonthlyRent;
        stall.Remarks = Clean(dto.Remarks) ?? stall.Remarks;
        stall.UpdatedAt = DateTime.UtcNow;
        stall.UpdatedBy = User.Identity?.Name;
        await _shg.SaveStallAsync(stall);
        return Ok(new { code = stall.StallCode, message = string.Format(_L["ShgStallAllocated"].Value, stall.StallCode, app.ShgName) });
    }

    // ── Reject ────────────────────────────────────────────────────

    /// <summary>
    /// Rejects an application that is in process or still incomplete, with a reason (required, up to 500 characters).
    /// It is then shown as Rejected, can no longer be given a stall, and the website can no longer change or submit it.
    /// A District Officer can only reject in their own district.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ShgRejectDto? dto)
    {
        var app = await FindInScopeAsync(id);
        if (app == null) return NotFound();
        if (app.IsRejected) return ErrorResult("request", _L["ShgValAlreadyRejected"].Value);
        if (await _shg.StallOfAsync(app.Id) is { } held) return ErrorResult("request", string.Format(_L["ShgValHasStall"].Value, held.StallCode));

        var reason = Clean(dto?.Reason);
        if (reason == null || reason.Length > ShgDetail.RejectionReasonMaxLength)
            return BadRequest(new { errors = new Dictionary<string, string> { ["reason"] = _L["ShgValRejectReason"].Value } });

        if (!await _shg.RejectAsync(app.Id, reason, User.Identity?.Name, DateTime.UtcNow))
            return ErrorResult("request", _L["ShgValCannotReject"].Value);
        return Ok(new { message = string.Format(_L["ShgRejectedMsg"].Value, app.ApplicationRef ?? "—", app.ShgName) });
    }

    // ── Delete ────────────────────────────────────────────────────

    /// <summary>
    /// Deletes an application of any status that holds no stall (a stall must be vacated in Stall Management first),
    /// with its uploaded photos and documents. A District Officer can only delete in their own district.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var app = await FindInScopeAsync(id);
        if (app == null) return NotFound();
        if (await _shg.StallOfAsync(app.Id) is { } held)
            return ErrorResult("request", string.Format(_L["ShgValDeleteHasStall"].Value, held.StallCode));

        if (!await _shg.DeleteAsync(app.Id)) return ErrorResult("request", _L["ShgValCannotDelete"].Value);

        // the files live in the website's upload folder under {PublicId}; nothing can reach them once the row is gone
        var folder = Path.Combine(_uploadRoot, app.PublicId.ToString("N"));
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* the row is deleted; a locked file is left behind */ }

        return Ok(new { message = string.Format(_L["ShgDeletedMsg"].Value, app.ApplicationRef ?? "—", app.ShgName) });
    }

    private async Task<ShgDetail?> FindInScopeAsync(Guid id)
    {
        var scope = await ScopeAsync();
        return scope.SeesNothing ? null : await _shg.FindAsync(id, scope.Filter);
    }

    /// <summary>Status codes sent to the page (and its filter).</summary>
    internal static class ShgStatus
    {
        public const string Incomplete = "incomplete", InProcess = "inprocess", Submitted = "submitted", Rejected = "rejected";
    }

    /// <summary>
    /// Incomplete: started on the website but not submitted. In process: submitted, waiting for a decision.
    /// Submitted: a stall has been allotted to it. Rejected: an officer rejected it (submitted or incomplete).
    /// </summary>
    internal static string Status(ShgDetail a, bool hasStall) =>
        hasStall ? ShgStatus.Submitted
        : a.IsRejected ? ShgStatus.Rejected
        : !a.IsSubmitted ? ShgStatus.Incomplete
        : ShgStatus.InProcess;

    /// <summary>A stall can be allotted only while the application is in process.</summary>
    internal static bool CanAllocate(ShgDetail a, bool hasStall) => Status(a, hasStall) == ShgStatus.InProcess;

    /// <summary>An application can be rejected while in process or still incomplete.</summary>
    internal static bool CanReject(ShgDetail a, bool hasStall) => Status(a, hasStall) is ShgStatus.InProcess or ShgStatus.Incomplete;

    private string StatusLabel(ShgDetail a, bool hasStall) => _L[Status(a, hasStall) switch
    {
        ShgStatus.Incomplete => "ShgStatusIncomplete",
        ShgStatus.InProcess => "ShgStatusInProcess",
        ShgStatus.Rejected => "RejectedLabel",
        _ => "ShgSubmitted"
    }].Value;

    private string? DistrictLabel(string? name) =>
        DistrictNames.ResourceKeyFor(name) is { } key ? _L[key].Value : name;
}
