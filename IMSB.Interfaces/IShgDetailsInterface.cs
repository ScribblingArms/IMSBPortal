using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>
    /// The public stall application form (website): SHG, representative, product, stall and declaration steps saved in
    /// SHGDetails, their uploads, the final submission and the form's lookups. The controller only validates the posted
    /// fields and turns the <see cref="ShgFormResult"/> into a response.
    /// </summary>
    public interface IShgDetailsInterface
    {
        /// <summary>Districts by name and the product categories in display order (names trimmed, blank ones left out).</summary>
        Task<ShgFormLookups> FormLookupsAsync();

        Task<bool> DistrictExistsAsync(int districtId);

        Task<bool> CategoryExistsAsync(int categoryId);

        /// <summary>Today in India time (UTC+5:30, no DST): the form is for Telangana, so "next month" is worked out there.</summary>
        DateTime IndiaNow();

        /// <summary>Product photos already saved for the application (0 when there is none).</summary>
        Task<int> SavedPhotoCountAsync(Guid? publicId);

        /// <summary>
        /// Checks one upload by its bytes, not its name: photos must be JPG or PNG, documents (<paramref name="allowPdf"/>)
        /// may also be PDF, 2 MB at most. Returns null, or the English message with {0} for the file name (to localize).
        /// </summary>
        Task<string?> CheckUploadAsync(UploadedFile file, bool allowPdf = false);

        /// <summary>Step 1: the first save creates the row (new public id); saving with that id again updates it.</summary>
        Task<ShgFormResult> SaveShgDetailsAsync(Guid? publicId, Action<ShgDetail> apply);

        /// <summary>Steps 2 and 4 (no uploads): applies the posted values to the row created by step 1.</summary>
        Task<ShgFormResult> SaveStepAsync(Guid? publicId, string step, Action<ShgDetail> apply);

        /// <summary>Step 3: product details; new photos replace the saved ones, none keeps them.</summary>
        Task<ShgFormResult> SaveProductDetailsAsync(Guid? publicId, IReadOnlyList<UploadedFile> photos, Action<ShgDetail> apply);

        /// <summary>Step 5: the declarations; a new SHG document / identity proof replaces the saved one, none keeps it.</summary>
        Task<ShgFormResult> SaveDeclarationAsync(Guid? publicId, UploadedFile? shgDocument, UploadedFile? idProof, Action<ShgDetail> apply);

        /// <summary>
        /// Gives a fully saved application its reference number (IMSB-yyyy-00001) and locks it. Submitting again returns
        /// the same reference; <see cref="ShgFormStatus.Incomplete"/> names the first step still to be saved.
        /// </summary>
        Task<ShgFormResult> SubmitAsync(Guid? publicId);
    }

    /// <summary>An uploaded file, without tying the backend to ASP.NET's IFormFile.</summary>
    public sealed record UploadedFile(string FileName, long Length, Func<Stream> OpenReadStream);

    public sealed record ShgFormLookups(List<District> Districts, List<ProductCategory> Categories);

    public enum ShgFormStatus
    {
        Saved,
        /// <summary>No application with that public id: the visitor has to start again from step 1.</summary>
        NotFound,
        AlreadySubmitted,
        /// <summary>An officer rejected the application (possibly before it was submitted): it can no longer be changed or submitted.</summary>
        Rejected,
        /// <summary>Submit with a step not saved yet (<see cref="ShgFormResult.MissingStep"/>).</summary>
        Incomplete,
        /// <summary>The database or the file store failed; already logged.</summary>
        Failed
    }

    public sealed record ShgFormResult(ShgFormStatus Status, Guid? PublicId = null, string? Reference = null,
        int Files = 0, int? MissingStep = null)
    {
        public bool Succeeded => Status == ShgFormStatus.Saved;
    }
}
