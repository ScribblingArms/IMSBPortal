using System.Text.Json;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IMSB.Admin.Tests.ApplicationTestHelpers;
using static IMSB.Admin.Tests.BazaarTestHelpers;

namespace IMSB.Admin.Tests;

public class ApplicationsControllerTests
{
    private static ArtisanApplication Get(IMSBContext ctx, string no) => ctx.ArtisanApplications.AsNoTracking().Single(a => a.ApplicationNo == no);
    private static string Ids(IActionResult r) =>
        string.Join(",", TestHelpers.OkJson(r).GetProperty("applications").EnumerateArray().Select(a => a.GetProperty("id").GetString()));

    // ── New application ───────────────────────────────────────────

    [Fact]
    public async Task New_application_is_pending_and_does_not_store_aadhaar()
    {
        using var ctx = Seeded();
        var dto = NewApplication(mobile: "+91 91234 56780");
        var json = TestHelpers.OkJson(await Applications(ctx, "clerk").Create(dto));
        Assert.Equal("APP-0001", json.GetProperty("id").GetString());

        var a = Get(ctx, "APP-0001");
        Assert.Equal((ApplicationStatuses.Pending, "9123456780", "sridevi@example.com", DateTime.Today, "clerk", Pottery),
            (a.Status, a.Mobile, a.Email, a.SubmittedDate, a.CreatedBy, a.CategoryId));
        Assert.Equal(Aadhaar.Hash(dto.Aadhaar!), a.AadhaarHash);
        Assert.Equal("XXXX XXXX " + dto.Aadhaar![^4..], a.MaskedAadhaar);
        Assert.Null(a.ArtisanId);
        // a new application does not register anybody yet
        Assert.Equal(4, ctx.Artisans.Count());
    }

    [Fact]
    public async Task Application_numbers_follow_the_highest_existing()
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0007");
        Assert.Equal("APP-0008", TestHelpers.OkJson(await Applications(ctx).Create(NewApplication())).GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("mobile", "12345", "Enter a valid 10-digit mobile number")]
    [InlineData("mobile", "9876543210", "Mobile already registered")]          // Kamala (ART-0001)
    [InlineData("district", "Chennai", "Select a district")]
    [InlineData("gender", "Unknown", "Select a gender")]
    [InlineData("category_id", "not-a-guid", "Select a craft category")]
    [InlineData("date_of_birth", "2015-01-01", "Artisan must be at least 18 years old")]
    [InlineData("submitted_date", "2999-01-01", "Date cannot be in the future")]
    [InlineData("submitted_date", "01/02/2026", "Invalid date")]
    public async Task New_application_validation(string field, string value, string message)
    {
        using var ctx = Seeded();
        var dto = NewApplication();
        switch (field)
        {
            case "mobile": dto.Mobile = value; break;
            case "district": dto.District = value; break;
            case "gender": dto.Gender = value; break;
            case "category_id": dto.CategoryId = value; break;
            case "date_of_birth": dto.DateOfBirth = value; break;
            case "submitted_date": dto.SubmittedDate = value; break;
        }
        Assert.Equal(message, TestHelpers.Errors(await Applications(ctx).Create(dto))[field]);
        Assert.Empty(ctx.ArtisanApplications);
    }

    [Fact]
    public async Task Aadhaar_of_a_registered_artisan_is_refused()
    {
        using var ctx = Seeded();
        var dto = NewApplication();
        dto.Aadhaar = ValidAadhaar(1);   // ART-0001's number
        Assert.Equal("This Aadhaar number is already registered", TestHelpers.Errors(await Applications(ctx).Create(dto))["aadhaar"]);
    }

    [Fact]
    public async Task Aadhaar_is_required_and_checksummed()
    {
        using var ctx = Seeded();
        var dto = NewApplication();
        dto.Aadhaar = null;
        Assert.Equal("Enter a valid 12-digit Aadhaar number", TestHelpers.Errors(await Applications(ctx).Create(dto))["aadhaar"]);
        dto.Aadhaar = "234123412347";   // wrong check digit
        Assert.True(TestHelpers.Errors(await Applications(ctx).Create(dto)).ContainsKey("aadhaar"));
    }

    [Fact]
    public async Task Two_open_applications_cannot_share_mobile_or_aadhaar_but_a_rejected_applicant_may_reapply()
    {
        using var ctx = Seeded();
        var c = Applications(ctx);
        Assert.IsType<OkObjectResult>(await c.Create(NewApplication()));

        var errors = TestHelpers.Errors(await c.Create(NewApplication()));
        Assert.Equal("Another open application already uses this mobile number", errors["mobile"]);
        Assert.Equal("Another open application already uses this Aadhaar number", errors["aadhaar"]);

        Assert.IsType<OkObjectResult>(await c.Reject("APP-0001", new DecisionDto { Remarks = "Documents missing" }));
        Assert.IsType<OkObjectResult>(await c.Create(NewApplication()));
        Assert.Equal(2, ctx.ArtisanApplications.Count());
    }

