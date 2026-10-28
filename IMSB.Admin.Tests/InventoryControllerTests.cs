using System.Text;
using System.Text.Json;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IMSB.Admin.Tests.InventoryTestHelpers;

namespace IMSB.Admin.Tests;

public class InventoryProductCrudTests
{
    [Fact]
    public async Task Create_persists_product_and_records_initial_stock()
    {
        using var ctx = CreateContext();
        var controller = InventoryTestHelpers.CreateController(ctx, userName: "keeper@imsb");
        var dto = ValidCreate(qty: 12);
        dto.StallCode = " b-04 ";
        dto.Name = "  Brass Temple Lamp ";

        var code = TestHelpers.OkJson(await controller.Create(dto)).GetProperty("code").GetString();

        var p = await ctx.Products.SingleAsync();
        Assert.Equal("PRD001", code);
        Assert.Equal("Brass Temple Lamp", p.Name);
        Assert.Equal("B-04", p.StallCode);           // trimmed + upper-cased
        Assert.Equal(12, p.Quantity);
        Assert.Equal(5, p.MinStock);
        Assert.Equal(1800m, p.UnitPrice);
        Assert.Equal("keeper@imsb", p.CreatedBy);
        var m = await ctx.StockMovements.SingleAsync();
        Assert.Equal(StockMovementTypes.Initial, m.MovementType);
        Assert.Equal((0, 12, 12), (m.QuantityBefore, m.QuantityAfter, m.Quantity));
        Assert.Equal(p.Id, m.ProductId);
        Assert.Equal("PRD001", m.ProductCode);
    }

    [Fact]
    public async Task Product_codes_continue_after_highest_existing()
    {
        using var ctx = SeededContext();   // PRD001..PRD004
        AddProduct(ctx, "PRD010", "Ten", Textiles, "Z-10", 1, 0);
        var code = TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).Create(ValidCreate())).GetProperty("code").GetString();
        Assert.Equal("PRD011", code);
    }

    [Fact]
    public async Task Same_name_in_same_stall_is_rejected_case_insensitively()
    {
        using var ctx = SeededContext();
        var errors = TestHelpers.Errors(await InventoryTestHelpers.CreateController(ctx).Create(ValidCreate(name: "kanjivaram silk saree", stall: "a-01")));
        Assert.Equal("This product already exists in this stall", errors["name"]);
    }

    [Fact]
    public async Task Same_name_in_a_different_stall_is_allowed()
    {
        using var ctx = SeededContext();
        Assert.IsType<OkObjectResult>(await InventoryTestHelpers.CreateController(ctx).Create(ValidCreate(name: "Kanjivaram Silk Saree", stall: "A-02")));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("99999999-9999-9999-9999-999999999999")]
    public async Task Unknown_category_is_rejected(string categoryId)
    {
        using var ctx = CreateContext();
        var dto = ValidCreate();
        dto.CategoryId = categoryId;
        var errors = TestHelpers.Errors(await InventoryTestHelpers.CreateController(ctx).Create(dto));
        Assert.Equal("Category is required", errors["category_id"]);
        Assert.Empty(ctx.Products);
    }

    [Fact]
    public async Task Model_state_errors_are_keyed_by_form_field()
    {
        using var ctx = CreateContext();
        var controller = InventoryTestHelpers.CreateController(ctx);
        var dto = ValidCreate(name: "A", stall: "A 01", qty: -1);
        TestHelpers.BindModelState(controller, dto);

        var errors = TestHelpers.Errors(await controller.Create(dto));

        Assert.Contains("name", errors.Keys);
        Assert.Contains("stall_code", errors.Keys);
        Assert.Contains("quantity", errors.Keys);
    }

    [Fact]
    public async Task Null_bodies_are_bad_requests()
    {
        using var ctx = SeededContext();
        var c = InventoryTestHelpers.CreateController(ctx);
        Assert.IsType<BadRequestResult>(await c.Create(null));
        Assert.IsType<BadRequestResult>(await c.Update("PRD001", null));
        Assert.IsType<BadRequestResult>(await c.UpdateStock(null));
    }

    [Fact]
    public async Task Update_changes_details_but_never_quantity()
    {
        using var ctx = SeededContext();
        var dto = new UpdateProductDto { Name = "Silk Dupatta (Red)", CategoryId = Pottery.ToString(), StallCode = "a-09", MinStock = 7, UnitPrice = 999.5m };

        var result = await InventoryTestHelpers.CreateController(ctx, userName: "editor").Update("PRD002", dto);

        Assert.IsType<OkObjectResult>(result);
        var p = await ctx.Products.SingleAsync(x => x.ProductCode == "PRD002");
        Assert.Equal("Silk Dupatta (Red)", p.Name);
        Assert.Equal(Pottery, p.CategoryId);
        Assert.Equal("A-09", p.StallCode);
        Assert.Equal(7, p.MinStock);
        Assert.Equal(999.5m, p.UnitPrice);
        Assert.Equal(15, p.Quantity);
        Assert.Equal("editor", p.UpdatedBy);
        Assert.NotNull(p.UpdatedAt);
        Assert.Empty(ctx.StockMovements);   // editing details is not a stock movement
    }

    [Fact]
    public async Task Update_keeping_own_name_and_stall_is_not_a_duplicate()
    {
        using var ctx = SeededContext();
        var dto = new UpdateProductDto { Name = "Silk Dupatta (Blue)", CategoryId = Textiles.ToString(), StallCode = "A-03", MinStock = 1, UnitPrice = 1 };
        Assert.IsType<OkObjectResult>(await InventoryTestHelpers.CreateController(ctx).Update("PRD002", dto));
    }

    [Fact]
    public async Task Update_to_another_products_name_and_stall_is_rejected()
    {
        using var ctx = SeededContext();
        var dto = new UpdateProductDto { Name = "Kanjivaram Silk Saree", CategoryId = Textiles.ToString(), StallCode = "A-01", MinStock = 1, UnitPrice = 1 };
        var errors = TestHelpers.Errors(await InventoryTestHelpers.CreateController(ctx).Update("PRD002", dto));
        Assert.Contains("name", errors.Keys);
    }

    [Fact]
    public async Task Update_and_delete_unknown_product_return_404()
    {
        using var ctx = SeededContext();
        var c = InventoryTestHelpers.CreateController(ctx);
        var dto = new UpdateProductDto { Name = "X1", CategoryId = Textiles.ToString(), StallCode = "A-01" };
        Assert.IsType<NotFoundResult>(await c.Update("PRD999", dto));
        Assert.IsType<NotFoundResult>(await c.Delete("PRD999"));
    }

    [Fact]
    public async Task Delete_removes_product_but_keeps_its_stock_history()
    {
        using var ctx = SeededContext();
        var c = InventoryTestHelpers.CreateController(ctx);
        await c.UpdateStock(Stock("PRD003", "ADD", 10));

        Assert.IsType<OkObjectResult>(await c.Delete("PRD003"));

        Assert.False(await ctx.Products.AnyAsync(p => p.ProductCode == "PRD003"));
        var history = await ctx.StockMovements.SingleAsync();
        Assert.Null(history.ProductId);
        Assert.Equal("PRD003", history.ProductCode);
        Assert.Equal("Terracotta Flower Pot", history.ProductName);
    }
}

