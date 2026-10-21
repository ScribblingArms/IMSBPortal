using System.ComponentModel.DataAnnotations;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

public class InventoryModelTests
{
    [Theory]
    [InlineData(0, 10, StockStatuses.OutOfStock)]
    [InlineData(-1, 10, StockStatuses.OutOfStock)]
    [InlineData(9, 10, StockStatuses.LowStock)]
    [InlineData(10, 10, StockStatuses.InStock)]   // at the minimum is still in stock
    [InlineData(50, 10, StockStatuses.InStock)]
    [InlineData(1, 0, StockStatuses.InStock)]     // no minimum configured
    public void Stock_status_is_derived_from_quantity_and_minimum(int qty, int min, string expected)
    {
        Assert.Equal(expected, StockStatuses.For(qty, min));
        Assert.Equal(expected, new Product { Quantity = qty, MinStock = min }.StockStatus);
    }

    [Theory]
    [InlineData(0, 10, "CRITICAL")]
    [InlineData(1, 10, "CRITICAL")]    // 10% of minimum
    [InlineData(2, 10, "MODERATE")]    // exactly 20% is not below 20%
    [InlineData(9, 10, "MODERATE")]
    public void Urgency_is_critical_below_20_percent_of_minimum(int qty, int min, string expected)
    {
        Assert.Equal(expected, StockStatuses.Urgency(qty, min));
    }

    [Theory]
    [InlineData(StockMovementTypes.Add, 10, 5, 15)]
    [InlineData(StockMovementTypes.Reduce, 10, 4, 6)]
    [InlineData(StockMovementTypes.Reduce, 10, 10, 0)]
    [InlineData(StockMovementTypes.Correction, 10, 3, 3)]
    [InlineData(StockMovementTypes.Correction, 10, 0, 0)]
    public void Movement_types_apply_to_current_quantity(string type, int current, int qty, int expected)
    {
        Assert.Equal(expected, StockMovementTypes.Apply(type, current, qty));
    }

    [Fact]
    public void Reduce_below_zero_and_unknown_types_are_rejected()
    {
        Assert.Null(StockMovementTypes.Apply(StockMovementTypes.Reduce, 3, 4));
        Assert.Null(StockMovementTypes.Apply("RESET", 3, 0));
        Assert.Null(StockMovementTypes.Apply(StockMovementTypes.Initial, 0, 5));   // INITIAL is only written on create
    }

    [Fact]
    public void New_product_defaults()
    {
        var p = new Product();
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal(0, p.Quantity);
        Assert.Equal(StockStatuses.OutOfStock, p.StockStatus);
        Assert.Null(p.UpdatedAt);
    }

    [Fact]
    public void Seed_categories_match_the_prototype()
    {
        Assert.Equal(new[] { "Textiles", "Handicrafts", "Jewellery", "Pottery", "Paintings", "Herbal", "Food & Snacks" },
            IMSBContext.SeedCategories.Select(c => c.Name));
        Assert.Equal(7, IMSBContext.SeedCategories.Select(c => c.Id).Distinct().Count());
    }

    private static IEnumerable<string> Failed(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);
        return results.SelectMany(r => r.MemberNames);
    }

    [Fact]
    public void Valid_product_dto_passes()
    {
        Assert.Empty(Failed(InventoryTestHelpers.ValidCreate()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    public void Product_name_needs_two_chars(string name)
    {
        Assert.Contains(nameof(CreateProductDto.Name), Failed(InventoryTestHelpers.ValidCreate(name: name)));
    }

    [Theory]
    [InlineData("A-01", true)]
    [InlineData("b12", true)]
    [InlineData("", false)]
    [InlineData("A 01", false)]
    [InlineData("A/01", false)]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU", false)]   // 21 chars
    public void Stall_code_format(string stall, bool valid)
    {
        Assert.Equal(valid, !Failed(InventoryTestHelpers.ValidCreate(stall: stall)).Contains(nameof(CreateProductDto.StallCode)));
    }

    [Fact]
    public void Negative_numbers_are_rejected()
    {
        var dto = InventoryTestHelpers.ValidCreate(qty: -1);
        dto.MinStock = -1;
        dto.UnitPrice = -0.01m;
        var failed = Failed(dto).ToList();
        Assert.Contains(nameof(CreateProductDto.Quantity), failed);
        Assert.Contains(nameof(CreateProductDto.MinStock), failed);
        Assert.Contains(nameof(CreateProductDto.UnitPrice), failed);
    }

    [Theory]
    [InlineData("ADD", true)]
    [InlineData("REDUCE", true)]
    [InlineData("CORRECTION", true)]
    [InlineData("INITIAL", false)]   // not user-selectable
    [InlineData("add", false)]
    [InlineData("", false)]
    public void Stock_update_type_is_restricted(string type, bool valid)
    {
        Assert.Equal(valid, !Failed(InventoryTestHelpers.Stock("PRD001", type, 1)).Contains(nameof(StockUpdateDto.UpdateType)));
    }

    [Fact]
    public void Stock_update_remarks_limited_to_500()
    {
        Assert.Contains(nameof(StockUpdateDto.Remarks), Failed(InventoryTestHelpers.Stock("PRD001", "ADD", 1, remarks: new string('x', 501))));
        Assert.DoesNotContain(nameof(StockUpdateDto.Remarks), Failed(InventoryTestHelpers.Stock("PRD001", "ADD", 1, remarks: new string('x', 500))));
    }
}
