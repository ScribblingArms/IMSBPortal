using System.Security.Claims;
using System.Text;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IMSB.Admin.Tests;

/// <summary>Fixtures for the sales and dashboard tests: seeded inventory, wired controllers and sale builders.</summary>
internal static class SalesTestHelpers
{
    public const int TotalStalls = 10;

    public static IOptions<BazaarOptions> Bazaar(int totalStalls = TotalStalls) => Options.Create(new BazaarOptions { TotalStalls = totalStalls });

    private static ControllerContext Context(string userName) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userName), new Claim(ClaimTypes.Role, "Admin") }, "Test"))
        }
    };

    public static SalesController CreateSales(IMSBContext ctx, string userName = "admin@imsb") =>
        new(new IMSB.Backend.Sales(ctx), TestHelpers.LocalizerFactory(), Bazaar()) { ControllerContext = Context(userName) };

    public static DashboardController CreateDashboard(IMSBContext ctx, int totalStalls = TotalStalls) =>
        new(new IMSB.Backend.Dashboard(ctx), TestHelpers.LocalizerFactory(), Bazaar(totalStalls)) { ControllerContext = Context("admin@imsb") };

    /// <summary>
    /// PRD001 Saree (A-01, 45 @ 8500, min 20), PRD002 Dupatta (A-03, 15 @ 1200, min 20),
    /// PRD003 Pot (C-03, 3 @ 450, min 25), PRD004 Oil (F-02, 0 @ 320, min 50).
    /// </summary>
    public static IMSBContext SeededContext() => InventoryTestHelpers.SeededContext();

    public static SaleItemDto Item(string code, int qty, decimal price) => new() { ProductCode = code, Quantity = qty, UnitPrice = price };

    public static SalesEntryDto Entry(DateTime? date = null, string? time = "00:00", string? remarks = null, params SaleItemDto[] items) => new()
    {
        Date = (date ?? DateTime.Today).ToString("yyyy-MM-dd"),
        Time = time,
        Remarks = remarks,
        Items = items.ToList()
    };

    /// <summary>Creates a sale through the controller and returns its entry number.</summary>
    public static async Task<string> Sell(SalesController c, DateTime? date, string? time, params SaleItemDto[] items)
    {
        var result = await c.Create(Entry(date, time, null, items));
        return TestHelpers.OkJson(result).GetProperty("entryNo").GetString()!;
    }

    public static string CsvText(IActionResult result)
    {
        var file = Assert.IsType<FileContentResult>(result);
        return ExcelExportTestHelpers.CsvText(file);
    }
}
