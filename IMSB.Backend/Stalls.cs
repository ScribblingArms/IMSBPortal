using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Stalls : BackendBase, IStallsInterface
    {
        public Stalls(IMSBContext context) : base(context) { }

        public async Task<StallStats> StatsAsync()
        {
            var counts = await CountByAsync(_context.Stalls.Select(s => s.Status));
            return new StallStats(
                Total: counts.Values.Sum(),
                Occupied: counts.GetValueOrDefault(StallStatuses.Occupied),
                Vacant: counts.GetValueOrDefault(StallStatuses.Vacant),
                Reserved: counts.GetValueOrDefault(StallStatuses.Reserved),
                Maintenance: counts.GetValueOrDefault(StallStatuses.Maintenance),
                PendingReallocation: await _context.StallReallocations.CountAsync(r => r.Status == ReallocationStatuses.Pending));
        }

        public Task<List<Stall>> AllAsync() =>
            _context.Stalls.AsNoTracking().Include(s => s.Category).Include(s => s.Artisan)
                .OrderBy(s => s.Block).ThenBy(s => s.StallCode).ToListAsync();

        public async Task<HashSet<Guid>> FilteredIdsAsync(string? q, string? status, string? categoryId, string? block) =>
            (await ApplyFilters(_context.Stalls.AsNoTracking(), q, status, categoryId, block).Select(s => s.Id).ToListAsync()).ToHashSet();

        public Task<List<Stall>> FilterAsync(string? q, string? status, string? categoryId, string? block) =>
            ApplyFilters(_context.Stalls.AsNoTracking().Include(s => s.Category).Include(s => s.Artisan), q, status, categoryId, block)
                .OrderBy(s => s.Block).ThenBy(s => s.StallCode).ToListAsync();

        /// <summary>Shared with the Reports backend. q matches code, name, location and the artisan's name or code.</summary>
        internal static IQueryable<Stall> ApplyFilters(IQueryable<Stall> query, string? q, string? status, string? categoryId, string? block)
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(s => s.StallCode.ToLower().Contains(term) || s.Name.ToLower().Contains(term)
                    || (s.Location != null && s.Location.ToLower().Contains(term))
                    || (s.Artisan != null && (s.Artisan.FullName.ToLower().Contains(term) || s.Artisan.ArtisanCode.ToLower().Contains(term))));
            }
            var st = status?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(st) && StallStatuses.All.Contains(st)) query = query.Where(s => s.Status == st);
            if (!string.IsNullOrWhiteSpace(categoryId) && Guid.TryParse(categoryId, out var catId)) query = query.Where(s => s.CategoryId == catId);
            if (!string.IsNullOrWhiteSpace(block))
            {
                var b = block.Trim().ToUpper();
                query = query.Where(s => s.Block.ToUpper() == b);
            }
            return query;
        }

        public async Task<HashSet<Guid>> PendingReallocationStallIdsAsync() =>
            (await _context.StallReallocations.AsNoTracking().Where(r => r.Status == ReallocationStatuses.Pending)
                .Select(r => r.StallId).ToListAsync()).ToHashSet();

        public Task<Dictionary<string, int>> ProductCountsAsync() =>
            CountByAsync(_context.Products.Select(p => p.StallCode.Trim().ToUpper()));

        public Task<int> ProductCountAsync(string stallCode)
        {
            var code = stallCode.ToUpper();
            return _context.Products.CountAsync(p => p.StallCode.ToUpper() == code);
        }

        public Task<List<Artisan>> FreeArtisansAsync() =>
            _context.Artisans.AsNoTracking().Include(a => a.Category)
                .Where(a => a.Status != ArtisanStatuses.Inactive && !_context.Stalls.Any(s => s.ArtisanId == a.Id))
                .OrderBy(a => a.FullName).ToListAsync();

        public Task<List<StallReallocation>> RecentReallocationsAsync(int take) =>
            _context.StallReallocations.AsNoTracking()
                .OrderBy(r => r.Status == ReallocationStatuses.Pending ? 0 : 1).ThenByDescending(r => r.CreatedAt)
                .Take(take).ToListAsync();

        public Task<bool> StallCodeExistsAsync(string stallCode) => _context.Stalls.AnyAsync(s => s.StallCode == stallCode);

        public Task<Stall?> FindAsync(string stallCode, bool includeArtisan = false)
        {
            var query = includeArtisan ? _context.Stalls.Include(s => s.Artisan) : _context.Stalls.AsQueryable();
            return query.FirstOrDefaultAsync(s => s.StallCode == stallCode);
        }

        public Task<Stall?> FindByIdAsync(Guid id) => _context.Stalls.FirstOrDefaultAsync(s => s.Id == id);

        public Task<Artisan?> FindArtisanAsync(string artisanCode) => _context.Artisans.FirstOrDefaultAsync(a => a.ArtisanCode == artisanCode);

        public Task<Artisan?> FindArtisanByIdAsync(Guid id) => _context.Artisans.FirstOrDefaultAsync(a => a.Id == id);

        public Task<bool> ArtisanHasStallAsync(Guid artisanId) => _context.Stalls.AnyAsync(s => s.ArtisanId == artisanId);

        public Task<bool> HasPendingReallocationAsync(Guid stallId) =>
            _context.StallReallocations.AnyAsync(r => r.StallId == stallId && r.Status == ReallocationStatuses.Pending);

        public Task<List<StallReallocation>> OpenReallocationsAsync(Guid stallId) =>
            _context.StallReallocations.Where(r => r.Status == ReallocationStatuses.Pending && r.StallId == stallId).ToListAsync();

        public Task<StallReallocation?> FindReallocationAsync(string requestNo) =>
            _context.StallReallocations.FirstOrDefaultAsync(r => r.RequestNo == requestNo);

        public Task<List<string>> ReallocationNumbersAsync() => _context.StallReallocations.Select(r => r.RequestNo).ToListAsync();

        public void Add(Stall stall) => _context.Stalls.Add(stall);

        public void Remove(Stall stall) => _context.Stalls.Remove(stall);

        public void AddReallocation(StallReallocation request) => _context.StallReallocations.Add(request);
    }
}
