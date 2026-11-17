using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IMSB.Backend
{
    /// <summary>The website's stall application form: every step's save, its uploads and the final submission.</summary>
    public class ShgDetails : BackendBase, IShgDetailsInterface
    {
        private static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);

        private readonly ShgFileStore _files;
        private readonly TimeProvider _time;
        private readonly ILogger<ShgDetails> _logger;

        public ShgDetails(IMSBContext context, ShgFileStore files, TimeProvider time, ILogger<ShgDetails> logger) : base(context)
        {
            _files = files;
            _time = time;
            _logger = logger;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        public DateTime IndiaNow() => UtcNow + IndiaOffset;

        // ───────────── lookups ─────────────

        public async Task<ShgFormLookups> FormLookupsAsync()
        {
            var districts = await _context.Districts.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            var categories = await CategoriesAsync();
            // some names in product_categories carry stray tabs / line breaks
            categories.ForEach(c => c.Name = c.Name.Trim());
            return new ShgFormLookups(districts, categories.Where(c => c.Name.Length > 0).ToList());
        }

        public Task<bool> DistrictExistsAsync(int districtId) => _context.Districts.AnyAsync(d => d.Id == districtId);

        public async Task<int> SavedPhotoCountAsync(Guid? publicId)
        {
            if (publicId is not Guid id) return 0;
            var photos = await _context.ShgDetails.AsNoTracking().Where(s => s.PublicId == id)
                .Select(s => s.ProductPhotos).FirstOrDefaultAsync();
            return string.IsNullOrEmpty(photos) ? 0 : photos.Split('|', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public Task<string?> CheckUploadAsync(UploadedFile file, bool allowPdf = false) => ShgFileStore.CheckAsync(file, allowPdf);

        // ───────────── steps ─────────────

        public async Task<ShgFormResult> SaveShgDetailsAsync(Guid? publicId, Action<ShgDetail> apply)
        {
            try
            {
                var details = publicId is Guid id ? await FindAsync(id) : null;
                if (details is { IsRejected: true }) return new(ShgFormStatus.Rejected);
                if (details is { IsSubmitted: true }) return new(ShgFormStatus.AlreadySubmitted);

                if (details is null)
                {
                    // a new row, or an id we do not know (e.g. the row was removed): start a new application
                    details = new ShgDetail();
                    apply(details);
                    _context.ShgDetails.Add(details);
                }
                else
                {
                    apply(details);
                    details.UpdatedAt = UtcNow;
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("SHG details saved for {ShgId} (row {Id})", details.ShgId, details.Id);
                return new(ShgFormStatus.Saved, details.PublicId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not save SHG details for application {PublicId}", publicId);
                return new(ShgFormStatus.Failed);
            }
        }

        public Task<ShgFormResult> SaveStepAsync(Guid? publicId, string step, Action<ShgDetail> apply) =>
            UpdateAsync(publicId, step, (details, _) => { apply(details); return Task.CompletedTask; });

        public Task<ShgFormResult> SaveProductDetailsAsync(Guid? publicId, IReadOnlyList<UploadedFile> photos, Action<ShgDetail> apply) =>
            UpdateAsync(publicId, "Product details", async (details, files) =>
            {
                if (photos.Count > 0)
                {
                    files.Written.AddRange(await _files.SaveAsync(details.PublicId, photos));
                    files.Replaced.AddRange(details.ProductPhotoFiles);
                    details.ProductPhotos = string.Join('|', files.Written);
                }
                apply(details);
                files.Count = details.ProductPhotoFiles.Count;
            });

        public Task<ShgFormResult> SaveDeclarationAsync(Guid? publicId, UploadedFile? shgDocument, UploadedFile? idProof,
            Action<ShgDetail> apply) =>
            UpdateAsync(publicId, "Declaration", async (details, files) =>
            {
                if (shgDocument is not null)
                    details.ShgDocument = await ReplaceDocumentAsync(details, shgDocument, details.ShgDocument, files);
                if (idProof is not null)
                    details.IdProofDocument = await ReplaceDocumentAsync(details, idProof, details.IdProofDocument, files);
                apply(details);
                files.Count = files.Written.Count;
            });

        public async Task<ShgFormResult> SubmitAsync(Guid? publicId)
        {
            var details = publicId is Guid id ? await FindAsync(id) : null;
            if (details is null) return new(ShgFormStatus.NotFound);
            if (details.IsRejected) return new(ShgFormStatus.Rejected);
            if (details.IsSubmitted) return new(ShgFormStatus.Saved, details.PublicId, details.ApplicationRef);

            var missing = details.MissingSteps;
            if (missing.Count > 0) return new(ShgFormStatus.Incomplete, details.PublicId, MissingStep: missing[0]);

            try
            {
                details.SubmittedAt = UtcNow;
                details.UpdatedAt = details.SubmittedAt;
                details.ApplicationRef = $"IMSB-{IndiaNow().Year}-{details.Id:D5}";
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not submit application for row {Id}", details.Id);
                return new(ShgFormStatus.Failed);
            }

            _logger.LogInformation("Application {Reference} submitted for {ShgId} (row {Id})", details.ApplicationRef, details.ShgId, details.Id);
            return new(ShgFormStatus.Saved, details.PublicId, details.ApplicationRef);
        }

        // ───────────── shared ─────────────

        private Task<ShgDetail?> FindAsync(Guid publicId) =>
            _context.ShgDetails.FirstOrDefaultAsync(s => s.PublicId == publicId);

        /// <summary>Files written and replaced by one step's save.</summary>
        private sealed class StepFiles
        {
            public List<string> Written { get; } = new();
            public List<string> Replaced { get; } = new();
            public int Count { get; set; }
        }

        /// <summary>
        /// Steps 2-5: loads the row created by step 1, applies the step and saves it. When the save fails the row still
        /// points at the previous files, so only the files written now are removed; after a save the replaced ones go.
        /// </summary>
        private async Task<ShgFormResult> UpdateAsync(Guid? publicId, string step, Func<ShgDetail, StepFiles, Task> apply)
        {
            var details = publicId is Guid id ? await FindAsync(id) : null;
            if (details is null) return new(ShgFormStatus.NotFound);
            if (details.IsRejected) return new(ShgFormStatus.Rejected);
            if (details.IsSubmitted) return new(ShgFormStatus.AlreadySubmitted);

            var files = new StepFiles();
            try
            {
                await apply(details, files);
                details.UpdatedAt = UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _files.Delete(details.PublicId, files.Written);
                _logger.LogError(ex, "Could not save {Step} for row {Id}", step, details.Id);
                return new(ShgFormStatus.Failed);
            }

            _files.Delete(details.PublicId, files.Replaced);
            _logger.LogInformation("{Step} saved for {ShgId} (row {Id}, {Files} files)", step, details.ShgId, details.Id, files.Count);
            return new(ShgFormStatus.Saved, details.PublicId, Files: files.Count);
        }

        private async Task<string> ReplaceDocumentAsync(ShgDetail details, UploadedFile file, string? previous, StepFiles files)
        {
            var name = (await _files.SaveAsync(details.PublicId, new[] { file }, allowPdf: true))[0];
            files.Written.Add(name);
            if (previous is not null) files.Replaced.Add(previous);
            return name;
        }
    }
}