    // ── Workflow ──────────────────────────────────────────────────

    [Fact]
    public async Task Process_moves_a_new_application_to_review_once()
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001");
        Assert.IsType<OkObjectResult>(await Applications(ctx, "reviewer@imsb").Process("APP-0001"));
        var a = Get(ctx, "APP-0001");
        Assert.Equal((ApplicationStatuses.UnderReview, "reviewer@imsb"), (a.Status, a.Reviewer));
        Assert.NotNull(a.ReviewStartedAt);

        Assert.Equal("Only a new application can be processed", TestHelpers.Errors(await Applications(ctx).Process("APP-0001"))["application"]);
        Assert.IsType<NotFoundResult>(await Applications(ctx).Process("APP-9999"));
    }

    [Fact]
    public async Task Approve_registers_the_artisan_and_allots_the_chosen_stall()
    {
        using var ctx = Seeded();
        var c = Applications(ctx, "approver@imsb");
        await c.Create(NewApplication());
        await c.Process("APP-0001");

        var json = TestHelpers.OkJson(await c.Approve("APP-0001", new ApproveApplicationDto { StallCode = "a-02", Remarks = "Verified" }));
        Assert.Equal(("ART-0005", "A-02"), (json.GetProperty("artisanCode").GetString(), json.GetProperty("stallCode").GetString()));

        var app = Get(ctx, "APP-0001");
        Assert.Equal((ApplicationStatuses.Approved, "ART-0005", "A-02", "approver@imsb", "Verified"),
            (app.Status, app.ArtisanCode, app.StallCode, app.DecidedBy, app.DecisionRemarks));
        Assert.NotNull(app.DecidedAt);

        var artisan = ctx.Artisans.AsNoTracking().Single(a => a.ArtisanCode == "ART-0005");
        Assert.Equal(artisan.Id, app.ArtisanId);
        Assert.Equal(("Sridevi Mallesh", "9123456780", ArtisanStatuses.Active, DateTime.Today, Pottery, app.AadhaarHash),
            (artisan.FullName, artisan.Mobile, artisan.Status, artisan.JoinedDate, artisan.CategoryId, artisan.AadhaarHash));
        Assert.Equal("Registered from application APP-0001", artisan.Remarks);

        var stall = ctx.Stalls.AsNoTracking().Single(s => s.StallCode == "A-02");
        Assert.Equal((StallStatuses.Occupied, artisan.Id, DateTime.Today, Pottery), (stall.Status, stall.ArtisanId!.Value, stall.AllottedDate!.Value, stall.CategoryId!.Value));
    }

    [Fact]
    public async Task Approve_without_a_stall_straight_from_new()
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001");
        var json = TestHelpers.OkJson(await Applications(ctx, "boss").Approve("APP-0001", null));
        Assert.Equal(JsonValueKind.Null, json.GetProperty("stallCode").ValueKind);
        var app = Get(ctx, "APP-0001");
        Assert.Equal(("boss", (string?)null), (app.Reviewer, app.StallCode));
        Assert.Equal(5, ctx.Artisans.Count());
    }

    [Theory]
    [InlineData("A-01", "Only a vacant stall can be allocated")]   // occupied
    [InlineData("B-02", "Only a vacant stall can be allocated")]   // reserved
    [InlineData("Z-99", "Select a stall")]
    public async Task Approve_needs_a_vacant_stall(string stall, string message)
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001");
        Assert.Equal(message, TestHelpers.Errors(await Applications(ctx).Approve("APP-0001", new ApproveApplicationDto { StallCode = stall }))["stall_code"]);
        Assert.Equal(ApplicationStatuses.Pending, Get(ctx, "APP-0001").Status);
        Assert.Equal(4, ctx.Artisans.Count());
    }

    [Fact]
    public async Task Approve_refuses_an_applicant_registered_in_the_meantime()
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001", mobile: "8765432109");   // Saroja's mobile, stored before she registered
        Assert.Equal("This mobile number or Aadhaar is already registered to an artisan",
            TestHelpers.Errors(await Applications(ctx).Approve("APP-0001", null))["application"]);
    }

    [Fact]
    public async Task Reject_needs_a_reason_and_a_decided_application_is_final()
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001", ApplicationStatuses.UnderReview);
        var c = Applications(ctx);
        Assert.Equal("Enter the reason for rejection", TestHelpers.Errors(await c.Reject("APP-0001", new DecisionDto { Remarks = "  " }))["remarks"]);

        Assert.IsType<OkObjectResult>(await c.Reject("APP-0001", new DecisionDto { Remarks = "Incomplete documents" }));
        var a = Get(ctx, "APP-0001");
        Assert.Equal((ApplicationStatuses.Rejected, "Incomplete documents"), (a.Status, a.DecisionRemarks));

        const string decided = "This application has already been decided";
        Assert.Equal(decided, TestHelpers.Errors(await c.Approve("APP-0001", null))["application"]);
        Assert.Equal(decided, TestHelpers.Errors(await c.Reject("APP-0001", new DecisionDto { Remarks = "again" }))["application"]);
        Assert.Equal(decided, TestHelpers.Errors(await c.Update("APP-0001", ToDto(a)))["application"]);
    }

    // ── Edit / delete ─────────────────────────────────────────────

    [Fact]
    public async Task Edit_updates_an_open_application_and_keeps_aadhaar_when_blank()
    {
        using var ctx = Seeded();
        var before = AddApplication(ctx, "APP-0001", ApplicationStatuses.UnderReview);
        var dto = ToDto(before);
        dto.FullName = "Renamed Applicant";
        dto.District = "warangal";
        Assert.IsType<OkObjectResult>(await Applications(ctx, "editor").Update("APP-0001", dto));

        var after = Get(ctx, "APP-0001");
        Assert.Equal(("Renamed Applicant", "Warangal", before.AadhaarHash, "editor", ApplicationStatuses.UnderReview),
            (after.FullName, after.District, after.AadhaarHash, after.UpdatedBy, after.Status));
        Assert.IsType<NotFoundResult>(await Applications(ctx).Update("APP-9999", dto));
    }

    [Fact]
    public async Task Delete_removes_open_and_rejected_but_keeps_approved()
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001");
        AddApplication(ctx, "APP-0002", ApplicationStatuses.Rejected, decidedAt: DateTime.UtcNow);
        AddApplication(ctx, "APP-0003", ApplicationStatuses.Approved, decidedAt: DateTime.UtcNow);
        var c = Applications(ctx);

        Assert.IsType<OkObjectResult>(await c.Delete("APP-0001"));
        Assert.IsType<OkObjectResult>(await c.Delete("APP-0002"));
        Assert.Equal("An approved application is kept as the artisan's registration record and cannot be deleted",
            TestHelpers.Errors(await c.Delete("APP-0003"))["application"]);
        Assert.Equal("APP-0003", ctx.ArtisanApplications.Single().ApplicationNo);
        Assert.IsType<NotFoundResult>(await c.Delete("APP-0001"));
    }

    [Fact]
    public async Task Deleting_the_artisan_keeps_the_approved_application_with_its_code()
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001");
        await Applications(ctx).Approve("APP-0001", null);
        Assert.IsType<OkObjectResult>(await Artisans(ctx).Delete("ART-0005"));
        var a = Get(ctx, "APP-0001");
        Assert.Equal(((Guid?)null, "ART-0005", ApplicationStatuses.Approved), (a.ArtisanId, a.ArtisanCode, a.Status));
    }

    // ── Data ──────────────────────────────────────────────────────

    [Fact]
    public async Task Data_has_stats_rows_pickers_and_vacant_stalls()
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001", submitted: DateTime.Today.AddDays(-5));
        AddApplication(ctx, "APP-0002", ApplicationStatuses.UnderReview);
        AddApplication(ctx, "APP-0003", ApplicationStatuses.Approved, decidedAt: DateTime.UtcNow);
        AddApplication(ctx, "APP-0004", ApplicationStatuses.Rejected, decidedAt: DateTime.UtcNow.AddMonths(-3));

        var c = Applications(ctx);
        Assert.Equal(new ApplicationStats(Total: 4, Approved: 1, UnderReview: 1, Pending: 1, Rejected: 1, ApprovedThisMonth: 1), await StatsOf(ctx));
        var json = TestHelpers.OkJson(await c.Data(null, null, null, null));
        var pending = json.GetProperty("applications").EnumerateArray().Single(a => a.GetProperty("id").GetString() == "APP-0001");
        Assert.Equal((5, "Textiles"), (pending.GetProperty("daysWaiting").GetInt32(), pending.GetProperty("categoryName").GetString()));
        var approved = json.GetProperty("applications").EnumerateArray().Single(a => a.GetProperty("id").GetString() == "APP-0003");
        Assert.Equal(JsonValueKind.Null, approved.GetProperty("daysWaiting").ValueKind);
        Assert.Equal(new[] { "A-02", "B-01" }, json.GetProperty("vacantStalls").EnumerateArray().Select(s => s.GetProperty("code").GetString()));
        Assert.Equal(33, json.GetProperty("districts").GetArrayLength());
        Assert.False(pending.TryGetProperty("aadhaarHash", out _));

        var view = Assert.IsType<ViewResult>(await c.Index());
        Assert.IsType<ApplicationStats>(view.ViewData["Stats"]);
        Assert.Equal("Applications", view.ViewData["Title"]);
    }

    private static Task<ApplicationStats> StatsOf(IMSBContext ctx) => new IMSB.Backend.Applications(ctx).StatsAsync();

    [Theory]
    [InlineData(null, "approved", null, "APP-0002")]
    [InlineData(null, "PENDING", null, "APP-0001,APP-0003")]
    [InlineData("app-0003", null, null, "APP-0003")]
    [InlineData("7000000001", null, null, "APP-0001")]
    [InlineData("warangal", null, null, "APP-0003")]
    [InlineData("ART-0042", null, null, "APP-0002")]
    [InlineData(null, null, "Warangal", "APP-0003")]
    [InlineData("nobody", null, null, "")]
    public async Task Data_filters(string? q, string? status, string? district, string expected)
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001");
        var approved = AddApplication(ctx, "APP-0002", ApplicationStatuses.Approved, decidedAt: DateTime.UtcNow);
        AddApplication(ctx, "APP-0003", district: "Warangal", category: Pottery);
        approved.ArtisanCode = "ART-0042";
        ctx.SaveChanges();
        var ids = Ids(await Applications(ctx).Data(q, status, null, district)).Split(',', StringSplitOptions.RemoveEmptyEntries).OrderBy(x => x);
        Assert.Equal(expected, string.Join(",", ids));
    }

    [Fact]
    public async Task Data_category_filter()
    {
        using var ctx = Seeded();
        AddApplication(ctx, "APP-0001");
        AddApplication(ctx, "APP-0002", category: Pottery);
        Assert.Equal("APP-0002", Ids(await Applications(ctx).Data(null, null, Pottery.ToString(), null)));
    }

    [Fact]
    public async Task Get_returns_details_with_masked_aadhaar()
    {
        using var ctx = Seeded();
        var a = AddApplication(ctx, "APP-0001");
        var json = TestHelpers.OkJson(await Applications(ctx).Get("APP-0001"));
        Assert.Equal(("APP-0001", "XXXX XXXX " + a.AadhaarLast4, "PENDING"),
            (json.GetProperty("id").GetString(), json.GetProperty("aadhaarMasked").GetString(), json.GetProperty("status").GetString()));
        Assert.IsType<NotFoundResult>(await Applications(ctx).Get("APP-9999"));
    }

    // ── Export ────────────────────────────────────────────────────

    [Fact]
    public async Task Export_follows_the_tab_and_masks_aadhaar()
    {
        using var ctx = Seeded();
        var pending = AddApplication(ctx, "APP-0001", submitted: DateTime.Today.AddDays(-2));
        AddApplication(ctx, "APP-0002", ApplicationStatuses.Approved, decidedAt: DateTime.UtcNow);

        var file = Assert.IsType<FileContentResult>(await Applications(ctx).Export(null, "PENDING", null, null));
        Assert.StartsWith("applications_pending_", file.FileDownloadName);
        var lines = SalesTestHelpers.CsvText(file).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("App ID,Applicant,Gender,Date of Birth,Mobile,Email,Aadhaar Number,District,Craft Category,Years of Experience,Status,Submitted,Days Waiting,Reviewer,Decided On,Decided By,Decision Remarks,Artisan ID,Stall Assigned", lines[0]);
        Assert.Equal($"APP-0001,Applicant 1,Female,1990-01-01,7000000001,,XXXX XXXX {pending.AadhaarLast4},Hyderabad,Textiles,0,Pending,{DateTime.Today.AddDays(-2):yyyy-MM-dd},2,,,,,,", lines[1]);
        Assert.Equal(2, lines.Length);

        var all = SalesTestHelpers.CsvText(await Applications(ctx).Export(null, null, null, null));
        Assert.Contains("APP-0002", all);
        Assert.Contains(",Approved,", all);

        using var _ = new TestHelpers.CultureScope("te-IN");
        var te = SalesTestHelpers.CsvText(await Applications(ctx).Export(null, "PENDING", null, null));
        Assert.Contains("," + TestHelpers.SharedLocalizer()["PendingLabel"].Value + ",", te);
        Assert.DoesNotContain(",Pending,", te);
        Assert.Contains(",దరఖాస్తుదారు,", te.Split(Environment.NewLine)[0]);
    }
}
