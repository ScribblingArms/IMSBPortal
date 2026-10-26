using IMSB.Admin.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SalesController : Controller
    {
        private const int MaxItemsPerEntry = 100;
        private const int DeductionLogSize = 100;

        private readonly ISalesInterface _sales;
        private readonly IStringLocalizer _L;
        private readonly BazaarOptions _bazaar;

        public SalesController(ISalesInterface sales, IStringLocalizerFactory localizerFactory, IOptions<BazaarOptions> bazaar)
        {
            _sales = sales;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(SalesController).Assembly.GetName().Name!);
            _bazaar = bazaar.Value;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = _L["Sales"].Value;
            ViewBag.Summary = await _sales.SummaryAsync(DateTime.Today, await _sales.TotalStallsAsync(_bazaar));
            return View();
        }

        // Everything the Daily Sales Entry and Sales Chart tabs need for one day
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? date, [FromQuery] string? stall)
        {
            if (!TryParseDay(date, out var day)) return ErrorResult("date", _L["ValDateInvalid"]);

            // summary bar follows the selected date; the "today" stat cards always show today
            var totalStalls = await _sales.TotalStallsAsync(_bazaar);
            var summary = await _sales.SummaryAsync(day, totalStalls);
            var todaySummary = day == DateTime.Today ? summary : await _sales.SummaryAsync(DateTime.Today, totalStalls);

            var stallFilter = stall?.Trim().ToUpperInvariant();
            var entries = await _sales.EntriesOnAsync(day, string.IsNullOrEmpty(stallFilter) ? null : stallFilter);
            var categories = await _sales.ByCategoryAsync(day, day.AddDays(1));
            var hourly = await _sales.HourlyAsync(day);
            var products = await _sales.ProductsAsync();
            var saleStalls = await _sales.SaleStallsAsync();

            return Ok(new
            {
                date = day.ToString("yyyy-MM-dd"),
                summary = summary.ToItem(),
                today = todaySummary.ToItem(),
                entries = entries.Select(e => e.ToItem()),
                categoryChart = new { labels = categories.Select(c => c.Category), values = categories.Select(c => c.Revenue) },
                hourlyChart = new { hours = hourly.Select(h => h.Hour), values = hourly.Select(h => h.Revenue) },
                productOptions = products.Select(p => p.ToSaleOption()),
                categories = (await _sales.CategoriesAsync()).ToOptions(),
                stalls = products.Select(p => p.StallCode).Concat(saleStalls).Select(s => s.ToUpperInvariant()).Distinct().OrderBy(s => s)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Get(string id)
        {
            var entry = await _sales.GetEntryAsync(id);
            return entry == null ? NotFound() : Ok(entry.ToItem());
        }

        /// <summary>Product-wise Sales Report: one row per product per day, with every filter the page offers.</summary>
        [HttpGet]
        public async Task<IActionResult> ProductReport([FromQuery] string? q, [FromQuery] string? categoryId, [FromQuery] string? stall,
            [FromQuery] string? from, [FromQuery] string? to)
        {
            var errors = ValidateRange(from, to, out var fromDay, out var toDay);
            if (errors.Count > 0) return BadRequest(new { errors });

            var rows = await _sales.ProductReportRowsAsync(q, categoryId, stall, fromDay, toDay);
            return Ok(new
            {
                rows,
                totals = new { quantity = rows.Sum(r => r.Quantity), revenue = rows.Sum(r => r.Revenue), products = rows.Select(r => r.Code).Distinct().Count() }
            });
        }

        /// <summary>Auto Stock Deduction Log: the stock movements written by sales (deductions and reversals).</summary>
        [HttpGet]
        public async Task<IActionResult> DeductionLog([FromQuery] string? date)
        {
            DateTime? day = null;
            if (!string.IsNullOrWhiteSpace(date))
            {
                if (!TryParseDate(date, out var d)) return ErrorResult("date", _L["ValDateInvalid"]);
                day = d;
            }
            var (movements, products) = await _sales.DeductionLogAsync(day, DeductionLogSize);
            return Ok(movements.Select(m =>
                m.ToDeductionItem(m.ProductId != null && products.TryGetValue(m.ProductId.Value, out var p) ? p : null)));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] SalesEntryDto? dto)
        {
            if (dto == null) return BadRequest();
            var (errors, soldAt, lines) = await ValidateAsync(dto, null);
            if (errors.Count > 0) return BadRequest(new { errors });

            var user = User.Identity?.Name;
            var entry = new SalesEntry
            {
                EntryNo = await _sales.NextEntryNoAsync(),
                SoldAt = soldAt,
                Remarks = CleanRemarks(dto.Remarks),
                CreatedBy = user
            };
            SetItems(entry, lines);

            // auto stock deduction
            var movements = lines.GroupBy(l => l.Product.Id)
                .Select(g => Move(g.First().Product, StockMovementTypes.Sale, g.Sum(l => l.Quantity), soldAt, entry.EntryNo, user))
                .ToList();

            await _sales.AddAsync(entry, movements);
            return Ok(new { entryNo = entry.EntryNo, total = entry.TotalAmount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string id, [FromBody] SalesEntryDto? dto)
        {
            if (dto == null) return BadRequest();
            var entry = await _sales.FindEntryAsync(id);
            if (entry == null) return NotFound();

            var (errors, soldAt, lines) = await ValidateAsync(dto, entry);
            if (errors.Count > 0) return BadRequest(new { errors });

            // only the net change per product touches stock: more sold -> SALE, fewer sold -> SALE_REVERSAL
            var user = User.Identity?.Name;
            var oldQty = SoldPerProduct(entry);
            var newQty = lines.GroupBy(l => l.Product.Id).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            var products = await _sales.ProductsByIdAsync(oldQty.Keys.Union(newQty.Keys));
            var movements = new List<StockMovement>();
            foreach (var (productId, product) in products)
            {
                var delta = newQty.GetValueOrDefault(productId) - oldQty.GetValueOrDefault(productId);
                if (delta > 0) movements.Add(Move(product, StockMovementTypes.Sale, delta, soldAt, entry.EntryNo, user));
                else if (delta < 0) movements.Add(Move(product, StockMovementTypes.SaleReversal, -delta, soldAt, entry.EntryNo, user));
            }
            // products deleted since the sale are not in the dictionary: nothing to put back

            var oldItems = entry.Items.ToList();
            entry.Items.Clear();
            SetItems(entry, lines);
            entry.SoldAt = soldAt;
            entry.Remarks = CleanRemarks(dto.Remarks);
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = user;
            await _sales.UpdateAsync(entry, oldItems, movements);
            return Ok(new { entryNo = entry.EntryNo, total = entry.TotalAmount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var entry = await _sales.FindEntryAsync(id);
            if (entry == null) return NotFound();

            // put the sold quantities back into stock
            var user = User.Identity?.Name;
            var sold = SoldPerProduct(entry);
            var movements = (await _sales.ProductsByIdAsync(sold.Keys))
                .Select(p => Move(p.Value, StockMovementTypes.SaleReversal, sold[p.Key], entry.SoldAt, entry.EntryNo, user))
                .ToList();

            await _sales.DeleteAsync(entry, movements);
            return Ok(new { entryNo = id });
        }

        /// <summary>Line items of the sales entries in a date range (default: today), optionally for one stall.</summary>
        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? stall)
        {
            var errors = ValidateRange(from, to, out var fromDay, out var toDay);
            if (errors.Count > 0) return BadRequest(new { errors });
            var start = fromDay ?? toDay ?? DateTime.Today;
            var end = (toDay ?? fromDay ?? DateTime.Today).AddDays(1);

            var items = await _sales.ExportRowsAsync(start, end, stall);

            var exportRows = new List<string?[]>
            {
                new string?[] { _L["EntryNo"], _L["Date"], _L["Time"], _L["ProductCode"], _L["ProductName"], _L["Category"], _L["Stall"],
                    _L["QtySold"], _L["UnitPrice"], _L["LineTotal"], _L["UpdatedBy"] }
            };
            foreach (var i in items.OrderBy(i => i.SoldAt).ThenBy(i => i.EntryNo))
            {
                exportRows.Add(new string?[] { i.EntryNo, i.SoldAt.ToString("yyyy-MM-dd"), i.SoldAt.ToString("HH:mm"), i.ProductCode, i.ProductName, i.CategoryName, i.StallCode,
                    Int(i.Quantity), Money(i.UnitPrice), Money(i.LineTotal), i.CreatedBy ?? string.Empty });
            }
            exportRows.Add(new string?[] { _L["Total"], "", "", "", "", "", "", Int(items.Sum(i => i.Quantity)), "", Money(items.Sum(i => i.LineTotal)), "" });
            return ExportCsv($"sales_{start:yyyyMMdd}_{end.AddDays(-1):yyyyMMdd}.csv", exportRows);
        }

        [HttpGet]
        public async Task<IActionResult> ExportProductReport([FromQuery] string? q, [FromQuery] string? categoryId, [FromQuery] string? stall,
            [FromQuery] string? from, [FromQuery] string? to)
        {
            var errors = ValidateRange(from, to, out var fromDay, out var toDay);
            if (errors.Count > 0) return BadRequest(new { errors });
            var rows = await _sales.ProductReportRowsAsync(q, categoryId, stall, fromDay, toDay);

            var exportRows = new List<string?[]>
            {
                new string?[] { _L["Date"], _L["ProductCode"], _L["ProductName"], _L["Category"], _L["Stall"], _L["QtySold"], _L["UnitPrice"], _L["Revenue"], _L["StockLeft"] }
            };
            foreach (var r in rows)
            {
                exportRows.Add(new string?[] { r.Date, r.Code, r.Name, r.CategoryName, r.StallCode, Int(r.Quantity),
                    Money(r.UnitPrice), Money(r.Revenue), r.StockLeft?.ToString(CultureInfo.InvariantCulture) ?? string.Empty });
            }
            return ExportCsv($"product_sales_{DateTime.Now:yyyyMMddHHmmss}.csv", exportRows);
        }

        // ── helpers ──────────────────────────────────────────────────────

        internal record Line(Product Product, int Quantity, decimal UnitPrice);

        /// <summary>Validates a submission; for an edit, the quantities already sold by that entry count as available again.</summary>
        private async Task<(Dictionary<string, string> Errors, DateTime SoldAt, List<Line> Lines)> ValidateAsync(SalesEntryDto dto, SalesEntry? existing)
        {
            // "Items[2].UnitPrice" -> "items_2_unit_price", the form's field names
            var errors = ModelStateErrors(ModelState);
            var soldAt = DateTime.MinValue;
            var lines = new List<Line>();

            if (!errors.ContainsKey("date"))
            {
                if (!TryParseDay(dto.Date, out var day)) errors["date"] = _L["ValDateInvalid"];
                else if (day > DateTime.Today) errors["date"] = _L["ValDateFuture"];
                else
                {
                    var time = !string.IsNullOrWhiteSpace(dto.Time) && TimeSpan.TryParseExact(dto.Time, @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? t
                        : existing != null && existing.SoldAt.Date == day ? existing.SoldAt.TimeOfDay
                        : day == DateTime.Today ? new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0)
                        : new TimeSpan(12, 0, 0);
                    soldAt = day + time;
                    if (soldAt > DateTime.Now && !errors.ContainsKey("time")) errors["time"] = _L["ValTimeFuture"];
                }
            }

            var items = dto.Items ?? new List<SaleItemDto>();
            if (items.Count == 0) { errors["items"] = _L["ValSalesItemsRequired"]; return (errors, soldAt, lines); }
            if (items.Count > MaxItemsPerEntry) { errors["items"] = string.Format(_L["ValSalesItemsMax"], MaxItemsPerEntry); return (errors, soldAt, lines); }

            var codes = items.Select(i => i.ProductCode?.Trim() ?? "").Where(c => c != "").Distinct().ToList();
            var products = await _sales.ProductsByCodeAsync(codes);
            var alreadySold = existing == null ? new Dictionary<Guid, int>() : SoldPerProduct(existing);

            var seen = new HashSet<string>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var code = item.ProductCode?.Trim() ?? "";
                var prefix = $"items_{i}_";
                if (!products.TryGetValue(code, out var product))
                {
                    errors.TryAdd(prefix + "product_code", _L["ValSelectProduct"]);
                    continue;
                }
                if (!seen.Add(code)) { errors.TryAdd(prefix + "product_code", _L["ValDuplicateProduct"]); continue; }
                // DataAnnotations already cover these when MVC binds the body; checked again for direct callers
                if (item.Quantity < 1) errors.TryAdd(prefix + "quantity", _L["ValQuantityPositive"]);
                if (item.UnitPrice < 0) errors.TryAdd(prefix + "unit_price", _L["ValUnitPrice"]);
                if (errors.ContainsKey(prefix + "quantity") || errors.ContainsKey(prefix + "unit_price")) continue;

                var available = product.Quantity + alreadySold.GetValueOrDefault(product.Id);
                if (item.Quantity > available) { errors[prefix + "quantity"] = string.Format(_L["ValInsufficientStock"], available); continue; }
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
        private static StockMovement Move(Product product, string type, int quantity, DateTime soldAt, string entryNo, string? user)
        {
            var after = type == StockMovementTypes.Sale ? product.Quantity - quantity : product.Quantity + quantity;
            var movement = product.ToMovement(type, quantity, after, soldAt.Date, entryNo, user);
            product.Quantity = after;
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = user;
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
                else errors["from"] = _L["ValDateInvalid"];
            }
            if (!string.IsNullOrWhiteSpace(to))
            {
                if (TryParseDate(to, out var t)) toDay = t;
                else errors["to"] = _L["ValDateInvalid"];
            }
            if (fromDay != null && toDay != null && fromDay > toDay) errors["to"] = _L["ValDateRange"];
            return errors;
        }
    }
}
