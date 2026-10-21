using System.Text.Json;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IMSB.Admin.Tests.BazaarTestHelpers;

namespace IMSB.Admin.Tests;

public class ArtisansControllerTests
{
    private static Artisan Get(IMSBContext ctx, string code) => ctx.Artisans.AsNoTracking().Single(a => a.ArtisanCode == code);
    private static string Codes(IActionResult r) =>
        string.Join(",", TestHelpers.OkJson(r).GetProperty("artisans").EnumerateArray().Select(a => a.GetProperty("code").GetString()));

    // ── Stats + chart ─────────────────────────────────────────────

    [Fact]
    public async Task Stats_and_chart_count_each_status()
    {
        using var ctx = Seeded();
        AddArtisan(ctx, "ART-0005", "Radha", "9000000005", ArtisanStatuses.RenewalDue);
        var c = Artisans(ctx);

        var s = await c.ComputeStatsAsync();
        Assert.Equal(new ArtisanStats(Total: 5, Active: 2, OnLeave: 1, RenewalPending: 1, Inactive: 1, WithStall: 1), s);

        var chart = TestHelpers.OkJson(await c.Data(null, null, null, null, null)).GetProperty("chart");
        Assert.Equal(new[] { "ACTIVE", "ON_LEAVE", "RENEWAL_DUE", "INACTIVE" }, chart.GetProperty("statuses").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { 2, 1, 1, 1 }, chart.GetProperty("values").EnumerateArray().Select(x => x.GetInt32()));

        var view = Assert.IsType<ViewResult>(await c.Index());
        Assert.Equal(s, view.ViewData["Stats"]);
    }

    // ── Directory + filters ───────────────────────────────────────

    [Fact]
    public async Task Directory_rows_include_stall_and_category()
    {
        using var ctx = Seeded();
        var json = TestHelpers.OkJson(await Artisans(ctx).Data(null, null, null, null, null));
        var rows = json.GetProperty("artisans").EnumerateArray().ToList();
        Assert.Equal(new[] { "Kamala Rajendran", "Lakshmi Velu", "Meena Selvam", "Saroja Murugan" }, rows.Select(r => r.GetProperty("fullName").GetString()));
        Assert.Equal(("A-01", "Textiles"), (rows[0].GetProperty("stallCode").GetString(), rows[0].GetProperty("categoryName").GetString()));
        Assert.Equal(JsonValueKind.Null, rows[3].GetProperty("stallCode").ValueKind);
        Assert.Equal(33, json.GetProperty("districts").GetArrayLength());
    }

    [Theory]
    [InlineData("saroja", null, null, null, null, "ART-0002")]
    [InlineData("ART-0003", null, null, null, null, "ART-0003")]
    [InlineData("98765", null, null, null, null, "ART-0001")]
    [InlineData("a-01", null, null, null, null, "ART-0001")]
    [InlineData(null, "on_leave", null, null, null, "ART-0003")]
    [InlineData(null, "INACTIVE", null, null, null, "ART-0004")]
    [InlineData(null, null, null, "warangal", null, "ART-0004,ART-0002")]
    [InlineData(null, null, null, null, "assigned", "ART-0001")]
    [InlineData(null, null, null, null, "unassigned", "ART-0004,ART-0003,ART-0002")]
    [InlineData(null, "ACTIVE", null, "Warangal", "unassigned", "ART-0002")]
    [InlineData("nobody", null, null, null, null, "")]
    public async Task Directory_filters(string? q, string? status, string? category, string? district, string? stall, string expected)
    {
        using var ctx = Seeded();
        Assert.Equal(expected, Codes(await Artisans(ctx).Data(q, status, category, district, stall)));
    }

    [Fact]
    public async Task Category_filter()
    {
        using var ctx = Seeded();
        Assert.Equal("ART-0002", Codes(await Artisans(ctx).Data(null, null, Pottery.ToString(), null, null)));
    }

    // ── New application ───────────────────────────────────────────

    [Fact]
    public async Task New_application_registers_artisan_without_storing_aadhaar()
    {
        using var ctx = Seeded();
        var dto = NewArtisan(mobile: "+91 91234 56780");
        var result = await Artisans(ctx, "clerk").Create(dto);
        Assert.Equal("ART-0005", TestHelpers.OkJson(result).GetProperty("code").GetString());

        var a = Get(ctx, "ART-0005");
        Assert.Equal(("Anitha Jayakumar", "9123456780", "anitha@example.com", "Siddipet", ArtisanStatuses.Active, DateTime.Today, "clerk"),
            (a.FullName, a.Mobile, a.Email, a.District, a.Status, a.JoinedDate, a.CreatedBy));
        Assert.Equal(dto.Aadhaar![^4..], a.AadhaarLast4);
        Assert.Equal(Aadhaar.Hash(dto.Aadhaar), a.AadhaarHash);
        Assert.DoesNotContain(dto.Aadhaar, a.AadhaarHash);
        Assert.Equal("XXXX XXXX " + dto.Aadhaar[^4..], a.MaskedAadhaar);
    }

