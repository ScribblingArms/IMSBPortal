using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Notifications : BackendBase, INotificationsInterface
    {
        private static readonly string[] SeverityOrder = { "danger", "warning", "info" };

        public Notifications(IMSBContext context) : base(context) { }

        public async Task<List<Notification>> BuildAsync(bool isAdmin, bool isInventory, bool isStaff, int expiryWindowDays)
        {
            var today = DateTime.Today;
            var soon = today.AddDays(expiryWindowDays);
            var list = new List<Notification>();
            void Add(string type, string severity, int count, string url)
            {
                if (count > 0) list.Add(new Notification($"{type}:{count}", type, severity, count, url));
            }

            if (isAdmin)
            {
                var apps = await OpenApplicationCountsAsync();
                Add("APPS_PENDING", "warning", apps.GetValueOrDefault(ApplicationStatuses.Pending), "/Applications/Index?tab=pending");
                Add("APPS_REVIEW", "info", apps.GetValueOrDefault(ApplicationStatuses.UnderReview), "/Applications/Index?tab=review");
                Add("REALLOCATIONS_PENDING", "warning", await _context.StallReallocations.CountAsync(r => r.Status == ReallocationStatuses.Pending), "/Stalls/Index");

                // expired and expiring agreements in one query
                var agreements = await _context.Stalls
                    .Where(s => s.ArtisanId != null && s.AgreementEndDate != null && s.AgreementEndDate <= soon)
                    .GroupBy(_ => 1)
                    .Select(g => new { Expired = g.Count(s => s.AgreementEndDate < today), Expiring = g.Count(s => s.AgreementEndDate >= today) })
                    .FirstOrDefaultAsync();
                Add("AGREEMENTS_EXPIRED", "danger", agreements?.Expired ?? 0, "/Stalls/Index");
                Add("AGREEMENTS_EXPIRING", "warning", agreements?.Expiring ?? 0, "/Stalls/Index");
                Add("ARTISAN_RENEWALS", "info", await _context.Artisans.CountAsync(a => a.Status == ArtisanStatuses.RenewalDue), "/Artisans/Index");
            }

            if (isAdmin || isInventory)
            {
                var (outOfStock, low) = await StockAlertCountsAsync();
                Add("OUT_OF_STOCK", "danger", outOfStock, "/Inventory/Index");
                Add("LOW_STOCK", "warning", low, "/Inventory/Index");
            }

            // (no staff contract alert: employees are SERP.tbl_Employee rows, which have no contract end date)

            // most urgent first
            return list.OrderBy(n => Array.IndexOf(SeverityOrder, n.Severity)).ToList();
        }

        public async Task<NavBadges> NavBadgesAsync(bool isAdmin, bool isInventory)
        {
            // same rule as StockStatuses.For: out of stock, or below the minimum
            var belowMinimum = isAdmin || isInventory
                ? await _context.Products.CountAsync(p => p.Quantity <= 0 || p.Quantity < p.MinStock)
                : 0;
            var openApplications = isAdmin
                ? await OpenApplications().CountAsync()
                : 0;
            return new NavBadges(belowMinimum, openApplications);
        }

        /// <summary>Applications waiting for a decision (new + under review).</summary>
        private IQueryable<ArtisanApplication> OpenApplications() =>
            _context.ArtisanApplications.Where(a => a.Status == ApplicationStatuses.Pending || a.Status == ApplicationStatuses.UnderReview);

        private Task<Dictionary<string, int>> OpenApplicationCountsAsync() => CountByAsync(OpenApplications().Select(a => a.Status));

        /// <summary>Out-of-stock and low-stock (above zero, below the minimum) products, counted in one query.</summary>
        private async Task<(int OutOfStock, int Low)> StockAlertCountsAsync()
        {
            var counts = await _context.Products
                .Where(p => p.Quantity <= 0 || p.Quantity < p.MinStock)
                .GroupBy(_ => 1)
                .Select(g => new { Out = g.Count(p => p.Quantity <= 0), Low = g.Count(p => p.Quantity > 0) })
                .FirstOrDefaultAsync();
            return (counts?.Out ?? 0, counts?.Low ?? 0);
        }
    }
}
