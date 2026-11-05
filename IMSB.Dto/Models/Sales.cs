using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    /// <summary>
    /// One sales transaction (a submitted Daily Sales Entry). Saving it deducts the sold quantities from
    /// inventory; editing or deleting it puts the old quantities back first (see SalesController).
    /// </summary>
    [Table("sales_entries")]
    public class SalesEntry
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("EntryNo")]
        [MaxLength(20)]
        public string EntryNo { get; set; } = string.Empty;       // SAL-000001

        /// <summary>Local date and time of the sale (drives the daily totals and the hourly chart).</summary>
        [Column("SoldAt")]
        public DateTime SoldAt { get; set; }

        [Column("TotalAmount", TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column("TotalQuantity")]
        public int TotalQuantity { get; set; }

        [Column("Remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

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

        public List<SaleItem> Items { get; set; } = new();
    }

    /// <summary>A product line of a sales entry. Code/name/category/stall are snapshots so reports survive product edits and deletes.</summary>
    [Table("sale_items")]
    public class SaleItem
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("SalesEntryId")]
        public Guid SalesEntryId { get; set; }

        public SalesEntry? SalesEntry { get; set; }

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

        [Column("CategoryId")]
        public Guid? CategoryId { get; set; }

        [Column("CategoryName")]
        [MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [Required]
        [Column("StallCode")]
        [MaxLength(20)]
        public string StallCode { get; set; } = string.Empty;

        [Column("Quantity")]
        public int Quantity { get; set; }

        [Column("UnitPrice", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column("LineTotal", TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }
    }
}
