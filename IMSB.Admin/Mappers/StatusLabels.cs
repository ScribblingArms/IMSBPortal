using IMSB.Dto.Models;
using Microsoft.Extensions.Localization;

namespace IMSB.Admin.Mappers
{
    /// <summary>Localized display text for the status codes stored in the database (CSV / PDF exports, search hits).</summary>
    public static class StatusLabels
    {
        public static string Application(IStringLocalizer L, string status) => status switch
        {
            ApplicationStatuses.UnderReview => L["UnderReviewLabel"],
            ApplicationStatuses.Approved => L["ApprovedLabel"],
            ApplicationStatuses.Rejected => L["RejectedLabel"],
            _ => L["PendingLabel"]
        };

        public static string Artisan(IStringLocalizer L, string status) => status switch
        {
            ArtisanStatuses.OnLeave => L["OnLeave"],
            ArtisanStatuses.RenewalDue => L["RenewalPending"],
            ArtisanStatuses.Inactive => L["Inactive"],
            _ => L["Active"]
        };

        public static string Stall(IStringLocalizer L, string status) => status switch
        {
            StallStatuses.Occupied => L["Occupied"],
            StallStatuses.Reserved => L["Reserved"],
            StallStatuses.Maintenance => L["UnderMaintenance"],
            _ => L["Vacant"]
        };

        public static string Stock(IStringLocalizer L, string status) => status switch
        {
            StockStatuses.OutOfStock => L["OutOfStock"],
            StockStatuses.LowStock => L["LowStock"],
            _ => L["InStock"]
        };
    }
}
