using System.ComponentModel.DataAnnotations;

namespace IMSB.Dto.Models
{
    // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)

    /// <summary>A Daily Sales Entry submission (create and edit use the same shape).</summary>
    public class SalesEntryDto
    {
        [Required(ErrorMessage = "ValDate")]
        public string Date { get; set; } = "";   // yyyy-MM-dd

        /// <summary>Optional HH:mm; defaults to the current time for today and 12:00 for earlier dates.</summary>
        [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "ValTime")]
        public string? Time { get; set; }

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }

        public List<SaleItemDto> Items { get; set; } = new();
    }

    public class SaleItemDto
    {
        [Required(ErrorMessage = "ValSelectProduct")]
        public string ProductCode { get; set; } = "";

        [Range(1, 100_000, ErrorMessage = "ValQuantityPositive")]
        public int Quantity { get; set; }

        [Range(typeof(decimal), "0", "10000000", ErrorMessage = "ValUnitPrice")]
        public decimal UnitPrice { get; set; }
    }

    /// <summary>Summary bar + stat cards on the Sales page for one day.</summary>
    public record SalesSummary(
        DateTime Date,
        decimal TotalSales,
        int ItemsSold,
        int Transactions,
        int ActiveStalls,
        decimal PreviousDaySales,
        decimal? ChangeVsPreviousDay,   // percent; null when the previous day had no sales
        decimal MonthSales,
        decimal PreviousMonthSales,
        decimal? ChangeVsPreviousMonth,
        decimal AverageTransaction,
        int TotalStalls);
}