public class InventoryStockUpdateTests
{
    private static async Task<Dictionary<string, string>> ErrorsFor(IMSBContext ctx, StockUpdateDto dto)
        => TestHelpers.Errors(await InventoryTestHelpers.CreateController(ctx).UpdateStock(dto));

    [Fact]
    public async Task Add_increases_quantity_and_records_movement()
    {
        using var ctx = SeededContext();
        var result = await InventoryTestHelpers.CreateController(ctx, userName: "keeper").UpdateStock(Stock("PRD003", "ADD", 30, remarks: "  supplier delivery "));

        var json = TestHelpers.OkJson(result);
        Assert.Equal(33, json.GetProperty("quantity").GetInt32());
        Assert.Equal("IN_STOCK", json.GetProperty("status").GetString());
        var m = await ctx.StockMovements.SingleAsync();
        Assert.Equal(("ADD", 30, 3, 33), (m.MovementType, m.Quantity, m.QuantityBefore, m.QuantityAfter));
        Assert.Equal("supplier delivery", m.Remarks);
        Assert.Equal("keeper", m.CreatedBy);
        Assert.Equal(DateTime.Today, m.MovementDate);
        Assert.Equal("keeper", (await ctx.Products.SingleAsync(p => p.ProductCode == "PRD003")).UpdatedBy);
    }

