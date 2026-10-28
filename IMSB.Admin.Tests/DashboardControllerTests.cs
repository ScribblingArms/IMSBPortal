using System.Text.Json;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Admin.Tests.SalesTestHelpers;

namespace IMSB.Admin.Tests;

public class DashboardControllerTests
{
    // ── Stat cards ────────────────────────────────────────────────

    [Fact]
    public async Task Stats_come_from_inventory_sales_and_staff()
    {
        using var ctx = SeededContext();   // 4 products in 4 stalls; dupatta low, pot critical, oil out
        ctx.Staff.AddRange(
            TestHelpers.ExistingStaff("EMP-0001", "Meena", "9000000001"),
            TestHelpers.ExistingStaff("EMP-0002", "Radha", "9000000002", status: "ON_LEAVE"));
        ctx.SaveChanges();
        var sales = CreateSales(ctx);
        await Sell(sales, DateTime.Today.AddDays(-1), "10:00", Item("PRD001", 1, 1000));
        await Sell(sales, null, "00:00", Item("PRD001", 1, 1250));

        var s = await CreateDashboard(ctx).ComputeStatsAsync();

        Assert.Equal(TotalStalls, s.TotalStalls);
        Assert.Equal(4, s.OccupiedStalls);
        Assert.Equal(6, s.VacantStalls);
        Assert.Equal(40, s.OccupancyPct);
        Assert.Equal(1250m, s.TodaySales);
        Assert.Equal(25.0m, s.SalesChangePct);
        Assert.Equal(3, s.LowStockItems);
        Assert.Equal(1, s.OutOfStockItems);
        Assert.Equal((2, 1), (s.TotalStaff, s.ActiveStaff));
        Assert.Equal(0, s.TotalArtisans);   // counted from the artisans table
        Assert.Equal(0, s.PendingApplications);
    }

    [Fact]
    public async Task Pending_applications_count_new_and_under_review()
    {
        using var ctx = SeededContext();
        ApplicationTestHelpers.AddApplication(ctx, "APP-0001", ApplicationStatuses.Pending);
        ApplicationTestHelpers.AddApplication(ctx, "APP-0002", ApplicationStatuses.UnderReview);
        ApplicationTestHelpers.AddApplication(ctx, "APP-0003", ApplicationStatuses.Approved, decidedAt: DateTime.UtcNow);
        ApplicationTestHelpers.AddApplication(ctx, "APP-0004", ApplicationStatuses.Approved, decidedAt: DateTime.UtcNow.AddMonths(-2));
        ApplicationTestHelpers.AddApplication(ctx, "APP-0005", ApplicationStatuses.Rejected, decidedAt: DateTime.UtcNow);

        Assert.Equal(2, (await CreateDashboard(ctx).ComputeStatsAsync()).PendingApplications);
        var apps = TestHelpers.OkJson(await CreateDashboard(ctx).Data(null)).GetProperty("applications");
        // "approved" counts this month's approvals only
        Assert.Equal((1, 1, 1), (apps.GetProperty("approved").GetInt32(), apps.GetProperty("review").GetInt32(), apps.GetProperty("pending").GetInt32()));
    }

    [Fact]
    public async Task Occupied_stalls_ignore_case_and_never_exceed_capacity()
    {
        using var ctx = InventoryTestHelpers.CreateContext();
        InventoryTestHelpers.AddProduct(ctx, "PRD001", "A", InventoryTestHelpers.Textiles, "a-01", 5, 1);
        InventoryTestHelpers.AddProduct(ctx, "PRD002", "B", InventoryTestHelpers.Textiles, "A-01", 5, 1);
        InventoryTestHelpers.AddProduct(ctx, "PRD003", "C", InventoryTestHelpers.Textiles, "B-02", 5, 1);

        var s = await CreateDashboard(ctx, totalStalls: 1).ComputeStatsAsync();
        Assert.Equal((2, 2, 0, 100), (s.TotalStalls, s.OccupiedStalls, s.VacantStalls, s.OccupancyPct));
    }

    [Fact]
    public async Task Empty_database_gives_zeros_not_errors()
    {
        using var ctx = TestHelpers.CreateContext(seedReferenceData: false);
        var s = await CreateDashboard(ctx).ComputeStatsAsync();
        Assert.Equal((TotalStalls, 0, TotalStalls, 0), (s.TotalStalls, s.OccupiedStalls, s.VacantStalls, s.OccupancyPct));
        Assert.Equal(0m, s.TodaySales);
        Assert.Null(s.SalesChangePct);

        var json = TestHelpers.OkJson(await CreateDashboard(ctx).Data(null));
        Assert.Empty(json.GetProperty("topStalls").EnumerateArray());
        Assert.Empty(json.GetProperty("lowStock").EnumerateArray());
        Assert.Empty(json.GetProperty("activity").EnumerateArray());
    }

