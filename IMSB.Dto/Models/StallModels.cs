using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Dto.Models
{
    // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)

    /// <summary>Create / edit a stall. StallCode is used on create only (it is fixed afterwards).</summary>
    public class StallDto
    {
        [RegularExpression("^[A-Za-z0-9-]{1,20}$", ErrorMessage = "ValStallCode")]
        public string? StallCode { get; set; }

        [Required(ErrorMessage = "ValStallName"), StringLength(100, MinimumLength = 2, ErrorMessage = "ValStallName")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "ValBlock"), RegularExpression("^[A-Za-z0-9 -]{1,20}$", ErrorMessage = "ValBlock")]
        public string Block { get; set; } = "";

        [StringLength(20, ErrorMessage = "ValRow")]
        public string? RowLabel { get; set; }

        [StringLength(500, ErrorMessage = "ValLocation")]
        public string? Location { get; set; }

        /// <summary>VACANT, RESERVED or MAINTENANCE for a free stall; an occupied stall stays OCCUPIED.</summary>
        public string? Status { get; set; }

        public string? CategoryId { get; set; }

        [Range(typeof(decimal), "0", "10000000", ErrorMessage = "ValRent")]
        public decimal? MonthlyRent { get; set; }

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    public class AllocateStallDto
    {
        [Required(ErrorMessage = "ValSelectStall")]
        public string StallCode { get; set; } = "";

        [Required(ErrorMessage = "ValSelectArtisan")]
        public string ArtisanCode { get; set; } = "";

        /// <summary>Optional; defaults to the artisan's craft category.</summary>
        public string? CategoryId { get; set; }

        [Required(ErrorMessage = "ValDate")]
        public string AllottedDate { get; set; } = "";   // yyyy-MM-dd

        public string? AgreementEndDate { get; set; }    // yyyy-MM-dd

        [Range(typeof(decimal), "0", "10000000", ErrorMessage = "ValRent")]
        public decimal? MonthlyRent { get; set; }

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    public class ReallocationDto
    {
        [Required(ErrorMessage = "ValSelectStall")]
        public string StallCode { get; set; } = "";

        [Required(ErrorMessage = "ValReason")]
        public string Reason { get; set; } = "";

        /// <summary>Who gets the stall on approval; empty = vacate it.</summary>
        public string? NewArtisanCode { get; set; }

        [Required(ErrorMessage = "ValDate")]
        public string RequestedDate { get; set; } = "";

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    public class DecisionDto
    {
        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    public record StallStats(int Total, int Occupied, int Vacant, int Reserved, int Maintenance, int PendingReallocation);

    /// <summary>
    /// Stall capacity and occupancy for the Dashboard and Sales pages: the stalls table once stalls are registered,
    /// otherwise the configured capacity with stalls that have inventory counted as occupied.
    /// </summary>
    public static class BazaarStats
    {
        public static async Task<(int Total, int Occupied)> StallsAsync(IMSBContext ctx, BazaarOptions options)
        {
            var registered = await ctx.Stalls.CountAsync();
            if (registered > 0)
                return (registered, await ctx.Stalls.CountAsync(s => s.Status == StallStatuses.Occupied));

            var withStock = (await ctx.Products.Select(p => p.StallCode).ToListAsync())
                .Select(s => s.Trim().ToUpperInvariant()).Distinct().Count();
            return (Math.Max(options.TotalStalls, withStock), withStock);
        }
    }
}
