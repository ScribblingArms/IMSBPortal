using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("product_categories")]
    public class ProductCategory
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("Name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("SortOrder")]
        public int SortOrder { get; set; }
    }

    [Table("products")]
    public class Product
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("ProductCode")]
        [MaxLength(20)]
        public string ProductCode { get; set; } = string.Empty;   // PRD001

        [Required]
        [Column("Name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column("CategoryId")]
        public Guid CategoryId { get; set; }

        public ProductCategory? Category { get; set; }

        [Required]
        [Column("StallCode")]
        [MaxLength(20)]
        public string StallCode { get; set; } = string.Empty;     // A-01

        [Column("Quantity")]
        public int Quantity { get; set; }

        [Column("MinStock")]
        public int MinStock { get; set; }

        [Column("UnitPrice", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("CreatedBy")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [Column("UpdatedBy")]
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        [NotMapped]
        public string StockStatus => StockStatuses.For(Quantity, MinStock);
    }

    /// <summary>
    /// One row per stock change (initial quantity, restock, sale, correction). Keeps a name snapshot so the
    /// history survives when the product itself is deleted (ProductId is then set to NULL).
    /// </summary>
    [Table("stock_movements")]
    public class StockMovement
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("ProductId")]
        public Guid? ProductId { get; set; }

        public Product? Product { get; set; }

        [Required]
        [Column("ProductCode")]
        [MaxLength(20)]
        public string ProductCode { get; set; } = string.Empty;

        [Required]
        [Column("ProductName")]
        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Column("MovementType")]
        [MaxLength(20)]
        public string MovementType { get; set; } = StockMovementTypes.Add;

        /// <summary>Quantity entered by the user (for CORRECTION: the new absolute quantity).</summary>
        [Column("Quantity")]
        public int Quantity { get; set; }

        [Column("QuantityBefore")]
        public int QuantityBefore { get; set; }

        [Column("QuantityAfter")]
        public int QuantityAfter { get; set; }

        [Column("MovementDate", TypeName = "date")]
        public DateTime MovementDate { get; set; }

        [Column("Remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("CreatedBy")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
    }

    public static class StockMovementTypes
    {
        public const string Initial = "INITIAL";      // quantity entered when the product is created
        public const string Add = "ADD";              // restock
        public const string Reduce = "REDUCE";        // sold / damaged
        public const string Correction = "CORRECTION"; // set to an exact counted quantity
        public const string Sale = "SALE";            // automatic deduction when a sale is recorded
        public const string SaleReversal = "SALE_REVERSAL"; // stock put back when a sale is edited or deleted

        public static readonly string[] UserSelectable = { Add, Reduce, Correction };

        /// <summary>Applies a movement to the current quantity; null when it would go below zero.</summary>
        public static int? Apply(string type, int current, int quantity) => type switch
        {
            Add => current + quantity,
            Reduce => current - quantity >= 0 ? current - quantity : null,
            Correction => quantity,
            _ => null
        };
    }

    public static class StockStatuses
    {
        public const string InStock = "IN_STOCK";
        public const string LowStock = "LOW_STOCK";
        public const string OutOfStock = "OUT_OF_STOCK";

        /// <summary>0 = out of stock; below the minimum = low; otherwise in stock.</summary>
        public static string For(int quantity, int minStock) =>
            quantity <= 0 ? OutOfStock : quantity < minStock ? LowStock : InStock;

        /// <summary>Alert urgency for low/out-of-stock items: under 20% of the minimum is critical.</summary>
        public static string Urgency(int quantity, int minStock) =>
            quantity <= 0 || (minStock > 0 && quantity * 5 < minStock) ? "CRITICAL" : "MODERATE";
    }
}
