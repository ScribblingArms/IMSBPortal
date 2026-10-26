using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace IMSB.Backend
{
    public class Artisans : BackendBase, IArtisansInterface
    {
        public Artisans(IMSBContext context) : base(context) { }

        public async Task<ArtisanStats> StatsAsync()
        {
            var counts = await CountByAsync(_context.Artisans.Select(a => a.Status));
            return new ArtisanStats(
                Total: counts.Values.Sum(),
                Active: counts.GetValueOrDefault(ArtisanStatuses.Active),
                OnLeave: counts.GetValueOrDefault(ArtisanStatuses.OnLeave),
                RenewalPending: counts.GetValueOrDefault(ArtisanStatuses.RenewalDue),
                Inactive: counts.GetValueOrDefault(ArtisanStatuses.Inactive),
                WithStall: await _context.Stalls.CountAsync(s => s.ArtisanId != null));
        }

        public Task<Dictionary<Guid, string>> StallsByArtisanAsync() =>
            _context.Stalls.AsNoTracking().Where(s => s.ArtisanId != null).ToDictionaryAsync(s => s.ArtisanId!.Value, s => s.StallCode);

        public Task<List<Artisan>> FilterAsync(string? q, string? status, string? categoryId, string? district, string? stall) =>
            ApplyFilters(_context.Artisans.AsNoTracking().Include(a => a.Category), q, status, categoryId, district, stall)
                .OrderBy(a => a.FullName).ToListAsync();

        private IQueryable<Artisan> ApplyFilters(IQueryable<Artisan> query, string? q, string? status, string? categoryId, string? district, string? stall)
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                var digits = Regex.Replace(term, @"\D", "");
                query = query.Where(a => a.FullName.ToLower().Contains(term) || a.ArtisanCode.ToLower().Contains(term)
                    || (digits.Length >= 3 && a.Mobile.Contains(digits))
                    || _context.Stalls.Any(s => s.ArtisanId == a.Id && s.StallCode.ToLower().Contains(term)));
            }
            var st = status?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(st) && ArtisanStatuses.All.Contains(st)) query = query.Where(a => a.Status == st);
            if (!string.IsNullOrWhiteSpace(categoryId) && Guid.TryParse(categoryId, out var catId)) query = query.Where(a => a.CategoryId == catId);
            if (!string.IsNullOrWhiteSpace(district))
            {
                var d = district.Trim().ToLower();
                query = query.Where(a => a.District.ToLower() == d);
            }
            switch (stall?.Trim().ToLowerInvariant())
            {
                case "assigned": query = query.Where(a => _context.Stalls.Any(s => s.ArtisanId == a.Id)); break;
                case "unassigned": query = query.Where(a => !_context.Stalls.Any(s => s.ArtisanId == a.Id)); break;
            }
            return query;
        }

        public Task<Artisan?> GetDetailsAsync(string artisanCode) =>
            _context.Artisans.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.ArtisanCode == artisanCode);

        public Task<Artisan?> FindAsync(string artisanCode) => _context.Artisans.FirstOrDefaultAsync(a => a.ArtisanCode == artisanCode);

        public Task<Stall?> StallOfAsync(Guid artisanId) => _context.Stalls.AsNoTracking().FirstOrDefaultAsync(s => s.ArtisanId == artisanId);

        public async Task<(decimal MonthSales, int Products)> StallFiguresAsync(string stallCode)
        {
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var code = stallCode.ToUpper();
            var monthSales = await SalesAnalytics.ItemsBetween(_context, monthStart, DateTime.Today.AddDays(1))
                .Where(i => i.StallCode.ToUpper() == code).SumAsync(i => i.LineTotal);
            var products = await _context.Products.CountAsync(p => p.StallCode.ToUpper() == code);
            return (monthSales, products);
        }

        public Task<List<string>> ArtisanCodesAsync() => _context.Artisans.Select(a => a.ArtisanCode).ToListAsync();

        public Task<bool> MobileExistsAsync(string mobile, Guid exceptId) =>
            _context.Artisans.AnyAsync(a => a.Mobile == mobile && a.Id != exceptId);

        public Task<bool> AadhaarExistsAsync(string aadhaarHash, Guid exceptId) =>
            _context.Artisans.AnyAsync(a => a.AadhaarHash == aadhaarHash && a.Id != exceptId);

        public Task<bool> HasStallAsync(Guid artisanId) => _context.Stalls.AnyAsync(s => s.ArtisanId == artisanId);

        public async Task AddAsync(Artisan artisan)
        {
            _context.Artisans.Add(artisan);
            await _context.SaveChangesAsync();
        }

        public Task UpdateAsync(Artisan artisan) => _context.SaveChangesAsync();

        public async Task DeleteAsync(Artisan artisan, string? user, string closedRemarks)
        {
            var related = await _context.StallReallocations
                .Where(r => r.CurrentArtisanId == artisan.Id || r.NewArtisanId == artisan.Id).ToListAsync();
            foreach (var r in related)
            {
                if (r.Status == ReallocationStatuses.Pending && r.NewArtisanId == artisan.Id)
                {
                    r.Status = ReallocationStatuses.Rejected;
                    r.DecidedAt = DateTime.UtcNow;
                    r.DecidedBy = user;
                    r.DecisionRemarks = closedRemarks;
                }
                if (r.CurrentArtisanId == artisan.Id) r.CurrentArtisanId = null;
                if (r.NewArtisanId == artisan.Id) r.NewArtisanId = null;
            }
            // the approved application stays on record; its artisan_code snapshot keeps the reference
            foreach (var app in await _context.ArtisanApplications.Where(x => x.ArtisanId == artisan.Id).ToListAsync())
                app.ArtisanId = null;
            _context.Artisans.Remove(artisan);
            await _context.SaveChangesAsync();
        }
    }
}
