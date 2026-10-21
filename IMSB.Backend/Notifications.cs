using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Notifications : BackendBase, INotificationsInterface
    {
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
                var apps = await Applications.StatsAsync(_context);
                Add("APPS_PENDING", "warning", apps.Pending, "/Applications/Index?tab=pending");
                Add("APPS_REVIEW", "info", apps.UnderReview, "/Applications/Index?tab=review");
                Add("REALLOCATIONS_PENDING", "warning", await _context.StallReallocations.CountAsync(r => r.Status == ReallocationStatuses.Pending), "/Stalls/Index");
                Add("AGREEMENTS_EXPIRED", "danger",
                    await _context.Stalls.CountAsync(s => s.ArtisanId != null && s.AgreementEndDate != null && s.AgreementEndDate < today), "/Stalls/Index");
                Add("AGREEMENTS_EXPIRING", "warning",
                    await _context.Stalls.CountAsync(s => s.ArtisanId != null && s.AgreementEndDate >= today && s.AgreementEndDate <= soon), "/Stalls/Index");
                Add("ARTISAN_RENEWALS", "info", await _context.Artisans.CountAsync(a => a.Status == ArtisanStatuses.RenewalDue), "/Artisans/Index");
            }

            if (isAdmin || isInventory)
            {
                Add("OUT_OF_STOCK", "danger", await _context.Products.CountAsync(p => p.Quantity <= 0), "/Inventory/Index");
                Add("LOW_STOCK", "warning", await _context.Products.CountAsync(p => p.Quantity > 0 && p.Quantity < p.MinStock), "/Inventory/Index");
            }

            if (isAdmin || isStaff)
            {
                Add("CONTRACTS_EXPIRING", "warning",
                    await _context.Staff.CountAsync(s => s.Status == "ACTIVE" && s.ContractEndDate >= today && s.ContractEndDate <= soon), "/Staff/Index");
            }

            // most urgent first
            var order = new[] { "danger", "warning", "info" };
            return list.OrderBy(n => Array.IndexOf(order, n.Severity)).ToList();
        }
    }
}
