using System.Text.RegularExpressions;
using IMSB.Backend.Mappers;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Api.Controllers
{
    /// <summary>Global search, limited to the modules the signed-in role can open.</summary>
    public class SearchController : ApiControllerBase
    {
        public const int MinLength = 2;
        public const int PerGroup = 5;

        private static readonly Regex NonDigits = new(@"\D", RegexOptions.Compiled);

        private readonly ISearchInterface _search;

        public SearchController(ISearchInterface search) => _search = search;

        /// <summary>A hit: Key identifies the record for the matching endpoint (e.g. GET /api/artisans/{key}).</summary>
        public record Hit(string Title, string? Subtitle, string Key);
        public record Group(string Type, List<Hit> Items);

        /// <summary>At least two characters; three or more digits also match mobile numbers.</summary>
        [HttpGet]
        public async Task<IActionResult> Query([FromQuery] string? q)
        {
            var term = (q ?? "").Trim();
            if (term.Length < MinLength) return Ok(new { q = term, groups = Array.Empty<Group>() });
            var lower = term.ToLower();
            var digits = NonDigits.Replace(term, "");
            bool Can(params string[] roles) => roles.Any(User.IsInRole);
            var groups = new List<Group>();

            if (Can("Admin"))
            {
                Add(groups, "ARTISANS", (await _search.ArtisansAsync(lower, digits, PerGroup))
                    .Select(a => new Hit(a.FullName, a.ArtisanCode + " · " + a.District, a.ArtisanCode)));
                Add(groups, "APPLICATIONS", (await _search.ApplicationsAsync(lower, digits, PerGroup))
                    .Select(a => new Hit(a.FullName, a.ApplicationNo + " · " + StatusLabels.Application(L, a.Status), a.ApplicationNo)));
                Add(groups, "STALLS", (await _search.StallsAsync(lower, PerGroup))
                    .Select(s => new Hit(s.StallCode + " – " + s.Name, s.Artisan?.FullName, s.StallCode)));
            }
            if (Can("Admin", "Staff"))
            {
                Add(groups, "STAFF", (await _search.StaffAsync(lower, digits, PerGroup))
                    .Select(s => new Hit(s.FullName, s.EmployeeId + " · " + s.Designation, s.EmployeeId)));
            }
            if (Can("Admin", "Inventory"))
            {
                Add(groups, "PRODUCTS", (await _search.ProductsAsync(lower, PerGroup))
                    .Select(p => new Hit(p.Name, p.ProductCode + " · " + p.StallCode, p.ProductCode)));
            }
            return Ok(new { q = term, groups });
        }

        private static void Add(List<Group> groups, string type, IEnumerable<Hit> items)
        {
            var list = items.ToList();
            if (list.Count > 0) groups.Add(new Group(type, list));
        }
    }
}
