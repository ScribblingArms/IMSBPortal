using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    /// <summary>SHGDetails for the admin portal's SHG Applications page: reading, rejecting and stall allotment.</summary>
    public class ShgApplications : BackendBase, IShgApplicationsInterface
    {
        public ShgApplications(IMSBContext context) : base(context) { }

        private IQueryable<ShgDetail> InScope(int? districtId)
        {
            var q = _context.ShgDetails.AsNoTracking().Include(s => s.District).Include(s => s.ProductCategory).AsQueryable();
            return districtId is int id ? q.Where(s => s.DistrictId == id) : q;
        }

        public Task<List<ShgDetail>> ListAsync(int? districtId) =>
            InScope(districtId).OrderByDescending(s => s.SubmittedAt ?? s.CreatedAt).ThenByDescending(s => s.Id).ToListAsync();

        public Task<ShgDetail?> FindAsync(Guid publicId, int? districtId) =>
            InScope(districtId).FirstOrDefaultAsync(s => s.PublicId == publicId);

        public Task<List<District>> DistrictsAsync() =>
            _context.Districts.AsNoTracking().OrderBy(d => d.Name).ToListAsync();

        public Task<string?> LoginDistrictAsync(int loginId) =>
            _context.Logins.AsNoTracking().Where(l => l.Id == loginId).Select(l => l.District).FirstOrDefaultAsync();

        public async Task<bool> RejectAsync(int shgDetailId, string reason, string? rejectedBy, DateTime rejectedAt)
        {
            var app = await _context.ShgDetails.FirstOrDefaultAsync(s => s.Id == shgDetailId);
            // submitted or still incomplete; never twice
            if (app is null || app.IsRejected) return false;
            // checked again here so a stall allotted meanwhile is never left on a rejected application
            if (await _context.Stalls.AnyAsync(s => s.ShgDetailId == shgDetailId)) return false;

            app.Reject(reason, rejectedBy, rejectedAt);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int shgDetailId)
        {
            var app = await _context.ShgDetails.FirstOrDefaultAsync(s => s.Id == shgDetailId);
            if (app is null) return false;
            // checked again here (stalls.ShgDetailId is a restricting foreign key): a stall allotted meanwhile must be vacated first
            if (await _context.Stalls.AnyAsync(s => s.ShgDetailId == shgDetailId)) return false;

            _context.ShgDetails.Remove(app);
            await _context.SaveChangesAsync();
            return true;
        }

        public Task<Dictionary<int, Stall>> AllottedStallsAsync() =>
            _context.Stalls.AsNoTracking().Where(s => s.ShgDetailId != null).ToDictionaryAsync(s => s.ShgDetailId!.Value);

        public Task<Stall?> StallOfAsync(int shgDetailId) =>
            _context.Stalls.AsNoTracking().FirstOrDefaultAsync(s => s.ShgDetailId == shgDetailId);

        public Task<List<Stall>> VacantStallsAsync() =>
            _context.Stalls.AsNoTracking().Where(s => s.Status == StallStatuses.Vacant)
                .OrderBy(s => s.Block).ThenBy(s => s.StallCode).ToListAsync();

        public Task<Stall?> FindStallAsync(string stallCode) =>
            _context.Stalls.FirstOrDefaultAsync(s => s.StallCode == stallCode);

        public Task SaveStallAsync(Stall stall) => _context.SaveChangesAsync();
    }
}
