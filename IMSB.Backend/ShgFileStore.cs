using IMSB.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace IMSB.Backend
{
    /// <summary>
    /// Product photos and supporting documents of the stall application, kept outside wwwroot (never publicly served):
    /// {Uploads:ShgPhotosPath}/{publicId}/{random}.jpg|png|pdf. Default folder: App_Data/shg-photos under the content root.
    /// </summary>
    public class ShgFileStore
    {
        public const long MaxBytes = 2 * 1024 * 1024;
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D }; // "%PDF-"

        private readonly string _root;

        public ShgFileStore(IConfiguration configuration, IWebHostEnvironment env)
        {
            var configured = configuration["Uploads:ShgPhotosPath"];
            _root = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "App_Data", "shg-photos")
                : Path.GetFullPath(configured, env.ContentRootPath);
        }

        /// <summary>Null when the file is acceptable, otherwise the English message ({0} = file name).</summary>
        public static async Task<string?> CheckAsync(UploadedFile file, bool allowPdf)
        {
            if (file.Length == 0) return "{0} is empty";
            if (file.Length > MaxBytes) return "{0} must be smaller than 2 MB";
            return await DetectExtensionAsync(file, allowPdf) is null
                ? allowPdf ? "{0} must be a JPG, PNG or PDF" : "{0} must be a JPG or PNG"
                : null;
        }

        /// <summary>Saves the files for an application under new random names; all or nothing. Returns the stored names.</summary>
        public async Task<List<string>> SaveAsync(Guid publicId, IEnumerable<UploadedFile> files, bool allowPdf = false)
        {
            var dir = FolderFor(publicId);
            Directory.CreateDirectory(dir);

            var saved = new List<string>();
            try
            {
                foreach (var file in files)
                {
                    var ext = await DetectExtensionAsync(file, allowPdf) ?? throw new InvalidOperationException("Unsupported file type");
                    var name = $"{Guid.NewGuid():N}{ext}";
                    await using var source = file.OpenReadStream();
                    await using var target = File.Create(Path.Combine(dir, name));
                    await source.CopyToAsync(target);
                    saved.Add(name);
                }
            }
            catch
            {
                Delete(dir, saved);
                throw;
            }
            return saved;
        }

        /// <summary>Removes stored files (best effort), e.g. the previous set after a replacement was saved.</summary>
        public void Delete(Guid publicId, IEnumerable<string> names) => Delete(FolderFor(publicId), names);

        private string FolderFor(Guid publicId) => Path.Combine(_root, publicId.ToString("N"));

        private static void Delete(string dir, IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                // stored names are generated here; never follow a path out of the folder
                var path = Path.Combine(dir, Path.GetFileName(name));
                try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static async Task<string?> DetectExtensionAsync(UploadedFile file, bool allowPdf)
        {
            var head = new byte[8];
            await using var s = file.OpenReadStream();
            var read = await s.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false);
            if (read >= Jpeg.Length && head.AsSpan(0, Jpeg.Length).SequenceEqual(Jpeg)) return ".jpg";
            if (read >= Png.Length && head.AsSpan(0, Png.Length).SequenceEqual(Png)) return ".png";
            if (allowPdf && read >= Pdf.Length && head.AsSpan(0, Pdf.Length).SequenceEqual(Pdf)) return ".pdf";
            return null;
        }
    }
}