    [Theory]
    [InlineData("mobile", "12345", "Enter a valid 10-digit mobile number")]
    [InlineData("mobile", "5123456789", "Enter a valid 10-digit mobile number")]
    [InlineData("mobile", "9876543210", "Mobile already registered")]
    [InlineData("district", "Chennai", "Select a district")]
    [InlineData("gender", "Unknown", "Select a gender")]
    [InlineData("category_id", "not-a-guid", "Select a craft category")]
    [InlineData("status", "RETIRED", "Invalid status")]
    [InlineData("date_of_birth", "2015-01-01", "Artisan must be at least 18 years old")]
    [InlineData("date_of_birth", "1990/01/01", "Invalid date of birth")]
    [InlineData("joined_date", "2999-01-01", "Join date cannot be in the future")]
    public async Task New_application_validation(string field, string value, string message)
    {
        using var ctx = Seeded();
        var dto = NewArtisan();
        switch (field)
        {
            case "mobile": dto.Mobile = value; break;
            case "district": dto.District = value; break;
            case "gender": dto.Gender = value; break;
            case "category_id": dto.CategoryId = value; break;
            case "status": dto.Status = value; break;
            case "date_of_birth": dto.DateOfBirth = value; break;
            case "joined_date": dto.JoinedDate = value; break;
        }
        var errors = TestHelpers.Errors(await Artisans(ctx).Create(dto));
        Assert.Equal(message, errors[field]);
        Assert.Equal(4, ctx.Artisans.Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1234 5678 9012")]   // starts with 1
    [InlineData("2234 5678 901")]    // 11 digits
    [InlineData("abcd efgh ijkl")]
    public async Task Aadhaar_is_required_and_checked(string? aadhaar)
    {
        using var ctx = Seeded();
        var dto = NewArtisan();
        dto.Aadhaar = aadhaar;
        Assert.Equal("Enter a valid 12-digit Aadhaar number", TestHelpers.Errors(await Artisans(ctx).Create(dto))["aadhaar"]);
    }

    [Fact]
    public async Task Aadhaar_checksum_typo_is_caught()
    {
        using var ctx = Seeded();
        var dto = NewArtisan();
        var digits = dto.Aadhaar!;
        dto.Aadhaar = digits[..11] + (char)('0' + (digits[11] - '0' + 1) % 10);   // wrong check digit
        Assert.True(TestHelpers.Errors(await Artisans(ctx).Create(dto)).ContainsKey("aadhaar"));
    }

    [Fact]
    public async Task Same_aadhaar_cannot_register_twice_even_formatted_differently()
    {
        using var ctx = Seeded();
        var c = Artisans(ctx);
        var first = NewArtisan();
        Assert.IsType<OkObjectResult>(await c.Create(first));
        var second = NewArtisan(mobile: "9988776655", name: "Someone Else");
        second.Aadhaar = string.Join(" ", first.Aadhaar![..4], first.Aadhaar[4..8], first.Aadhaar[8..]);
        Assert.Equal("This Aadhaar number is already registered", TestHelpers.Errors(await c.Create(second))["aadhaar"]);
    }

    // ── Edit ──────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_updates_fields_and_keeps_aadhaar_when_blank()
    {
        using var ctx = Seeded();
        var before = Get(ctx, "ART-0002");
        var dto = ToDto(before, ArtisanStatuses.OnLeave);
        dto.FullName = "Saroja M.";
        dto.Mobile = before.Mobile;   // own number is not a duplicate
        Assert.IsType<OkObjectResult>(await Artisans(ctx, "editor").Update("ART-0002", dto));

        var after = Get(ctx, "ART-0002");
        Assert.Equal(("Saroja M.", ArtisanStatuses.OnLeave, before.AadhaarHash, "editor"), (after.FullName, after.Status, after.AadhaarHash, after.UpdatedBy));
    }

    [Fact]
    public async Task Edit_can_replace_aadhaar()
    {
        using var ctx = Seeded();
        var dto = ToDto(Get(ctx, "ART-0002"));
        dto.Aadhaar = ValidAadhaar(777);
        await Artisans(ctx).Update("ART-0002", dto);
        Assert.Equal(Aadhaar.Hash(dto.Aadhaar), Get(ctx, "ART-0002").AadhaarHash);
    }

    [Fact]
    public async Task Artisan_with_stall_cannot_become_inactive()
    {
        using var ctx = Seeded();
        var errors = TestHelpers.Errors(await Artisans(ctx).Update("ART-0001", ToDto(Get(ctx, "ART-0001"), ArtisanStatuses.Inactive)));
        Assert.Equal("Vacate the artisan's stall before marking them inactive", errors["status"]);
    }

    // ── Delete ────────────────────────────────────────────────────

    [Fact]
    public async Task Artisan_with_stall_cannot_be_deleted()
    {
        using var ctx = Seeded();
        var errors = TestHelpers.Errors(await Artisans(ctx).Delete("ART-0001"));
        Assert.Equal("This artisan holds stall A-01. Vacate or reallocate it first.", errors["artisan"]);
        Assert.Equal(4, ctx.Artisans.Count());
    }

    [Fact]
    public async Task Delete_closes_requests_naming_the_artisan_and_keeps_history()
    {
        using var ctx = Seeded();
        var stalls = Stalls(ctx);
        await stalls.RequestReallocation(Reallocation(newArtisan: "ART-0002"));

        Assert.IsType<OkObjectResult>(await Artisans(ctx).Delete("ART-0002"));
        Assert.False(ctx.Artisans.Any(a => a.ArtisanCode == "ART-0002"));
        var r = ctx.StallReallocations.AsNoTracking().Single();
        Assert.Equal((ReallocationStatuses.Rejected, (Guid?)null, "Saroja Murugan", "Closed: the artisan was deleted"),
            (r.Status, r.NewArtisanId, r.NewArtisanName, r.DecisionRemarks));
        Assert.IsType<NotFoundResult>(await Artisans(ctx).Delete("ART-0002"));
    }

    // ── Profile ───────────────────────────────────────────────────

    [Fact]
    public async Task Profile_shows_masked_aadhaar_stall_and_month_sales()
    {
        using var ctx = Seeded();
        InventoryTestHelpers.AddProduct(ctx, "PRD001", "Saree", Textiles, "A-01", 10, 1, 1000);
        await SalesTestHelpers.Sell(SalesTestHelpers.CreateSales(ctx), null, "00:00", SalesTestHelpers.Item("PRD001", 3, 1000));

        var json = TestHelpers.OkJson(await Artisans(ctx).Get("ART-0001"));
        Assert.Equal("A-01", json.GetProperty("stallCode").GetString());
        Assert.Equal(3000m, json.GetProperty("monthSales").GetDecimal());
        Assert.Equal(1, json.GetProperty("productsListed").GetInt32());
        Assert.StartsWith("XXXX XXXX ", json.GetProperty("aadhaarMasked").GetString());
        Assert.False(json.TryGetProperty("aadhaarHash", out _));
        Assert.IsType<NotFoundResult>(await Artisans(ctx).Get("ART-9999"));
    }

    // ── Export ────────────────────────────────────────────────────

    [Fact]
    public async Task Export_masks_aadhaar_and_follows_filters()
    {
        using var ctx = Seeded();
        var csv = SalesTestHelpers.CsvText(await Artisans(ctx).Export(null, null, null, null, "assigned"));
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Artisan ID,Full Name,Gender,Date of Birth,Mobile,Email,Aadhaar Number,District,Craft Category,Years of Experience,Stall,Status,Joined", lines[0]);
        var a = Get(ctx, "ART-0001");
        Assert.Equal($"ART-0001,Kamala Rajendran,Female,1985-05-05,9876543210,,XXXX XXXX {a.AadhaarLast4},Hyderabad,Textiles,0,A-01,Active,2024-01-15", lines[1]);
        Assert.Equal(2, lines.Length);

        using var _ = new TestHelpers.CultureScope("hi-IN");
        Assert.Contains(",अवकाश पर,",SalesTestHelpers.CsvText(await Artisans(ctx).Export(null, "ON_LEAVE", null, null, null)));
    }

    // ── Aadhaar helper ────────────────────────────────────────────

    [Theory]
    [InlineData("234123412346", true)]    // Verhoeff-valid sample
    [InlineData("2341 2341 2346", true)]
    [InlineData("234123412347", false)]
    [InlineData("034123412346", false)]
    [InlineData("23412341234", false)]
    public void Aadhaar_validation(string value, bool valid) => Assert.Equal(valid, Aadhaar.IsValid(value));

    [Fact]
    public void Check_digit_round_trips()
    {
        for (int i = 1; i < 200; i++) Assert.True(Aadhaar.IsValid(ValidAadhaar(i)), ValidAadhaar(i));
    }
}
