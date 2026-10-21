using IMSB.Admin.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Text.RegularExpressions;

namespace IMSB.Admin.Controllers
{
    /// <summary>
    /// The header search box. Any signed-in user can search, but only within the modules their role can open
    /// (the same rules as the controllers' [Authorize(Roles = ...)] and the sidebar).
    /// </summary>
    public class SearchController : Controller
    {
        public const int MinLength = 2;
        public const int PerGroup = 5;

        private static readonly Regex NonDigits = new(@"\D", RegexOptions.Compiled);

        private readonly ISearchInterface _search;
        private readonly IStringLocalizer _L;

        public SearchController(ISearchInterface search, IStringLocalizerFactory localizerFactory)
        {
            _search = search;
            _L = localizerFactory.Create("SharedResource", typeof(SearchController).Assembly.GetName().Name!);
        }

        /// <summary>Navigation pages: (resource key, url, roles that may open it; empty = everyone).</summary>
        internal static readonly (string Key, string Url, string Icon, string[] Roles)[] Pages =
        {
            ("Dashboard", "/Dashboard/Index", "fa-gauge-high", new[] { "Admin" }),
            ("StallManagement", "/Stalls/Index", "fa-store", new[] { "Admin" }),
            ("Inventory", "/Inventory/Index", "fa-boxes-stacked", new[] { "Admin", "Inventory" }),
            ("Sales", "/Sales/Index", "fa-chart-line", new[] { "Admin" }),
            ("Artisans", "/Artisans/Index", "fa-palette", new[] { "Admin" }),
            ("Applications", "/Applications/Index", "fa-file-circle-check", new[] { "Admin" }),
            ("StaffHR", "/Staff/Index", "fa-users", new[] { "Admin", "Staff" }),
            ("Attendance", "/Attendance/Index", "fa-calendar-check", new[] { "Admin", "Staff" }),
            ("ReportsAnalytics", "/Reports/Index", "fa-chart-pie", new[] { "Admin" }),
            ("ActiveSessions", "/Account/Sessions", "fa-shield-halved", Array.Empty<string>()),
        };

        public record Hit(string Title, string? Subtitle, string Url);
        public record Group(string Type, string Icon, List<Hit> Items);

        [HttpGet]
        public async Task<IActionResult> Query([FromQuery] string? q)
        {
            var term = (q ?? "").Trim();
            if (term.Length < MinLength) return Ok(new { q = term, groups = Array.Empty<Group>() });
            var lower = term.ToLower();
            var digits = NonDigits.Replace(term, "");
            bool Can(params string[] roles) => roles.Length == 0 || roles.Any(User.IsInRole);
            var groups = new List<Group>();

            var pages = Pages.Where(p => Can(p.Roles))
                .Select(p => (Page: p, Title: _L[p.Key].Value))
                .Where(x => x.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase) || x.Page.Key.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(x => new Hit(x.Title, null, x.Page.Url)).Take(PerGroup).ToList();
            Add(groups, "PAGES", "fa-compass", pages);

            if (Can("Admin"))
            {
                Add(groups, "ARTISANS", "fa-palette", (await _search.ArtisansAsync(lower, digits, PerGroup))
                    .Select(a => new Hit(a.FullName, a.ArtisanCode + " · " + a.District, "/Artisans/Index?q=" + a.ArtisanCode)).ToList());

                Add(groups, "APPLICATIONS", "fa-file-circle-check", (await _search.ApplicationsAsync(lower, digits, PerGroup))
                    .Select(a => new Hit(a.FullName, a.ApplicationNo + " · " + StatusLabels.Application(_L, a.Status),
                        $"/Applications/Index?tab={ApplicationTab(a.Status)}&q={a.ApplicationNo}")).ToList());

                Add(groups, "STALLS", "fa-store", (await _search.StallsAsync(lower, PerGroup))
                    .Select(s => new Hit(s.StallCode + " – " + s.Name, s.Artisan?.FullName, "/Stalls/Index?q=" + s.StallCode)).ToList());
            }

            if (Can("Admin", "Staff"))
            {
                Add(groups, "STAFF", "fa-users", (await _search.StaffAsync(lower, digits, PerGroup))
                    .Select(s => new Hit(s.FullName, s.EmployeeId + " · " + s.Designation, "/Staff/Index?q=" + s.EmployeeId)).ToList());
            }

            if (Can("Admin", "Inventory"))
            {
                Add(groups, "PRODUCTS", "fa-boxes-stacked", (await _search.ProductsAsync(lower, PerGroup))
                    .Select(p => new Hit(p.Name, p.ProductCode + " · " + p.StallCode, "/Inventory/Index?q=" + p.ProductCode)).ToList());
            }

            return Ok(new { q = term, groups });
        }

        private static void Add(List<Group> groups, string type, string icon, List<Hit> items)
        {
            if (items.Count > 0) groups.Add(new Group(type, icon, items));
        }

        /// <summary>The Applications page tab (data-tab) that lists a status.</summary>
        internal static string ApplicationTab(string status) => status switch
        {
            ApplicationStatuses.Approved => "approved",
            ApplicationStatuses.UnderReview => "review",
            ApplicationStatuses.Rejected => "rejected",
            _ => "pending"
        };
    }
}
