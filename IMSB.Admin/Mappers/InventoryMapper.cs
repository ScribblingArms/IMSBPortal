using IMSB.Dto.Models;

namespace IMSB.Admin.Mappers
{
    public record ProductListItem(string Code, string Name, string CategoryId, string CategoryName, string StallCode, int Quantity, int MinStock,
        decimal UnitPrice, string Status);

    public record LowStockItem(string Code, string Name, string CategoryName, string StallCode, int Quantity, int MinStock, string Status, string Urgency);

    public record ProductOption(string Code, string Name, string StallCode, int Quantity);

    public record MovementItem(string Date, string Code, string Name, string Type, int Quantity, int Before, int After, string? Remarks, string? By,
        bool ProductDeleted);

    public static class InventoryMapper
    {
        public static ProductListItem ToListItem(this Product p, string categoryName) => new(
            p.ProductCode, p.Name, p.CategoryId.ToString(), categoryName, p.StallCode, p.Quantity, p.MinStock, p.UnitPrice, p.StockStatus);

        public static LowStockItem ToLowStockItem(this Product p, string categoryName) => new(
            p.ProductCode, p.Name, categoryName, p.StallCode, p.Quantity, p.MinStock, p.StockStatus, StockStatuses.Urgency(p.Quantity, p.MinStock));

        public static ProductOption ToOption(this Product p) => new(p.ProductCode, p.Name, p.StallCode, p.Quantity);

        public static MovementItem ToItem(this StockMovement m) => new(
            m.MovementDate.ToString("yyyy-MM-dd"), m.ProductCode, m.ProductName, m.MovementType, m.Quantity, m.QuantityBefore, m.QuantityAfter,
            m.Remarks, m.CreatedBy, m.ProductId == null);

        /// <summary>A stock history row for a change to the product's quantity (before = its quantity now, after = the new one).</summary>
        public static StockMovement ToMovement(this Product product, string type, int quantity, int after, DateTime date, string? remarks, string? user) => new()
        {
            ProductId = product.Id,
            ProductCode = product.ProductCode,
            ProductName = product.Name,
            MovementType = type,
            Quantity = quantity,
            QuantityBefore = product.Quantity,
            QuantityAfter = after,
            MovementDate = date,
            Remarks = remarks,
            CreatedBy = user
        };
    }
}
