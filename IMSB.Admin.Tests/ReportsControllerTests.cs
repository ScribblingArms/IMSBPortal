using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IMSB.Admin.Tests.ApplicationTestHelpers;
using static IMSB.Admin.Tests.BazaarTestHelpers;
using static IMSB.Admin.Tests.SalesTestHelpers;

namespace IMSB.Admin.Tests;

public class ReportsControllerTests
{
    private static readonly DateTime Today = DateTime.Today;
    private static readonly DateTime MonthStart = new(Today.Year, Today.Month, 1);

    /// <summary>
    /// Bazaar seed + PRD001 Saree (Textiles, A-01, 10 @ min 5) and PRD002 Pot (Pottery, A-02, 2 @ min 10).
    /// Sales: today 3000 (A-01) + 200 (A-02); 1st of last month 1600 (A-01); a year ago 500 (A-01).
    /// </summary>
    private static async Task<IMSBContext> ReportContext()
    {
        var ctx = Seeded();
        InventoryTestHelpers.AddProduct(ctx, "PRD001", "Saree", Textiles, "A-01", 10, 5, 1000);
        InventoryTestHelpers.AddProduct(ctx, "PRD002", "Pot", Pottery, "A-02", 2, 10, 200);
        var sales = CreateSales(ctx);
        await Sell(sales, null, "00:00", Item("PRD001", 3, 1000));
        await Sell(sales, null, "00:00", Item("PRD002", 1, 200));
        await Sell(sales, MonthStart.AddMonths(-1), "10:00", Item("PRD001", 1, 1600));
        await Sell(sales, Today.AddYears(-1), "10:00", Item("PRD001", 1, 500));
        return ctx;
    }

    private static ReportPeriod ThisMonth => ReportPeriod.ForMonth(null, Today);

    // ── Period ────────────────────────────────────────────────────

    [Fact]
    public void Period_defaults_to_the_current_month_up_to_today()
    {
        var p = ReportPeriod.ForMonth(null, new DateTime(2026, 10, 9));
        Assert.Equal((new DateTime(2026, 10, 1), new DateTime(2026, 10, 10), 9, "2026-10"), (p.From, p.To, p.Days, p.Key));
    }

    [Theory]
    [InlineData("2026-08", "2026-08-01", "2026-09-01")]
    [InlineData("2026-12", "2026-10-01", "2026-10-10")]   // future
    [InlineData("garbage", "2026-10-01", "2026-10-10")]
    [InlineData("2001-01", "2026-10-01", "2026-10-10")]   // more than ten years back
    public void Period_parsing(string month, string from, string to)
    {
        var p = ReportPeriod.ForMonth(month, new DateTime(2026, 10, 9));
        Assert.Equal((DateTime.Parse(from), DateTime.Parse(to)), (p.From, p.To));
    }

    // ── KPIs ──────────────────────────────────────────────────────

    [Fact]
    public async Task Kpis_for_the_month()
    {
        using var ctx = await ReportContext();
        AddApplication(ctx, "APP-0001", ApplicationStatuses.Approved, decidedAt: DateTime.UtcNow);
        AddApplication(ctx, "APP-0002", ApplicationStatuses.Approved, decidedAt: DateTime.UtcNow.AddMonths(-2));

        var k = await Reports(ctx).KpisAsync(ThisMonth, null);
        Assert.Equal(3200m, k.TotalSales);
        Assert.Equal((1, 5, 20), (k.OccupiedStalls, k.TotalStalls, k.OccupancyPct));
        Assert.Equal(2, k.ActiveArtisans);          // Kamala, Saroja
        Assert.Null(k.AvgAttendancePct);            // not recorded in the system
        Assert.Equal(1, k.ApplicationsApproved);
        Assert.Equal(1, k.LowStockItems);           // the pot
        // last month's sale was on its 1st, inside the compared span
        Assert.Equal(100.0m, k.SalesChangePct);
    }