    [Fact]
    public async Task Reduce_decreases_quantity_down_to_zero()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).UpdateStock(Stock("PRD002", "REDUCE", 15)));
        Assert.Equal(0, json.GetProperty("quantity").GetInt32());
        Assert.Equal("OUT_OF_STOCK", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reduce_more_than_available_is_rejected_and_nothing_changes()
    {
        using var ctx = SeededContext();
        var errors = await ErrorsFor(ctx, Stock("PRD002", "REDUCE", 16));
        Assert.Equal("Not enough stock: only 15 available", errors["quantity"]);
        Assert.Equal(15, (await ctx.Products.SingleAsync(p => p.ProductCode == "PRD002")).Quantity);
        Assert.Empty(ctx.StockMovements);
    }

    [Fact]
    public async Task Correction_sets_exact_quantity_including_zero()
    {
        using var ctx = SeededContext();
        var c = InventoryTestHelpers.CreateController(ctx);
        Assert.Equal(40, TestHelpers.OkJson(await c.UpdateStock(Stock("PRD001", "CORRECTION", 40))).GetProperty("quantity").GetInt32());
        Assert.Equal(0, TestHelpers.OkJson(await c.UpdateStock(Stock("PRD001", "CORRECTION", 0))).GetProperty("quantity").GetInt32());
        var moves = await ctx.StockMovements.OrderBy(m => m.QuantityBefore).ToListAsync();
        Assert.Equal(new[] { (40, 0), (45, 40) }, moves.Select(m => (m.QuantityBefore, m.QuantityAfter)));
    }

    [Theory]
    [InlineData("ADD")]
    [InlineData("REDUCE")]
    public async Task Add_and_reduce_need_at_least_one(string type)
    {
        using var ctx = SeededContext();
        Assert.Equal("Quantity must be at least 1", (await ErrorsFor(ctx, Stock("PRD001", type, 0)))["quantity"]);
    }

    [Fact]
    public async Task Unknown_product_is_rejected()
    {
        using var ctx = SeededContext();
        Assert.Equal("Select a product", (await ErrorsFor(ctx, Stock("PRD999", "ADD", 1)))["product_code"]);
    }

    [Theory]
    [InlineData("INITIAL")]
    [InlineData("RESET")]
    [InlineData("")]
    public async Task Invalid_update_type_is_rejected(string type)
    {
        using var ctx = SeededContext();
        Assert.Equal("Select an update type", (await ErrorsFor(ctx, Stock("PRD001", type, 1)))["update_type"]);
    }

    [Fact]
    public async Task Future_and_malformed_dates_are_rejected()
    {
        using var ctx = SeededContext();
        Assert.Equal("Date cannot be in the future", (await ErrorsFor(ctx, Stock("PRD001", "ADD", 1, DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"))))["date"]);
        Assert.Equal("Invalid date", (await ErrorsFor(ctx, Stock("PRD001", "ADD", 1, "07/10/2026")))["date"]);
    }

    [Fact]
    public async Task Backdated_update_keeps_the_entered_date()
    {
        using var ctx = SeededContext();
        await InventoryTestHelpers.CreateController(ctx).UpdateStock(Stock("PRD001", "ADD", 1, "2026-01-15"));
        Assert.Equal(new DateTime(2026, 1, 15), (await ctx.StockMovements.SingleAsync()).MovementDate);
    }

    [Fact]
    public async Task Sequential_updates_chain_before_and_after()
    {
        using var ctx = SeededContext();
        var c = InventoryTestHelpers.CreateController(ctx);
        await c.UpdateStock(Stock("PRD004", "ADD", 60));
        await c.UpdateStock(Stock("PRD004", "REDUCE", 25));
        await c.UpdateStock(Stock("PRD004", "CORRECTION", 33));

        Assert.Equal(33, (await ctx.Products.SingleAsync(p => p.ProductCode == "PRD004")).Quantity);
        // each movement starts where the previous one ended
        var moves = (await ctx.StockMovements.ToListAsync()).Select(m => (m.MovementType, m.QuantityBefore, m.QuantityAfter)).ToHashSet();
        Assert.Equal(new HashSet<(string, int, int)> { ("ADD", 0, 60), ("REDUCE", 60, 35), ("CORRECTION", 35, 33) }, moves);
    }
}

public class InventoryQueryTests
{
    private static List<string> Codes(JsonElement json, string list = "products") =>
        json.GetProperty(list).EnumerateArray().Select(p => p.GetProperty("code").GetString()!).ToList();

    [Fact]
    public async Task Data_returns_stats_matching_the_header_cards()
    {
        using var ctx = SeededContext();
        var stats = TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).Data(null, null, null)).GetProperty("stats");

        Assert.Equal(4, stats.GetProperty("total").GetInt32());
        Assert.Equal(1, stats.GetProperty("inStock").GetInt32());
        Assert.Equal(2, stats.GetProperty("lowStock").GetInt32());
        Assert.Equal(1, stats.GetProperty("outOfStock").GetInt32());
        Assert.Equal(3, stats.GetProperty("belowMinimum").GetInt32());
        Assert.Equal(7, stats.GetProperty("categories").GetInt32());
        Assert.Equal(4, stats.GetProperty("stallsTracked").GetInt32());
    }

    [Fact]
    public async Task Index_view_gets_the_same_stats()
    {
        using var ctx = SeededContext();
        var view = Assert.IsType<ViewResult>(await InventoryTestHelpers.CreateController(ctx).Index());
        var stats = Assert.IsType<InventoryStats>(view.ViewData["Stats"]);
        Assert.Equal(new InventoryStats(4, 1, 2, 1, 3, 7, 4), stats);
    }

    [Fact]
    public async Task Products_are_sorted_by_name_with_display_fields()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).Data(null, null, null));
        Assert.Equal(new[] { "PRD004", "PRD001", "PRD002", "PRD003" }, Codes(json));
        var oil = json.GetProperty("products")[0];
        Assert.Equal("Herbal", oil.GetProperty("categoryName").GetString());
        Assert.Equal("OUT_OF_STOCK", oil.GetProperty("status").GetString());
        Assert.Equal(320m, oil.GetProperty("unitPrice").GetDecimal());
    }

    [Theory]
    [InlineData("silk", new[] { "PRD001", "PRD002" })]
    [InlineData("PRD003", new[] { "PRD003" })]
    [InlineData("f-02", new[] { "PRD004" })]        // stall code
    [InlineData("  JASMINE ", new[] { "PRD004" })]
    [InlineData("nothing", new string[0])]
    public async Task Search_matches_name_code_or_stall(string q, string[] expected)
    {
        using var ctx = SeededContext();
        Assert.Equal(expected, Codes(TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).Data(q, null, null))));
    }

    [Theory]
    [InlineData("IN_STOCK", new[] { "PRD001" })]
    [InlineData("LOW_STOCK", new[] { "PRD002", "PRD003" })]
    [InlineData("out_of_stock", new[] { "PRD004" })]
    [InlineData("", new[] { "PRD004", "PRD001", "PRD002", "PRD003" })]
    [InlineData("bogus", new[] { "PRD004", "PRD001", "PRD002", "PRD003" })]
    public async Task Status_filter(string status, string[] expected)
    {
        using var ctx = SeededContext();
        Assert.Equal(expected, Codes(TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).Data(null, null, status))));
    }

    [Fact]
    public async Task Category_filter_and_combined_filters()
    {
        using var ctx = SeededContext();
        var c = InventoryTestHelpers.CreateController(ctx);
        Assert.Equal(new[] { "PRD001", "PRD002" }, Codes(TestHelpers.OkJson(await c.Data(null, Textiles.ToString(), null))));
        Assert.Equal(new[] { "PRD002" }, Codes(TestHelpers.OkJson(await c.Data("silk", Textiles.ToString(), "LOW_STOCK"))));
        Assert.Equal(4, Codes(TestHelpers.OkJson(await c.Data(null, "not-a-guid", null))).Count);
    }

    [Fact]
    public async Task Filters_do_not_affect_stats_alerts_or_stock_picker()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).Data("nothing", null, null));
        Assert.Empty(json.GetProperty("products").EnumerateArray());
        Assert.Equal(4, json.GetProperty("stats").GetProperty("total").GetInt32());
        Assert.Equal(3, json.GetProperty("lowStock").GetArrayLength());
        Assert.Equal(4, json.GetProperty("productOptions").GetArrayLength());
    }

    [Fact]
    public async Task Low_stock_list_is_most_urgent_first_with_urgency()
    {
        using var ctx = SeededContext();
        var low = TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).Data(null, null, null)).GetProperty("lowStock");
        Assert.Equal(new[] { "PRD004", "PRD003", "PRD002" }, low.EnumerateArray().Select(p => p.GetProperty("code").GetString()));
        Assert.Equal(new[] { "CRITICAL", "CRITICAL", "MODERATE" }, low.EnumerateArray().Select(p => p.GetProperty("urgency").GetString()));
    }

    [Fact]
    public async Task Categories_and_chart_aggregate_all_products()
    {
        using var ctx = SeededContext();
        var json = TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).Data(null, null, null));

        var textiles = json.GetProperty("categories").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Textiles");
        Assert.Equal(2, textiles.GetProperty("productCount").GetInt32());
        Assert.Equal(1, textiles.GetProperty("lowCount").GetInt32());
        Assert.Equal(60, textiles.GetProperty("totalQuantity").GetInt32());
        Assert.Equal(40, textiles.GetProperty("totalMinStock").GetInt32());
        Assert.Equal(45 * 8500m + 15 * 1200m, textiles.GetProperty("stockValue").GetDecimal());

        var chart = json.GetProperty("chart");
        Assert.Equal(IMSBContext.SeedCategories.Select(c => c.Name), chart.GetProperty("labels").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { 60, 0, 0, 3, 0, 0, 0 }, chart.GetProperty("current").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal(new[] { 40, 0, 0, 25, 0, 50, 0 }, chart.GetProperty("minimum").EnumerateArray().Select(x => x.GetInt32()));
    }

    [Fact]
    public async Task Movements_are_newest_first_and_flag_deleted_products()
    {
        using var ctx = SeededContext();
        var c = InventoryTestHelpers.CreateController(ctx);
        await c.UpdateStock(Stock("PRD003", "ADD", 5, remarks: "first"));
        await Task.Delay(5);
        await c.UpdateStock(Stock("PRD001", "REDUCE", 2, remarks: "second"));
        await c.Delete("PRD003");

        var moves = TestHelpers.OkJson(await c.Data(null, null, null)).GetProperty("movements").EnumerateArray().ToList();
        Assert.Equal(new[] { "second", "first" }, moves.Select(m => m.GetProperty("remarks").GetString()));
        Assert.False(moves[0].GetProperty("productDeleted").GetBoolean());
        Assert.True(moves[1].GetProperty("productDeleted").GetBoolean());
        Assert.Equal(-2, moves[0].GetProperty("after").GetInt32() - moves[0].GetProperty("before").GetInt32());
    }

    [Fact]
    public async Task Stall_list_is_distinct_and_sorted()
    {
        using var ctx = SeededContext();
        AddProduct(ctx, "PRD005", "Another", Herbal, "A-01", 1, 0);
        var stalls = TestHelpers.OkJson(await InventoryTestHelpers.CreateController(ctx).Data(null, null, null)).GetProperty("stalls");
        Assert.Equal(new[] { "A-01", "A-03", "C-03", "F-02" }, stalls.EnumerateArray().Select(s => s.GetString()));
    }

    [Fact]
    public async Task Export_returns_filtered_csv_with_localized_status()
    {
        using var ctx = SeededContext();
        var file = Assert.IsType<FileContentResult>(await InventoryTestHelpers.CreateController(ctx).Export(null, null, "LOW_STOCK"));

        Assert.Equal("application/vnd.ms-excel", file.ContentType);
        Assert.StartsWith("inventory_", file.FileDownloadName);
        var lines = ExcelExportTestHelpers.CsvText(file).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("ProductCode,ProductName,Category,Stall,Quantity,MinStock,UnitPrice,Status", lines[0]);
        Assert.Equal("PRD002,Silk Dupatta (Blue),Textiles,A-03,15,20,1200.00,Low Stock", lines[1]);
        Assert.Equal("PRD003,Terracotta Flower Pot,Pottery,C-03,3,25,450.00,Low Stock", lines[2]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task Export_keeps_formula_like_product_names_as_text()
    {
        using var ctx = CreateContext();
        AddProduct(ctx, "PRD001", "=HYPERLINK(\"x\"), evil", Textiles, "A-01", 5, 1);
        var file = Assert.IsType<FileContentResult>(await InventoryTestHelpers.CreateController(ctx).Export(null, null, null));
        var productRow = ExcelExportTestHelpers.Rows(file.FileContents)[1];
        Assert.Equal("=HYPERLINK(\"x\"), evil", productRow[1]);
    }

    [Theory]
    [InlineData("ta-IN", "குறைந்த இருப்பு")]
    [InlineData("hi-IN", "कम स्टॉक")]
    public async Task Export_status_follows_culture(string culture, string expected)
    {
        using var scope = new TestHelpers.CultureScope(culture);
        using var ctx = SeededContext();
        var file = Assert.IsType<FileContentResult>(await InventoryTestHelpers.CreateController(ctx).Export("PRD002", null, null));
        Assert.EndsWith("," + expected, ExcelExportTestHelpers.CsvText(file).Split("\r\n")[1]);
    }

    [Theory]
    [InlineData("ta-IN", "போதுமான இருப்பு இல்லை: 15 மட்டுமே உள்ளது")]
    [InlineData("te-IN", "సరిపడా స్టాక్ లేదు: 15 మాత్రమే అందుబాటులో ఉంది")]
    [InlineData("hi-IN", "पर्याप्त स्टॉक नहीं: केवल 15 उपलब्ध")]
    public async Task Stock_errors_follow_culture(string culture, string expected)
    {
        using var scope = new TestHelpers.CultureScope(culture);
        using var ctx = SeededContext();
        var errors = TestHelpers.Errors(await InventoryTestHelpers.CreateController(ctx).UpdateStock(Stock("PRD002", "REDUCE", 99)));
        Assert.Equal(expected, errors["quantity"]);
    }
}
