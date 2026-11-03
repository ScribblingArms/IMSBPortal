using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>Products, stock levels and the stock history.</summary>
    [Authorize(Roles = "Inventory,Admin")]
    public class InventoryController : ApiControllerBase
    {
        private const int RecentMovements = 50;

        private readonly IInventoryInterface _inventory;

        public InventoryController(IInventoryInterface inventory) => _inventory = inventory;

        /// <summary>Stats, categories, the filtered products, low-stock alerts and recent stock movements.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? q, [FromQuery] string? categoryId, [FromQuery] string? status)
        {
            var categories = await _inventory.CategoriesAsync();
            var all = await _inventory.ProductsAsync();
            var catNames = categories.ToDictionary(c => c.Id, c => c.Name);
            string CatName(Guid id) => catNames.GetValueOrDefault(id) ?? string.Empty;
            var byCategory = all.ToLookup(p => p.CategoryId);

            return Ok(new
            {
                stats = ComputeStats(all, categories.Count),
                categories = categories.Select(c => new
                {
                    id = c.Id.ToString(),
                    name = c.Name,
                    productCount = byCategory[c.Id].Count(),
                    lowCount = byCategory[c.Id].Count(p => p.StockStatus != StockStatuses.InStock),
                    totalQuantity = byCategory[c.Id].Sum(p => p.Quantity),
                    totalMinStock = byCategory[c.Id].Sum(p => p.MinStock),
                    stockValue = byCategory[c.Id].Sum(p => p.Quantity * p.UnitPrice)
                }),
                products = (await _inventory.FilterAsync(q, categoryId, status)).Select(p => p.ToListItem(CatName(p.CategoryId))),
                productOptions = all.OrderBy(p => p.Name).Select(p => p.ToOption()),
                stalls = all.Select(p => p.StallCode).Distinct().OrderBy(s => s),
                lowStock = all.Where(p => p.StockStatus != StockStatuses.InStock)
                    .OrderBy(p => p.MinStock == 0 ? 1.0 : (double)p.Quantity / p.MinStock).ThenBy(p => p.Name)
                    .Select(p => p.ToLowStockItem(CatName(p.CategoryId))),
                movements = (await _inventory.RecentMovementsAsync(RecentMovements)).Select(m => m.ToItem())
            });
        }

        [HttpGet("{code}")]
        public async Task<IActionResult> Get(string code)
        {
            var product = await _inventory.FindAsync(code);
            return product == null ? NotFound()
                : Ok(product.ToListItem((await _inventory.CategoryNamesAsync()).GetValueOrDefault(product.CategoryId) ?? string.Empty));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
        {
            var errors = await ValidateProductAsync(dto.Name, dto.CategoryId, dto.StallCode, null);
            if (errors.Count > 0) return Invalid(errors);

            var product = new Product
            {
                ProductCode = NextCode(await _inventory.ProductCodesAsync(), "PRD", 3),
                Name = dto.Name.Trim(),
                CategoryId = Guid.Parse(dto.CategoryId),
                StallCode = dto.StallCode.Trim().ToUpperInvariant(),
                Quantity = 0,
                MinStock = dto.MinStock,
                UnitPrice = dto.UnitPrice,
                CreatedBy = UserName
            };
            // the opening quantity is the first entry in the stock history
            var opening = product.ToMovement(StockMovementTypes.Initial, dto.Quantity, dto.Quantity, DateTime.Today, null, UserName);
            product.Quantity = dto.Quantity;
            await _inventory.AddAsync(product, opening);
            return Ok(new { code = product.ProductCode });
        }

        [HttpPut("{code}")]
        public async Task<IActionResult> Update(string code, [FromBody] UpdateProductDto dto)
        {
            var product = await _inventory.FindAsync(code);
            if (product == null) return NotFound();
            var errors = await ValidateProductAsync(dto.Name, dto.CategoryId, dto.StallCode, product.Id);
            if (errors.Count > 0) return Invalid(errors);

            product.Name = dto.Name.Trim();
            product.CategoryId = Guid.Parse(dto.CategoryId);
            product.StallCode = dto.StallCode.Trim().ToUpperInvariant();
            product.MinStock = dto.MinStock;
            product.UnitPrice = dto.UnitPrice;
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = UserName;
            await _inventory.UpdateAsync(product);
            return Ok(new { code = product.ProductCode });
        }

        /// <summary>The stock history and sale lines keep their code / name snapshot of the product.</summary>
        [HttpDelete("{code}")]
        public async Task<IActionResult> Delete(string code)
        {
            var product = await _inventory.FindAsync(code);
            if (product == null) return NotFound();
            await _inventory.DeleteAsync(product);
            return Ok(new { code });
        }

        /// <summary>ADD / REDUCE by a quantity, or CORRECTION to the counted quantity.</summary>
        [HttpPost("stock")]
        public async Task<IActionResult> UpdateStock([FromBody] StockUpdateDto dto)
        {
            var errors = new Dictionary<string, string>();
            var product = string.IsNullOrWhiteSpace(dto.ProductCode) ? null : await _inventory.FindAsync(dto.ProductCode);
            if (product == null) errors["product_code"] = L["ValSelectProduct"];

            var type = dto.UpdateType?.Trim().ToUpperInvariant() ?? "";
            if (!StockMovementTypes.UserSelectable.Contains(type)) errors["update_type"] = L["ValUpdateType"];
            // ADD / REDUCE move by at least 1; CORRECTION sets the counted quantity, which may be 0
            else if (type != StockMovementTypes.Correction && dto.Quantity < 1) errors["quantity"] = L["ValQuantityPositive"];

            if (!TryParseDate(dto.Date, out var date)) errors["date"] = L["ValDateInvalid"];
            else if (date > DateTime.Today) errors["date"] = L["ValDateFuture"];

            int? after = null;
            if (product != null && !errors.ContainsKey("update_type") && !errors.ContainsKey("quantity"))
            {
                after = StockMovementTypes.Apply(type, product.Quantity, dto.Quantity);
                if (after == null) errors["quantity"] = string.Format(L["ValInsufficientStock"], product.Quantity);
            }
            if (errors.Count > 0) return Invalid(errors);

            var p = product!;
            var movement = p.ToMovement(type, dto.Quantity, after!.Value, date,
                string.IsNullOrWhiteSpace(dto.Remarks) ? null : dto.Remarks.Trim(), UserName);
            p.Quantity = after.Value;
            p.UpdatedAt = DateTime.UtcNow;
            p.UpdatedBy = UserName;
            await _inventory.UpdateStockAsync(p, movement);
            return Ok(new { code = p.ProductCode, quantity = p.Quantity, status = p.StockStatus });
        }

        private static InventoryStats ComputeStats(List<Product> products, int categoryCount)
        {
            var statuses = products.Select(p => p.StockStatus).ToList();
            var inStock = statuses.Count(s => s == StockStatuses.InStock);
            return new(
                Total: products.Count,
                InStock: inStock,
                LowStock: statuses.Count(s => s == StockStatuses.LowStock),
                OutOfStock: statuses.Count(s => s == StockStatuses.OutOfStock),
                BelowMinimum: products.Count - inStock,
                Categories: categoryCount,
                StallsTracked: products.Select(p => p.StallCode.ToUpper()).Distinct().Count());
        }

        private async Task<Dictionary<string, string>> ValidateProductAsync(string name, string categoryId, string stallCode, Guid? existingId)
        {
            var errors = new Dictionary<string, string>();
            if (!Guid.TryParse(categoryId, out var catId) || !await _inventory.CategoryExistsAsync(catId))
                errors["category_id"] = L["ValCategory"];
            // the same product name may exist in different stalls, but not twice in one stall
            if (await _inventory.ProductExistsAsync(name, stallCode, existingId))
                errors["name"] = L["ValProductExists"];
            return errors;
        }
    }
}
