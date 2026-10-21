using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class Search : BackendBase, ISearchInterface
    {
        public Search(IMSBContext context) : base(context) { }

        public Task<List<Artisan>> ArtisansAsync(string term, string digits, int take) =>
            _context.Artisans.AsNoTracking()
                .Where(a => a.FullName.ToLower().Contains(term) || a.ArtisanCode.ToLower().Contains(term) || (digits.Length >= 3 && a.Mobile.Contains(digits)))
                .OrderBy(a => a.FullName).Take(take).ToListAsync();

        public Task<List<ArtisanApplication>> ApplicationsAsync(string term, string digits, int take) =>
            _context.ArtisanApplications.AsNoTracking()
                .Where(a => a.FullName.ToLower().Contains(term) || a.ApplicationNo.ToLower().Contains(term) || (digits.Length >= 3 && a.Mobile.Contains(digits)))
                .OrderByDescending(a => a.SubmittedDate).Take(take).ToListAsync();

        public Task<List<Stall>> StallsAsync(string term, int take) =>
            _context.Stalls.AsNoTracking().Include(s => s.Artisan)
                .Where(s => s.StallCode.ToLower().Contains(term) || s.Name.ToLower().Contains(term)
                    || (s.Artisan != null && s.Artisan.FullName.ToLower().Contains(term)))
                .OrderBy(s => s.StallCode).Take(take).ToListAsync();

        public Task<List<Staff>> StaffAsync(string term, string digits, int take) =>
            _context.Staff.AsNoTracking()
                .Where(s => s.FullName.ToLower().Contains(term) || s.EmployeeId.ToLower().Contains(term) || (digits.Length >= 3 && s.Mobile.Contains(digits)))
                .OrderBy(s => s.FullName).Take(take).ToListAsync();

        public Task<List<Product>> ProductsAsync(string term, int take) =>
            _context.Products.AsNoTracking()
                .Where(p => p.Name.ToLower().Contains(term) || p.ProductCode.ToLower().Contains(term))
                .OrderBy(p => p.Name).Take(take).ToListAsync();
    }
}
