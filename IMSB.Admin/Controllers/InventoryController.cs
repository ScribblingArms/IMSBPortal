using IMSB.Admin.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers
{
    [Authorize(Roles = "Inventory,Admin")]
    public class InventoryController : Controller
    {
        private const int RecentMovements = 50;

        private readonly IInventoryInterface _inventory;
        private readonly IStringLocalizer _L;

        public InventoryController(IInventoryInterface inventory, IStringLocalizerFactory localizerFactory)
        {
            _inventory = inventory;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(InventoryController).Assembly.GetName().Name!);
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = _L["Inventory"].Value;
            var products = await _inventory.ProductsAsync();
            ViewBag.Stats = ComputeStats(products, await _inventory.CategoryCountAsync());
            return View();
        }

        // Everything the page needs in one call: filtered product list, categories, chart, alerts, history
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? q, [FromQuery] string? categoryId, [FromQuery] string? status)
        {
            var categories = await _inventory.CategoriesAsync();
            var all = await _inventory.ProductsAsync();
            var catNames = categories.ToDictionary(c => c.Id, c => c.Name);
            string CatName(Guid id) => catNames.GetValueOrDefault(id) ?? string.Empty;

            var filtered = await _inventory.FilterAsync(q, categoryId, status);
            var byCategory = all.ToLookup(p => p.CategoryId);

            var categoriesDto = categories.Select(c =>
            {
                var items = byCategory[c.Id];
                return new
                {
                    id = c.Id.ToString(),
                    name = c.Name,
                    productCount = items.Count(),
                    lowCount = items.Count(p => p.StockStatus != StockStatuses.InStock),
                    totalQuantity = items.Sum(p => p.Quantity),
                    totalMinStock = items.Sum(p => p.MinStock),
                    stockValue = items.Sum(p => p.Quantity * p.UnitPrice)
                };
            }).ToList();

            var lowStock = all
                .Where(p => p.StockStatus != StockStatuses.InStock)
                .OrderBy(p => p.MinStock == 0 ? 1.0 : (double)p.Quantity / p.MinStock)
                .ThenBy(p => p.Name)
                .Select(p => p.ToLowStockItem(CatName(p.CategoryId)))
                .ToList();

            return Ok(new
            {
                stats = ComputeStats(all, categories.Count),
                categories = categoriesDto,
                products = filtered.Select(p => p.ToListItem(CatName(p.CategoryId))),
                // for the Stock Update product picker (not affected by the list filters)
                productOptions = all.OrderBy(p => p.Name).Select(p => p.ToOption()),
                stalls = all.Select(p => p.StallCode).Distinct().OrderBy(s => s),
                lowStock,
                chart = new
                {
                    labels = categoriesDto.Select(c => c.name),
                    current = categoriesDto.Select(c => c.totalQuantity),
                    minimum = categoriesDto.Select(c => c.totalMinStock)
                },
                movements = (await _inventory.RecentMovementsAsync(RecentMovements)).Select(m => m.ToItem())
            });
        }

        internal static InventoryStats ComputeStats(List<Product> products, int categoryCount)
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateProductDto? dto)
        {
            if (dto == null) return BadRequest();
            var errors = await ValidateProductAsync(dto.Name, dto.CategoryId, dto.StallCode, null);
            if (errors.Count > 0) return BadRequest(new { errors });

            var user = User.Identity?.Name;
            var product = new Product
            {
                ProductCode = NextCode(await _inventory.ProductCodesAsync(), "PRD", 3),
                Name = dto.Name.Trim(),
                CategoryId = Guid.Parse(dto.CategoryId),
                StallCode = dto.StallCode.Trim().ToUpperInvariant(),
                Quantity = 0,
                MinStock = dto.MinStock,
                UnitPrice = dto.UnitPrice,
                CreatedBy = user
            };
            // the opening quantity is the first entry in the stock history
            var opening = product.ToMovement(StockMovementTypes.Initial, dto.Quantity, dto.Quantity, DateTime.Today, null, user);
            product.Quantity = dto.Quantity;
            await _inventory.AddAsync(product, opening);
            return Ok(new { code = product.ProductCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateProductDto? dto)
        {
            if (dto == null) return BadRequest();
            var product = await _inventory.FindAsync(id);
            if (product == null) return NotFound();

            var errors = await ValidateProductAsync(dto.Name, dto.CategoryId, dto.StallCode, product.Id);
            if (errors.Count > 0) return BadRequest(new { errors });

            product.Name = dto.Name.Trim();
            product.CategoryId = Guid.Parse(dto.CategoryId);
            product.StallCode = dto.StallCode.Trim().ToUpperInvariant();
            product.MinStock = dto.MinStock;
            product.UnitPrice = dto.UnitPrice;
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = User.Identity?.Name;
            await _inventory.UpdateAsync(product);
            return Ok(new { code = product.ProductCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var product = await _inventory.FindAsync(id);
            if (product == null) return NotFound();

            // the stock history and sale lines keep their code/name snapshot
            await _inventory.DeleteAsync(product);
            return Ok(new { code = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock([FromBody] StockUpdateDto? dto)
        {
            if (dto == null) return BadRequest();
            var errors = ModelStateErrors(ModelState);

            var product = string.IsNullOrWhiteSpace(dto.ProductCode) ? null : await _inventory.FindAsync(dto.ProductCode);
            if (product == null && !errors.ContainsKey("product_code")) errors["product_code"] = _L["ValSelectProduct"];

            var type = dto.UpdateType?.Trim().ToUpperInvariant() ?? "";
            if (!errors.ContainsKey("update_type") && !StockMovementTypes.UserSelectable.Contains(type)) errors["update_type"] = _L["ValUpdateType"];

            // ADD/REDUCE move by at least 1; CORRECTION sets the counted quantity, which may be 0
            if (!errors.ContainsKey("quantity") && type != StockMovementTypes.Correction && dto.Quantity < 1)
                errors["quantity"] = _L["ValQuantityPositive"];

            DateTime date = default;
            if (!errors.ContainsKey("date"))
            {
                if (!TryParseDate(dto.Date, out date)) errors["date"] = _L["ValDateInvalid"];
                else if (date > DateTime.Today) errors["date"] = _L["ValDateFuture"];
            }

            int? after = null;
            if (product != null && !errors.ContainsKey("update_type") && !errors.ContainsKey("quantity"))
            {
                after = StockMovementTypes.Apply(type, product.Quantity, dto.Quantity);
                if (after == null) errors["quantity"] = string.Format(_L["ValInsufficientStock"], product.Quantity);
            }
            if (errors.Count > 0) return BadRequest(new { errors });

            var user = User.Identity?.Name;
            var p = product!;
            var movement = p.ToMovement(type, dto.Quantity, after!.Value, date,
                string.IsNullOrWhiteSpace(dto.Remarks) ? null : dto.Remarks.Trim(), user);
            p.Quantity = after.Value;
            p.UpdatedAt = DateTime.UtcNow;
            p.UpdatedBy = user;
            await _inventory.UpdateStockAsync(p, movement);

            return Ok(new { code = p.ProductCode, quantity = p.Quantity, status = p.StockStatus });
        }

        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] string? q, [FromQuery] string? categoryId, [FromQuery] string? status)
        {
            var products = await _inventory.FilterAsync(q, categoryId, status);
            var catNames = await _inventory.CategoryNamesAsync();

            var rows = new List<string?[]>
            {
                new string?[] { "ProductCode", "ProductName", "Category", "Stall", "Quantity", "MinStock", "UnitPrice", "Status" }
            };
            foreach (var p in products)
            {
                rows.Add(new string?[] { p.ProductCode, p.Name, catNames.GetValueOrDefault(p.CategoryId), p.StallCode, Int(p.Quantity), Int(p.MinStock),
                    Money(p.UnitPrice), StatusLabels.Stock(_L, p.StockStatus) });
            }
            return ExportCsv($"inventory_{DateTime.UtcNow:yyyyMMddHHmmss}.csv", rows);
        }

        // Errors are keyed by the snake_case field names the forms use for their err_* elements
        private async Task<Dictionary<string, string>> ValidateProductAsync(string name, string categoryId, string stallCode, Guid? existingId)
        {
            var errors = ModelStateErrors(ModelState);
            if (errors.Count > 0) return errors;

            if (!Guid.TryParse(categoryId, out var catId) || !await _inventory.CategoryExistsAsync(catId))
                errors["category_id"] = _L["ValCategory"];

            // the same product name may exist in different stalls, but not twice in one stall
            if (await _inventory.ProductExistsAsync(name, stallCode, existingId))
                errors["name"] = _L["ValProductExists"];

            return errors;
        }
    }
}
