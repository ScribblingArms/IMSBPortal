using IMSB.Dto.Models;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Mappers
{
    public record ApplicationListItem(string Id, string FullName, string Mobile, string District, string CategoryId, string? CategoryName,
        string Status, string SubmittedDate, int? DaysWaiting, string? Reviewer, string DecidedDate, string? DecidedBy, string? DecisionRemarks,
        string? ArtisanCode, string? StallCode);

    /// <summary>One application with every field; the Aadhaar is masked.</summary>
    public record ApplicationDetails(string Id, string FullName, string DateOfBirth, string Gender, string Mobile, string? Email, string Address,
        string AadhaarMasked, string District, string CategoryId, string? CategoryName, int YearsOfExperience, string? Skills, string? Remarks,
        string Status, string SubmittedDate, int? DaysWaiting, string? Reviewer, string DecidedDate, string? DecidedBy, string? DecisionRemarks,
        string? ArtisanCode, string? StallCode);

    public record VacantStallOption(string Code, string Block, string? CategoryName);

    public static class ApplicationMapper
    {
        internal static int DaysSince(DateTime date) => Math.Max(0, (DateTime.Today - date.Date).Days);

        /// <summary>Days since submission while the application is open; null once decided.</summary>
        public static int? DaysWaiting(this ArtisanApplication a) => ApplicationStatuses.IsOpen(a.Status) ? DaysSince(a.SubmittedDate) : null;

        public static ApplicationListItem ToListItem(this ArtisanApplication a) => new(
            a.ApplicationNo, a.FullName, a.Mobile, a.District, a.CategoryId.ToString(), a.Category?.Name,
            a.Status, Date(a.SubmittedDate), a.DaysWaiting(), a.Reviewer, LocalDate(a.DecidedAt), a.DecidedBy, a.DecisionRemarks,
            a.ArtisanCode, a.StallCode);

        public static ApplicationDetails ToDetails(this ArtisanApplication a) => new(
            a.ApplicationNo, a.FullName, Date(a.DateOfBirth), a.Gender, a.Mobile, a.Email, a.Address,
            a.MaskedAadhaar, a.District, a.CategoryId.ToString(), a.Category?.Name, a.YearsOfExperience, a.Skills, a.Remarks,
            a.Status, Date(a.SubmittedDate), a.DaysWaiting(), a.Reviewer, LocalDate(a.DecidedAt), a.DecidedBy, a.DecisionRemarks,
            a.ArtisanCode, a.StallCode);

        public static VacantStallOption ToVacantOption(this Stall s) => new(s.StallCode, s.Block, s.Category?.Name);

        /// <summary>The ACTIVE artisan registered when an application is approved: the applicant's details carried over.</summary>
        public static Artisan ToArtisan(this ArtisanApplication app, string artisanCode, string remarks, string? createdBy) => new()
        {
            ArtisanCode = artisanCode,
            FullName = app.FullName,
            DateOfBirth = app.DateOfBirth,
            Gender = app.Gender,
            Mobile = app.Mobile,
            Email = app.Email,
            Address = app.Address,
            AadhaarHash = app.AadhaarHash,
            AadhaarLast4 = app.AadhaarLast4,
            District = app.District,
            CategoryId = app.CategoryId,
            YearsOfExperience = app.YearsOfExperience,
            Skills = app.Skills,
            Status = ArtisanStatuses.Active,
            JoinedDate = DateTime.Today,
            Remarks = remarks,
            CreatedBy = createdBy
        };
    }
}