    [Fact]
    public async Task Kpis_follow_the_category_filter()
    {
        using var ctx = await ReportContext();
        var pottery = ctx.ProductCategories.AsNoTracking().Single(c => c.Id == Pottery);
        var k = await Reports(ctx).KpisAsync(ThisMonth, pottery);
        Assert.Equal((200m, 1, 1), (k.TotalSales, k.ActiveArtisans, k.LowStockItems));
        Assert.Equal((0, 0, 0), (k.OccupiedStalls, k.TotalStalls, k.OccupancyPct));
    }

    [Fact]
    public async Task Data_for_a_past_month()
    {
        using var ctx = await ReportContext();
        var json = TestHelpers.OkJson(await Reports(ctx).Data(MonthStart.AddMonths(-1).ToString("yyyy-MM"), null));
        Assert.Equal(MonthStart.AddMonths(-1).ToString("yyyy-MM"), json.GetProperty("month").GetString());
        Assert.Equal(1600m, json.GetProperty("kpis").GetProperty("totalSales").GetDecimal());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("attendance").ValueKind);
    }

    // ── Charts ────────────────────────────────────────────────────

    [Fact]
    public async Task Sales_trend_covers_twelve_months_with_last_year_alongside()
    {
        using var ctx = await ReportContext();
        var json = TestHelpers.OkJson(await Reports(ctx).Data(null, null)).GetProperty("salesTrend");
        var labels = json.GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
        var current = json.GetProperty("current").EnumerateArray().Select(x => x.GetDecimal()).ToList();
        var previous = json.GetProperty("previous").EnumerateArray().Select(x => x.GetDecimal()).ToList();
        Assert.Equal(12, labels.Count);
        Assert.Equal((MonthStart.ToString("yyyy-MM"), MonthStart.AddMonths(-11).ToString("yyyy-MM")), (labels[^1], labels[0]));
        Assert.Equal((3200m, 1600m, 500m), (current[^1], current[^2], previous[^1]));
        Assert.Equal(4800m, current.Sum());
    }

    [Fact]
    public async Task Category_revenue_lists_every_category()
    {
        using var ctx = await ReportContext();
        var json = TestHelpers.OkJson(await Reports(ctx).Data(null, null)).GetProperty("categoryRevenue");
        var labels = json.GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Equal(7, labels.Count);
        Assert.Equal(3000m, json.GetProperty("values")[labels.IndexOf("Textiles")].GetDecimal());
        Assert.Equal(200m, json.GetProperty("values")[labels.IndexOf("Pottery")].GetDecimal());
    }

    [Fact]
    public async Task Occupancy_trend_counts_stalls_from_their_allotted_date()
    {
        using var ctx = await ReportContext();
        // B-01 allotted to Saroja this month
        var b01 = ctx.Stalls.Single(s => s.StallCode == "B-01");
        b01.ArtisanId = ctx.Artisans.Single(a => a.ArtisanCode == "ART-0002").Id;
        b01.Status = StallStatuses.Occupied;
        b01.AllottedDate = MonthStart;
        ctx.SaveChanges();

        var rows = await Reports(ctx).OccupancyTrendAsync(ThisMonth, null);
        Assert.Equal(12, rows.Count);
        Assert.Equal((2, 5), (rows[^1].Occupied, rows[^1].Total));
        // earlier months: only A-01 (allotted 2025-01-01); B-01 was not taken yet
        Assert.Equal(1, rows[^2].Occupied);
    }

    // ── Stall report ──────────────────────────────────────────────

    [Fact]
    public async Task Stall_report_rows()
    {
        using var ctx = await ReportContext();
        var rows = await Reports(ctx).StallRowsAsync(ThisMonth, null, null, null, null);
        Assert.Equal(new[] { "A-01", "A-02", "B-01", "B-02", "C-01" }, rows.Select(r => r.StallCode));
        var a01 = rows[0];
        Assert.Equal(("Kamala Rajendran", 3000m, 1, 5, ThisMonth.Days, ThisMonth.Days, StallStatuses.Occupied),
            (a01.ArtisanName, a01.MonthlySales, a01.Products, a01.StockUnits, a01.OccupancyDays, a01.DaysInPeriod, a01.Status));
        Assert.Equal((200m, 0), (rows[1].MonthlySales, rows[1].OccupancyDays));
    }

    [Theory]
    [InlineData(null, "OCCUPIED", null, "A-01")]
    [InlineData(null, null, "b", "B-01,B-02")]
    [InlineData("kamala", null, null, "A-01")]
    [InlineData(null, "MAINTENANCE", null, "C-01")]
    public async Task Stall_report_filters(string? q, string? status, string? block, string expected)
    {
        using var ctx = await ReportContext();
        var json = TestHelpers.OkJson(await Reports(ctx).StallReport(null, q, status, block, null));
        Assert.Equal(expected, string.Join(",", json.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("stallCode").GetString())));
        Assert.Equal(new[] { "A", "B", "C" }, json.GetProperty("blocks").EnumerateArray().Select(b => b.GetString()));
    }

    [Fact]
    public async Task Occupancy_days_start_at_the_allotment()
    {
        using var ctx = await ReportContext();
        ctx.Stalls.Single(s => s.StallCode == "A-01").AllottedDate = Today;
        ctx.SaveChanges();
        var a01 = (await Reports(ctx).StallRowsAsync(ThisMonth, "A-01", null, null, null)).Single();
        Assert.Equal(1, a01.OccupancyDays);
    }

    // ── Artisan performance ───────────────────────────────────────

    [Fact]
    public async Task Artisan_report_has_sales_share_and_rating()
    {
        using var ctx = await ReportContext();
        var rows = await Reports(ctx).ArtisanRowsAsync(ThisMonth, null, null, null);
        Assert.Equal(new[] { "Kamala Rajendran", "Lakshmi Velu", "Meena Selvam", "Saroja Murugan" }, rows.Select(r => r.Name));
        var kamala = rows[0];
        Assert.Equal(("A-01", 3000m, 3, 1, 93.8m, 5), (kamala.StallCode, kamala.MonthlySales, kamala.UnitsSold, kamala.Products, kamala.SharePct, kamala.Rating));
        Assert.All(rows.Skip(1), r => Assert.Equal((0m, 0), (r.MonthlySales, r.Rating)));
    }

    [Theory]
    [InlineData("saroja", null, null, "ART-0002")]
    [InlineData(null, "INACTIVE", null, "ART-0004")]
    [InlineData("a-01", null, null, "ART-0001")]
    [InlineData("warangal", null, null, "ART-0004,ART-0002")]
    public async Task Artisan_report_filters(string? q, string? status, string? category, string expected)
    {
        using var ctx = await ReportContext();
        var json = TestHelpers.OkJson(await Reports(ctx).ArtisanReport(null, q, status, category));
        Assert.Equal(expected, string.Join(",", json.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("artisanCode").GetString())));
    }

    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(1000, 1000, 5)]
    [InlineData(810, 1000, 5)]
    [InlineData(500, 1000, 3)]
    [InlineData(1, 1000, 1)]
    [InlineData(100, 0, 0)]
    public void Rating_compares_with_the_best_seller(decimal sales, decimal best, int expected) =>
        Assert.Equal(expected, ReportsController.Rating(sales, best));

    // ── Exports ───────────────────────────────────────────────────

    [Fact]
    public async Task Kpi_csv()
    {
        using var ctx = await ReportContext();
        var csv = CsvText(await Reports(ctx).Export("kpis", "csv", null, null, null, null, null));
        var lines = csv.Split(Environment.NewLine).ToList();
        Assert.StartsWith("Key Performance Indicators,", lines[0]);
        Assert.Contains("Total Monthly Sales,3200.00", lines);
        Assert.Contains("Change vs Last Month,100.0%", lines);
        Assert.Contains("Stall Occupancy,1/5 (20%)", lines);
        Assert.Contains("Avg Attendance %,Not available", lines);
        Assert.Contains("Low Stock Items,1", lines);
    }

    [Theory]
    [InlineData("sales-trend", "Monthly Sales Trend")]
    [InlineData("category-revenue", "Category-wise Revenue")]
    [InlineData("occupancy-trend", "Stall Occupancy Over Time")]
    [InlineData("stalls", "Stall Occupancy Report")]
    [InlineData("artisans", "Artisan Performance Report")]
    [InlineData("applications", "Applications Report")]
    [InlineData("inventory", "Inventory Report")]
    [InlineData("sales", "Sales Report")]
    [InlineData("ALL", "Reports & Analytics")]
    public async Task Every_report_exports_as_csv(string id, string title)
    {
        using var ctx = await ReportContext();
        var file = Assert.IsType<FileContentResult>(await Reports(ctx).Export(id, null, null, null, null, null, null));
        Assert.StartsWith($"report_{id.ToLowerInvariant()}_{MonthStart:yyyyMM}_", file.FileDownloadName);
        Assert.StartsWith(title + ",", CsvText(file));
    }

    [Fact]
    public async Task Export_all_contains_every_section()
    {
        using var ctx = await ReportContext();
        var lines = CsvText(await Reports(ctx).Export("all", "csv", null, null, null, null, null)).Split(Environment.NewLine).ToList();
        foreach (var section in new[] { "Key Performance Indicators", "Monthly Sales Trend", "Category-wise Revenue", "Stall Occupancy Over Time",
                     "Stall Occupancy Report", "Artisan Performance Report", "Applications Summary", "Applications Report", "Inventory Report", "Daily Sales", "Stall-wise Sales" })
            Assert.Contains(section, lines);
        Assert.Contains("PRD002,Pot,Pottery,A-02,1,10,200.00,200.00,Low Stock", lines);
        Assert.Contains($"{Today:yyyy-MM-dd},2,4,3200.00", lines);
    }

    [Fact]
    public async Task Stall_and_artisan_exports_follow_their_filters()
    {
        using var ctx = await ReportContext();
        var stalls = CsvText(await Reports(ctx).Export("stalls", "csv", null, null, null, "OCCUPIED", null));
        Assert.Contains($"A-01,A,ART-0001,Kamala Rajendran,Textiles,3000.00,1,5,{ThisMonth.Days}/{ThisMonth.Days},Occupied", stalls);
        Assert.DoesNotContain("A-02,", stalls);

        var artisans = CsvText(await Reports(ctx).Export("artisans", "csv", null, null, "kamala", null, null));
        Assert.Contains("ART-0001,Kamala Rajendran,Hyderabad,Textiles,A-01,3000.00,3,1,93.8%,5/5,Active", artisans);
        Assert.DoesNotContain("Saroja", artisans);
    }

    [Fact]
    public async Task Pdf_export_is_the_print_view()
    {
        using var ctx = await ReportContext();
        var view = Assert.IsType<ViewResult>(await Reports(ctx, "boss@imsb").Export("stalls", "PDF", null, null, null, null, null));
        Assert.Equal("Print", view.ViewName);
        var doc = Assert.IsType<ReportDocument>(view.Model);
        Assert.Equal(("Stall Occupancy Report", "boss@imsb", 5), (doc.Title, doc.GeneratedBy, doc.Tables.Single().Rows.Count));
        Assert.Contains(5, doc.Tables[0].NumericColumns);
    }

    [Fact]
    public async Task Unknown_report_is_not_found()
    {
        using var ctx = await ReportContext();
        Assert.IsType<NotFoundResult>(await Reports(ctx).Export("procurement", "csv", null, null, null, null, null));
    }

    [Fact]
    public async Task Exports_are_localized()
    {
        using var ctx = await ReportContext();
        using var _ = new TestHelpers.CultureScope("hi-IN");
        var csv = CsvText(await Reports(ctx).Export("kpis", "csv", null, null, null, null, null));
        Assert.Contains(TestHelpers.SharedLocalizer()["TotalMonthlySales"].Value + ",3200.00", csv);
        Assert.DoesNotContain("Total Monthly Sales", csv);
    }

    [Fact]
    public async Task Index_offers_twelve_months_and_categories()
    {
        using var ctx = await ReportContext();
        var view = Assert.IsType<ViewResult>(await Reports(ctx).Index());
        var months = Assert.IsType<List<KeyValuePair<string, string>>>(view.ViewData["Months"]);
        Assert.Equal((12, MonthStart.ToString("yyyy-MM")), (months.Count, months[0].Key));
        Assert.IsType<ReportKpis>(view.ViewData["Kpis"]);
        Assert.Equal(7, Assert.IsType<List<KeyValuePair<string, string>>>(view.ViewData["Categories"]).Count);
    }
}
