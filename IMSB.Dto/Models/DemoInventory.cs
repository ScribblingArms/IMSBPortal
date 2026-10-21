namespace IMSB.Dto.Models
{
    // Sample products from the prototype (IMSB_ADMIN_PANEL/pages/inventory.html), seeded in the
    // Development environment only, and only when the products table is empty (see Program.cs).
    public static class DemoInventory
    {
        private record Sample(string Name, string Category, string Stall, int Qty, int Min, decimal Price);

        private static readonly Sample[] Samples =
        {
            new("Kanjivaram Silk Saree", "Textiles", "A-01", 45, 20, 8500),
            new("Silk Dupatta (Blue)", "Textiles", "A-03", 3, 20, 1200),
            new("Handloom Saree", "Textiles", "A-07", 4, 30, 2800),
            new("Terracotta Flower Pot", "Pottery", "C-03", 5, 25, 450),
            new("Brass Temple Lamp", "Handicrafts", "B-04", 7, 40, 1800),
            new("Jasmine Essential Oil", "Herbal", "F-02", 8, 50, 320),
            new("Tanjore Painting (Small)", "Paintings", "D-09", 22, 10, 4500),
            new("Pearl Necklace Set", "Jewellery", "E-06", 18, 15, 6200),
            new("Madhubani Wall Art", "Paintings", "D-11", 15, 8, 3200),
            new("Chilli Murukku (500g)", "Food & Snacks", "F-05", 60, 40, 180),
            new("Bamboo Basket Set", "Handicrafts", "B-12", 35, 20, 750),
            new("Ruby Drop Earrings", "Jewellery", "E-03", 24, 10, 2400),
        };

        public static void SeedIfEmpty(IMSBContext context)
        {
            if (context.Products.Any()) return;
            var categories = context.ProductCategories.ToDictionary(c => c.Name, c => c.Id);
            var n = 0;
            foreach (var s in Samples)
            {
                if (!categories.TryGetValue(s.Category, out var categoryId)) continue;
                var product = new Product
                {
                    ProductCode = $"PRD{++n:D3}",
                    Name = s.Name,
                    CategoryId = categoryId,
                    StallCode = s.Stall,
                    Quantity = s.Qty,
                    MinStock = s.Min,
                    UnitPrice = s.Price,
                    CreatedBy = "seed"
                };
                context.Products.Add(product);
                context.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    ProductCode = product.ProductCode,
                    ProductName = product.Name,
                    MovementType = StockMovementTypes.Initial,
                    Quantity = s.Qty,
                    QuantityBefore = 0,
                    QuantityAfter = s.Qty,
                    MovementDate = DateTime.Today,
                    Remarks = "Sample data",
                    CreatedBy = "seed"
                });
            }
            context.SaveChanges();
        }
    }
}
