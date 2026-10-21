using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace IMSB.Backend
{
    public class Applications : BackendBase, IApplicationsInterface
    {
        public Applications(IMSBContext context) : base(context) { }

        public Task<ApplicationStats> StatsAsync() => StatsAsync(_context);

        /// <summary>Counts per status, plus approvals decided this month (local time). Shared with the Dashboard, Notifications and Reports.</summary>
        internal static async Task<ApplicationStats> StatsAsync(IMSBContext ctx)
        {
            var monthStartUtc = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToUniversalTime();
            var counts = await CountByAsync(ctx.ArtisanApplications.Select(a => a.Status));
            return new ApplicationStats(
                Total: counts.Values.Sum(),
                Approved: counts.GetValueOrDefault(ApplicationStatuses.Approved),
                UnderReview: counts.GetValueOrDefault(ApplicationStatuses.UnderReview),
                Pending: counts.GetValueOrDefault(ApplicationStatuses.Pending),
                Rejected: counts.GetValueOrDefault(ApplicationStatuses.Rejected),
                ApprovedThisMonth: await ctx.ArtisanApplications.CountAsync(a => a.Status == ApplicationStatuses.Approved && a.DecidedAt >= monthStartUtc));
        }

        public Task<List<ArtisanApplication>> FilterAsync(string? q, string? status, string? categoryId, string? district) =>
            ApplyFilters(_context.ArtisanApplications.AsNoTracking().Include(a => a.Category), q, status, categoryId, district)
                .OrderByDescending(a => a.DecidedAt).ThenBy(a => a.SubmittedDate).ThenBy(a => a.ApplicationNo).ToListAsync();

        public Task<List<ArtisanApplication>> ExportAsync(string? q, string? status, string? categoryId, string? district) =>
            ApplyFilters(_context.ArtisanApplications.AsNoTracking().Include(a => a.Category), q, status, categoryId, district)
                .OrderBy(a => a.ApplicationNo).ToListAsync();

        private static IQueryable<ArtisanApplication> ApplyFilters(IQueryable<ArtisanApplication> query, string? q, string? status, string? categoryId, string? district)
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                var digits = Regex.Replace(term, @"\D", "");
                query = query.Where(a => a.FullName.ToLower().Contains(term) || a.ApplicationNo.ToLower().Contains(term)
                    || a.District.ToLower().Contains(term)
                    || (digits.Length >= 3 && a.Mobile.Contains(digits))
                    || (a.ArtisanCode != null && a.ArtisanCode.ToLower().Contains(term))
                    || (a.StallCode != null && a.StallCode.ToLower().Contains(term)));
            }
            var st = status?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(st) && ApplicationStatuses.All.Contains(st)) query = query.Where(a => a.Status == st);
            if (!string.IsNullOrWhiteSpace(categoryId) && Guid.TryParse(categoryId, out var catId)) query = query.Where(a => a.CategoryId == catId);
            if (!string.IsNullOrWhiteSpace(district))
            {
                var d = district.Trim().ToLower();
                query = query.Where(a => a.District.ToLower() == d);
            }
            return query;
        }

        public Task<List<Stall>> VacantStallsAsync() =>
            _context.Stalls.AsNoTracking().Include(s => s.Category)
                .Where(s => s.Status == StallStatuses.Vacant).OrderBy(s => s.Block).ThenBy(s => s.StallCode)
                .ToListAsync();

        public Task<ArtisanApplication?> GetDetailsAsync(string applicationNo) =>
            _context.ArtisanApplications.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.ApplicationNo == applicationNo);

        public Task<ArtisanApplication?> FindAsync(string applicationNo) =>
            _context.ArtisanApplications.FirstOrDefaultAsync(a => a.ApplicationNo == applicationNo);

        public Task<List<string>> ApplicationNumbersAsync() => _context.ArtisanApplications.Select(a => a.ApplicationNo).ToListAsync();

        public Task<List<string>> ArtisanCodesAsync() => _context.Artisans.Select(a => a.ArtisanCode).ToListAsync();

        public Task<bool> ArtisanMobileExistsAsync(string mobile) => _context.Artisans.AnyAsync(a => a.Mobile == mobile);

        public Task<bool> ArtisanAadhaarExistsAsync(string aadhaarHash) => _context.Artisans.AnyAsync(a => a.AadhaarHash == aadhaarHash);

        public Task<bool> ArtisanExistsAsync(string mobile, string aadhaarHash) =>
            _context.Artisans.AnyAsync(a => a.Mobile == mobile || a.AadhaarHash == aadhaarHash);

        public Task<bool> OpenApplicationMobileExistsAsync(string mobile, Guid exceptId) =>
            OpenApplications(exceptId).AnyAsync(a => a.Mobile == mobile);

        public Task<bool> OpenApplicationAadhaarExistsAsync(string aadhaarHash, Guid exceptId) =>
            OpenApplications(exceptId).AnyAsync(a => a.AadhaarHash == aadhaarHash);

        private IQueryable<ArtisanApplication> OpenApplications(Guid exceptId) =>
            _context.ArtisanApplications.Where(a => a.Id != exceptId
                && (a.Status == ApplicationStatuses.Pending || a.Status == ApplicationStatuses.UnderReview));

        public Task<Stall?> FindStallAsync(string stallCode) => _context.Stalls.FirstOrDefaultAsync(s => s.StallCode == stallCode);

        public void Add(ArtisanApplication application) => _context.ArtisanApplications.Add(application);

        public void Remove(ArtisanApplication application) => _context.ArtisanApplications.Remove(application);

        public void AddArtisan(Artisan artisan) => _context.Artisans.Add(artisan);
    }
}
