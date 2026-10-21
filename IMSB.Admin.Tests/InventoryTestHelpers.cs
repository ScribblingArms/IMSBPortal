using System.Security.Claims;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

/// <summary>Fixtures for the inventory tests: categories from the real seed list, sample products and a wired controller.</summary>
internal static class InventoryTestHelpers
{
    public static readonly Guid Textiles = IMSBContext.SeedCategories[0].Id;
    public static readonly Guid Pottery = IMSBContext.SeedCategories[3].Id;
    public static readonly Guid Herbal = IMSBContext.SeedCategories[5].Id;

    public static IMSBContext CreateContext()
    {
        var ctx = TestHelpers.CreateContext(seedReferenceData: false);
        ctx.ProductCategories.AddRange(IMSBContext.SeedCategories.Select((c, i) =>
            new ProductCategory { Id = c.Id, Name = c.Name, SortOrder = i + 1 }));
        ctx.SaveChanges();
        return ctx;
    }

    public static InventoryController CreateController(IMSBContext ctx, string userName = "store@imsb", string role = "Inventory")
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userName), new Claim(ClaimTypes.Role, role) }, "Test");
        return new InventoryController(new IMSB.Backend.Inventory(ctx), TestHelpers.LocalizerFactory())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
    }

    public static Product AddProduct(IMSBContext ctx, string code, string name, Guid category, string stall, int qty, int min, decimal price = 100)
    {
        var p = new Product { ProductCode = code, Name = name, CategoryId = category, StallCode = stall, Quantity = qty, MinStock = min, UnitPrice = price };
        ctx.Products.Add(p);
        ctx.SaveChanges();
        return p;
    }

    /// <summary>Saree (in stock), Dupatta (low), Pot (critical low), Oil (out of stock).</summary>
    public static IMSBContext SeededContext()
    {
        var ctx = CreateContext();
        AddProduct(ctx, "PRD001", "Kanjivaram Silk Saree", Textiles, "A-01", 45, 20, 8500);
        AddProduct(ctx, "PRD002", "Silk Dupatta (Blue)", Textiles, "A-03", 15, 20, 1200);
        AddProduct(ctx, "PRD003", "Terracotta Flower Pot", Pottery, "C-03", 3, 25, 450);
        AddProduct(ctx, "PRD004", "Jasmine Essential Oil", Herbal, "F-02", 0, 50, 320);
        return ctx;
    }

    public static CreateProductDto ValidCreate(string name = "Brass Temple Lamp", string stall = "B-04", int qty = 10) => new()
    {
        Name = name, CategoryId = Textiles.ToString(), StallCode = stall, Quantity = qty, MinStock = 5, UnitPrice = 1800
    };

    public static StockUpdateDto Stock(string code, string type, int qty, string? date = null, string? remarks = null) => new()
    {
        ProductCode = code, UpdateType = type, Quantity = qty, Date = date ?? DateTime.Today.ToString("yyyy-MM-dd"), Remarks = remarks
    };
}
