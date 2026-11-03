using System.Globalization;
using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>Daily sales entries; saving, editing or deleting one moves the products' stock to match.</summary>
    [Authorize(Roles = "Admin")]
    public class SalesController : ApiControllerBase
    {
        private const int MaxItemsPerEntry = 100;
        private const int DeductionLogSize = 100;

        private readonly ISalesInterface _sales;
        private readonly BazaarOptions _bazaar;

        public SalesController(ISalesInterface sales, IOptions<BazaarOptions> bazaar)
        {
            _sales = sales;
            _bazaar = bazaar.Value;
        }

        /// <summary>One day (yyyy-MM-dd, default today): summary, entries, charts and the product / stall pickers.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? date, [FromQuery] string? stall)
        {
            if (!TryParseDay(date, out var day)) return Invalid("date", "ValDateInvalid");

            var totalStalls = await _sales.TotalStallsAsync(_bazaar);
            var summary = await _sales.SummaryAsync(day, totalStalls);
            var todaySummary = day == DateTime.Today ? summary : await _sales.SummaryAsync(DateTime.Today, totalStalls);
            var stallFilter = stall?.Trim().ToUpperInvariant();
            var categories = await _sales.ByCategoryAsync(day, day.AddDays(1));
            var hourly = await _sales.HourlyAsync(day);
            var products = await _sales.ProductsAsync();
            var saleStalls = await _sales.SaleStallsAsync();

            return Ok(new
            {
                date = day.ToString("yyyy-MM-dd"),
                summary = summary.ToItem(),
                today = todaySummary.ToItem(),
                entries = (await _sales.EntriesOnAsync(day, string.IsNullOrEmpty(stallFilter) ? null : stallFilter)).Select(e => e.ToItem()),
                categoryChart = new { labels = categories.Select(c => c.Category), values = categories.Select(c => c.Revenue) },
                hourlyChart = new { hours = hourly.Select(h => h.Hour), values = hourly.Select(h => h.Revenue) },
                productOptions = products.Select(p => p.ToSaleOption()),
                categories = (await _sales.CategoriesAsync()).ToOptions(),
                stalls = products.Select(p => p.StallCode).Concat(saleStalls).Select(s => s.ToUpperInvariant()).Distinct().OrderBy(s => s)
            });
        }

        [HttpGet("{entryNo}")]
        public async Task<IActionResult> Get(string entryNo)
        {
            var entry = await _sales.GetEntryAsync(entryNo);
            return entry == null ? NotFound() : Ok(entry.ToItem());
        }

        /// <summary>Product-wise sales: one row per product per day.</summary>
        [HttpGet("product-report")]
        public async Task<IActionResult> ProductReport([FromQuery] string? q, [FromQuery] string? categoryId, [FromQuery] string? stall,
            [FromQuery] string? from, [FromQuery] string? to)
        {
            var errors = ValidateRange(from, to, out var fromDay, out var toDay);
            if (errors.Count > 0) return Invalid(errors);

            var rows = await _sales.ProductReportRowsAsync(q, categoryId, stall, fromDay, toDay);
            return Ok(new
            {
                rows,
                totals = new { quantity = rows.Sum(r => r.Quantity), revenue = rows.Sum(r => r.Revenue), products = rows.Select(r => r.Code).Distinct().Count() }
            });
        }

        /// <summary>The stock movements written by sales (deductions and reversals), newest first.</summary>
        [HttpGet("deduction-log")]
        public async Task<IActionResult> DeductionLog([FromQuery] string? date)
        {
            DateTime? day = null;
            if (!string.IsNullOrWhiteSpace(date))
            {
                if (!TryParseDate(date, out var d)) return Invalid("date", "ValDateInvalid");
                day = d;
            }
            var (movements, products) = await _sales.DeductionLogAsync(day, DeductionLogSize);
            return Ok(movements.Select(m =>
                m.ToDeductionItem(m.ProductId != null && products.TryGetValue(m.ProductId.Value, out var p) ? p : null)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SalesEntryDto dto)
        {
            var (errors, soldAt, lines) = await ValidateAsync(dto, null);
            if (errors.Count > 0) return Invalid(errors);

            var entry = new SalesEntry
            {
                EntryNo = await _sales.NextEntryNoAsync(),
                SoldAt = soldAt,
                Remarks = CleanRemarks(dto.Remarks),
                CreatedBy = UserName
            };
            SetItems(entry, lines);
            // auto stock deduction
            var movements = lines.GroupBy(l => l.Product.Id)
                .Select(g => Move(g.First().Product, StockMovementTypes.Sale, g.Sum(l => l.Quantity), soldAt, entry.EntryNo))
                .ToList();

            await _sales.AddAsync(entry, movements);
            return Ok(new { entryNo = entry.EntryNo, total = entry.TotalAmount });
        }

        /// <summary>Only the net change per product touches stock: more sold -> SALE, fewer sold -> SALE_REVERSAL.</summary>
        [HttpPut("{entryNo}")]
        public async Task<IActionResult> Update(string entryNo, [FromBody] SalesEntryDto dto)
        {
            var entry = await _sales.FindEntryAsync(entryNo);
            if (entry == null) return NotFound();
            var (errors, soldAt, lines) = await ValidateAsync(dto, entry);
            if (errors.Count > 0) return Invalid(errors);

            var oldQty = SoldPerProduct(entry);
            var newQty = lines.GroupBy(l => l.Product.Id).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            var movements = new List<StockMovement>();
            // products deleted since the sale are not returned: nothing to put back
            foreach (var (productId, product) in await _sales.ProductsByIdAsync(oldQty.Keys.Union(newQty.Keys)))
            {
                var delta = newQty.GetValueOrDefault(productId) - oldQty.GetValueOrDefault(productId);
                if (delta > 0) movements.Add(Move(product, StockMovementTypes.Sale, delta, soldAt, entry.EntryNo));
                else if (delta < 0) movements.Add(Move(product, StockMovementTypes.SaleReversal, -delta, soldAt, entry.EntryNo));
            }

            var oldItems = entry.Items.ToList();
            entry.Items.Clear();
            SetItems(entry, lines);
            entry.SoldAt = soldAt;
            entry.Remarks = CleanRemarks(dto.Remarks);
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = UserName;
            await _sales.UpdateAsync(entry, oldItems, movements);
            return Ok(new { entryNo = entry.EntryNo, total = entry.TotalAmount });
        }

        /// <summary>Deletes the entry and puts the sold quantities back into stock.</summary>
        [HttpDelete("{entryNo}")]
        public async Task<IActionResult> Delete(string entryNo)
        {
            var entry = await _sales.FindEntryAsync(entryNo);
            if (entry == null) return NotFound();

            var sold = SoldPerProduct(entry);
            var movements = (await _sales.ProductsByIdAsync(sold.Keys))
                .Select(p => Move(p.Value, StockMovementTypes.SaleReversal, sold[p.Key], entry.SoldAt, entry.EntryNo))
                .ToList();
            await _sales.DeleteAsync(entry, movements);
            return Ok(new { entryNo });
        }

        private record Line(Product Product, int Quantity, decimal UnitPrice);

        /// <summary>Validates a submission; for an edit, the quantities already sold by that entry count as available again.</summary>
        private async Task<(Dictionary<string, string> Errors, DateTime SoldAt, List<Line> Lines)> ValidateAsync(SalesEntryDto dto, SalesEntry? existing)
        {
            var errors = new Dictionary<string, string>();
            var soldAt = DateTime.MinValue;
            var lines = new List<Line>();

            if (!TryParseDay(dto.Date, out var day)) errors["date"] = L["ValDateInvalid"];
            else if (day > DateTime.Today) errors["date"] = L["ValDateFuture"];
            else
            {
                var time = !string.IsNullOrWhiteSpace(dto.Time) && TimeSpan.TryParseExact(dto.Time, @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? t
                    : existing != null && existing.SoldAt.Date == day ? existing.SoldAt.TimeOfDay
                    : day == DateTime.Today ? new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0)
                    : new TimeSpan(12, 0, 0);
                soldAt = day + time;
                if (soldAt > DateTime.Now) errors["time"] = L["ValTimeFuture"];
            }

            var items = dto.Items ?? new List<SaleItemDto>();
            if (items.Count == 0) { errors["items"] = L["ValSalesItemsRequired"]; return (errors, soldAt, lines); }
            if (items.Count > MaxItemsPerEntry) { errors["items"] = L["ValSalesItemsMax", MaxItemsPerEntry]; return (errors, soldAt, lines); }

            var products = await _sales.ProductsByCodeAsync(items.Select(i => i.ProductCode?.Trim() ?? "").Where(c => c != "").Distinct().ToList());
            var alreadySold = existing == null ? new Dictionary<Guid, int>() : SoldPerProduct(existing);
            var seen = new HashSet<string>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var code = item.ProductCode?.Trim() ?? "";
                var prefix = $"items_{i}_";
                if (!products.TryGetValue(code, out var product)) { errors.TryAdd(prefix + "product_code", L["ValSelectProduct"]); continue; }
                if (!seen.Add(code)) { errors.TryAdd(prefix + "product_code", L["ValDuplicateProduct"]); continue; }

                var available = product.Quantity + alreadySold.GetValueOrDefault(product.Id);
                if (item.Quantity > available) { errors[prefix + "quantity"] = L["ValInsufficientStock", available]; continue; }
                lines.Add(new Line(product, item.Quantity, Math.Round(item.UnitPrice, 2)));
            }
            return (errors, soldAt, lines);
        }

        /// <summary>Quantity per product on an entry's lines (lines of deleted products are skipped).</summary>
        private static Dictionary<Guid, int> SoldPerProduct(SalesEntry entry) =>
            entry.Items.Where(i => i.ProductId != null).GroupBy(i => i.ProductId!.Value).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        private static void SetItems(SalesEntry entry, List<Line> lines)
        {
            foreach (var l in lines) entry.Items.Add(l.Product.ToSaleItem(entry.Id, l.Quantity, l.UnitPrice));
            entry.TotalQuantity = entry.Items.Sum(i => i.Quantity);
            entry.TotalAmount = entry.Items.Sum(i => i.LineTotal);
        }

        /// <summary>Changes the product's quantity and returns the matching stock history row (saved by the backend).</summary>
        private StockMovement Move(Product product, string type, int quantity, DateTime soldAt, string entryNo)
        {
            var after = type == StockMovementTypes.Sale ? product.Quantity - quantity : product.Quantity + quantity;
            var movement = product.ToMovement(type, quantity, after, soldAt.Date, entryNo, UserName);
            product.Quantity = after;
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = UserName;
            return movement;
        }

        private static string? CleanRemarks(string? remarks) => string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        /// <summary>yyyy-MM-dd; an empty value means today.</summary>
        private static bool TryParseDay(string? value, out DateTime day)
        {
            if (string.IsNullOrWhiteSpace(value)) { day = DateTime.Today; return true; }
            return TryParseDate(value, out day);
        }

        private Dictionary<string, string> ValidateRange(string? from, string? to, out DateTime? fromDay, out DateTime? toDay)
        {
            var errors = new Dictionary<string, string>();
            fromDay = toDay = null;
            if (!string.IsNullOrWhiteSpace(from))
            {
                if (TryParseDate(from, out var f)) fromDay = f;
                else errors["from"] = L["ValDateInvalid"];
            }
            if (!string.IsNullOrWhiteSpace(to))
            {
                if (TryParseDate(to, out var t)) toDay = t;
                else errors["to"] = L["ValDateInvalid"];
            }
            if (fromDay != null && toDay != null && fromDay > toDay) errors["to"] = L["ValDateRange"];
            return errors;
        }
    }
}
