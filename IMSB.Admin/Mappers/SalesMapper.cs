using IMSB.Dto.Models;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Mappers
{
    public record SalesSummaryItem(string Date, decimal TotalSales, int ItemsSold, int Transactions, int ActiveStalls, int TotalStalls,
        int ActiveStallsPct, decimal PreviousDaySales, decimal? ChangeVsPreviousDay, decimal MonthSales, decimal? ChangeVsPreviousMonth,
        decimal AverageTransaction);

    public record SaleLineItem(string Code, string Name, string CategoryName, string StallCode, int Quantity, decimal UnitPrice, decimal LineTotal,
        bool ProductDeleted);

    public record SalesEntryItem(string EntryNo, string Date, string Time, decimal Total, int Quantity, string? Remarks, string? By,
        IEnumerable<SaleLineItem> Items);

    public record SaleProductOption(string Code, string Name, string CategoryName, string StallCode, decimal UnitPrice, int Quantity);

    public record DeductionLogItem(string Date, DateTime CreatedAt, string Code, string Name, string? StallCode, string Type, int Quantity,
        int Before, int After, string? EntryNo, string? By, string? Status, bool ProductDeleted);

    public static class SalesMapper
    {
        public static SalesSummaryItem ToItem(this SalesSummary s) => new(
            s.Date.ToString("yyyy-MM-dd"), s.TotalSales, s.ItemsSold, s.Transactions, s.ActiveStalls, s.TotalStalls,
            s.TotalStalls == 0 ? 0 : (int)Math.Round(s.ActiveStalls * 100.0 / s.TotalStalls),
            s.PreviousDaySales, s.ChangeVsPreviousDay, s.MonthSales, s.ChangeVsPreviousMonth, s.AverageTransaction);

        public static SalesEntryItem ToItem(this SalesEntry e) => new(
            e.EntryNo, e.SoldAt.ToString("yyyy-MM-dd"), e.SoldAt.ToString("HH:mm"), e.TotalAmount, e.TotalQuantity, e.Remarks,
            e.UpdatedBy ?? e.CreatedBy,
            e.Items.Select(i => new SaleLineItem(i.ProductCode, i.ProductName, i.CategoryName, i.StallCode, i.Quantity, i.UnitPrice, i.LineTotal,
                i.ProductId == null)).ToList());

        public static SaleProductOption ToSaleOption(this Product p) =>
            new(p.ProductCode, p.Name, p.Category?.Name ?? string.Empty, p.StallCode, p.UnitPrice, p.Quantity);

        /// <summary>A sale's stock movement; product is null once the product has been deleted.</summary>
        public static DeductionLogItem ToDeductionItem(this StockMovement m, Product? product) => new(
            m.MovementDate.ToString("yyyy-MM-dd"), Utc(m.CreatedAt), m.ProductCode, m.ProductName, product?.StallCode, m.MovementType,
            m.Quantity, m.QuantityBefore, m.QuantityAfter, m.Remarks, m.CreatedBy,
            product == null ? null : StockStatuses.For(m.QuantityAfter, product.MinStock), product == null);

        /// <summary>The sale line snapshot: code, name, category and stall are copied so history survives product edits.</summary>
        public static SaleItem ToSaleItem(this Product product, Guid salesEntryId, int quantity, decimal unitPrice) => new()
        {
            SalesEntryId = salesEntryId,
            ProductId = product.Id,
            ProductCode = product.ProductCode,
            ProductName = product.Name,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            StallCode = product.StallCode,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = quantity * unitPrice
        };
    }
}
