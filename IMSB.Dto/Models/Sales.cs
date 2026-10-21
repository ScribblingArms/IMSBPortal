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
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("entry_no")]
        [MaxLength(20)]
        public string EntryNo { get; set; } = string.Empty;       // SAL-000001

        /// <summary>Local date and time of the sale (drives the daily totals and the hourly chart).</summary>
        [Column("sold_at")]
        public DateTime SoldAt { get; set; }

        [Column("total_amount", TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column("total_quantity")]
        public int TotalQuantity { get; set; }

        [Column("remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

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

        public List<SaleItem> Items { get; set; } = new();
    }

    /// <summary>A product line of a sales entry. Code/name/category/stall are snapshots so reports survive product edits and deletes.</summary>
    [Table("sale_items")]
    public class SaleItem
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("sales_entry_id")]
        public Guid SalesEntryId { get; set; }

        public SalesEntry? SalesEntry { get; set; }

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

        [Column("category_id")]
        public Guid? CategoryId { get; set; }

        [Column("category_name")]
        [MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [Required]
        [Column("stall_code")]
        [MaxLength(20)]
        public string StallCode { get; set; } = string.Empty;

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("unit_price", TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column("line_total", TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }
    }
}
