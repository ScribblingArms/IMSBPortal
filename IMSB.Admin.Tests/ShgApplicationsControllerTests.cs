using System.Security.Claims;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace IMSB.Admin.Tests;

/// <summary>SHG Applications: officers read the website's stall applications (SHGDetails); District Officers only their own district.</summary>
public class ShgApplicationsControllerTests : IDisposable
{
    // the Districts table spells two districts differently from the officers' list (TelanganaDistricts.All)
    private const int Hyderabad = 4, Medchal = 17, RangaReddy = 26;
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), "shg-admin-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_uploads, true); } catch (DirectoryNotFoundException) { }
    }

    private sealed class TestEnv : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "IMSB.Admin";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }

    private static IMSBContext Context()
    {
        var context = TestHelpers.CreateContext();
        context.Districts.AddRange(
            new District { Id = Hyderabad, Name = "Hyderabad" },
            new District { Id = Medchal, Name = "Medchal–Malkajgiri" },
            new District { Id = RangaReddy, Name = "Ranga Reddy" });
        context.SaveChanges();
        return context;
    }

    private static ShgDetail Add(IMSBContext context, string shgName, int districtId, bool submitted = true)
    {
        var row = new ShgDetail
        {
            ShgName = shgName, ShgId = "TG" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), VillageOrganisation = "VO", MandalSamakhya = "MS",
            DistrictId = districtId, Mandal = "Mandal of " + shgName, Village = "Village", MemberCount = 12,
            RepName = "Rep " + shgName, RepRole = "President", RepMobile = "9876543210", RepAddress = "Addr", RepPinCode = "500001", RepSavedAt = DateTime.UtcNow,
            ProductType = "Sarees", ProductDescription = "Handwoven ikat sarees", PriceMin = 250.5m, PriceMax = 900m, ProductPhotos = "a1.jpg|b2.png", ProductSavedAt = DateTime.UtcNow
        };
        if (submitted)
        {
            row.StallDuration = "1 month"; row.StallStartMonth = new DateTime(2027, 1, 1); row.StallFacilities = "Electricity|Storage space";
            row.PreviousExhibition = false; row.StallSavedAt = DateTime.UtcNow;
            row.ShgDocument = "doc.pdf"; row.DeclarationTrue = row.DeclarationConsent = true; row.DeclarationSavedAt = DateTime.UtcNow;
            row.SubmittedAt = DateTime.UtcNow; row.ApplicationRef = "IMSB-2026-" + Random.Shared.Next(10000, 99999);
        }
        context.ShgDetails.Add(row);
        context.SaveChanges();
        return row;
    }

    private static Login Officer(IMSBContext context, string role, string? district)
    {
        var login = new Login { Username = role + Guid.NewGuid().ToString("N")[..6], PasswordHash = "x", Role = role, District = district };
        context.Logins.Add(login);
        context.SaveChanges();
        return login;
    }

    private ShgApplicationsController Controller(IMSBContext context, string role, int loginId = 0)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:ShgPhotosPath"] = _uploads }).Build();
        return new ShgApplicationsController(new IMSB.Backend.ShgApplications(context), TestHelpers.LocalizerFactory(), config, new TestEnv())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, loginId.ToString()),
                        new Claim(ClaimTypes.Name, "user@imsb"),
                        new Claim(ClaimTypes.Role, role)
                    }, "Test"))
                }
            }
        };
    }

    private static List<string?> Names(System.Text.Json.JsonElement data) =>
        data.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("shgName").GetString()).OrderBy(n => n).ToList();

    [Fact]
    public void The_page_is_open_to_admin_and_both_officer_roles()
    {
        var roles = typeof(ShgApplicationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles;
        Assert.Equal(new[] { "Admin", "DistrictOfficer", "StateAdminOfficer" }, roles!.Split(','));
    }

    [Theory]
    [InlineData("Medchal-Malkajgiri", Medchal, "Medchal SHG")]   // hyphen on the login, en dash in the Districts table
    [InlineData("Rangareddy", RangaReddy, "Ranga SHG")]          // one word on the login, two in the table
    [InlineData("hyderabad", Hyderabad, "Hyderabad SHG")]
    public async Task A_district_officer_sees_only_their_own_district(string loginDistrict, int districtId, string expected)
    {
        using var context = Context();
        Add(context, "Hyderabad SHG", Hyderabad);
        Add(context, "Medchal SHG", Medchal);
        Add(context, "Ranga SHG", RangaReddy);
        var officer = Officer(context, UserTypes.DistrictOfficer, loginDistrict);

        var data = TestHelpers.OkJson(await Controller(context, UserTypes.DistrictOfficer, officer.Id).Data());

        Assert.Equal(new[] { expected }, Names(data));
        Assert.Equal(districtId, data.GetProperty("rows")[0].GetProperty("districtId").GetInt32());
        Assert.False(data.GetProperty("scope").GetProperty("allDistricts").GetBoolean());
        Assert.Empty(data.GetProperty("districts").EnumerateArray());   // no district filter for them
    }

    [Theory]
    [InlineData(UserTypes.StateAdminOfficer)]
    [InlineData("Admin")]
    public async Task State_admin_officers_and_admin_see_every_district(string role)
    {
        using var context = Context();
        Add(context, "Hyderabad SHG", Hyderabad);
        Add(context, "Medchal SHG", Medchal);
        Add(context, "Draft SHG", RangaReddy, submitted: false);
        var login = Officer(context, role, "Hyderabad");   // a district on the login does not narrow these roles

        var data = TestHelpers.OkJson(await Controller(context, role, login.Id).Data());

        Assert.Equal(new[] { "Draft SHG", "Hyderabad SHG", "Medchal SHG" }, Names(data));
        Assert.True(data.GetProperty("scope").GetProperty("allDistricts").GetBoolean());
        Assert.Equal(3, data.GetProperty("districts").GetArrayLength());
        var stats = data.GetProperty("stats");
        Assert.Equal(3, stats.GetProperty("total").GetInt32());
        Assert.Equal(2, stats.GetProperty("inProcess").GetInt32());     // submitted, no stall yet
        Assert.Equal(0, stats.GetProperty("submitted").GetInt32());     // stall allotted
        Assert.Equal(1, stats.GetProperty("incomplete").GetInt32());    // not submitted on the website
    }

    [Fact]
    public async Task Status_is_in_process_until_a_stall_is_allotted_then_submitted()
    {
        using var context = Context();
        var draft = Add(context, "Draft SHG", Hyderabad, submitted: false);
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        AddStall(context, "A-01");
        var controller = Controller(context, "Admin");

        string? StatusOf(System.Text.Json.JsonElement data, string name) =>
            data.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("shgName").GetString() == name).GetProperty("statusLabel").GetString();

        var before = TestHelpers.OkJson(await controller.Data());
        Assert.Equal("Incomplete", StatusOf(before, "Draft SHG"));
        Assert.Equal("In process", StatusOf(before, "Hyderabad SHG"));
        Assert.Equal("inprocess", TestHelpers.OkJson(await controller.Get(app.PublicId)).GetProperty("status").GetString());

        TestHelpers.OkJson(await controller.Allocate(app.PublicId, Allocation()));

        var after = TestHelpers.OkJson(await controller.Data());
        Assert.Equal("Submitted", StatusOf(after, "Hyderabad SHG"));
        Assert.Equal("Incomplete", StatusOf(after, "Draft SHG"));
        Assert.Equal(1, after.GetProperty("stats").GetProperty("submitted").GetInt32());
        Assert.Equal(0, after.GetProperty("stats").GetProperty("inProcess").GetInt32());
        var details = TestHelpers.OkJson(await controller.Get(app.PublicId));
        Assert.Equal("submitted", details.GetProperty("status").GetString());
        Assert.Equal("Submitted", details.GetProperty("statusLabel").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Atlantis")]
    public async Task A_district_officer_without_a_known_district_sees_nothing(string? district)
    {
        using var context = Context();
        var row = Add(context, "Hyderabad SHG", Hyderabad);
        var officer = Officer(context, UserTypes.DistrictOfficer, district);
        var controller = Controller(context, UserTypes.DistrictOfficer, officer.Id);

        var data = TestHelpers.OkJson(await controller.Data());

        Assert.Empty(data.GetProperty("rows").EnumerateArray());
        Assert.True(data.GetProperty("scope").GetProperty("seesNothing").GetBoolean());
        Assert.IsType<NotFoundResult>(await controller.Get(row.PublicId));
    }

    [Fact]
    public async Task A_district_officer_cannot_open_another_districts_application_or_files()
    {
        using var context = Context();
        var other = Add(context, "Medchal SHG", Medchal);
        var officer = Officer(context, UserTypes.DistrictOfficer, "Hyderabad");
        var controller = Controller(context, UserTypes.DistrictOfficer, officer.Id);

        Assert.IsType<NotFoundResult>(await controller.Get(other.PublicId));
        Assert.IsType<NotFoundResult>(await controller.File(other.PublicId, "a1.jpg"));
    }

    [Fact]
    public async Task Details_show_every_saved_step_and_null_for_steps_not_saved()
    {
        using var context = Context();
        var submitted = Add(context, "Hyderabad SHG", Hyderabad);
        var draft = Add(context, "Draft SHG", Hyderabad, submitted: false);
        var controller = Controller(context, UserTypes.StateAdminOfficer);

        var full = TestHelpers.OkJson(await controller.Get(submitted.PublicId));
        Assert.Equal(submitted.ApplicationRef, full.GetProperty("reference").GetString());
        Assert.Equal("Mandal of Hyderabad SHG", full.GetProperty("shg").GetProperty("mandal").GetString());
        Assert.Equal("Hyderabad", full.GetProperty("shg").GetProperty("districtLabel").GetString());
        Assert.Equal("Rep Hyderabad SHG", full.GetProperty("representative").GetProperty("name").GetString());
        Assert.Equal(2, full.GetProperty("product").GetProperty("photos").GetArrayLength());
        Assert.Equal("2027-01", full.GetProperty("stall").GetProperty("startMonth").GetString());
        Assert.Equal(new[] { "Electricity", "Storage space" }, full.GetProperty("stall").GetProperty("facilities").EnumerateArray().Select(f => f.GetString()));
        var doc = full.GetProperty("declaration").GetProperty("shgDocument");
        Assert.True(doc.GetProperty("isPdf").GetBoolean());
        Assert.Equal($"/ShgApplications/File/{submitted.PublicId}?name=doc.pdf", doc.GetProperty("url").GetString());

        var partial = TestHelpers.OkJson(await controller.Get(draft.PublicId));
        Assert.False(partial.GetProperty("submitted").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, partial.GetProperty("stall").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, partial.GetProperty("declaration").ValueKind);
        Assert.Equal(new[] { 4, 5 }, partial.GetProperty("missingSteps").EnumerateArray().Select(s => s.GetInt32()));
    }

    [Fact]
    public async Task Files_are_served_only_when_stored_on_the_application()
    {
        using var context = Context();
        var row = Add(context, "Hyderabad SHG", Hyderabad);
        var folder = Path.Combine(_uploads, row.PublicId.ToString("N"));
        Directory.CreateDirectory(folder);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, "a1.jpg"), new byte[] { 0xFF, 0xD8, 0xFF, 1 });
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, "secret.jpg"), new byte[] { 0xFF, 0xD8, 0xFF, 2 });
        var controller = Controller(context, UserTypes.StateAdminOfficer);

        var photo = Assert.IsType<PhysicalFileResult>(await controller.File(row.PublicId, "a1.jpg"));
        Assert.Equal("image/jpeg", photo.ContentType);

        Assert.IsType<NotFoundResult>(await controller.File(row.PublicId, "secret.jpg"));          // on disk but not on the row
        Assert.IsType<NotFoundResult>(await controller.File(row.PublicId, "../../appsettings.json"));
        Assert.IsType<NotFoundResult>(await controller.File(row.PublicId, "b2.png"));               // on the row but not on disk
        Assert.IsType<NotFoundResult>(await controller.File(Guid.NewGuid(), "a1.jpg"));
    }

    [Fact]
    public async Task Export_holds_only_the_officers_district()
    {
        using var context = Context();
        Add(context, "Hyderabad SHG", Hyderabad);
        Add(context, "Medchal SHG", Medchal);
        var officer = Officer(context, UserTypes.DistrictOfficer, "Hyderabad");

        var file = Assert.IsType<FileContentResult>(await Controller(context, UserTypes.DistrictOfficer, officer.Id).Export());
        var text = System.Text.Encoding.UTF8.GetString(file.FileContents);

        Assert.Contains("Hyderabad SHG", text);
        Assert.DoesNotContain("Medchal SHG", text);
    }

    // ── Allocate Stall: the stall goes to the SHG itself (stalls.ShgDetailId, IsArtisan false) ──

    private static Stall AddStall(IMSBContext context, string code, string status = StallStatuses.Vacant, decimal? rent = 2000)
    {
        var stall = new Stall { StallCode = code, Name = "Stall " + code, Block = code[..1], Status = status, MonthlyRent = rent };
        context.Stalls.Add(stall);
        context.SaveChanges();
        return stall;
    }

    private static ShgAllocateStallDto Allocation(string stall = "A-01", string? date = "2026-11-14", string? end = null, decimal? rent = 3500) =>
        new() { StallCode = stall, AllottedDate = date ?? "", AgreementEndDate = end, MonthlyRent = rent, Remarks = "  Near the  gate " };

    private static string? Error(IActionResult result, string field) =>
        System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(Assert.IsType<BadRequestObjectResult>(result).Value))
            .RootElement.GetProperty("errors").TryGetProperty(field, out var e) ? e.GetString() : null;

    [Fact]
    public async Task Allocating_gives_the_stall_to_the_shg_with_its_category_and_no_artisan()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        app.ProductCategoryId = 7;
        context.SaveChanges();
        AddStall(context, "A-01");
        var controller = Controller(context, UserTypes.StateAdminOfficer);

        var result = TestHelpers.OkJson(await controller.Allocate(app.PublicId, Allocation(end: "2027-11-13")));

        Assert.Equal("A-01", result.GetProperty("code").GetString());
        var stall = context.Stalls.AsNoTracking().Single();
        Assert.False(stall.IsArtisan);   // IsArtisan is true only for a stall held by an artisan
        Assert.Equal(app.Id, stall.ShgDetailId);
        Assert.Null(stall.ArtisanId);
        Assert.Equal(StallStatuses.Occupied, stall.Status);
        Assert.Equal(7, stall.CategoryId);                         // from the application
        Assert.Equal(new DateTime(2026, 11, 14), stall.AllottedDate);
        Assert.Equal(new DateTime(2027, 11, 13), stall.AgreementEndDate);
        Assert.Equal(3500m, stall.MonthlyRent);
        Assert.Equal("Near the gate", stall.Remarks);
        Assert.Equal("user@imsb", stall.UpdatedBy);

        // the application now shows its stall and offers no second allocation
        var details = TestHelpers.OkJson(await controller.Get(app.PublicId));
        Assert.False(details.GetProperty("canAllocate").GetBoolean());
        Assert.Equal("A-01", details.GetProperty("allottedStall").GetProperty("code").GetString());
        var row = TestHelpers.OkJson(await controller.Data()).GetProperty("rows")[0];
        Assert.Equal("A-01", row.GetProperty("stallCode").GetString());
    }

    [Fact]
    public async Task Without_a_rent_the_stalls_own_rent_is_kept()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        AddStall(context, "A-01", rent: 2000);

        TestHelpers.OkJson(await Controller(context, "Admin").Allocate(app.PublicId, Allocation(rent: null)));

        Assert.Equal(2000m, context.Stalls.AsNoTracking().Single().MonthlyRent);
    }

    [Fact]
    public async Task Only_a_submitted_application_without_a_stall_can_be_given_one()
    {
        using var context = Context();
        var draft = Add(context, "Draft SHG", Hyderabad, submitted: false);
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        AddStall(context, "A-01");
        AddStall(context, "A-02");
        var controller = Controller(context, UserTypes.StateAdminOfficer);

        Assert.Equal("Only a submitted application can be given a stall.", Error(await controller.Allocate(draft.PublicId, Allocation()), "request"));
        Assert.False(TestHelpers.OkJson(await controller.Get(draft.PublicId)).GetProperty("canAllocate").GetBoolean());

        TestHelpers.OkJson(await controller.Allocate(app.PublicId, Allocation("A-01")));
        Assert.Equal("This SHG already holds stall A-01.", Error(await controller.Allocate(app.PublicId, Allocation("A-02")), "request"));
        Assert.Equal(StallStatuses.Vacant, context.Stalls.AsNoTracking().Single(s => s.StallCode == "A-02").Status);
    }

    [Theory]
    [InlineData("A-09", "2026-11-14", null, null, "stall_code")]          // no such stall
    [InlineData("B-01", "2026-11-14", null, null, "stall_code")]          // reserved, not vacant
    [InlineData("A-01", "14-11-2026", null, null, "allotted_date")]
    [InlineData("A-01", "2026-11-14", "2026-11-14", null, "agreement_end_date")]   // must end after the allotment
    [InlineData("A-01", "2026-11-14", null, -5.0, "monthly_rent")]
    public async Task Invalid_allocations_are_refused_and_change_nothing(string stall, string date, string? end, double? rent, string field)
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        AddStall(context, "A-01");
        AddStall(context, "B-01", StallStatuses.Reserved);

        var result = await Controller(context, "Admin").Allocate(app.PublicId, Allocation(stall, date, end, rent == null ? 3500 : (decimal)rent));

        Assert.NotNull(Error(result, field));
        Assert.All(context.Stalls.AsNoTracking(), s => Assert.Null(s.ShgDetailId));
    }

    [Fact]
    public async Task A_district_officer_can_allocate_only_in_their_own_district()
    {
        using var context = Context();
        var own = Add(context, "Hyderabad SHG", Hyderabad);
        var other = Add(context, "Medchal SHG", Medchal);
        AddStall(context, "A-01");
        AddStall(context, "A-02");
        var officer = Officer(context, UserTypes.DistrictOfficer, "Hyderabad");
        var controller = Controller(context, UserTypes.DistrictOfficer, officer.Id);

        Assert.IsType<NotFoundResult>(await controller.Allocate(other.PublicId, Allocation("A-01")));
        TestHelpers.OkJson(await controller.Allocate(own.PublicId, Allocation("A-02")));

        Assert.Equal(own.Id, context.Stalls.AsNoTracking().Single(s => s.ShgDetailId != null).ShgDetailId);
    }

    [Fact]
    public async Task The_popup_lists_only_vacant_stalls()
    {
        using var context = Context();
        AddStall(context, "A-01");
        AddStall(context, "A-02", StallStatuses.Maintenance);
        AddStall(context, "B-01");

        var stalls = TestHelpers.OkJson(await Controller(context, "Admin").VacantStalls());

        Assert.Equal(new[] { "A-01", "B-01" }, stalls.EnumerateArray().Select(s => s.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Stall_management_shows_the_shg_and_frees_its_stall_only_by_vacating()
    {
        using var context = Context();
        var app = Add(context, "Lakshmi SHG", Hyderabad);
        AddStall(context, "A-01");
        TestHelpers.OkJson(await Controller(context, "Admin").Allocate(app.PublicId, Allocation()));
        var stalls = BazaarTestHelpers.Stalls(context);

        // listed with the SHG as the holder, and found by its name
        var data = TestHelpers.OkJson(await stalls.Data("lakshmi", null, null, null));
        var item = Assert.Single(data.GetProperty("stalls").EnumerateArray());
        Assert.Equal("Lakshmi SHG", item.GetProperty("artisanName").GetString());
        Assert.Equal(app.ApplicationRef, item.GetProperty("artisanCode").GetString());

        // it is occupied: cannot be deleted, set vacant by editing, or reallocated between artisans
        Assert.IsType<BadRequestObjectResult>(await stalls.Delete("A-01"));
        Assert.NotNull(Error(await stalls.Update("A-01", new StallDto { Name = "Stall A-01", Block = "A", Status = StallStatuses.Vacant }), "status"));
        Assert.Equal("This stall is allotted to an SHG. Vacate it first to give it to an artisan.",
            Error(await stalls.RequestReallocation(BazaarTestHelpers.Reallocation("A-01", newArtisan: null)), "stall_code"));

        Assert.IsType<OkObjectResult>(await stalls.Vacate("A-01"));
        var freed = context.Stalls.AsNoTracking().Single();
        Assert.Equal(StallStatuses.Vacant, freed.Status);
        Assert.False(freed.IsArtisan);
        Assert.Null(freed.ShgDetailId);
        Assert.Null(freed.AllottedDate);

        // and the application can be given a stall again
        Assert.True(TestHelpers.OkJson(await Controller(context, "Admin").Get(app.PublicId)).GetProperty("canAllocate").GetBoolean());
    }

    // ── Reject: a submitted application without a stall, with a reason (RejectedAt / RejectedBy / RejectionReason) ──

    private static ShgRejectDto Rejection(string? reason = "  Products  not handmade ") => new() { Reason = reason! };

    private static string? RequestError(IActionResult result) => Error(result, "request");

    [Fact]
    public async Task Rejecting_saves_the_reason_who_and_when_and_shows_it_as_rejected()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        var controller = Controller(context, UserTypes.StateAdminOfficer);
        var before = DateTime.UtcNow;

        var result = TestHelpers.OkJson(await controller.Reject(app.PublicId, Rejection()));

        Assert.Equal($"Application {app.ApplicationRef} of Hyderabad SHG was rejected.", result.GetProperty("message").GetString());
        var saved = context.ShgDetails.AsNoTracking().Single();
        Assert.Equal("Products not handmade", saved.RejectionReason);   // trimmed, inner spaces collapsed
        Assert.Equal("user@imsb", saved.RejectedBy);
        Assert.NotNull(saved.RejectedAt);
        Assert.InRange(saved.RejectedAt!.Value, before, DateTime.UtcNow);
        Assert.Equal(saved.RejectedAt, saved.UpdatedAt);

        var details = TestHelpers.OkJson(await controller.Get(app.PublicId));
        Assert.Equal("rejected", details.GetProperty("status").GetString());
        Assert.Equal("Rejected", details.GetProperty("statusLabel").GetString());
        Assert.False(details.GetProperty("canReject").GetBoolean());
        Assert.False(details.GetProperty("canAllocate").GetBoolean());
        var rejection = details.GetProperty("rejection");
        Assert.Equal("Products not handmade", rejection.GetProperty("reason").GetString());
        Assert.Equal("user@imsb", rejection.GetProperty("rejectedBy").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.String, rejection.GetProperty("rejectedAt").ValueKind);
    }

    [Fact]
    public async Task An_application_in_process_offers_reject_and_has_no_rejection()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);

        var details = TestHelpers.OkJson(await Controller(context, "Admin").Get(app.PublicId));

        Assert.True(details.GetProperty("canReject").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, details.GetProperty("rejection").ValueKind);
    }

    [Fact]
    public async Task Rejected_applications_are_counted_and_exported_with_their_reason()
    {
        using var context = Context();
        var rejected = Add(context, "Rejected SHG", Hyderabad);
        Add(context, "Waiting SHG", Hyderabad);
        var controller = Controller(context, "Admin");
        TestHelpers.OkJson(await controller.Reject(rejected.PublicId, Rejection("Duplicate application")));

        var data = TestHelpers.OkJson(await controller.Data());
        var stats = data.GetProperty("stats");
        Assert.Equal(1, stats.GetProperty("rejected").GetInt32());
        Assert.Equal(1, stats.GetProperty("inProcess").GetInt32());
        var row = data.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("shgName").GetString() == "Rejected SHG");
        Assert.Equal("rejected", row.GetProperty("status").GetString());
        Assert.Equal("Duplicate application", row.GetProperty("rejectionReason").GetString());   // shown under the badge in the list
        var waiting = data.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("shgName").GetString() == "Waiting SHG");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, waiting.GetProperty("rejectionReason").ValueKind);

        var file = Assert.IsType<FileContentResult>(await controller.Export());
        Assert.Contains("Duplicate application", System.Text.Encoding.UTF8.GetString(file.FileContents));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public async Task A_reason_is_required(string? reason)
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);

        var result = await Controller(context, "Admin").Reject(app.PublicId, Rejection(reason));

        Assert.Equal("Enter the reason for rejection (up to 500 characters)", Error(result, "reason"));
        Assert.Null(context.ShgDetails.AsNoTracking().Single().RejectedAt);
    }

    [Fact]
    public async Task A_missing_body_is_refused_like_an_empty_reason()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);

        Assert.NotNull(Error(await Controller(context, "Admin").Reject(app.PublicId, null), "reason"));
        Assert.Null(context.ShgDetails.AsNoTracking().Single().RejectedAt);
    }

    [Fact]
    public async Task The_reason_may_be_up_to_500_characters()
    {
        using var context = Context();
        var exact = Add(context, "Exact SHG", Hyderabad);
        var tooLong = Add(context, "Long SHG", Hyderabad);
        var controller = Controller(context, "Admin");

        TestHelpers.OkJson(await controller.Reject(exact.PublicId, Rejection(new string('x', ShgDetail.RejectionReasonMaxLength))));
        Assert.NotNull(Error(await controller.Reject(tooLong.PublicId, Rejection(new string('x', ShgDetail.RejectionReasonMaxLength + 1))), "reason"));

        Assert.Equal(500, context.ShgDetails.AsNoTracking().Single(s => s.Id == exact.Id).RejectionReason!.Length);
        Assert.Null(context.ShgDetails.AsNoTracking().Single(s => s.Id == tooLong.Id).RejectedAt);
    }

    [Fact]
    public async Task An_incomplete_application_can_be_rejected_but_not_given_a_stall()
    {
        using var context = Context();
        var draft = Add(context, "Draft SHG", Hyderabad, submitted: false);
        AddStall(context, "A-01");
        var controller = Controller(context, "Admin");

        var before = TestHelpers.OkJson(await controller.Get(draft.PublicId));
        Assert.Equal("incomplete", before.GetProperty("status").GetString());
        Assert.True(before.GetProperty("canReject").GetBoolean());
        Assert.False(before.GetProperty("canAllocate").GetBoolean());

        var result = TestHelpers.OkJson(await controller.Reject(draft.PublicId, Rejection("Documents never uploaded")));
        Assert.Equal("Application — of Draft SHG was rejected.", result.GetProperty("message").GetString());   // no reference yet

        var after = TestHelpers.OkJson(await controller.Get(draft.PublicId));
        Assert.Equal("rejected", after.GetProperty("status").GetString());   // rejected wins over incomplete
        Assert.False(after.GetProperty("canReject").GetBoolean());
        Assert.False(after.GetProperty("canAllocate").GetBoolean());
        Assert.Equal("Documents never uploaded", after.GetProperty("rejection").GetProperty("reason").GetString());
        Assert.Equal(1, TestHelpers.OkJson(await controller.Data()).GetProperty("stats").GetProperty("rejected").GetInt32());
        Assert.Equal(0, TestHelpers.OkJson(await controller.Data()).GetProperty("stats").GetProperty("incomplete").GetInt32());
        Assert.NotNull(RequestError(await controller.Allocate(draft.PublicId, Allocation())));
    }

    [Fact]
    public async Task An_application_cannot_be_rejected_twice_and_the_first_reason_is_kept()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        var controller = Controller(context, "Admin");
        TestHelpers.OkJson(await controller.Reject(app.PublicId, Rejection("First reason")));

        Assert.Equal("This application has already been rejected.", RequestError(await controller.Reject(app.PublicId, Rejection("Second reason"))));
        Assert.Equal("First reason", context.ShgDetails.AsNoTracking().Single().RejectionReason);
    }

    [Fact]
    public async Task An_application_holding_a_stall_cannot_be_rejected()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        AddStall(context, "A-01");
        var controller = Controller(context, "Admin");
        TestHelpers.OkJson(await controller.Allocate(app.PublicId, Allocation()));

        Assert.Equal("This SHG already holds stall A-01.", RequestError(await controller.Reject(app.PublicId, Rejection())));
        // the page shows Reject disabled (canReject false) because a stall is allotted, and no Allocate Stall
        var details = TestHelpers.OkJson(await controller.Get(app.PublicId));
        Assert.False(details.GetProperty("canReject").GetBoolean());
        Assert.False(details.GetProperty("canAllocate").GetBoolean());
        Assert.Equal("A-01", details.GetProperty("allottedStall").GetProperty("code").GetString());
        Assert.Null(context.ShgDetails.AsNoTracking().Single().RejectedAt);
    }

    [Fact]
    public async Task A_rejected_application_cannot_be_given_a_stall()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        AddStall(context, "A-01");
        var controller = Controller(context, "Admin");
        TestHelpers.OkJson(await controller.Reject(app.PublicId, Rejection()));

        // the page hides Allocate Stall (canAllocate false) and there is no stall to show
        var details = TestHelpers.OkJson(await controller.Get(app.PublicId));
        Assert.False(details.GetProperty("canAllocate").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, details.GetProperty("allottedStall").ValueKind);
        Assert.Equal("This application was rejected and cannot be given a stall.", RequestError(await controller.Allocate(app.PublicId, Allocation())));
        var stall = context.Stalls.AsNoTracking().Single();
        Assert.Equal(StallStatuses.Vacant, stall.Status);
        Assert.Null(stall.ShgDetailId);
    }

    [Fact]
    public async Task A_district_officer_can_reject_only_in_their_own_district()
    {
        using var context = Context();
        var own = Add(context, "Hyderabad SHG", Hyderabad);
        var other = Add(context, "Medchal SHG", Medchal);
        var officer = Officer(context, UserTypes.DistrictOfficer, "Hyderabad");
        var controller = Controller(context, UserTypes.DistrictOfficer, officer.Id);

        Assert.IsType<NotFoundResult>(await controller.Reject(other.PublicId, Rejection()));
        TestHelpers.OkJson(await controller.Reject(own.PublicId, Rejection()));

        Assert.Null(context.ShgDetails.AsNoTracking().Single(s => s.Id == other.Id).RejectedAt);
        Assert.NotNull(context.ShgDetails.AsNoTracking().Single(s => s.Id == own.Id).RejectedAt);
    }

    [Fact]
    public async Task Rejecting_an_unknown_application_is_not_found()
    {
        using var context = Context();
        Assert.IsType<NotFoundResult>(await Controller(context, "Admin").Reject(Guid.NewGuid(), Rejection()));
    }

    [Fact]
    public void Reject_requires_post_and_an_antiforgery_token()
    {
        var method = typeof(ShgApplicationsController).GetMethod(nameof(ShgApplicationsController.Reject))!;
        Assert.NotNull(method.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
    }

    // ── Delete: any status, not while holding a stall; the uploaded files go too ──

    [Theory]
    [InlineData(false, false)]   // incomplete
    [InlineData(true, false)]    // in process
    [InlineData(true, true)]     // rejected
    public async Task An_application_without_a_stall_can_be_deleted_with_its_files(bool submitted, bool rejected)
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad, submitted);
        var keep = Add(context, "Other SHG", Hyderabad);
        var controller = Controller(context, "Admin");
        if (rejected) TestHelpers.OkJson(await controller.Reject(app.PublicId, Rejection()));
        var folder = Path.Combine(_uploads, app.PublicId.ToString("N"));
        Directory.CreateDirectory(folder);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, "a1.jpg"), new byte[] { 1 });

        var result = TestHelpers.OkJson(await controller.Delete(app.PublicId));

        Assert.Contains("Hyderabad SHG", result.GetProperty("message").GetString());
        Assert.Equal(keep.Id, context.ShgDetails.AsNoTracking().Single().Id);
        Assert.False(Directory.Exists(folder));
        Assert.IsType<NotFoundResult>(await controller.Get(app.PublicId));
    }

    [Fact]
    public async Task An_application_holding_a_stall_cannot_be_deleted_until_the_stall_is_vacated()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        AddStall(context, "A-01");
        var controller = Controller(context, "Admin");
        TestHelpers.OkJson(await controller.Allocate(app.PublicId, Allocation()));

        Assert.Equal("This SHG holds stall A-01. Vacate it in Stall Management before deleting the application.",
            RequestError(await controller.Delete(app.PublicId)));
        Assert.Single(context.ShgDetails.AsNoTracking());

        Assert.IsType<OkObjectResult>(await BazaarTestHelpers.Stalls(context).Vacate("A-01"));
        TestHelpers.OkJson(await controller.Delete(app.PublicId));
        Assert.Empty(context.ShgDetails.AsNoTracking());
    }

    [Fact]
    public async Task A_district_officer_can_delete_only_in_their_own_district()
    {
        using var context = Context();
        var own = Add(context, "Hyderabad SHG", Hyderabad);
        var other = Add(context, "Medchal SHG", Medchal);
        var officer = Officer(context, UserTypes.DistrictOfficer, "Hyderabad");
        var controller = Controller(context, UserTypes.DistrictOfficer, officer.Id);

        Assert.IsType<NotFoundResult>(await controller.Delete(other.PublicId));
        TestHelpers.OkJson(await controller.Delete(own.PublicId));

        Assert.Equal(other.Id, context.ShgDetails.AsNoTracking().Single().Id);
    }

    [Fact]
    public async Task Deleting_an_unknown_application_is_not_found()
    {
        using var context = Context();
        Assert.IsType<NotFoundResult>(await Controller(context, "Admin").Delete(Guid.NewGuid()));
    }

    [Fact]
    public async Task The_backend_refuses_to_delete_when_a_stall_was_allotted_meanwhile()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        var stall = AddStall(context, "A-01", StallStatuses.Occupied);
        stall.ShgDetailId = app.Id;
        context.SaveChanges();
        var backend = new IMSB.Backend.ShgApplications(context);

        Assert.False(await backend.DeleteAsync(app.Id));
        Assert.False(await backend.DeleteAsync(-1));
        Assert.Single(context.ShgDetails.AsNoTracking());
    }

    private static string AdminFile(params string[] parts) => System.IO.File.ReadAllText(Path.Combine(new[] {
        typeof(ShgApplicationsControllerTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a => a.Key == "AdminProjectDir").Value! }.Concat(parts).ToArray()));

    [Fact]
    public void Hidden_buttons_stay_hidden()
    {
        // .btn sets display:inline-flex, which beats the browser's [hidden] rule; without this the details page showed
        // Allocate Stall and Reject for every status
        Assert.Contains("[hidden] { display: none !important; }", AdminFile("wwwroot", "css", "style.css"));
    }

    [Fact]
    public void The_page_sends_the_strings_its_buttons_use()
    {
        var view = AdminFile("Views", "ShgApplications", "Index.cshtml");
        foreach (var key in new[] { "Delete", "ShgConfirmDelete", "ShgValDeleteHasStall", "ShgValHasStall" })
            Assert.Contains($"\"{key}\"", view);
    }

    [Fact]
    public void Delete_requires_post_and_an_antiforgery_token()
    {
        var method = typeof(ShgApplicationsController).GetMethod(nameof(ShgApplicationsController.Delete))!;
        Assert.NotNull(method.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
    }

    // ── Backend and model guards (a stall allotted between the page check and the save) ──

    [Fact]
    public async Task The_backend_refuses_to_reject_when_a_stall_was_allotted_meanwhile()
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        var stall = AddStall(context, "A-01", StallStatuses.Occupied);
        stall.ShgDetailId = app.Id;
        context.SaveChanges();

        Assert.False(await new IMSB.Backend.ShgApplications(context).RejectAsync(app.Id, "Late", "user@imsb", DateTime.UtcNow));
        Assert.Null(context.ShgDetails.AsNoTracking().Single().RejectedAt);
    }

    [Fact]
    public async Task The_backend_rejects_drafts_but_refuses_unknown_and_already_rejected_applications()
    {
        using var context = Context();
        var draft = Add(context, "Draft SHG", Hyderabad, submitted: false);
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        var backend = new IMSB.Backend.ShgApplications(context);

        Assert.False(await backend.RejectAsync(-1, "x", null, DateTime.UtcNow));
        Assert.True(await backend.RejectAsync(draft.Id, "x", null, DateTime.UtcNow));
        Assert.True(await backend.RejectAsync(app.Id, "first", null, DateTime.UtcNow));
        Assert.False(await backend.RejectAsync(app.Id, "second", null, DateTime.UtcNow));
        Assert.Equal("first", context.ShgDetails.AsNoTracking().Single(s => s.Id == app.Id).RejectionReason);
    }

    [Fact]
    public void The_model_rejects_a_draft_once_and_closes_it()
    {
        var draft = new ShgDetail();
        Assert.False(draft.IsClosed);
        draft.Reject("x", "officer", DateTime.UtcNow);
        Assert.True(draft.IsRejected);
        Assert.True(draft.IsClosed);
        Assert.Throws<InvalidOperationException>(() => draft.Reject("again", null, DateTime.UtcNow));
        Assert.Equal("x", draft.RejectionReason);
    }

    [Theory]
    [InlineData(false, false, false, "incomplete", false, true)]
    [InlineData(false, false, true, "rejected", false, false)]   // rejected before it was submitted
    [InlineData(true, false, false, "inprocess", true, true)]
    [InlineData(true, true, false, "submitted", false, false)]
    [InlineData(true, false, true, "rejected", false, false)]
    [InlineData(true, true, true, "submitted", false, false)]    // holding a stall wins over an old rejection
    public void Status_and_buttons_cover_every_combination(bool submitted, bool hasStall, bool rejected, string expected, bool canAllocate, bool canReject)
    {
        var a = new ShgDetail { SubmittedAt = submitted ? DateTime.UtcNow : null, RejectedAt = rejected ? DateTime.UtcNow : null };
        Assert.Equal(expected, ShgApplicationsController.Status(a, hasStall));
        Assert.Equal(canAllocate, ShgApplicationsController.CanAllocate(a, hasStall));
        Assert.Equal(canReject, ShgApplicationsController.CanReject(a, hasStall));
    }

    [Theory]
    [InlineData("te-IN", "తిరస్కరించబడింది")]
    [InlineData("hi-IN", "अस्वीकृत")]
    [InlineData("ta-IN", "நிராகரிக்கப்பட்டது")]
    public async Task The_rejected_label_follows_the_ui_culture(string culture, string label)
    {
        using var context = Context();
        var app = Add(context, "Hyderabad SHG", Hyderabad);
        TestHelpers.OkJson(await Controller(context, "Admin").Reject(app.PublicId, Rejection()));
        using var _ = new TestHelpers.CultureScope(culture);

        var details = TestHelpers.OkJson(await Controller(context, "Admin").Get(app.PublicId));

        Assert.Equal(label, details.GetProperty("statusLabel").GetString());
    }

    // ── The website: a rejected application (even one never submitted) can no longer be changed or submitted ──

    private IMSB.Backend.ShgDetails Website(IMSBContext context)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:ShgPhotosPath"] = _uploads }).Build();
        return new IMSB.Backend.ShgDetails(context, new IMSB.Backend.ShgFileStore(config, new TestEnv()), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<IMSB.Backend.ShgDetails>.Instance);
    }

    [Fact]
    public async Task The_website_cannot_change_or_submit_a_rejected_draft()
    {
        using var context = Context();
        var draft = Add(context, "Draft SHG", Hyderabad, submitted: false);
        TestHelpers.OkJson(await Controller(context, "Admin").Reject(draft.PublicId, Rejection()));
        var website = Website(context);

        Assert.Equal(IMSB.Interfaces.ShgFormStatus.Rejected, (await website.SaveShgDetailsAsync(draft.PublicId, d => d.ShgName = "Changed")).Status);
        Assert.Equal(IMSB.Interfaces.ShgFormStatus.Rejected, (await website.SaveStepAsync(draft.PublicId, "Stall", d => d.StallSavedAt = DateTime.UtcNow)).Status);
        Assert.Equal(IMSB.Interfaces.ShgFormStatus.Rejected, (await website.SubmitAsync(draft.PublicId)).Status);

        var saved = context.ShgDetails.AsNoTracking().Single();
        Assert.Equal("Draft SHG", saved.ShgName);
        Assert.Null(saved.StallSavedAt);
        Assert.Null(saved.SubmittedAt);
    }

    [Fact]
    public async Task The_website_still_saves_a_draft_that_was_not_rejected()
    {
        using var context = Context();
        var draft = Add(context, "Draft SHG", Hyderabad, submitted: false);

        var result = await Website(context).SaveStepAsync(draft.PublicId, "Stall", d => d.StallSavedAt = DateTime.UtcNow);

        Assert.Equal(IMSB.Interfaces.ShgFormStatus.Saved, result.Status);
    }

    [Theory]
    [InlineData("te-IN", "ఎస్‌హెచ్‌జీ దరఖాస్తులు", "హైదరాబాద్")]
    [InlineData("hi-IN", "एसएचजी आवेदन", "हैदराबाद")]
    public async Task Labels_follow_the_ui_culture(string culture, string title, string hyderabad)
    {
        using var context = Context();
        Add(context, "Hyderabad SHG", Hyderabad);
        using var _ = new TestHelpers.CultureScope(culture);
        var controller = Controller(context, UserTypes.StateAdminOfficer);

        await controller.Index();
        var data = TestHelpers.OkJson(await controller.Data());

        Assert.Equal(title, controller.ViewData["Title"]);
        Assert.Equal(hyderabad, data.GetProperty("rows")[0].GetProperty("districtLabel").GetString());
    }
}
