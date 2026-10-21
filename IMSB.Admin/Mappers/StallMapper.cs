using IMSB.Dto.Models;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Mappers
{
    public record StallListItem(string Code, string Name, string Block, string? RowLabel, string? Location, string Status, string? CategoryId,
        string? CategoryName, string? ArtisanCode, string? ArtisanName, string? AllottedDate, string? AgreementEndDate, decimal? MonthlyRent,
        string? Remarks, int ProductCount, bool HasPendingReallocation);

    public record StallMapItem(string Code, string Block, string Status, string? ArtisanName, string? CategoryName, bool HasPendingReallocation);

    public record FreeArtisanOption(string Code, string Name, string CategoryId, string? CategoryName);

    public record ReallocationItem(string Id, string StallCode, string? CurrentArtisanName, string? NewArtisanName, string Reason, string? Remarks,
        string RequestedDate, string Status, string? DecidedBy, string? DecidedAt, string? DecisionRemarks);

    public static class StallMapper
    {
        private static string? OptionalDate(DateTime? d) => d == null ? null : Date(d);

        public static StallListItem ToListItem(this Stall s, bool pendingReallocation, IReadOnlyDictionary<string, int> productCounts) => new(
            s.StallCode, s.Name, s.Block, s.RowLabel, s.Location, s.Status, s.CategoryId?.ToString(),
            s.Category?.Name, s.Artisan?.ArtisanCode, s.Artisan?.FullName, OptionalDate(s.AllottedDate), OptionalDate(s.AgreementEndDate), s.MonthlyRent,
            s.Remarks, productCounts.GetValueOrDefault(s.StallCode.ToUpperInvariant()), pendingReallocation);

        public static StallMapItem ToMapItem(this Stall s, bool pendingReallocation) =>
            new(s.StallCode, s.Block, s.Status, s.Artisan?.FullName, s.Category?.Name, pendingReallocation);

        public static FreeArtisanOption ToFreeOption(this Artisan a) => new(a.ArtisanCode, a.FullName, a.CategoryId.ToString(), a.Category?.Name);

        public static ReallocationItem ToItem(this StallReallocation r) => new(
            r.RequestNo, r.StallCode, r.CurrentArtisanName, r.NewArtisanName, r.Reason, r.Remarks,
            Date(r.RequestedDate), r.Status, r.DecidedBy, r.DecidedAt == null ? null : Utc(r.DecidedAt.Value).ToString("o"), r.DecisionRemarks);
    }
}