    [Fact]
    public async Task Index_renders_with_stats_and_localized_title()
    {
        using var ctx = SeededContext();
        var view = Assert.IsType<ViewResult>(await CreateDashboard(ctx).Index());
        Assert.IsType<DashboardStats>(view.ViewData["Stats"]);
        Assert.Equal("Operations Dashboard", view.ViewData["Title"]);
    }

    // ── Data ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, 40)]
    [InlineData(40, 40)]
    [InlineData(90, 90)]
    [InlineData(180, 180)]
    [InlineData(7, 40)]
    [InlineData(100000, 40)]
    [InlineData(-5, 40)]
    public void Trend_period_is_one_of_the_offered_options(int? requested, int expected) =>
        Assert.Equal(expected, DashboardController.NormalizePeriod(requested));

    [Fact]
    public async Task Data_sales_trend_is_zero_filled_and_ends_today()
    {
        using var ctx = SeededContext();
        var sales = CreateSales(ctx);
        await Sell(sales, DateTime.Today.AddDays(-3), "10:00", Item("PRD001", 1, 500));
        await Sell(sales, DateTime.Today.AddDays(-3), "15:00", Item("PRD002", 1, 250));
        await Sell(sales, null, "00:00", Item("PRD001", 1, 100));

        var json = TestHelpers.OkJson(await CreateDashboard(ctx).Data(90));
        Assert.Equal(90, json.GetProperty("period").GetInt32());
        var labels = json.GetProperty("salesTrend").GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
        var values = json.GetProperty("salesTrend").GetProperty("values").EnumerateArray().Select(x => x.GetDecimal()).ToList();
        Assert.Equal(90, labels.Count);
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), labels[^1]);
        Assert.Equal(DateTime.Today.AddDays(-89).ToString("yyyy-MM-dd"), labels[0]);
        Assert.Equal(100m, values[^1]);
        Assert.Equal(750m, values[^4]);
        Assert.Equal(850m, values.Sum());
    }

    [Fact]
    public async Task Data_has_occupancy_category_sales_and_empty_modules()
    {
        using var ctx = SeededContext();
        await Sell(CreateSales(ctx), null, "00:00", Item("PRD003", 2, 400));

        var json = TestHelpers.OkJson(await CreateDashboard(ctx).Data(null));
        Assert.Equal((4, 6), (json.GetProperty("occupancy").GetProperty("occupied").GetInt32(), json.GetProperty("occupancy").GetProperty("vacant").GetInt32()));

        var cat = json.GetProperty("categorySales");
        Assert.Equal(DateTime.Today.ToString("yyyy-MM"), cat.GetProperty("month").GetString());
        var labels = cat.GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Equal(800m, cat.GetProperty("values")[labels.IndexOf("Pottery")].GetDecimal());

        // attendance has no table yet; applications are always reported
        foreach (var module in new[] { "attendance", "deptAttendance" })
            Assert.Equal(JsonValueKind.Null, json.GetProperty(module).ValueKind);
        Assert.Equal(JsonValueKind.Object, json.GetProperty("applications").ValueKind);
    }

    [Fact]
    public async Task Data_low_stock_is_most_urgent_first()
    {
        using var ctx = SeededContext();
        var rows = TestHelpers.OkJson(await CreateDashboard(ctx).Data(null)).GetProperty("lowStock").EnumerateArray().ToList();
        Assert.Equal(new[] { "PRD004", "PRD003", "PRD002" }, rows.Select(r => r.GetProperty("code").GetString()));
        Assert.Equal(("CRITICAL", 0), (rows[0].GetProperty("urgency").GetString(), rows[0].GetProperty("pct").GetInt32()));
        Assert.Equal(("MODERATE", 75), (rows[2].GetProperty("urgency").GetString(), rows[2].GetProperty("pct").GetInt32()));
        Assert.Equal("Herbal", rows[0].GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task Data_low_stock_shows_at_most_five()
    {
        using var ctx = InventoryTestHelpers.CreateContext();
        for (int i = 1; i <= 7; i++) InventoryTestHelpers.AddProduct(ctx, $"PRD{i:D3}", $"P{i}", InventoryTestHelpers.Textiles, "A-01", i, 10);
        var rows = TestHelpers.OkJson(await CreateDashboard(ctx).Data(null)).GetProperty("lowStock");
        Assert.Equal(5, rows.GetArrayLength());
    }

    [Fact]
    public async Task Data_ranks_stalls_for_the_month()
    {
        using var ctx = SeededContext();
        var sales = CreateSales(ctx);
        await Sell(sales, null, "00:00", Item("PRD001", 1, 9000), Item("PRD002", 1, 3000));
        var json = TestHelpers.OkJson(await CreateDashboard(ctx).Data(null));

        var top = json.GetProperty("topStalls").EnumerateArray().ToList();
        Assert.Equal(new[] { "A-01", "A-03" }, top.Select(s => s.GetProperty("stallCode").GetString()));
        Assert.Equal((100, 33), (top[0].GetProperty("pct").GetInt32(), top[1].GetProperty("pct").GetInt32()));
        Assert.Equal("Textiles", top[0].GetProperty("category").GetString());

        // occupied stalls without sales are the low performers; never the same stall twice
        var low = json.GetProperty("lowStalls").EnumerateArray().Select(s => s.GetProperty("stallCode").GetString()).ToList();
        Assert.Equal(new[] { "C-03", "F-02" }, low);
    }

    [Fact]
    public void Ranking_splits_top_and_low_without_overlap()
    {
        var stalls = Enumerable.Range(1, 8).Select(i => new SalesAnalytics.StallPerformance($"S-{i:D2}", "Textiles", i * 100, i)).ToList();
        var (top, low) = DashboardController.RankStalls(stalls);
        Assert.Equal(new[] { "S-08", "S-07", "S-06", "S-05", "S-04" }, top.Select(s => s.StallCode));
        Assert.Equal(new[] { "S-01", "S-02", "S-03" }, low.Select(s => s.StallCode));
        Assert.Equal(new[] { 13, 25, 38 }, low.Select(s => s.Pct));
    }

    [Fact]
    public void Ranking_with_no_sales_puts_everything_in_low_at_zero()
    {
        var stalls = new[] { "B", "A" }.Select(s => new SalesAnalytics.StallPerformance(s, "", 0, 0)).ToList();
        var (top, low) = DashboardController.RankStalls(stalls);
        Assert.Empty(top);
        Assert.Equal(new[] { ("A", 0), ("B", 0) }, low.Select(s => (s.StallCode, s.Pct)));
    }

    [Fact]
    public async Task Activity_merges_sales_stock_and_staff_newest_first()
    {
        using var ctx = SeededContext();
        ctx.Staff.Add(TestHelpers.ExistingStaff("EMP-0007", "Lakshmi", "9000000007", createdAt: DateTime.UtcNow.AddMinutes(-30)));
        ctx.StockMovements.Add(new StockMovement { ProductCode = "PRD001", ProductName = "Saree", MovementType = StockMovementTypes.Add, Quantity = 4, QuantityBefore = 1, QuantityAfter = 5, CreatedAt = DateTime.UtcNow.AddMinutes(-10), MovementDate = DateTime.Today });
        ctx.SaveChanges();
        await Sell(CreateSales(ctx, "cashier"), null, "00:00", Item("PRD002", 2, 600));

        var activity = TestHelpers.OkJson(await CreateDashboard(ctx).Data(null)).GetProperty("activity").EnumerateArray().ToList();
        Assert.Equal(new[] { "SALE", "STOCK_ADD", "STAFF_ADDED" }, activity.Select(a => a.GetProperty("type").GetString()));
        Assert.Equal(("SAL-000001", 1200m, 2, "cashier"), (activity[0].GetProperty("subject").GetString(), activity[0].GetProperty("amount").GetDecimal(),
            activity[0].GetProperty("quantity").GetInt32(), activity[0].GetProperty("by").GetString()));
        Assert.Equal(("Lakshmi", "EMP-0007"), (activity[2].GetProperty("subject").GetString(), activity[2].GetProperty("reference").GetString()));
        // the sale's own stock deduction is not listed separately
        Assert.DoesNotContain(activity, a => a.GetProperty("type").GetString()!.Contains("SALE_"));
        // times are UTC so the browser can show "x min ago"
        Assert.EndsWith("Z", activity[0].GetProperty("at").GetString());
    }

    // ── Export ────────────────────────────────────────────────────

    [Fact]
    public async Task Export_contains_every_section()
    {
        using var ctx = SeededContext();
        await Sell(CreateSales(ctx), null, "00:00", Item("PRD001", 2, 8500));

        var csv = CsvText(await CreateDashboard(ctx).Export(null));
        var lines = csv.Split(Environment.NewLine).ToList();

        Assert.Equal($"Operations Dashboard,{DateTime.Today:yyyy-MM-dd}", lines[0]);
        Assert.Contains("Total Stalls,10", lines);
        Assert.Contains("Occupied Stalls,4", lines);
        Assert.Contains("Vacant Stalls,6", lines);
        Assert.Contains("Occupancy Rate,40%", lines);
        Assert.Contains("Total Artisans,0", lines);
        Assert.Contains("Today's Sales,17000.00", lines);
        Assert.Contains("Low Stock Items,3", lines);
        Assert.Contains("Pending Applications,0", lines);
        Assert.Contains("Change vs Yesterday,Not available", lines);
        Assert.DoesNotContain("{0}", csv);   // no format strings used as plain labels

        Assert.Contains("Sales Trend,Last 40 Days", lines);
        Assert.Contains($"{DateTime.Today:yyyy-MM-dd},17000.00", lines);
        Assert.Contains($"Category-wise Sales,{DateTime.Today:yyyy-MM}", lines);
        Assert.Contains("Textiles,2,17000.00", lines);
        Assert.Contains("A-01,Textiles,2,17000.00", lines);
        Assert.Contains("Low Stock Alerts", lines);
        Assert.Contains("PRD004,Jasmine Essential Oil,Herbal,F-02,0,50", lines);

        // the trend section has exactly one row per day
        var start = lines.IndexOf("Sales Trend,Last 40 Days") + 2;
        Assert.Equal(40, lines.Skip(start).TakeWhile(l => l != "").Count());
    }

    [Fact]
    public async Task Export_period_and_file_name()
    {
        using var ctx = SeededContext();
        var file = Assert.IsType<FileContentResult>(await CreateDashboard(ctx).Export(180));
        Assert.Equal($"dashboard_report_{DateTime.Today:yyyyMMdd}.xls", file.FileDownloadName);
        Assert.Contains("Sales Trend,Last 180 Days", CsvText(file));
    }

    [Fact]
    public async Task Export_is_localized()
    {
        using var ctx = SeededContext();
        using var _ = new TestHelpers.CultureScope("ta-IN");
        var csv = CsvText(await CreateDashboard(ctx).Export(null));
        Assert.Contains("மொத்த ஸ்டால்கள்,10", csv);
        Assert.Contains("மொத்த கலைஞர்கள்,0", csv);
    }
}

public class SalesAnalyticsTests
{
    [Theory]
    [InlineData(150, 100, 50.0)]
    [InlineData(50, 100, -50.0)]
    [InlineData(100, 100, 0.0)]
    [InlineData(1, 3, -66.7)]
    public void Percent_change(decimal current, decimal previous, decimal expected) =>
        Assert.Equal(expected, SalesAnalytics.PercentChange(current, previous));

    [Fact]
    public void Percent_change_without_a_baseline_is_null() => Assert.Null(SalesAnalytics.PercentChange(500, 0));

    [Fact]
    public async Task Month_to_date_compares_with_the_same_span_last_month()
    {
        using var ctx = SeededContext();
        var sales = CreateSales(ctx);
        var today = DateTime.Today;
        var lastMonth = today.AddMonths(-1);
        var lastMonthSameDay = new DateTime(lastMonth.Year, lastMonth.Month, Math.Min(today.Day, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month)));
        await Sell(sales, lastMonthSameDay, "10:00", Item("PRD001", 1, 400));
        if (lastMonthSameDay.Day < DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month))
            await Sell(sales, lastMonthSameDay.AddDays(1), "10:00", Item("PRD001", 1, 9999));   // after the span: ignored
        await Sell(sales, today, "00:00", Item("PRD002", 1, 600));

        var s = await SalesAnalytics.SummaryAsync(ctx, today, 10);
        Assert.Equal(600m, s.MonthSales);
        Assert.Equal(400m, s.PreviousMonthSales);
        Assert.Equal(50.0m, s.ChangeVsPreviousMonth);
    }

    [Fact]
    public async Task Hourly_covers_business_hours_and_widens_for_late_sales()
    {
        using var ctx = SeededContext();
        var day = DateTime.Today.AddDays(-1);
        var empty = await SalesAnalytics.HourlyAsync(ctx, day);
        Assert.Equal(Enumerable.Range(8, 13), empty.Select(h => h.Hour));

        await Sell(CreateSales(ctx), day, "22:10", Item("PRD001", 1, 300));
        await Sell(CreateSales(ctx), day, "22:50", Item("PRD002", 1, 200));
        var hourly = await SalesAnalytics.HourlyAsync(ctx, day);
        Assert.Equal(8, hourly.First().Hour);
        Assert.Equal((22, 500m), hourly.Last());
    }

    [Fact]
    public async Task Category_snapshot_names_outside_the_list_are_kept()
    {
        using var ctx = SeededContext();
        await Sell(CreateSales(ctx), null, "00:00", Item("PRD001", 1, 100));
        ctx.SaleItems.Single().CategoryName = "Retired Category";
        ctx.SaveChanges();
        var rows = await SalesAnalytics.ByCategoryAsync(ctx, DateTime.Today, DateTime.Today.AddDays(1));
        Assert.Equal(("Retired Category", 100m, 1), rows.Last());
        Assert.Equal(0m, rows.Single(r => r.Category == "Textiles").Revenue);
    }
}
