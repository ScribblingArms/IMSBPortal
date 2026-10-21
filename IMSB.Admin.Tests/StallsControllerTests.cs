using System.Text.Json;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IMSB.Admin.Tests.BazaarTestHelpers;

namespace IMSB.Admin.Tests;

public class StallsControllerTests
{
    private static Stall Get(IMSBContext ctx, string code) => ctx.Stalls.AsNoTracking().Single(s => s.StallCode == code);
    private static List<string> Codes(IActionResult r, string prop = "stalls") =>
        TestHelpers.OkJson(r).GetProperty(prop).EnumerateArray().Select(s => s.GetProperty("code").GetString()!).ToList();

    // ── Stats ─────────────────────────────────────────────────────

    [Fact]
    public async Task Stats_count_each_status_and_pending_reallocations()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        await c.RequestReallocation(Reallocation());

        var s = await c.ComputeStatsAsync();
        Assert.Equal((5, 1, 2, 1, 1, 1), (s.Total, s.Occupied, s.Vacant, s.Reserved, s.Maintenance, s.PendingReallocation));

        var view = Assert.IsType<ViewResult>(await c.Index());
        Assert.Equal(s, view.ViewData["Stats"]);
        Assert.Equal("Stall Management", view.ViewData["Title"]);
    }

    [Fact]
    public async Task Empty_database_has_zero_stats()
    {
        using var ctx = InventoryTestHelpers.CreateContext();
        var s = await Stalls(ctx).ComputeStatsAsync();
        Assert.Equal(new StallStats(0, 0, 0, 0, 0, 0), s);
    }

    // ── Data / filters ────────────────────────────────────────────

    [Fact]
    public async Task Data_returns_list_map_pickers_and_artisan_details()
    {
        using var ctx = Seeded();
        InventoryTestHelpers.AddProduct(ctx, "PRD001", "Saree", Textiles, "a-01", 5, 1);
        var json = TestHelpers.OkJson(await Stalls(ctx).Data(null, null, null, null));

        Assert.Equal(new[] { "A-01", "A-02", "B-01", "B-02", "C-01" }, json.GetProperty("stalls").EnumerateArray().Select(s => s.GetProperty("code").GetString()));
        Assert.Equal(5, json.GetProperty("map").GetArrayLength());
        Assert.Equal(new[] { "A", "B", "C" }, json.GetProperty("blocks").EnumerateArray().Select(b => b.GetString()));

        var a01 = json.GetProperty("stalls")[0];
        Assert.Equal(("OCCUPIED", "Kamala Rajendran", "ART-0001", "Textiles", 1), (a01.GetProperty("status").GetString(), a01.GetProperty("artisanName").GetString(),
            a01.GetProperty("artisanCode").GetString(), a01.GetProperty("categoryName").GetString(), a01.GetProperty("productCount").GetInt32()));

        // only artisans who can take a stall and have none: not Kamala (has A-01), not Lakshmi (inactive)
        Assert.Equal(new[] { "ART-0003", "ART-0002" }, json.GetProperty("freeArtisans").EnumerateArray().Select(a => a.GetProperty("code").GetString()));
    }

    [Theory]
    [InlineData("kamala", null, null, null, "A-01")]
    [InlineData("art-0001", null, null, null, "A-01")]
    [InlineData("b-0", null, null, null, "B-01,B-02")]
    [InlineData("near gate", null, null, null, "")]
    [InlineData(null, "vacant", null, null, "A-02,B-01")]
    [InlineData(null, "RESERVED", null, null, "B-02")]
    [InlineData(null, "MAINTENANCE", null, null, "C-01")]
    [InlineData(null, "nonsense", null, null, "A-01,A-02,B-01,B-02,C-01")]
    [InlineData(null, null, null, "b", "B-01,B-02")]
    [InlineData(null, "VACANT", null, "B", "B-01")]
    public async Task List_filters(string? q, string? status, string? category, string? block, string expected)
    {
        using var ctx = Seeded();
        Assert.Equal(expected, string.Join(",", Codes(await Stalls(ctx).Data(q, status, category, block))));
    }

    [Fact]
    public async Task Category_filter_and_map_ignore_list_filters()
    {
        using var ctx = Seeded();
        var json = TestHelpers.OkJson(await Stalls(ctx).Data(null, null, Textiles.ToString(), null));
        Assert.Equal(new[] { "A-01" }, json.GetProperty("stalls").EnumerateArray().Select(s => s.GetProperty("code").GetString()));
        Assert.Equal(5, json.GetProperty("map").GetArrayLength());
    }

    // ── Create / update / delete ──────────────────────────────────

    [Fact]
    public async Task Create_normalizes_and_saves()
    {
        using var ctx = Seeded();
        var result = await Stalls(ctx, "clerk@imsb").Create(NewStall(code: " d-01 ", name: "  Corner   stall ", block: "d"));
        Assert.Equal("D-01", TestHelpers.OkJson(result).GetProperty("code").GetString());
        var s = Get(ctx, "D-01");
        Assert.Equal(("Corner stall", "D", "VACANT", 2500m, "clerk@imsb"), (s.Name, s.Block, s.Status, s.MonthlyRent!.Value, s.CreatedBy));
    }

    [Theory]
    [InlineData("A-01", "stall_code", "A stall with this ID already exists")]
    [InlineData("a-01", "stall_code", "A stall with this ID already exists")]
    [InlineData("", "stall_code", "Stall ID is required (letters, digits, -)")]
    [InlineData("A 01", "stall_code", "Stall ID is required (letters, digits, -)")]
    public async Task Create_rejects_bad_or_duplicate_codes(string code, string key, string message)
    {
        using var ctx = Seeded();
        var errors = TestHelpers.Errors(await Stalls(ctx).Create(NewStall(code: code)));
        Assert.Equal(message, errors[key]);
    }

    [Fact]
    public async Task Create_cannot_start_occupied_and_checks_category()
    {
        using var ctx = Seeded();
        var dto = NewStall(status: "OCCUPIED");
        dto.CategoryId = Guid.NewGuid().ToString();
        var errors = TestHelpers.Errors(await Stalls(ctx).Create(dto));
        Assert.Equal("Select Vacant, Reserved or Maintenance", errors["status"]);
        Assert.Equal("Category is required", errors["category_id"]);
    }

    [Fact]
    public async Task Model_state_errors_are_returned_by_field()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        var dto = NewStall(name: "X", block: "!!");
        TestHelpers.BindModelState(c, dto);
        var errors = TestHelpers.Errors(await c.Create(dto));
        // BindModelState passes the annotation's resource key through (MVC translates it when binding for real)
        Assert.Equal("ValStallName", errors["name"]);
        Assert.Equal("ValBlock", errors["block"]);
    }

    [Fact]
    public async Task Update_changes_details_but_not_the_code()
    {
        using var ctx = Seeded();
        var dto = NewStall(code: "Z-99", name: "Renamed", block: "a", status: "RESERVED");
        Assert.IsType<OkObjectResult>(await Stalls(ctx, "editor").Update("A-02", dto));
        var s = Get(ctx, "A-02");
        Assert.Equal(("Renamed", "RESERVED", "editor"), (s.Name, s.Status, s.UpdatedBy));
        Assert.False(ctx.Stalls.Any(x => x.StallCode == "Z-99"));
    }

    [Fact]
    public async Task Occupied_stall_keeps_its_status_on_edit()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        var errors = TestHelpers.Errors(await c.Update("A-01", NewStall(status: "VACANT")));
        Assert.Equal("An occupied stall stays occupied; vacate or reallocate it instead", errors["status"]);

        // without a status the other fields can still be edited
        Assert.IsType<OkObjectResult>(await c.Update("A-01", NewStall(name: "Kamala's corner")));
        Assert.Equal(("Kamala's corner", "OCCUPIED"), (Get(ctx, "A-01").Name, Get(ctx, "A-01").Status));
        Assert.NotNull(Get(ctx, "A-01").ArtisanId);
    }

    [Fact]
    public async Task Delete_rules()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        Assert.Equal("An occupied stall cannot be deleted. Vacate it first.", TestHelpers.Errors(await c.Delete("A-01"))["stall"]);

        InventoryTestHelpers.AddProduct(ctx, "PRD001", "Pot", Pottery, "b-01", 3, 1);
        Assert.Equal("1 products are assigned to this stall. Move them in Inventory first.", TestHelpers.Errors(await c.Delete("B-01"))["stall"]);

        Assert.IsType<OkObjectResult>(await c.Delete("A-02"));
        Assert.False(ctx.Stalls.Any(s => s.StallCode == "A-02"));
        Assert.IsType<NotFoundResult>(await c.Delete("A-02"));
        Assert.IsType<NotFoundResult>(await c.Update("A-02", NewStall()));
    }

    // ── Allocation ────────────────────────────────────────────────

    [Fact]
    public async Task Allocate_assigns_artisan_and_marks_occupied()
    {
        using var ctx = Seeded();
        var result = await Stalls(ctx).Allocate(Allocation(end: DateTime.Today.AddYears(1).ToString("yyyy-MM-dd")));
        Assert.Equal("ART-0002", TestHelpers.OkJson(result).GetProperty("artisanCode").GetString());

        var s = Get(ctx, "A-02");
        var saroja = ctx.Artisans.Single(a => a.ArtisanCode == "ART-0002");
        Assert.Equal((StallStatuses.Occupied, saroja.Id, DateTime.Today, 3000m), (s.Status, s.ArtisanId!.Value, s.AllottedDate!.Value, s.MonthlyRent!.Value));
        Assert.Equal(Pottery, s.CategoryId);   // defaults to the artisan's craft
    }

    [Fact]
    public async Task Allocate_rules()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        string Err(IActionResult r, string k) => TestHelpers.Errors(r)[k];

        Assert.Equal("Only a vacant stall can be allocated", Err(await c.Allocate(Allocation(stall: "A-01")), "stall_code"));
        Assert.Equal("Only a vacant stall can be allocated", Err(await c.Allocate(Allocation(stall: "B-02")), "stall_code"));   // reserved
        Assert.Equal("Select a stall", Err(await c.Allocate(Allocation(stall: "Z-99")), "stall_code"));
        Assert.Equal("This artisan already holds a stall", Err(await c.Allocate(Allocation(artisan: "ART-0001")), "artisan_code"));
        Assert.Equal("Inactive artisans cannot be given a stall", Err(await c.Allocate(Allocation(artisan: "ART-0004")), "artisan_code"));
        Assert.Equal("Select an artisan", Err(await c.Allocate(Allocation(artisan: "ART-9999")), "artisan_code"));
        Assert.Equal("Invalid date", Err(await c.Allocate(Allocation(date: "01/01/2025")), "allotted_date"));
        Assert.Equal("Agreement end must be after the allotment date",
            Err(await c.Allocate(Allocation(date: "2025-06-01", end: "2025-06-01")), "agreement_end_date"));
        Assert.Equal(StallStatuses.Vacant, Get(ctx, "A-02").Status);   // nothing changed
    }

    [Fact]
    public async Task On_leave_artisans_can_be_allocated()
    {
        using var ctx = Seeded();
        Assert.IsType<OkObjectResult>(await Stalls(ctx).Allocate(Allocation(artisan: "ART-0003")));
    }

    [Fact]
    public async Task Vacate_frees_the_stall_and_closes_open_requests()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        await c.RequestReallocation(Reallocation());

        Assert.IsType<OkObjectResult>(await c.Vacate("A-01"));
        var s = Get(ctx, "A-01");
        Assert.Equal((StallStatuses.Vacant, (Guid?)null, (DateTime?)null), (s.Status, s.ArtisanId, s.AllottedDate));
        var r = ctx.StallReallocations.AsNoTracking().Single();
        Assert.Equal((ReallocationStatuses.Rejected, "Closed: the stall was vacated"), (r.Status, r.DecisionRemarks));

        Assert.Equal("This stall is not occupied", TestHelpers.Errors(await c.Vacate("A-01"))["stall"]);
        Assert.IsType<NotFoundResult>(await c.Vacate("Z-99"));
    }

    // ── Reallocation ──────────────────────────────────────────────

    [Fact]
    public async Task Reallocation_request_and_approval_moves_the_stall()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx, "officer");
        var id = TestHelpers.OkJson(await c.RequestReallocation(Reallocation())).GetProperty("id").GetString()!;
        Assert.Equal("REA-0001", id);

        var r = ctx.StallReallocations.AsNoTracking().Single();
        Assert.Equal(("A-01", "Kamala Rajendran", "Saroja Murugan", "ARTISAN_REQUEST", "PENDING"), (r.StallCode, r.CurrentArtisanName, r.NewArtisanName, r.Reason, r.Status));

        Assert.IsType<OkObjectResult>(await c.ApproveReallocation(id, new DecisionDto { Remarks = " ok " }));
        var s = Get(ctx, "A-01");
        Assert.Equal(ctx.Artisans.Single(a => a.ArtisanCode == "ART-0002").Id, s.ArtisanId);
        Assert.Equal((DateTime.Today, Pottery), (s.AllottedDate!.Value, s.CategoryId!.Value));
        r = ctx.StallReallocations.AsNoTracking().Single();
        Assert.Equal(("APPROVED", "officer", "ok"), (r.Status, r.DecidedBy, r.DecisionRemarks));

        // decided requests cannot be decided again
        Assert.Equal("This request has already been decided", TestHelpers.Errors(await c.RejectReallocation(id, null))["request"]);
    }

    [Fact]
    public async Task Approving_a_request_without_new_artisan_vacates()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        var id = TestHelpers.OkJson(await c.RequestReallocation(Reallocation(reason: "closure", newArtisan: null))).GetProperty("id").GetString()!;
        await c.ApproveReallocation(id, null);
        Assert.Equal(StallStatuses.Vacant, Get(ctx, "A-01").Status);
    }

    [Fact]
    public async Task Rejecting_leaves_the_stall_as_it_was()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        var id = TestHelpers.OkJson(await c.RequestReallocation(Reallocation())).GetProperty("id").GetString()!;
        Assert.IsType<OkObjectResult>(await c.RejectReallocation(id, new DecisionDto { Remarks = "no" }));
        Assert.Equal("ART-0001", ctx.Artisans.Single(a => a.Id == Get(ctx, "A-01").ArtisanId).ArtisanCode);
        Assert.Equal(0, (await c.ComputeStatsAsync()).PendingReallocation);
    }

    [Fact]
    public async Task Reallocation_request_rules()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        string Err(IActionResult r, string k) => TestHelpers.Errors(r)[k];

        Assert.Equal("This stall is not occupied", Err(await c.RequestReallocation(Reallocation(stall: "A-02")), "stall_code"));
        Assert.Equal("Select a reason", Err(await c.RequestReallocation(Reallocation(reason: "BORED")), "reason"));
        Assert.Equal("This artisan already holds the stall", Err(await c.RequestReallocation(Reallocation(newArtisan: "ART-0001")), "new_artisan_code"));
        Assert.Equal("Inactive artisans cannot be given a stall", Err(await c.RequestReallocation(Reallocation(newArtisan: "ART-0004")), "new_artisan_code"));
        var future = Reallocation();
        future.RequestedDate = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        Assert.Equal("Date cannot be in the future", Err(await c.RequestReallocation(future), "requested_date"));

        await c.RequestReallocation(Reallocation());
        Assert.Equal("This stall already has a pending reallocation request", Err(await c.RequestReallocation(Reallocation(newArtisan: "ART-0003")), "stall_code"));
    }

    [Fact]
    public async Task Stale_or_unavailable_requests_cannot_be_approved()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        var id = TestHelpers.OkJson(await c.RequestReallocation(Reallocation())).GetProperty("id").GetString()!;
        // the target artisan got another stall in the meantime
        await c.Allocate(Allocation(stall: "A-02", artisan: "ART-0002"));
        Assert.Equal("The new artisan can no longer take this stall", TestHelpers.Errors(await c.ApproveReallocation(id, null))["request"]);
        Assert.Equal("ART-0001", ctx.Artisans.Single(a => a.Id == Get(ctx, "A-01").ArtisanId).ArtisanCode);

        Assert.IsType<NotFoundResult>(await c.ApproveReallocation("REA-9999", null));
        Assert.IsType<NotFoundResult>(await c.RejectReallocation("REA-9999", null));
    }

    [Fact]
    public async Task Request_listing_puts_pending_first()
    {
        using var ctx = Seeded();
        var c = Stalls(ctx);
        var first = TestHelpers.OkJson(await c.RequestReallocation(Reallocation())).GetProperty("id").GetString()!;
        await c.RejectReallocation(first, null);
        await c.RequestReallocation(Reallocation(newArtisan: "ART-0003"));
        var list = TestHelpers.OkJson(await c.Data(null, null, null, null)).GetProperty("reallocations").EnumerateArray()
            .Select(r => (r.GetProperty("id").GetString()!, r.GetProperty("status").GetString()!)).ToList();
        Assert.Equal(new[] { ("REA-0002", "PENDING"), ("REA-0001", "REJECTED") }, list);
    }

    // ── Export ────────────────────────────────────────────────────

    [Fact]
    public async Task Export_follows_filters_with_localized_status()
    {
        using var ctx = Seeded();
        var csv = SalesTestHelpers.CsvText(await Stalls(ctx).Export(null, null, null, "A"));
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Stall ID,Stall Name,Block / Zone,Row,Location,Status,Category,Artisan ID,Artisan,Allotted Date,Agreement End Date,Monthly Rent (₹),Remarks", lines[0]);
        Assert.Equal("A-01,Stall A-01,A,,,Occupied,Textiles,ART-0001,Kamala Rajendran,2025-01-01,,,", lines[1]);
        Assert.Equal("A-02,Stall A-02,A,,,Vacant,,,,,,,", lines[2]);
        Assert.Equal(3, lines.Length);

        using var _ = new TestHelpers.CultureScope("te-IN");
        Assert.Contains(",ఖాళీ,", SalesTestHelpers.CsvText(await Stalls(ctx).Export(null, "VACANT", null, null)));
    }

    // ── Bazaar stats used by Dashboard / Sales ────────────────────

    [Fact]
    public async Task Bazaar_stats_use_registered_stalls_when_there_are_any()
    {
        using var ctx = InventoryTestHelpers.CreateContext();
        InventoryTestHelpers.AddProduct(ctx, "PRD001", "Pot", Pottery, "X-01", 3, 1);
        var options = new BazaarOptions { TotalStalls = 50 };
        Assert.Equal((50, 1), await BazaarStats.StallsAsync(ctx, options));   // no stalls yet: configured capacity

        var kamala = AddArtisan(ctx, "ART-0001", "Kamala", "9876543210");
        AddStall(ctx, "A-01", artisan: kamala);
        AddStall(ctx, "A-02");
        Assert.Equal((2, 1), await BazaarStats.StallsAsync(ctx, options));
    }
}
