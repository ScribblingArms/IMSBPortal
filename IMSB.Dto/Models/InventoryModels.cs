using System.ComponentModel.DataAnnotations;

namespace IMSB.Dto.Models
{
    // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)
    public class CreateProductDto
    {
        [Required(ErrorMessage = "ValProductName"), StringLength(200, MinimumLength = 2, ErrorMessage = "ValProductName")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "ValCategory")]
        public string CategoryId { get; set; } = "";

        [Required(ErrorMessage = "ValStall"), RegularExpression("^[A-Za-z0-9-]{1,20}$", ErrorMessage = "ValStall")]
        public string StallCode { get; set; } = "";

        /// <summary>Initial quantity (create only; afterwards quantity changes go through stock updates).</summary>
        [Range(0, 1_000_000, ErrorMessage = "ValQuantity")]
        public int Quantity { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "ValMinStock")]
        public int MinStock { get; set; }

        [Range(typeof(decimal), "0", "10000000", ErrorMessage = "ValUnitPrice")]
        public decimal UnitPrice { get; set; }
    }

    /// <summary>Edit keeps the quantity unchanged; stock is changed through StockUpdateDto so it is audited.</summary>
    public class UpdateProductDto
    {
        [Required(ErrorMessage = "ValProductName"), StringLength(200, MinimumLength = 2, ErrorMessage = "ValProductName")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "ValCategory")]
        public string CategoryId { get; set; } = "";

        [Required(ErrorMessage = "ValStall"), RegularExpression("^[A-Za-z0-9-]{1,20}$", ErrorMessage = "ValStall")]
        public string StallCode { get; set; } = "";

        [Range(0, 1_000_000, ErrorMessage = "ValMinStock")]
        public int MinStock { get; set; }

        [Range(typeof(decimal), "0", "10000000", ErrorMessage = "ValUnitPrice")]
        public decimal UnitPrice { get; set; }
    }

    /// <summary>Header cards on the Inventory page. "BelowMinimum" = low + out of stock (the alert banner).</summary>
    public record InventoryStats(int Total, int InStock, int LowStock, int OutOfStock, int BelowMinimum, int Categories, int StallsTracked);

    public class StockUpdateDto
    {
        [Required(ErrorMessage = "ValSelectProduct")]
        public string ProductCode { get; set; } = "";

        [Required(ErrorMessage = "ValUpdateType"), RegularExpression("^(ADD|REDUCE|CORRECTION)$", ErrorMessage = "ValUpdateType")]
        public string UpdateType { get; set; } = "";

        [Range(0, 1_000_000, ErrorMessage = "ValQuantity")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "ValDate")]
        public string Date { get; set; } = "";   // yyyy-MM-dd

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }
}
