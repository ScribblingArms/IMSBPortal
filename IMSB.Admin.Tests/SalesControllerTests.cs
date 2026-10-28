using System.Text.Json;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IMSB.Admin.Tests.SalesTestHelpers;

namespace IMSB.Admin.Tests;

public class SalesControllerTests
{
    private static int Stock(IMSBContext ctx, string code) => ctx.Products.AsNoTracking().Single(p => p.ProductCode == code).Quantity;

    private static List<StockMovement> Movements(IMSBContext ctx, string code) =>
        ctx.StockMovements.AsNoTracking().Where(m => m.ProductCode == code).OrderBy(m => m.CreatedAt).ToList();

    // ── Create + auto stock deduction ─────────────────────────────

    [Fact]
    public async Task Create_saves_entry_and_deducts_stock_with_history()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx, "cashier@imsb");

        var result = await c.Create(Entry(remarks: "  festival  ", items: new[] { Item("PRD001", 2, 8000), Item("PRD002", 5, 1200) }));

        var json = TestHelpers.OkJson(result);
        Assert.Equal("SAL-000001", json.GetProperty("entryNo").GetString());
        Assert.Equal(22000m, json.GetProperty("total").GetDecimal());

        var entry = ctx.SalesEntries.Include(e => e.Items).Single();
        Assert.Equal(7, entry.TotalQuantity);
        Assert.Equal("festival", entry.Remarks);
        Assert.Equal("cashier@imsb", entry.CreatedBy);
        Assert.Equal(DateTime.Today, entry.SoldAt.Date);
        var saree = entry.Items.Single(i => i.ProductCode == "PRD001");
        Assert.Equal(("Kanjivaram Silk Saree", "Textiles", "A-01", 8000m, 16000m), (saree.ProductName, saree.CategoryName, saree.StallCode, saree.UnitPrice, saree.LineTotal));

        Assert.Equal(43, Stock(ctx, "PRD001"));
        Assert.Equal(10, Stock(ctx, "PRD002"));
        var m = Assert.Single(Movements(ctx, "PRD001"));
        Assert.Equal((StockMovementTypes.Sale, 2, 45, 43, "SAL-000001"), (m.MovementType, m.Quantity, m.QuantityBefore, m.QuantityAfter, m.Remarks));
    }

    [Fact]
    public async Task Entry_numbers_increase()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        Assert.Equal("SAL-000001", await Sell(c, null, "00:00", Item("PRD001", 1, 100)));
        Assert.Equal("SAL-000002", await Sell(c, null, "00:00", Item("PRD001", 1, 100)));
    }

    [Fact]
    public async Task Selling_the_whole_stock_is_allowed_but_not_more()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);

        var errors = TestHelpers.Errors(await c.Create(Entry(items: Item("PRD003", 4, 450))));
        Assert.Equal("Not enough stock: only 3 available", errors["items_0_quantity"]);
        Assert.Equal(3, Stock(ctx, "PRD003"));
        Assert.Empty(ctx.SalesEntries);

        await Sell(c, null, "00:00", Item("PRD003", 3, 450));
        Assert.Equal(0, Stock(ctx, "PRD003"));
    }

    [Fact]
    public async Task Out_of_stock_product_cannot_be_sold()
    {
        using var ctx = SeededContext();
        var errors = TestHelpers.Errors(await CreateSales(ctx).Create(Entry(items: Item("PRD004", 1, 320))));
        Assert.Equal("Not enough stock: only 0 available", errors["items_0_quantity"]);
    }

    [Fact]
    public async Task Failed_validation_changes_nothing()
    {
        using var ctx = SeededContext();
        // first line is fine, second is not: the whole entry is rejected
        var result = await CreateSales(ctx).Create(Entry(items: new[] { Item("PRD001", 1, 8500), Item("PRD003", 99, 450) }));
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(ctx.SalesEntries);
        Assert.Empty(ctx.StockMovements);
        Assert.Equal(45, Stock(ctx, "PRD001"));
    }

    [Theory]
    [InlineData("", 1, 10, "items_0_product_code", "Select a product")]
    [InlineData("PRD999", 1, 10, "items_0_product_code", "Select a product")]
    [InlineData("PRD001", 0, 10, "items_0_quantity", "Quantity must be at least 1")]
    [InlineData("PRD001", -2, 10, "items_0_quantity", "Quantity must be at least 1")]
    [InlineData("PRD001", 1, -1, "items_0_unit_price", "Enter a valid price")]
    public async Task Line_validation(string code, int qty, decimal price, string key, string message)
    {
        using var ctx = SeededContext();
        var errors = TestHelpers.Errors(await CreateSales(ctx).Create(Entry(items: Item(code, qty, price))));
        Assert.Equal(message, errors[key]);
    }

    [Fact]
    public async Task The_same_product_twice_is_rejected_on_the_second_row()
    {
        using var ctx = SeededContext();
        var errors = TestHelpers.Errors(await CreateSales(ctx).Create(Entry(items: new[] { Item("PRD001", 1, 1), Item("PRD002", 1, 1), Item("PRD001", 1, 1) })));
        Assert.Equal(new[] { "items_2_product_code" }, errors.Keys);
    }

    [Fact]
    public async Task An_entry_needs_at_least_one_product()
    {
        using var ctx = SeededContext();
        var errors = TestHelpers.Errors(await CreateSales(ctx).Create(Entry()));
        Assert.Equal("Add at least one product", errors["items"]);
    }

    [Fact]
    public async Task An_entry_has_at_most_100_products()
    {
        using var ctx = SeededContext();
        var items = Enumerable.Range(0, 101).Select(_ => Item("PRD001", 1, 1)).ToArray();
        var errors = TestHelpers.Errors(await CreateSales(ctx).Create(Entry(items: items)));
        Assert.Equal("An entry can have at most 100 products", errors["items"]);
    }

    [Theory]
    [InlineData("2026-13-01", "Invalid date")]
    [InlineData("08/10/2026", "Invalid date")]
    public async Task Invalid_date_is_rejected(string date, string message)
    {
        using var ctx = SeededContext();
        var dto = Entry(items: Item("PRD001", 1, 1));
        dto.Date = date;
        Assert.Equal(message, TestHelpers.Errors(await CreateSales(ctx).Create(dto))["date"]);
    }

    [Fact]
    public async Task Future_date_is_rejected()
    {
        using var ctx = SeededContext();
        var errors = TestHelpers.Errors(await CreateSales(ctx).Create(Entry(DateTime.Today.AddDays(1), items: Item("PRD001", 1, 1))));
        Assert.Equal("Date cannot be in the future", errors["date"]);
    }

    [Fact]
    public async Task Future_time_today_is_rejected()
    {
        if (DateTime.Now.Hour == 23 && DateTime.Now.Minute == 59) return;   // no later minute left today
        using var ctx = SeededContext();
        var errors = TestHelpers.Errors(await CreateSales(ctx).Create(Entry(time: "23:59", items: Item("PRD001", 1, 1))));
        Assert.Equal("Time cannot be in the future", errors["time"]);
    }

    [Fact]
    public async Task Earlier_dates_default_to_noon_and_keep_a_given_time()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        var yesterday = DateTime.Today.AddDays(-1);
        await Sell(c, yesterday, null, Item("PRD001", 1, 1));
        await Sell(c, yesterday, "18:45", Item("PRD002", 1, 1));
        var times = ctx.SalesEntries.OrderBy(e => e.EntryNo).Select(e => e.SoldAt).ToList();
        Assert.Equal(new[] { yesterday.AddHours(12), yesterday.AddHours(18).AddMinutes(45) }, times);
        // the stock history is dated on the sale date
        Assert.Equal(yesterday, Movements(ctx, "PRD001").Single().MovementDate);
    }

    [Fact]
    public async Task Null_body_is_bad_request()
    {
        using var ctx = SeededContext();
        Assert.IsType<BadRequestResult>(await CreateSales(ctx).Create(null));
        Assert.IsType<BadRequestResult>(await CreateSales(ctx).Update("SAL-000001", null));
    }

    [Fact]
    public async Task Model_state_errors_keep_the_row_index()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        c.ModelState.AddModelError("Items[1].UnitPrice", "Enter a valid price");
        c.ModelState.AddModelError("Remarks", "Remarks can be at most 500 characters");
        var errors = TestHelpers.Errors(await c.Create(Entry(items: new[] { Item("PRD001", 1, 1), Item("PRD002", 1, 1) })));
        Assert.Equal("Enter a valid price", errors["items_1_unit_price"]);
        Assert.Equal("Remarks can be at most 500 characters", errors["remarks"]);
    }

    [Theory]
    [InlineData("Date", "date")]
    [InlineData("Items[0].ProductCode", "items_0_product_code")]
    [InlineData("Items[12].UnitPrice", "items_12_unit_price")]
    [InlineData("dto.Time", "time")]
    [InlineData("$.items[0].quantity", "items")]
    public void Error_keys_match_the_form(string modelStateKey, string expected) =>
        Assert.Equal(expected, IMSB.Common.CommonHelpers.ToErrorKey(modelStateKey));

    [Fact]
    public void Dto_annotations_use_resource_keys()
    {
        var dto = new SalesEntryDto { Date = "", Time = "25:00", Remarks = new string('x', 501) };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(dto, new(dto), results, true);
        Assert.Equal(new[] { "ValDate", "ValRemarks", "ValTime" }, results.Select(r => r.ErrorMessage).OrderBy(m => m));

        var item = new SaleItemDto { ProductCode = "", Quantity = 0, UnitPrice = -1 };
        results.Clear();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(item, new(item), results, true);
        Assert.Equal(new[] { "ValQuantityPositive", "ValSelectProduct", "ValUnitPrice" }, results.Select(r => r.ErrorMessage).OrderBy(m => m));
    }

    // ── Update ────────────────────────────────────────────────────

    [Fact]
    public async Task Update_moves_only_the_net_difference_per_product()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx, "first@imsb");
        var no = await Sell(c, null, "00:00", Item("PRD001", 5, 8500), Item("PRD002", 4, 1200), Item("PRD003", 1, 450));

        c = CreateSales(ctx, "editor@imsb");
        // saree 5 -> 7 (sell 2 more), dupatta 4 -> 1 (3 back), pot removed (1 back)
        var result = await c.Update(no, Entry(remarks: "fixed", items: new[] { Item("PRD001", 7, 8000), Item("PRD002", 1, 1200) }));
        Assert.Equal(57200m, TestHelpers.OkJson(result).GetProperty("total").GetDecimal());

        Assert.Equal(38, Stock(ctx, "PRD001"));
        Assert.Equal(14, Stock(ctx, "PRD002"));
        Assert.Equal(3, Stock(ctx, "PRD003"));

        var saree = Movements(ctx, "PRD001");
        Assert.Equal(new[] { (StockMovementTypes.Sale, 5), (StockMovementTypes.Sale, 2) }, saree.Select(m => (m.MovementType, m.Quantity)));
        var dupatta = Movements(ctx, "PRD002");
        Assert.Equal(new[] { (StockMovementTypes.Sale, 4), (StockMovementTypes.SaleReversal, 3) }, dupatta.Select(m => (m.MovementType, m.Quantity)));
        Assert.Equal((StockMovementTypes.SaleReversal, 1, 2, 3), Movements(ctx, "PRD003").Select(m => (m.MovementType, m.Quantity, m.QuantityBefore, m.QuantityAfter)).Last());

        var entry = ctx.SalesEntries.Include(e => e.Items).AsNoTracking().Single();
        Assert.Equal(2, entry.Items.Count);
        Assert.Equal(8, entry.TotalQuantity);
        Assert.Equal("fixed", entry.Remarks);
        Assert.Equal("editor@imsb", entry.UpdatedBy);
        Assert.Equal(2, ctx.SaleItems.Count());   // old lines are gone
    }

    [Fact]
    public async Task Unchanged_quantities_write_no_stock_movements()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        var no = await Sell(c, null, "00:00", Item("PRD001", 2, 8500));
        await c.Update(no, Entry(items: Item("PRD001", 2, 7000)));   // price change only
        Assert.Single(Movements(ctx, "PRD001"));
        Assert.Equal(14000m, ctx.SalesEntries.Single().TotalAmount);
    }

    [Fact]
    public async Task Update_counts_the_entrys_own_quantity_as_available()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        var no = await Sell(c, null, "00:00", Item("PRD003", 3, 450));   // stock now 0
        Assert.Equal(0, Stock(ctx, "PRD003"));

        // keeping (or lowering) the 3 already sold is fine
        Assert.IsType<OkObjectResult>(await c.Update(no, Entry(items: Item("PRD003", 3, 400))));
        // asking for more than stock + already sold is not
        var errors = TestHelpers.Errors(await c.Update(no, Entry(items: Item("PRD003", 4, 400))));
        Assert.Equal("Not enough stock: only 3 available", errors["items_0_quantity"]);
    }

    [Fact]
    public async Task Update_can_move_the_sale_to_another_date()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        var no = await Sell(c, null, "00:00", Item("PRD001", 1, 1));
        var earlier = DateTime.Today.AddDays(-3);
        await c.Update(no, Entry(earlier, "09:15", items: Item("PRD001", 1, 1)));
        Assert.Equal(earlier.AddHours(9).AddMinutes(15), ctx.SalesEntries.Single().SoldAt);
    }

    [Fact]
    public async Task Update_and_delete_of_unknown_entry_are_not_found()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        Assert.IsType<NotFoundResult>(await c.Update("SAL-999999", Entry(items: Item("PRD001", 1, 1))));
        Assert.IsType<NotFoundResult>(await c.Delete("SAL-999999"));
        Assert.IsType<NotFoundResult>(await c.Get("SAL-999999"));
    }

    // ── Delete ────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_puts_stock_back_and_keeps_the_history()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        var no = await Sell(c, null, "00:00", Item("PRD001", 4, 8500), Item("PRD002", 2, 1200));

        var result = await c.Delete(no);
        Assert.Equal(no, TestHelpers.OkJson(result).GetProperty("entryNo").GetString());

        Assert.Empty(ctx.SalesEntries);
        Assert.Empty(ctx.SaleItems);
        Assert.Equal(45, Stock(ctx, "PRD001"));
        Assert.Equal(15, Stock(ctx, "PRD002"));
        Assert.Equal(new[] { StockMovementTypes.Sale, StockMovementTypes.SaleReversal }, Movements(ctx, "PRD001").Select(m => m.MovementType));
    }

    [Fact]
    public async Task Deleting_a_product_keeps_its_sales_and_their_entries_still_delete()
    {
        using var ctx = SeededContext();
        var sales = CreateSales(ctx);
        var no = await Sell(sales, null, "00:00", Item("PRD001", 1, 8500), Item("PRD002", 1, 1200));

        var inventory = InventoryTestHelpers.CreateController(ctx);
        Assert.IsType<OkObjectResult>(await inventory.Delete("PRD001"));

        var line = ctx.SaleItems.AsNoTracking().Single(i => i.ProductCode == "PRD001");
        Assert.Null(line.ProductId);
        Assert.Equal("Kanjivaram Silk Saree", line.ProductName);

        // the product report still lists it, without stock
        var report = TestHelpers.OkJson(await sales.ProductReport(null, null, null, null, null));
        var row = report.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "PRD001");
        Assert.Equal(JsonValueKind.Null, row.GetProperty("stockLeft").ValueKind);

        // deleting the entry restores only the product that still exists
        Assert.IsType<OkObjectResult>(await sales.Delete(no));
        Assert.Equal(15, Stock(ctx, "PRD002"));
    }

    // ── Data / summary / charts ───────────────────────────────────

    [Fact]
    public async Task Data_returns_summary_entries_charts_and_options()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        var yesterday = DateTime.Today.AddDays(-1);
        await Sell(c, yesterday, "10:00", Item("PRD002", 1, 1000));
        await Sell(c, null, "00:00", Item("PRD001", 1, 1000), Item("PRD003", 2, 250));
        await Sell(c, null, "00:30", Item("PRD002", 3, 500));

        var json = TestHelpers.OkJson(await c.Data(null, null));
        var s = json.GetProperty("summary");
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), json.GetProperty("date").GetString());
        Assert.Equal(3000m, s.GetProperty("totalSales").GetDecimal());
        Assert.Equal(6, s.GetProperty("itemsSold").GetInt32());
        Assert.Equal(2, s.GetProperty("transactions").GetInt32());
        Assert.Equal(3, s.GetProperty("activeStalls").GetInt32());   // A-01, C-03, A-03
        Assert.Equal(30, s.GetProperty("activeStallsPct").GetInt32());
        Assert.Equal(1000m, s.GetProperty("previousDaySales").GetDecimal());
        Assert.Equal(200m, s.GetProperty("changeVsPreviousDay").GetDecimal());
        Assert.Equal(1500m, s.GetProperty("averageTransaction").GetDecimal());

        // newest first
        Assert.Equal(new[] { "SAL-000003", "SAL-000002" }, json.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("entryNo").GetString()));

        var cat = json.GetProperty("categoryChart");
        var labels = cat.GetProperty("labels").EnumerateArray().Select(l => l.GetString()).ToList();
        var values = cat.GetProperty("values").EnumerateArray().Select(v => v.GetDecimal()).ToList();
        Assert.Equal(IMSBContext.SeedCategories.Select(x => x.Name), labels);   // every category, in order
        Assert.Equal(2500m, values[labels.IndexOf("Textiles")]);
        Assert.Equal(500m, values[labels.IndexOf("Pottery")]);
        Assert.Equal(0m, values[labels.IndexOf("Herbal")]);

        var hourly = json.GetProperty("hourlyChart");
        var hours = hourly.GetProperty("hours").EnumerateArray().Select(h => h.GetInt32()).ToList();
        Assert.Equal(0, hours.First());   // widened from 08:00 to include the midnight sales
        Assert.Equal(20, hours.Last());
        Assert.Equal(3000m, hourly.GetProperty("values")[0].GetDecimal());

        Assert.Equal(4, json.GetProperty("productOptions").GetArrayLength());
        Assert.Equal(new[] { "A-01", "A-03", "C-03", "F-02" }, json.GetProperty("stalls").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Data_for_an_earlier_date_keeps_today_cards_separate()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        var yesterday = DateTime.Today.AddDays(-1);
        await Sell(c, yesterday, "10:00", Item("PRD002", 2, 1000));
        await Sell(c, null, "00:00", Item("PRD001", 1, 500));

        var json = TestHelpers.OkJson(await c.Data(yesterday.ToString("yyyy-MM-dd"), null));
        Assert.Equal(2000m, json.GetProperty("summary").GetProperty("totalSales").GetDecimal());
        Assert.Equal(500m, json.GetProperty("today").GetProperty("totalSales").GetDecimal());
        Assert.Equal("SAL-000001", json.GetProperty("entries")[0].GetProperty("entryNo").GetString());
    }

    [Fact]
    public async Task Data_stall_filter_limits_entries()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        await Sell(c, null, "00:00", Item("PRD001", 1, 1));
        await Sell(c, null, "00:00", Item("PRD002", 1, 1));
        var json = TestHelpers.OkJson(await c.Data(null, "a-03"));
        Assert.Equal("SAL-000002", json.GetProperty("entries").EnumerateArray().Single().GetProperty("entryNo").GetString());
    }

    [Fact]
    public async Task Data_rejects_a_bad_date()
    {
        using var ctx = SeededContext();
        Assert.Equal("Invalid date", TestHelpers.Errors(await CreateSales(ctx).Data("yesterday", null))["date"]);
    }

    [Fact]
    public async Task Empty_day_has_zero_summary_and_no_comparison()
    {
        using var ctx = SeededContext();
        var s = TestHelpers.OkJson(await CreateSales(ctx).Data(null, null)).GetProperty("summary");
        Assert.Equal(0m, s.GetProperty("totalSales").GetDecimal());
        Assert.Equal(0, s.GetProperty("transactions").GetInt32());
        Assert.Equal(0m, s.GetProperty("averageTransaction").GetDecimal());
        Assert.Equal(JsonValueKind.Null, s.GetProperty("changeVsPreviousDay").ValueKind);
    }

    [Fact]
    public async Task Get_returns_the_entry_with_items()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        var no = await Sell(c, null, "00:00", Item("PRD001", 2, 100));
        var json = TestHelpers.OkJson(await c.Get(no));
        Assert.Equal("00:00", json.GetProperty("time").GetString());
        var item = json.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(("PRD001", 2, 200m), (item.GetProperty("code").GetString(), item.GetProperty("quantity").GetInt32(), item.GetProperty("lineTotal").GetDecimal()));
    }

    // ── Product-wise report ───────────────────────────────────────

    private static async Task<SalesController> ReportFixture(IMSBContext ctx)
    {
        var c = CreateSales(ctx);
        var d2 = DateTime.Today.AddDays(-2);
        await Sell(c, d2, "10:00", Item("PRD001", 1, 8000), Item("PRD003", 1, 450));
        await Sell(c, d2, "11:00", Item("PRD001", 2, 8500));
        await Sell(c, null, "00:00", Item("PRD002", 3, 1200));
        return c;
    }

    private static List<JsonElement> Rows(IActionResult r) => TestHelpers.OkJson(r).GetProperty("rows").EnumerateArray().ToList();

    [Fact]
    public async Task Report_groups_by_product_and_day()
    {
        using var ctx = SeededContext();
        var c = await ReportFixture(ctx);
        var json = TestHelpers.OkJson(await c.ProductReport(null, null, null, null, null));
        var rows = json.GetProperty("rows").EnumerateArray().ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("PRD002", rows[0].GetProperty("code").GetString());   // newest day first
        var saree = rows.Single(r => r.GetProperty("code").GetString() == "PRD001");
        Assert.Equal(3, saree.GetProperty("quantity").GetInt32());
        Assert.Equal(25000m, saree.GetProperty("revenue").GetDecimal());
        Assert.Equal(8333.33m, saree.GetProperty("unitPrice").GetDecimal());   // average realised price
        Assert.Equal(42, saree.GetProperty("stockLeft").GetInt32());
        Assert.Equal("Textiles", saree.GetProperty("categoryName").GetString());

        var totals = json.GetProperty("totals");
        Assert.Equal((7, 29050m, 3), (totals.GetProperty("quantity").GetInt32(), totals.GetProperty("revenue").GetDecimal(), totals.GetProperty("products").GetInt32()));
    }

    [Fact]
    public async Task Report_filters()
    {
        using var ctx = SeededContext();
        var c = await ReportFixture(ctx);
        string Codes(IActionResult r) => string.Join(",", Rows(r).Select(x => x.GetProperty("code").GetString()).OrderBy(x => x));
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var twoDaysAgo = DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd");

        Assert.Equal("PRD001", Codes(await c.ProductReport("silk saree", null, null, null, null)));
        Assert.Equal("PRD003", Codes(await c.ProductReport("prd003", null, null, null, null)));
        Assert.Equal("PRD001,PRD002", Codes(await c.ProductReport(null, InventoryTestHelpers.Textiles.ToString(), null, null, null)));
        Assert.Equal("PRD003", Codes(await c.ProductReport(null, null, "c-03", null, null)));
        Assert.Equal("PRD002", Codes(await c.ProductReport(null, null, null, today, today)));
        Assert.Equal("PRD001,PRD003", Codes(await c.ProductReport(null, null, null, null, twoDaysAgo)));
        Assert.Equal("PRD001", Codes(await c.ProductReport("saree", InventoryTestHelpers.Textiles.ToString(), "A-01", twoDaysAgo, today)));
        Assert.Equal("", Codes(await c.ProductReport("nothing like this", null, null, null, null)));
        // an unparseable category id is ignored rather than failing
        Assert.Equal(3, Rows(await c.ProductReport(null, "not-a-guid", null, null, null)).Count);
    }

    [Fact]
    public async Task Report_rejects_bad_ranges()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        Assert.Equal("The end date must be on or after the start date", TestHelpers.Errors(await c.ProductReport(null, null, null, "2026-05-10", "2026-05-01"))["to"]);
        Assert.Equal("Invalid date", TestHelpers.Errors(await c.ProductReport(null, null, null, "10-05-2026", null))["from"]);
        Assert.IsType<BadRequestObjectResult>(await c.ExportProductReport(null, null, null, "2026-05-10", "2026-05-01"));
        Assert.IsType<BadRequestObjectResult>(await c.Export("bad", null, null));
    }

    // ── Deduction log ─────────────────────────────────────────────

    [Fact]
    public async Task Deduction_log_lists_only_sale_movements_with_status()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        await InventoryTestHelpers.CreateController(ctx).UpdateStock(InventoryTestHelpers.Stock("PRD001", "ADD", 5));   // not a sale
        var no = await Sell(c, null, "00:00", Item("PRD002", 10, 1200));
        await c.Update(no, Entry(items: Item("PRD002", 8, 1200)));

        var rows = TestHelpers.OkJson(await c.DeductionLog(null)).EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("SALE_REVERSAL", rows[0].GetProperty("type").GetString());   // newest first
        var sale = rows[1];
        Assert.Equal(("SALE", 10, 15, 5), (sale.GetProperty("type").GetString(), sale.GetProperty("quantity").GetInt32(), sale.GetProperty("before").GetInt32(), sale.GetProperty("after").GetInt32()));
        Assert.Equal(no, sale.GetProperty("entryNo").GetString());
        Assert.Equal("A-03", sale.GetProperty("stallCode").GetString());
        Assert.Equal("LOW_STOCK", sale.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Deduction_log_date_filter()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        await Sell(c, DateTime.Today.AddDays(-1), "10:00", Item("PRD001", 1, 1));
        await Sell(c, null, "00:00", Item("PRD002", 1, 1));
        var rows = TestHelpers.OkJson(await c.DeductionLog(DateTime.Today.ToString("yyyy-MM-dd"))).EnumerateArray().ToList();
        Assert.Equal("PRD002", Assert.Single(rows).GetProperty("code").GetString());
        Assert.IsType<BadRequestObjectResult>(await c.DeductionLog("nope"));
    }

    // ── Exports ───────────────────────────────────────────────────

    [Fact]
    public async Task Export_lists_line_items_with_totals()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx, "=cmd@imsb");
        var no = await Sell(c, null, "00:00", Item("PRD001", 2, 8500), Item("PRD002", 1, 1199.5m));
        await Sell(c, DateTime.Today.AddDays(-1), "10:00", Item("PRD003", 1, 450));   // other day: not exported

        var csv = CsvText(await c.Export(null, null, null));
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Entry No,Date,Time,Product Code,Product Name,Category,Stall,Qty Sold,Unit Price (₹),Line Total,By", lines[0]);
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        Assert.Contains($"{no},{today},00:00,PRD001,Kanjivaram Silk Saree,Textiles,A-01,2,8500.00,17000.00,=cmd@imsb", lines);
        Assert.Contains($"{no},{today},00:00,PRD002,Silk Dupatta (Blue),Textiles,A-03,1,1199.50,1199.50,=cmd@imsb", lines);
        Assert.Equal("Total,,,,,,,3,,18199.50,", lines[^1]);
        Assert.Equal(4, lines.Length);
    }

    [Fact]
    public async Task Export_range_and_stall()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        var d1 = DateTime.Today.AddDays(-1);
        await Sell(c, d1, "10:00", Item("PRD001", 1, 1), Item("PRD003", 1, 1));
        await Sell(c, null, "00:00", Item("PRD001", 1, 1));
        var csv = CsvText(await c.Export(d1.ToString("yyyy-MM-dd"), DateTime.Today.ToString("yyyy-MM-dd"), "A-01"));
        Assert.Equal(2, csv.Split(Environment.NewLine).Count(l => l.Contains(",PRD001,")));
        Assert.DoesNotContain("PRD003", csv);
    }

    [Fact]
    public async Task Export_product_report_follows_filters()
    {
        using var ctx = SeededContext();
        var c = await ReportFixture(ctx);
        var csv = CsvText(await c.ExportProductReport(null, null, "A-01", null, null));
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Date,Product Code,Product Name,Category,Stall,Qty Sold,Unit Price (₹),Revenue (₹),Stock Left", lines[0]);
        Assert.Equal($"{DateTime.Today.AddDays(-2):yyyy-MM-dd},PRD001,Kanjivaram Silk Saree,Textiles,A-01,3,8333.33,25000.00,42", lines[1]);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task Export_headers_follow_the_culture()
    {
        using var ctx = SeededContext();
        using var _ = new TestHelpers.CultureScope("hi-IN");
        var csv = CsvText(await CreateSales(ctx).Export(null, null, null));
        Assert.StartsWith("प्रविष्टि संख्या,तारीख", csv);
    }

    [Fact]
    public async Task Validation_messages_follow_the_culture()
    {
        using var ctx = SeededContext();
        using var _ = new TestHelpers.CultureScope("te-IN");
        var errors = TestHelpers.Errors(await CreateSales(ctx).Create(Entry()));
        Assert.Equal("కనీసం ఒక ఉత్పత్తిని జోడించండి", errors["items"]);
    }

    [Fact]
    public async Task Index_puts_today_summary_in_viewbag()
    {
        using var ctx = SeededContext();
        var c = CreateSales(ctx);
        await Sell(c, null, "00:00", Item("PRD001", 1, 750));
        var view = Assert.IsType<ViewResult>(await c.Index());
        var summary = Assert.IsType<SalesSummary>(view.ViewData["Summary"]);
        Assert.Equal((750m, 1, 1, TotalStalls), (summary.TotalSales, summary.Transactions, summary.ActiveStalls, summary.TotalStalls));
        Assert.Equal("Sales", view.ViewData["Title"]);
    }
}
