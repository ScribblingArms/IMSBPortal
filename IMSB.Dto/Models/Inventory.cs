using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("product_categories")]
    public class ProductCategory
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }

    [Table("products")]
    public class Product
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("product_code")]
        [MaxLength(20)]
        public string ProductCode { get; set; } = string.Empty;   // PRD001

        [Required]
        [Column("name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column("category_id")]
        public Guid CategoryId { get; set; }

        public ProductCategory? Category { get; set; }

        [Required]
        [Column("stall_code")]
        [MaxLength(20)]
        public string StallCode { get; set; } = string.Empty;     // A-01

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("min_stock")]
        public int MinStock { get; set; }

        [Column("unit_price", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("updated_by")]
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        [NotMapped]
        public string StockStatus => StockStatuses.For(Quantity, MinStock);
    }

    /// <summary>
    /// One row per stock change (initial quantity, restock, sale, correction). Keeps a name snapshot so the
    /// history survives when the product itself is deleted (product_id is then set to NULL).
    /// </summary>
    [Table("stock_movements")]
    public class StockMovement
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("product_id")]
        public Guid? ProductId { get; set; }

        public Product? Product { get; set; }

        [Required]
        [Column("product_code")]
        [MaxLength(20)]
        public string ProductCode { get; set; } = string.Empty;

        [Required]
        [Column("product_name")]
        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Column("movement_type")]
        [MaxLength(20)]
        public string MovementType { get; set; } = StockMovementTypes.Add;

        /// <summary>Quantity entered by the user (for CORRECTION: the new absolute quantity).</summary>
        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("quantity_before")]
        public int QuantityBefore { get; set; }

        [Column("quantity_after")]
        public int QuantityAfter { get; set; }

        [Column("movement_date", TypeName = "date")]
        public DateTime MovementDate { get; set; }

        [Column("remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
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
